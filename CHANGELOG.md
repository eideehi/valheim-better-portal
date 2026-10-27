#### v1.1.0 [2026-10-27]
- While editing a destination tag, a clickable list of existing tags appears with scrolling and a highlight for the matching tag. The list is a snapshot taken on focus, even when empty; refocus the field to refresh it.
- Configure Better Portal in the game’s Settings screen under Mods; select Better Portal and press Apply to save changes. To hide Better Portal from Mods, turn off `Show on the Settings screen` in Configuration Manager or set `show_on_settings_screen=false` in the config file; the option is not shown in the Better Portal Settings panel and takes effect the next time Settings opens.

#### v1.0.8 [2026-10-10]
- Updated for changes in Valheim 1.0.15
- Fixed clearing a portal connection when the destination tag changes
- Filtered censored destination tags in the portal hover text

#### v1.0.7 [2026-05-17]
- Updated for changes in Valheim 0.221.12
- Added a configurable modifier key for destination tag editing
- Fixed German localization file naming
- Updated build and release packaging metadata

#### v1.0.6 [2023-10-20]
- Add german translation **([#4])**
- Fix translation not loading on language change

#### v1.0.5 [2023-10-07]
- Fix tag name completion in Valheim 0.217.24 **([#3])**

#### v1.0.4 [2023-07-02]
- Updated for changes in Valheim 0.216.9

#### v1.0.3 [2023-03-18]
- Updated for changes in Valheim 0.214.2

#### v1.0.2 [2023-03-02]
- Improve input autocomplete

#### v1.0.1 [2022-12-14]
- Change to update connections immediately when destination tag is changed
- Add the function to complete tag names while entering destination tags

#### v1.0.0 [2022-11-23]
- Initial release

<!-- Issue / PR links -->
[#3]: https://github.com/eideehi/valheim-better-portal/pull/3
[#4]: https://github.com/eideehi/valheim-better-portal/pull/4
