#!/usr/bin/env bash
#
# Regression tests for scripts/check-version.sh.
#
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
repo_root="$(cd -- "$script_dir/.." &>/dev/null && pwd)"
tmp_dir="$(mktemp -d)"

cleanup() {
    rm -rf "$tmp_dir"
}
trap cleanup EXIT

fixture="$tmp_dir/repo"
mkdir -p "$fixture/scripts" "$fixture/BetterPortal/Properties" "$fixture/distributor/thunderstore" "$fixture/docs"
cp "${1:-$repo_root/scripts/check-version.sh}" "$fixture/scripts/check-version.sh"
chmod +x "$fixture/scripts/check-version.sh"

write_versions() {
    local version="$1"
    local assembly_version="$2"

    cat >"$fixture/BetterPortal/BetterPortal.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Version>$version</Version>
  </PropertyGroup>
</Project>
EOF

    cat >"$fixture/BetterPortal/BetterPortal.cs" <<EOF
public static class UnityPlugin
{
    private const string ModVersion = "$version";
}
EOF

    cat >"$fixture/distributor/thunderstore/manifest.json" <<EOF
{
  "version_number": "$version"
}
EOF

    cat >"$fixture/BetterPortal/Properties/AssemblyInfo.cs" <<EOF
[assembly: AssemblyVersion("$assembly_version")]
[assembly: AssemblyFileVersion("$assembly_version")]
EOF
}

write_versions "1.6.0" "1.6.0.0"

cat >"$fixture/CHANGELOG.md" <<'EOF'
#### v1.6.0 [2026-05-17]
- Release notes.
EOF

cat >"$fixture/distributor/thunderstore/README.md" <<'EOF'
See [the user guide](https://github.com/eideehi/valheim-better-portal/blob/1.6.0/docs/user-guide.md).
EOF

cat >"$fixture/docs/user-guide.md" <<'EOF'
# User Guide
EOF

git -C "$fixture" init -q
git -C "$fixture" config user.email "betterportal-tests@example.invalid"
git -C "$fixture" config user.name "BetterPortal Tests"
git -C "$fixture" add .
git -C "$fixture" commit -q -m "fixture release"
git -C "$fixture" tag 1.6.0


# Images may reference a full commit created after the release tag.
mkdir -p "$fixture/docs/images/readme/tree.png"
printf 'image\n' >"$fixture/docs/images/readme/image.png"
printf 'child\n' >"$fixture/docs/images/readme/tree.png/child"
git -C "$fixture" add docs/images/readme
git -C "$fixture" commit -q -m "add images"
image_commit="$(git -C "$fixture" rev-parse HEAD)"
url_base="https://raw.githubusercontent.com/eideehi/valheim-better-portal/"
write_link() {
    printf 'See [guide](https://github.com/eideehi/valheim-better-portal/blob/1.6.0/docs/user-guide.md).\n![image](%s%s)\n' "$url_base" "$1" >"$fixture/distributor/thunderstore/README.md"
}
expect_failure() {
    local entry="$1" expected="$2"
    write_link "$entry"
    if "$fixture/scripts/check-version.sh" >"$tmp_dir/link.out" 2>"$tmp_dir/link.err"; then
        echo "error: check-version accepted $entry" >&2
        exit 1
    fi
    if ! grep -Fq "$expected" "$tmp_dir/link.err"; then
        echo "error: check-version failed for the wrong reason for $entry" >&2
        cat "$tmp_dir/link.err" >&2
        exit 1
    fi
}
write_link "$image_commit/docs/images/readme/image.png"
"$fixture/scripts/check-version.sh" >/dev/null
# Blob links are parsed by the same policy.
printf 'See [guide](https://github.com/eideehi/valheim-better-portal/blob/1.6.0/docs/user-guide.md).\n![image](https://github.com/eideehi/valheim-better-portal/blob/%s/docs/images/readme/image.png)\n' "$image_commit" >"$fixture/distributor/thunderstore/README.md"
"$fixture/scripts/check-version.sh" >/dev/null
expect_failure "main/docs/images/readme/image.png" 'contains main/master links'
expect_failure "${image_commit:0:12}/docs/images/readme/image.png" 'does not match latest release tag'
expect_failure "0000000000000000000000000000000000000000/docs/images/readme/image.png" 'is not a commit'
expect_failure "$image_commit/docs/images/readme/missing.png" 'is not a blob'
expect_failure "$image_commit/docs/images/readme/tree.png" 'is not a blob'
expect_failure "$image_commit/docs/user-guide.md" 'does not match latest release tag'
expect_failure '1.5.0/docs/user-guide.md' 'does not match latest release tag'

echo "check-version regression tests passed."
