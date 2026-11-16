using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace FoulFoxHacks.Core.Editor
{
    [CustomEditor(typeof(FFHUrlPool))]
    public class FFHUrlPoolEditor : UnityEditor.Editor
    {
        private int _count = 20000;

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            var pool = (FFHUrlPool)target;

            EditorGUILayout.LabelField("FoulFoxHacks URL Pool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Udon can't build URLs at runtime, so this holds pre-made URLs. Services hand out a slot number and the world loads urls[slot].",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            string pattern = EditorGUILayout.TextField(new GUIContent("URL pattern", "Must contain {slot}"), pool.urlPattern);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pool, "Change URL pattern");
                pool.urlPattern = pattern;
                UdonSharpEditorUtility.CopyProxyToUdon(pool);
            }

            int size = pool.GetSize();
            EditorGUILayout.LabelField("Baked slots", size == 0 ? "none" : $"{size:N0}  ({pool.urls[0]?.Get()} … )");

            _count = EditorGUILayout.IntField(new GUIContent("Slots to bake", "BetterYTS uses 20000"), _count);
            if (GUILayout.Button(size == 0 ? "Bake URL pool" : "Re-bake URL pool", GUILayout.Height(28)))
            {
                FFHUrlPoolBaker.Bake(pool, pool.urlPattern, _count);
            }
        }
    }
}
