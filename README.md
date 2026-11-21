# BetterYTS for VRChat by FoulFoxHacks

VRChat world packages for [BetterYTS](https://yts.foulfoxhacks.dev): ad-free YouTube search inside your world,
ProTV integration, and Unity editor tools.

## Install

Add the FoulFoxHacks listing to **VRChat Creator Companion** (or ALCOM):

```
https://vpm.foulfoxhacks.dev/index.json
```

Or click: [Add to VCC](vcc://vpm/addRepo?url=https%3A%2F%2Fvpm.foulfoxhacks.dev%2Findex.json) · Listing page: <https://vpm.foulfoxhacks.dev>

## Packages

| Package | What it is |
|---|---|
| `com.foulfoxhacks.core` | Shared UdonSharp building blocks: baked URL pool, video-player targets. |
| `com.foulfoxhacks.betteryts` | In-world YouTube search panel: search, 4×3 thumbnail grid, click to play. |
| `com.foulfoxhacks.betteryts.protv` | Plays search results on a ProTV 3 TV (sync, queue, lock rules). |
| `com.foulfoxhacks.betteryts.editor` | Tools > FoulFoxHacks > BetterYTS: one-click panel setup, link builder, ProTV playlist import. |

## Quick start

1. Add **BetterYTS In-World Search**, **BetterYTS Editor Tools** and (for ProTV) **BetterYTS for ProTV** to your world in VCC.
2. Put a video player in your scene (ProTV recommended).
3. **Tools > FoulFoxHacks > Create BetterYTS Search Panel.** Done: the panel, URL pool, player link and button events are all set up.

4. Optional: **Tools > FoulFoxHacks > Create BetterYTS Link Panel** lets players link YouTube or Spotify and keep their playlists across visits.

Players need **Allow Untrusted URLs** enabled (Settings > Comfort & Safety) to use any custom search service in VRChat.

## ProTV playlists from YouTube

Paste a link like `https://yts.foulfoxhacks.dev/vrc/protv.playlist?q=lofi%20hip%20hop&n=50` (or `?list=PL…` for a
YouTube playlist) into a ProTV Playlist's **Remote URL** import. The editor window builds these links for you.

## How the in-world search works

See the [protocol](https://github.com/foulfoxhacks/betteryts/blob/main/docs/vrchat-protocol.md). In short: Udon can't build URLs at
runtime, so worlds bake 20,000 `…/vrc/p/{slot}` URLs, and the server tells the world which slot plays each result.

## Development

- `Packages/` holds the four VPM packages; versions move in lockstep.
- `node tools/generate-meta.mjs` creates missing `.meta` files (stable GUIDs) and UdonSharp program/assembly assets.
- Release: bump every `package.json` to the same version, commit, push tag `vX.Y.Z`. The workflow zips each package
  into one GitHub release; the listing at [foulfoxhacks/vpm](https://github.com/foulfoxhacks/vpm) indexes it.

MIT © FoulFoxHacks (Sammy The Femboy Puppy) · [akasammythepuppy.me](https://akasammythepuppy.me)
