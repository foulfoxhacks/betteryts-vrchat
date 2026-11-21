# Testing account linking in VRChat

Needs the BetterYTS Worker with linking protocol v2 deployed (`/vrc/link/start/*`, `/vrc/l/{a}`, `/vrc/k/{b}`,
`/vrc/forget`, `/inworld/*`, and `/tt/*` for TikTok). Turn on **Allow Untrusted URLs** first. Build & Test is fine for
most of this, and ClientSim's PlayerData window helps with the key steps.

1. **Build the panel:** Tools > FoulFoxHacks > Create BetterYTS Link Panel in a scene that has a search panel and ProTV.
   The new panel should reuse the search panel's URL pool and video target. Its inspector should say "All link URLs
   are baked". The Library URL Pool should start at `…/vrc/l/0`, the Library Key URL Pool at `…/vrc/k/0`, both with
   10,000 slots, and Forget URL should be `…/vrc/forget`.
2. **Link for this visit only:** press Link YouTube. You should get a code, the address
   `yts.foulfoxhacks.dev/inworld/youtube/auth` and a 10:00 countdown. On a phone, enter the code, sign in, pick 2 or 3
   playlists and leave "save" unticked. Within about 5 seconds the panel should list them and say "for this visit
   only. Nothing is saved". The header shows "· this visit only". Open a playlist, page through it, click a song
   (it plays in ProTV), and check Back. PlayerData `betteryts.lib.youtube.a` / `.b` should stay unset or `-1`.
   **Reload** should say there's nothing to reload.
3. **Link and save:** do it again and tick "save". The status should say "saved for your next visit (30 days)",
   and `betteryts.lib.youtube.a` / `.b` should hold two numbers from 0 to 9999.
4. **Expiry:** get a code and leave it. At 0:00 (or when the server says `expired`) the panel should stop polling and
   show **Get a new code**. **Cancel** should go back without polling.
5. **Next visit:** rejoin. The list should come back on its own after about 5 to 10 seconds (two loads, 5 s apart).
   The `.a` / `.b` values should change every time you rejoin.
6. **My key:** press it. The status shows `AAAA-BBBB` and the manage address. Enter that key on the site: it should
   show the same list, offer a download, and let you delete it.
7. **Unlink right after a load:** press Unlink within a minute of the list loading. It should say the list was
   deleted from BetterYTS, the keys become `-1`, and the Link buttons come back. Rejoin: nothing loads, and the old
   key on the site finds nothing.
8. **Unlink later:** link and save again, wait more than a minute, then press Unlink. The panel loads the list first
   (about 10 s), then deletes it. Same result as 7.
9. **Unlink failure:** with Untrusted URLs turned off mid-session, Unlink should say it couldn't delete, show the key,
   and keep the key saved so you can retry.
10. **Spotify:** repeat 2, 3 and 5 with the Spotify tab. Libraries for different services are kept side by side, and
    switching tabs loads the other one.
11. **TikTok:** Link TikTok shows `yts.foulfoxhacks.dev/inworld/tiktok` (no `/auth`). After signing in (the TikTok
    sandbox until the app is approved), the panel lists "My TikTok videos" with lengths and "on TikTok". Clicking one
    says TikTok videos can't play in VRChat. If the server isn't set up for TikTok, the panel shows its message, or
    "TikTok linking isn't available yet", and stays on the Link buttons.
12. **Missing list:** set `.a` / `.b` to an unused pair (e.g. 9999 / 9999) in ClientSim. The panel should say the
    list expired or was deleted, reset both keys to `-1`, and offer Link. After 5 wrong pairs in an hour the server
    locks you out for the hour; the panel should show its message, not hang.
13. **Limits and errors:** a big library caps at 20 playlists / 1,000 songs. With Untrusted URLs off, the panel shows
    the "turn on Allow Untrusted URLs" hint. Rate-limit messages from the server appear as they are.
14. **Alongside search:** search on the search panel while a code is showing or a list is loading. Both should keep
    working (VRChat queues the downloads, so each one may wait a few seconds).
