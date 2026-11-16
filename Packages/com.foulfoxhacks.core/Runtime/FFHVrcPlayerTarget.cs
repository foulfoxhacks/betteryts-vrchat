using UdonSharp;
using UnityEngine;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;

namespace FoulFoxHacks.Core
{
    /// <summary>
    /// Plays URLs on one of VRChat's built-in video players (VRC Unity Video Player or VRC AVPro Video Player).
    /// Playback is local to whoever clicked. For synced playback use ProTV (com.foulfoxhacks.betteryts.protv)
    /// or another synced player with <see cref="FFHUdonEventTarget"/>.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FFHVrcPlayerTarget : FFHVideoTarget
    {
        [Tooltip("A VRC Unity Video Player or VRC AVPro Video Player.")]
        public BaseVRCVideoPlayer player;

        public override void _PlayUrl(VRCUrl url, string title)
        {
            if (player == null || url == null) return;
            player.PlayURL(url);
        }
    }
}
