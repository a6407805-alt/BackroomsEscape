using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Backrooms
{
    /// <summary>All screens: main menu, settings, credits, HUD, pause, death, escape. Built in code.</summary>
    public class GameUI : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _menu, _settings, _credits, _hud, _pause, _death, _win, _loading;
        private Image _fade, _stamina, _battery, _sanity;
        private CanvasGroup _staminaGroup, _batteryGroup, _sanityGroup, _titleGroup;
        private Text _prompt, _levelTitle, _levelSub, _deathTitle, _deathText, _winText, _creditsText, _loadingText, _toast;
        private float _titleTimer, _toastTimer, _subTimer, _pointTimer;
        private Text _objective, _subtitle, _arrow, _arrowLabel, _pauseRoster;
        private Vector3 _pointAt;
        private RectTransform _creditsScroll;
        private RectTransform _settingsReturn;
        private GameBoot _game;

        public void Build(GameBoot game)
        {
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
            _root = root;

            BuildHud(root);
            BuildMenu(root);
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

        // ------------------------------------------------------------------ screens
        private RectTransform MakeScreen(Transform root, string name, bool darken)
        {
            var s = UIKit.Stretch(root, name);
            if (darken) UIKit.Img(s, "Shade", new Color(0, 0, 0, 0.6f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return s;
        }

        private void BuildMenu(Transform root)
        {
            _menu = MakeScreen(root, "Menu", false);
            var left = UIKit.Img(_menu, "Left", new Color(0.03f, 0.03f, 0.015f, 0.82f), new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(760, 0), new Vector2(0, 0.5f));
            left.raycastTarget = false;
            UIKit.Label(_menu, "BACKROOMS", 104, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(390, -170), new Vector2(700, 130), FontStyle.Bold);
            UIKit.Label(_menu, "E S C A P E", 34, UIKit.Accent, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(395, -285), new Vector2(700, 50), FontStyle.Bold);
                UIKit.Label(_menu, "Aaron's drum set is in pieces, scattered through the Backrooms. He is very angry.", 22, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(395, -335), new Vector2(700, 40));
            float x = -960 + 120 + 280, y = 40;
            Btn(_menu, "PLAY", new Vector2(x, y), () => _game.StartRun());
            Btn(_menu, "SETTINGS", new Vector2(x, y - 80), () => OpenSettings(_menu));
            Btn(_menu, "CREDITS", new Vector2(x, y - 160), OpenCredits);
            Btn(_menu, "QUIT", new Vector2(x, y - 240), _game.Quit);
            UIKit.Label(_menu, "v" + Application.version + "   ·   SCP content CC BY-SA 3.0 (see Credits)", 16, UIKit.Dim, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(400, 40), new Vector2(720, 30));
            UIKit.Label(_menu, "WASD move · SHIFT sprint · CTRL crouch · SPACE jump · F flashlight · E use · Q listen · ESC pause", 16, UIKit.Dim, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(400, 70), new Vector2(720, 30));
        }

        private static Button Btn(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction a) =>
            UIKit.Btn(parent, label, pos, new Vector2(460, 64), a, 32);

        private Transform _root;

        private void BuildSettings(Transform root)
        {
            _settings = MakeScreen(root, "Settings", true);
            UIKit.Label(_settings, "SETTINGS", 60, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 430), new Vector2(800, 80), FontStyle.Bold);
            var P = GameConfig.Current.player;
            float L = -470f, R = 470f, y0 = 320f, dy = 62f;

            // ---- left: controls & sound
            UIKit.Label(_settings, "CONTROLS & SOUND", 22, UIKit.Accent, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(L, y0 + 50), new Vector2(700, 30), FontStyle.Bold);
            UIKit.Slide(_settings, "Mouse sensitivity", new Vector2(L, y0), 0.02f, 0.5f, PlayerPrefs.GetFloat("sens", P.mouseSensitivity),
                v => PlayerPrefs.SetFloat("sens", v), v => v.ToString("0.00"));
            UIKit.Slide(_settings, "Field of view", new Vector2(L, y0 - dy), 60f, 100f, PlayerPrefs.GetFloat("fov", P.fov),
                v => { PlayerPrefs.SetFloat("fov", v); if (_game.Player != null) _game.Player.Cam.fieldOfView = v; }, v => v.ToString("0"));
            UIKit.Slide(_settings, "Volume", new Vector2(L, y0 - dy * 2), 0f, 1f, PlayerPrefs.GetFloat("vol", 0.8f),
                v => { PlayerPrefs.SetFloat("vol", v); AudioListener.volume = v; }, v => Mathf.RoundToInt(v * 100) + "%");
            UIKit.Slide(_settings, "Brightness", new Vector2(L, y0 - dy * 3), 0.6f, 1.6f, PlayerPrefs.GetFloat("bright", 1f),
                v => { PlayerPrefs.SetFloat("bright", v); _game.ApplyBrightness(); }, v => Mathf.RoundToInt(v * 100) + "%");
            UIKit.Cycle(_settings, "Head bob", new Vector2(L, y0 - dy * 4), GraphicsOptions.OnOff, GraphicsOptions.HeadBob, i => GraphicsOptions.HeadBob = i);
            UIKit.Label(_settings, "F11 or Alt+Enter switches between a window and fullscreen at any time.", 18, UIKit.Dim, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(L, y0 - dy * 5), new Vector2(700, 30));

            // ---- right: display & graphics
            UIKit.Label(_settings, "DISPLAY & GRAPHICS", 22, UIKit.Accent, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(R, y0 + 50), new Vector2(700, 30), FontStyle.Bold);
            var resList = GraphicsOptions.Resolutions();
            string cur = GraphicsOptions.CurrentRes();
            if (!resList.Contains(cur)) resList.Add(cur);
            var res = resList.ToArray();
            UIKit.Cycle(_settings, "Display mode", new Vector2(R, y0), GraphicsOptions.Modes, GraphicsOptions.Mode,
                i => { GraphicsOptions.Mode = i; GraphicsOptions.ApplyDisplay(); });
            UIKit.Cycle(_settings, "Resolution", new Vector2(R, y0 - dy), res, System.Array.IndexOf(res, cur),
                i => { GraphicsOptions.Res = res[i]; GraphicsOptions.ApplyDisplay(); });
            UIKit.Cycle(_settings, "Quality preset", new Vector2(R, y0 - dy * 2), GraphicsOptions.Presets, GraphicsOptions.Preset,
                i => { GraphicsOptions.ApplyPreset(i); GraphicsOptions.ApplyQuality(); RebuildSettings(); });
            UIKit.Slide(_settings, "Render scale", new Vector2(R, y0 - dy * 3), 0.5f, 1f, GraphicsOptions.Scale,
                v => { GraphicsOptions.Scale = v; GraphicsOptions.ApplyQuality(); }, v => Mathf.RoundToInt(v * 100) + "%");
            UIKit.Cycle(_settings, "Anti-aliasing", new Vector2(R, y0 - dy * 4), GraphicsOptions.AAs, GraphicsOptions.AA,
                i => { GraphicsOptions.AA = i; GraphicsOptions.ApplyQuality(); });
            UIKit.Cycle(_settings, "Shadows", new Vector2(R, y0 - dy * 5), GraphicsOptions.ShadowOpts, GraphicsOptions.Shadows,
                i => { GraphicsOptions.Shadows = i; GraphicsOptions.ApplyQuality(); });
            UIKit.Cycle(_settings, "Draw distance", new Vector2(R, y0 - dy * 6), GraphicsOptions.Distances, GraphicsOptions.Distance,
                i => { GraphicsOptions.Distance = i; GraphicsOptions.ApplyQuality(); });
            UIKit.Cycle(_settings, "Screen effects", new Vector2(R, y0 - dy * 7), GraphicsOptions.OnOff, GraphicsOptions.Effects,
                i => { GraphicsOptions.Effects = i; GraphicsOptions.ApplyQuality(); });
            UIKit.Cycle(_settings, "V-Sync", new Vector2(R, y0 - dy * 8), GraphicsOptions.OnOff, GraphicsOptions.VSync,
                i => { GraphicsOptions.VSync = i; GraphicsOptions.ApplyQuality(); });
            UIKit.Cycle(_settings, "Frame limit", new Vector2(R, y0 - dy * 9), GraphicsOptions.FpsOpts, GraphicsOptions.Fps,
                i => { GraphicsOptions.Fps = i; GraphicsOptions.ApplyQuality(); });

            Btn(_settings, "BACK", new Vector2(0, -420), () => { PlayerPrefs.Save(); ShowOnly(_settingsReturn); });
        }

        /// <summary>Redraw the settings screen (after a preset changed the values under it).</summary>
        private void RebuildSettings()
        {
            int index = _settings.GetSiblingIndex();
            Destroy(_settings.gameObject);
            BuildSettings(_root);
            _settings.SetSiblingIndex(index);
            ShowOnly(_settings);
        }

        private void BuildCredits(Transform root)
        {
            _credits = MakeScreen(root, "Credits", true);
            var mask = UIKit.RT(_credits, "Mask", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1300, 820));
            mask.gameObject.AddComponent<RectMask2D>();
            _creditsScroll = UIKit.RT(mask, "Scroll", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1300, 10), new Vector2(0.5f, 1f));
            _creditsText = UIKit.Label(_creditsScroll, "", 22, UIKit.Ink, TextAnchor.UpperCenter, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 10));
            _creditsText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _creditsText.supportRichText = true;
            Btn(_credits, "BACK", new Vector2(0, -460), () => ShowOnly(_menu));
        }

        private void BuildPause(Transform root)
        {
            _pause = MakeScreen(root, "Pause", true);
            UIKit.Label(_pause, "PAUSED", 70, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(800, 90), FontStyle.Bold);
            Btn(_pause, "RESUME", new Vector2(0, 60), () => _game.SetPaused(false));
            Btn(_pause, "SETTINGS", new Vector2(0, -20), () => OpenSettings(_pause));
            Btn(_pause, "MAIN MENU", new Vector2(0, -100), _game.ToMenu);
            Btn(_pause, "QUIT", new Vector2(0, -180), _game.Quit);
            _pauseRoster = UIKit.Label(_pause, "", 20, UIKit.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(1400, 60));
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
            UIKit.Label(_win, "AARON IS HAPPY", 96, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1200, 120), FontStyle.Bold);
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

        private void BuildHud(Transform root)
        {
            _hud = MakeScreen(root, "HUD", false);
            UIKit.Img(_hud, "Dot", new Color(1, 1, 1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 4));
            _prompt = UIKit.Label(_hud, "", 26, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(900, 40), FontStyle.Bold);
            _stamina = UIKit.Meter(_hud, "STAMINA", new Vector2(40, 40), UIKit.Ink, out _staminaGroup);
            _battery = UIKit.Meter(_hud, "FLASHLIGHT", new Vector2(40, 84), UIKit.Accent, out _batteryGroup);
            _sanity = UIKit.Meter(_hud, "SANITY", new Vector2(40, 128), new Color(0.55f, 0.7f, 0.9f), out _sanityGroup);
            var title = UIKit.RT(_hud, "LevelTitle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1200, 200));
            _titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            _levelTitle = UIKit.Label(title, "", 72, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1200, 90), FontStyle.Bold);
            _levelSub = UIKit.Label(title, "", 30, UIKit.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1200, 50));
            _toast = UIKit.Label(_hud, "", 24, UIKit.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(1400, 40));
            _objective = UIKit.Label(_hud, "", 20, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(230, -130), new Vector2(420, 220));
            _objective.supportRichText = true;
            _subtitle = UIKit.Label(_hud, "", 30, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 230), new Vector2(1400, 50), FontStyle.Bold);
            _subtitle.supportRichText = true;
            _arrow = UIKit.Label(_hud, "▲", 46, UIKit.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(80, 80), FontStyle.Bold);
            _arrowLabel = UIKit.Label(_hud, "", 18, UIKit.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -145), new Vector2(800, 30));
        }

        // ------------------------------------------------------------------ public API
        public void ShowOnly(RectTransform screen)
        {
            foreach (var s in new[] { _menu, _settings, _credits, _hud, _pause, _death, _win, _loading })
                if (s != null) s.gameObject.SetActive(s == screen);
        }

        public void ShowMenu() => ShowOnly(_menu);
        public void ShowHud() => ShowOnly(_hud);
        public void ShowPause()
        {
            var roster = EntityDirector.Instance != null ? EntityDirector.Instance.Roster : null;
            _pauseRoster.text = roster != null && roster.Count > 0 ? "Down here this time: " + string.Join(", ", roster) : "";
            ShowOnly(_pause);
        }

        public void Subtitle(string who, string text)
        {
            _subtitle.text = "<color=#C9A227>" + who + ":</color> " + text;
            _subTimer = 4.5f;
        }

        /// <summary>Show the arrow pointing at a world position for a while (Q - listen).</summary>
        public void PointTo(Vector3 world, float seconds, string label)
        {
            _pointAt = world; _pointTimer = seconds; _arrowLabel.text = label;
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

        public static string CreditsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<size=54><b>BACKROOMS ESCAPE</b></size>\n");
            sb.AppendLine("A fan game. Made with Unity.\n\n");
            sb.AppendLine("<size=34><b>ENTITIES</b></size>\n");
            sb.AppendLine("Based on works of the SCP Foundation community (scpwiki.com),\nlicensed under Creative Commons Attribution-ShareAlike 3.0.\n");
            var seen = new HashSet<string>();
            if (EntityLibrary.Instance != null)
                foreach (var d in EntityLibrary.Instance.Defs)
                {
                    var c = d.credit;
                    sb.AppendLine("<b>" + d.name + (string.IsNullOrEmpty(d.nickname) ? "" : " - " + d.nickname) + "</b>");
                    if (!string.IsNullOrEmpty(c.article)) sb.AppendLine("\"" + c.article + "\" by " + c.author);
                    if (!string.IsNullOrEmpty(c.articleUrl)) sb.AppendLine(c.articleUrl);
                    if (!string.IsNullOrEmpty(c.assets)) sb.AppendLine("Model, animation and sounds: " + c.assets + (string.IsNullOrEmpty(c.assetAuthors) ? "" : " (" + c.assetAuthors + ")"));
                    if (!string.IsNullOrEmpty(c.changes)) sb.AppendLine("Changes: " + c.changes);
                    sb.AppendLine(c.license + "  " + c.licenseUrl + "\n");
                    seen.Add(c.assets);
                }
            sb.AppendLine("\n<size=34><b>SCP - CONTAINMENT BREACH</b></size>\n");
            sb.AppendLine("3D models, animations and sound effects of the entities are from\nSCP - Containment Breach by Joonas Rikkonen (\"Regalis\") and contributors,\nlicensed under CC BY-SA 3.0. github.com/Regalis11/scpcb\n");
            sb.AppendLine("3D modellers: Regalis, Apocryphos, Atomicsneak, Brandon Smith, EternalError, GamerEntitlement, Hakkla,\nIvan Mikhailov, James Bear, Kenneth Crooker, Lionel Ruskin, Micro, Night, Prosth, TheKMan,\nReilly Grindle, SaitoFox, Sam Lynch, Shaun Gaylard, Tom Malinowski, Vladislav Borisevich\n");
            sb.AppendLine("Sound and music: Regalis, Caleb Yu, Class401, FireFox, gmodfan11, Kevin MacLeod, phlip45, revo794,\nShaun Gaylard, Slagar, Tim Morrison, Vladislav Borisevich, Walach\n");
            sb.AppendLine("Voice actors: TheVolgun, DukeLovesYou, CDWinters, Stealthy Zoroark, Hunter, ItsDuke, Joshofsouls,\nMoonstiee, SolidBaker, Tallfellow\n");
            sb.AppendLine("\n<size=34><b>LICENSE</b></size>\n");
            sb.AppendLine("The SCP-derived content in this game (entity models, animations, sounds, names and descriptions)\nis shared under CC BY-SA 3.0 - you may share and adapt it under the same license.\nSee LICENSE-ASSETS.md in the game folder.\n");
            sb.AppendLine("\n\nThanks for playing.");
            return sb.ToString();
        }

        private void UpdateObjective(PlayerController p)
        {
            var g = _game;
            var sb = new StringBuilder();
            sb.Append("<b>AARON'S DRUM SET  ").Append(g.Placed.Count).Append('/').Append(g.TotalPieces).Append("</b>\n");
            foreach (var id in g.LevelPieces)
            {
                string mark = g.Placed.Contains(id) ? "<color=#7FBF6A>✓</color> " : g.Carried.Contains(id) ? "<color=#C9A227>▲</color> " : "<color=#8A8466>·</color> ";
                string name = DrumKit.NameOf(id);
                sb.Append(mark).Append(g.Placed.Contains(id) ? "<color=#8A8466>" + name + "</color>" : name);
                if (g.Carried.Contains(id)) sb.Append("  <size=16><color=#8A8466>(carrying)</color></size>");
                sb.Append('\n');
            }
            _objective.text = sb.ToString();

            // arrow: back to Aaron while you carry pieces, or toward what you heard when you listened
            _pointTimer -= Time.deltaTime;
            Vector3? target = null;
            if (_pointTimer > 0f) target = _pointAt;
            else if (g.Carried.Count > 0 && g.Room != null) { target = g.Room.Position; _arrowLabel.text = "Aaron"; }
            _arrow.enabled = target.HasValue;
            _arrowLabel.enabled = target.HasValue;
            if (target.HasValue)
            {
                Vector3 to = target.Value - p.transform.position; to.y = 0;
                Vector3 fwd = p.Cam.transform.forward; fwd.y = 0;
                float ang = Vector3.SignedAngle(fwd, to, Vector3.up);
                _arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -ang);
                _arrowLabel.text = (_pointTimer > 0f ? _arrowLabel.text.Split('·')[0].TrimEnd() : "Aaron") + "  ·  " + Mathf.RoundToInt(to.magnitude) + " m";
            }
        }

        private void Update()
        {
            var p = _game != null ? _game.Player : null;
            if (_hud != null && _hud.gameObject.activeSelf && p != null)
            {
                var P = GameConfig.Current.player;
                _stamina.fillAmount = p.Stamina / P.staminaMax;
                _staminaGroup.alpha = Mathf.MoveTowards(_staminaGroup.alpha, p.Stamina < P.staminaMax - 0.5f ? 1f : 0.15f, Time.deltaTime * 2f);
                _battery.fillAmount = p.Battery / P.flashlightBatteryMax;
                _batteryGroup.alpha = Mathf.MoveTowards(_batteryGroup.alpha, p.Flashlight.enabled ? 1f : 0.15f, Time.deltaTime * 2f);
                _sanity.fillAmount = p.Sanity / P.sanityMax;
                _sanityGroup.alpha = Mathf.MoveTowards(_sanityGroup.alpha, p.Sanity < P.sanityMax - 1f ? 1f : 0.15f, Time.deltaTime * 2f);
                _prompt.text = p.InteractPrompt ?? "";
                UpdateObjective(p);
            }
            _subTimer -= Time.unscaledDeltaTime;
            if (_subtitle != null) _subtitle.color = new Color(1, 1, 1, Mathf.Clamp01(_subTimer));
            _titleTimer -= Time.unscaledDeltaTime;
            if (_titleGroup != null) _titleGroup.alpha = Mathf.Clamp01(_titleTimer > 4f ? (5f - _titleTimer) : _titleTimer / 1.5f);
            _toastTimer -= Time.unscaledDeltaTime;
            if (_toast != null) _toast.color = new Color(UIKit.Dim.r, UIKit.Dim.g, UIKit.Dim.b, Mathf.Clamp01(_toastTimer));
            if (_credits != null && _credits.gameObject.activeSelf)
            {
                _creditsScroll.anchoredPosition += new Vector2(0, 45f * Time.unscaledDeltaTime);
                if (_creditsScroll.anchoredPosition.y > _creditsText.preferredHeight + 100) _creditsScroll.anchoredPosition = new Vector2(0, -820);
            }
        }
    }
}
