#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "$script_dir/.." && pwd)"
version="$(jq -r '.Version' "$repo_dir/Info.json")"
package_dir="$repo_dir/dist/BuildingPlacementTweaks-$version-github"

"$script_dir/validate-release.sh"

for required_file in \
    "$repo_dir/Client/BuildingPlacementTweaks_Windows_P.pak" \
    "$repo_dir/Server/BuildingPlacementTweaks_WindowsServer_P.pak" \
    "$repo_dir/ServerLinux/BuildingPlacementTweaks_LinuxServer_P.pak"
do
    if [[ ! -f "$required_file" ]]; then
        printf 'Required file is missing: %s\n' "$required_file" >&2
        exit 1
    fi
done

case "$package_dir" in
    "$repo_dir"/dist/BuildingPlacementTweaks-*-github) ;;
    *)
        printf 'Refusing unsafe package directory: %s\n' "$package_dir" >&2
        exit 1
        ;;
esac

mkdir -p "$package_dir"
find "$package_dir" -mindepth 1 -depth -delete
install -m0644 "$repo_dir/Client/BuildingPlacementTweaks_Windows_P.pak" "$package_dir/"
install -m0644 "$repo_dir/Server/BuildingPlacementTweaks_WindowsServer_P.pak" "$package_dir/"
install -m0644 "$repo_dir/ServerLinux/BuildingPlacementTweaks_LinuxServer_P.pak" "$package_dir/"
(
    cd "$package_dir"
    sha256sum ./*.pak > SHA256SUMS
)

if find "$package_dir" -type l -print -quit | grep -q .; then
    printf 'GitHub release package contains a symbolic link.\n' >&2
    exit 1
fi

printf 'GitHub release assets: %s\n' "$package_dir"
find "$package_dir" -type f -printf '%P\n' | sort
