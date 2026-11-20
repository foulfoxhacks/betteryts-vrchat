# Changelog

## Unreleased

- Added: `BetterYTSLink`, in-world account linking. Players link YouTube or Spotify with a code on their phone, browse their playlists and play songs; the library is kept in PlayerData for the next visit.
- `BetterYTSLink` shows the server's own error message when a download fails and VRChat passes the response body on; otherwise it falls back to a message based on the HTTP status.

## 1.0.1 (2026-11-17)

- Fixed: the runtime assembly failed to compile (CS0012). It now references `VRC.Udon.Serialization.OdinSerializer`.

## 1.0.0 (2026-11-15)

- First release.
