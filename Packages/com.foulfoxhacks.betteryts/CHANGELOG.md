# Changelog

## Unreleased

- Added: `BetterYTSLink`, in-world account linking. Players link YouTube or Spotify with a code on their phone, browse their playlists and play songs; the library is kept in PlayerData for the next visit.
- `BetterYTSLink` shows the server's own error message when a download fails and VRChat passes the response body on; otherwise it falls back to a message based on the HTTP status.
- Added: TikTok as a third linking service (`_LinkTikTok`, `_ShowTikTok`, PlayerData key `betteryts.library.tiktok`). Until the server supports it, Link TikTok shows "TikTok linking isn't available yet" or the server's message.
- Changed: `BetterYTSLink` speaks linking protocol v2. Saving is optional ("just this visit" is the default); saved lists use a two-part key (`/vrc/l/{a}` then `/vrc/k/{b}`) kept in PlayerData as `betteryts.lib.{service}.a` / `.b`. Unlink deletes the list on the server (`/vrc/forget`), and **My key** (`_ShowKey`) shows the key for managing it on the site. TikTok videos are listed but not played.

## 1.0.1 (2026-11-17)

- Fixed: the runtime assembly failed to compile (CS0012). It now references `VRC.Udon.Serialization.OdinSerializer`.

## 1.0.0 (2026-11-15)

- First release.
