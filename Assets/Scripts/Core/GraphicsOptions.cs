using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Backrooms
{
    /// <summary>
    /// Display and graphics settings (saved in PlayerPrefs, applied at boot and whenever one changes).
    /// The URP asset is cloned at runtime so changing settings never edits the project's assets.
    /// F11 or Alt+Enter flips between windowed and borderless fullscreen.
    /// </summary>
    public static class GraphicsOptions
    {
        public static readonly string[] Modes = { "Fullscreen", "Borderless", "Windowed" };
        public static readonly string[] Presets = { "Low", "Medium", "High", "Ultra" };
        public static readonly string[] AAs = { "Off", "FXAA", "SMAA", "MSAA 4x" };
        public static readonly string[] ShadowOpts = { "Off", "Low", "High" };
        public static readonly string[] Distances = { "Near", "Normal", "Far" };
        public static readonly string[] FpsOpts = { "30", "60", "120", "144", "Unlimited" };
        private static readonly int[] FpsValues = { 30, 60, 120, 144, -1 };
        public static readonly string[] OnOff = { "Off", "On" };

        public static int Mode { get => PlayerPrefs.GetInt("gfx.mode", 2); set => PlayerPrefs.SetInt("gfx.mode", value); }
        public static string Res { get => PlayerPrefs.GetString("gfx.res", ""); set => PlayerPrefs.SetString("gfx.res", value); }
        public static int Preset { get => PlayerPrefs.GetInt("gfx.preset", 2); set => PlayerPrefs.SetInt("gfx.preset", value); }
        public static float Scale { get => PlayerPrefs.GetFloat("gfx.scale", 1f); set => PlayerPrefs.SetFloat("gfx.scale", value); }
        public static int AA { get => PlayerPrefs.GetInt("gfx.aa", 2); set => PlayerPrefs.SetInt("gfx.aa", value); }
        public static int Shadows { get => PlayerPrefs.GetInt("gfx.shadows", 2); set => PlayerPrefs.SetInt("gfx.shadows", value); }
        public static int Distance { get => PlayerPrefs.GetInt("gfx.dist", 1); set => PlayerPrefs.SetInt("gfx.dist", value); }
        public static int Fps { get => PlayerPrefs.GetInt("gfx.fps", 4); set => PlayerPrefs.SetInt("gfx.fps", value); }
        public static int VSync { get => PlayerPrefs.GetInt("gfx.vsync", 1); set => PlayerPrefs.SetInt("gfx.vsync", value); }
        public static int Effects { get => PlayerPrefs.GetInt("gfx.fx", 1); set => PlayerPrefs.SetInt("gfx.fx", value); }
        public static int HeadBob { get => PlayerPrefs.GetInt("bob", 1); set => PlayerPrefs.SetInt("bob", value); }

        private static UniversalRenderPipelineAsset _urp;

        /// <summary>Distinct resolutions the monitor supports, smallest first, as "WxH".</summary>
        public static List<string> Resolutions()
        {
            var list = new List<string>();
            var seen = new HashSet<string>();
            foreach (var r in Screen.resolutions)
            {
                string s = r.width + "x" + r.height;
                if (r.width >= 800 && seen.Add(s)) list.Add(s);
            }
            if (list.Count == 0) list.Add(Screen.currentResolution.width + "x" + Screen.currentResolution.height);
            return list;
        }

        public static string CurrentRes()
        {
            if (!string.IsNullOrEmpty(Res)) return Res;
            var native = Screen.currentResolution;
            // first launch: a comfortable window, not the whole screen
            if (Mode == 2) return Mathf.Min(1600, native.width - 80) + "x" + Mathf.Min(900, native.height - 120);
            return native.width + "x" + native.height;
        }

        /// <summary>A preset sets everything below it in one go.</summary>
        public static void ApplyPreset(int p)
        {
            Preset = p;
            Scale = new[] { 0.7f, 0.85f, 1f, 1f }[p];
            AA = new[] { 1, 1, 2, 3 }[p];
            Shadows = new[] { 0, 1, 2, 2 }[p];
            Distance = new[] { 0, 1, 1, 2 }[p];
            Effects = p == 0 ? 0 : 1;
        }

        public static void ApplyDisplay()
        {
            var parts = CurrentRes().Split('x');
            int w = 1600, h = 900;
            if (parts.Length == 2) { int.TryParse(parts[0], out w); int.TryParse(parts[1], out h); }
            var mode = Mode == 0 ? FullScreenMode.ExclusiveFullScreen : Mode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Mode == 1) { w = Screen.currentResolution.width; h = Screen.currentResolution.height; }   // borderless always covers the screen
            Screen.SetResolution(w, h, mode);
        }

        public static void ApplyAll()
        {
            ApplyDisplay();
            ApplyQuality();
        }

        public static void ApplyQuality()
        {
            QualitySettings.vSyncCount = VSync;
            Application.targetFrameRate = VSync == 1 ? -1 : FpsValues[Mathf.Clamp(Fps, 0, FpsValues.Length - 1)];

            if (_urp == null && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset src)
            {
                _urp = Object.Instantiate(src);
                _urp.name = src.name + " (runtime)";
                GraphicsSettings.defaultRenderPipeline = _urp;
                QualitySettings.renderPipeline = _urp;
            }
            if (_urp != null)
            {
                _urp.renderScale = Mathf.Clamp(Scale, 0.5f, 1f);
                _urp.msaaSampleCount = AA == 3 ? 4 : 1;
                _urp.shadowDistance = Shadows == 0 ? 0f : Shadows == 1 ? 20f : 50f;
            }

            var game = GameBoot.Instance;
            var player = game != null ? game.Player : null;
            if (player != null && player.Cam != null)
            {
                var data = player.Cam.GetComponent<UniversalAdditionalCameraData>();
                if (data != null)
                {
                    data.antialiasing = AA == 1 ? AntialiasingMode.FastApproximateAntialiasing : AA == 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
                    data.antialiasingQuality = AntialiasingQuality.High;
                }
                player.Cam.allowMSAA = AA == 3;
                player.Flashlight.shadows = Shadows == 0 ? LightShadows.None : Shadows == 1 ? LightShadows.Hard : LightShadows.Soft;
            }
            if (GameConfig.Current != null) GameConfig.Current.world.loadRadius = Distance + 1;
            if (game != null) game.ApplyEffects(Effects == 1);
        }

        /// <summary>F11 / Alt+Enter.</summary>
        public static void ToggleFullscreen()
        {
            Mode = Mode == 2 ? 1 : 2;
            PlayerPrefs.Save();
            ApplyDisplay();
        }
    }
}
