# Publishing checklist

## Steam Workshop

- Item: <https://steamcommunity.com/sharedfiles/filedetails/?id=3783134964>
- Title: `Building Placement Tweaks`
- Package type: `Paks`
- Version: `1.0.0`
- Description source: [`STEAM_DESCRIPTION.txt`](../STEAM_DESCRIPTION.txt)

Build and stage the package:

```bash
./scripts/build.sh
./scripts/package-workshop.sh
```

Open Pocketpair's Palworld Mod Uploader, reload Workshop item `3783134964`,
and upload `dist/BuildingPlacementTweaks-1.0.0/`.

Suggested change note:

```text
Initial release of Building Placement Tweaks with flexible placement across
supported structures and Base facilities, expanded building freedom, and any
number of Pal Expedition Stations in each Base.
```

## GitHub release

Stage the release assets:

```bash
./scripts/package-github.sh
```

Publish every file in `dist/BuildingPlacementTweaks-1.0.0-github/` as an
individual release asset for tag `v1.0.0`. Keep the filenames unchanged so
the links in the Steam description remain stable.

Repository: <https://github.com/mikaba-eu/palworld_building_placement_tweaks>

Linux release asset:
<https://github.com/mikaba-eu/palworld_building_placement_tweaks/releases/latest/download/BuildingPlacementTweaks_LinuxServer_P.pak>
