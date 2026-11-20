# Testing account linking in VRChat

Needs the BetterYTS Worker with the linking routes deployed (`/vrc/link/start/*`, `/vrc/l/{n}`, `/inworld/*`).
Turn on **Allow Untrusted URLs** first. Build & Test is fine for most of this, and ClientSim's PlayerData window helps for step 5.

1. **Build the panel:** Tools > FoulFoxHacks > Create BetterYTS Link Panel in a scene that has a search panel and ProTV.
   The new panel should reuse the search panel's URL pool and video target. Its inspector should say "All link URLs are baked",
   and the Library URL Pool should show 10,000 slots starting at `…/vrc/l/0`.
2. **Link YouTube:** press it. Within a few seconds you should see a code like `K7QX-M2PA`, the address
   `yts.foulfoxhacks.dev/inworld/youtube/auth` and a 10:00 countdown that ticks down.
3. **Finish on a phone:** enter the code, sign in, pick 2 or 3 playlists. The panel should switch to the playlist list
   within about 5 seconds, saying "Linked! N playlists saved…". Open a playlist, page through it with ‹ ›, and click a song:
   it should play in ProTV. Also check the Back button.
4. **Expiry:** get a code and leave it. At 0:00 (or when the server says `expired`) the panel should stop polling and show
   **Get a new code**, which gets a fresh one. **Cancel** should go back without polling.
5. **Next visit:** rejoin the world. The playlists should come back on their own. PlayerData `betteryts.library.youtube`
   should hold a *different* number from the one before (it rotates on every load). Rejoin again and it should still work.
6. **Spotify:** repeat 2 to 5 with the Spotify tab and Link Spotify. Both libraries should be kept side by side; switching tabs
   loads the other one.
7. **Unlink:** press it. The key should be `-1` (PlayerData can't delete keys) and the Link buttons come back. Rejoin: nothing loads.
8. **Missing library:** set the key to a number that's not in use (e.g. 9999) in ClientSim, or wait out a replaced one.
   The panel should say the playlists expired, reset the key to `-1`, and offer Link again.
9. **Limits and errors:** a library with lots of songs should cap at 20 playlists / 1,000 songs. With Untrusted URLs off,
   the panel should show the "turn on Allow Untrusted URLs" hint. Pressing Link many times quickly should give a 429 message, not a hang.
10. **Alongside search:** search on the search panel while a code is showing. Both should keep working (VRChat queues the
    downloads, so each one may wait a few seconds).
