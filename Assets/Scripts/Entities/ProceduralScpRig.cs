using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Builds and animates articulated 3D nightmare silhouettes for procedural SCPs in the 325+ rotating roster
    /// (crooked bone-snappers, shadow wraiths, flesh amalgams, weeping rebar/porcelain idols, hazmat doppelgangers,
    /// arachnid stalkers, ocular clusters, doorway wheezers, quadruped crawlers, ceiling ambushers, skeletal titans).
    /// Also synthesises per-archetype horror audio cues on demand so zero external files are required.
    /// </summary>
    public class ProceduralScpRig : MonoBehaviour
    {
        private string _archetype = "tall_lurker";
        private string _mode = "idle";
        private float _animSpeed = 1f;
        private float _phase;
        private float _twitchTimer;
        private Vector3 _headTwitch;

        private Transform _pelvis;
        private Transform _torso;
        private Transform _head;
        private Transform _jaw;
        private readonly List<Transform> _leftLimbs = new List<Transform>();
        private readonly List<Transform> _rightLimbs = new List<Transform>();
        private readonly List<Transform> _tendrils = new List<Transform>();
        private float _basePelvisY;

        public void SetMotion(string mode, float speed)
        {
            _mode = string.IsNullOrEmpty(mode) ? "idle" : mode;
            _animSpeed = Mathf.Max(0.2f, speed);
        }

        public static ProceduralScpRig Build(EntityDef def, Transform parent)
        {
            var holder = new GameObject("ProceduralModel");
            holder.transform.SetParent(parent, false);
            var rig = holder.AddComponent<ProceduralScpRig>();
            rig.Construct(def);
            return rig;
        }

        private void Construct(EntityDef def)
        {
            _archetype = string.IsNullOrEmpty(def.rigArchetype) ? "tall_lurker" : def.rigArchetype;
            int seed = (int)Hash.U(def.id.GetHashCode(), 17, 31);
            var rng = new System.Random(seed);

            float h = Mathf.Clamp(def.height, 0.7f, 4.2f);
            Material bodyMat = BodyMaterial(_archetype, def.primaryColor);
            Material accMat = BodyMaterial(_archetype, def.accentColor);
            Material eyeMat = def.eyeGlow > 0.05f ? Mats.Emissive(def.eyeColor, def.eyeGlow) : Mats.Lit(def.eyeColor, null, 0.8f);

            _pelvis = Node(transform, "Pelvis", Vector3.zero);

            switch (_archetype)
            {
                case "crawler_quad":
                case "arachnid":
                {
                    int pairs = _archetype == "arachnid" ? 4 : 2;
                    _basePelvisY = h * 0.48f;
                    _pelvis.localPosition = new Vector3(0f, _basePelvisY, 0f);
                    _torso = Node(_pelvis, "Thorax", Vector3.zero);
                    Prim(_torso, PrimitiveType.Sphere, new Vector3(0f, 0f, -0.15f * h), new Vector3(0.46f * h, 0.34f * h, 0.72f * h), bodyMat);
                    Prim(_torso, PrimitiveType.Sphere, new Vector3(0f, 0.06f * h, 0.25f * h), new Vector3(0.34f * h, 0.28f * h, 0.42f * h), accMat);
                    _head = Node(_torso, "Head", new Vector3(0f, 0.06f * h, 0.46f * h));
                    Prim(_head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.28f * h, 0.24f * h, 0.30f * h), bodyMat);
                    SpawnEyes(_head, eyeMat, _archetype == "arachnid" ? 6 : 2, 0.10f * h, 0.14f * h);

                    for (int i = 0; i < pairs; i++)
                    {
                        float zOff = Mathf.Lerp(0.28f, -0.32f, pairs == 1 ? 0.5f : i / (float)(pairs - 1)) * h;
                        float spread = _archetype == "arachnid" ? 0.52f : 0.28f;
                        _leftLimbs.Add(BuildSpiderOrCrawlerLeg(_pelvis, -1f, zOff, h, spread, bodyMat, accMat));
                        _rightLimbs.Add(BuildSpiderOrCrawlerLeg(_pelvis, 1f, zOff, h, spread, bodyMat, accMat));
                    }
                    break;
                }

                case "flesh_amalgam":
                {
                    _basePelvisY = h * 0.44f;
                    _pelvis.localPosition = new Vector3(0f, _basePelvisY, 0f);
                    _torso = Node(_pelvis, "BiomassCore", Vector3.zero);
                    for (int i = 0; i < 7; i++)
                    {
                        Vector3 off = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.45f * h, ((float)rng.NextDouble() - 0.3f) * 0.45f * h, ((float)rng.NextDouble() - 0.5f) * 0.42f * h);
                        float r = Mathf.Lerp(0.24f, 0.52f, (float)rng.NextDouble()) * h;
                        Prim(_torso, PrimitiveType.Sphere, off, new Vector3(r * 1.15f, r * 0.95f, r), i % 2 == 0 ? bodyMat : accMat);
                    }
                    _head = Node(_torso, "ClusterHead", new Vector3(0.05f * h, 0.34f * h, 0.12f * h));
                    Prim(_head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.32f * h, 0.36f * h, 0.30f * h), bodyMat);
                    SpawnEyes(_head, eyeMat, 5, 0.13f * h, 0.15f * h);
                    _leftLimbs.Add(BuildHulkingArm(_torso, -1f, h, bodyMat, accMat));
                    _rightLimbs.Add(BuildHulkingArm(_torso, 1f, h, bodyMat, accMat));
                    _leftLimbs.Add(BuildBipedLeg(_pelvis, -1f, h * 0.44f, 0.18f * h, bodyMat));
                    _rightLimbs.Add(BuildBipedLeg(_pelvis, 1f, h * 0.44f, 0.18f * h, bodyMat));
                    break;
                }

                case "ocular_swarm":
                {
                    _basePelvisY = h * 0.58f;
                    _pelvis.localPosition = new Vector3(0f, _basePelvisY, 0f);
                    _torso = Node(_pelvis, "VoidCore", Vector3.zero);
                    Prim(_torso, PrimitiveType.Sphere, Vector3.zero, Vector3.one * (0.48f * h), bodyMat);
                    _head = Node(_torso, "CentralEye", new Vector3(0f, 0.04f * h, 0.18f * h));
                    Prim(_head, PrimitiveType.Sphere, Vector3.zero, Vector3.one * (0.28f * h), eyeMat);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI * 0.25f;
                        var tendril = Node(_torso, "Tendril_" + i, new Vector3(Mathf.Cos(ang) * 0.2f * h, -0.1f * h, Mathf.Sin(ang) * 0.2f * h));
                        Prim(tendril, PrimitiveType.Cylinder, new Vector3(0f, -0.28f * h, 0f), new Vector3(0.045f * h, 0.28f * h, 0.045f * h), accMat);
                        Prim(tendril, PrimitiveType.Sphere, new Vector3(0f, -0.54f * h, 0f), Vector3.one * (0.09f * h), eyeMat);
                        _tendrils.Add(tendril);
                    }
                    break;
                }

                case "doppelganger":
                {
                    // Distorted Backrooms hazmat explorer with wrong proportions & hollow visor
                    _basePelvisY = h * 0.50f;
                    _pelvis.localPosition = new Vector3(0f, _basePelvisY, 0f);
                    _torso = Node(_pelvis, "HazmatTorso", Vector3.zero);
                    Prim(_torso, PrimitiveType.Capsule, new Vector3(0f, 0.18f * h, 0f), new Vector3(0.28f * h, 0.24f * h, 0.20f * h), bodyMat);
                    Prim(_torso, PrimitiveType.Cube, new Vector3(0f, 0.20f * h, -0.14f * h), new Vector3(0.22f * h, 0.28f * h, 0.12f * h), accMat); // oxygen pack
                    _head = Node(_torso, "Hood", new Vector3(0f, 0.42f * h, 0.03f * h));
                    _head.localRotation = Quaternion.Euler(0f, 0f, 18f); // uncanny tilted neck
                    Prim(_head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.21f * h, 0.24f * h, 0.22f * h), bodyMat);
                    Prim(_head, PrimitiveType.Cube, new Vector3(0f, 0.01f * h, 0.10f * h), new Vector3(0.14f * h, 0.09f * h, 0.04f * h), Mats.Lit(new Color(0.03f, 0.03f, 0.04f), null, 0.9f));
                    SpawnEyes(_head, eyeMat, 2, 0.04f * h, 0.12f * h);
                    _leftLimbs.Add(BuildLongArm(_torso, -1f, h * 1.12f, bodyMat, accMat));
                    _rightLimbs.Add(BuildLongArm(_torso, 1f, h * 1.12f, bodyMat, accMat));
                    _leftLimbs.Add(BuildBipedLeg(_pelvis, -1f, _basePelvisY, 0.11f * h, bodyMat));
                    _rightLimbs.Add(BuildBipedLeg(_pelvis, 1f, _basePelvisY, 0.11f * h, bodyMat));
                    break;
                }

                default:
                {
                    // crooked, weeping_idol, shadow_wraith, doorman, ceiling_ambusher, skeletal_titan, tall_lurker
                    bool crooked = _archetype == "crooked" || _archetype == "ceiling_ambusher";
                    bool idol = _archetype == "weeping_idol";
                    bool wraith = _archetype == "shadow_wraith";
                    bool skeletal = _archetype == "skeletal_titan";

                    _basePelvisY = h * 0.52f;
                    _pelvis.localPosition = new Vector3(0f, _basePelvisY, 0f);
                    _torso = Node(_pelvis, "Spine", Vector3.zero);
                    if (crooked) _torso.localRotation = Quaternion.Euler(14f, 0f, -12f);

                    float chestW = skeletal ? 0.18f : idol ? 0.26f : 0.22f;
                    Prim(_torso, PrimitiveType.Capsule, new Vector3(0f, 0.19f * h, 0f), new Vector3(chestW * h, 0.24f * h, 0.15f * h), bodyMat);
                    if (skeletal || crooked)
                    {
                        for (int r = 0; r < 4; r++)
                            Prim(_torso, PrimitiveType.Cube, new Vector3(0f, (0.10f + r * 0.06f) * h, 0.02f * h), new Vector3(0.28f * h, 0.022f * h, 0.16f * h), accMat);
                    }
                    if (idol)
                    {
                        // Exposed twisted rebar rods
                        Prim(_torso, PrimitiveType.Cylinder, new Vector3(0.08f * h, 0.28f * h, 0f), new Vector3(0.02f * h, 0.24f * h, 0.02f * h), accMat).localRotation = Quaternion.Euler(0f, 0f, -18f);
                        Prim(_torso, PrimitiveType.Cylinder, new Vector3(-0.09f * h, 0.26f * h, 0f), new Vector3(0.02f * h, 0.24f * h, 0.02f * h), accMat).localRotation = Quaternion.Euler(12f, 0f, 22f);
                    }

                    _head = Node(_torso, "Head", new Vector3(crooked ? 0.06f * h : 0f, 0.43f * h, crooked ? 0.07f * h : 0.02f * h));
                    if (crooked) _head.localRotation = Quaternion.Euler(8f, 0f, 28f);
                    Prim(_head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.16f * h, 0.22f * h, 0.17f * h), bodyMat);

                    // Gaping jaw that drops open during screams / attacks
                    _jaw = Node(_head, "Jaw", new Vector3(0f, -0.06f * h, 0.03f * h));
                    Prim(_jaw, PrimitiveType.Cube, new Vector3(0f, -0.04f * h, 0.02f * h), new Vector3(0.10f * h, 0.08f * h, 0.10f * h), accMat);

                    if (_archetype != "doorman")
                        SpawnEyes(_head, eyeMat, idol ? 3 : 2, 0.045f * h, 0.085f * h);

                    float armScale = crooked ? 1.35f : wraith ? 1.25f : 1.05f;
                    _leftLimbs.Add(BuildLongArm(_torso, -1f, h * armScale, bodyMat, accMat));
                    _rightLimbs.Add(BuildLongArm(_torso, 1f, h * armScale, bodyMat, accMat));
                    if (!wraith)
                    {
                        _leftLimbs.Add(BuildBipedLeg(_pelvis, -1f, _basePelvisY, 0.095f * h, bodyMat));
                        _rightLimbs.Add(BuildBipedLeg(_pelvis, 1f, _basePelvisY, 0.095f * h, bodyMat));
                    }
                    else
                    {
                        // Trailing shadow cloak tendrils
                        for (int i = 0; i < 5; i++)
                        {
                            var t = Node(_pelvis, "Shroud_" + i, new Vector3((i - 2) * 0.07f * h, 0f, -0.03f * h));
                            Prim(t, PrimitiveType.Cylinder, new Vector3(0f, -0.24f * h, 0f), new Vector3(0.07f * h, 0.25f * h, 0.07f * h), bodyMat);
                            _tendrils.Add(t);
                        }
                    }
                    break;
                }
            }
        }

        private static Transform BuildLongArm(Transform parent, float side, float h, Material mat, Material clawMat)
        {
            var shoulder = Node(parent, side < 0 ? "ArmL" : "ArmR", new Vector3(side * 0.17f * h, 0.34f * h, 0f));
            Prim(shoulder, PrimitiveType.Cylinder, new Vector3(side * 0.03f * h, -0.18f * h, 0f), new Vector3(0.048f * h, 0.18f * h, 0.048f * h), mat);
            var elbow = Node(shoulder, "Forearm", new Vector3(side * 0.05f * h, -0.35f * h, 0.03f * h));
            elbow.localRotation = Quaternion.Euler(-18f, 0f, side * -6f);
            Prim(elbow, PrimitiveType.Cylinder, new Vector3(0f, -0.19f * h, 0f), new Vector3(0.038f * h, 0.19f * h, 0.038f * h), mat);
            for (int c = -1; c <= 1; c++)
                Prim(elbow, PrimitiveType.Cube, new Vector3(c * 0.02f * h, -0.40f * h, 0.02f * h), new Vector3(0.012f * h, 0.09f * h, 0.015f * h), clawMat);
            return shoulder;
        }

        private static Transform BuildHulkingArm(Transform parent, float side, float h, Material mat, Material accMat)
        {
            var shoulder = Node(parent, side < 0 ? "HulkArmL" : "HulkArmR", new Vector3(side * 0.28f * h, 0.18f * h, 0.05f * h));
            Prim(shoulder, PrimitiveType.Capsule, new Vector3(side * 0.05f * h, -0.20f * h, 0.04f * h), new Vector3(0.14f * h, 0.24f * h, 0.14f * h), mat);
            Prim(shoulder, PrimitiveType.Sphere, new Vector3(side * 0.06f * h, -0.42f * h, 0.08f * h), Vector3.one * (0.16f * h), accMat);
            return shoulder;
        }

        private static Transform BuildBipedLeg(Transform parent, float side, float hipH, float xSpread, Material mat)
        {
            var hip = Node(parent, side < 0 ? "LegL" : "LegR", new Vector3(side * xSpread, 0f, 0f));
            Prim(hip, PrimitiveType.Cylinder, new Vector3(0f, -hipH * 0.26f, 0f), new Vector3(hipH * 0.11f, hipH * 0.26f, hipH * 0.11f), mat);
            var knee = Node(hip, "Shin", new Vector3(0f, -hipH * 0.50f, 0f));
            Prim(knee, PrimitiveType.Cylinder, new Vector3(0f, -hipH * 0.25f, 0f), new Vector3(hipH * 0.085f, hipH * 0.25f, hipH * 0.085f), mat);
            return hip;
        }

        private static Transform BuildSpiderOrCrawlerLeg(Transform parent, float side, float zOff, float h, float spread, Material mat, Material tipMat)
        {
            var root = Node(parent, "CrawlLeg", new Vector3(side * 0.16f * h, 0f, zOff));
            var upper = Prim(root, PrimitiveType.Cylinder, new Vector3(side * spread * 0.45f * h, 0.10f * h, 0f), new Vector3(0.04f * h, spread * 0.48f * h, 0.04f * h), mat);
            upper.localRotation = Quaternion.Euler(0f, 0f, side * -62f);
            var lower = Prim(root, PrimitiveType.Cylinder, new Vector3(side * spread * 0.85f * h, -0.18f * h, 0f), new Vector3(0.028f * h, 0.30f * h, 0.028f * h), tipMat);
            lower.localRotation = Quaternion.Euler(0f, 0f, side * -12f);
            return root;
        }

        private static void SpawnEyes(Transform head, Material eyeMat, int count, float spreadX, float zFront)
        {
            if (count <= 2)
            {
                Prim(head, PrimitiveType.Sphere, new Vector3(-spreadX, 0.02f, zFront), Vector3.one * (spreadX * 0.65f), eyeMat);
                Prim(head, PrimitiveType.Sphere, new Vector3(spreadX, 0.02f, zFront), Vector3.one * (spreadX * 0.65f), eyeMat);
                return;
            }
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)(count - 1) - 0.5f) * 1.4f;
                float y = (i % 2 == 0 ? 0.03f : -0.02f);
                Prim(head, PrimitiveType.Sphere, new Vector3(Mathf.Sin(a) * spreadX * 1.3f, y, zFront * Mathf.Cos(a * 0.5f)), Vector3.one * (spreadX * 0.42f), eyeMat);
            }
        }

        private static Transform Node(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static Transform Prim(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go.transform;
        }

        private static Material BodyMaterial(string archetype, Color c)
        {
            float smooth = archetype == "flesh_amalgam" ? 0.72f : archetype == "weeping_idol" ? 0.08f : archetype == "shadow_wraith" ? 0.02f : 0.22f;
            Texture2D tex = archetype == "weeping_idol" ? ProcTex.Concrete() : archetype == "doppelganger" ? ProcTex.Carpet() : ProcTex.Tile();
            return Mats.Lit(c, tex, smooth);
        }

        private void Update()
        {
            if (_pelvis == null) return;
            if (_mode == "frozen") return; // completely stone-still while watched!

            float dt = Time.deltaTime;
            bool moving = _mode == "walk" || _mode == "run";
            bool attacking = _mode == "attack" || _mode == "special" || _mode == "triggered";
            float rate = _mode == "run" ? 11f * _animSpeed : moving ? 5.2f * _animSpeed : 1.8f;
            _phase += dt * rate;

            // Unnatural horror head twitches
            _twitchTimer -= dt;
            if (_twitchTimer <= 0f)
            {
                _twitchTimer = Random.Range(0.18f, 1.35f);
                _headTwitch = (_archetype == "crooked" || _mode == "special" || attacking)
                    ? new Vector3(Random.Range(-28f, 28f), Random.Range(-35f, 35f), Random.Range(-42f, 42f))
                    : new Vector3(Random.Range(-6f, 6f), Random.Range(-10f, 10f), Random.Range(-12f, 12f));
            }
            if (_head != null)
                _head.localRotation = Quaternion.Slerp(_head.localRotation, Quaternion.Euler(_headTwitch), dt * 14f);

            if (_jaw != null)
            {
                float jawOpen = attacking || _mode == "special" ? 38f + Mathf.Sin(_phase * 3f) * 10f : 4f + Mathf.Abs(Mathf.Sin(_phase * 0.5f)) * 6f;
                _jaw.localRotation = Quaternion.Euler(jawOpen, 0f, 0f);
            }

            float bob = moving ? Mathf.Abs(Mathf.Sin(_phase)) * 0.07f : Mathf.Sin(_phase) * 0.02f;
            if (_archetype == "ocular_swarm" || _archetype == "shadow_wraith") bob = Mathf.Sin(_phase * 0.8f) * 0.12f;
            _pelvis.localPosition = new Vector3(0f, _basePelvisY + bob, 0f);

            if (_torso != null && _archetype != "ocular_swarm")
            {
                float lean = _mode == "run" ? 24f : moving ? 10f : 3f;
                float sway = moving ? Mathf.Sin(_phase) * (_archetype == "crooked" ? 16f : 7f) : Mathf.Sin(_phase * 0.7f) * 2.5f;
                _torso.localRotation = Quaternion.Euler(lean, sway * 0.6f, sway);
            }

            float swingAmp = attacking ? 65f : _mode == "run" ? 48f : moving ? 28f : 4f;
            for (int i = 0; i < _leftLimbs.Count; i++)
            {
                float s = Mathf.Sin(_phase + i * Mathf.PI);
                if (attacking && i == 0)
                    _leftLimbs[i].localRotation = Quaternion.Euler(-75f + s * 35f, 0f, -12f);
                else
                    _leftLimbs[i].localRotation = Quaternion.Euler(s * swingAmp, 0f, 0f);
            }
            for (int i = 0; i < _rightLimbs.Count; i++)
            {
                float s = Mathf.Sin(_phase + (i + 1) * Mathf.PI);
                if (attacking && i == 0)
                    _rightLimbs[i].localRotation = Quaternion.Euler(-75f - s * 35f, 0f, 12f);
                else
                    _rightLimbs[i].localRotation = Quaternion.Euler(s * swingAmp, 0f, 0f);
            }
            for (int i = 0; i < _tendrils.Count; i++)
            {
                _tendrils[i].localRotation = Quaternion.Euler(Mathf.Sin(_phase + i) * 18f, 0f, Mathf.Cos(_phase * 0.9f + i) * 18f);
            }
        }
    }

    /// <summary>Lazy procedural horror sound synthesiser for the 320+ rotating SCP anomalies.</summary>
    public static class ProceduralScpAudio
    {
        public enum CueKind { Ambient, Alert, Attack, Idle }

        private const int Rate = 22050;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static AudioClip GetCue(string profile, CueKind kind)
        {
            string s = kind == CueKind.Ambient ? "ambient" : kind == CueKind.Alert ? "alert" : kind == CueKind.Attack ? "attack" : "idle";
            int v = kind == CueKind.Ambient ? 0 : UnityEngine.Random.Range(0, 8);
            return GetCue(profile, s, v);
        }

        public static AudioClip GetCue(string profile, string cueKind, int variantSeed)
        {
            if (string.IsNullOrEmpty(profile)) profile = "wheezing";
            string key = $"{profile}_{cueKind}_{variantSeed & 7}";
            if (Cache.TryGetValue(key, out var c) && c != null) return c;

            var rng = new System.Random(key.GetHashCode());
            float dur = cueKind == "ambient" ? 2.8f : cueKind == "attack" ? 0.95f : 1.35f;
            int n = Mathf.RoundToInt(Rate * dur);
            var d = new float[n];
            float pitchMod = Mathf.Lerp(0.78f, 1.26f, (float)rng.NextDouble());
            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float u = i / (float)n;
                float env = cueKind == "ambient"
                    ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 6f)) * Mathf.Sin(Mathf.PI * Mathf.Clamp01((1f - u) * 6f) * 0.5f + 0.5f)
                    : Mathf.Exp(-t * (cueKind == "attack" ? 2.4f : 1.6f));
                if (cueKind == "ambient") env = Mathf.Min(1f, Mathf.Min(u * 8f, (1f - u) * 8f));

                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * 0.08f;
                float sample = 0f;

                switch (profile)
                {
                    case "clicking": // SCP-4975 rhythmic cervical vertebra clicking
                    {
                        float clickPeriod = cueKind == "alert" || cueKind == "attack" ? 0.11f : 0.35f;
                        float localT = t % clickPeriod;
                        float transient = Mathf.Exp(-localT * 140f);
                        sample = (Mathf.Sin(2f * Mathf.PI * 1150f * pitchMod * localT) + white * 0.7f) * transient * 0.85f;
                        break;
                    }
                    case "bone_snap": // SCP-783 Crooked Man snapping limbs
                    {
                        float snap1 = Mathf.Exp(-Mathf.Abs(t - 0.08f) * 55f);
                        float snap2 = Mathf.Exp(-Mathf.Abs(t - 0.34f) * 45f);
                        float snap3 = Mathf.Exp(-Mathf.Abs(t - 0.68f) * 60f);
                        sample = (white * 0.8f + Mathf.Sin(2f * Mathf.PI * 420f * pitchMod * t) * 0.5f) * (snap1 + snap2 + snap3) + lp * 0.18f * env;
                        break;
                    }
                    case "wheezing": // SCP-303 heavy rasping bronchial wheeze behind doors
                    {
                        float breath = Mathf.Sin(2f * Mathf.PI * 0.55f * t);
                        float rasp = lp * 1.6f + 0.25f * Mathf.Sin(2f * Mathf.PI * (290f + 60f * breath) * pitchMod * t);
                        sample = rasp * Mathf.Abs(breath) * env * 0.7f;
                        break;
                    }
                    case "radio_numbers": // SCP-3034 eerie shortwave tone intervals + static
                    {
                        int step = Mathf.FloorToInt(t * 3.5f);
                        float freq = (step % 3 == 0 ? 523.25f : step % 3 == 1 ? 415.3f : 311.1f) * pitchMod;
                        sample = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.32f + white * 0.14f) * env;
                        break;
                    }
                    case "statue_scrape": // Grinding concrete & rebar
                    {
                        float grit = Mathf.Sin(2f * Mathf.PI * 92f * pitchMod * t) * 0.4f + lp * 1.4f;
                        sample = grit * env * (cueKind == "ambient" ? 0.35f : 0.85f);
                        break;
                    }
                    case "flesh_squelch": // SCP-610 / SCP-3199 wet visceral biomass + shriek
                    {
                        float squelch = Mathf.Sin(2f * Mathf.PI * (140f + 90f * Mathf.Sin(19f * t)) * pitchMod * t) * 0.45f;
                        float shriek = cueKind == "alert" || cueKind == "attack" ? Mathf.Sin(2f * Mathf.PI * (980f - 260f * t) * pitchMod * t) * 0.45f : 0f;
                        sample = (squelch + shriek + lp * 0.6f) * env;
                        break;
                    }
                    case "metallic_drag":
                    {
                        float ring = Mathf.Sin(2f * Mathf.PI * 740f * pitchMod * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 1110f * pitchMod * t) * 0.18f;
                        sample = (ring + lp * 0.9f) * env * 0.75f;
                        break;
                    }
                    case "siren_static":
                    {
                        float sweep = 340f + 220f * Mathf.Sin(2f * Mathf.PI * 1.3f * t);
                        sample = (Mathf.Sign(Mathf.Sin(2f * Mathf.PI * sweep * pitchMod * t)) * 0.22f + white * 0.22f) * env;
                        break;
                    }
                    case "whisper_choir":
                    {
                        float mod = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 6.5f * t);
                        sample = (lp * 1.8f * mod + Mathf.Sin(2f * Mathf.PI * 196f * pitchMod * t) * 0.18f) * env;
                        break;
                    }
                    default: // sub_drone
                    {
                        sample = (0.45f * Mathf.Sin(2f * Mathf.PI * 52f * pitchMod * t)
                                + 0.28f * Mathf.Sin(2f * Mathf.PI * 77.8f * pitchMod * t)
                                + lp * 0.25f) * env;
                        break;
                    }
                }
                d[i] = Mathf.Clamp(sample, -0.95f, 0.95f);
            }

            c = AudioClip.Create(key, n, 1, Rate, false);
            c.SetData(d, 0);
            Cache[key] = c;
            return c;
        }
    }
}
