#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "$script_dir/.." && pwd)"
remove_dependencies=false

case "${1:-}" in
    '') ;;
    --all) remove_dependencies=true ;;
    *)
        printf 'Usage: ./scripts/clean.sh [--all]\n' >&2
        exit 2
        ;;
esac

for target in \
    "$repo_dir/build" \
    "$repo_dir/dist" \
    "$repo_dir/src/PlacementBuilder/bin" \
    "$repo_dir/src/PlacementBuilder/obj"
do
    case "$target" in
        "$repo_dir"/build|"$repo_dir"/dist|\
        "$repo_dir"/src/PlacementBuilder/bin|"$repo_dir"/src/PlacementBuilder/obj) ;;
        *)
            printf 'Refusing unsafe clean target: %s\n' "$target" >&2
            exit 1
            ;;
    esac
    if [[ -d "$target" ]]; then
        find "$target" -mindepth 1 -depth -delete
        rmdir "$target"
    fi
done

if [[ "$remove_dependencies" == true && -d "$repo_dir/.deps" ]]; then
    case "$repo_dir/.deps" in
        "$repo_dir"/.deps)
            find "$repo_dir/.deps" -mindepth 1 -depth -delete
            rmdir "$repo_dir/.deps"
            ;;
    esac
fi

printf 'Removed generated build directories.\n'
