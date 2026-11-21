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
    /// BetterYTS in-world account linking and saved libraries (linking protocol v2:
    /// https://github.com/foulfoxhacks/betteryts/blob/main/docs/vrchat-protocol.md#linking).
    ///
    /// Works like a TV sign-in: <c>Link YouTube</c> / <c>Link Spotify</c> / <c>Link TikTok</c> loads a
    /// baked start URL that returns an activation code. The player enters it at the shown address on
    /// their phone, picks what to send and whether to save it, and this script polls <c>pool[poll]</c>
    /// until it arrives.
    ///
    /// Saving is optional. A saved library comes with a two-part key (a, b) kept in PlayerData.
    /// Next visit: load <c>…/vrc/l/{a}</c>, then <c>…/vrc/k/{b}</c> from the same client within 60 s. Every
    /// load hands out a NEW key, saved at once. Unlink loads <c>…/vrc/forget</c> right after a load or
    /// link, which deletes the library on the server.
    ///
    /// Everything is local to the player. Bake the URLs with the inspector button, or build a
    /// whole panel with Tools > FoulFoxHacks > Create BetterYTS Link Panel.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BetterYTSLink : UdonSharpBehaviour
    {
        private const int ProtocolVersion = 2;
        public const int ServiceYouTube = 0;
        public const int ServiceSpotify = 1;
        public const int ServiceTikTok = 2;
        public const string TikTokUnavailable = "TikTok linking isn't available yet.";
        public const int MaxRows = 10;
        private const int MaxPlaylists = 20;
        private const int MaxTracks = 1000;
        private const float StepSeconds = 5.5f; // String downloads are limited to one per 5 s.
        private const float ForgetWindow = 50f; // The server remembers the last load for 60 s.

        private const int ReqNone = 0;
        private const int ReqStart = 1;
        private const int ReqPoll = 2;
        private const int ReqLibraryA = 3;
        private const int ReqLibraryB = 4;
        private const int ReqForget = 5;

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
        [Tooltip("…/vrc/forget: deletes the library this client just loaded.")]
        public VRCUrl forgetUrl;
        [Tooltip("Baked BetterYTS URL pool (…/vrc/p/{slot}, 20000 slots). Used for polling and playing.")]
        public FFHUrlPool pool;
        [Tooltip("Saved library, step 1 (…/vrc/l/{slot}, 10000 slots).")]
        public FFHUrlPool libraryPool;
        [Tooltip("Saved library, step 2 (…/vrc/k/{slot}, 10000 slots).")]
        public FFHUrlPool keyPool;
        [Tooltip("Where songs play: ProTV adapter, VRChat player target, or Udon event target.")]
        public FFHVideoTarget videoTarget;

        [Header("PlayerData")]
        [Tooltip("Keys are {prefix}{service}.a and {prefix}{service}.b, e.g. betteryts.lib.youtube.a")]
        public string playerDataPrefix = "betteryts.lib.";

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
        public string privacyLine = "Only send what you're OK sharing. Saving for your next visit is optional and lasts 30 days; Unlink deletes it. Privacy: yts.foulfoxhacks.dev/privacy";
        public string manageAddress = "yts.foulfoxhacks.dev/?view=link#manage";
        public string untrustedUrlsHint = "Couldn't reach BetterYTS. Turn on Allow Untrusted URLs in Settings > Comfort & Safety, then try again.";

        private IUdonEventReceiver _receiver;
        private bool _restored;

        // Saved key halves (-1 = none), indexed by service.
        private int[] _savedA;
        private int[] _savedB;
        // Loaded libraries, indexed by service (null = not loaded).
        private DataList _ytPlaylists;
        private DataList _spPlaylists;
        private DataList _ttPlaylists;
        private string[] _accounts;

        private int _service = ServiceYouTube;
        private int _view = ViewLink;

        // Request in flight, and the next step of a multi-step load/unlink.
        private int _request = ReqNone;
        private int _requestService;
        private float _lastRequestTime = -100f;
        private int _nextStep = ReqNone;
        private int _nextStepService;
        private bool _unlinkAfterLoad;

        // What this client last received (the server's /vrc/forget acts on it).
        private int _lastReceivedService = -1;
        private float _lastReceivedTime = -1000f;

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
            _savedA = new int[] { -1, -1, -1 };
            _savedB = new int[] { -1, -1, -1 };
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
            for (int s = 0; s < 3; s++)
            {
                _savedA[s] = ReadSaved(player, KeyFor(s) + ".a");
                _savedB[s] = ReadSaved(player, KeyFor(s) + ".b");
            }
            // Open on the first service with a saved library (YouTube if none).
            _service = ServiceYouTube;
            if (!HasSaved(ServiceYouTube))
            {
                if (HasSaved(ServiceSpotify)) _service = ServiceSpotify;
                else if (HasSaved(ServiceTikTok)) _service = ServiceTikTok;
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

        private bool HasSaved(int service)
        {
            return _savedA[service] >= 0 && _savedB[service] >= 0;
        }

        private void Save(int service, int a, int b)
        {
            _savedA[service] = a;
            _savedB[service] = b;
            // PlayerData has no delete, so -1 means "no library".
            PlayerData.SetInt(KeyFor(service) + ".a", a);
            PlayerData.SetInt(KeyFor(service) + ".b", b);
        }

        private void ClearSaved(int service)
        {
            Save(service, -1, -1);
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

        /// <summary>Unlink the selected service: delete its library on BetterYTS, then forget the key.</summary>
        public void _Unlink()
        {
            if (!_restored) return;
            if (Busy()) { SetStatus("One moment…"); return; }
            int service = _service;

            // The server deletes what this client last received, if that was within 60 s.
            if (_lastReceivedService == service && Time.realtimeSinceStartup - _lastReceivedTime < ForgetWindow)
            {
                SetStatus("Unlinking…");
                Schedule(ReqForget, service);
                return;
            }
            // Otherwise load it first (the key rotates), then forget it.
            if (HasSaved(service))
            {
                _unlinkAfterLoad = true;
                LoadLibrary(service);
                return;
            }
            // Nothing saved and nothing recent: only this visit's copy to drop.
            UnlinkLocal(service, "Unlinked. Nothing was saved on BetterYTS.");
        }

        /// <summary>Reload the selected library (fresh slots and a new key).</summary>
        public void _Reload()
        {
            if (HasSaved(_service)) LoadLibrary(_service);
            else SetStatus("This list is only for this visit, so there's nothing to reload.");
        }

        /// <summary>Show the key the player can use on the site to view, download or delete their library.</summary>
        public void _ShowKey()
        {
            if (!HasSaved(_service))
            {
                SetStatus("Nothing is saved for " + ServiceName(_service) + ", so there's no key.");
                return;
            }
            SetStatus("Your key: " + Pad4(_savedA[_service]) + "-" + Pad4(_savedB[_service])
                + ". Enter it at " + manageAddress + " to view, download or delete your saved list. It changes every visit; don't share it.");
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

        private bool Busy()
        {
            return _request != ReqNone || _nextStep != ReqNone;
        }

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
            if (HasSaved(_service)) { LoadLibrary(_service); return; }
            ShowView(ViewLink);
        }

        private void StartLink(int service)
        {
            if (Busy()) { SetStatus("One moment…"); return; }
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

        /// <summary>Step 1 of a saved-library load: …/vrc/l/{a}. Step 2 follows in _RunNextStep.</summary>
        private void LoadLibrary(int service)
        {
            if (Busy()) return;
            if (libraryPool == null || keyPool == null)
            {
                _unlinkAfterLoad = false;
                SetStatus("This panel's saved-library URLs aren't baked. Ask the world creator to press Bake.");
                ShowView(ViewLink);
                return;
            }
            VRCUrl url = libraryPool.GetUrl(_savedA[service]);
            if (url == null || keyPool.GetUrl(_savedB[service]) == null)
            {
                _unlinkAfterLoad = false;
                SetStatus("This panel's library URLs need re-baking (10000 slots each).");
                ShowView(ViewLink);
                return;
            }
            ShowView(ViewBusy);
            SetStatus(_unlinkAfterLoad ? "Unlinking…" : "Loading your " + ServiceName(service) + " list…");
            Send(ReqLibraryA, service, url);
        }

        private void Send(int kind, int service, VRCUrl url)
        {
            _request = kind;
            _requestService = service;
            _lastRequestTime = Time.realtimeSinceStartup;
            VRCStringDownloader.LoadUrl(url, _receiver);
        }

        /// <summary>Queue the next request of a multi-step load or unlink, after the download gap.</summary>
        private void Schedule(int kind, int service)
        {
            _nextStep = kind;
            _nextStepService = service;
            float wait = StepSeconds - (Time.realtimeSinceStartup - _lastRequestTime);
            SendCustomEventDelayedSeconds(nameof(_RunNextStep), Mathf.Max(0.1f, wait));
        }

        public void _RunNextStep()
        {
            int kind = _nextStep;
            int service = _nextStepService;
            if (kind == ReqNone) return;
            if (_request != ReqNone) { SendCustomEventDelayedSeconds(nameof(_RunNextStep), 1f); return; }
            _nextStep = ReqNone;

            VRCUrl url = null;
            if (kind == ReqLibraryB && keyPool != null) url = keyPool.GetUrl(_savedB[service]);
            else if (kind == ReqForget) url = forgetUrl;
            if (url == null || url.Get().Length == 0)
            {
                _unlinkAfterLoad = false;
                Failed(kind, "This panel's link URLs need re-baking. Ask the world creator to press Bake.");
                return;
            }
            Send(kind, service, url);
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

            if (!Busy() && Time.realtimeSinceStartup - _lastRequestTime >= StepSeconds && pool != null)
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
            SetStatus("On your phone or PC, open the address above and enter the code. Waiting…");
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
                if (kind != ReqPoll) _unlinkAfterLoad = false;
                Failed(kind, "BetterYTS sent something unexpected. Try again.");
                return;
            }
            DataDictionary data = root.DataDictionary;

            DataToken version;
            if (data.TryGetValue("v", TokenType.Double, out version) && (int)version.Double != ProtocolVersion)
            {
                _unlinkAfterLoad = false;
                Failed(kind, "This panel needs an update (BetterYTS linking protocol changed).");
                return;
            }
            DataToken ok;
            if (data.TryGetValue("ok", TokenType.Boolean, out ok) && !ok.Boolean)
            {
                // Linking errors (rate limits included) come back as HTTP 200 { ok:false, error }.
                string err = ReadString(data, "error");
                if (kind == ReqStart && service == ServiceTikTok) err = TikTokStartError(err);
                if (kind == ReqPoll) return; // Keep polling until the code expires.
                bool unlinking = _unlinkAfterLoad || kind == ReqForget;
                _unlinkAfterLoad = false;
                if (unlinking) err = CouldntDelete(service, err);
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
            if (kind == ReqLibraryA)
            {
                // Step 1 never says whether the key exists; step 2 must follow from this client within 60 s.
                if (status == "next") { Schedule(ReqLibraryB, service); return; }
                _unlinkAfterLoad = false;
                Failed(kind, "BetterYTS sent something unexpected. Try again.");
                return;
            }
            if (kind == ReqLibraryB)
            {
                if (status == "linked")
                {
                    if (_unlinkAfterLoad)
                    {
                        _unlinkAfterLoad = false;
                        // Keep the new key until the delete is confirmed, so a failed delete can be retried.
                        SaveKeyFrom(service, data);
                        MarkReceived(service);
                        Schedule(ReqForget, service);
                        return;
                    }
                    OnLinked(service, data, false);
                    return;
                }
                if (status == "missing")
                {
                    bool unlinking = _unlinkAfterLoad;
                    _unlinkAfterLoad = false;
                    ClearSaved(service);
                    SetPlaylists(service, null, "");
                    if (unlinking) { UnlinkLocal(service, "Unlinked. Nothing was saved on BetterYTS any more."); return; }
                    SetStatus("Your saved " + ServiceName(service) + " list expired or was deleted. Link again to bring it back.");
                    if (service == _service) Refresh();
                    return;
                }
                _unlinkAfterLoad = false;
                Failed(kind, "BetterYTS sent something unexpected. Try again.");
                return;
            }
            if (kind == ReqForget)
            {
                if (status == "forgotten") { UnlinkLocal(service, "Unlinked. Your saved " + ServiceName(service) + " list was deleted from BetterYTS."); return; }
                if (status == "nothing") { UnlinkLocal(service, "Unlinked. Nothing was saved on BetterYTS."); return; }
                Failed(kind, CouldntDelete(service, ""));
            }
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            int kind = _request;
            int service = _requestService;
            _request = ReqNone;
            int code = result.ErrorCode;
            if (kind == ReqPoll) return; // Transient; keep polling until the code expires.

            bool unlinking = _unlinkAfterLoad || kind == ReqForget;
            _unlinkAfterLoad = false;
            string serverError = ErrorFromBody(result.Result);
            bool unreachable = code == 0 || code == 401 || code == 403;
            string message;
            if (kind == ReqStart && service == ServiceTikTok && !unreachable && code != 429) message = TikTokStartError(serverError);
            else if (serverError.Length > 0) message = serverError;
            else if (unreachable) message = untrustedUrlsHint;
            else if (code == 429) message = "BetterYTS is busy for this connection. Try again in a few minutes.";
            else message = "BetterYTS error " + code + ". Try again in a moment.";
            if (unlinking) message = CouldntDelete(service, message);
            Failed(kind, message);
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

        /// <summary>A failed unlink keeps the key, so the player can retry or delete it on the site.</summary>
        private string CouldntDelete(int service, string reason)
        {
            string msg = "Couldn't delete your list from BetterYTS" + (reason != null && reason.Length > 0 ? " (" + reason + ")" : "") + ". Try Unlink again";
            if (HasSaved(service)) msg += ", or delete it at " + manageAddress + " with key " + Pad4(_savedA[service]) + "-" + Pad4(_savedB[service]);
            return msg + ".";
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
            _nextStep = ReqNone;
            SetStatus(message);
            if (kind == ReqPoll) return;
            bool library = kind == ReqLibraryA || kind == ReqLibraryB || kind == ReqForget;
            if (library && GetPlaylists(_service) == null) { ShowView(ViewLink); UpdateTabs(); return; }
            if (library) { ShowView(ViewLibrary); DrawBrowser(); UpdateTabs(); return; }
            Refresh();
        }

        private void UnlinkLocal(int service, string message)
        {
            ClearSaved(service);
            SetPlaylists(service, null, "");
            if (_lastReceivedService == service) _lastReceivedService = -1;
            _playlist = -1;
            _page = 0;
            SetStatus(message);
            if (service == _service) { UpdateTabs(); ShowView(ViewLink); }
        }

        private void OnStart(int service, DataDictionary data)
        {
            string code = ReadString(data, "code");
            int poll = ReadInt(data, "poll", -1);
            if (pool == null) { Failed(ReqStart, "No URL pool is linked to this panel."); return; }
            if (code.Length == 0 || poll < 0) { Failed(ReqStart, "BetterYTS sent something unexpected. Try again."); return; }
            if (pool.GetUrl(poll) == null) { Failed(ReqStart, "This world's URL pool is too small. Re-bake it with 20000 slots."); return; }
            string url = ReadString(data, "url");
            if (url.Length == 0) url = "yts.foulfoxhacks.dev/inworld/" + ServiceSlug(service) + (service == ServiceTikTok ? "" : "/auth");
            ShowCode(service, code, url, ReadInt(data, "expiresIn", 600), poll);
        }

        /// <summary>Saves library {a, b} if present. Returns false for null ("just this visit").</summary>
        private bool SaveKeyFrom(int service, DataDictionary data)
        {
            DataToken libToken;
            if (!data.TryGetValue("library", TokenType.DataDictionary, out libToken)) return false;
            int a = ReadInt(libToken.DataDictionary, "a", -1);
            int b = ReadInt(libToken.DataDictionary, "b", -1);
            if (a < 0 || b < 0) return false;
            Save(service, a, b);
            return true;
        }

        private void MarkReceived(int service)
        {
            _lastReceivedService = service;
            _lastReceivedTime = Time.realtimeSinceStartup;
        }

        private void OnLinked(int service, DataDictionary data, bool fromCode)
        {
            // Save the (new) key straight away: the old one stops working once this one is used.
            bool saved = SaveKeyFrom(service, data);
            MarkReceived(service);

            DataToken listToken;
            DataList playlists = data.TryGetValue("playlists", TokenType.DataList, out listToken) ? listToken.DataList : new DataList();
            SetPlaylists(service, playlists, ReadString(data, "account"));

            _service = service;
            _playlist = -1;
            _page = 0;
            UpdateTabs();
            ShowView(ViewLibrary);
            DrawBrowser();
            if (!fromCode) { SetStatus(""); return; }
            int count = Mathf.Min(playlists.Count, MaxPlaylists);
            string what = count + (count == 1 ? " list" : " lists");
            SetStatus(saved
                ? "Linked! " + what + ", saved for your next visit (30 days). Unlink deletes it."
                : "Linked! " + what + " for this visit only. Nothing is saved.");
        }

        // ------------------------------------------------------------------ Browser

        private void DrawBrowser()
        {
            DataList playlists = GetPlaylists(_service);
            if (playlists == null) return;

            string account = _accounts[_service];
            string who = ServiceName(_service) + (account.Length > 0 ? " · " + account : "");
            if (!HasSaved(_service)) who += " · this visit only";
            int total = ItemCount();
            int pages = PageCount();
            if (_page >= pages) _page = pages - 1;
            if (_page < 0) _page = 0;
            int first = _page * MaxRows;

            DataDictionary pl = _playlist >= 0 ? GetPlaylist(_playlist) : null;
            DataList tracks = pl != null ? GetTracks(pl) : null;
            bool tiktok = _service == ServiceTikTok;

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
                        detail = n + (tiktok ? (n == 1 ? " video" : " videos") : (n == 1 ? " song" : " songs"));
                    }
                }
                else
                {
                    DataToken t;
                    if (tracks.TryGetValue(i, TokenType.DataDictionary, out t))
                    {
                        DataDictionary track = t.DataDictionary;
                        title = ReadString(track, "title");
                        detail = ReadString(track, "channel");
                        if (ReadInt(track, "slot", -1) < 0)
                        {
                            int secs = ReadInt(track, "duration", -1);
                            string length = secs >= 0 ? (secs / 60) + ":" + (secs % 60 < 10 ? "0" : "") + (secs % 60) : "";
                            detail = tiktok ? (length.Length > 0 ? length + " · " : "") + "on TikTok" : "Unavailable";
                        }
                    }
                }
                if (r < rowTitles.Length && rowTitles[r] != null) rowTitles[r].text = title;
                if (r < rowDetails.Length && rowDetails[r] != null) rowDetails[r].text = detail;
            }

            if (backButton != null) backButton.gameObject.SetActive(pl != null);
            if (prevPageButton != null) prevPageButton.interactable = _page > 0;
            if (nextPageButton != null) nextPageButton.interactable = _page < pages - 1;
            if (pageText != null) pageText.text = pages > 1 ? (_page + 1) + " / " + pages : "";
            if (total == 0) SetStatus(pl != null ? "This list is empty." : "Nothing in this list.");
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
            if (slot < 0 && _service == ServiceTikTok)
            {
                // TikTok gives no playable file, and BetterYTS doesn't download or re-host TikTok videos.
                SetStatus("TikTok videos can't play in VRChat. Watch \"" + title + "\" in the TikTok app.");
                return;
            }
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

        /// <summary>PlayerData key stem for a service, e.g. "betteryts.lib.youtube" (+ ".a" / ".b").</summary>
        private string KeyFor(int service)
        {
            return playerDataPrefix + ServiceSlug(service);
        }

        private VRCUrl StartUrlFor(int service)
        {
            if (service == ServiceSpotify) return startSpotifyUrl;
            if (service == ServiceTikTok) return startTikTokUrl;
            return startYouTubeUrl;
        }

        private string Pad4(int n)
        {
            string s = n.ToString();
            while (s.Length < 4) s = "0" + s;
            return s;
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
