using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Backrooms
{
    /// <summary>
    /// All screens: main menu (Solo, Host Co-Op, Join Co-Op, SCP Dossier), settings, credits, HUD (live Mic &amp; Voice Mimic
    /// indicator, Co-Op squad &amp; join code panel, TAB Field Dossier), pause, death, escape. Built in code.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        private Canvas _canvas;
        private RectTransform _menu, _coop, _dossier, _settings, _credits, _hud, _pause, _death, _win, _loading;
        private Image _fade, _stamina, _battery, _sanity, _mic;
        private CanvasGroup _staminaGroup, _batteryGroup, _sanityGroup, _micGroup, _titleGroup;
        private Text _prompt, _levelTitle, _levelSub, _deathTitle, _deathText, _winText, _creditsText, _loadingText, _toast;
        private Text _coopStatusText, _hudSquadText, _hudMicStatus, _hudTabDossierText, _dossierBodyText, _pauseRoomText, _menuRosterSub;
        private RectTransform _hudSquadPanel, _hudTabDossierPanel;
        private InputField _callsignInput, _joinCodeInput;
        private float _titleTimer, _toastTimer;
        private RectTransform _creditsScroll;
        private RectTransform _settingsReturn, _dossierReturn;
        private int _dossierPage;
        private GameBoot _game;

        private void Awake() => Instance = this;

        public void Build(GameBoot game)
        {
            Instance = this;
            _game = game;
            var cgo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            _canvas = cgo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
            Transform root = cgo.transform;

            BuildHud(root);
            BuildMenu(root);
            BuildCoopScreen(root);
            BuildDossierScreen(root);
            BuildSettings(root);
            BuildCredits(root);
            BuildPause(root);
            BuildDeath(root);
            BuildWin(root);
            BuildLoading(root);
            _fade = UIKit.Img(root, "Fade", Color.black, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _fade.raycastTarget = false;
            ShowOnly(null);
        }

        private RectTransform MakeScreen(Transform root, string name, bool darken)
        {
            var s = UIKit.Stretch(root, name);
            if (darken) UIKit.Img(s, "Shade", new Color(0, 0, 0, 0.72f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return s;
        }

        // ------------------------------------------------------------------ Main Menu
        private void BuildMenu(Transform root)
        {
            _menu = MakeScreen(root, "Menu", false);
            var left = UIKit.Img(_menu, "Left", new Color(0.03f, 0.03f, 0.015f, 0.85f), new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(800, 0), new Vector2(0, 0.5f));
            left.raycastTarget = false;
            UIKit.Label(_menu, "BACKROOMS", 100, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(400, -145), new Vector2(720, 125), FontStyle.Bold);
            UIKit.Label(_menu, "E S C A P E   ·   C O - O P", 32, UIKit.Accent, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(405, -255), new Vector2(720, 48), FontStyle.Bold);
            _menuRosterSub = UIKit.Label(_menu, "350+ Rotating SCP Anomalies · Live Mic Hearing & Voice Mimicry", 20, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(405, -305), new Vector2(720, 40));

            float x = -960 + 120 + 280, y = 115, step = 68;
            BtnSmall(_menu, "SOLO PLAY", new Vector2(x, y), () => _game.StartSoloRun());
            BtnSmall(_menu, "HOST CO-OP", new Vector2(x, y - step), () => _game.StartCoopHost());
            BtnSmall(_menu, "JOIN CO-OP", new Vector2(x, y - step * 2), OpenCoopScreen);
            BtnSmall(_menu, "SCP ARCHIVE (350+)", new Vector2(x, y - step * 3), () => OpenDossier(_menu));
            BtnSmall(_menu, "SETTINGS", new Vector2(x, y - step * 4), () => OpenSettings(_menu));
            BtnSmall(_menu, "CREDITS", new Vector2(x, y - step * 5), OpenCredits);
            BtnSmall(_menu, "QUIT", new Vector2(x, y - step * 6), _game.Quit);

            UIKit.Label(_menu, "v" + Application.version + "   ·   SCP content CC BY-SA 3.0 (see Credits)", 16, UIKit.Dim, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(410, 34), new Vector2(740, 28));
            UIKit.Label(_menu, "WASD move · SHIFT sprint · CTRL crouch · F light · V mute mic · TAB dossier · E interact/revive", 15, UIKit.Dim, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(410, 62), new Vector2(760, 28));
        }

        // ------------------------------------------------------------------ Co-Op Join / LAN Screen
        private void BuildCoopScreen(Transform root)
        {
            _coop = MakeScreen(root, "Coop", true);
            UIKit.Label(_coop, "CO-OP MULTIPLAYER", 58, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 320), new Vector2(1000, 80), FontStyle.Bold);
            UIKit.Label(_coop, "Explore together with proximity mic chat — careful: voice-stealing SCPs record what you say to bait your squad!", 21, UIKit.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 255), new Vector2(1200, 45));

            string defaultName = BackroomsNet.Instance != null ? BackroomsNet.Instance.MyName : "EXPLORER";
            _callsignInput = UIKit.Input(_coop, "Callsign", defaultName, "Your explorer name...", new Vector2(0, 155),
                v => BackroomsNet.Instance?.SetCallsign(v));
            _joinCodeInput = UIKit.Input(_coop, "Code or IP", "", "6-letter Code or IP:7778 (e.g. 192.168.1.10:7778)", new Vector2(0, 75), null);

            Btn(_coop, "JOIN BY CODE / IP", new Vector2(0, -25), () =>
            {
                BackroomsNet.Instance?.SetCallsign(_callsignInput.text);
                BackroomsNet.Instance?.JoinGame(_joinCodeInput.text);
            });
            Btn(_coop, "AUTO-FIND LAN / LOCAL GAME", new Vector2(0, -100), () =>
            {
                BackroomsNet.Instance?.SetCallsign(_callsignInput.text);
                BackroomsNet.Instance?.DiscoverLanAndJoin();
            });
            Btn(_coop, "HOST NEW CO-OP ROOM", new Vector2(0, -175), () =>
            {
                BackroomsNet.Instance?.SetCallsign(_callsignInput.text);
                _game.StartCoopHost();
            });

            _coopStatusText = UIKit.Label(_coop, "", 22, UIKit.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(1200, 50), FontStyle.Bold);
            Btn(_coop, "BACK", new Vector2(0, -345), () => { BackroomsNet.Instance?.StopNetwork(); ShowOnly(_menu); });
        }

        // ------------------------------------------------------------------ 350+ SCP Archive & Encounter Dossier Screen
        private void BuildDossierScreen(Transform root)
        {
            _dossier = MakeScreen(root, "Dossier", true);
            UIKit.Label(_dossier, "SCP CONTAINMENT BREACH ARCHIVE", 52, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 430), new Vector2(1400, 70), FontStyle.Bold);
            var panel = UIKit.Img(_dossier, "Card", new Color(0.05f, 0.045f, 0.025f, 0.92f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 15), new Vector2(1460, 720));
            _dossierBodyText = UIKit.Label(panel.transform, "", 20, UIKit.Ink, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _dossierBodyText.rectTransform.offsetMin = new Vector2(34, 26);
            _dossierBodyText.rectTransform.offsetMax = new Vector2(-34, -26);
            _dossierBodyText.supportRichText = true;

            UIKit.Btn(_dossier, "< PREV PAGE", new Vector2(-340, -420), new Vector2(280, 56), () => { _dossierPage = Mathf.Max(0, _dossierPage - 1); RefreshDossierPage(); }, 24);
            UIKit.Btn(_dossier, "NEXT PAGE >", new Vector2(340, -420), new Vector2(280, 56), () => { _dossierPage++; RefreshDossierPage(); }, 24);
            UIKit.Btn(_dossier, "BACK", new Vector2(0, -420), new Vector2(280, 56), () => ShowOnly(_dossierReturn ?? _menu), 26);
        }

        private void OpenDossier(RectTransform ret)
        {
            _dossierReturn = ret;
            _dossierPage = 0;
            RefreshDossierPage();
            ShowOnly(_dossier);
        }

        private void RefreshDossierPage()
        {
            if (EntityLibrary.Instance == null) return;
            var all = EntityLibrary.Instance.Defs;
            const int perPage = 14;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(all.Count / (float)perPage));
            _dossierPage = Mathf.Clamp(_dossierPage, 0, totalPages - 1);

            var sb = new StringBuilder();
            int logged = EntityDirector.Instance != null ? EntityDirector.Instance.EncounteredIds.Count : 0;
            sb.AppendLine($"<color=#C9A326><b>ROTATING ROSTER DATABASE: {all.Count} UNIQUE SCP ANOMALIES</b></color>   ·   {EntityDirector.RosterPerFloor} Drafted Per Floor   ·   Logged This Run: {logged}   ·   Page {_dossierPage + 1}/{totalPages}\n");

            int start = _dossierPage * perPage;
            int end = Mathf.Min(all.Count, start + perPage);
            for (int i = start; i < end; i++)
            {
                var d = all[i];
                bool seen = EntityDirector.Instance != null && EntityDirector.Instance.EncounteredIds.Contains(d.id);
                string tag = seen ? " <color=#E85040>[ENCOUNTERED]</color>" : "";
                string traits = (d.stealsVoices ? " · Voice-Mimic" : "") + (d.freezesWhenWatched ? " · Freeze-Watched" : "")
                              + (d.fearsFlashlight ? " · Fears-Light" : "") + (d.phaseThroughWalls ? " · Wall-Phaser" : "");
                sb.AppendLine($"<b>{d.name} — {d.nickname}</b>  <color=#B2A880>[{d.objectClass} / {d.behaviour}{traits}]</color>{tag}");
                sb.AppendLine($"<size=17><color=#B2A880>{d.description}</color></size>");
            }
            _dossierBodyText.text = sb.ToString();
        }

        private static Button Btn(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction a) =>
            UIKit.Btn(parent, label, pos, new Vector2(460, 62), a, 28);

        private static Button BtnSmall(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction a) =>
            UIKit.Btn(parent, label, pos, new Vector2(460, 56), a, 27);

        private void BuildSettings(Transform root)
        {
            _settings = MakeScreen(root, "Settings", true);
            UIKit.Label(_settings, "SETTINGS", 60, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(800, 80), FontStyle.Bold);
            var P = GameConfig.Current.player;
            UIKit.Slide(_settings, "Mouse sensitivity", new Vector2(0, 160), 0.02f, 0.5f, PlayerPrefs.GetFloat("sens", P.mouseSensitivity),
                v => PlayerPrefs.SetFloat("sens", v), v => v.ToString("0.00"));
            UIKit.Slide(_settings, "Field of view", new Vector2(0, 80), 60f, 100f, PlayerPrefs.GetFloat("fov", P.fov),
                v => { PlayerPrefs.SetFloat("fov", v); if (_game.Player != null) _game.Player.Cam.fieldOfView = v; }, v => v.ToString("0"));
            UIKit.Slide(_settings, "Volume", new Vector2(0, 0), 0f, 1f, PlayerPrefs.GetFloat("vol", 0.8f),
                v => { PlayerPrefs.SetFloat("vol", v); AudioListener.volume = v; }, v => Mathf.RoundToInt(v * 100) + "%");
            UIKit.Slide(_settings, "Brightness", new Vector2(0, -80), 0.6f, 1.6f, PlayerPrefs.GetFloat("bright", 1f),
                v => { PlayerPrefs.SetFloat("bright", v); _game.ApplyBrightness(); }, v => Mathf.RoundToInt(v * 100) + "%");
            Btn(_settings, "BACK", new Vector2(0, -260), () => { PlayerPrefs.Save(); ShowOnly(_settingsReturn); });
        }

        private void BuildCredits(Transform root)
        {
            _credits = MakeScreen(root, "Credits", true);
            var mask = UIKit.RT(_credits, "Mask", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1340, 820));
            mask.gameObject.AddComponent<RectMask2D>();
            _creditsScroll = UIKit.RT(mask, "Scroll", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1340, 10), new Vector2(0.5f, 1f));
            _creditsText = UIKit.Label(_creditsScroll, "", 22, UIKit.Ink, TextAnchor.UpperCenter, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 10));
            _creditsText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _creditsText.supportRichText = true;
            Btn(_credits, "BACK", new Vector2(0, -460), () => ShowOnly(_menu));
        }

        private void BuildPause(Transform root)
        {
            _pause = MakeScreen(root, "Pause", true);
            UIKit.Label(_pause, "PAUSED", 70, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 290), new Vector2(800, 90), FontStyle.Bold);
            _pauseRoomText = UIKit.Label(_pause, "", 22, UIKit.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(1100, 40), FontStyle.Bold);

            Btn(_pause, "RESUME", new Vector2(0, 130), () => _game.SetPaused(false));
            Btn(_pause, "COPY CO-OP CODE / IP", new Vector2(0, 55), () =>
            {
                if (BackroomsNet.IsOnline)
                {
                    GUIUtility.systemCopyBuffer = BackroomsNet.Instance.JoinCode;
                    _pauseRoomText.text = $"COPIED TO CLIPBOARD: {BackroomsNet.Instance.JoinCode}";
                }
            });
            Btn(_pause, "SCP ARCHIVE & LOG", new Vector2(0, -20), () => OpenDossier(_pause));
            Btn(_pause, "SETTINGS", new Vector2(0, -95), () => OpenSettings(_pause));
            Btn(_pause, "MAIN MENU", new Vector2(0, -170), _game.ToMenu);
            Btn(_pause, "QUIT", new Vector2(0, -245), _game.Quit);
        }

        private void BuildDeath(Transform root)
        {
            _death = MakeScreen(root, "Death", true);
            _deathTitle = UIKit.Label(_death, "YOU DIED", 96, UIKit.Danger, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1200, 120), FontStyle.Bold);
            _deathText = UIKit.Label(_death, "", 28, UIKit.Ink, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1200, 160));
            Btn(_death, "TRY AGAIN", new Vector2(0, -80), () => _game.StartRun());
            Btn(_death, "MAIN MENU", new Vector2(0, -160), _game.ToMenu);
        }

        private void BuildWin(Transform root)
        {
            _win = MakeScreen(root, "Win", true);
            UIKit.Label(_win, "YOU ESCAPED", 96, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1200, 120), FontStyle.Bold);
            _winText = UIKit.Label(_win, "", 28, UIKit.Ink, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1200, 160));
            Btn(_win, "PLAY AGAIN", new Vector2(0, -80), () => _game.StartRun());
            Btn(_win, "MAIN MENU", new Vector2(0, -160), _game.ToMenu);
        }

        private void BuildLoading(Transform root)
        {
            _loading = MakeScreen(root, "Loading", false);
            UIKit.Img(_loading, "Black", Color.black, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _loadingText = UIKit.Label(_loading, "Loading...", 28, UIKit.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 60));
        }

        // ------------------------------------------------------------------ HUD
        private void BuildHud(Transform root)
        {
            _hud = MakeScreen(root, "HUD", false);
            UIKit.Img(_hud, "Dot", new Color(1, 1, 1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 4));
            _prompt = UIKit.Label(_hud, "", 26, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(980, 44), FontStyle.Bold);

            _stamina = UIKit.Meter(_hud, "STAMINA", new Vector2(40, 40), UIKit.Ink, out _staminaGroup);
            _battery = UIKit.Meter(_hud, "FLASHLIGHT", new Vector2(40, 84), UIKit.Accent, out _batteryGroup);
            _sanity = UIKit.Meter(_hud, "SANITY", new Vector2(40, 128), new Color(0.55f, 0.7f, 0.9f), out _sanityGroup);
            _mic = UIKit.Meter(_hud, "MIC [V]", new Vector2(40, 172), new Color(0.45f, 0.85f, 0.52f), out _micGroup);
            _hudMicStatus = UIKit.Label(_hud, "", 15, UIKit.Danger, TextAnchor.LowerLeft, Vector2.zero, Vector2.zero, new Vector2(190, 206), new Vector2(360, 24), FontStyle.Bold);

            // Top-Right Co-Op Squad & Join Code Panel
            _hudSquadPanel = UIKit.RT(_hud, "CoopSquad", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -24), new Vector2(420, 190), new Vector2(1f, 1f));
            var sqBg = UIKit.Img(_hudSquadPanel, "Bg", new Color(0.04f, 0.04f, 0.02f, 0.72f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            sqBg.raycastTarget = false;
            _hudSquadText = UIKit.Label(_hudSquadPanel, "", 18, UIKit.Ink, TextAnchor.UpperRight, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _hudSquadText.rectTransform.offsetMin = new Vector2(14, 10);
            _hudSquadText.rectTransform.offsetMax = new Vector2(-14, -10);
            _hudSquadText.supportRichText = true;

            // Live [TAB] Field Dossier Card
            _hudTabDossierPanel = UIKit.RT(_hud, "TabDossier", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1050, 560));
            var tabBg = UIKit.Img(_hudTabDossierPanel, "Bg", new Color(0.04f, 0.035f, 0.02f, 0.90f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            tabBg.raycastTarget = false;
            _hudTabDossierText = UIKit.Label(_hudTabDossierPanel, "", 20, UIKit.Ink, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _hudTabDossierText.rectTransform.offsetMin = new Vector2(28, 24);
            _hudTabDossierText.rectTransform.offsetMax = new Vector2(-28, -24);
            _hudTabDossierText.supportRichText = true;
            _hudTabDossierPanel.gameObject.SetActive(false);

            var title = UIKit.RT(_hud, "LevelTitle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1200, 200));
            _titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            _levelTitle = UIKit.Label(title, "", 72, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1200, 90), FontStyle.Bold);
            _levelSub = UIKit.Label(title, "", 30, UIKit.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1200, 50));
            _toast = UIKit.Label(_hud, "", 22, UIKit.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(1400, 42), FontStyle.Bold);
        }

        // ------------------------------------------------------------------ Public API
        public void ShowOnly(RectTransform screen)
        {
            foreach (var s in new[] { _menu, _coop, _dossier, _settings, _credits, _hud, _pause, _death, _win, _loading })
                if (s != null) s.gameObject.SetActive(s == screen);
        }

        public void ShowMenu()
        {
            if (_menuRosterSub != null && EntityLibrary.Instance != null)
                _menuRosterSub.text = $"{EntityLibrary.Instance.Defs.Count} Rotating SCP Anomalies · Live Mic Hearing & Voice Mimicry";
            ShowOnly(_menu);
        }

        private void OpenCoopScreen()
        {
            if (_coopStatusText != null) _coopStatusText.text = "";
            ShowOnly(_coop);
        }

        public void ShowHud() => ShowOnly(_hud);

        public void ShowPause()
        {
            if (_pauseRoomText != null)
            {
                _pauseRoomText.text = BackroomsNet.IsOnline
                    ? $"CO-OP ROOM: {BackroomsNet.Instance.JoinCode}   ·   PLAYERS: {BackroomsNet.Instance.TotalPlayerCount}"
                    : $"SOLO RUN   ·   ROTATING POOL: {(EntityLibrary.Instance != null ? EntityLibrary.Instance.Defs.Count : 350)} SCPs";
            }
            ShowOnly(_pause);
        }

        public void ShowLoading(string text) { _loadingText.text = text; ShowOnly(_loading); }

        public void ShowDeath(string killer, string stats)
        {
            _deathTitle.text = "YOU DIED";
            _deathText.text = "Caught by <b>" + killer + "</b>\n\n" + stats;
            _deathText.supportRichText = true;
            ShowOnly(_death);
        }

        public void ShowWin(string stats) { _winText.text = stats; ShowOnly(_win); }

        public void LevelTitle(string title, string sub)
        {
            _levelTitle.text = title; _levelSub.text = sub; _titleTimer = 5f;
        }

        public void Toast(string text, float seconds = 4f) { _toast.text = text; _toastTimer = seconds; }

        public void SetFade(float a) => _fade.color = new Color(0, 0, 0, Mathf.Clamp01(a));

        private void OpenSettings(RectTransform from) { _settingsReturn = from; ShowOnly(_settings); }

        private void OpenCredits()
        {
            _creditsText.text = CreditsText();
            _creditsScroll.anchoredPosition = new Vector2(0, -820);
            ShowOnly(_credits);
        }

        /// <summary>Compact CC BY-SA 3.0 credits safe against Unity uGUI's 65k vertex mesh cap even with 350+ SCPs.</summary>
        public static string CreditsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<size=54><b>BACKROOMS ESCAPE</b></size>\n");
            sb.AppendLine("Solo & Co-Op Horror. Made with Unity.\n\n");
            sb.AppendLine("<size=34><b>ENTITIES &amp; SCP ARCHIVE</b></size>\n");
            sb.AppendLine("Based on works of the SCP Foundation community (scp-wiki.wikidot.com),\nlicensed under Creative Commons Attribution-ShareAlike 3.0 (CC BY-SA 3.0).\n");

            var procNames = new List<string>();
            if (EntityLibrary.Instance != null)
            {
                foreach (var d in EntityLibrary.Instance.Defs)
                {
                    if (EntityLibrary.UsesGltf(d))
                    {
                        var c = d.credit;
                        sb.AppendLine("<b>" + d.name + (string.IsNullOrEmpty(d.nickname) ? "" : " - " + d.nickname) + "</b>");
                        if (!string.IsNullOrEmpty(c.article)) sb.AppendLine("\"" + c.article + "\" by " + c.author + " (" + c.articleUrl + ")");
                        if (!string.IsNullOrEmpty(c.assets)) sb.AppendLine("3D Model & Sounds: " + c.assets);
                        sb.AppendLine(c.license + "  " + c.licenseUrl + "\n");
                    }
                    else
                    {
                        procNames.Add(d.name);
                    }
                }
            }

            if (procNames.Count > 0)
            {
                sb.AppendLine($"\n<size=30><b>ROTATING PROCEDURAL SCP CATALOG ({procNames.Count} ANOMALIES)</b></size>\n");
                sb.AppendLine("Original procedural 3D horror rigs & synthesised soundscapes inspired by SCP Wiki articles\nby their respective SCP Wiki authors (scp-wiki.wikidot.com), licensed under CC BY-SA 3.0.\nFull individual article URLs & descriptions are viewable in the in-game SCP Archive.\n");
                // List compactly 8 per line so total length stays well below uGUI 65k vertex limit!
                for (int i = 0; i < procNames.Count; i += 8)
                {
                    int take = Mathf.Min(8, procNames.Count - i);
                    sb.AppendLine(string.Join(" · ", procNames.GetRange(i, take)));
                }
            }

            sb.AppendLine("\n<size=34><b>SCP - CONTAINMENT BREACH</b></size>\n");
            sb.AppendLine("Bundled glTF 3D models, animations and sound effects from\nSCP - Containment Breach by Joonas Rikkonen (\"Regalis\") and contributors,\nlicensed under CC BY-SA 3.0. github.com/Regalis11/scpcb\n");
            sb.AppendLine("3D modellers: Regalis, Apocryphos, Atomicsneak, Brandon Smith, EternalError, GamerEntitlement, Hakkla,\nIvan Mikhailov, James Bear, Kenneth Crooker, Lionel Ruskin, Micro, Night, Prosth, TheKMan,\nReilly Grindle, SaitoFox, Sam Lynch, Shaun Gaylard, Tom Malinowski, Vladislav Borisevich\n");
            sb.AppendLine("Sound and music: Regalis, Caleb Yu, Class401, FireFox, gmodfan11, Kevin MacLeod, phlip45, revo794,\nShaun Gaylard, Slagar, Tim Morrison, Vladislav Borisevich, Walach\n");
            sb.AppendLine("Voice actors: TheVolgun, DukeLovesYou, CDWinters, Stealthy Zoroark, Hunter, ItsDuke, Joshofsouls,\nMoonstiee, SolidBaker, Tallfellow\n");
            sb.AppendLine("\n<size=34><b>LICENSE</b></size>\n");
            sb.AppendLine("All SCP-derived content in this game is shared under CC BY-SA 3.0.\nhttps://creativecommons.org/licenses/by-sa/3.0/\nSee LICENSE-ASSETS.md in the game folder.\n");
            sb.AppendLine("\nThanks for playing.");
            return sb.ToString();
        }

        private void Update()
        {
            if (_coop != null && _coop.gameObject.activeSelf && BackroomsNet.Instance != null)
                _coopStatusText.text = BackroomsNet.Instance.Status;

            var p = _game != null ? _game.Player : null;
            if (_hud != null && _hud.gameObject.activeSelf && p != null)
            {
                var P = GameConfig.Current.player;
                _stamina.fillAmount = p.Stamina / P.staminaMax;
                _staminaGroup.alpha = Mathf.MoveTowards(_staminaGroup.alpha, p.Stamina < P.staminaMax - 0.5f ? 1f : 0.18f, Time.deltaTime * 2f);
                _battery.fillAmount = p.Battery / P.flashlightBatteryMax;
                _batteryGroup.alpha = Mathf.MoveTowards(_batteryGroup.alpha, p.Flashlight.enabled ? 1f : 0.18f, Time.deltaTime * 2f);
                _sanity.fillAmount = p.Sanity / P.sanityMax;
                _sanityGroup.alpha = Mathf.MoveTowards(_sanityGroup.alpha, p.Sanity < P.sanityMax - 1f ? 1f : 0.18f, Time.deltaTime * 2f);
                _prompt.text = p.InteractPrompt ?? "";

                // Live Microphone & Voice Mimic HUD Meter
                var vm = VoiceMimicSystem.Instance;
                if (vm != null && vm.HasMic)
                {
                    _mic.fillAmount = vm.IsMuted ? 0f : Mathf.Max(0.04f, vm.MicLevel01);
                    _mic.color = vm.IsAudibleToMonsters ? UIKit.Danger : UIKit.Accent;
                    _micGroup.alpha = vm.IsMuted ? 0.35f : Mathf.Lerp(0.45f, 1f, vm.MicLevel01 * 2f);
                    _hudMicStatus.text = vm.IsMuted ? "MUTED"
                                       : vm.RecentlyRecorded ? "VOICE MIMICKED!"
                                       : vm.IsAudibleToMonsters ? "MONSTERS HEAR YOU"
                                       : "";
                }
                else
                {
                    _mic.fillAmount = 0f;
                    _micGroup.alpha = 0.15f;
                    _hudMicStatus.text = "NO MIC";
                }

                // Co-Op Squad Top-Right Panel
                bool online = BackroomsNet.IsOnline;
                _hudSquadPanel.gameObject.SetActive(online);
                if (online)
                {
                    var net = BackroomsNet.Instance;
                    var sb = new StringBuilder();
                    sb.AppendLine($"<color=#C9A326><b>CO-OP: {net.JoinCode}</b></color>");
                    string myState = p.Dead ? "<color=#CC2020>DEAD</color>" : p.Downed ? "<color=#FF6633>DOWNED</color>" : (vm != null && vm.IsSpeaking ? "<color=#C9A326>(( MIC ))</color>" : "OK");
                    sb.AppendLine($"{net.MyName} (YOU) — {myState}");
                    foreach (var kv in net.Avatars)
                    {
                        var av = kv.Value;
                        if (av == null) continue;
                        string st = av.Dead ? "<color=#CC2020>DEAD</color>" : av.Downed ? "<color=#FF6633>DOWNED!</color>" : av.Speaking ? "<color=#C9A326>(( MIC ))</color>" : "OK";
                        sb.AppendLine($"{av.PlayerName} — {st}");
                    }
                    sb.AppendLine($"<size=14><color=#B2A880>Stolen Voice Clips: {VoiceMimicBank.Phrases.Count}</color></size>");
                    _hudSquadText.text = sb.ToString();
                }

                // Hold [TAB] for quick in-run SCP Encounter & Breach Dossier
                bool holdTab = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
                if (_hudTabDossierPanel.gameObject.activeSelf != holdTab)
                {
                    _hudTabDossierPanel.gameObject.SetActive(holdTab);
                    if (holdTab) RefreshHudTabDossier();
                }
            }

            _titleTimer -= Time.unscaledDeltaTime;
            if (_titleGroup != null) _titleGroup.alpha = Mathf.Clamp01(_titleTimer > 4f ? (5f - _titleTimer) : _titleTimer / 1.5f);
            _toastTimer -= Time.unscaledDeltaTime;
            if (_toast != null) _toast.color = new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, Mathf.Clamp01(_toastTimer));
            if (_credits != null && _credits.gameObject.activeSelf)
            {
                _creditsScroll.anchoredPosition += new Vector2(0, 45f * Time.unscaledDeltaTime);
                if (_creditsScroll.anchoredPosition.y > _creditsText.preferredHeight + 100) _creditsScroll.anchoredPosition = new Vector2(0, -820);
            }
        }

        private void RefreshHudTabDossier()
        {
            var dir = EntityDirector.Instance;
            var lib = EntityLibrary.Instance;
            if (dir == null || lib == null) return;
            var sb = new StringBuilder();
            sb.AppendLine($"<size=28><color=#C9A326><b>FIELD CONTAINMENT DOSSIER</b></color></size>");
            sb.AppendLine($"Total Rotating Pool: <b>{lib.Defs.Count} SCPs</b>   ·   Active Floor Breach Roster: <b>{dir.ActiveRoster.Count} SCPs</b>   ·   Voice Clips Stolen: <b>{VoiceMimicBank.Phrases.Count}</b>\n");
            sb.AppendLine("<b>ANOMALIES ENCOUNTERED THIS RUN:</b>");
            if (dir.EncounteredIds.Count == 0)
            {
                sb.AppendLine("<color=#B2A880>None logged at close range yet. Listen for footsteps, light flickers, or familiar voices calling from the dark...</color>");
            }
            else
            {
                int shown = 0;
                foreach (string id in dir.EncounteredIds)
                {
                    if (shown++ >= 10) { sb.AppendLine("..."); break; }
                    var d = lib.Get(id);
                    if (d == null) continue;
                    sb.AppendLine($"• <b>{d.name} ({d.nickname})</b> [{d.objectClass} / {d.behaviour}] — <color=#B2A880>{d.description}</color>");
                }
            }
            _hudTabDossierText.text = sb.ToString();
        }
    }
}
