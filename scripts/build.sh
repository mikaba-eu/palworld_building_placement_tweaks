#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "$script_dir/.." && pwd)"
workspace_dir="$(cd -- "$repo_dir/../.." && pwd)"
config_path="$repo_dir/config/placement-config.jsonc"
builder_project="$repo_dir/src/PlacementBuilder/PlacementBuilder.csproj"
work_dir="$repo_dir/build/work"

windows_client_pak="$workspace_dir/client/Palworld/Pal/Content/Paks/Pal-Windows.pak"
windows_server_pak="$workspace_dir/pal_data/server_bins/build-24466863/windows/Pal-WindowsServer.pak"
linux_server_pak="$workspace_dir/pal_data/server_bins/Pal-LinuxServer.pak"
mapping_path="$workspace_dir/pal_data/mappings/2026-08-02/Pal-5.1.1-0+++UE5+Release-5.1-c838a8ac.usmap"
repak_bin="${REPAK_BIN:-}"
dotnet_bin="${DOTNET_BIN:-}"
uasset_api_project="${UASSETAPI_PROJECT:-}"

usage() {
    printf '%s\n' \
        'usage: ./scripts/build.sh [options]' \
        '' \
        'Game data:' \
        '  --windows-client-pak PATH' \
        '  --windows-server-pak PATH' \
        '  --linux-server-pak PATH' \
        '  --mapping PATH' \
        '' \
        'Build tools:' \
        '  --repak PATH' \
        '  --dotnet PATH' \
        '  --uassetapi-project PATH'
}

require_option_value() {
    if (( $# < 2 )); then
        printf 'Option requires a value: %s\n' "$1" >&2
        exit 2
    fi
}

while (( $# > 0 )); do
    case "$1" in
        --windows-client-pak|--windows-pak)
            require_option_value "$@"
            windows_client_pak="$2"
            shift 2
            ;;
        --windows-server-pak)
            require_option_value "$@"
            windows_server_pak="$2"
            shift 2
            ;;
        --linux-server-pak|--linux-pak)
            require_option_value "$@"
            linux_server_pak="$2"
            shift 2
            ;;
        --mapping)
            require_option_value "$@"
            mapping_path="$2"
            shift 2
            ;;
        --repak)
            require_option_value "$@"
            repak_bin="$2"
            shift 2
            ;;
        --dotnet)
            require_option_value "$@"
            dotnet_bin="$2"
            shift 2
            ;;
        --uassetapi-project)
            require_option_value "$@"
            uasset_api_project="$2"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            printf 'Unknown argument: %s\n' "$1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

if [[ -z "$repak_bin" ]]; then
    for candidate in \
        "$repo_dir/.deps/repak/repak" \
        "$workspace_dir/tools/vendor/repak-v0.2.3/repak_cli-x86_64-unknown-linux-gnu/repak"
    do
        if [[ -x "$candidate" ]]; then
            repak_bin="$candidate"
            break
        fi
    done
fi
if [[ -z "$repak_bin" ]]; then
    repak_bin="$(command -v repak 2>/dev/null || true)"
fi

if [[ -z "$dotnet_bin" ]]; then
    if [[ -x "$workspace_dir/tools/toolchains/dotnet-10/dotnet" ]]; then
        dotnet_bin="$workspace_dir/tools/toolchains/dotnet-10/dotnet"
    else
        dotnet_bin="$(command -v dotnet 2>/dev/null || true)"
    fi
fi

if [[ -z "$uasset_api_project" ]]; then
    for candidate in \
        "$repo_dir/.deps/UAssetAPI/UAssetAPI/UAssetAPI.csproj" \
        "$workspace_dir/tools/vendor/UAssetAPI-33ef77e/UAssetAPI/UAssetAPI.csproj"
    do
        if [[ -f "$candidate" ]]; then
            uasset_api_project="$candidate"
            break
        fi
    done
fi

for required_file in \
    "$config_path" \
    "$windows_client_pak" \
    "$windows_server_pak" \
    "$linux_server_pak" \
    "$mapping_path" \
    "$repak_bin" \
    "$dotnet_bin" \
    "$uasset_api_project"
do
    if [[ -z "$required_file" || ! -f "$required_file" ]]; then
        printf 'Required file is missing: %s\n' "${required_file:-<not configured>}" >&2
        exit 1
    fi
done

case "$work_dir" in
    "$repo_dir"/build/work) ;;
    *)
        printf 'Refusing unsafe working directory: %s\n' "$work_dir" >&2
        exit 1
        ;;
esac

mkdir -p "$work_dir"
find "$work_dir" -mindepth 1 -depth -delete
mkdir -p \
    "$work_dir/windows-client-source" \
    "$work_dir/windows-server-source" \
    "$work_dir/linux-server-source" \
    "$work_dir/windows-client-patched" \
    "$work_dir/windows-server-patched" \
    "$work_dir/linux-server-patched" \
    "$repo_dir/Client" \
    "$repo_dir/Server" \
    "$repo_dir/ServerLinux" \
    "$repo_dir/build/audit"
find "$repo_dir/build/audit" -mindepth 1 -maxdepth 1 -type f -delete

if [[ "$dotnet_bin" == "$workspace_dir/tools/toolchains/dotnet-10/dotnet" ]]; then
    export DOTNET_ROOT="$(dirname -- "$dotnet_bin")"
