#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "$script_dir/.." && pwd)"

jq --exit-status '
    .ModName == "Building Placement Tweaks"
    and .PackageName == "BuildingPlacementTweaks"
    and .Version == "1.0.2"
    and .Thumbnail == "thumbnail.png"
' "$repo_dir/Info.json" >/dev/null
jq empty "$repo_dir/config/placement-config.jsonc"

for required_file in \
    "$repo_dir/README.md" \
    "$repo_dir/STEAM_DESCRIPTION.txt" \
    "$repo_dir/docs/FEATURES.md" \
    "$repo_dir/docs/TECHNICAL_DETAILS.md" \
    "$repo_dir/docs/BUILD.md" \
    "$repo_dir/docs/PUBLISHING.md"
do
    if [[ ! -f "$required_file" ]]; then
        printf 'Required repository file is missing: %s\n' "$required_file" >&2
        exit 1
    fi
done

for script in \
    "$script_dir/build.sh" \
    "$script_dir/clean.sh" \
    "$script_dir/package-github.sh" \
    "$script_dir/package-workshop.sh" \
    "$script_dir/setup-dependencies.sh" \
    "$script_dir/validate-release.sh"
do
    bash -n "$script"
done

(
    cd "$repo_dir"
    sha256sum --check SHA256SUMS
)

if find "$repo_dir" -path "$repo_dir/.git" -prune -o -type l -print -quit | grep -q .; then
    printf 'Repository contains a symbolic link.\n' >&2
    exit 1
fi

legacy_package_name='Unified''PlacementTweaks'
legacy_display_name='Unified Placement'' Tweaks'
if grep -R -n --exclude-dir=.git --exclude-dir=.deps --exclude-dir=build \
    --exclude-dir=dist --exclude='*.pak' --exclude='thumbnail.png' \
    "$legacy_package_name\|$legacy_display_name" "$repo_dir"; then
    printf 'Legacy project name found.\n' >&2
    exit 1
fi

automation_provenance=$'\x43\x6f\x64\x65\x78|\x4f\x70\x65\x6e\x41\x49|\x43\x68\x61\x74\x47\x50\x54|\x61\x72\x74\x69\x66\x69\x63\x69\x61\x6c\x20\x69\x6e\x74\x65\x6c\x6c\x69\x67\x65\x6e\x63\x65|\x69\x6d\x61\x67\x65.?\x67\x65\x6e|\x67\x65\x6e\x65\x72\x61\x74\x69\x6f\x6e\x20\x70\x72\x6f\x6d\x70\x74|\x67\x65\x6e\x65\x72\x61\x74\x65\x64\x20\x77\x69\x74\x68'
if grep -R -E -i -n --exclude-dir=.git --exclude-dir=.deps --exclude-dir=build \
    --exclude-dir=dist --exclude='*.pak' --exclude='thumbnail.png' \
    "$automation_provenance" "$repo_dir"; then
    printf 'Automation provenance found in repository content.\n' >&2
    exit 1
fi

github_placeholder='GITHUB_''OWNER'
if grep -R -n --exclude-dir=.git --exclude-dir=.deps --exclude-dir=build \
    --exclude-dir=dist --exclude='*.pak' --exclude='thumbnail.png' \
    "$github_placeholder" "$repo_dir"; then
    printf 'Replace the GitHub URL placeholder before publishing.\n' >&2
    exit 1
fi

printf 'Release validation passed.\n'
