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
    /// <summary>Finds every entity folder in StreamingAssets/Entities, loads its sounds, and loads/instantiates its glTF model.</summary>
    public class EntityLibrary : MonoBehaviour
    {
        public static EntityLibrary Instance { get; private set; }
        public readonly List<EntityDef> Defs = new List<EntityDef>();
        public bool Ready { get; private set; }

        private readonly Dictionary<string, GltfImport> _models = new Dictionary<string, GltfImport>();
        private readonly Dictionary<string, Task<GltfImport>> _loading = new Dictionary<string, Task<GltfImport>>();

        public static string Root => Path.Combine(Application.streamingAssetsPath, "Entities");

        private void Awake() => Instance = this;

        public IEnumerator LoadAll()
        {
            Defs.Clear();
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
                    Defs.Add(def);
                }
            }
            Debug.Log($"[Backrooms] {Defs.Count} entities found in {Root}");
            foreach (var def in Defs) yield return LoadSounds(def);
            Ready = true;
        }

        private static IEnumerator LoadSounds(EntityDef def)
        {
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

        public EntityDef Get(string id) => Defs.Find(d => d.id == id);

        public async Task<GltfImport> Model(EntityDef def)
        {
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
            if (!File.Exists(def.ModelPath)) { Debug.LogWarning("[Backrooms] missing model " + def.ModelPath); return null; }
            var gltf = new GltfImport(materialGenerator: new LitMaterialGenerator());
            var settings = new ImportSettings { AnimationMethod = AnimationMethod.Legacy, GenerateMipMaps = true };
            bool ok = await gltf.LoadFile(def.ModelPath, new Uri(def.ModelPath), settings);
            if (!ok) { Debug.LogWarning("[Backrooms] could not load " + def.ModelPath); gltf.Dispose(); return null; }
            return gltf;
        }

        /// <summary>Instantiates the entity's model under `parent`, stood up, scaled to def.height and grounded. Returns the Animation (may be null).</summary>
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

        /// <summary>Scale the posed model to def.height and put its lowest point on the floor.</summary>
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
            // after scaling around the holder's origin, the bottom moved to pivot.y + (min.y - pivot.y) * s
            float bottom = pivot.y + (min.y - pivot.y) * s;
            Vector3 centerXZ = new Vector3((min.x + max.x) / 2f - pivot.x, 0, (min.z + max.z) / 2f - pivot.z) * s;
            holder.position = new Vector3(pivot.x - centerXZ.x, pivot.y + (pivot.y - bottom) + def.yOffset, pivot.z - centerXZ.z);
        }
    }

    /// <summary>
    /// Makes glTF materials from our URP Lit templates (so we don't depend on glTFast's shader graphs being in the build).
    /// Supports base colour + texture and alpha cutout - all the SCP models need.
    /// </summary>
    public class LitMaterialGenerator : IMaterialGenerator
    {
        public UnityEngine.Material GetDefaultMaterial(bool pointsSupport = false) => Mats.Lit(new Color(0.7f, 0.7f, 0.7f));

        public UnityEngine.Material GenerateMaterial(GLTFast.Schema.MaterialBase gltfMaterial, IGltfReadable gltf, bool pointsSupport = false)
        {
            Color color = Color.white;
            Texture2D tex = null;
            var pbr = gltfMaterial.PbrMetallicRoughness;
            if (pbr != null)
            {
                color = pbr.BaseColor;
                var ti = pbr.BaseColorTexture;
                if (ti != null && ti.index >= 0) tex = gltf.GetTexture(ti.index);
            }
            UnityEngine.Material m = gltfMaterial.GetAlphaMode() == GLTFast.Schema.MaterialBase.AlphaMode.Mask
                ? Mats.Cutout(color, tex, gltfMaterial.alphaCutoff)
                : Mats.Lit(color, tex, 0.2f);
            m.name = gltfMaterial.name;
            return m;
        }

        public void SetLogger(ICodeLogger logger) { }
    }
}
