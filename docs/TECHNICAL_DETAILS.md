# Technical details

This document contains the implementation-facing values and package scope for
Building Placement Tweaks version 1.0.0. The player-facing overview is in the
repository [README](../README.md), and the complete structure list is in
[FEATURES.md](FEATURES.md).

## Build reference

- Palworld client build: `24467282`
- Configured assets per platform: `205`
- Configured changes per platform: `212`
- Structures and facilities with overlapping placement: `203`

The overlap total consists of the 197 structures listed by category in
`FEATURES.md` and the six Base facilities listed there separately.

## Global building values

| Setting | Configured value |
|---|---:|
| Maximum slope (`BuildSimulationLeanAngleMax`) | `90` degrees |
| Building capacity (`PlayerRecord_BuildingObjectMaxNum`) | `100000000` |
| Vertical range (`BuildingMaxZ`) | `100000000` units |
| Foundation floating allowance (`BuildSimulationFoundationFloatingAllowance`) | `100000000` units |

## Pal Expedition Station capacity

The `Expedition` row in the building data sets
`InstallMaxNumInBaseCamp` to `0`. Palworld uses this value for structures that
have no per-Base maximum.

## Package targets

- Windows client: `Client/BuildingPlacementTweaks_Windows_P.pak`
- Windows Dedicated Server:
  `Server/BuildingPlacementTweaks_WindowsServer_P.pak`
- Linux Dedicated Server:
  `ServerLinux/BuildingPlacementTweaks_LinuxServer_P.pak`

Each platform package is built from its matching Palworld source PAK, unpacked
after packing, and verified against the configuration. Published checksums are
stored in `SHA256SUMS`.
