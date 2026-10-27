# Better Portal - Valheim Mod
Better Portal lets you change a Valheim portal's destination to any tag. It is a minimal alternative for players who do not want to install Jotunn. If Jotunn is already installed, this mod has no advantage over similar portal mods.

> **IMPORTANT**
>
> This mod is developed by an individual and is not associated with the game's developer. Do not contact the game developer about this mod.

## Features

For released documentation, see [docs/user-guide.md](https://github.com/eideehi/valheim-better-portal/blob/1.1.0/docs/user-guide.md).

When interacting with a portal, hold the configured modifier key to set the destination tag. Portals do not have to be connected to each other, and teleportation can be one-way.

> **TIP**: Pressing the Insert key while entering the destination tag autocompletes from existing portal tags. Pressing the Up Arrow or Down Arrow key rotates existing tags. While the destination tag field has focus, a clickable list of existing portal tags also appears below the dialog.

> **NOTE**: If multiple portals have the specified destination tag, the connection target is selected randomly.

![Features - Image](https://raw.githubusercontent.com/eideehi/valheim-better-portal/97754f624e13a859b5e8ffaf4720f9af507bd2a8/docs/images/readme/features.jpg)

## Configurations

Open the game's Settings screen from the main menu or the pause menu, select the **Mods** tab, and then select **Better Portal**. Changes are saved when you press **Apply**.

![Settings screen](https://raw.githubusercontent.com/eideehi/valheim-better-portal/97754f624e13a859b5e8ffaf4720f9af507bd2a8/docs/images/readme/settings-screen.jpg)

Better Portal also works with [Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager).

To remove Better Portal from the **Mods** tab, turn off `Show on the Settings screen` in Configuration Manager’s `Settings screen` category, or set `show_on_settings_screen=false` in the config file’s `[mod_utils]` section. The change takes effect the next time the Settings screen opens; this option is not shown in the Better Portal Settings panel.

Use [CONFIG.md](https://github.com/eideehi/valheim-better-portal/blob/1.1.0/CONFIG.md) for every configuration entry and default value. Use [docs/user-guide.md](https://github.com/eideehi/valheim-better-portal/blob/1.1.0/docs/user-guide.md) for workflow notes.

## Languages

| Language | Translators | Status |
| --- | --- | --- |
| English | Translation Tools | 100% |
| German | FreeFun | 100% |
| Japanese | EideeHi | 100% |

## Contacts

[Open an issue](https://github.com/eideehi/valheim-better-portal/issues) for bug reports.

## Credits

- **Dependencies**:
  - [LitJSON](https://litjson.net)
- **Contributors**:
  - [Fusionette](https://github.com/Fusionette)
  - [FreeFun](https://github.com/xXFreeFunXx)

## License

Better Portal is developed and released under the MIT license. See [LICENSE](https://github.com/eideehi/valheim-better-portal/blob/1.1.0/LICENSE) for the full license text.
