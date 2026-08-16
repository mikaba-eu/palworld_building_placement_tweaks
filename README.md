# Building Placement Tweaks

![Building Placement Tweaks Workshop card](thumbnail.png)

Building Placement Tweaks gives Base builders more room to create. Combine
supported pieces more closely, adapt builds to steep or uneven terrain, reach
new heights, and realize larger, more ambitious layouts.

## Highlights

- Place supported structures, decorations, lights, and workstations closer
  together or overlap them for more detailed builds.
- Shape creative builds on steep slopes and uneven ground.
- Expand upward and downward, including elevated and floating sections.
- Grow larger Bases with greatly expanded construction capacity.
- Place Breeding Farms, Ranches, Fishing Ponds, Palboxes, Item Retrieval
  Machines, Pal Essence Condensers, and Pal Expedition Stations more flexibly.
- Continue building throughout the area around the Palbox.
- Place as many Pal Expedition Stations in each Base as you want.

See [docs/FEATURES.md](docs/FEATURES.md) for the complete list of supported
structures.

## Downloads

- [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3783134964)
- [Windows client PAK](Client/BuildingPlacementTweaks_Windows_P.pak)
- [Windows Dedicated Server PAK](Server/BuildingPlacementTweaks_WindowsServer_P.pak)
- [Linux Dedicated Server PAK](ServerLinux/BuildingPlacementTweaks_LinuxServer_P.pak)
- [Latest Linux Dedicated Server release](https://github.com/mikaba-eu/palworld_building_placement_tweaks/releases/latest/download/BuildingPlacementTweaks_LinuxServer_P.pak)

## Installation

### Steam Workshop

Subscribe on Steam, enable **Building Placement Tweaks** under
**Options -> Mod Management**, and restart Palworld.

### Dedicated servers

Use the PAK that matches the server operating system. Stop the server before
replacing the file, then install it at the following path relative to the
Palworld Dedicated Server root:

| Server | PAK | Installation path |
|---|---|---|
| Windows | `BuildingPlacementTweaks_WindowsServer_P.pak` | `Pal\Content\Paks\~WorkshopMods\BuildingPlacementTweaks\BuildingPlacementTweaks_WindowsServer_P.pak` |
| Linux | `BuildingPlacementTweaks_LinuxServer_P.pak` | `Pal/Content/Paks/~WorkshopMods/BuildingPlacementTweaks/BuildingPlacementTweaks_LinuxServer_P.pak` |

Create the `~WorkshopMods/BuildingPlacementTweaks` directory if it does not
exist. Make sure that no older copy of Building Placement Tweaks remains in a
different active PAK directory, then fully restart the server.

Palworld's official server-side Workshop loader currently supports only the
Windows Dedicated Server. The Linux server PAK is installed directly using the
path above.

Every participating client, co-op host, and dedicated server must use the same
Building Placement Tweaks release. Do not mix the Windows client, Windows
server, or Linux server PAKs. Clients using Steam Workshop must enable
**Building Placement Tweaks** under **Options -> Mod Management** and fully
restart Palworld after installing or updating it.

## Repository

- [Source repository](https://github.com/mikaba-eu/palworld_building_placement_tweaks)
- [Build and release guide](docs/BUILD.md)
- [One-command Workshop publishing](docs/PUBLISHING.md#steam-workshop)
- [Steam Workshop description](STEAM_DESCRIPTION.txt)
- [Publishing checklist](docs/PUBLISHING.md)
- [Technical details](docs/TECHNICAL_DETAILS.md)
- [Version history](CHANGELOG.md)
