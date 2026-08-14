# Technical details

This document contains the implementation-facing values and package scope for
Building Placement Tweaks version 1.0.3. The player-facing overview is in the
repository [README](../README.md), and the complete structure list is in
[FEATURES.md](FEATURES.md).

## Build reference

- Palworld client build: `24467282`
- Palworld Windows Dedicated Server build: `24575149`
- Configured assets per platform: `209`
- Configured changes per platform: `218`
- Structures and facilities with overlapping placement: `206`

The overlap total consists of the 198 structures listed by category in
`FEATURES.md` and the eight Base facilities listed there separately.

## Fishing Pond placement

The Fishing Pond and Large Fishing Pond use the standard `Normal` install
strategy and allow overlapping placement.

## Global building values

| Setting | Configured value |
|---|---:|
| Maximum slope (`BuildSimulationLeanAngleMax`) | `90` degrees |
| Building capacity (`PlayerRecord_BuildingObjectMaxNum`) | `100000000` |
| Vertical range (`BuildingMaxZ`) | `100000000` units |
| Foundation floating allowance (`BuildSimulationFoundationFloatingAllowance`) | `100000000` units |

## Pal Expedition Station capacity

The `Expedition` row sets `InstallMaxNumInBaseCamp` to `100000000` in both
`DT_BuildObjectDataTable` and its `DT_BuildObjectDataTable_Common` parent. The
parent must be patched because the composite table rebuilds its runtime rows
from that source.

## Package targets

- Windows client: `Client/BuildingPlacementTweaks_Windows_P.pak`
- Windows Dedicated Server:
  `Server/BuildingPlacementTweaks_WindowsServer_P.pak`
- Linux Dedicated Server:
  `ServerLinux/BuildingPlacementTweaks_LinuxServer_P.pak`

Each platform package is built from its matching Palworld source PAK, unpacked
after packing, and verified against the configuration. Published checksums are
stored in `SHA256SUMS`.
