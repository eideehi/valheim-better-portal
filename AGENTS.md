# Build Notes

## Project layout
- Solution: `BetterPortal.sln`
- Project: `BetterPortal/BetterPortal.csproj`
- Target framework: `.NET Framework 4.8`

## Repository notes
- `BetterPortal/Libraries/mod-utils` is a read-only library dependency. Do not edit files under this directory unless the task is explicitly to update the submodule.
- When preparing a release, create the Git tag that matches the release/package version so versioned README and Thunderstore links resolve correctly.
- Every release commit must update `distributor/thunderstore/manifest.json`: set `version_number` to the release version, and set the `denikson-BepInExPack_Valheim-<version>` dependency to the BepInExPack Valheim version the release was verified with. Never carry the previous release's dependency version forward unchecked. Read the current version from the `User is running BepInExPack Valheim version <version>` line in `<Valheim>/BepInEx/LogOutput.log`. `scripts/check-version.sh` does not check this dependency.

## Required local dependencies
- .NET SDK 8.0 or newer
- A local Valheim installation
- BepInEx installed inside the Valheim directory

The project resolves game references from the Valheim install directory. Set the following environment variable before building:

- `VALHEIM_DIR`: absolute path to the Valheim root directory that contains `valheim_Data/Managed`

The expected directory shape is:

- `<Valheim>/valheim_Data/Managed`
- `<Valheim>/BepInEx/core`

## Build commands
Use `scripts/build.sh` for normal local builds. It validates `VALHEIM_DIR` and BepInEx core files, passes `FrameworkPathOverride`, and runs `dotnet msbuild`.

Debug build:

```bash
scripts/build.sh
```

Release build:

```bash
scripts/build.sh Release
```

Clean before building:

```bash
scripts/build.sh Debug clean
```

Version consistency check:

```bash
scripts/check-version.sh
```

## Transpiler IL check
`tools/il-verify` is a standalone .NET 8 console tool that reads the installed `assembly_valheim.dll` with Mono.Cecil and asserts that the IL shapes the `Game.ConnectPortals` Harmony transpiler pattern-matches against are still present.

```bash
VALHEIM_DIR=<Valheim> dotnet run --project tools/il-verify -c Release
```

Run it after a Valheim update and before a release. A non-zero exit code means the game's IL moved and the transpiler in `BetterPortal/Patches.cs` needs to be revisited. See `tools/il-verify/README.md` for what it does and does not verify.

## Direct MSBuild fallback
Use direct MSBuild commands only when debugging the build script or project file.

Debug build:

```bash
dotnet msbuild BetterPortal.sln /restore /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" \
  /p:FrameworkPathOverride=$VALHEIM_DIR/valheim_Data/Managed
```

Release build:

```bash
dotnet msbuild BetterPortal.sln /restore /t:Build /p:Configuration=Release "/p:Platform=Any CPU" \
  /p:FrameworkPathOverride=$VALHEIM_DIR/valheim_Data/Managed
```

## Output
- Debug output: `BetterPortal/bin/Debug/BetterPortal.dll`
- Release output without packaging: `BetterPortal/bin/Release/BetterPortal.dll`
- Release output with `SEVENZIP_PATH`: package archives under `BetterPortal/bin/Release`
- Installed mod path: `<Valheim>/BepInEx/plugins/BetterPortal`

## Debug build auto-deploy
Debug builds through `scripts/build.sh` or direct MSBuild trigger `DeployModFiles` when `VALHEIM_DIR` points to a valid BepInEx Valheim install.

## Release repack
Release builds use `ILRepack.Lib.MSBuild.Task` to merge `LitJSON` into the output assembly. If the ILRepack build targets are missing, the Release build fails.

## Release packaging
Set `SEVENZIP_PATH` to a `7z` executable to create release archives after ILRepack:

- `BetterPortal - <Version>.7z` — Nexus package (mod files in a `BetterPortal/` subfolder)
- `BetterPortal - <Version>.zip` — Thunderstore package (`plugins/`, `icon.png`, `manifest.json`, `README.md`, `CHANGELOG.md`)

Thunderstore assets (`icon.png`, `manifest.json`, `README.md`) are copied from `distributor/thunderstore/`. `CHANGELOG.md` is copied into the Thunderstore package as a separate root file.

## Manual install
Copy the following into `<Valheim>/BepInEx/plugins/BetterPortal`:
- `BetterPortal.dll`
- `Languages/`

## Troubleshooting
- If assembly references fail, verify that the chosen Valheim directory really contains `valheim_Data/Managed`.
- If BepInEx references fail, verify that `BepInEx/core/0Harmony.dll` and `BepInEx/core/BepInEx.dll` exist under the same Valheim directory.
