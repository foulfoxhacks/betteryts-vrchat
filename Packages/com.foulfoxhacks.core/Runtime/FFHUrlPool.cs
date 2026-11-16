using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace FoulFoxHacks.Core
{
    /// <summary>
    /// A list of VRCUrls baked at edit time. Udon can't build a VRCUrl at runtime, so services
    /// such as BetterYTS hand out "slots" and the world loads <c>urls[slot]</c>.
    /// Bake it from the inspector (or Tools > FoulFoxHacks > BetterYTS).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FFHUrlPool : UdonSharpBehaviour
    {
        [Tooltip("Pattern the pool was baked from. {slot} is replaced by 0, 1, 2…")]
        public string urlPattern = "https://yts.foulfoxhacks.dev/vrc/p/{slot}";

        [Tooltip("Baked URLs. Don't edit by hand: use the Bake button.")]
        public VRCUrl[] urls = new VRCUrl[0];

        /// <summary>Number of baked slots.</summary>
        public int GetSize()
        {
            return urls == null ? 0 : urls.Length;
        }

        /// <summary>The URL for a slot, or null if the slot is outside the baked range.</summary>
        public VRCUrl GetUrl(int slot)
        {
            if (urls == null || slot < 0 || slot >= urls.Length) return null;
            return urls[slot];
        }
    }
}
