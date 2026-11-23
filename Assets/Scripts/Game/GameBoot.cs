using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Backrooms
{
    /// <summary>
    /// The whole game hangs off this one object (scene "Main"): loads config and entities, runs the menu, builds levels,
    /// handles exits, death, the escape, pause and post-processing.
    /// </summary>
    public class GameBoot : MonoBehaviour
    {
        public enum Mode { Boot, Menu, Playing, Paused, Transition, Dead, Won }

        public static GameBoot Instance { get; private set; }
        public Mode State { get; private set; } = Mode.Boot;
        public PlayerController Player { get; private set; }
        public ChunkManager Chunks { get; private set; }
        public MazeField Field { get; private set; }
        public int LevelIndex { get; private set; }

        private GameConfig _cfg;
        private GameUI _ui;
        private EntityDirector _director;
        private EntityLibrary _library;
        private AudioSource _hum;
        private Volume _volume;
        private Vignette _vignette;
        private FilmGrain _grain;
        private ChromaticAberration _chroma;
        private ColorAdjustments _color;
        private LensDistortion _lens;
        private int _runSeed;
        private float _runTime;

        /// <summary>Make sure the game exists even if someone opens an empty scene.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            if (FindAnyObjectByType<GameBoot>() == null) new GameObject("Game").AddComponent<GameBoot>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _cfg = GameConfig.Load();
            AudioListener.volume = PlayerPrefs.GetFloat("vol", 0.8f);
            QualitySettings.vSyncCount = 1;

            _library = new GameObject("Entities").AddComponent<EntityLibrary>();
            _library.transform.SetParent(transform, false);
            _director = new GameObject("Director").AddComponent<EntityDirector>();
            _director.transform.SetParent(transform, false);
            Chunks = new GameObject("World").AddComponent<ChunkManager>();
            _ui = new GameObject("GameUI").AddComponent<GameUI>();
            _ui.transform.SetParent(transform, false);
            _ui.Build(this);

            _hum = gameObject.AddComponent<AudioSource>();
            _hum.clip = ProcAudio.Hum(); _hum.loop = true; _hum.spatialBlend = 0f; _hum.volume = 0f; _hum.Play();

            BuildPostProcessing();
            Player = PlayerController.Spawn(Vector3.zero, 0f);
            Player.ControlsEnabled = false;
        }

        private IEnumerator Start()
        {
            _ui.ShowLoading("Loading...");
            _ui.SetFade(0f);
            yield return _library.LoadAll();
            EnterMenu();
        }

        // ------------------------------------------------------------------ flow
        private void EnterMenu()
        {
            State = Mode.Menu;
            _runSeed = Random.Range(1, int.MaxValue);
            BuildLevel(0, attract: true);
            Player.ControlsEnabled = false;
            _director.Enabled = false;
            _ui.ShowMenu();
            _ui.SetFade(0f);
            SetCursor(false);
        }

        public void StartRun() => StartCoroutine(StartRunCo());

        private IEnumerator StartRunCo()
        {
            State = Mode.Transition;
            Time.timeScale = 1f;
            yield return Fade(0f, 1f, 0.6f);
            _runSeed = _cfg.world.seed != 0 ? _cfg.world.seed : Random.Range(1, int.MaxValue);
            _runTime = 0f;
            Player.ResetStats();
            BuildLevel(0, attract: false);
            _ui.ShowHud();
            SetCursor(true);
            yield return Fade(1f, 0f, 1.2f);
            Player.ControlsEnabled = true;
            _director.Enabled = true;
            State = Mode.Playing;
        }

        private void BuildLevel(int index, bool attract)
        {
            LevelIndex = Mathf.Clamp(index, 0, _cfg.levels.Length - 1);
            var theme = _cfg.levels[LevelIndex];
            _director.Clear();
            Chunks.Clear();
            int seed = (int)Hash.U(_runSeed, LevelIndex, 7);
            Field = new MazeField(seed, _cfg.world.cellSize, _cfg.world.chunkCells, theme);
            Chunks.Init(Field, _cfg.world);
            Chunks.Target = Player.transform;
            Vector3 start = Field.CellCenter(Field.StartCell);
            Player.Teleport(start, Random.Range(0, 4) * 90f);
            Chunks.BuildAllNow();
            ApplyTheme(theme);
            if (!attract)
            {
                _director.Begin(Field, theme, Player);
                _ui.LevelTitle(theme.displayName.ToUpperInvariant(), theme.subtitle);
            }
        }

        public void ExitReached()
        {
            if (State != Mode.Playing) return;
            StartCoroutine(ExitCo());
        }

        private IEnumerator ExitCo()
        {
            State = Mode.Transition;
            Player.ControlsEnabled = false;
            _director.Enabled = false;
            yield return Fade(0f, 1f, 1.5f);
            if (LevelIndex + 1 < _cfg.levels.Length)
            {
                BuildLevel(LevelIndex + 1, attract: false);
                yield return new WaitForSeconds(0.5f);
                yield return Fade(1f, 0f, 1.5f);
                Player.ControlsEnabled = true;
                _director.Enabled = true;
                State = Mode.Playing;
            }
            else
            {
                State = Mode.Won;
                _director.Clear();
                SetCursor(false);
                _ui.ShowWin(Stats());
                yield return Fade(1f, 0f, 1f);
            }
        }

        public void PlayerDied(string killer)
        {
            if (State != Mode.Playing) return;
            State = Mode.Dead;
            _director.Enabled = false;
            StartCoroutine(DeathCo(killer));
        }

        private IEnumerator DeathCo(string killer)
        {
            yield return new WaitForSeconds(2.2f);
            yield return Fade(0f, 0.85f, 0.8f);
            SetCursor(false);
            _ui.ShowDeath(killer, Stats());
            _ui.SetFade(0f);
        }

        private string Stats()
        {
            int mins = Mathf.FloorToInt(_runTime / 60f), secs = Mathf.FloorToInt(_runTime % 60f);
            var theme = _cfg.levels[LevelIndex];
            return $"Reached {theme.displayName} - {theme.subtitle}\nTime {mins}:{secs:00}   ·   Walked {Player.DistanceWalked:0} m";
        }

        public void ToMenu()
        {
            Time.timeScale = 1f;
            StopAllCoroutines();
            StartCoroutine(ToMenuCo());
        }

        private IEnumerator ToMenuCo()
        {
            yield return Fade(0f, 1f, 0.5f);
            Player.ResetStats();
            EnterMenu();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SetPaused(bool paused)
        {
            if (paused && State == Mode.Playing)
            {
                State = Mode.Paused; Time.timeScale = 0f; AudioListener.pause = true;
                Player.ControlsEnabled = false; SetCursor(false); _ui.ShowPause();
            }
            else if (!paused && State == Mode.Paused)
            {
                State = Mode.Playing; Time.timeScale = 1f; AudioListener.pause = false;
                Player.ControlsEnabled = true; SetCursor(true); _ui.ShowHud();
            }
        }

        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                _ui.SetFade(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            _ui.SetFade(to);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (State == Mode.Playing) SetPaused(true);
                else if (State == Mode.Paused) SetPaused(false);
            }
            if (State == Mode.Playing) _runTime += Time.deltaTime;
            if (State == Mode.Menu && Player != null) Player.AddYaw(Time.deltaTime * 4f);   // slow look-around behind the menu
            if (State == Mode.Playing && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursor(true);
            UpdateFear();
        }

        // ------------------------------------------------------------------ look & feel
        private void ApplyTheme(LevelTheme t)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = t.fogColor;
            RenderSettings.fogDensity = t.fogDensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = t.ambientColor;
            RenderSettings.skybox = null;
            if (Player != null && Player.Cam != null)
            {
                Player.Cam.clearFlags = CameraClearFlags.SolidColor;
                Player.Cam.backgroundColor = t.fogColor * 0.5f;
            }
            _hum.volume = t.humVolume;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.enabled = false;
        }

        private void BuildPostProcessing()
        {
            var go = new GameObject("PostProcessing");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.8f); bloom.threshold.Override(0.95f); bloom.scatter.Override(0.65f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            _color = profile.Add<ColorAdjustments>(true);
            _color.postExposure.Override(0f); _color.contrast.Override(8f); _color.saturation.Override(-8f);
            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.32f); _vignette.smoothness.Override(0.5f);
            _grain = profile.Add<FilmGrain>(true);
            _grain.type.Override(FilmGrainLookup.Medium3); _grain.intensity.Override(0.25f);
            _chroma = profile.Add<ChromaticAberration>(true);
            _chroma.intensity.Override(0.05f);
            _lens = profile.Add<LensDistortion>(true);
            _lens.intensity.Override(0f);
            _volume.sharedProfile = profile;
            ApplyBrightness();
        }

        public void ApplyBrightness()
        {
            if (_color != null) _color.postExposure.Override(Mathf.Log(PlayerPrefs.GetFloat("bright", 1f), 2f));
        }

        private void UpdateFear()
        {
            if (Player == null || _vignette == null) return;
            float fear = State == Mode.Playing || State == Mode.Dead ? 1f - Player.Sanity / _cfg.player.sanityMax : 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (2f + fear * 6f));
            _vignette.intensity.Override(0.32f + fear * 0.25f + (fear > 0.6f ? pulse * 0.08f : 0f));
            _grain.intensity.Override(0.25f + fear * 0.5f);
            _chroma.intensity.Override(0.05f + fear * 0.6f);
            _lens.intensity.Override(fear > 0.5f ? -0.25f * (fear - 0.5f) * 2f * (0.7f + 0.3f * pulse) : 0f);
            if (State == Mode.Dead) _vignette.intensity.Override(0.6f);
        }
    }
}
