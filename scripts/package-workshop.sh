#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "$script_dir/.." && pwd)"
workspace_dir="$(cd -- "$repo_dir/../.." && pwd)"
version="$(jq -r '.Version' "$repo_dir/Info.json")"
package_dir="$repo_dir/dist/BuildingPlacementTweaks-$version"

"$script_dir/validate-release.sh"
if [[ -x "$workspace_dir/tools/validate_mod.py" ]]; then
    "$workspace_dir/tools/validate_mod.py" --release "$repo_dir"
fi

for required_file in \
    "$repo_dir/Info.json" \
    "$repo_dir/thumbnail.png" \
    "$repo_dir/Client/BuildingPlacementTweaks_Windows_P.pak" \
    "$repo_dir/Server/BuildingPlacementTweaks_WindowsServer_P.pak"
do
    if [[ ! -f "$required_file" ]]; then
        printf 'Required file is missing: %s\n' "$required_file" >&2
        exit 1
    fi
done

case "$package_dir" in
    "$repo_dir"/dist/BuildingPlacementTweaks-*) ;;
    *)
        printf 'Refusing unsafe package directory: %s\n' "$package_dir" >&2
        exit 1
        ;;
esac

mkdir -p "$package_dir"
find "$package_dir" -mindepth 1 -depth -delete
install -Dm0644 "$repo_dir/Info.json" "$package_dir/Info.json"
install -Dm0644 "$repo_dir/thumbnail.png" "$package_dir/thumbnail.png"
install -Dm0644 \
    "$repo_dir/Client/BuildingPlacementTweaks_Windows_P.pak" \
    "$package_dir/Client/BuildingPlacementTweaks_Windows_P.pak"
install -Dm0644 \
    "$repo_dir/Server/BuildingPlacementTweaks_WindowsServer_P.pak" \
    "$package_dir/Server/BuildingPlacementTweaks_WindowsServer_P.pak"

if find "$package_dir" -type l -print -quit | grep -q .; then
    printf 'Workshop package contains a symbolic link.\n' >&2
    exit 1
fi

printf 'Workshop package: %s\n' "$package_dir"
find "$package_dir" -type f -printf '%P\n' | sort
