# BetterPortal Documentation Shape Migration Implementation Plan

## Goal
- Bring BetterPortal documentation closer to the local `~/dev/mods/valheim-automatics` documentation shape: Markdown root README, exact configuration reference, practical user guide, and matching Thunderstore README, without changing verified BetterPortal runtime behavior.
- Current slice: documentation structure and prose migration only. Release packaging alignment and local screenshot migration are separate decisions because they can change distribution behavior or require missing assets.

## Verified facts and sources
| Claim | Evidence | Source | Impact |
| --- | --- | --- | --- |
| BetterPortal currently has `README.adoc` at the repository root, `distributor/thunderstore/README.md`, and no root `README.md`, `CONFIG.md`, or `docs/` directory in the non-hidden file list. | Local investigation | `rg --files` in `/home/eideehi/dev/mods/valheim-better-portal` | Defines the files to convert or add for the current slice. |
| BetterPortal's current public README content covers overview, important notice, features, languages, contacts, credits, and license, and uses two Box-hosted image URLs: one feature image and one bug-report contact image. | Local investigation | `README.adoc`, `distributor/thunderstore/README.md` | Preserve existing public claims, download the feature image during implementation, replace the bug-report image with an Automatics-style text issue link, and remove Box URLs from public docs. |
| BetterPortal has one verified user-facing config entry: section `general`, key `ModifierKey`, default `KeyCode.LeftShift`. `KeyCode.None` is rejected by resetting to the default. Left/right Shift, Control, and Alt are paired so either side works when one side is configured. | Local investigation | `BetterPortal/BetterPortal.cs` | `CONFIG.md` should document only this verified option unless new code evidence is found. |
| The English localization names `ModifierKey` as `Modifier Key` and describes it as the key held while interacting with a portal to set its destination tag. | Local investigation | `BetterPortal/Languages/en_us.json` | Use existing localized terms in documentation. |
| Verified player behavior: using a portal without the modifier edits the normal portal tag; using it with the modifier edits the destination tag; private-area access is checked; destination text length is requested with max length `10`; hover text shows source tag, destination tag, connection status, and the modifier prompt. | Local investigation | `BetterPortal/Patches.cs` | User guide should describe these behaviors and not invent additional workflows. |
| Verified text input helpers: `Insert` autocompletes from existing portal tags, and `UpArrow` / `DownArrow` rotate existing tags. | Local investigation | `BetterPortal/TextInputExtension.cs` | User guide should include these shortcuts. |
| Automatics uses a Markdown documentation shape with root `README.md`, root `CONFIG.md`, `docs/user-guide.md`, docs images under `docs/images/readme/`, plus feature-specific docs only where features need them. | Local investigation | `rg --files` and heading scan in `/home/eideehi/dev/mods/valheim-automatics` | Target shape for BetterPortal should be adapted, not copied wholesale. |
| Automatics root README uses relative repository links and local images; Automatics Thunderstore README uses absolute GitHub `blob` and `raw.githubusercontent.com` links tied to version `1.6.0`. | Local investigation | `README.md`, `distributor/thunderstore/README.md` in Automatics | BetterPortal needs a link strategy for root docs versus Thunderstore docs. |
| BetterPortal release packaging currently concatenates `distributor/thunderstore/README.md` and `CHANGELOG.md` into the packaged Thunderstore `README.md`; project instructions document that behavior. Automatics packaging copies `README.md` and `CHANGELOG.md` as separate Thunderstore package files. | Primary source and Local investigation | User-provided `AGENTS.md` instructions; `BetterPortal/BetterPortal.csproj`; `Automatics/Automatics.csproj` | Full Automatics packaging parity is a release-behavior change and should not be bundled into the minimal documentation slice without a decision. |
| BetterPortal manifest version is `1.1.0`, but local tags list only `1.0.0` through `1.0.6`; the latest local commit message is `v1.1.0`. | Local investigation | `distributor/thunderstore/manifest.json`; `git tag`; `git log --oneline -5` | Versioned Thunderstore links to `/blob/1.1.0/` are unproven locally. Use `main` links unless the tag is verified or created before release. |
| Optional skills verified in the current environment include `vibe-plan-execution`, `writing-style-guide`, and `vibe-commit-message`. | Primary source | Current session skill metadata | These can support later execution, prose review, and commit message creation but are not required for the plan to remain valid. |

