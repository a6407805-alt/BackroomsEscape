using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Aaron's drum set: the ten pieces, how each one looks (built from primitives), where it sits on the kit,
    /// and the drum sounds (synthesised).
    /// </summary>
    public static class DrumKit
    {
        public class Piece
        {
            public string Id, Name;
            public Vector3 KitPos;     // position on the kit (kit space: +z faces the audience)
            public Vector3 KitEuler;
            public Piece(string id, string name, Vector3 pos, Vector3 euler) { Id = id; Name = name; KitPos = pos; KitEuler = euler; }
        }

        public static readonly Piece[] All =
        {
            new Piece("kick", "Kick drum", new Vector3(0f, 0.3f, 0.1f), new Vector3(90, 0, 0)),
            new Piece("pedal", "Kick pedal", new Vector3(0f, 0f, -0.22f), Vector3.zero),
            new Piece("snare", "Snare drum", new Vector3(-0.38f, 0.62f, -0.28f), new Vector3(8, 0, 0)),
            new Piece("sticks", "Drumsticks", new Vector3(-0.38f, 0.72f, -0.28f), Vector3.zero),
            new Piece("hihat", "Hi-hat", new Vector3(-0.72f, 0f, -0.15f), Vector3.zero),
            new Piece("racktom", "Rack tom", new Vector3(0.18f, 0.86f, 0.02f), new Vector3(18, 0, 0)),
            new Piece("floortom", "Floor tom", new Vector3(0.62f, 0f, -0.3f), Vector3.zero),
            new Piece("crash", "Crash cymbal", new Vector3(-0.5f, 0f, 0.3f), Vector3.zero),
            new Piece("ride", "Ride cymbal", new Vector3(0.8f, 0f, 0.15f), Vector3.zero),
            new Piece("throne", "Drum throne", new Vector3(0f, 0f, -0.78f), Vector3.zero),
        };

        public static Piece Get(string id)
        {
            foreach (var p in All) if (p.Id == id) return p;
            return null;
        }

        public static string NameOf(string id) => Get(id)?.Name ?? id;

        // ------------------------------------------------------------------ looks
        private static Material _shell, _head, _chrome, _brass, _wood, _black, _seat;

        private static void EnsureMats()
        {
            if (_shell != null) return;
            _shell = Mats.Lit(new Color(0.62f, 0.05f, 0.07f), null, 0.75f);     // Aaron's kit: wine-red sparkle
            _head = Mats.Lit(new Color(0.93f, 0.92f, 0.88f), null, 0.35f);
            _chrome = Mats.Lit(new Color(0.78f, 0.79f, 0.8f), null, 0.85f);
            _chrome.SetFloat("_Metallic", 0.9f);
            _brass = Mats.Lit(new Color(0.78f, 0.6f, 0.22f), null, 0.75f);
            _brass.SetFloat("_Metallic", 0.85f);
            _wood = Mats.Lit(new Color(0.75f, 0.58f, 0.36f), null, 0.4f);
            _black = Mats.Lit(new Color(0.06f, 0.06f, 0.06f), null, 0.3f);
            _seat = Mats.Lit(new Color(0.08f, 0.08f, 0.08f), null, 0.2f);
        }

        private static Transform Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(t);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        /// <summary>A drum: cylinder shell with heads, radius r and depth d, axis along local Y.</summary>
        private static void Drum(Transform p, Vector3 pos, float r, float d, Vector3 euler = default)
        {
            var root = new GameObject("Drum").transform;
            root.SetParent(p, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(euler);
            Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(r * 2, d / 2, r * 2), _shell);
            Part(root, PrimitiveType.Cylinder, new Vector3(0, d / 2, 0), new Vector3(r * 2.02f, 0.006f, r * 2.02f), _head);
            Part(root, PrimitiveType.Cylinder, new Vector3(0, -d / 2, 0), new Vector3(r * 2.02f, 0.006f, r * 2.02f), _head);
            Part(root, PrimitiveType.Cylinder, new Vector3(0, d / 2, 0), new Vector3(r * 2.06f, 0.012f, r * 2.06f), _chrome);
            Part(root, PrimitiveType.Cylinder, new Vector3(0, -d / 2, 0), new Vector3(r * 2.06f, 0.012f, r * 2.06f), _chrome);
        }

        private static void Stand(Transform p, float height)
        {
            Part(p, PrimitiveType.Cylinder, new Vector3(0, height / 2, 0), new Vector3(0.025f, height / 2, 0.025f), _chrome);
            for (int i = 0; i < 3; i++)
                Part(p, PrimitiveType.Cylinder, new Vector3(0, 0.08f, 0) + Quaternion.Euler(0, i * 120, 0) * new Vector3(0, 0, 0.14f),
                    new Vector3(0.015f, 0.16f, 0.015f), _chrome, new Vector3(0, i * 120, 0) + new Vector3(60, 0, 0));
        }

        private static void Cymbal(Transform p, float y, float r, float tilt)
        {
            Part(p, PrimitiveType.Cylinder, new Vector3(0, y, 0), new Vector3(r * 2, 0.004f, r * 2), _brass, new Vector3(tilt, 0, 0));
            Part(p, PrimitiveType.Sphere, new Vector3(0, y + 0.01f, 0), new Vector3(0.06f, 0.025f, 0.06f), _brass);
        }

        /// <summary>Builds the model of one piece under `parent` (origin at the piece's floor/mount point).</summary>
        public static void Build(string id, Transform parent)
        {
            EnsureMats();
            switch (id)
            {
                case "kick":
                    Drum(parent, Vector3.zero, 0.28f, 0.42f);
                    Part(parent, PrimitiveType.Cylinder, new Vector3(0, 0, 0.215f), new Vector3(0.3f, 0.005f, 0.3f), _black);   // port/logo
                    break;
                case "pedal":
                    Part(parent, PrimitiveType.Cube, new Vector3(0, 0.02f, 0), new Vector3(0.1f, 0.02f, 0.28f), _black);
                    Part(parent, PrimitiveType.Cube, new Vector3(0, 0.06f, 0.02f), new Vector3(0.08f, 0.012f, 0.24f), _chrome, new Vector3(-15, 0, 0));
                    Part(parent, PrimitiveType.Cylinder, new Vector3(0, 0.2f, 0.14f), new Vector3(0.02f, 0.12f, 0.02f), _chrome);
                    Part(parent, PrimitiveType.Sphere, new Vector3(0, 0.32f, 0.14f), new Vector3(0.06f, 0.06f, 0.06f), _head);
                    break;
                case "snare":
                    Drum(parent, Vector3.zero, 0.18f, 0.13f);
                    break;
                case "sticks":
                    Part(parent, PrimitiveType.Cylinder, new Vector3(-0.03f, 0.02f, 0), new Vector3(0.018f, 0.2f, 0.018f), _wood, new Vector3(0, 20, 90));
                    Part(parent, PrimitiveType.Cylinder, new Vector3(0.03f, 0.02f, 0.02f), new Vector3(0.018f, 0.2f, 0.018f), _wood, new Vector3(0, -15, 90));
                    break;
                case "hihat":
                    Stand(parent, 0.95f);
                    Cymbal(parent, 0.93f, 0.18f, 0f);
                    Cymbal(parent, 0.96f, 0.18f, 0f);
                    break;
                case "racktom":
                    Drum(parent, Vector3.zero, 0.13f, 0.2f);
                    break;
                case "floortom":
                    Drum(parent, new Vector3(0, 0.38f, 0), 0.2f, 0.4f);
                    for (int i = 0; i < 3; i++)
                        Part(parent, PrimitiveType.Cylinder, Quaternion.Euler(0, i * 120, 0) * new Vector3(0, 0.1f, 0.22f), new Vector3(0.015f, 0.1f, 0.015f), _chrome);
                    break;
                case "crash":
                    Stand(parent, 1.3f);
                    Cymbal(parent, 1.32f, 0.24f, -12f);
                    break;
                case "ride":
                    Stand(parent, 1.12f);
                    Cymbal(parent, 1.14f, 0.28f, -8f);
                    break;
                case "throne":
                    Stand(parent, 0.48f);
                    Part(parent, PrimitiveType.Cylinder, new Vector3(0, 0.52f, 0), new Vector3(0.38f, 0.04f, 0.38f), _seat);
                    break;
                default:
                    Part(parent, PrimitiveType.Cube, new Vector3(0, 0.15f, 0), Vector3.one * 0.3f, _shell);
                    break;
            }
        }

        // ------------------------------------------------------------------ sounds
        private const int Rate = 44100;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        public static AudioClip Sound(string kind)
        {
            if (Clips.TryGetValue(kind, out var c)) return c;
            var rng = new System.Random(kind.GetHashCode());
            float len = kind == "crash" ? 2.2f : kind == "ride" ? 1.6f : kind == "stomp" ? 0.4f : 0.6f;
            int n = (int)(Rate * len);
            var d = new float[n];
            float hp = 0, prev = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)rng.NextDouble() * 2 - 1;
                hp = 0.92f * (hp + noise - prev); prev = noise;
                lp += (noise - lp) * 0.25f;
                float s;
                switch (kind)
                {
                    case "kick":
                    case "stomp":
                    {
                        float f = 45f + 110f * Mathf.Exp(-t * 28f);
                        s = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * (kind == "stomp" ? 9f : 6f)) + lp * Mathf.Exp(-t * 60f) * 0.3f;
                        break;
                    }
                    case "snare":
                        s = Mathf.Sin(2 * Mathf.PI * 190f * t) * Mathf.Exp(-t * 18f) * 0.5f + hp * Mathf.Exp(-t * 14f) * 0.8f;
                        break;
                    case "hat":
                        s = hp * Mathf.Exp(-t * 45f) * 0.6f;
                        break;
                    case "crash":
                        s = hp * Mathf.Exp(-t * 1.6f) * 0.5f * (1f + 0.3f * Mathf.Sin(2 * Mathf.PI * 3400 * t));
                        break;
                    case "ride":
                        s = (hp * 0.25f + 0.25f * Mathf.Sin(2 * Mathf.PI * 3100f * t) + 0.2f * Mathf.Sin(2 * Mathf.PI * 4700f * t)) * Mathf.Exp(-t * 2.5f);
                        break;
                    case "tomhi":
                        s = Mathf.Sin(2 * Mathf.PI * (180f + 60f * Mathf.Exp(-t * 20f)) * t) * Mathf.Exp(-t * 7f) + lp * Mathf.Exp(-t * 40f) * 0.2f;
                        break;
                    case "tomlo":
                        s = Mathf.Sin(2 * Mathf.PI * (95f + 40f * Mathf.Exp(-t * 20f)) * t) * Mathf.Exp(-t * 5f) + lp * Mathf.Exp(-t * 40f) * 0.2f;
                        break;
                    case "shimmer":
                        // the faint ringing a lost piece makes, so you can hear it nearby
                        s = (Mathf.Sin(2 * Mathf.PI * 1318f * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 1975f * t)) * 0.12f * (0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * 1.5f * t));
                        break;
                    default:
                        s = 0;
                        break;
                }
                d[i] = Mathf.Clamp(s, -1f, 1f);
            }
            c = AudioClip.Create(kind, n, 1, Rate, false);
            c.SetData(d, 0);
            Clips[kind] = c;
            return c;
        }
    }

    /// <summary>A lost drum piece lying somewhere in the Backrooms.</summary>
    public class DrumPickup : MonoBehaviour, IInteractable
    {
        public string PieceId;
        private Transform _model;
        private Vector3 _base;

        public string Prompt => "[E] Pick up the " + DrumKit.NameOf(PieceId).ToLowerInvariant();

        public static DrumPickup Create(Transform parent, Vector3 pos, string id)
        {
            var go = new GameObject("Piece " + id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var p = go.AddComponent<DrumPickup>();
            p.PieceId = id;
            p._model = new GameObject("Model").transform;
            p._model.SetParent(go.transform, false);
            var piece = DrumKit.Get(id);
            DrumKit.Build(id, p._model);
            // lying around rather than mounted: the kick on its side, the rest upright
            if (id == "kick") p._model.localPosition = new Vector3(0, 0.28f, 0);
            if (id == "snare" || id == "racktom") p._model.localPosition = new Vector3(0, 0.12f, 0);
            p._base = p._model.localPosition;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true; col.center = new Vector3(0, 0.5f, 0); col.size = new Vector3(1.1f, 1.2f, 1.1f);
            var l = new GameObject("Glow").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0, 1.4f, 0);
            l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.55f); l.intensity = 1.6f; l.range = 3.5f;
            var a = go.AddComponent<AudioSource>();
            a.clip = DrumKit.Sound("shimmer"); a.loop = true; a.spatialBlend = 1f; a.minDistance = 1.5f; a.maxDistance = 22f;
            a.rolloffMode = AudioRolloffMode.Linear; a.volume = 0.7f; a.Play();
            return p;
        }

        private void Update()
        {
            if (_model != null) _model.localPosition = _base + Vector3.up * (0.04f * Mathf.Sin(Time.time * 1.7f));
        }

        public void Interact(PlayerController player)
        {
            GameBoot.Instance.PickUpPiece(PieceId);
            Destroy(gameObject);
        }
    }
}
