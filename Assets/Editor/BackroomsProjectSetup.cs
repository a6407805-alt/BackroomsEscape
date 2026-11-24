using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Backrooms.EditorTools
{
    /// <summary>
    /// One-time project setup that runs whenever scripts reload: creates the Main scene (the only scene, holding the
    /// Game object), puts it in Build Settings, and creates the URP material templates in Resources/BackroomsMaterials
    /// so those shaders and their alpha-clip / emission variants are always in builds. Safe to run again: Backrooms > Repair Project Setup.
    /// </summary>
    [InitializeOnLoad]
    public static class BackroomsProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string MatDir = "Assets/Resources/BackroomsMaterials";

        static BackroomsProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Run(false);
            };
        }

        [MenuItem("Backrooms/Repair Project Setup", priority = 20)]
        private static void Repair() => Run(true);

        public static void RepairFromCli() => Run(true);

        [MenuItem("Backrooms/Open Entities Folder", priority = 21)]
        private static void OpenEntities()
        {
            Directory.CreateDirectory("Assets/StreamingAssets/Entities");
            EditorUtility.RevealInFinder("Assets/StreamingAssets/Entities");
        }

        private static void Run(bool verbose)
        {
            bool changed = false;
            changed |= EnsureMaterials();
            changed |= EnsureScene();
            changed |= EnsureBuildSettings();
            if (PlayerSettings.productName != "BackroomsEscape") { PlayerSettings.productName = "BackroomsEscape"; changed = true; }
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
            { PlayerSettings.bundleVersion = "0.1.0"; changed = true; }
            if (changed) AssetDatabase.SaveAssets();
            if (verbose || changed) Debug.Log("[Backrooms] Project setup checked" + (changed ? " (fixed some things)." : "."));
        }

        private static bool EnsureMaterials()
        {
            bool changed = false;
            Directory.CreateDirectory(MatDir);
            changed |= Mat("Lit", "Universal Render Pipeline/Lit", m => { });
            changed |= Mat("LitCutout", "Universal Render Pipeline/Lit", m =>
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            });
            changed |= Mat("LitEmissive", "Universal Render Pipeline/Lit", m =>
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            changed |= Mat("Unlit", "Universal Render Pipeline/Unlit", m => { });
            return changed;
        }

        private static bool Mat(string name, string shader, System.Action<Material> setup)
        {
            string path = MatDir + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return false;
            var sh = Shader.Find(shader);
            if (sh == null) { Debug.LogWarning("[Backrooms] shader not found yet: " + shader); return false; }
            var m = new Material(sh) { name = name };
            setup(m);
            AssetDatabase.CreateAsset(m, path);
            return true;
        }

        private static bool EnsureScene()
        {
            if (File.Exists(ScenePath)) return false;
            if (SceneManager.GetActiveScene().isDirty) return false;   // don't throw away unsaved work; try again next reload
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Game").AddComponent<GameBoot>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            return true;
        }

        private static bool EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 1 && scenes[0].path == ScenePath && scenes[0].enabled) return false;
            if (!File.Exists(ScenePath)) return false;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return true;
        }
    }
}
