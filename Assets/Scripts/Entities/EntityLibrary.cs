using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using UnityEngine;
using UnityEngine.Networking;

namespace Backrooms
{
    /// <summary>
    /// Loads every modded/bundled entity folder in StreamingAssets/Entities AND merges the 345+ procedural SCP Foundation
    /// horror anomalies from ScpCatalog (350+ total rotating roster). Supports both glTF (.glb) models and articulated
    /// procedural nightmare rigs.
    /// </summary>
    public class EntityLibrary : MonoBehaviour
    {
        public static EntityLibrary Instance { get; private set; }
        public readonly List<EntityDef> Defs = new List<EntityDef>();
        public bool Ready { get; private set; }

        private readonly Dictionary<string, int> _indexById = new Dictionary<string, int>();
        private readonly Dictionary<string, GltfImport> _models = new Dictionary<string, GltfImport>();
        private readonly Dictionary<string, Task<GltfImport>> _loading = new Dictionary<string, Task<GltfImport>>();

        public static string Root => Path.Combine(Application.streamingAssetsPath, "Entities");

        private void Awake() => Instance = this;

        public IEnumerator LoadAll()
        {
            Defs.Clear();
            _indexById.Clear();
            int diskCount = 0;

            if (Directory.Exists(Root))
            {
                foreach (var dir in Directory.GetDirectories(Root))
                {
                    string json = Path.Combine(dir, "entity.json");
                    if (!File.Exists(json)) continue;
                    EntityDef def = null;
                    try { def = JsonUtility.FromJson<EntityDef>(File.ReadAllText(json)); }
                    catch (Exception e) { Debug.LogWarning($"[Backrooms] {json}: {e.Message}"); }
                    if (def == null || string.IsNullOrEmpty(def.id)) continue;
                    def.Folder = dir;
                    // Upgrade bundled SCPs with new voice-stealing / light-flickering traits
                    if (def.behaviour == "listener" || def.id == "scp-939") def.stealsVoices = true;
                    if (def.behaviour == "stalker" || def.id == "scp-106") def.flickersLights = true;
                    Register(def);
                    diskCount++;
                }
            }

            for (int i = 0; i < diskCount; i++)
                yield return LoadSounds(Defs[i]);

            // Merge the 345+ SCP horror catalog (any StreamingAssets folder with the same id overrides the procedural entry)
            var catalog = ScpCatalog.BuildAll();
            int procCount = 0;
            foreach (var c in catalog)
            {
                if (_indexById.ContainsKey(c.id)) continue;
                Register(c);
                procCount++;
            }

            Debug.Log($"[Backrooms] {Defs.Count} SCP entities ready ({diskCount} glTF + {procCount} procedural rotating catalog).");
            Ready = true;
        }

        private void Register(EntityDef def)
        {
            _indexById[def.id] = Defs.Count;
            Defs.Add(def);
        }

        private static IEnumerator LoadSounds(EntityDef def)
        {
            if (string.IsNullOrEmpty(def.Folder)) yield break;
            var files = new HashSet<string>();
            void Add(string[] a) { if (a != null) foreach (var f in a) if (!string.IsNullOrEmpty(f)) files.Add(f); }
            Add(def.sounds.ambient); Add(def.sounds.alert); Add(def.sounds.chase); Add(def.sounds.attack); Add(def.sounds.idleLines);
            foreach (var f in files)
            {
                string path = Path.Combine(def.Folder, f);
                if (!File.Exists(path)) { Debug.LogWarning("[Backrooms] missing sound " + path); continue; }
                var type = f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? AudioType.WAV : AudioType.OGGVORBIS;
                using var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var clip = DownloadHandlerAudioClip.GetContent(req);
                    clip.name = f;
                    def.LoadedSounds[f] = clip;
                }
                else Debug.LogWarning($"[Backrooms] {path}: {req.error}");
            }
        }

        public EntityDef Get(string id) => _indexById.TryGetValue(id, out int idx) ? Defs[idx] : null;
        public int IndexOf(EntityDef def) => def != null && _indexById.TryGetValue(def.id, out int idx) ? idx : -1;
        public EntityDef GetByIndex(int idx) => idx >= 0 && idx < Defs.Count ? Defs[idx] : null;

        public static bool UsesGltf(EntityDef def) =>
            def != null && !string.IsNullOrEmpty(def.model) && !string.IsNullOrEmpty(def.ModelPath) && File.Exists(def.ModelPath);

        public async Task<GltfImport> Model(EntityDef def)
        {
            if (!UsesGltf(def)) return null;
            if (_models.TryGetValue(def.id, out var g)) return g;
            if (!_loading.TryGetValue(def.id, out var task))
            {
                task = LoadModel(def);
                _loading[def.id] = task;
            }
            g = await task;
            if (g != null) _models[def.id] = g;
            return g;
        }

