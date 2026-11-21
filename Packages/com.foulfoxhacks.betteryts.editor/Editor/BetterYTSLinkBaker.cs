using FoulFoxHacks.Core;
using FoulFoxHacks.Core.Editor;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRC.SDKBase;

namespace FoulFoxHacks.BetterYTS.Editor
{
    /// <summary>
    /// Bakes every URL a <see cref="BetterYTSLink"/> needs (Udon can't build URLs at runtime):
    /// the three start URLs, the saved-library pool <c>{base}/vrc/l/{n}</c> and, if it's empty,
    /// the shared slot pool <c>{base}/vrc/p/{slot}</c>.
    /// </summary>
    public static class BetterYTSLinkBaker
    {
        /// <summary>Must match LIBRARY_SLOTS on the server.</summary>
        public const int LibrarySlots = 10000;

        public static string StartUrl(string baseUrl, string service) => baseUrl.TrimEnd('/') + "/vrc/link/start/" + service;
        public static string LibraryPattern(string baseUrl) => baseUrl.TrimEnd('/') + "/vrc/l/{slot}";
        public static string PoolPattern(string baseUrl) => baseUrl.TrimEnd('/') + "/vrc/p/{slot}";

        /// <param name="poolSize">Slot pool size to bake if <c>link.pool</c> is empty (BetterYTS uses 20000).</param>
        public static void Bake(BetterYTSLink link, string baseUrl, int poolSize)
        {
            if (link == null) return;
            baseUrl = baseUrl.TrimEnd('/');

            link.startYouTubeUrl = new VRCUrl(StartUrl(baseUrl, "youtube"));
            link.startSpotifyUrl = new VRCUrl(StartUrl(baseUrl, "spotify"));
            link.startTikTokUrl = new VRCUrl(StartUrl(baseUrl, "tiktok"));

            if (link.libraryPool != null)
            {
                FFHUrlPoolBaker.Bake(link.libraryPool, LibraryPattern(baseUrl), LibrarySlots);
            }
            if (link.pool != null && link.pool.GetSize() == 0)
            {
                FFHUrlPoolBaker.Bake(link.pool, PoolPattern(baseUrl), poolSize);
            }

            UdonSharpEditorUtility.CopyProxyToUdon(link);
            EditorUtility.SetDirty(link);
            PrefabUtility.RecordPrefabInstancePropertyModifications(link);
            if (link.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(link.gameObject.scene);
        }

        /// <summary>Problems that would stop linking from working, or null if it's all baked.</summary>
        public static string Check(BetterYTSLink link)
        {
            if (link == null) return "No BetterYTSLink.";
            if (link.startYouTubeUrl == null || string.IsNullOrEmpty(link.startYouTubeUrl.Get()) ||
                link.startSpotifyUrl == null || string.IsNullOrEmpty(link.startSpotifyUrl.Get()) ||
                link.startTikTokUrl == null || string.IsNullOrEmpty(link.startTikTokUrl.Get()))
                return "Start URLs aren't baked.";
            if (link.libraryPool == null) return "No saved-library pool is assigned.";
            if (link.libraryPool.GetSize() != LibrarySlots) return $"The saved-library pool has {link.libraryPool.GetSize():N0} slots; it needs {LibrarySlots:N0}.";
            if (link.libraryPool == link.pool) return "The saved-library pool and the slot pool must be different objects.";
            if (link.pool == null || link.pool.GetSize() == 0) return "No slot pool (…/vrc/p/{slot}) is assigned or baked.";
            return null;
        }
    }

    [CustomEditor(typeof(BetterYTSLink))]
    public class BetterYTSLinkEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            var link = (BetterYTSLink)target;

            EditorGUILayout.LabelField("BetterYTS Linking", EditorStyles.boldLabel);
            string problem = BetterYTSLinkBaker.Check(link);
            EditorGUILayout.HelpBox(problem ?? "All link URLs are baked.", problem == null ? MessageType.Info : MessageType.Warning);

            string server = BetterYTSWindow.Server;
            EditorGUILayout.LabelField("Server", server);
            if (GUILayoutButton(problem == null ? "Re-bake link URLs" : "Bake link URLs"))
            {
                BetterYTSLinkBaker.Bake(link, server, 20000);
            }
            EditorGUILayout.Space(6);
            base.OnInspectorGUI();
        }

        private static bool GUILayoutButton(string label) => UnityEngine.GUILayout.Button(label, UnityEngine.GUILayout.Height(28));
    }
}
