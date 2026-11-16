using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace FoulFoxHacks.Core
{
    /// <summary>
    /// Plays URLs on any Udon video player by writing the URL into one of its variables and then
    /// sending it a custom event. Use this for players that don't have a dedicated adapter.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FFHUdonEventTarget : FFHVideoTarget
    {
        [Tooltip("The video player's UdonBehaviour.")]
        public UdonBehaviour target;

        [Tooltip("Name of the VRCUrl variable on the player that receives the URL.")]
        public string urlVariable = "";

        [Tooltip("Optional: name of a string variable that receives the title.")]
        public string titleVariable = "";

        [Tooltip("Custom event sent after the variables are set, e.g. the player's play/load event.")]
        public string playEvent = "";

        [Tooltip("Take ownership of the player first (most synced players need this).")]
        public bool takeOwnership = true;

        public override void _PlayUrl(VRCUrl url, string title)
        {
            if (target == null || url == null || string.IsNullOrEmpty(urlVariable)) return;
            if (takeOwnership && !Networking.IsOwner(target.gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, target.gameObject);
            }
            target.SetProgramVariable(urlVariable, url);
            if (!string.IsNullOrEmpty(titleVariable)) target.SetProgramVariable(titleVariable, title);
            if (!string.IsNullOrEmpty(playEvent)) target.SendCustomEvent(playEvent);
        }
    }
}
