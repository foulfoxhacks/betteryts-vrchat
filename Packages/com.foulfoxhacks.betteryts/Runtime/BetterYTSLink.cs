using FoulFoxHacks.Core;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDK3.Persistence;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace FoulFoxHacks.BetterYTS
{
    /// <summary>
    /// BetterYTS in-world account linking and saved libraries (protocol v1, "Linking":
    /// https://github.com/foulfoxhacks/betteryts/blob/main/docs/vrchat-protocol.md#linking).
    ///
    /// Works like a TV sign-in: <c>Link YouTube</c> / <c>Link Spotify</c> / <c>Link TikTok</c> loads a baked start URL
    /// that returns an activation code. The player types it at the shown address on their phone,
    /// picks playlists, and this script polls <c>pool[poll]</c> until they arrive. The library
    /// number is kept in PlayerData so the playlists come back on the next visit via the baked
    /// <c>libraryPool</c> (<c>…/vrc/l/{n}</c>). Every load hands out a NEW number, saved at once.
    ///
    /// Everything is local to the player. Bake the URLs with the inspector button, or build a
    /// whole panel with Tools > FoulFoxHacks > Create BetterYTS Link Panel.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BetterYTSLink : UdonSharpBehaviour
    {
        private const int ProtocolVersion = 1;
        public const int ServiceYouTube = 0;
        public const int ServiceSpotify = 1;
        public const int ServiceTikTok = 2;
        public const string TikTokUnavailable = "TikTok linking isn't available yet.";
        public const int MaxRows = 10;
        private const int MaxPlaylists = 20;
        private const int MaxTracks = 1000;
        private const float PollSeconds = 5.5f; // String downloads are limited to one per 5 s.

        private const int ReqNone = 0;
        private const int ReqStart = 1;
        private const int ReqPoll = 2;
        private const int ReqLibrary = 3;

        private const int ViewLink = 0;
        private const int ViewCode = 1;
        private const int ViewLibrary = 2;
        private const int ViewBusy = 3;

        [Header("BetterYTS (baked: use the Bake button)")]
        [Tooltip("…/vrc/link/start/youtube")]
        public VRCUrl startYouTubeUrl;
        [Tooltip("…/vrc/link/start/spotify")]
        public VRCUrl startSpotifyUrl;
        [Tooltip("…/vrc/link/start/tiktok")]
        public VRCUrl startTikTokUrl;
        [Tooltip("Baked BetterYTS URL pool (…/vrc/p/{slot}, 20000 slots). Used for polling and playing.")]
        public FFHUrlPool pool;
        [Tooltip("Baked saved-library URLs (…/vrc/l/{slot}, 10000 slots).")]
        public FFHUrlPool libraryPool;
        [Tooltip("Where songs play: ProTV adapter, VRChat player target, or Udon event target.")]
        public FFHVideoTarget videoTarget;

        [Header("PlayerData keys")]
        public string youTubeKey = "betteryts.library.youtube";
        public string spotifyKey = "betteryts.library.spotify";
        public string tiktokKey = "betteryts.library.tiktok";

        [Header("Panels")]
        [Tooltip("Shown when the selected service has no saved library: the Link buttons and privacy line.")]
        public GameObject linkPanel;
        [Tooltip("Shown while an activation code is on screen.")]
        public GameObject codePanel;
        [Tooltip("Shown when a library is loaded: playlist and track browser.")]
        public GameObject libraryPanel;

        [Header("Service tabs (optional)")]
        public Button youTubeTab;
        public Button spotifyTab;
        public Button tiktokTab;

        [Header("Code panel")]
        public TextMeshProUGUI codeText;
        public TextMeshProUGUI codeUrlText;
        public TextMeshProUGUI countdownText;
        [Tooltip("Shown instead of the countdown once the code expires.")]
        public Button newCodeButton;

        [Header("Library panel")]
        public TextMeshProUGUI headerText;
        public Button backButton;
        public Button prevPageButton;
        public Button nextPageButton;
        public TextMeshProUGUI pageText;
        public GameObject[] rows = new GameObject[MaxRows];
        public TextMeshProUGUI[] rowTitles = new TextMeshProUGUI[MaxRows];
        public TextMeshProUGUI[] rowDetails = new TextMeshProUGUI[MaxRows];

        [Header("Shared")]
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI privacyText;

        [Header("Text")]
        public string privacyLine = "Only send playlists you're OK sharing: anyone with your library number could load them.";
        public string untrustedUrlsHint = "Couldn't reach BetterYTS. Turn on Allow Untrusted URLs in Settings > Comfort & Safety, then try again.";

        private IUdonEventReceiver _receiver;
        private bool _restored;

        // Saved library numbers (-1 = none), indexed by service.
        private int[] _saved;
        // Loaded libraries, indexed by service (null = not loaded yet).
        private DataList _ytPlaylists;
        private DataList _spPlaylists;
        private DataList _ttPlaylists;
        private string[] _accounts;

        private int _service = ServiceYouTube;
        private int _view = ViewLink;

        // Request in flight
        private int _request = ReqNone;
        private int _requestService;
        private float _lastRequestTime = -100f;

        // Activation code
        private bool _codeActive;
        private int _codeService;
        private int _pollSlot = -1;
        private float _expiresAt;
        private bool _ticking;

        // Browser
        private int _playlist = -1; // -1 = playlist list
        private int _page;

        private void Start()
        {
            _receiver = (IUdonEventReceiver)this;
            _saved = new int[] { -1, -1, -1 };
            _accounts = new string[] { "", "", "" };
            if (privacyText != null) privacyText.text = privacyLine;
            ShowView(ViewBusy);
            SetStatus("Loading your saved playlists…");
        }

        // ------------------------------------------------------------------ PlayerData

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (player == null || !player.isLocal) return;
            _restored = true;
            _saved[ServiceYouTube] = ReadSaved(player, youTubeKey);
            _saved[ServiceSpotify] = ReadSaved(player, spotifyKey);
            _saved[ServiceTikTok] = ReadSaved(player, tiktokKey);
            // Open on the first service with a saved library (YouTube if none).
            _service = ServiceYouTube;
            if (_saved[ServiceYouTube] < 0)
            {
                if (_saved[ServiceSpotify] >= 0) _service = ServiceSpotify;
                else if (_saved[ServiceTikTok] >= 0) _service = ServiceTikTok;
            }
            SetStatus("");
            Refresh();
        }

        private int ReadSaved(VRCPlayerApi player, string key)
        {
            int value;
            if (PlayerData.TryGetInt(player, key, out value)) return value;
            return -1;
        }

        private void Save(int service, int library)
        {
            _saved[service] = library;
            // PlayerData has no delete, so -1 means "no library".
            PlayerData.SetInt(KeyFor(service), library);
        }

        // ------------------------------------------------------------------ UI events

        public void _LinkYouTube() { StartLink(ServiceYouTube); }
        public void _LinkSpotify() { StartLink(ServiceSpotify); }
        public void _LinkTikTok() { StartLink(ServiceTikTok); }

        /// <summary>Get a new code for the service whose code expired (or is showing).</summary>
        public void _NewCode() { StartLink(_codeService); }

        public void _CancelCode()
        {
            _codeActive = false;
            SetStatus("");
            Refresh();
        }

        public void _ShowYouTube() { SelectService(ServiceYouTube); }
        public void _ShowSpotify() { SelectService(ServiceSpotify); }
        public void _ShowTikTok() { SelectService(ServiceTikTok); }

        /// <summary>Forget the saved library for the selected service.</summary>
        public void _Unlink()
        {
            if (!_restored) return;
            Save(_service, -1);
            SetPlaylists(_service, null, "");
            _playlist = -1;
            _page = 0;
            SetStatus("Unlinked. Your playlists are no longer saved in this world.");
            Refresh();
        }

        /// <summary>Reload the selected library (fresh slots; uses one library load).</summary>
        public void _Reload()
        {
            if (_saved[_service] >= 0) LoadLibrary(_service);
        }

        public void _Back()
        {
            _playlist = -1;
            _page = 0;
            DrawBrowser();
        }

        public void _PrevPage()
        {
            if (_page > 0) _page--;
            DrawBrowser();
        }

        public void _NextPage()
        {
            if (_page < PageCount() - 1) _page++;
            DrawBrowser();
        }

        public void _Row0() { ClickRow(0); }
        public void _Row1() { ClickRow(1); }
        public void _Row2() { ClickRow(2); }
        public void _Row3() { ClickRow(3); }
        public void _Row4() { ClickRow(4); }
        public void _Row5() { ClickRow(5); }
        public void _Row6() { ClickRow(6); }
        public void _Row7() { ClickRow(7); }
        public void _Row8() { ClickRow(8); }
        public void _Row9() { ClickRow(9); }

        // ------------------------------------------------------------------ Flow

        private void SelectService(int service)
        {
            _service = service;
            _playlist = -1;
            _page = 0;
            SetStatus("");
            Refresh();
        }

        /// <summary>Pick the right view for the selected service, loading its library if needed.</summary>
        private void Refresh()
        {
            UpdateTabs();
            if (_codeActive) { ShowView(ViewCode); return; }
            if (!_restored) { ShowView(ViewBusy); return; }
            if (GetPlaylists(_service) != null) { ShowView(ViewLibrary); DrawBrowser(); return; }
            if (_saved[_service] >= 0) { LoadLibrary(_service); return; }
            ShowView(ViewLink);
        }

        private void StartLink(int service)
        {
            if (_request != ReqNone) { SetStatus("One moment…"); return; }
            VRCUrl url = StartUrlFor(service);
            if (url == null || url.Get().Length == 0)
            {
                if (service == ServiceTikTok) { SetStatus(TikTokUnavailable); return; }
                SetStatus("This panel's link URLs aren't baked. Ask the world creator to press Bake.");
                return;
            }
            _service = service;
            _codeActive = false;
            UpdateTabs();
            ShowView(ViewBusy);
            SetStatus("Getting a code…");
            Send(ReqStart, service, url);
        }

        private void LoadLibrary(int service)
        {
            if (_request != ReqNone) return;
            if (libraryPool == null) { SetStatus("No saved-library URLs are linked to this panel."); ShowView(ViewLink); return; }
            VRCUrl url = libraryPool.GetUrl(_saved[service]);
            if (url == null)
            {
                SetStatus("This panel's library URLs need re-baking (10000 slots).");
                ShowView(ViewLink);
                return;
            }
            ShowView(ViewBusy);
            SetStatus("Loading your " + ServiceName(service) + " playlists…");
            Send(ReqLibrary, service, url);
        }

        private void Send(int kind, int service, VRCUrl url)
        {
            _request = kind;
            _requestService = service;
            _lastRequestTime = Time.realtimeSinceStartup;
            VRCStringDownloader.LoadUrl(url, _receiver);
        }

        /// <summary>Runs once a second while a code is on screen: countdown and polling.</summary>
        public void _Tick()
        {
            if (!_codeActive) { _ticking = false; return; }

            float left = _expiresAt - Time.realtimeSinceStartup;
            if (left <= 0f)
            {
                CodeExpired();
                _ticking = false;
                return;
            }
            int secs = Mathf.CeilToInt(left);
            int s = secs % 60;
            if (countdownText != null) countdownText.text = "Code expires in " + (secs / 60) + ":" + (s < 10 ? "0" : "") + s;

            if (_request == ReqNone && Time.realtimeSinceStartup - _lastRequestTime >= PollSeconds && pool != null)
            {
                VRCUrl url = pool.GetUrl(_pollSlot);
                if (url != null) Send(ReqPoll, _codeService, url);
            }
            SendCustomEventDelayedSeconds(nameof(_Tick), 1f);
        }

        private void ShowCode(int service, string code, string url, int expiresIn, int poll)
        {
            _codeActive = true;
            _codeService = service;
            _pollSlot = poll;
            _expiresAt = Time.realtimeSinceStartup + Mathf.Max(30, expiresIn);
            if (codeText != null) codeText.text = code;
            if (codeUrlText != null) codeUrlText.text = url;
            if (newCodeButton != null) newCodeButton.gameObject.SetActive(false);
            if (countdownText != null) countdownText.gameObject.SetActive(true);
            ShowView(ViewCode);
            SetStatus("On your phone or PC, open the address above and enter the code. Waiting for your playlists…");
            if (!_ticking)
            {
                _ticking = true;
                SendCustomEventDelayedSeconds(nameof(_Tick), 1f);
            }
        }

        private void CodeExpired()
        {
            _codeActive = false;
            if (codeText != null) codeText.text = "––––-––––";
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (newCodeButton != null) newCodeButton.gameObject.SetActive(true);
            ShowView(ViewCode);
            SetStatus("That code expired. Get a new one to try again.");
        }

        // ------------------------------------------------------------------ Responses

        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            int kind = _request;
            int service = _requestService;
            _request = ReqNone;

            DataToken root;
            if (!VRCJson.TryDeserializeFromJson(result.Result, out root) || root.TokenType != TokenType.DataDictionary)
            {
                Failed(kind, "BetterYTS sent something unexpected. Try again.");
                return;
            }
            DataDictionary data = root.DataDictionary;

            DataToken version;
            if (data.TryGetValue("v", TokenType.Double, out version) && (int)version.Double != ProtocolVersion)
            {
                Failed(kind, "This panel needs an update (BetterYTS protocol changed).");
                return;
            }
            DataToken ok;
            if (data.TryGetValue("ok", TokenType.Boolean, out ok) && !ok.Boolean)
            {
                string err = ReadString(data, "error");
                if (kind == ReqStart && service == ServiceTikTok) err = TikTokStartError(err);
                Failed(kind, err.Length > 0 ? err : "BetterYTS couldn't do that. Try again.");
                return;
            }

            if (kind == ReqStart) { OnStart(service, data); return; }

            string status = ReadString(data, "status");
            if (kind == ReqPoll)
            {
                if (!_codeActive || service != _codeService) return; // Cancelled meanwhile.
                if (status == "waiting") return; // _Tick polls again.
                if (status == "linked") { _codeActive = false; OnLinked(service, data, true); return; }
                if (status == "expired") { CodeExpired(); return; }
                return;
            }
            if (kind == ReqLibrary)
            {
                if (status == "linked") { OnLinked(service, data, false); return; }
                if (status == "missing")
                {
                    Save(service, -1);
                    SetPlaylists(service, null, "");
                    SetStatus("Your saved " + ServiceName(service) + " playlists expired. Link again to bring them back.");
                    if (service == _service) Refresh();
                    return;
                }
                Failed(kind, "BetterYTS sent something unexpected. Try again.");
            }
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            int kind = _request;
            int service = _requestService;
            _request = ReqNone;
            int code = result.ErrorCode;
            if (kind == ReqPoll) return; // Transient; keep polling until the code expires.
            // BetterYTS errors carry { ok:false, error:"…" }. Show that when VRChat passes the body on.
            string serverError = ErrorFromBody(result.Result);
            bool unreachable = code == 0 || code == 401 || code == 403;
            if (kind == ReqStart && service == ServiceTikTok && !unreachable && code != 429)
            {
                // The server may not offer TikTok yet (no endpoint, or the app isn't approved).
                Failed(kind, TikTokStartError(serverError));
                return;
            }
            if (serverError.Length > 0) Failed(kind, serverError);
            else if (code == 0 || code == 401 || code == 403) Failed(kind, untrustedUrlsHint);
            else if (code == 429) Failed(kind, "BetterYTS is busy for this connection. Try again in a few minutes.");
            else Failed(kind, "BetterYTS error " + code + ". Try again in a moment.");
        }

        /// <summary>
        /// What to show when a TikTok start request fails: the server's own message, unless it's
        /// missing or just "Unknown service" (the server has no TikTok endpoint yet).
        /// </summary>
        private string TikTokStartError(string serverError)
        {
            if (serverError == null || serverError.Length == 0 || serverError == "Unknown service") return TikTokUnavailable;
            return serverError;
        }

        /// <summary>The "error" text of a BetterYTS error body, or "" if there's no usable body.</summary>
        private string ErrorFromBody(string body)
        {
            if (body == null || body.Length == 0) return "";
            DataToken root;
            if (!VRCJson.TryDeserializeFromJson(body, out root) || root.TokenType != TokenType.DataDictionary) return "";
            return ReadString(root.DataDictionary, "error");
        }

        private void Failed(int kind, string message)
        {
            SetStatus(message);
            if (kind == ReqPoll) return;
            if (kind == ReqLibrary && GetPlaylists(_service) == null) { ShowView(ViewLink); UpdateTabs(); return; }
            Refresh();
        }

        private void OnStart(int service, DataDictionary data)
        {
            string code = ReadString(data, "code");
            int poll = ReadInt(data, "poll", -1);
            if (pool == null) { Failed(ReqStart, "No URL pool is linked to this panel."); return; }
            if (code.Length == 0 || poll < 0) { Failed(ReqStart, "BetterYTS sent something unexpected. Try again."); return; }
            if (pool.GetUrl(poll) == null) { Failed(ReqStart, "This world's URL pool is too small. Re-bake it with 20000 slots."); return; }
            string url = ReadString(data, "url");
            if (url.Length == 0) url = "yts.foulfoxhacks.dev/inworld/" + ServiceSlug(service) + "/auth";
            ShowCode(service, code, url, ReadInt(data, "expiresIn", 600), poll);
        }

        private void OnLinked(int service, DataDictionary data, bool fromCode)
        {
            // Save the (new) library number straight away: the old one stops working once this one is used.
            int library = ReadInt(data, "library", -1);
            if (library >= 0) Save(service, library);

            DataToken listToken;
            DataList playlists = data.TryGetValue("playlists", TokenType.DataList, out listToken) ? listToken.DataList : new DataList();
            SetPlaylists(service, playlists, ReadString(data, "account"));

            _service = service;
            _playlist = -1;
            _page = 0;
            UpdateTabs();
            ShowView(ViewLibrary);
            DrawBrowser();
            int count = Mathf.Min(playlists.Count, MaxPlaylists);
            SetStatus(fromCode
                ? "Linked! " + count + (count == 1 ? " playlist" : " playlists") + " saved for your next visit."
                : "");
        }

        // ------------------------------------------------------------------ Browser

        private void DrawBrowser()
        {
            DataList playlists = GetPlaylists(_service);
            if (playlists == null) return;

            string account = _accounts[_service];
            string who = ServiceName(_service) + (account.Length > 0 ? " · " + account : "");
            int total = ItemCount();
            int pages = PageCount();
            if (_page >= pages) _page = pages - 1;
            if (_page < 0) _page = 0;
            int first = _page * MaxRows;

            DataDictionary pl = _playlist >= 0 ? GetPlaylist(_playlist) : null;
            DataList tracks = pl != null ? GetTracks(pl) : null;

            if (headerText != null)
            {
                headerText.text = pl != null
                    ? ReadString(pl, "name") + "  <size=70%><color=#a79fb2>" + who + "</color></size>"
                    : who;
            }

            for (int r = 0; r < MaxRows; r++)
            {
                int i = first + r;
                bool used = i < total;
                if (r < rows.Length && rows[r] != null) rows[r].SetActive(used);
                if (!used) continue;

                string title = "";
                string detail = "";
                if (tracks == null)
                {
                    DataDictionary p = GetPlaylist(i);
                    if (p != null)
                    {
                        title = ReadString(p, "name");
                        int n = TrackCountFor(i);
                        detail = n + (n == 1 ? " song" : " songs");
                    }
                }
                else
                {
                    DataToken t;
                    if (tracks.TryGetValue(i, TokenType.DataDictionary, out t))
                    {
                        title = ReadString(t.DataDictionary, "title");
                        detail = ReadString(t.DataDictionary, "channel");
                        if (ReadInt(t.DataDictionary, "slot", -1) < 0) detail = "Unavailable";
                    }
                }
                if (r < rowTitles.Length && rowTitles[r] != null) rowTitles[r].text = title;
                if (r < rowDetails.Length && rowDetails[r] != null) rowDetails[r].text = detail;
            }

            if (backButton != null) backButton.gameObject.SetActive(pl != null);
            if (prevPageButton != null) prevPageButton.interactable = _page > 0;
            if (nextPageButton != null) nextPageButton.interactable = _page < pages - 1;
            if (pageText != null) pageText.text = pages > 1 ? (_page + 1) + " / " + pages : "";
            if (total == 0) SetStatus(pl != null ? "This playlist is empty." : "No playlists in this library.");
        }

        private void ClickRow(int r)
        {
            if (_view != ViewLibrary) return;
            int i = _page * MaxRows + r;
            if (i >= ItemCount()) return;

            if (_playlist < 0)
            {
                _playlist = i;
                _page = 0;
                SetStatus("");
                DrawBrowser();
                return;
            }

            DataDictionary pl = GetPlaylist(_playlist);
            DataList tracks = pl != null ? GetTracks(pl) : null;
            DataToken t;
            if (tracks == null || !tracks.TryGetValue(i, TokenType.DataDictionary, out t)) return;
            string title = ReadString(t.DataDictionary, "title");
            int slot = ReadInt(t.DataDictionary, "slot", -1);
            if (videoTarget == null) { SetStatus("No video player is linked to this panel."); return; }
            if (pool == null) { SetStatus("No URL pool is linked to this panel."); return; }
            if (slot < 0) { SetStatus("That song isn't available right now. Try Reload."); return; }
            VRCUrl url = pool.GetUrl(slot);
            if (url == null) { SetStatus("This world's URL pool is too small. Re-bake it with 20000 slots."); return; }
            videoTarget._PlayUrl(url, title);
            SetStatus("Playing: " + title);
        }

        /// <summary>Rows in the current list (playlists, or the open playlist's tracks), within the limits.</summary>
        private int ItemCount()
        {
            DataList playlists = GetPlaylists(_service);
            if (playlists == null) return 0;
            int lists = Mathf.Min(playlists.Count, MaxPlaylists);
            if (_playlist < 0) return lists;
            if (_playlist >= lists) return 0;
            return TrackCountFor(_playlist);
        }

        /// <summary>Tracks shown for playlist <paramref name="index"/>, keeping the whole library within MaxTracks.</summary>
        private int TrackCountFor(int index)
        {
            int before = 0;
            for (int p = 0; p < index; p++)
            {
                DataDictionary prev = GetPlaylist(p);
                if (prev != null) before += GetTracks(prev).Count;
            }
            DataDictionary pl = GetPlaylist(index);
            if (pl == null) return 0;
            return Mathf.Max(0, Mathf.Min(GetTracks(pl).Count, MaxTracks - before));
        }

        private int PageCount()
        {
            return Mathf.Max(1, (ItemCount() + MaxRows - 1) / MaxRows);
        }

        private DataDictionary GetPlaylist(int index)
        {
            DataList playlists = GetPlaylists(_service);
            DataToken t;
            if (playlists == null || index >= MaxPlaylists || !playlists.TryGetValue(index, TokenType.DataDictionary, out t)) return null;
            return t.DataDictionary;
        }

        private DataList GetTracks(DataDictionary playlist)
        {
            DataToken t;
            return playlist.TryGetValue("tracks", TokenType.DataList, out t) ? t.DataList : new DataList();
        }

        private DataList GetPlaylists(int service)
        {
            if (service == ServiceSpotify) return _spPlaylists;
            if (service == ServiceTikTok) return _ttPlaylists;
            return _ytPlaylists;
        }

        private void SetPlaylists(int service, DataList playlists, string account)
        {
            if (service == ServiceSpotify) _spPlaylists = playlists;
            else if (service == ServiceTikTok) _ttPlaylists = playlists;
            else _ytPlaylists = playlists;
            _accounts[service] = account;
        }

        // ------------------------------------------------------------------ Helpers

        private void ShowView(int view)
        {
            _view = view;
            if (linkPanel != null) linkPanel.SetActive(view == ViewLink);
            if (codePanel != null) codePanel.SetActive(view == ViewCode);
            if (libraryPanel != null) libraryPanel.SetActive(view == ViewLibrary);
        }

        private void UpdateTabs()
        {
            if (youTubeTab != null) youTubeTab.interactable = _service != ServiceYouTube;
            if (spotifyTab != null) spotifyTab.interactable = _service != ServiceSpotify;
            if (tiktokTab != null) tiktokTab.interactable = _service != ServiceTikTok;
        }

        private string ServiceName(int service)
        {
            if (service == ServiceSpotify) return "Spotify";
            if (service == ServiceTikTok) return "TikTok";
            return "YouTube";
        }

        private string ServiceSlug(int service)
        {
            if (service == ServiceSpotify) return "spotify";
            if (service == ServiceTikTok) return "tiktok";
            return "youtube";
        }

        private string KeyFor(int service)
        {
            if (service == ServiceSpotify) return spotifyKey;
            if (service == ServiceTikTok) return tiktokKey;
            return youTubeKey;
        }

        private VRCUrl StartUrlFor(int service)
        {
            if (service == ServiceSpotify) return startSpotifyUrl;
            if (service == ServiceTikTok) return startTikTokUrl;
            return startYouTubeUrl;
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
