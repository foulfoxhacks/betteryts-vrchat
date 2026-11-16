using ArchiTech.ProTV;
using FoulFoxHacks.Core;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace FoulFoxHacks.BetterYTS.ProTV
{
    /// <summary>
    /// Sends URLs from BetterYTS (or any FoulFoxHacks tool) to a ProTV 3 TV.
    /// ProTV handles ownership, sync and its lock/permission rules: if the TV is locked and the
    /// player isn't allowed, ProTV ignores the request.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BetterYTSProTVTarget : FFHVideoTarget
    {
        [Tooltip("The ProTV TV (the object with the TVManager component).")]
        public TVManager tv;

        [Tooltip("Also send the URL as ProTV's alternate (Quest/Android) URL. YouTube links work on both.")]
        public bool useAsAlternateUrl = true;

        public override void _PlayUrl(VRCUrl url, string title)
        {
            if (tv == null || url == null) return;
            tv._ChangeMedia(url, useAsAlternateUrl ? url : null, title);
        }
    }
}