## Requirements
- In scope:
  - Convert the root user-facing README from AsciiDoc shape to Automatics-style Markdown at `README.md`.
  - Add `CONFIG.md` with the exact verified BetterPortal configuration entry and its practical effect.
  - Add `docs/user-guide.md` with practical usage guidance for normal portal tags, destination tags, modifier behavior, autocomplete, tag rotation, duplicate destination handling, private-area access, and configuration.
  - Download the currently referenced feature Box image and place it under `docs/images/readme/`, following Automatics' local README image layout.
  - Replace the current bug-report image link with an Automatics-style text issue link; do not add `bug-report.png`.
  - Update `distributor/thunderstore/README.md` so it mirrors the new README structure while using Thunderstore-safe absolute links for repository documentation and a raw GitHub image URL for the downloaded feature image.
  - Update repository-local documentation references away from root `README.adoc` where they exist in BetterPortal-owned files.
  - Preserve existing facts, credits, language table entries, issue link, and license statement while replacing Box image URLs with local repository image paths.
- Out of scope:
  - Runtime code changes, config changes, translations, mod version bump, release notes beyond documentation references, and new feature claims.
  - Automatics-specific docs such as `docs/add-user-defined-object.md`, `docs/custom-icon-pack.md`, `examples/custom-icon-pack/`, and console-command docs because BetterPortal has no verified equivalent features.
  - `.github/ISSUE_TEMPLATE` changes because BetterPortal already has the same issue template filenames as Automatics.
  - Changing Thunderstore package README/CHANGELOG layout to match Automatics unless the packaging decision is accepted.
- Constraints:
  - Use local code and existing docs as the source of truth for behavior.
  - Do not claim a full accepted-value list for `ModifierKey` unless Unity/BepInEx parsing is verified from a primary source or local runtime proof.
  - Root README links should be relative repository links. Thunderstore README documentation links should be absolute GitHub links.
  - Root README image links should be relative paths under `docs/images/readme/`. Thunderstore README image links should use `https://raw.githubusercontent.com/eideehi/valheim-better-portal/main/docs/images/readme/...` unless a version tag is verified before implementation.
  - Downloaded image files must be validated as renderable images before docs are updated to reference them.
  - For Thunderstore documentation links, prefer `main` until a `1.1.0` tag is verified or created.

## Ambiguities, questions, and decisions
- Item: Whether "ドキュメント形態を `~/dev/mods/valheim-automatics` のように" includes release packaging behavior.
- Options or decision: Recommended current path is to align the documentation files and keep BetterPortal's existing packaging behavior. Full Automatics parity would also modify `BetterPortal/BetterPortal.csproj` and `AGENTS.md` so the Thunderstore package contains separate `README.md` and `CHANGELOG.md`.
- Evidence: BetterPortal packaging concatenates README plus changelog; Automatics copies them separately.
- Recommended path: Implement documentation shape first. Change packaging only after an explicit user decision because it changes release artifacts.

- Item: Whether to migrate current Box-hosted images into `docs/images/readme/`.
- Options or decision: Decision accepted by user on 2026-05-10: download the existing feature Box image and place it like Automatics. Do not migrate the bug-report contact image because Automatics does not use `bug-report.png`.
- Evidence: BetterPortal has no local README images; Automatics has local images under `docs/images/readme/` but its Contacts section uses a text issue link, not a bug-report image; user explicitly requested Automatics alignment.
- Recommended path: Include only feature image download and local placement in the current slice. Use `docs/images/readme/features.jpg` unless downloaded MIME/extension evidence requires a different extension. Replace Contacts with `[Open an issue](https://github.com/eideehi/valheim-better-portal/issues) for bug reports.`

