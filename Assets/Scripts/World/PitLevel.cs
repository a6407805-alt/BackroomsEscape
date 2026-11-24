using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Backrooms
{
    /// <summary>
    /// "The Porcelain Pit": an upside-down stepped pyramid, roughly the size of the Great Pyramid, made of nothing but
    /// toilets. Every tier is 12 feet (3.66 m) below the one before and steps inward; each tier has a walkway and a row of
    /// toilets against its back wall, so every toilet sits right above-and-behind the one on the next tier down.
    /// Stairs run down along the back wall of every tier, so every single toilet can be reached.
    /// Toilets are drawn with GPU instancing (there are thousands); tiers, walls and stairs have colliders.
    /// </summary>
    public class PitLevel : MonoBehaviour
    {
        public int Tiers { get; private set; }
        public float R0 { get; private set; }          // half-size of the pit at the rim
        public Vector3 RimSpot => new Vector3(0, 0, -(R0 + RimWidth * 0.6f));

        private const float RimWidth = 18f;
        private LevelTheme _t;
        private float _h, _d;
        private int _seed;
        private Mesh _toilet;
        private Material _porcelain;
        private readonly List<Matrix4x4[]> _batches = new List<Matrix4x4[]>();
        private readonly List<Vector3> _toilets = new List<Vector3>();
        private readonly List<Vector3> _lampSpots = new List<Vector3>();
        private readonly List<Light> _lightPool = new List<Light>();
        private float _lightTick, _toiletTick;
        private ToiletSeat _seat;
        private RenderParams _rp;

        public static PitLevel Create(LevelTheme theme, int seed)
        {
            var go = new GameObject("PorcelainPit");
            var pit = go.AddComponent<PitLevel>();
            pit._t = theme; pit._seed = seed;
            pit.Build();
            return pit;
        }

        /// <summary>Half-size of tier k's inner edge (k = 0 is the rim).</summary>
        private float Inner(int k) => R0 - k * _d;

        /// <summary>Floor height under a point (the rim is 0, tier k is -k * 12 ft).</summary>
        public float HeightAt(Vector3 p)
        {
            float m = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
            if (m >= R0) return 0f;
            int k = Mathf.Min(Tiers, Mathf.FloorToInt((R0 - m) / _d) + 1);
            return -k * _h;
        }

        private void Build()
        {
            _h = _t.tierHeight; _d = _t.terraceDepth;
            R0 = _t.pitSize / 2f;
            Tiers = Mathf.Max(3, Mathf.FloorToInt((R0 - 4f) / _d));
            float bottomY = -Tiers * _h;

            var floorMat = Mats.Lit(_t.floorColor, ProcTex.Tile(), 0.55f);
            var wallMat = Mats.Lit(_t.wallColor, ProcTex.Tile(), 0.6f);
            var stairMat = Mats.Lit(_t.floorColor * 0.85f, ProcTex.Concrete(), 0.3f);
            var stripMat = Mats.Emissive(_t.lightColor, 2.4f);

            var floors = new MeshBuilder();
            var walls = new MeshBuilder();
            var stairs = new MeshBuilder();
            var strips = new MeshBuilder();

            // rim: a wide tiled floor all the way round, closed in by tall walls
            float outer = R0 + RimWidth;
            Ring(floors, 0f, R0, outer, 0.5f, true);
            float wallH = 14f;
            Slab(walls, new Vector3(-outer - 0.5f, 0, -outer - 0.5f), new Vector3(outer + 0.5f, wallH, -outer));
            Slab(walls, new Vector3(-outer - 0.5f, 0, outer), new Vector3(outer + 0.5f, wallH, outer + 0.5f));
            Slab(walls, new Vector3(-outer - 0.5f, 0, -outer), new Vector3(-outer, wallH, outer));
            Slab(walls, new Vector3(outer, 0, -outer), new Vector3(outer + 0.5f, wallH, outer));

            for (int k = 1; k <= Tiers; k++)
            {
                float y = -k * _h;
                float back = Inner(k - 1), front = k == Tiers ? 0f : Inner(k);
                // terrace (the bottom tier is the whole floor of the pit)
                if (k == Tiers) Slab(floors, new Vector3(-back, y - 0.5f, -back), new Vector3(back, y, back), true);
                else Ring(floors, y, front, back, 0.5f, true);
                // back wall: the drop from the tier above
                Ring(walls, y, back - 0.05f, back + 0.25f, -_h, true);
                // a strip light along the lip of every tier: from the rim the whole pit is lit in rings
                Ring(strips, y + _h - 0.02f, back - 0.04f, back + 0.02f, 0.06f, false);
                if (k % 2 == 0) for (int s = 0; s < 4; s++) _lampSpots.Add(SidePoint(s, 0.5f, back - 1f, y + 2.4f));

                // stairs: one flight per tier, on a different side each time, running down along the back wall
                int side = (k + (int)(Hash.U(_seed, k, 0, 3) % 2)) % 4;
                float along = Mathf.Lerp(-0.3f, 0.3f, Hash.F(_seed, k, 1, 4));
                BuildStairs(stairs, side, along, back, y);

                // toilets on this tier, against the back wall, facing the middle of the pit
                float len = back * 2f;
                int count = Mathf.FloorToInt(len / _t.toiletSpacing);
                for (int s = 0; s < 4; s++)
                    for (int i = 1; i < count; i++)
                    {
                        float u = -back + i * (len / count);
                        if (s == side && Mathf.Abs(u - along * back * 2f) < 4.2f) continue;   // the stairs are here
                        Vector3 pos = SideXZ(s, u, back - 0.42f, y);
                        _toilets.Add(pos);
                    }
            }

            AddMesh("Floors", floors, floorMat, true);
            AddMesh("Walls", walls, wallMat, true);
            AddMesh("Stairs", stairs, stairMat, false);
            AddMesh("Strips", strips, stripMat, false);
            BuildToilets();

            // the bottom is darker and stranger: a single cold light far below
            var bottom = new GameObject("BottomLight").AddComponent<Light>();
            bottom.transform.SetParent(transform, false);
            bottom.transform.position = new Vector3(0, bottomY + 6f, 0);
            bottom.type = LightType.Point; bottom.range = 30f; bottom.intensity = 4f; bottom.color = new Color(0.7f, 0.85f, 1f);

            for (int i = 0; i < 28; i++)
            {
                var l = new GameObject("PitLight").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point; l.range = _t.lightRange; l.intensity = _t.lightIntensity; l.color = _t.lightColor;
                l.shadows = LightShadows.None;
                _lightPool.Add(l);
            }

            _seat = new GameObject("ToiletSeat").AddComponent<ToiletSeat>();
            _seat.transform.SetParent(transform, false);
        }

        // ------------------------------------------------------------------ geometry helpers
        /// <summary>Point on side s (0=S,1=E,2=N,3=W) of a square of half-size `half`, `u` along the side, at height y.</summary>
        private static Vector3 SideXZ(int s, float u, float half, float y)
        {
            switch (s)
            {
                case 0: return new Vector3(u, y, -half);
                case 1: return new Vector3(half, y, u);
                case 2: return new Vector3(-u, y, half);
                default: return new Vector3(-half, y, -u);
            }
        }

        private static Vector3 SidePoint(int s, float t01, float half, float y) => SideXZ(s, Mathf.Lerp(-half, half, t01), half, y);

        private static Quaternion FacingIn(int s) => Quaternion.Euler(0, s * -90f, 0);   // side 0 (south wall) faces +z

        private static void Slab(MeshBuilder mb, Vector3 a, Vector3 b, bool unused = false) => mb.Box(Vector3.Min(a, b), Vector3.Max(a, b));

        /// <summary>Square ring between half-sizes inner..outer, top at y, `thick` down (negative = up).</summary>
        private static void Ring(MeshBuilder mb, float y, float inner, float outer, float thick, bool unused)
        {
            float y0 = thick > 0 ? y - thick : y, y1 = thick > 0 ? y : y - thick;
            mb.Box(new Vector3(-outer, y0, -outer), new Vector3(outer, y1, -inner));
            mb.Box(new Vector3(-outer, y0, inner), new Vector3(outer, y1, outer));
            mb.Box(new Vector3(-outer, y0, -inner), new Vector3(-inner, y1, inner));
            mb.Box(new Vector3(inner, y0, -inner), new Vector3(outer, y1, inner));
        }

        private void BuildStairs(MeshBuilder mb, int side, float along, float back, float bottomY)
        {
            // flight runs along the wall, from the tier above (top) down to this tier
            const float width = 1.3f, run = 7.2f;
            int steps = 16;
            float uMid = along * back * 2f;
            Quaternion rot = FacingIn(side);
            Vector3 wallPoint = SideXZ(side, uMid, back, bottomY);
            Vector3 alongDir = rot * Vector3.right;                 // along the wall
            Vector3 inward = rot * Vector3.forward;                 // away from the wall
            Vector3 start = wallPoint - alongDir * (run / 2f) + inward * (width / 2f);   // top end
            var go = new GameObject("StairCollider");
            go.transform.SetParent(transform, false);
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                float topY = bottomY + _h * (1f - (float)i / steps);
                Vector3 c = start + alongDir * (run * t);
                Vector3 half = new Vector3(run / steps / 2f, 0, width / 2f);
                // steps are axis-aligned boxes on N/S walls; swap axes for E/W
                bool xAxis = side == 0 || side == 2;
                Vector3 ext = xAxis ? new Vector3(half.x, 0, half.z) : new Vector3(half.z, 0, half.x);
                mb.Box(new Vector3(c.x - ext.x, bottomY, c.z - ext.z), new Vector3(c.x + ext.x, topY, c.z + ext.z));
            }
            // one smooth ramp collider so walking down never snags
            var col = go.AddComponent<BoxCollider>();
            Vector3 top = start + Vector3.up * _h, bottom = start + alongDir * run;
            Vector3 mid = (top + bottom) / 2f;
            go.transform.position = mid;
            Vector3 dir = (bottom - top).normalized;
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.Cross(dir, Vector3.Cross(Vector3.up, dir)));
            col.size = new Vector3(width, 0.1f, (bottom - top).magnitude + 0.3f);
        }

        private void AddMesh(string name, MeshBuilder mb, Material mat, bool collide)
        {
            if (!mb.HasGeometry) return;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mesh = mb.ToMesh(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // ------------------------------------------------------------------ toilets
        private void BuildToilets()
        {
            _toilet = MakeToiletMesh();
            _porcelain = Mats.Lit(new Color(0.95f, 0.96f, 0.97f), null, 0.9f);
            _porcelain.enableInstancing = true;
            _rp = new RenderParams(_porcelain) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, worldBounds = new Bounds(new Vector3(0, -Tiers * _h / 2f, 0), new Vector3(R0 * 2 + 40, Tiers * _h + 40, R0 * 2 + 40)) };
            var batch = new List<Matrix4x4>(1023);
            foreach (var p in _toilets)
            {
                int side = SideOf(p);
                batch.Add(Matrix4x4.TRS(p, FacingIn(side), Vector3.one));
                if (batch.Count == 1023) { _batches.Add(batch.ToArray()); batch.Clear(); }
            }
            if (batch.Count > 0) _batches.Add(batch.ToArray());
            Debug.Log($"[Backrooms] Porcelain Pit: {Tiers} tiers, {_toilets.Count} toilets, {Tiers * _h:0} m deep");
        }

        private static int SideOf(Vector3 p)
        {
            if (Mathf.Abs(p.z) >= Mathf.Abs(p.x)) return p.z < 0 ? 0 : 2;
            return p.x > 0 ? 1 : 3;
        }

        /// <summary>A toilet (bowl, seat, lid, tank) built from primitives, origin at the floor, facing +z.</summary>
        private static Mesh MakeToiletMesh()
        {
            var parts = new List<CombineInstance>();
            void Add(PrimitiveType t, Vector3 pos, Vector3 scale, Vector3 euler = default)
            {
                var go = GameObject.CreatePrimitive(t);
                var m = go.GetComponent<MeshFilter>().sharedMesh;
                parts.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, Quaternion.Euler(euler), scale) });
                Object.Destroy(go);
            }
            Add(PrimitiveType.Cylinder, new Vector3(0, 0.17f, 0.05f), new Vector3(0.26f, 0.17f, 0.34f));        // pedestal
            Add(PrimitiveType.Sphere, new Vector3(0, 0.36f, 0.08f), new Vector3(0.4f, 0.18f, 0.52f));           // bowl
            Add(PrimitiveType.Cylinder, new Vector3(0, 0.42f, 0.08f), new Vector3(0.42f, 0.015f, 0.54f));       // seat
            Add(PrimitiveType.Cube, new Vector3(0, 0.66f, -0.17f), new Vector3(0.4f, 0.48f, 0.04f), new Vector3(-8, 0, 0)); // lid up
            Add(PrimitiveType.Cube, new Vector3(0, 0.62f, -0.24f), new Vector3(0.46f, 0.4f, 0.18f));            // tank
            Add(PrimitiveType.Cube, new Vector3(0, 0.83f, -0.24f), new Vector3(0.48f, 0.03f, 0.2f));            // tank lid
            var mesh = new Mesh { name = "Toilet" };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        public Vector3 NearestToilet(Vector3 p, out int index)
        {
            index = -1; float best = float.MaxValue;
            for (int i = 0; i < _toilets.Count; i++)
            {
                var t = _toilets[i];
                if (Mathf.Abs(t.y - p.y) > 2f) continue;
                float d = (t - p).sqrMagnitude;
                if (d < best) { best = d; index = i; }
            }
            return index >= 0 ? _toilets[index] : p;
        }

        // ------------------------------------------------------------------ pieces and spawns
        /// <summary>Where the i-th of n drum pieces lies: deeper for later pieces, the last at the very bottom.</summary>
        public Vector3 PieceSpot(int i, int n)
        {
            if (i == n - 1) return new Vector3(2f, -Tiers * _h, 2f);
            int k = Mathf.Clamp(Mathf.RoundToInt(Tiers * (i + 1f) / n), 1, Tiers - 1);
            return Walkway(k, (int)(Hash.U(_seed, i, 9, 5) % 4), Hash.F(_seed, i, 9, 6));
        }

        /// <summary>A point on tier k's walkway, on side s, t01 along it.</summary>
        public Vector3 Walkway(int k, int s, float t01)
        {
            float back = Inner(k - 1), front = Inner(k);
            float half = (back - 0.9f + front) / 2f;
            return SideXZ(s, Mathf.Lerp(-half * 0.85f, half * 0.85f, t01), half, -k * _h);
        }

        public Vector3 RandomSpot(Vector3 near, float minDist, float maxDist)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                int k = Random.Range(0, Tiers + 1);
                Vector3 p = k == 0 ? SideXZ(Random.Range(0, 4), Random.Range(-R0, R0), R0 + 4f, 0f) : Walkway(k, Random.Range(0, 4), Random.value);
                float d = Vector3.Distance(p, near);
                if (d >= minDist && d <= maxDist) return p;
            }
            return near + Vector3.forward * minDist;
        }

        // ------------------------------------------------------------------ per frame
        private void Update()
        {
            foreach (var b in _batches) Graphics.RenderMeshInstanced(_rp, _toilet, 0, b);

            var player = GameBoot.Instance != null ? GameBoot.Instance.Player : null;
            if (player == null) return;
            Vector3 pp = player.transform.position;

            _lightTick -= Time.deltaTime;
            if (_lightTick <= 0f)
            {
                _lightTick = 0.4f;
                _lampSpots.Sort((a, b) => (a - pp).sqrMagnitude.CompareTo((b - pp).sqrMagnitude));
                for (int i = 0; i < _lightPool.Count; i++)
                {
                    bool on = i < _lampSpots.Count;
                    _lightPool[i].enabled = on;
                    if (on) _lightPool[i].transform.position = _lampSpots[i];
                }
            }

            _toiletTick -= Time.deltaTime;
            if (_toiletTick <= 0f)
            {
                _toiletTick = 0.15f;
                Vector3 t = NearestToilet(pp, out int idx);
                _seat.Place(idx >= 0 && (t - pp).sqrMagnitude < 9f ? t : (Vector3?)null, idx, SideOf(t));
            }
        }

        // ------------------------------------------------------------------ the seat you can use
        /// <summary>One invisible interact volume that follows the toilet nearest to you.</summary>
        public class ToiletSeat : MonoBehaviour, IInteractable
        {
            private int _index;
            private BoxCollider _col;
            private AudioSource _a;

            private static readonly string[] Lines =
            {
                "You used toilet #{0}. It is twelve feet above toilet #{1}.",
                "Toilet #{0}. The one below it is watching.",
                "You feel lighter. The pit does not.",
                "Toilet #{0} flushes. Somewhere far below, thousands answer.",
                "There is no toilet paper. There has never been toilet paper."
            };

            public string Prompt => "[E] Use toilet #" + (_index + 1).ToString("N0");

            private void Awake()
            {
                _col = gameObject.AddComponent<BoxCollider>();
                _col.isTrigger = true; _col.size = new Vector3(0.6f, 1f, 0.8f); _col.center = new Vector3(0, 0.5f, 0.1f);
                _a = gameObject.AddComponent<AudioSource>();
                _a.spatialBlend = 1f; _a.maxDistance = 30f;
            }

            public void Place(Vector3? at, int index, int side)
            {
                _col.enabled = at.HasValue;
                if (!at.HasValue) return;
                _index = index;
                transform.position = at.Value;
                transform.rotation = FacingIn(side);
            }

            public void Interact(PlayerController player)
            {
                _a.PlayOneShot(Flush(), 0.9f);
                player.DrinkAlmondWater();   // relief
                NoiseSystem.Emit(transform.position, 18f);
                var line = Lines[Random.Range(0, Lines.Length)];
                GameBoot.Instance.UI.Toast(string.Format(line, (_index + 1).ToString("N0"), (_index + 2).ToString("N0")), 4f);
            }

            private static AudioClip _flush;
            private static AudioClip Flush()
            {
                if (_flush != null) return _flush;
                int rate = 44100, n = rate * 3;
                var d = new float[n];
                var rng = new System.Random(3);
                float lp = 0, lp2 = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate;
                    float noise = (float)rng.NextDouble() * 2 - 1;
                    lp += (noise - lp) * Mathf.Lerp(0.5f, 0.05f, t / 3f);
                    lp2 += (lp - lp2) * 0.3f;
                    float env = Mathf.Clamp01(t * 8f) * Mathf.Exp(-t * 0.9f);
                    d[i] = (lp2 * 1.6f + Mathf.Sin(2 * Mathf.PI * (300 - t * 80) * t) * 0.05f) * env;
                }
                _flush = AudioClip.Create("flush", n, 1, rate, false);
                _flush.SetData(d, 0);
                return _flush;
            }
        }
    }
}
