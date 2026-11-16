# BetterYTS for ProTV

Plays BetterYTS in-world search results on a [ProTV 3](https://protv.dev) TV, so playback gets ProTV's sync,
ownership and lock rules (if the TV is locked, ProTV decides who may change media).

`BetterYTSProTVTarget` is an `FFHVideoTarget`: link it as the search panel's **Video Target** and set its **TV** field.
The BetterYTS editor tool does both automatically when it finds a ProTV TV in the scene.

### ProTV playlists from YouTube

You don't need this package for playlists. In a ProTV Playlist, set *Media Entries* to **Remote URL** and paste a
BetterYTS playlist link, then click **Download** and **Import**:

```
https://yts.foulfoxhacks.dev/vrc/protv.playlist?q=lofi%20hip%20hop&n=50
https://yts.foulfoxhacks.dev/vrc/protv.playlist?list=PLxxxxxxxxxxxxxxxx&n=200
```

The **ProTV playlists** tab in Tools > FoulFoxHacks > BetterYTS builds these links for you.

Requires ProTV 3 (`dev.architech.protv`, from `https://vpm.techanon.dev/index.json`). ProTV 3 is a pre-release:
enable *Show pre-release packages* in VCC if it doesn't appear.
