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

## My playlists (account linking)

**Tools > FoulFoxHacks > Create BetterYTS Link Panel** adds a second panel where players bring their own playlists:

1. They press **Link YouTube** or **Link Spotify**. The panel shows a short code and an address with a 10-minute countdown.
2. On their phone or PC they open the address, enter the code, sign in and pick playlists (up to 20 playlists, 1,000 songs).
3. The panel picks them up (it checks every 5 seconds while the code is on screen) and lists them. Clicking a song plays it.
4. The library number is saved in the player's PlayerData (`betteryts.library.youtube` / `betteryts.library.spotify`),
   so the playlists load again on their next visit. **Unlink** forgets it.

Anyone with a library number could load those playlists, so the panel tells players to send only playlists they're OK sharing.

Udon can't build URLs, so the panel bakes `…/vrc/link/start/youtube`, `…/vrc/link/start/spotify` and 10,000
`…/vrc/l/{n}` library URLs, and reuses the `…/vrc/p/{slot}` pool for polling and playing. The **Bake link URLs** button
on the component re-bakes them (from the server set in the BetterYTS window).

## Components

| Component | Purpose |
|---|---|
| `BetterYTSSearch` | The panel logic. Hooked up by the editor tool; events `_OnSearchSubmit`, `_Next`, `_Back`, `_Cell0`…`_Cell11`. |
| `BetterYTSLink` | The link panel logic. Events `_LinkYouTube`, `_LinkSpotify`, `_NewCode`, `_CancelCode`, `_ShowYouTube`, `_ShowSpotify`, `_Back`, `_PrevPage`, `_NextPage`, `_Reload`, `_Unlink`, `_Row0`…`_Row9`. |
| `FFHUrlPool` (core) | The baked URL pool. |
| `FFHVideoTarget` (core) | Where results play. Use the ProTV adapter, `FFHVrcPlayerTarget`, or `FFHUdonEventTarget`. |
