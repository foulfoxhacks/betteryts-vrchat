# FoulFoxHacks Core

Shared UdonSharp building blocks used by the FoulFoxHacks world tools.

| Component | What it does |
|---|---|
| `FFHUrlPool` | Holds pre-baked `VRCUrl`s. Udon can't build a URL at runtime, so services hand out slot numbers and the world loads `urls[slot]`. Bake it from the inspector. |
| `FFHVideoTarget` | Base class for "something that can play a URL". Tools talk to this instead of a specific player. |
| `FFHVrcPlayerTarget` | Plays on VRChat's built-in Unity / AVPro video players (local playback). |
| `FFHUdonEventTarget` | Plays on any Udon video player: sets a URL variable, then sends a custom event. |

Requires the VRChat Worlds SDK 3.7+ (UdonSharp is included).
