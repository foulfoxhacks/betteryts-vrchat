# BetterYTS In-World Search

**BetterYTS by FoulFoxHacks** for VRChat worlds: players type a search, browse a 4×3 thumbnail grid
of ad-free YouTube results, and click one to play it on your world's video player.

## Setup

1. Add the FoulFoxHacks listing to VRChat Creator Companion: `https://vpm.foulfoxhacks.dev/index.json`
2. Add **BetterYTS In-World Search** and **BetterYTS Editor Tools** to your world project (plus **BetterYTS for ProTV** if you use ProTV).
3. In Unity: **Tools > FoulFoxHacks > BetterYTS > Create search panel in scene**.
   The panel is built, its 20,000-slot URL pool is baked, and a ProTV TV or VRC video player in the scene is linked automatically.

## How it works

Udon can't build URLs at runtime, so the panel ships a baked pool of `https://yts.foulfoxhacks.dev/vrc/p/{slot}` URLs.
The search reply tells the panel which slot plays each video, which slot returns the thumbnail sheet, and which slot is the next page.
Full contract: [docs/vrchat-protocol.md](https://github.com/foulfoxhacks/betteryts/blob/main/docs/vrchat-protocol.md).

- VRChat allows one string and one image download per 5 seconds per world, so a page is one JSON request plus one thumbnail sheet.
- Players need **Allow Untrusted URLs** on (Settings > Comfort & Safety), as with any custom search service. The panel says so when a download fails.
- Searching is per-player. Sync of what's playing comes from your video player (ProTV syncs; VRChat's bare players play locally).

## Components

| Component | Purpose |
|---|---|
| `BetterYTSSearch` | The panel logic. Hooked up by the editor tool; events `_OnSearchSubmit`, `_Next`, `_Back`, `_Cell0`…`_Cell11`. |
| `FFHUrlPool` (core) | The baked URL pool. |
| `FFHVideoTarget` (core) | Where results play. Use the ProTV adapter, `FFHVrcPlayerTarget`, or `FFHUdonEventTarget`. |
