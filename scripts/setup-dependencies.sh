#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "$script_dir/.." && pwd)"
deps_dir="$repo_dir/.deps"
uasset_dir="$deps_dir/UAssetAPI"
repak_dir="$deps_dir/repak"
uasset_commit='33ef77e5a309062ea80b4a939f34ae8579c2d3bb'
repak_url='https://github.com/trumank/repak/releases/download/v0.2.3/repak_cli-x86_64-unknown-linux-gnu.tar.xz'
repak_sha256='933bdb8e26f34e8fd70ea50201efca39df041de58aa83b1cd6eb83da124a2046'

for command_name in git curl tar sha256sum; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        printf 'Required command is missing: %s\n' "$command_name" >&2
        exit 1
    fi
done

mkdir -p "$deps_dir"

if [[ ! -d "$uasset_dir/.git" ]]; then
    git clone --filter=blob:none https://github.com/atenfyr/UAssetAPI.git "$uasset_dir"
fi
git -C "$uasset_dir" fetch --filter=blob:none origin "$uasset_commit"
git -C "$uasset_dir" checkout --detach "$uasset_commit"
if [[ "$(git -C "$uasset_dir" rev-parse HEAD)" != "$uasset_commit" ]]; then
    printf 'UAssetAPI commit verification failed.\n' >&2
    exit 1
fi

archive="$(mktemp "$deps_dir/repak-v0.2.3.XXXXXX.tar.xz")"
trap 'rm -f -- "$archive"' EXIT
curl --fail --location --silent --show-error "$repak_url" --output "$archive"
printf '%s  %s\n' "$repak_sha256" "$archive" | sha256sum --check --status

mkdir -p "$repak_dir"
find "$repak_dir" -mindepth 1 -depth -delete
tar -xJf "$archive" -C "$repak_dir" --strip-components=1
chmod 0755 "$repak_dir/repak"

printf 'UAssetAPI: %s\n' "$uasset_commit"
printf 'repak: %s\n' "$repak_dir/repak"
