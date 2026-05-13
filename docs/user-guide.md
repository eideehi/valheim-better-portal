# User guide

Use this guide when you want practical notes for setting portal destination tags. For the exact configuration entry and default value, see [CONFIG.md](../CONFIG.md).

## Quick links

- [Start here](#start-here)
- [Portal tags and destination tags](#portal-tags-and-destination-tags)
- [Tag input helpers](#tag-input-helpers)
- [Configurations](#configurations)
- [Related references](#related-references)

## Start here

Start with the README for a short overview. Better Portal keeps the normal portal tag and adds a separate destination tag.

When you look at a portal, the hover text shows the portal tag, destination tag, and connection status. Empty tags are shown as `Empty Tag`.

## Portal tags and destination tags

Use a portal normally to edit its portal tag. Hold the configured modifier key while using the portal to edit its destination tag.

Portal tag input and destination tag input both use the same 10-character tag length used by the portal text input.

Private-area access is checked before either tag editor opens. If you do not have access, the game shows its normal no-access message and the tag editor does not open.

Destination tags are matched against existing portal tags. This allows one-way travel because the destination portal does not need to point back to the source portal.

If multiple portals have the matching portal tag, Better Portal randomly chooses one of them as the destination.

## Tag input helpers

The destination tag editor has shortcuts for existing portal tags:

| Key | Effect |
| --- | --- |
| `Insert` | Autocompletes from existing portal tags. Press it again to cycle through matching completions. |
| `UpArrow` | Rotates to the previous existing portal tag. |
| `DownArrow` | Rotates to the next existing portal tag. |

The helper list is built from portal tags that already exist in the current world.

## Configurations

I recommend using [Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) to edit Better Portal options in game.

Key setting:

| Setting | Effect |
| --- | --- |
| `Modifier Key` | Sets the key held while using a portal to edit its destination tag. |

The default modifier key is `LeftShift`. `None` is not allowed and is reset to the default value.

Left and right variants of Shift, Control, and Alt are treated as pairs. For example, if `LeftShift` is configured, either Shift key can be used.

## Related references

- [README](../README.md)
- [CONFIG.md](../CONFIG.md)
