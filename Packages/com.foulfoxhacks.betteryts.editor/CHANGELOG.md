# Changelog

## Unreleased

- Added: **Create BetterYTS Link Panel** (menu and BetterYTS window) and a **Bake link URLs** inspector button that bakes the start URLs and the 10,000 saved-library URLs.
- Fixed: creating a search or link panel threw a NullReferenceException in projects without TextMesh Pro's Essential Resources. The builders now offer to import them and finish building once the import is done.
- The link panel builder adds a **Link TikTok** button and a **TikTok** tab (three tabs across), and the baker bakes `…/vrc/link/start/tiktok`.
- The baker also bakes `…/vrc/k/{b}` (10,000) and `…/vrc/forget`, and the link panel builder adds a key pool and a **My key** button.

## 1.0.1 (2026-11-17)

- Fixed: the scene builder failed to compile (CS0266). The search field now uses `VRCUrlInputField.LineType`.
- Requires BetterYTS In-World Search 1.0.1.

## 1.0.0 (2026-11-15)

- First release.