- Item: Whether Thunderstore links should use `/blob/1.1.0/` like Automatics uses `/blob/1.6.0/`.
- Options or decision: Use `main` links for the docs update, or verify/create a `1.1.0` tag before using versioned links.
- Evidence: BetterPortal manifest says `1.1.0`, but local git tags do not include `1.1.0`.
- Recommended path: Use `main` links unless tag proof is added before implementation.

## Acceptance criteria
- `README.md` exists at the repository root and follows the Automatics-style Markdown structure: title, overview, important notice, features, configurations, languages, contacts, credits, and license.
- Root `README.md` links to `docs/user-guide.md` and `CONFIG.md` with relative links.
- Root `README.adoc` is removed or replaced by `README.md`, and BetterPortal-owned docs no longer link to the root `README.adoc`.
- `CONFIG.md` documents only verified BetterPortal config entries. At minimum it includes `General / Modifier Key / [ModifierKey]`, default `LeftShift`, the destination-tag editing effect, and the `None` reset behavior.
- `docs/user-guide.md` exists and explains the verified portal workflows: editing the normal tag, editing the destination tag with the configured modifier, destination tag length behavior, existing-tag autocomplete with `Insert`, tag rotation with `UpArrow` and `DownArrow`, random connection when multiple portals share the destination tag, private-area access behavior, and where to edit the config.
- `distributor/thunderstore/README.md` mirrors the new README content and links to `CONFIG.md`, `docs/user-guide.md`, and `LICENSE` using absolute GitHub URLs that are valid under the chosen link strategy.
- No BetterPortal-owned public documentation file references `app.box.com` after the migration.
- `docs/images/readme/features.jpg` exists, is a renderable image, and is referenced by root `README.md` with a relative path.
- No `docs/images/readme/bug-report.png` is added.
- Contacts uses an Automatics-style text issue link rather than an image link.
- `distributor/thunderstore/README.md` references the downloaded feature image with a raw GitHub URL under the chosen branch or tag.
- Automatics-only capabilities are not mentioned in BetterPortal docs.
- Existing credits for LitJSON, Fusionette, and FreeFun remain present.
- Existing language support rows for English, German, and Japanese remain present.
- The documentation change does not require a Valheim or BepInEx build for the current slice. If packaging behavior is changed, the release build target must also be verified.

## Test plan
- Acceptance tests:
  - Inspect `README.md`, `CONFIG.md`, `docs/user-guide.md`, and `distributor/thunderstore/README.md` against the acceptance criteria.
  - Run `rg -n "README\\.adoc|docs/add-user-defined-object|docs/custom-icon-pack|Automatic door|Automatic mapping|Automatics" README.md CONFIG.md docs distributor/thunderstore` and confirm only intentional historical or unrelated matches remain.
  - Run `rg -n "ModifierKey|Modifier Key|LeftShift|Insert|UpArrow|DownArrow" README.md CONFIG.md docs/user-guide.md distributor/thunderstore/README.md` and confirm verified behavior is documented in the right files.
  - Run `rg -n "app\\.box\\.com|box\\.com" README.md CONFIG.md docs distributor/thunderstore` and confirm there are no matches in BetterPortal-owned public docs.
  - Confirm `docs/images/readme/features.jpg` exists and matches the image format used by the docs.
  - Confirm `docs/images/readme/bug-report.png` does not exist and no public doc references it.
- Regression tests:
  - Run `git diff --check` to catch whitespace and patch formatting issues.
  - If `BetterPortal/BetterPortal.csproj` is not changed, no build is required for the documentation-only slice.
  - If packaging is changed, run the documented Release packaging build with `SEVENZIP_PATH` and valid `VALHEIM_DIR`, or mark packaging verification blocked if those dependencies are unavailable.
