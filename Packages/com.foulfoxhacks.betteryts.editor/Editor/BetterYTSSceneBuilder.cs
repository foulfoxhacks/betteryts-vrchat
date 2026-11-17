using System;
using FoulFoxHacks.Core;
using FoulFoxHacks.Core.Editor;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace FoulFoxHacks.BetterYTS.Editor
{
    /// <summary>Builds a complete, wired-up BetterYTS search panel in the open scene.</summary>
    public static class BetterYTSSceneBuilder
    {
        // akasammythepuppy.me palette
        private static readonly Color Bg = Hex("#130d0a");
        private static readonly Color Card = Hex("#1e1511");
        private static readonly Color Cream = Hex("#fff3e4");
        private static readonly Color Muted = Hex("#a79fb2");
        private static readonly Color Gold = Hex("#ffc857");
        private static readonly Color Ink = Hex("#1d1005");

        private const float Width = 1600f;
        private const float Height = 1400f;
        private const int Cells = 12;

        public static BetterYTSSearch Build(string baseUrl, int poolSize)
        {
            baseUrl = baseUrl.TrimEnd('/');
            var root = new GameObject("BetterYTS Search Panel");
            Undo.RegisterCreatedObjectUndo(root, "Create BetterYTS Search Panel");
            PlaceInFrontOfSceneView(root.transform);

            // World-space canvas VRChat can interact with
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(Width, Height);
            root.transform.localScale = Vector3.one * 0.001f;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<VRCUiShape>();

            var bg = Child(root.transform, "Background");
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = Bg;

            // Header
            var title = Tmp(root.transform, "Title", "BetterYTS", 64, Cream, TextAlignmentOptions.Left, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -110), new Vector2(700, -24));
            var by = Tmp(root.transform, "By", "BY FOULFOXHACKS", 22, Gold, TextAlignmentOptions.Left, FontStyles.Bold);
            by.characterSpacing = 18f;
            Anchor(by.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(52, -150), new Vector2(700, -108));

            // Search field (VRCUrlInputField is a legacy InputField, so it needs legacy Text)
            var inputGo = Child(root.transform, "Search Field");
            Anchor(inputGo, new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -250), new Vector2(-48, -170));
            var inputBg = inputGo.gameObject.AddComponent<Image>();
            inputBg.color = Cream;
            var input = inputGo.gameObject.AddComponent<VRCUrlInputField>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var inputText = LegacyText(inputGo, "Text", "", font, 30, Ink);
            var placeholder = LegacyText(inputGo, "Placeholder", "Type your search after q= and press Enter", font, 30, new Color(0.45f, 0.38f, 0.33f));
            placeholder.fontStyle = FontStyle.Italic;
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.targetGraphic = inputBg;
            input.lineType = VRCUrlInputField.LineType.SingleLine;

            // Results grid
            var grid = Child(root.transform, "Results");
            Anchor(grid, new Vector2(0, 0), new Vector2(1, 1), new Vector2(48, 130), new Vector2(-48, -280));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(365f, 318f);
            layout.spacing = new Vector2(19f, 18f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;

            var cells = new GameObject[Cells];
            var thumbs = new RawImage[Cells];
            var titles = new TextMeshProUGUI[Cells];
            var details = new TextMeshProUGUI[Cells];
            var buttons = new Button[Cells];
            for (int i = 0; i < Cells; i++)
            {
                var cell = Child(grid, $"Result {i + 1}");
                var cellBg = cell.gameObject.AddComponent<Image>();
                cellBg.color = Card;
                buttons[i] = cell.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = cellBg;
                var colors = buttons[i].colors;
                colors.highlightedColor = new Color(1f, 0.85f, 0.6f);
                buttons[i].colors = colors;

                var thumb = Child(cell, "Thumbnail");
                Anchor(thumb, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -205), new Vector2(0, 0));
                thumbs[i] = thumb.gameObject.AddComponent<RawImage>();
                thumbs[i].color = Color.white;
                thumbs[i].raycastTarget = false;

                titles[i] = Tmp(cell, "Title", "", 24, Cream, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                titles[i].enableWordWrapping = true;
                titles[i].overflowMode = TextOverflowModes.Ellipsis;
                titles[i].maxVisibleLines = 2;
                Anchor(titles[i].rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(14, 40), new Vector2(-14, -214));

                details[i] = Tmp(cell, "Details", "", 18, Muted, TextAlignmentOptions.BottomLeft, FontStyles.Normal);
                details[i].overflowMode = TextOverflowModes.Ellipsis;
                Anchor(details[i].rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(14, 10), new Vector2(-14, 40));

                cells[i] = cell.gameObject;
            }

            // Footer: back · status · next
            var back = MakeButton(root.transform, "Back Button", "‹ Back", new Vector2(0, 0), new Vector2(48, 36), new Vector2(268, 106));
            var next = MakeButton(root.transform, "Next Button", "Next ›", new Vector2(1, 0), new Vector2(-268, 36), new Vector2(-48, 106));
            var status = Tmp(root.transform, "Status", "Type your search after q= and press Enter.", 24, Muted, TextAlignmentOptions.Center, FontStyles.Normal);
            Anchor(status.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(290, 36), new Vector2(-290, 106));

            // URL pool
            var poolGo = new GameObject("URL Pool");
            Undo.RegisterCreatedObjectUndo(poolGo, "Create BetterYTS URL pool");
            poolGo.transform.SetParent(root.transform, false);
            var pool = UdonSharpUndo.AddComponent<FFHUrlPool>(poolGo);
            FFHUrlPoolBaker.Bake(pool, baseUrl + "/vrc/p/{slot}", poolSize);

            // Video target
            var target = CreateVideoTarget(root.transform);

            // Search behaviour
            var search = UdonSharpUndo.AddComponent<BetterYTSSearch>(root);
            search.searchPrefix = new VRCUrl(baseUrl + "/vrc/search?q=");
            search.pool = pool;
            search.videoTarget = target;
            search.searchInput = input;
            search.statusText = status;
            search.cells = cells;
            search.thumbnails = thumbs;
            search.titles = titles;
            search.details = details;
            search.nextButton = next;
            search.backButton = back;
            UdonSharpEditorUtility.CopyProxyToUdon(search);

            // UI → Udon events
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(search);
            UnityEventTools.AddStringPersistentListener(input.onEndEdit, udon.SendCustomEvent, nameof(BetterYTSSearch._OnSearchSubmit));
            UnityEventTools.AddStringPersistentListener(next.onClick, udon.SendCustomEvent, nameof(BetterYTSSearch._Next));
            UnityEventTools.AddStringPersistentListener(back.onClick, udon.SendCustomEvent, nameof(BetterYTSSearch._Back));
            for (int i = 0; i < Cells; i++)
            {
                UnityEventTools.AddStringPersistentListener(buttons[i].onClick, udon.SendCustomEvent, "_Cell" + i);
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            if (target == null)
            {
                EditorUtility.DisplayDialog("BetterYTS",
                    "The search panel is ready, but no video player was found in the scene.\n\nAdd ProTV (or a VRC video player), then set the panel's Video Target. The BetterYTS window can create one for you.",
                    "OK");
            }
            return search;
        }

        /// <summary>
        /// Finds a video player in the scene and adds the matching adapter: ProTV (if the ProTV
        /// package is installed) or VRChat's built-in players. Returns null if there's no player.
        /// </summary>
        public static FFHVideoTarget CreateVideoTarget(Transform parent)
        {
            Type tvType = Type.GetType("ArchiTech.ProTV.TVManager, ArchiTech.ProTV.Runtime");
            Type adapterType = Type.GetType("FoulFoxHacks.BetterYTS.ProTV.BetterYTSProTVTarget, FoulFoxHacks.BetterYTS.ProTV");
            if (tvType != null && adapterType != null)
            {
                Object tv = Object.FindObjectOfType(tvType);
                if (tv != null)
                {
                    var go = TargetObject(parent, "Video Target (ProTV)");
                    var adapter = (FFHVideoTarget)UdonSharpUndo.AddComponent(go, adapterType);
                    adapterType.GetField("tv").SetValue(adapter, tv);
                    UdonSharpEditorUtility.CopyProxyToUdon(adapter);
                    return adapter;
                }
            }

            var player = Object.FindObjectOfType<BaseVRCVideoPlayer>();
            if (player != null)
            {
                var go = TargetObject(parent, "Video Target (VRC Player)");
                var adapter = UdonSharpUndo.AddComponent<FFHVrcPlayerTarget>(go);
                adapter.player = player;
                UdonSharpEditorUtility.CopyProxyToUdon(adapter);
                return adapter;
            }
            return null;
        }

        // ------------------------------------------------------------------ UI helpers

        private static GameObject TargetObject(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create BetterYTS video target");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 min, Vector2 max)
        {
            var rect = Child(parent, name);
            Anchor(rect, anchor, anchor, min, max);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = Gold;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var text = Tmp(rect, "Label", label, 30, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform);
            return button;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI Tmp(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
        {
            var rect = Child(parent, name);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Text LegacyText(Transform parent, string name, string text, Font font, int size, Color color)
        {
            var rect = Child(parent, name);
            Anchor(rect, Vector2.zero, Vector2.one, new Vector2(24, 8), new Vector2(-24, -8));
            var t = rect.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = false;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rect)
        {
            Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void PlaceInFrontOfSceneView(Transform t)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
            {
                t.position = new Vector3(0f, 1.5f, 2f);
                return;
            }
            var cam = view.camera.transform;
            t.position = cam.position + cam.forward * 2.5f;
            t.rotation = Quaternion.LookRotation(cam.forward, Vector3.up);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
