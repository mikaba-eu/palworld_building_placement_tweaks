#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "$script_dir/.." && pwd)"
workspace_dir="$(cd -- "$repo_dir/../.." && pwd)"
uploader_launcher="$workspace_dir/tools/open_palworld_mod_uploader.sh"
workshop_item_id='3783134964'
steam_root="$HOME/.local/share/Steam"
workshop_content_dir="$steam_root/steamapps/workshop/content/1623730"
workshop_item_dir="$workshop_content_dir/$workshop_item_id"
workshop_metadata="$workshop_item_dir/.workshop.json"
prepare_only=false

usage() {
    printf '%s\n' \
        'usage: ./scripts/prepare-workshop-upload.sh [--prepare-only]' \
        '' \
        'Validates Building Placement Tweaks, stages the Steam' \
        'Workshop package, and opens the Palworld Mod Uploader.' \
        '' \
        'Options:' \
        '  --prepare-only  Prepare the package without opening the uploader.' \
        '  -h, --help      Show this help.'
}

while (( $# > 0 )); do
    case "$1" in
        --prepare-only)
            prepare_only=true
            shift
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

if [[ ! -x "$uploader_launcher" ]]; then
    printf 'Palworld Mod Uploader launcher is missing or not executable: %s\n' \
        "$uploader_launcher" >&2
    exit 1
fi

printf '%s\n' 'Removing generated data from earlier build and uploader runs...'
"$script_dir/clean.sh"

printf '%s\n' 'Validating and staging the Steam Workshop package...'
"$script_dir/package-workshop.sh"

version="$(jq -er '.Version' "$repo_dir/Info.json")"
package_dir="$repo_dir/dist/BuildingPlacementTweaks-$version"

case "$workshop_item_dir" in
    "$steam_root"/steamapps/workshop/content/1623730/3783134964) ;;
    *)
        printf 'Refusing unsafe Workshop item directory: %s\n' \
            "$workshop_item_dir" >&2
        exit 1
        ;;
esac

if pgrep -f 'PalworldModUploader\.exe' >/dev/null 2>&1; then
    printf '%s\n' \
        'Palworld Mod Uploader is running. Close it before preparing the item.' >&2
    exit 1
fi

if [[ ! -f "$workshop_metadata" ]] || \
    ! jq -e --arg item "$workshop_item_id" \
        '(.publishedfileid | tostring) == $item' \
        "$workshop_metadata" >/dev/null
then
    printf 'Workshop metadata does not identify item %s: %s\n' \
        "$workshop_item_id" "$workshop_metadata" >&2
    exit 1
fi

if find "$workshop_item_dir" -type l -print -quit | grep -q .; then
    printf 'Workshop item directory contains a symbolic link: %s\n' \
        "$workshop_item_dir" >&2
    exit 1
fi

printf 'Updating Workshop item %s...\n' "$workshop_item_id"
current_package_name="$(jq -er '.PackageName' "$workshop_item_dir/Info.json")"
new_package_name="$(jq -er '.PackageName' "$package_dir/Info.json")"
for package_name in "$current_package_name" "$new_package_name"; do
    if [[ ! "$package_name" =~ ^[A-Za-z0-9]+$ ]]; then
        printf 'Unsafe package name in Workshop metadata: %s\n' \
            "$package_name" >&2
        exit 1
    fi
done

if [[ "$current_package_name" != "$new_package_name" ]]; then
    for previous_file in \
        "$workshop_item_dir/Client/${current_package_name}_Windows_P.pak" \
        "$workshop_item_dir/Server/${current_package_name}_WindowsServer_P.pak"
    do
        if [[ -f "$previous_file" ]]; then
            printf 'Removing previous package file: %s\n' \
                "${previous_file#"$workshop_item_dir/"}"
            rm -f -- "$previous_file"
        fi
    done
fi

for relative_path in \
    Info.json \
    thumbnail.png \
    Client/BuildingPlacementTweaks_Windows_P.pak \
    Server/BuildingPlacementTweaks_WindowsServer_P.pak
do
    install -Dm0644 \
        "$package_dir/$relative_path" \
        "$workshop_item_dir/$relative_path"
    if ! cmp -s \
        "$package_dir/$relative_path" \
        "$workshop_item_dir/$relative_path"
    then
        printf 'Workshop file verification failed: %s\n' \
            "$relative_path" >&2
        exit 1
    fi
done

mapfile -t unexpected_paks < <(
    find "$workshop_item_dir/Client" "$workshop_item_dir/Server" \
        -maxdepth 1 -type f -name '*.pak' \
        ! -name "${new_package_name}_Windows_P.pak" \
        ! -name "${new_package_name}_WindowsServer_P.pak" \
        -printf '%P\n'
)
if (( ${#unexpected_paks[@]} > 0 )); then
    printf 'Unexpected PAK file remains in Workshop item %s:\n' \
        "$workshop_item_id" >&2
    printf '  %s\n' "${unexpected_paks[@]}" >&2
    exit 1
fi

printf '\nWorkshop item: %s\n' "$workshop_item_id"
printf 'Upload folder: %s\n' "$package_dir"
printf 'Uploader content folder: %s\n' "$workshop_item_dir"
printf 'Description: %s\n' "$repo_dir/STEAM_DESCRIPTION.txt"

if [[ "$prepare_only" == true ]]; then
    printf '%s\n' 'Package preparation completed; uploader launch skipped.'
    exit 0
fi

printf '%s\n' 'Opening the Palworld Mod Uploader...'
exec "$uploader_launcher"