- Negative and edge cases:
  - Confirm the docs do not list unverified config options or a complete `KeyCode` value list.
  - Confirm Thunderstore links do not use `/blob/1.1.0/` unless the `1.1.0` tag has been verified or created.
  - Confirm local image references point only to image files added in the same change.
- Manual or visual checks:
  - Preview Markdown headings and tables in a Markdown renderer if available.
  - Open or inspect the downloaded images locally and check that root README image links and Thunderstore image links render under the chosen image strategy.

## Skill usage plan
- Skill: `vibe-plan-execution`
- Availability source: Current session skill metadata.
- Use when: Executing this implementation plan in a later turn.
- Matching reason: Its description matches applying an existing implementation plan with acceptance criteria.
- Fallback if unavailable: Follow this plan directly, using repository rules, the acceptance criteria, and the test plan as the execution contract.

- Skill: `writing-style-guide`
- Availability source: Current session skill metadata.
- Use when: Drafting or revising `README.md`, `CONFIG.md`, `docs/user-guide.md`, and `distributor/thunderstore/README.md`.
- Matching reason: Its description covers user-facing docs, READMEs, and changelogs.
- Fallback if unavailable: Keep prose concise, factual, behavior-based, and consistent with Automatics' current documentation style.

- Skill: `vibe-commit-message`
- Availability source: Current session skill metadata.
- Use when: A verified documentation slice is ready to commit.
- Matching reason: Its description covers writing commit messages and Conventional Commit context.
- Fallback if unavailable: Use recent repository history and a standalone Conventional Commit message such as `docs: align BetterPortal documentation shape with Automatics`.

## Implementation plan
1. Re-check local facts before editing: current file list, current `ModifierKey` implementation, language rows, packaging target, and local git tag state.
2. Decide the active branch of the plan:
   - Current slice: docs migration, preserve package concatenation, download the feature Box image into `docs/images/readme/`, replace the bug-report image with a text issue link, and use `main` links in Thunderstore docs.
   - Optional branch: if explicitly accepted, also align packaging with Automatics.
3. Download and validate the current README feature image:
   - Download `https://app.box.com/shared/static/8anhpoogiwa4tek8rznl2m1ag5mt6wso.jpg` to `docs/images/readme/features.jpg`.
   - Do not download `https://app.box.com/shared/static/g2v3vbju4jazq7kycoigp60ltki2kw8i.png`; replace that contact image with text to match Automatics.
   - Use a safe downloader with network approval when required by the sandbox.
   - Validate that the downloaded file is a renderable image and that the file extension matches the actual content type.
   - Stop and ask for a replacement asset if the feature image URL cannot be downloaded or does not produce a valid image.
4. Convert root `README.adoc` to `README.md`:
   - Preserve the BetterPortal overview, important notice, feature description, tips, note about random destination selection, feature image, language table, contacts, credits, and license.
   - Replace the current Contacts image with a text issue link in the same style as Automatics.
   - Add a `Configurations` section that links to `CONFIG.md` and `docs/user-guide.md`.
   - Use relative links for repository files and local image paths such as `docs/images/readme/features.jpg`.
5. Add `CONFIG.md`:
   - Document `## [ #1 General ] / [general]`.
   - Add `### Modifier Key / [ModifierKey]`.
   - Include default `LeftShift`, the destination-tag editing purpose, and the `None` reset behavior.
   - Avoid a full accepted-value list unless verified.
6. Add `docs/user-guide.md`:
   - Include quick links and "Start here" sections like Automatics, adapted to BetterPortal's smaller scope.
   - Cover portal tag and destination tag workflows, hover text, private-area access, autocomplete, rotation, duplicate destination tags, and config editing.
   - Include related references back to `README.md` and `CONFIG.md`.
7. Update `distributor/thunderstore/README.md`:
   - Mirror root README structure in Markdown.
   - Use absolute GitHub links for `CONFIG.md`, `docs/user-guide.md`, and `LICENSE`.
   - Use `main` links unless `1.1.0` tag proof has been added.
   - Use a raw GitHub URL for `docs/images/readme/features.jpg`.
   - Use an Automatics-style text issue link in Contacts.
