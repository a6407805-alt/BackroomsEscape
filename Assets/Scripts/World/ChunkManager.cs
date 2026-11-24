using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Builds the chunks of the maze around the player and throws away the ones left behind, so the level never ends.
    /// One chunk = ChunkCells x ChunkCells cells: one floor, one ceiling, one combined wall mesh (+ collider),
    /// ceiling light panels, point lights, the odd exit door and pickups.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        public MazeField Field { get; private set; }
        public Transform Target;

        private readonly Dictionary<Vector2Int, GameObject> _chunks = new Dictionary<Vector2Int, GameObject>();
        private readonly HashSet<Vector2Int> _collected = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, Pickup> _spawnedPickups = new Dictionary<Vector2Int, Pickup>();
        private readonly List<Light> _lights = new List<Light>();
        public IReadOnlyList<Light> ActiveLights => _lights;
        private Material _wallMat, _floorMat, _ceilMat, _panelOn, _panelOff, _pillarMat, _trimMat;
        private WorldSettings _ws;
        private float _lightTick;

        public void Init(MazeField field, WorldSettings ws)
        {
            Field = field;
            _ws = ws;
            var t = field.Theme;
            _wallMat = Mats.Lit(t.wallColor, ProcTex.ForWall(t.wallStyle), 0.08f);
            _floorMat = Mats.Lit(t.floorColor, ProcTex.ForFloor(t.wallStyle), 0.05f);
            _ceilMat = Mats.Lit(t.ceilingColor, ProcTex.ForCeiling(t.wallStyle), 0.05f);
            _panelOn = Mats.Emissive(t.lightColor, 2.2f);
            _panelOff = Mats.Lit(t.ceilingColor * 0.55f, null, 0.3f);
            _pillarMat = Mats.Lit(t.wallColor * 0.92f, ProcTex.ForWall(t.wallStyle), 0.08f);
            _trimMat = Mats.Lit(t.wallColor * 0.6f, null, 0.2f);
        }

        public bool IsCollected(Vector2Int cell) => _collected.Contains(cell);
        public void MarkCollected(Vector2Int cell)
        {
            _collected.Add(cell);
            _spawnedPickups.Remove(cell);
            BackroomsNet.Instance?.NotifyPickupTaken(cell);
        }

        public void MarkCollectedFromNet(Vector2Int cell)
        {
            _collected.Add(cell);
            if (_spawnedPickups.TryGetValue(cell, out var pk) && pk != null)
                Destroy(pk.gameObject);
            _spawnedPickups.Remove(cell);
        }

        public void Clear()
        {
            foreach (var kv in _chunks) if (kv.Value != null) Destroy(kv.Value);
            _chunks.Clear();
            _lights.Clear();
            _collected.Clear();
            _spawnedPickups.Clear();
        }

        private void Update()
        {
            if (Field == null || Target == null) return;
            var center = Field.ChunkOf(Field.CellAt(Target.position));
            int r = Mathf.Max(1, _ws.loadRadius);
            // build nearest first, at most two chunks a frame to avoid hitches
            int built = 0;
            for (int ring = 0; ring <= r && built < 2; ring++)
                for (int dx = -ring; dx <= ring && built < 2; dx++)
                    for (int dz = -ring; dz <= ring && built < 2; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                        var k = new Vector2Int(center.x + dx, center.y + dz);
                        if (_chunks.ContainsKey(k)) continue;
                        _chunks[k] = BuildChunk(k);
                        built++;
                    }
            // unload far chunks
            List<Vector2Int> drop = null;
            foreach (var kv in _chunks)
                if (Mathf.Max(Mathf.Abs(kv.Key.x - center.x), Mathf.Abs(kv.Key.y - center.y)) > r + 1)
                    (drop ??= new List<Vector2Int>()).Add(kv.Key);
            if (drop != null)
                foreach (var k in drop) { Destroy(_chunks[k]); _chunks.Remove(k); }

            _lightTick -= Time.deltaTime;
            if (_lightTick <= 0f) { _lightTick = 0.2f; CullLights(); }
        }

        /// <summary>Builds every chunk around the player right now (used when a level starts, behind the fade).</summary>
        public void BuildAllNow()
        {
            if (Target == null) return;
            var center = Field.ChunkOf(Field.CellAt(Target.position));
            int r = Mathf.Max(1, _ws.loadRadius);
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    var k = new Vector2Int(center.x + dx, center.y + dz);
                    if (!_chunks.ContainsKey(k)) _chunks[k] = BuildChunk(k);
                }
            CullLights();
        }

        private void CullLights()
        {
            _lights.RemoveAll(l => l == null);
            Vector3 p = Target.position;
            float max = _ws.lightLimitDistance * _ws.lightLimitDistance;
            foreach (var l in _lights) l.enabled = (l.transform.position - p).sqrMagnitude < max;
        }

        // ------------------------------------------------------------------ chunk building
        private GameObject BuildChunk(Vector2Int k)
        {
            var theme = Field.Theme;
            float S = Field.S, H = _ws.wallHeight;
            int N = Field.ChunkCells;
            int x0 = k.x * N, z0 = k.y * N;
            var root = new GameObject($"Chunk {k.x},{k.y}");
            root.transform.SetParent(transform, false);
            bool dark = Field.DarkChunk(k);

            // floor + ceiling
            Vector3 c0 = new Vector3(x0 * S, 0, z0 * S);
            float size = N * S;
            AddQuad(root, "Floor", c0, size, 0f, true, _floorMat, size / 4f);
            AddQuad(root, "Ceiling", c0, size, H, false, _ceilMat, size / 4f);
            var floorCol = root.AddComponent<BoxCollider>();
            floorCol.center = new Vector3(c0.x + size / 2, -0.5f, c0.z + size / 2);
            floorCol.size = new Vector3(size, 1f, size);
            var ceilCol = root.AddComponent<BoxCollider>();
            ceilCol.center = new Vector3(c0.x + size / 2, H + 0.5f, c0.z + size / 2);
            ceilCol.size = new Vector3(size, 1f, size);

            // walls + pillars + baseboard trim, one mesh
            var wb = new MeshBuilder();
            var trim = new MeshBuilder();
            const float T = 0.22f, trimH = 0.12f, trimT = 0.03f;
            for (int x = x0; x < x0 + N; x++)
                for (int z = z0; z < z0 + N; z++)
                {
                    if (Field.WallE(x, z))
                    {
                        float wx = (x + 1) * S;
                        wb.Box(new Vector3(wx - T / 2, 0, z * S - T / 2), new Vector3(wx + T / 2, H, (z + 1) * S + T / 2));
                        trim.Box(new Vector3(wx - T / 2 - trimT, 0, z * S), new Vector3(wx + T / 2 + trimT, trimH, (z + 1) * S));
                    }
                    if (Field.WallN(x, z))
                    {
                        float wz = (z + 1) * S;
                        wb.Box(new Vector3(x * S - T / 2, 0, wz - T / 2), new Vector3((x + 1) * S + T / 2, H, wz + T / 2));
                        trim.Box(new Vector3(x * S, 0, wz - T / 2 - trimT), new Vector3((x + 1) * S, trimH, wz + T / 2 + trimT));
                    }
                    if (Field.PillarAt(x, z))
                    {
                        const float P = 0.45f;
                        wb.Box(new Vector3(x * S - P, 0, z * S - P), new Vector3(x * S + P, H, z * S + P));
                    }
                }
            if (wb.HasGeometry)
            {
                var walls = new GameObject("Walls");
                walls.transform.SetParent(root.transform, false);
                var mesh = wb.ToMesh("walls");
                walls.AddComponent<MeshFilter>().sharedMesh = mesh;
                walls.AddComponent<MeshRenderer>().sharedMaterial = _wallMat;
                walls.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            if (trim.HasGeometry)
            {
                var t = new GameObject("Trim");
                t.transform.SetParent(root.transform, false);
                t.AddComponent<MeshFilter>().sharedMesh = trim.ToMesh("trim");
                t.AddComponent<MeshRenderer>().sharedMaterial = _trimMat;
            }

            // lights
            var panels = new MeshBuilder();
            var deadPanels = new MeshBuilder();
            for (int x = x0; x < x0 + N; x++)
                for (int z = z0; z < z0 + N; z++)
                {
                    if (!Field.HasLight(x, z)) continue;
                    Vector3 cc = Field.CellCenter(new Vector2Int(x, z));
                    bool off = dark || Field.LightBroken(x, z);
                    var mb = off ? deadPanels : panels;
                    mb.Box(new Vector3(cc.x - 0.6f, H - 0.03f, cc.z - 0.3f), new Vector3(cc.x + 0.6f, H, cc.z + 0.3f));
                    if (off) continue;
                    var lgo = new GameObject("Light");
                    lgo.transform.SetParent(root.transform, false);
                    lgo.transform.position = new Vector3(cc.x, H - 0.15f, cc.z);
                    var l = lgo.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = theme.lightColor;
                    l.intensity = theme.lightIntensity;
                    l.range = theme.lightRange;
                    l.shadows = LightShadows.None;
                    l.renderMode = LightRenderMode.Auto;
                    _lights.Add(l);
                    if (Field.LightFlickers(x, z)) lgo.AddComponent<Flicker>().Init(l, Hash.F(Field.Seed, x, z, 77));
                }
            if (panels.HasGeometry) AddMesh(root, "LightPanels", panels, _panelOn);
            if (deadPanels.HasGeometry) AddMesh(root, "DeadPanels", deadPanels, _panelOff);

            // exits and pickups
            for (int x = x0; x < x0 + N; x++)
                for (int z = z0; z < z0 + N; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (Field.IsExitCell(x, z))
                        ExitDoor.Create(root.transform, Field.CellCenter(cell), Field.ExitYawSteps(x, z) * 90f, theme, H);
                    var pk = Field.PickupAt(x, z);
                    if (pk != PickupKind.None && !_collected.Contains(cell))
                    {
                        Vector3 cc = Field.CellCenter(cell);
                        float ox = (Hash.F(Field.Seed, x, z, 31) - 0.5f) * S * 0.5f, oz = (Hash.F(Field.Seed, x, z, 32) - 0.5f) * S * 0.5f;
                        _spawnedPickups[cell] = Pickup.Create(root.transform, cc + new Vector3(ox, 0, oz), pk, cell, this);
                    }
                }

            root.isStatic = true;
            return root;
        }

        private static void AddMesh(GameObject root, string name, MeshBuilder mb, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void AddQuad(GameObject root, string name, Vector3 corner, float size, float y, bool up, Material mat, float tiles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var m = new Mesh { name = name };
            var a = new Vector3(corner.x, y, corner.z);
            var v = new[] { a, a + new Vector3(0, 0, size), a + new Vector3(size, 0, size), a + new Vector3(size, 0, 0) };
            // world-space UVs so the pattern lines up across chunks
            var uv = new Vector2[4];
            for (int i = 0; i < 4; i++) uv[i] = new Vector2(v[i].x / 4f, v[i].z / 4f);
            m.vertices = v;
            m.uv = uv;
            m.triangles = up ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// <summary>Accumulates axis-aligned boxes into one mesh with world-space UVs (1 texture repeat = 2 m).</summary>
    public class MeshBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly List<int> _t = new List<int>();
        public bool HasGeometry => _t.Count > 0;

        public void Box(Vector3 min, Vector3 max)
        {
            Face(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), Vector3.right);
            Face(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), Vector3.left);
            Face(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), Vector3.forward);
            Face(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z), Vector3.back);
            Face(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), Vector3.up);
            Face(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z), Vector3.down);
        }

        private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            for (int k = 0; k < 4; k++) _n.Add(n);
            foreach (var p in new[] { a, b, c, d })
            {
                // project onto the face plane for UVs: walls use horizontal distance + height
                Vector2 uv = Mathf.Abs(n.y) > 0.5f ? new Vector2(p.x, p.z) : Mathf.Abs(n.x) > 0.5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                _uv.Add(uv / 2f);
            }
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 1);
            _t.Add(i); _t.Add(i + 3); _t.Add(i + 2);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = _v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>A dying fluorescent tube: drops out at random, buzzes when it comes back.</summary>
    public class Flicker : MonoBehaviour
    {
        private Light _l;
        private float _base, _t, _seed;
        private AudioSource _a;

        public void Init(Light l, float seed)
        {
            _l = l; _base = l.intensity; _seed = seed * 100f;
            _a = gameObject.AddComponent<AudioSource>();
            _a.clip = ProcAudio.Buzz(); _a.spatialBlend = 1f; _a.maxDistance = 12f; _a.rolloffMode = AudioRolloffMode.Linear; _a.volume = 0.35f;
        }

        private void Update()
        {
            if (_l == null || !_l.enabled) return;
            _t += Time.deltaTime;
            float n = Mathf.PerlinNoise(_seed, _t * 3.5f);
            bool on = n > 0.32f || (Mathf.PerlinNoise(_seed + 5f, _t * 25f) > 0.6f && n > 0.2f);
            float target = on ? _base * (0.85f + 0.15f * Mathf.PerlinNoise(_seed + 9f, _t * 40f)) : 0.05f;
            if (on && _l.intensity < _base * 0.3f && _a != null && !_a.isPlaying) _a.Play();
            _l.intensity = target;
        }
    }
}
