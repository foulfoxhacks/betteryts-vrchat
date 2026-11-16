using System;
using System.IO;
using FoulFoxHacks.Core;
using FoulFoxHacks.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace FoulFoxHacks.BetterYTS.Editor
{
    /// <summary>Tools > FoulFoxHacks > BetterYTS</summary>
    public class BetterYTSWindow : EditorWindow
    {
        private const string DefaultServer = "https://yts.foulfoxhacks.dev";
        private const string PrefServer = "FoulFoxHacks.BetterYTS.Server";
        private static readonly string[] Tabs = { "In-world search", "VRChat links", "ProTV playlists" };

        [Serializable]
        private class ServerInfo
        {
            public int v;
            public int poolSize;
            public string poolUrl;
            public string searchPrefix;
        }

        private int _tab;
        private string _server;
        private ServerInfo _info;
        private string _serverStatus = "";
        private UnityWebRequest _request;
        private Action<UnityWebRequest> _onDone;
        private Vector2 _scroll;

        // VRChat link builder
        private string _query = "lofi hip hop";
        private int _n = 1;
        private int _maxMinutes;
        private bool _allowShorts;
        private bool _allowLive = true;
        private int _sort;
        private static readonly string[] Sorts = { "relevance", "date", "views", "rating" };

        // ProTV playlists
        private int _playlistSource;
        private static readonly string[] PlaylistSources = { "YouTube search", "YouTube playlist" };
        private string _playlistQuery = "lofi hip hop";
        private string _playlistId = "";
        private int _playlistCount = 50;

        [MenuItem("Tools/FoulFoxHacks/BetterYTS", priority = 100)]
        public static void Open()
        {
            var w = GetWindow<BetterYTSWindow>();
            w.titleContent = new GUIContent("BetterYTS");
            w.minSize = new Vector2(420, 360);
            w.Show();
        }

        [MenuItem("Tools/FoulFoxHacks/Create BetterYTS Search Panel", priority = 101)]
        public static void CreatePanelMenu()
        {
            BetterYTSSceneBuilder.Build(EditorPrefs.GetString(PrefServer, DefaultServer), 20000);
        }

        private void OnEnable()
        {
            _server = EditorPrefs.GetString(PrefServer, DefaultServer);
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollRequest;
            _request?.Dispose();
        }

        private void OnGUI()
        {
            DrawHeader();
            _tab = GUILayout.Toolbar(_tab, Tabs, GUILayout.Height(26));
            EditorGUILayout.Space(6);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawSearchTab(); break;
                case 1: DrawLinksTab(); break;
                case 2: DrawPlaylistTab(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            EditorGUILayout.LabelField("BetterYTS by FoulFoxHacks", style, GUILayout.Height(26));
            EditorGUI.BeginChangeCheck();
            _server = EditorGUILayout.TextField("Server", _server);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefServer, _server.TrimEnd('/'));
                _info = null;
            }
            EditorGUILayout.Space(4);
        }

        // ------------------------------------------------------------------ In-world search

        private void DrawSearchTab()
        {
            EditorGUILayout.HelpBox(
                "Adds a search panel to your world: players type a search, see a thumbnail grid of ad-free results, and click to play on your video player.\n\n" +
                "Players need 'Allow Untrusted URLs' on (Settings > Comfort & Safety), as with any custom search service.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _request == null;
                if (GUILayout.Button("Check server", GUILayout.Width(120))) CheckServer();
                GUI.enabled = true;
                EditorGUILayout.LabelField(_serverStatus, EditorStyles.wordWrappedMiniLabel);
            }

            int poolSize = _info != null && _info.poolSize > 0 ? _info.poolSize : 20000;
            EditorGUILayout.Space(8);
            if (GUILayout.Button("Create search panel in scene", GUILayout.Height(34)))
            {
                BetterYTSSceneBuilder.Build(_server, poolSize);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Selected panel", EditorStyles.boldLabel);
            var search = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<BetterYTSSearch>() : null;
            if (search == null)
            {
                EditorGUILayout.LabelField("Select a BetterYTS Search Panel to manage it.", EditorStyles.miniLabel);
                return;
            }

            if (search.videoTarget == null)
            {
                EditorGUILayout.HelpBox("No video target. Add ProTV or a VRC video player to the scene, then:", MessageType.Warning);
                if (GUILayout.Button("Link a video player found in the scene"))
                {
                    var target = BetterYTSSceneBuilder.CreateVideoTarget(search.transform);
                    if (target == null) EditorUtility.DisplayDialog("BetterYTS", "No ProTV TV or VRC video player was found in the scene.", "OK");
                    else
                    {
                        Undo.RecordObject(search, "Link video target");
                        search.videoTarget = target;
                        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(search);
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("Video target", search.videoTarget.GetType().Name);
            }

            if (search.pool != null)
            {
                EditorGUILayout.LabelField("URL pool", $"{search.pool.GetSize():N0} slots");
                if (search.pool.GetSize() != poolSize && GUILayout.Button($"Re-bake URL pool ({poolSize:N0} slots)"))
                {
                    FFHUrlPoolBaker.Bake(search.pool, _server.TrimEnd('/') + "/vrc/p/{slot}", poolSize);
                }
            }
        }

        private void CheckServer()
        {
            _serverStatus = "Checking…";
            Get(_server.TrimEnd('/') + "/vrc/info", req =>
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    _serverStatus = "Couldn't reach the server: " + req.error;
                    return;
                }
                _info = JsonUtility.FromJson<ServerInfo>(req.downloadHandler.text);
                _serverStatus = _info.v == 1
                    ? $"OK · protocol v{_info.v} · pool {_info.poolSize:N0} slots"
                    : $"Server speaks protocol v{_info.v}; this package speaks v1. Update the BetterYTS packages.";
            });
        }

        // ------------------------------------------------------------------ VRChat link builder

        private void DrawLinksTab()
        {
            EditorGUILayout.HelpBox(
                "Build a link that plays the top YouTube result in any VRChat video player, e.g. for a button, a playlist entry, or a sign in your world.",
                MessageType.Info);
            _query = EditorGUILayout.TextField("Search", _query);
            _n = EditorGUILayout.IntSlider(new GUIContent("Result #", "Play the nth result"), _n, 1, 20);
            _maxMinutes = EditorGUILayout.IntField(new GUIContent("Max length (min)", "0 = no limit"), _maxMinutes);
            _sort = EditorGUILayout.Popup("Sort", _sort, Sorts);
            _allowShorts = EditorGUILayout.Toggle("Allow Shorts", _allowShorts);
            _allowLive = EditorGUILayout.Toggle("Allow live streams", _allowLive);

            string url = BuildSearchUrl();
            EditorGUILayout.Space(6);
            EditorGUILayout.SelectableLabel(url, EditorStyles.textField, GUILayout.Height(20));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy")) EditorGUIUtility.systemCopyBuffer = url;
                if (GUILayout.Button("Open in browser")) Application.OpenURL(url.Replace("/search?", "/?"));
            }
        }

        private string BuildSearchUrl()
        {
            string url = _server.TrimEnd('/') + "/search?q=" + UnityWebRequest.EscapeURL(_query ?? "").Replace("+", "%20");
            if (_n > 1) url += "&n=" + _n;
            if (_maxMinutes > 0) url += "&maxlen=" + (_maxMinutes * 60);
            if (_sort > 0) url += "&sort=" + Sorts[_sort];
            if (_allowShorts) url += "&shorts=1";
            if (!_allowLive) url += "&live=0";
            return url;
        }

        // ------------------------------------------------------------------ ProTV playlists

        private void DrawPlaylistTab()
        {
            EditorGUILayout.HelpBox(
                "Turn a YouTube search or playlist into a ProTV playlist.\n\n" +
                "Option A: copy the link, then in your ProTV Playlist set Media Entries to 'Remote URL', paste it, and click Download > Import.\n" +
                "Option B: save the .playlist file into your project and import it with 'In Project'.",
                MessageType.Info);
            _playlistSource = EditorGUILayout.Popup("Source", _playlistSource, PlaylistSources);
            if (_playlistSource == 0) _playlistQuery = EditorGUILayout.TextField("Search", _playlistQuery);
            else _playlistId = EditorGUILayout.TextField(new GUIContent("Playlist URL or ID", "e.g. https://www.youtube.com/playlist?list=PL…"), _playlistId);
            _playlistCount = EditorGUILayout.IntSlider("Max entries", _playlistCount, 5, 200);

            string url = BuildPlaylistUrl();
            EditorGUILayout.Space(6);
            EditorGUILayout.SelectableLabel(url, EditorStyles.textField, GUILayout.Height(20));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy link")) EditorGUIUtility.systemCopyBuffer = url;
                GUI.enabled = _request == null;
                if (GUILayout.Button("Save .playlist to project")) SavePlaylist(url);
                GUI.enabled = true;
            }
        }

        private string BuildPlaylistUrl()
        {
            string baseUrl = _server.TrimEnd('/') + "/vrc/protv.playlist?n=" + _playlistCount;
            if (_playlistSource == 0) return baseUrl + "&q=" + UnityWebRequest.EscapeURL(_playlistQuery ?? "").Replace("+", "%20");
            string id = _playlistId ?? "";
            int list = id.IndexOf("list=", StringComparison.Ordinal);
            if (list >= 0)
            {
                id = id.Substring(list + 5);
                int amp = id.IndexOf('&');
                if (amp >= 0) id = id.Substring(0, amp);
            }
            return baseUrl + "&list=" + UnityWebRequest.EscapeURL(id.Trim());
        }

        private void SavePlaylist(string url)
        {
            Get(url, req =>
            {
                if (req.result != UnityWebRequest.Result.Success || string.IsNullOrEmpty(req.downloadHandler.text))
                {
                    EditorUtility.DisplayDialog("BetterYTS", "Couldn't download the playlist: " + req.error, "OK");
                    return;
                }
                const string folder = "Assets/BetterYTS/Playlists";
                Directory.CreateDirectory(folder);
                string name = _playlistSource == 0 ? _playlistQuery : _playlistId;
                foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                if (name.Length > 60) name = name.Substring(0, 60);
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.playlist");
                File.WriteAllText(path, req.downloadHandler.text);
                AssetDatabase.ImportAsset(path);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            });
        }

        // ------------------------------------------------------------------ HTTP

        private void Get(string url, Action<UnityWebRequest> onDone)
        {
            _request?.Dispose();
            _request = UnityWebRequest.Get(url);
            _request.SetRequestHeader("User-Agent", "BetterYTS-Unity-Editor/1.0");
            _onDone = onDone;
            _request.SendWebRequest();
            EditorApplication.update -= PollRequest;
            EditorApplication.update += PollRequest;
        }

        private void PollRequest()
        {
            if (_request == null || !_request.isDone) return;
            EditorApplication.update -= PollRequest;
            var req = _request;
            var done = _onDone;
            _request = null;
            _onDone = null;
            try { done?.Invoke(req); }
            finally { req.Dispose(); Repaint(); }
        }
    }
}