8. Remove root `README.adoc` after `README.md` replaces it, unless local evidence shows a required consumer that still needs AsciiDoc.
9. If and only if packaging parity is explicitly accepted:
   - Update `BetterPortal/BetterPortal.csproj` so Thunderstore files include `distributor/thunderstore/README.md`, `CHANGELOG.md`, `icon.png`, and `manifest.json` as separate files, matching Automatics.
   - Update `AGENTS.md` release packaging notes to remove the concatenation statement.
   - Verify Release packaging or mark the environment blocker.
10. Run the test plan commands and manually review Markdown content for behavior accuracy and link strategy consistency.
11. Final diff review: compare the result against this plan, acceptance criteria, and out-of-scope list before reporting completion.

## Commit checkpoints
- Current single-slice docs migration: commit checkpoints are omitted until the documentation files pass the acceptance tests. Proposed standalone commit message after verification: `docs: align BetterPortal documentation shape with Automatics`.
- Optional packaging-alignment branch: use a separate commit only after Release packaging verification or an explicitly recorded blocker. Proposed standalone commit message: `build: align Thunderstore package docs layout with Automatics`.

## Risks and unproven items
- Item: The user may intend full Automatics parity, including release packaging behavior.
- Evidence label: `Unproven`
- Impact: Packaging layout can change for Thunderstore consumers and should not be changed silently.
- Fastest proof path: Ask the user whether package README/CHANGELOG layout should be changed after they review this plan.
- Revisit trigger: Before editing `BetterPortal/BetterPortal.csproj` or `AGENTS.md` packaging notes.

- Item: The current Box-hosted feature image URL may fail to download or may not produce a valid image file in the implementation environment.
- Evidence label: `Accepted risk`
- Impact: The docs cannot fully remove Box dependencies while preserving the current feature visual unless a valid local image file is obtained. The bug-report image is intentionally not preserved to match Automatics.
- Fastest proof path: During implementation, download the feature image URL with approved network access and validate the resulting file type/renderability before updating README references.
- Revisit trigger: If the feature image download fails, if the downloaded content is not an image, or if the file extension differs from the expected content.

- Item: A remote or intended `1.1.0` Git tag may exist even though the local tag list does not show it.
- Evidence label: `Unproven`
- Impact: Versioned Thunderstore links can break if the tag is absent when users view the package.
- Fastest proof path: Run `git ls-remote --tags origin 1.1.0` with network approval, or create/push the release tag before using versioned links.
- Revisit trigger: Before using `/blob/1.1.0/` or `raw.githubusercontent.com/.../1.1.0/...` in Thunderstore docs.

- Item: The complete set of accepted serialized values for BepInEx `KeyCode` config entries has not been verified in this planning pass.
- Evidence label: `Unproven`
- Impact: A full accepted-values list in `CONFIG.md` could be inaccurate or too noisy.
- Fastest proof path: Verify against Unity `KeyCode` documentation or a local generated config/runtime test.
- Revisit trigger: Before documenting anything beyond the verified default and `None` reset behavior.

## Implementation handoff
- When implementing this plan, treat this document as authoritative. Re-check local facts before editing, follow the acceptance criteria, test plan, and skill usage plan, implement only the current in-scope slice, and stop if the `Proceed condition` is blocked or local evidence contradicts the plan.

## Proceed condition
- Ready to implement the current docs-and-image slice if the implementer preserves BetterPortal's current package concatenation, downloads and validates the feature Box image into `docs/images/readme/`, replaces the bug-report image with a text issue link, removes all Box URLs from public docs, and uses `main` links for Thunderstore documentation references.
- Blocked before changing Thunderstore packaging or using versioned `1.1.0` links until the corresponding decision or proof is recorded. Also blocked if the feature Box image cannot be downloaded and no replacement asset is provided.
