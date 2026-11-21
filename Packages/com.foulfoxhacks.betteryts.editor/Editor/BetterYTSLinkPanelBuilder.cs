using FoulFoxHacks.Core;
using FoulFoxHacks.Core.Editor;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using static FoulFoxHacks.BetterYTS.Editor.BetterYTSSceneBuilder;

namespace FoulFoxHacks.BetterYTS.Editor
{
    /// <summary>Builds a wired-up "My playlists" panel (account linking + saved libraries) in the open scene.</summary>
    public static class BetterYTSLinkPanelBuilder
    {
        private const float Width = 1200f;
        private const float Height = 1400f;

        public static BetterYTSLink Build(string baseUrl, int poolSize)
        {
            if (!EnsureTmpEssentials(() => Build(baseUrl, poolSize))) return null;
            baseUrl = baseUrl.TrimEnd('/');
            var root = new GameObject("BetterYTS Link Panel");
            Undo.RegisterCreatedObjectUndo(root, "Create BetterYTS Link Panel");
            PlaceInFrontOfSceneView(root.transform);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(Width, Height);
            root.transform.localScale = Vector3.one * 0.001f;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<VRCUiShape>();

            var bg = Child(root.transform, "Background");
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = Bg;

            // Header + service tabs
            var title = Tmp(root.transform, "Title", "My playlists", 60, Cream, TextAlignmentOptions.Left, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -110), new Vector2(520, -24));
            var by = Tmp(root.transform, "By", "BETTERYTS BY FOULFOXHACKS", 22, Gold, TextAlignmentOptions.Left, FontStyles.Bold);
            by.characterSpacing = 18f;
            Anchor(by.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(52, -150), new Vector2(520, -108));
            // Three tabs, 196 wide with 12 gaps, right-aligned: they end at -48 and start at -660.
            var ytTab = MakeButton(root.transform, "YouTube Tab", "YouTube", new Vector2(1, 1), new Vector2(-660, -112), new Vector2(-464, -32));
            var spTab = MakeButton(root.transform, "Spotify Tab", "Spotify", new Vector2(1, 1), new Vector2(-452, -112), new Vector2(-256, -32));
            var ttTab = MakeButton(root.transform, "TikTok Tab", "TikTok", new Vector2(1, 1), new Vector2(-244, -112), new Vector2(-48, -32));

            // Body area shared by the three panels
            var body = Child(root.transform, "Body");
            Anchor(body, Vector2.zero, Vector2.one, new Vector2(48, 230), new Vector2(-48, -180));

            // --- Link panel
            var linkPanel = Child(body, "Link Panel");
            Stretch(linkPanel);
            var linkIntro = Tmp(linkPanel, "Intro",
                "Bring your own playlists into this world.\nIt's for this visit only, unless you choose to save it.",
                38, Cream, TextAlignmentOptions.Center, FontStyles.Normal);
            linkIntro.enableWordWrapping = true;
            Anchor(linkIntro.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -260), new Vector2(0, -40));
            var linkYt = MakeButton(linkPanel, "Link YouTube Button", "Link YouTube", new Vector2(0.5f, 0.5f), new Vector2(-300, 80), new Vector2(300, 180));
            var linkSp = MakeButton(linkPanel, "Link Spotify Button", "Link Spotify", new Vector2(0.5f, 0.5f), new Vector2(-300, -50), new Vector2(300, 50));
            var linkTt = MakeButton(linkPanel, "Link TikTok Button", "Link TikTok", new Vector2(0.5f, 0.5f), new Vector2(-300, -180), new Vector2(300, -80));

