using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>Deterministic hashing so the infinite world is the same every time you walk back to a spot.</summary>
    public static class Hash
    {
        public static uint U(int seed, int x, int y, int salt = 0)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u; h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE3Du; h = (h << 17) | (h >> 15);
                h ^= (uint)salt * 0x27D4EB2Fu;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return h;
            }
        }

        /// <summary>0..1</summary>
        public static float F(int seed, int x, int y, int salt = 0) => (U(seed, x, y, salt) & 0xFFFFFF) / 16777216f;
    }

    /// <summary>
    /// Material templates live in Resources/BackroomsMaterials (made by the editor setup script) so the URP shaders and the
    /// keyword variants we use (alpha clip, emission) are always included in builds.
    /// </summary>
    public static class Mats
    {
        private static Material _lit, _cutout, _emissive, _unlit, _litN, _cutoutN;

        private static Material Template(ref Material cache, string resName, string shader)
        {
            if (cache != null) return cache;
            cache = Resources.Load<Material>("BackroomsMaterials/" + resName);
            if (cache == null)
            {
                var sh = Shader.Find(shader) ?? Shader.Find("Universal Render Pipeline/Lit");
                cache = new Material(sh);
            }
            return cache;
        }

        public static Material Lit(Color color, Texture tex = null, float smoothness = 0.15f, Vector2? tiling = null)
        {
            var m = new Material(Template(ref _lit, "Lit", "Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        /// <summary>Lit with a tangent-space normal map (entity models). Falls back to plain Lit when there's no normal map.</summary>
        public static Material LitNormal(Color color, Texture tex, Texture normal, float smoothness, float bumpScale = 1f)
        {
            if (normal == null) return Lit(color, tex, smoothness);
            var m = new Material(Template(ref _litN, "LitNormal", "Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bumpScale); m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", 0f);
            return m;
        }

        public static Material CutoutNormal(Color color, Texture tex, Texture normal, float cutoff, float smoothness, float bumpScale = 1f)
        {
            if (normal == null) return Cutout(color, tex, cutoff);
            var m = new Material(Template(ref _cutoutN, "LitCutoutNormal", "Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bumpScale); m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Cutoff", cutoff); m.SetFloat("_AlphaClip", 1f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0f);
            m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", 0f);
            return m;
        }

        public static Material Cutout(Color color, Texture tex, float cutoff = 0.5f)
        {
            var m = new Material(Template(ref _cutout, "LitCutout", "Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_AlphaClip", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            return m;
        }

        public static Material Emissive(Color color, float intensity)
        {
            var m = new Material(Template(ref _emissive, "LitEmissive", "Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", color * intensity);
            return m;
        }

        public static Material Unlit(Color color, Texture tex = null)
        {
            var m = new Material(Template(ref _unlit, "Unlit", "Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            return m;
        }
    }

    /// <summary>Runtime-generated textures (no texture files needed for the level).</summary>
    public static class ProcTex
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        private static Texture2D Make(string key, int size, System.Func<int, int, Color> f)
        {
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = key, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = f(x, y);
            t.SetPixels(px);
            t.Apply(true, true);
            Cache[key] = t;
            return t;
        }

        private static float Noise(float x, float y, float s) => Mathf.PerlinNoise(x * s + 13.1f, y * s + 7.7f);

        private static float Fbm(float x, float y, float s)
        {
            float v = 0, a = 0.5f;
            for (int i = 0; i < 4; i++) { v += a * Noise(x, y, s); s *= 2.03f; a *= 0.5f; }
            return v;
        }

        /// <summary>Grey-scale wallpaper: vertical stripes, a faint repeating motif and water stains. Tinted by the material colour.</summary>
        public static Texture2D Wallpaper() => Make("wallpaper", 256, (x, y) =>
        {
            float stripe = (x % 32) < 3 ? 0.86f : 1f;
            float dx = ((x + 8) % 32) - 16, dy = ((y + (x / 32 % 2) * 16) % 32) - 16;
            float motif = Mathf.Abs(Mathf.Abs(dx) + Mathf.Abs(dy) - 7f) < 1.1f ? 0.9f : 1f;
            float stain = Mathf.Lerp(0.78f, 1f, Mathf.SmoothStep(0.25f, 0.6f, Fbm(x, y, 0.012f)));
            float grain = 0.96f + 0.04f * Noise(x, y, 0.9f);
            float v = stripe * motif * stain * grain;
            return new Color(v, v, v * 0.97f, 1f);
        });

        public static Texture2D Carpet() => Make("carpet", 256, (x, y) =>
        {
            float fiber = 0.82f + 0.18f * Noise(x, y, 0.55f);
            float blot = Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0.3f, 0.65f, Fbm(x, y, 0.02f)));
            float v = fiber * blot;
            return new Color(v, v, v, 1f);
        });

        public static Texture2D CeilingTiles() => Make("ceiling", 256, (x, y) =>
        {
            bool grid = (x % 128) < 3 || (y % 128) < 3;
            float pits = Noise(x, y, 0.35f) > 0.72f ? 0.85f : 1f;
            float stain = Mathf.Lerp(0.75f, 1f, Mathf.SmoothStep(0.3f, 0.7f, Fbm(x, y, 0.015f)));
            float v = (grid ? 0.55f : 1f) * pits * stain;
            return new Color(v, v, v, 1f);
        });

        public static Texture2D Concrete() => Make("concrete", 256, (x, y) =>
        {
            float v = 0.72f + 0.28f * Fbm(x, y, 0.05f);
            if ((y % 128) < 2) v *= 0.7f;
            return new Color(v, v, v, 1f);
        });

        public static Texture2D Tile() => Make("tile", 256, (x, y) =>
        {
            bool grout = (x % 32) < 2 || (y % 32) < 2;
            float dirt = Mathf.Lerp(0.6f, 1f, Fbm(x, y, 0.03f));
            float v = (grout ? 0.35f : 0.95f) * dirt;
            return new Color(v, v, v, 1f);
        });

        public static Texture2D ForWall(string style) => style == "concrete" ? Concrete() : style == "tile" ? Tile() : Wallpaper();
        public static Texture2D ForFloor(string style) => style == "wallpaper" ? Carpet() : Concrete();
        public static Texture2D ForCeiling(string style) => style == "wallpaper" ? CeilingTiles() : Concrete();
    }

    /// <summary>Synthesised sounds so the game has audio without any sound files (entities bring their own).</summary>
    public static class ProcAudio
    {
        private const int Rate = 44100;
        private static AudioClip _hum, _step, _buzz, _heart, _door;

        public static AudioClip Hum()
        {
            if (_hum != null) return _hum;
            int n = Rate * 4;
            var d = new float[n];
            var rng = new System.Random(5);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // 60 Hz mains hum + harmonics, a whisper of ballast buzz and room noise; loops seamlessly (4 s = whole cycles)
                float s = 0.30f * Mathf.Sin(2 * Mathf.PI * 60 * t) + 0.18f * Mathf.Sin(2 * Mathf.PI * 120 * t)
                        + 0.06f * Mathf.Sin(2 * Mathf.PI * 180 * t) + 0.03f * Mathf.Sin(2 * Mathf.PI * 240 * t + 1f);
                lp += ((float)rng.NextDouble() * 2 - 1 - lp) * 0.05f;
                d[i] = (s + lp * 0.25f) * 0.35f;
            }
            _hum = AudioClip.Create("hum", n, 1, Rate, false);
            _hum.SetData(d, 0);
            return _hum;
        }

        public static AudioClip Step()
        {
            if (_step != null) return _step;
            int n = (int)(Rate * 0.18f);
            var d = new float[n];
            var rng = new System.Random(11);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Exp(-i / (Rate * 0.03f));
                lp += ((float)rng.NextDouble() * 2 - 1 - lp) * 0.18f;
                d[i] = lp * env * 1.4f + Mathf.Sin(i * 0.012f) * env * 0.25f;
            }
            _step = AudioClip.Create("step", n, 1, Rate, false);
            _step.SetData(d, 0);
            return _step;
        }

        public static AudioClip Buzz()
        {
            if (_buzz != null) return _buzz;
            int n = (int)(Rate * 0.5f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 120 * t)) * 0.2f + Mathf.Sin(2 * Mathf.PI * 2400 * t) * 0.05f;
                d[i] = s * (1f - t / 0.5f);
            }
            _buzz = AudioClip.Create("buzz", n, 1, Rate, false);
            _buzz.SetData(d, 0);
            return _buzz;
        }

        public static AudioClip Heartbeat()
        {
            if (_heart != null) return _heart;
            int n = Rate;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float b1 = Mathf.Exp(-Mathf.Pow((t - 0.05f) / 0.025f, 2)), b2 = Mathf.Exp(-Mathf.Pow((t - 0.3f) / 0.03f, 2)) * 0.7f;
                d[i] = Mathf.Sin(2 * Mathf.PI * 48 * t) * (b1 + b2) * 0.9f;
            }
            _heart = AudioClip.Create("heart", n, 1, Rate, false);
            _heart.SetData(d, 0);
            return _heart;
        }

        public static AudioClip DoorChime()
        {
            if (_door != null) return _door;
            int n = Rate * 2;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // soft, wrong-sounding chord the exit door hums so you can find it by ear
                float s = Mathf.Sin(2 * Mathf.PI * 220 * t) + 0.7f * Mathf.Sin(2 * Mathf.PI * 277.2f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 311.1f * t);
                d[i] = s * 0.12f * (0.75f + 0.25f * Mathf.Sin(2 * Mathf.PI * 0.5f * t));
            }
            _door = AudioClip.Create("door", n, 1, Rate, false);
            _door.SetData(d, 0);
            return _door;
        }
    }
}