fi

uasset_api_property="-p:UAssetApiProject=$uasset_api_project"
"$dotnet_bin" build "$builder_project" -c Release --nologo "$uasset_api_property"

mapfile -t assets < <(
    "$dotnet_bin" run --project "$builder_project" -c Release --no-build \
        "$uasset_api_property" -- list-assets "$config_path"
)
mapfile -t source_assets < <(
    "$dotnet_bin" run --project "$builder_project" -c Release --no-build \
        "$uasset_api_property" -- list-source-assets "$config_path"
)
if (( ${#assets[@]} == 0 )); then
    printf '%s\n' 'Configuration contains no enabled assets.' >&2
    exit 1
fi

include_args=()
for asset in "${source_assets[@]}"; do
    include_args+=("-i" "${asset}.uasset" "-i" "${asset}.uexp")
done

"$repak_bin" unpack -q "$windows_client_pak" -o "$work_dir/windows-client-source" "${include_args[@]}"
"$repak_bin" unpack -q "$windows_server_pak" -o "$work_dir/windows-server-source" "${include_args[@]}"
"$repak_bin" unpack -q "$linux_server_pak" -o "$work_dir/linux-server-source" "${include_args[@]}"

expected_files=$(( ${#source_assets[@]} * 2 ))
for source_dir in \
    "$work_dir/windows-client-source" \
    "$work_dir/windows-server-source" \
    "$work_dir/linux-server-source"
do
    actual_files="$(find "$source_dir" -type f | wc -l)"
    if [[ "$actual_files" -ne "$expected_files" ]]; then
        printf 'Incomplete extraction: found %s files in %s, expected %s.\n' \
            "$actual_files" "$source_dir" "$expected_files" >&2
        exit 1
    fi
done

run_builder() {
    "$dotnet_bin" run --project "$builder_project" -c Release --no-build \
        "$uasset_api_property" -- "$@"
}

run_builder patch "$config_path" "$work_dir/windows-client-source" \
    "$work_dir/windows-client-patched" "$mapping_path" windows-client \
    "$repo_dir/build/audit/windows-client-build.json"
run_builder patch "$config_path" "$work_dir/windows-server-source" \
    "$work_dir/windows-server-patched" "$mapping_path" windows-server \
    "$repo_dir/build/audit/windows-server-build.json"
run_builder patch "$config_path" "$work_dir/linux-server-source" \
    "$work_dir/linux-server-patched" "$mapping_path" linux-server \
    "$repo_dir/build/audit/linux-server-build.json"

"$repak_bin" pack -q --version V11 --compression Oodle --path-hash-seed 373574182 \
    "$work_dir/windows-client-patched" "$repo_dir/Client/BuildingPlacementTweaks_Windows_P.pak"
"$repak_bin" pack -q --version V11 --compression Oodle --path-hash-seed 373574182 \
    "$work_dir/windows-server-patched" "$repo_dir/Server/BuildingPlacementTweaks_WindowsServer_P.pak"
"$repak_bin" pack -q --version V11 --compression Oodle --path-hash-seed 373574182 \
    "$work_dir/linux-server-patched" "$repo_dir/ServerLinux/BuildingPlacementTweaks_LinuxServer_P.pak"

mkdir -p \
    "$work_dir/windows-client-verify" \
    "$work_dir/windows-server-verify" \
    "$work_dir/linux-server-verify"
"$repak_bin" unpack -q "$repo_dir/Client/BuildingPlacementTweaks_Windows_P.pak" \
    -o "$work_dir/windows-client-verify"
"$repak_bin" unpack -q "$repo_dir/Server/BuildingPlacementTweaks_WindowsServer_P.pak" \
    -o "$work_dir/windows-server-verify"
"$repak_bin" unpack -q "$repo_dir/ServerLinux/BuildingPlacementTweaks_LinuxServer_P.pak" \
    -o "$work_dir/linux-server-verify"

run_builder verify "$config_path" "$work_dir/windows-client-verify" \
    "$work_dir/windows-client-source" "$mapping_path" windows-client \
    "$repo_dir/build/audit/windows-client-verify.json"
run_builder verify "$config_path" "$work_dir/windows-server-verify" \
    "$work_dir/windows-server-source" "$mapping_path" windows-server \
    "$repo_dir/build/audit/windows-server-verify.json"
run_builder verify "$config_path" "$work_dir/linux-server-verify" \
    "$work_dir/linux-server-source" "$mapping_path" linux-server \
    "$repo_dir/build/audit/linux-server-verify.json"

(
    cd "$repo_dir"
    sha256sum \
        Client/BuildingPlacementTweaks_Windows_P.pak \
        Server/BuildingPlacementTweaks_WindowsServer_P.pak \
        ServerLinux/BuildingPlacementTweaks_LinuxServer_P.pak \
        > SHA256SUMS
)

printf 'Completed: %s assets per platform.\n' "${#assets[@]}"
printf '%s\n' \
    '  Client/BuildingPlacementTweaks_Windows_P.pak' \
    '  Server/BuildingPlacementTweaks_WindowsServer_P.pak' \
    '  ServerLinux/BuildingPlacementTweaks_LinuxServer_P.pak'
