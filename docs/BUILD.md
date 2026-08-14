# Build and release guide

Building Placement Tweaks uses a configuration-driven .NET builder. It creates
separate PAKs from matching Windows client, Windows Dedicated Server, and Linux
Dedicated Server source data.

## Requirements

- Bash, `jq`, `curl`, `git`, `tar`, and `sha256sum`
- .NET SDK 10
- legally obtained Palworld source PAKs for each target platform
- a matching Palworld USMAP

Prepare the pinned open-source dependencies:

```bash
./scripts/setup-dependencies.sh
```

This checks out UAssetAPI commit
`33ef77e5a309062ea80b4a939f34ae8579c2d3bb` and installs repak `v0.2.3`
inside the ignored `.deps/` directory. The repak archive is verified with
SHA-256 before extraction.

## Build

Inside the Palworld modding workspace, the default paths resolve automatically:

```bash
./scripts/build.sh
```

For a standalone clone, provide the platform sources and mapping explicitly:

```bash
./scripts/build.sh \
  --windows-client-pak /path/to/Pal-Windows.pak \
  --windows-server-pak /path/to/Pal-WindowsServer.pak \
  --linux-server-pak /path/to/Pal-LinuxServer.pak \
  --mapping /path/to/Pal.usmap
```

`--repak`, `--dotnet`, and `--uassetapi-project` can select custom tool paths.
The matching environment variables are `REPAK_BIN`, `DOTNET_BIN`, and
`UASSETAPI_PROJECT`.

## Outputs

- `Client/BuildingPlacementTweaks_Windows_P.pak`
- `Server/BuildingPlacementTweaks_WindowsServer_P.pak`
- `ServerLinux/BuildingPlacementTweaks_LinuxServer_P.pak`
- `SHA256SUMS`
- build and verification reports under `build/audit/`

Every build extracts only the configured source assets, checks the source
round trip, applies the configured values, repacks each platform separately,
extracts each finished PAK, verifies every configured value, and regenerates
the checksum manifest.

## Validation and packaging

```bash
./scripts/validate-release.sh
./scripts/package-workshop.sh
./scripts/package-github.sh
```

The Workshop package is written to `dist/BuildingPlacementTweaks-1.0.3/`.
The GitHub release assets are staged in
`dist/BuildingPlacementTweaks-1.0.3-github/`. Publish each file in that
directory as an individual release asset. This keeps the direct Linux PAK URL
stable.

Remove generated work directories with:

```bash
./scripts/clean.sh
```

Add `--all` to remove the downloaded `.deps/` directory as well.
