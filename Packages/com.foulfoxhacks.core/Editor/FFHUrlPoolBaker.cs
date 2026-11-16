using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRC.SDKBase;

namespace FoulFoxHacks.Core.Editor
{
    /// <summary>Fills an <see cref="FFHUrlPool"/> with URLs built from a pattern.</summary>
    public static class FFHUrlPoolBaker
    {
        public const int MaxSlots = 100000;

        /// <param name="pattern">URL with a <c>{slot}</c> placeholder, e.g. https://yts.foulfoxhacks.dev/vrc/p/{slot}</param>
        /// <param name="count">Number of slots (must match the server's pool size).</param>
        public static void Bake(FFHUrlPool pool, string pattern, int count)
        {
            if (pool == null) return;
            if (string.IsNullOrEmpty(pattern) || !pattern.Contains("{slot}"))
            {
                EditorUtility.DisplayDialog("FoulFoxHacks", "The URL pattern needs a {slot} placeholder.", "OK");
                return;
            }
            if (count < 1 || count > MaxSlots)
            {
                EditorUtility.DisplayDialog("FoulFoxHacks", $"Slot count must be between 1 and {MaxSlots}.", "OK");
                return;
            }

            var urls = new VRCUrl[count];
            for (int i = 0; i < count; i++)
            {
                if (i % 2000 == 0 && EditorUtility.DisplayCancelableProgressBar("Baking URL pool", $"{i} / {count}", (float)i / count))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }
                urls[i] = new VRCUrl(pattern.Replace("{slot}", i.ToString()));
            }
            EditorUtility.ClearProgressBar();

            // No Undo here on purpose: recording tens of thousands of URLs makes the editor crawl.
            pool.urlPattern = pattern;
            pool.urls = urls;
            UdonSharpEditorUtility.CopyProxyToUdon(pool);
            EditorUtility.SetDirty(pool);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pool);
            if (pool.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(pool.gameObject.scene);
        }
    }
}
