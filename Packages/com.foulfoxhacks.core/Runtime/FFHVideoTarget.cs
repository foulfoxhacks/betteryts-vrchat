using UdonSharp;
using VRC.SDKBase;

namespace FoulFoxHacks.Core
{
    /// <summary>
    /// Something that can play a URL. Tools (like the BetterYTS search panel) talk to this,
    /// so they work with any video player that has an adapter:
    /// <see cref="FFHVrcPlayerTarget"/> (VRChat's Unity/AVPro players),
    /// <see cref="FFHUdonEventTarget"/> (any Udon player via variable + event),
    /// or the ProTV adapter in com.foulfoxhacks.betteryts.protv.
    /// </summary>
    public abstract class FFHVideoTarget : UdonSharpBehaviour
    {
        /// <summary>Play <paramref name="url"/>. <paramref name="title"/> is optional display text.</summary>
        public abstract void _PlayUrl(VRCUrl url, string title);
    }
}
