# Publishing checklist

## Steam Workshop

- Item: <https://steamcommunity.com/sharedfiles/filedetails/?id=3783134964>
- Title: `Building Placement Tweaks`
- Package type: `Paks`
- Version: `1.0.4`
- Description source: [`STEAM_DESCRIPTION.txt`](../STEAM_DESCRIPTION.txt)

Run the complete preparation and uploader workflow:

```bash
./scripts/prepare-workshop-upload.sh
```

The script removes generated working data, validates the committed release
artifacts, stages the release package, synchronizes it into the existing local
Workshop item `3783134964`, checks the local uploader environment, and opens
Pocketpair's Palworld Mod Uploader. Select the item, press `Reload` if needed,
verify the displayed metadata and use `Upload To Steam`.

The sync preserves `.workshop.json`, verifies its Published ID before writing,
and removes only the previous package's validated client and server filenames.

After changing the mod itself, build and commit the new version before running
the publishing workflow. This keeps the Steam package aligned with the GitHub
release and repository checksums.

Prepare and verify the package without opening the uploader:

```bash
./scripts/prepare-workshop-upload.sh --prepare-only
```

Suggested change note:

```text
Rebuilt the mod for Palworld 1.0.3 so the game's updated building costs are preserved.
```

## GitHub release

Stage the release assets:

```bash
./scripts/package-github.sh
```

Publish every file in `dist/BuildingPlacementTweaks-1.0.4-github/` as an
individual release asset for tag `v1.0.4`. Keep the filenames unchanged so
the links in the Steam description remain stable.

Repository: <https://github.com/mikaba-eu/palworld_building_placement_tweaks>

Linux release asset:
<https://github.com/mikaba-eu/palworld_building_placement_tweaks/releases/latest/download/BuildingPlacementTweaks_LinuxServer_P.pak>