        private static async Task<GltfImport> LoadModel(EntityDef def)
        {
            if (!UsesGltf(def)) return null;
            var gltf = new GltfImport(materialGenerator: new LitMaterialGenerator());
            var settings = new ImportSettings { AnimationMethod = AnimationMethod.Legacy, GenerateMipMaps = true };
            bool ok = await gltf.LoadFile(def.ModelPath, new Uri(def.ModelPath), settings);
            if (!ok) { Debug.LogWarning("[Backrooms] could not load " + def.ModelPath); gltf.Dispose(); return null; }
            return gltf;
        }

        /// <summary>Spawns either a glTF model or a procedural articulated horror rig under `parent`.</summary>
        public async Task<(Animation legacyAnim, ProceduralScpRig procRig)> SpawnVisuals(EntityDef def, Transform parent)
        {
            if (parent == null || def == null) return (null, null);

            if (UsesGltf(def))
            {
                var anim = await Spawn(def, parent);
                if (parent == null) return (null, null);
                if (anim != null || parent.Find("Model") != null)
                    return (anim, null);
            }

            var rig = ProceduralScpRig.Build(def, parent);
            return (null, rig);
        }

        /// <summary>Instantiates a glTF entity's model under `parent`, stood up, scaled to def.height and grounded.</summary>
        public async Task<Animation> Spawn(EntityDef def, Transform parent)
        {
            var gltf = await Model(def);
            if (gltf == null || parent == null) return null;
            var holder = new GameObject("Model").transform;
            holder.SetParent(parent, false);
            var fix = new GameObject("Orient").transform;
            fix.SetParent(holder, false);
            var e = def.modelEuler != null && def.modelEuler.Length == 3 ? def.modelEuler : new float[] { 0, 0, 0 };
            fix.localRotation = Quaternion.Euler(e[0], e[1], e[2]);
            bool ok = await gltf.InstantiateMainSceneAsync(fix);
            if (!ok || holder == null) return null;

            var anim = holder.GetComponentInChildren<Animation>();
            if (anim != null)
            {
                foreach (AnimationState st in anim) st.wrapMode = WrapMode.Loop;
                string idle = def.clips.idle;
                if (!string.IsNullOrEmpty(idle) && anim[idle] != null) { anim.Play(idle); anim[idle].time = 0; anim.Sample(); }
            }
            foreach (var smr in holder.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Fit(holder, def);
            return anim;
        }

        private static void Fit(Transform holder, EntityDef def)
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = -min;
            var baked = new Mesh();
            foreach (var smr in holder.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.BakeMesh(baked, true);
                var m = smr.transform.localToWorldMatrix;
                foreach (var v in baked.vertices)
                {
                    var w = m.MultiplyPoint3x4(v);
                    min = Vector3.Min(min, w); max = Vector3.Max(max, w);
                }
            }
            foreach (var mr in holder.GetComponentsInChildren<MeshRenderer>())
            {
                min = Vector3.Min(min, mr.bounds.min); max = Vector3.Max(max, mr.bounds.max);
            }
            Destroy(baked);
            if (min.x > max.x) return;
            float h = Mathf.Max(0.01f, max.y - min.y);
            float s = def.height / h;
            Vector3 pivot = holder.position;
            holder.localScale = Vector3.one * s;
            float bottom = pivot.y + (min.y - pivot.y) * s;
            Vector3 centerXZ = new Vector3((min.x + max.x) / 2f - pivot.x, 0, (min.z + max.z) / 2f - pivot.z) * s;
            holder.position = new Vector3(pivot.x - centerXZ.x, pivot.y + (pivot.y - bottom) + def.yOffset, pivot.z - centerXZ.z);
        }
    }

    public class LitMaterialGenerator : IMaterialGenerator
    {
        public UnityEngine.Material GetDefaultMaterial(bool pointsSupport = false) => Mats.Lit(new Color(0.7f, 0.7f, 0.7f));

        public UnityEngine.Material GenerateMaterial(GLTFast.Schema.MaterialBase gltfMaterial, IGltfReadable gltf, bool pointsSupport = false)
        {
            Color color = Color.white;
            Texture2D tex = null, normal = null;
            float smoothness = 0.2f, bump = 1f;
            var pbr = gltfMaterial.PbrMetallicRoughness;
            if (pbr != null)
            {
                color = pbr.BaseColor;
                var ti = pbr.BaseColorTexture;
                if (ti != null && ti.index >= 0) tex = gltf.GetTexture(ti.index);
                smoothness = Mathf.Clamp01(1f - pbr.roughnessFactor) * 0.6f;   // keep skin/cloth from looking wet under the fluorescents
            }
            var ni = gltfMaterial.NormalTexture;
            if (ni != null && ni.index >= 0) { normal = gltf.GetTexture(ni.index); bump = ni.scale; }
            UnityEngine.Material m = gltfMaterial.GetAlphaMode() == GLTFast.Schema.MaterialBase.AlphaMode.Mask
                ? Mats.CutoutNormal(color, tex, normal, gltfMaterial.alphaCutoff, smoothness, bump)
                : Mats.LitNormal(color, tex, normal, smoothness, bump);
            m.name = gltfMaterial.name;
            return m;
        }

        public void SetLogger(ICodeLogger logger) { }
    }
}
