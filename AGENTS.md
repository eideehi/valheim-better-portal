# Build Notes

## Project layout
- Solution: `BetterPortal.sln`
- Project: `BetterPortal/BetterPortal.csproj`
- Target framework: `.NET Framework 4.8`

## Repository notes
- `BetterPortal/Libraries/mod-utils` is a read-only library dependency. Do not edit files under this directory unless the task is explicitly to update the submodule.
- When preparing a release, create the Git tag that matches the release/package version so versioned README and Thunderstore links resolve correctly.

## Required local dependencies
- A local Valheim installation
- A local BepInEx installation inside the Valheim directory

The project resolves game references from the Valheim install directory. Set the following environment variable before building:

- `VALHEIM_DIR`: absolute path to the Valheim root directory that contains `valheim_Data/Managed`

The expected directory shape is:

- `<Valheim>/valheim_Data/Managed`
- `<Valheim>/BepInEx/core`

## Build commands
Use MSBuild-compatible tooling.

Debug build:

```bash
msbuild BetterPortal.sln /p:Configuration=Debug "/p:Platform=Any CPU"
```

Release build:

```bash
msbuild BetterPortal.sln /p:Configuration=Release "/p:Platform=Any CPU"
```

If your environment prefers the .NET SDK entry point, `dotnet msbuild` can be used instead of `msbuild` as long as the machine can build .NET Framework 4.8 projects.

When building under WSL or Linux, pass `FrameworkPathOverride` so MSBuild can use Valheim's managed framework assemblies:

```bash
dotnet msbuild BetterPortal.sln /p:Configuration=Debug "/p:Platform=Any CPU" \
  /p:FrameworkPathOverride=$VALHEIM_DIR/valheim_Data/Managed
```

## Output
- Debug output: `BetterPortal/bin/Debug/BetterPortal.dll`
- Release output: `BetterPortal/bin/Release/BetterPortal.dll`
- Installed mod path: `<Valheim>/BepInEx/plugins/BetterPortal`

## Debug build auto-deploy
A native Windows Debug build auto-deploys the mod into `BepInEx/plugins/BetterPortal` if the game path is valid. WSL builds do not auto-deploy.

## Release repack
Release builds use `ILRepack.Lib.MSBuild.Task` to merge `LitJSON` into the output assembly.

## Release packaging
Optional: set `SEVENZIP_PATH` to a `7z` executable. If set, the Release build creates two archives after ILRepack:

- `BetterPortal - <Version>.7z` — Nexus package (mod files in a `BetterPortal/` subfolder)
- `BetterPortal - <Version>.zip` — Thunderstore package (`plugins/`, `icon.png`, `manifest.json`, `README.md`)

Thunderstore assets (`icon.png`, `manifest.json`, `README.md`) are sourced from `distributor/thunderstore/`. The Thunderstore `README.md` is a concatenation of `distributor/thunderstore/README.md` and `CHANGELOG.md`.

## Manual install
Copy the following into `<Valheim>/BepInEx/plugins/BetterPortal`:
- `BetterPortal.dll`
- `Languages/`

## Troubleshooting
- If assembly references fail, verify that the chosen Valheim directory really contains `valheim_Data/Managed`.
- If BepInEx references fail, verify that `BepInEx/core/0Harmony.dll` and `BepInEx/core/BepInEx.dll` exist under the same Valheim directory.
