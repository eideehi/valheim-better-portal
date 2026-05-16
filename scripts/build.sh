#!/usr/bin/env bash
#
# Build the BetterPortal mod.
#
# Usage:
#   scripts/build.sh               # Debug build (default)
#   scripts/build.sh Release       # Release build
#   scripts/build.sh Debug clean   # Clean first, then Debug build
#
# Environment:
#   VALHEIM_DIR   Path to the Valheim install that contains
#                 valheim_Data/Managed/assembly_valheim.dll and
#                 BepInEx/core/{0Harmony.dll,BepInEx.dll}.
#                 If unset, the script checks common Steam locations on
#                 WSL, Linux, and macOS.
#
set -euo pipefail

config="${1:-Debug}"
clean="${2:-}"

if [[ $# -gt 2 ]]; then
    echo "error: usage is scripts/build.sh [Debug|Release] [clean]" >&2
    exit 1
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
repo_root="$(cd -- "$script_dir/.." &>/dev/null && pwd)"
solution="$repo_root/BetterPortal.sln"
project="$repo_root/BetterPortal/BetterPortal.csproj"

if [[ ! -f "$solution" ]]; then
    echo "error: solution file not found: BetterPortal.sln" >&2
    exit 1
fi

if [[ ! -f "$project" ]]; then
    echo "error: project file not found: BetterPortal/BetterPortal.csproj" >&2
    exit 1
fi

case "$config" in
    Debug | Release) ;;
    *)
        echo "error: configuration must be Debug or Release (got: $config)" >&2
        exit 1
        ;;
esac

case "$clean" in
    "" | clean) ;;
    *)
        echo "error: second argument must be clean when provided (got: $clean)" >&2
        exit 1
        ;;
esac

if [[ -z "${VALHEIM_DIR:-}" ]]; then
    candidates=(
        "/mnt/c/Program Files (x86)/Steam/steamapps/common/Valheim"
        "$HOME/.steam/steam/steamapps/common/Valheim"
        "$HOME/.local/share/Steam/steamapps/common/Valheim"
        "$HOME/Library/Application Support/Steam/steamapps/common/Valheim"
    )
    for path in "${candidates[@]}"; do
        if [[ -f "$path/valheim_Data/Managed/assembly_valheim.dll" ]]; then
            export VALHEIM_DIR="$path"
            echo "Auto-detected VALHEIM_DIR=$VALHEIM_DIR"
            break
        fi
    done
fi

if [[ -z "${VALHEIM_DIR:-}" ]]; then
    echo "error: VALHEIM_DIR is not set and no Valheim install was auto-detected." >&2
    echo "       Expected: \$VALHEIM_DIR/valheim_Data/Managed/assembly_valheim.dll" >&2
    echo "       Set VALHEIM_DIR=/path/to/Valheim and re-run." >&2
    exit 1
fi

managed_dir="$VALHEIM_DIR/valheim_Data/Managed"
bepinex_core_dir="$VALHEIM_DIR/BepInEx/core"

required_files=(
    "$managed_dir/assembly_valheim.dll"
    "$bepinex_core_dir/0Harmony.dll"
    "$bepinex_core_dir/BepInEx.dll"
)

missing_files=()
for required_file in "${required_files[@]}"; do
    if [[ ! -f "$required_file" ]]; then
        missing_files+=("$required_file")
    fi
done

if (( ${#missing_files[@]} > 0 )); then
    echo "error: VALHEIM_DIR is missing required Valheim or BepInEx files." >&2
    for missing_file in "${missing_files[@]}"; do
        echo "       Missing: $missing_file" >&2
    done
    echo "       Set VALHEIM_DIR=/path/to/Valheim with BepInEx installed and re-run." >&2
    exit 1
fi

output_dir="$repo_root/BetterPortal/bin/$config"
output_dll="$output_dir/BetterPortal.dll"
msbuild_args=(
    "$solution"
    /restore
    /nologo
    "/p:Configuration=$config"
    "/p:Platform=Any CPU"
    "/p:FrameworkPathOverride=$managed_dir"
)

if [[ "$clean" == "clean" ]]; then
    dotnet msbuild "${msbuild_args[@]}" /t:Clean
fi

dotnet msbuild "${msbuild_args[@]}" /t:Build

echo
if [[ -f "$output_dll" ]]; then
    echo "Build succeeded. Output: $output_dll"
else
    echo "Build succeeded. Output directory: $output_dir"
fi

if [[ "$config" == "Debug" ]]; then
    echo "Debug deploy target: $VALHEIM_DIR/BepInEx/plugins/BetterPortal"
elif [[ "$config" == "Release" ]]; then
    nexus_package=$(find "$output_dir" -maxdepth 1 -name 'BetterPortal - *.7z' -print -quit 2>/dev/null || true)
    thunderstore_package=$(find "$output_dir" -maxdepth 1 -name 'BetterPortal - *.zip' -print -quit 2>/dev/null || true)
    if [[ -n "$nexus_package" ]]; then
        echo "Nexus package:      $nexus_package"
    fi
    if [[ -n "$thunderstore_package" ]]; then
        echo "Thunderstore zip:   $thunderstore_package"
    fi
fi
