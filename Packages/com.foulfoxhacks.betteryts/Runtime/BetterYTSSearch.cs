using FoulFoxHacks.Core;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Data;
using VRC.SDK3.Image;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace FoulFoxHacks.BetterYTS
{
    /// <summary>
    /// BetterYTS in-world search panel (protocol v1:
    /// https://github.com/foulfoxhacks/betteryts/blob/main/docs/vrchat-protocol.md).
    ///
    /// The player types after the pre-filled "…/vrc/search?q=" in the URL field. The JSON reply
    /// assigns pool slots to every video, the thumbnail sheet and the next page; this script loads
    /// <c>pool.GetUrl(slot)</c> for each. Searching is local to each player; playback sync is up to
    /// the video player behind <see cref="videoTarget"/>.
    ///
    /// Build one with Tools > FoulFoxHacks > BetterYTS > Create Search Panel.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BetterYTSSearch : UdonSharpBehaviour
    {
        private const int ProtocolVersion = 1;
        private const int MaxCells = 12;
        private const int MaxHistory = 64;

        [Header("BetterYTS")]
        [Tooltip("Pre-filled into the search field. The player types their search after it.")]
        public VRCUrl searchPrefix;
        [Tooltip("Baked BetterYTS URL pool (…/vrc/p/{slot}).")]
        public FFHUrlPool pool;
        [Tooltip("Where clicked results play: ProTV adapter, VRChat player target, or Udon event target.")]
        public FFHVideoTarget videoTarget;

        [Header("UI")]
        public VRCUrlInputField searchInput;
        public TextMeshProUGUI statusText;
        public GameObject[] cells = new GameObject[MaxCells];
        public RawImage[] thumbnails = new RawImage[MaxCells];
        public TextMeshProUGUI[] titles = new TextMeshProUGUI[MaxCells];
        public TextMeshProUGUI[] details = new TextMeshProUGUI[MaxCells];
        public Button nextButton;
        public Button backButton;
        [Tooltip("Shown in each thumbnail until the sheet arrives.")]
        public Texture placeholderTexture;

        [Header("Text")]
        public string untrustedUrlsHint = "Couldn't reach BetterYTS. Turn on Allow Untrusted URLs in Settings > Comfort & Safety, then search again.";

        private IUdonEventReceiver _receiver;
        private VRCImageDownloader _images;
        private IVRCImageDownload _sheetDownload;

        private string _prefixText = "";
        private VRCUrl _firstPageUrl;
        private bool _loading;

        // Current page
        private int[] _slots;
        private string[] _titles;
        private int[] _cellIndex;
        private int _count;
        private int _nextSlot = -1;
        private int _pageSlot = -1; // -1 = the typed search URL
        private int _sheetCols = 4;
        private int _sheetRows = 3;
        private string _query = "";

        // Back stack of page slots (-1 = first page)
        private int[] _history;
        private int _historyCount;

        private void Start()
        {
            _receiver = (IUdonEventReceiver)this;
            _slots = new int[MaxCells];
            _titles = new string[MaxCells];
            _cellIndex = new int[MaxCells];
            _history = new int[MaxHistory];
            _images = new VRCImageDownloader();
            if (searchPrefix != null) _prefixText = searchPrefix.Get();
            if (searchInput != null && searchPrefix != null) searchInput.SetUrl(searchPrefix);
            ClearCells();
            UpdateNavButtons();
            SetStatus("Type your search after q= and press Enter.");
        }

        private void OnDestroy()
        {
            if (_images != null) _images.Dispose();
        }

        // ------------------------------------------------------------------ UI events

        /// <summary>Hooked to the URL field's On End Edit.</summary>
        public void _OnSearchSubmit()
        {
            if (searchInput == null) return;
            VRCUrl url = searchInput.GetUrl();
            string text = url == null ? "" : url.Get();
            if (_prefixText.Length > 0 && !text.StartsWith(_prefixText))
            {
                SetStatus("Keep the BetterYTS address at the start and type after q=");
                if (searchPrefix != null) searchInput.SetUrl(searchPrefix);
                return;
            }
            if (text.Length <= _prefixText.Length)
            {
                SetStatus("Type something to search for.");
                return;
            }
            _historyCount = 0;
            _pageSlot = -1;
            _firstPageUrl = url;
            Load(url);
        }

        public void _Next()
        {
            if (_loading || _nextSlot < 0 || pool == null) return;
            VRCUrl url = pool.GetUrl(_nextSlot);
            if (url == null) { SetStatus("This world's URL pool is too small. Re-bake it with 20000 slots."); return; }
            PushHistory(_pageSlot);
            _pageSlot = _nextSlot;
            Load(url);
        }

        public void _Back()
        {
            if (_loading || _historyCount == 0) return;
            int slot = _history[--_historyCount];
            _pageSlot = slot;
            if (slot < 0) Load(_firstPageUrl);
            else if (pool != null) Load(pool.GetUrl(slot));
        }

        public void _Cell0() { Play(0); }
        public void _Cell1() { Play(1); }
        public void _Cell2() { Play(2); }
        public void _Cell3() { Play(3); }
        public void _Cell4() { Play(4); }
        public void _Cell5() { Play(5); }
        public void _Cell6() { Play(6); }
        public void _Cell7() { Play(7); }
        public void _Cell8() { Play(8); }
        public void _Cell9() { Play(9); }
        public void _Cell10() { Play(10); }
        public void _Cell11() { Play(11); }

        private void Play(int i)
        {
            if (i < 0 || i >= _count) return;
            if (videoTarget == null) { SetStatus("No video player is linked to this search panel."); return; }
            if (pool == null) { SetStatus("No URL pool is linked to this search panel."); return; }
            VRCUrl url = pool.GetUrl(_slots[i]);
            if (url == null) { SetStatus("This world's URL pool is too small. Re-bake it with 20000 slots."); return; }
            videoTarget._PlayUrl(url, _titles[i]);
            SetStatus("Playing: " + _titles[i]);
        }

        // ------------------------------------------------------------------ Loading

        private void Load(VRCUrl url)
        {
            if (url == null) return;
            _loading = true;
            UpdateNavButtons();
            SetStatus("Searching…");
            VRCStringDownloader.LoadUrl(url, _receiver);
        }

        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            _loading = false;
            DataToken root;
            if (!VRCJson.TryDeserializeFromJson(result.Result, out root) || root.TokenType != TokenType.DataDictionary)
            {
                SetStatus("BetterYTS sent something unexpected. Try again.");
                UpdateNavButtons();
                return;
            }
            DataDictionary data = root.DataDictionary;

            DataToken version;
            if (data.TryGetValue("v", TokenType.Double, out version) && (int)version.Double != ProtocolVersion)
            {
                SetStatus("This search panel needs an update (BetterYTS protocol changed).");
                UpdateNavButtons();
                return;
            }

            DataToken ok;
            if (data.TryGetValue("ok", TokenType.Boolean, out ok) && !ok.Boolean)
            {
                DataToken err;
                SetStatus(data.TryGetValue("error", TokenType.String, out err) ? err.String : "Search failed.");
                UpdateNavButtons();
                return;
            }
            ShowPage(data);
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            _loading = false;
            UpdateNavButtons();
            SetStatus(result.ErrorCode == 0 || result.ErrorCode == 401 || result.ErrorCode == 403
                ? untrustedUrlsHint
                : "BetterYTS error " + result.ErrorCode + ". Try again in a moment.");
        }

        private void ShowPage(DataDictionary data)
        {
            DataToken q;
            _query = data.TryGetValue("query", TokenType.String, out q) ? q.String : "";

            DataToken next;
            _nextSlot = data.TryGetValue("next", TokenType.Double, out next) ? (int)next.Double : -1;

            DataToken resultsToken;
            DataList results = data.TryGetValue("results", TokenType.DataList, out resultsToken) ? resultsToken.DataList : new DataList();
            _count = Mathf.Min(results.Count, MaxCells);

            for (int i = 0; i < MaxCells; i++)
            {
                bool used = i < _count;
                if (i < cells.Length && cells[i] != null) cells[i].SetActive(used);
                if (!used) continue;

                DataToken itemToken;
                if (!results.TryGetValue(i, TokenType.DataDictionary, out itemToken)) { _count = i; break; }
                DataDictionary item = itemToken.DataDictionary;

                _slots[i] = ReadInt(item, "slot", -1);
                _cellIndex[i] = ReadInt(item, "cell", i);
                _titles[i] = ReadString(item, "title");

                string channel = ReadString(item, "channel");
                string duration = ReadString(item, "duration");
                string views = ReadString(item, "views");
                DataToken live;
                bool isLive = item.TryGetValue("live", TokenType.Boolean, out live) && live.Boolean;

                string meta = channel;
                string length = isLive ? "LIVE" : duration;
                if (length.Length > 0) meta = meta.Length > 0 ? meta + " · " + length : length;
                if (views.Length > 0) meta = meta.Length > 0 ? meta + " · " + views : views;

                if (i < titles.Length && titles[i] != null) titles[i].text = _titles[i];
                if (i < details.Length && details[i] != null) details[i].text = meta;
                if (i < thumbnails.Length && thumbnails[i] != null)
                {
                    thumbnails[i].texture = placeholderTexture;
                    thumbnails[i].uvRect = new Rect(0f, 0f, 1f, 1f);
                }
            }

            LoadSheet(data);
            UpdateNavButtons();
            if (_count == 0) SetStatus("No results for \"" + _query + "\".");
            else SetStatus(_count + " results for \"" + _query + "\"" + (_historyCount > 0 ? " · page " + (_historyCount + 1) : ""));
        }

        private void LoadSheet(DataDictionary data)
        {
            DataToken sheetToken;
            if (pool == null || !data.TryGetValue("sheet", TokenType.DataDictionary, out sheetToken)) return;
            DataDictionary sheet = sheetToken.DataDictionary;
            _sheetCols = Mathf.Max(1, ReadInt(sheet, "cols", 4));
            _sheetRows = Mathf.Max(1, ReadInt(sheet, "rows", 3));
            VRCUrl url = pool.GetUrl(ReadInt(sheet, "slot", -1));
            if (url == null) return;

            if (_sheetDownload != null) _sheetDownload.Dispose();
            TextureInfo info = new TextureInfo();
            info.GenerateMipMaps = true;
            _sheetDownload = _images.DownloadImage(url, null, _receiver, info);
        }

        public override void OnImageLoadSuccess(IVRCImageDownload result)
        {
            if (result != _sheetDownload) return;
            Texture2D sheet = result.Result;
            float w = 1f / _sheetCols;
            float h = 1f / _sheetRows;
            for (int i = 0; i < _count && i < thumbnails.Length; i++)
            {
                if (thumbnails[i] == null) continue;
                int cell = _cellIndex[i];
                int col = cell % _sheetCols;
                int row = cell / _sheetCols;
                thumbnails[i].texture = sheet;
                // Sheet cells are row-major from the top-left; Unity UVs start at the bottom-left.
                thumbnails[i].uvRect = new Rect(col * w, 1f - (row + 1) * h, w, h);
            }
        }

        public override void OnImageLoadError(IVRCImageDownload result)
        {
            // Titles still work without thumbnails; keep the placeholders.
        }

        // ------------------------------------------------------------------ Helpers

        private void PushHistory(int slot)
        {
            if (_historyCount < MaxHistory) _history[_historyCount++] = slot;
        }

        private void ClearCells()
        {
            _count = 0;
            for (int i = 0; i < cells.Length; i++) if (cells[i] != null) cells[i].SetActive(false);
        }

        private void UpdateNavButtons()
        {
            if (nextButton != null) nextButton.interactable = !_loading && _nextSlot >= 0;
            if (backButton != null) backButton.interactable = !_loading && _historyCount > 0;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private int ReadInt(DataDictionary d, string key, int fallback)
        {
            DataToken t;
            return d.TryGetValue(key, TokenType.Double, out t) ? (int)t.Double : fallback;
        }

        private string ReadString(DataDictionary d, string key)
        {
            DataToken t;
            return d.TryGetValue(key, TokenType.String, out t) ? t.String : "";
        }
    }
}