            // --- Code panel
            var codePanel = Child(body, "Code Panel");
            Stretch(codePanel);
            var goTo = Tmp(codePanel, "Go To", "On your phone or PC, go to", 34, Muted, TextAlignmentOptions.Center, FontStyles.Normal);
            Anchor(goTo.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100), new Vector2(0, -30));
            var codeUrl = Tmp(codePanel, "Code URL", "", 44, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            codeUrl.enableWordWrapping = true;
            Anchor(codeUrl.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -230), new Vector2(0, -100));
            var enter = Tmp(codePanel, "Enter", "and enter this code", 34, Muted, TextAlignmentOptions.Center, FontStyles.Normal);
            Anchor(enter.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -300), new Vector2(0, -230));
            var code = Tmp(codePanel, "Code", "", 150, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            code.characterSpacing = 12f;
            Anchor(code.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -500), new Vector2(0, -310));
            var countdown = Tmp(codePanel, "Countdown", "", 32, Muted, TextAlignmentOptions.Center, FontStyles.Normal);
            Anchor(countdown.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -580), new Vector2(0, -510));
            var newCode = MakeButton(codePanel, "New Code Button", "Get a new code", new Vector2(0.5f, 1), new Vector2(-260, -620), new Vector2(260, -520));
            newCode.gameObject.SetActive(false);
            var cancel = MakeButton(codePanel, "Cancel Button", "Cancel", new Vector2(0.5f, 0), new Vector2(-160, 10), new Vector2(160, 90));

            // --- Library panel
            var libPanel = Child(body, "Library Panel");
            Stretch(libPanel);
            var back = MakeButton(libPanel, "Back Button", "‹ Playlists", new Vector2(0, 1), new Vector2(0, -80), new Vector2(240, 0));
            var header = Tmp(libPanel, "Header", "", 36, Cream, TextAlignmentOptions.Left, FontStyles.Bold);
            header.overflowMode = TextOverflowModes.Ellipsis;
            header.richText = true;
            Anchor(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(260, -80), new Vector2(0, 0));

            var list = Child(libPanel, "Rows");
            Anchor(list, Vector2.zero, Vector2.one, new Vector2(0, 110), new Vector2(0, -100));
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            int n = BetterYTSLink.MaxRows;
            var rows = new GameObject[n];
            var rowTitles = new TextMeshProUGUI[n];
            var rowDetails = new TextMeshProUGUI[n];
            var rowButtons = new Button[n];
            for (int i = 0; i < n; i++)
            {
                var row = Child(list, $"Row {i + 1}");
                var rowBg = row.gameObject.AddComponent<Image>();
                rowBg.color = Card;
                rowButtons[i] = row.gameObject.AddComponent<Button>();
                rowButtons[i].targetGraphic = rowBg;
                var colors = rowButtons[i].colors;
                colors.highlightedColor = new Color(1f, 0.85f, 0.6f);
                rowButtons[i].colors = colors;

                rowTitles[i] = Tmp(row, "Title", "", 28, Cream, TextAlignmentOptions.Left, FontStyles.Bold);
                rowTitles[i].overflowMode = TextOverflowModes.Ellipsis;
                Anchor(rowTitles[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-330, 0));
                rowDetails[i] = Tmp(row, "Details", "", 22, Muted, TextAlignmentOptions.Right, FontStyles.Normal);
                rowDetails[i].overflowMode = TextOverflowModes.Ellipsis;
                Anchor(rowDetails[i].rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-320, 0), new Vector2(-20, 0));
                rows[i] = row.gameObject;
            }

            var prev = MakeButton(libPanel, "Prev Page Button", "‹", new Vector2(0, 0), new Vector2(0, 10), new Vector2(110, 90));
            var next = MakeButton(libPanel, "Next Page Button", "›", new Vector2(0, 0), new Vector2(250, 10), new Vector2(360, 90));
            var page = Tmp(libPanel, "Page", "", 26, Muted, TextAlignmentOptions.Center, FontStyles.Normal);
            Anchor(page.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 10), new Vector2(250, 90));
            var key = MakeButton(libPanel, "My Key Button", "My key", new Vector2(1, 0), new Vector2(-680, 10), new Vector2(-480, 90));
            var reload = MakeButton(libPanel, "Reload Button", "Reload", new Vector2(1, 0), new Vector2(-460, 10), new Vector2(-240, 90));
            var unlink = MakeButton(libPanel, "Unlink Button", "Unlink", new Vector2(1, 0), new Vector2(-220, 10), new Vector2(0, 90));

            // Footer: privacy line + status
            var privacy = Tmp(root.transform, "Privacy", "", 24, Gold, TextAlignmentOptions.Center, FontStyles.Italic);
            privacy.enableWordWrapping = true;
            Anchor(privacy.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(48, 120), new Vector2(-48, 210));
            var status = Tmp(root.transform, "Status", "", 26, Muted, TextAlignmentOptions.Center, FontStyles.Normal);
            status.enableWordWrapping = true;
            Anchor(status.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(48, 30), new Vector2(-48, 115));

            // URL pools: reuse a baked slot pool from a search panel if there is one.
            FFHUrlPool pool = FindSlotPool(baseUrl);
            if (pool == null) pool = MakePool(root.transform, "URL Pool", BetterYTSLinkBaker.PoolPattern(baseUrl), poolSize);
            FFHUrlPool libraryPool = MakePool(root.transform, "Library URL Pool", BetterYTSLinkBaker.LibraryPattern(baseUrl), 0);
            FFHUrlPool keyPool = MakePool(root.transform, "Library Key URL Pool", BetterYTSLinkBaker.KeyPattern(baseUrl), 0);

            var target = Object.FindObjectOfType<BetterYTSSearch>() is BetterYTSSearch search && search.videoTarget != null
                ? search.videoTarget
                : CreateVideoTarget(root.transform);

            var link = UdonSharpUndo.AddComponent<BetterYTSLink>(root);
            link.pool = pool;
            link.libraryPool = libraryPool;
            link.keyPool = keyPool;
            link.videoTarget = target;
            link.linkPanel = linkPanel.gameObject;
            link.codePanel = codePanel.gameObject;
            link.libraryPanel = libPanel.gameObject;
            link.youTubeTab = ytTab;
            link.spotifyTab = spTab;
            link.tiktokTab = ttTab;
            link.codeText = code;
            link.codeUrlText = codeUrl;
            link.countdownText = countdown;
            link.newCodeButton = newCode;
            link.headerText = header;
            link.backButton = back;
            link.prevPageButton = prev;
            link.nextPageButton = next;
            link.pageText = page;
            link.rows = rows;
            link.rowTitles = rowTitles;
            link.rowDetails = rowDetails;
            link.statusText = status;
            link.privacyText = privacy;
            privacy.text = link.privacyLine;
            codePanel.gameObject.SetActive(false);
            libPanel.gameObject.SetActive(false);
            BetterYTSLinkBaker.Bake(link, baseUrl, poolSize); // start + forget URLs, 2 × 10000 library URLs

            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(link);
            Wire(linkYt, udon, nameof(BetterYTSLink._LinkYouTube));
            Wire(linkSp, udon, nameof(BetterYTSLink._LinkSpotify));
            Wire(linkTt, udon, nameof(BetterYTSLink._LinkTikTok));
            Wire(ytTab, udon, nameof(BetterYTSLink._ShowYouTube));
            Wire(spTab, udon, nameof(BetterYTSLink._ShowSpotify));
            Wire(ttTab, udon, nameof(BetterYTSLink._ShowTikTok));
            Wire(newCode, udon, nameof(BetterYTSLink._NewCode));
            Wire(cancel, udon, nameof(BetterYTSLink._CancelCode));
            Wire(back, udon, nameof(BetterYTSLink._Back));
            Wire(prev, udon, nameof(BetterYTSLink._PrevPage));
            Wire(next, udon, nameof(BetterYTSLink._NextPage));
            Wire(reload, udon, nameof(BetterYTSLink._Reload));
            Wire(key, udon, nameof(BetterYTSLink._ShowKey));
            Wire(unlink, udon, nameof(BetterYTSLink._Unlink));
            for (int i = 0; i < n; i++) Wire(rowButtons[i], udon, "_Row" + i);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            if (target == null)
            {
                EditorUtility.DisplayDialog("BetterYTS",
                    "The link panel is ready, but no video player was found in the scene.\n\nAdd ProTV (or a VRC video player), then set the panel's Video Target.",
                    "OK");
            }
            return link;
        }

        private static void Wire(Button button, VRC.Udon.UdonBehaviour udon, string evt)
        {
            UnityEventTools.AddStringPersistentListener(button.onClick, udon.SendCustomEvent, evt);
        }

        private static FFHUrlPool FindSlotPool(string baseUrl)
        {
            string pattern = BetterYTSLinkBaker.PoolPattern(baseUrl);
            foreach (var p in Object.FindObjectsOfType<FFHUrlPool>())
            {
                if (p.urlPattern == pattern && p.GetSize() > 0) return p;
            }
            return null;
        }

        /// <param name="count">Slots to bake now; 0 leaves it to <see cref="BetterYTSLinkBaker.Bake"/>.</param>
        private static FFHUrlPool MakePool(Transform parent, string name, string pattern, int count)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create BetterYTS URL pool");
            go.transform.SetParent(parent, false);
            var pool = UdonSharpUndo.AddComponent<FFHUrlPool>(go);
            pool.urlPattern = pattern;
            if (count > 0) FFHUrlPoolBaker.Bake(pool, pattern, count);
            else UdonSharpEditorUtility.CopyProxyToUdon(pool);
            return pool;
        }
    }
}
