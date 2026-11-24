using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Backrooms
{
    /// <summary>
    /// Root game controller (scene "Main"): loads config and the 350+ rotating SCP catalog, runs Solo &amp; Co-Op flows,
    /// deterministic level generation, live microphone voice mimicry, exits, death, escape, pause and URP horror post-FX.
    /// </summary>
    public class GameBoot : MonoBehaviour
    {
        public enum Mode : byte { Boot = 0, Menu = 1, Playing = 2, Paused = 3, Transition = 4, Dead = 5, Won = 6 }

        public static GameBoot Instance { get; private set; }
        public Mode State { get; private set; } = Mode.Boot;
        public PlayerController Player { get; private set; }
        public ChunkManager Chunks { get; private set; }
        public MazeField Field { get; private set; }
        public int LevelIndex { get; private set; }
        public GameUI UI => _ui;
        public AaronRoom Room { get; private set; }
        public PitLevel Pit { get; private set; }
        /// <summary>Drum pieces back on Aaron's kit / in your hands right now.</summary>
        public readonly HashSet<string> Placed = new HashSet<string>();
        public readonly List<string> Carried = new List<string>();
        public bool KitComplete
        {
            get
            {
                foreach (var l in _cfg.levels) foreach (var id in l.drumPieces) if (!Placed.Contains(id)) return false;
                return true;
            }
        }
        public int TotalPieces { get { int n = 0; foreach (var l in _cfg.levels) n += l.drumPieces.Length; return n; } }
        public string[] LevelPieces => _cfg.levels[LevelIndex].drumPieces;
        /// <summary>Where each of this level's pieces lies (cell for maze levels, world position for the pit).</summary>
        public readonly Dictionary<string, Vector3> PieceSpots = new Dictionary<string, Vector3>();
        private readonly Dictionary<Vector2Int, string> _pieceCells = new Dictionary<Vector2Int, string>();
        public int RunSeed => _runSeed;

        private GameConfig _cfg;
        private GameUI _ui;
        private EntityDirector _director;
        private EntityLibrary _library;
        private BackroomsNet _net;
        private VoiceMimicSystem _voice;
        private AudioSource _hum;
        private Volume _volume;
        private Vignette _vignette;
        private FilmGrain _grain;
        private ChromaticAberration _chroma;
        private ColorAdjustments _color;
        private LensDistortion _lens;
        private int _runSeed;
        private float _runTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            if (FindAnyObjectByType<GameBoot>() == null) new GameObject("Game").AddComponent<GameBoot>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.runInBackground = true; // critical for Co-Op & proximity voice
            _cfg = GameConfig.Load();
            AudioListener.volume = PlayerPrefs.GetFloat("vol", 0.8f);

            _library = new GameObject("Entities").AddComponent<EntityLibrary>();
            _library.transform.SetParent(transform, false);
            _director = new GameObject("Director").AddComponent<EntityDirector>();
            _director.transform.SetParent(transform, false);
            _net = new GameObject("CoopNet").AddComponent<BackroomsNet>();
            _net.transform.SetParent(transform, false);
            _voice = new GameObject("VoiceMimic").AddComponent<VoiceMimicSystem>();
            _voice.transform.SetParent(transform, false);
            Chunks = new GameObject("World").AddComponent<ChunkManager>();
            _ui = new GameObject("GameUI").AddComponent<GameUI>();
            _ui.transform.SetParent(transform, false);
            _ui.Build(this);

            _hum = gameObject.AddComponent<AudioSource>();
            _hum.clip = ProcAudio.Hum(); _hum.loop = true; _hum.spatialBlend = 0f; _hum.volume = 0f; _hum.Play();

            BuildPostProcessing();
            Player = PlayerController.Spawn(Vector3.zero, 0f);
            Player.ControlsEnabled = false;
            GraphicsOptions.ApplyAll();
        }

        private IEnumerator Start()
        {
            _ui.ShowLoading("Loading 350+ SCP Containment Archives...");
            _ui.SetFade(0f);
            yield return _library.LoadAll();
            EnterMenu();
        }

        // ------------------------------------------------------------------ Solo & Co-Op Flow
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

        public void StartSoloRun()
        {
            _net.StopNetwork();
            StartRun();
        }

        public void StartCoopHost()
        {
            _ui.ShowLoading("Opening Co-Op Room...");
            _net.StartHost(() => StartRun());
        }

        public void StartRun()
        {
            if (BackroomsNet.IsClient)
            {
                _ui.Toast("WAITING FOR CO-OP HOST TO RESTART RUN...", 3f);
                return;
            }
            StopAllCoroutines();
            StartCoroutine(StartRunCo());
        }

        private IEnumerator StartRunCo()
        {
            State = Mode.Transition;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return Fade(0f, 1f, 0.6f);
            _runSeed = _cfg.world.seed != 0 ? _cfg.world.seed : Random.Range(1, int.MaxValue);
            _runTime = 0f;
            _director.ResetRunDossier();
            Player.ResetStats();
            Placed.Clear();
            Carried.Clear();
            BuildLevel(0, attract: false);
            if (BackroomsNet.IsHost)
                _net.BroadcastRunState(_runSeed, LevelIndex, Mode.Playing);
            _ui.ShowHud();
            SetCursor(true);
            yield return Fade(1f, 0f, 1.1f);
            Player.ControlsEnabled = true;
            _director.Enabled = true;
            State = Mode.Playing;
            AnnounceFloorBreach();
        }

        public void ApplySyncedRunStateFromHost(int seed, int levelIndex, Mode hostMode, string killer)
        {
            StopAllCoroutines();
            StartCoroutine(ClientSyncCo(seed, levelIndex, hostMode, killer));
        }

        private IEnumerator ClientSyncCo(int seed, int levelIndex, Mode hostMode, string killer)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (hostMode == Mode.Dead)
            {
                Player.Kill(null, string.IsNullOrEmpty(killer) ? "CONTAINMENT BREACH" : killer);
                yield break;
            }
            if (hostMode == Mode.Won)
            {
                State = Mode.Won;
                _director.Clear();
                SetCursor(false);
                _ui.ShowWin(Stats());
                _ui.SetFade(0f);
                yield break;
            }

            State = Mode.Transition;
            Player.ControlsEnabled = false;
            yield return Fade(0f, 1f, 0.4f);

            bool newRun = seed != _runSeed;
            _runSeed = seed;
            if (newRun) { _runTime = 0f; _director.ResetRunDossier(); }
            Player.ResetStats();
            BuildLevel(levelIndex, attract: false);
            _ui.ShowHud();
            SetCursor(true);
            yield return Fade(1f, 0f, 0.8f);
            Player.ControlsEnabled = true;
            _director.Enabled = true;
            State = Mode.Playing;
            AnnounceFloorBreach();
        }

        private void AnnounceFloorBreach()
        {
            int total = _library.Defs.Count;
            int active = _director.ActiveRoster.Count;
            string coopTag = BackroomsNet.IsOnline ? $"CO-OP ({_net.JoinCode}) · " : "";
            _ui.Toast($"{coopTag}ROTATING BREACH: {active} ACTIVE SCPs DRAFTED FROM {total} ANOMALIES  [TAB DOSSIER]", 5f);
        }

        private void BuildLevel(int index, bool attract)
        {
            LevelIndex = Mathf.Clamp(index, 0, _cfg.levels.Length - 1);
            var theme = _cfg.levels[LevelIndex];
            _director.Clear();
            Chunks.Clear();
            if (Room != null) Destroy(Room.gameObject);
            if (Pit != null) Destroy(Pit.gameObject);
            Room = null; Pit = null;
            int seed = (int)Hash.U(_runSeed, LevelIndex, 7);
            Field = new MazeField(seed, _cfg.world.cellSize, _cfg.world.chunkCells, theme);
            PieceSpots.Clear();
            _pieceCells.Clear();

            Vector3 roomCenter;
            if (theme.layout == "pit")
            {
                Chunks.enabled = false;
                Pit = PitLevel.Create(theme, seed);
                roomCenter = Pit.RimSpot;
                for (int i = 0; i < theme.drumPieces.Length; i++)
                    PieceSpots[theme.drumPieces[i]] = Pit.PieceSpot(i, theme.drumPieces.Length);
                foreach (var kv in PieceSpots)
                    if (!Placed.Contains(kv.Key) && !Carried.Contains(kv.Key)) DrumPickup.Create(Pit.transform, kv.Value, kv.Key);
            }
            else
            {
                Chunks.enabled = true;
                PlacePieces(theme, seed);
                Chunks.Init(Field, _cfg.world);
                Chunks.Target = Player.transform;
                Chunks.PieceAt = c => _pieceCells.TryGetValue(c, out var id) && !Placed.Contains(id) && !Carried.Contains(id) && State != Mode.Menu ? id : null;
                roomCenter = Field.CellCenter(Field.StartCell);
            }
            if (attract)
            {
                Player.Teleport(roomCenter + new Vector3(0, 0, -3f), Random.Range(0, 4) * 90f);
            }
            else
            {
                Room = AaronRoom.Create(roomCenter, theme.drumPieces);
                Vector3 start = roomCenter + new Vector3(0, 0, -3.2f);
                if (BackroomsNet.IsOnline)
                {
                    // co-op: spread the squad out in front of Aaron's kit
                    int slot = _net.MySlot;
                    start += new Vector3(((slot % 3) - 1) * 0.9f, 0f, -((slot / 3)) * 0.9f);
                }
                Player.Teleport(start, 0f);
            }
            if (theme.layout != "pit") Chunks.BuildAllNow();
            ApplyTheme(theme);
            if (!attract)
            {
                _director.Begin(Field, theme, Player);
                _ui.LevelTitle(theme.displayName.ToUpperInvariant(), theme.subtitle);
                _ui.Toast(LevelIndex == 0
                    ? "Someone took Aaron's drum set apart and hid it in the Backrooms. He is very angry. Find the pieces. [Q] listen"
                    : "Aaron came with you. Of course he did. More of his kit is down here.", 8f);
            }
        }

        /// <summary>Hide this level's drum pieces in a ring of cells around Aaron's corner.</summary>
        private void PlacePieces(LevelTheme theme, int seed)
        {
            int n = theme.drumPieces.Length;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n + Hash.F(seed, i, 0, 41) * 0.6f) * Mathf.PI * 2f;
                float d = Mathf.Lerp(theme.pieceMinCells, theme.pieceMaxCells, (i + Hash.F(seed, i, 0, 42)) / Mathf.Max(1, n));
                var cell = Field.StartCell + new Vector2Int(Mathf.RoundToInt(Mathf.Cos(a) * d), Mathf.RoundToInt(Mathf.Sin(a) * d));
                while (_pieceCells.ContainsKey(cell)) cell += Vector2Int.right;
                _pieceCells[cell] = theme.drumPieces[i];
                Field.OpenCells.Add(cell);
                PieceSpots[theme.drumPieces[i]] = Field.CellCenter(cell);
            }
        }

        public void PickUpPiece(string id)
        {
            if (!Carried.Contains(id)) Carried.Add(id);
            _ui.Toast("Got the " + DrumKit.NameOf(id).ToLowerInvariant() + ". Bring it back to Aaron - follow the noise.", 5f);
        }

        public void DeliverPieces()
        {
            foreach (var id in Carried) Placed.Add(id);
            Carried.Clear();
        }

        /// <summary>Q: you stop and listen - which way is the nearest piece still missing on this level?</summary>
        public void Listen()
        {
            Vector3 best = default; float bd = float.MaxValue; string bid = null;
            foreach (var kv in PieceSpots)
            {
                if (Placed.Contains(kv.Key) || Carried.Contains(kv.Key)) continue;
                float d = (kv.Value - Player.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = kv.Value; bid = kv.Key; }
            }
            if (bid == null) { _ui.Toast(Carried.Count > 0 ? "Nothing else down here. Take it back to Aaron." : "You hear only Aaron, drumming.", 3f); return; }
            _ui.PointTo(best, 4f, "a faint ringing - the " + DrumKit.NameOf(bid).ToLowerInvariant());
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
            yield return Fade(0f, 1f, 1.4f);
            if (LevelIndex + 1 < _cfg.levels.Length)
            {
                Player.SetDowned(false, "");
                BuildLevel(LevelIndex + 1, attract: false);
                if (BackroomsNet.IsHost)
                    _net.BroadcastRunState(_runSeed, LevelIndex, Mode.Playing);
                yield return new WaitForSeconds(0.4f);
                yield return Fade(1f, 0f, 1.3f);
                Player.ControlsEnabled = true;
                _director.Enabled = true;
                State = Mode.Playing;
                AnnounceFloorBreach();
            }
            else
            {
                State = Mode.Won;
                _director.Clear();
                if (Room != null) Destroy(Room.gameObject);
                if (BackroomsNet.IsHost)
                    _net.BroadcastRunState(_runSeed, LevelIndex, Mode.Won);
                SetCursor(false);
                _ui.ShowWin(Stats());
                yield return Fade(1f, 0f, 1f);
            }
        }

        public void PlayerDied(string killer)
        {
            if (State == Mode.Dead) return;
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
            int logged = _director != null ? _director.EncounteredIds.Count : 0;
            return $"Reached {theme.displayName} - {theme.subtitle}\nAaron's drum set: {Placed.Count}/{TotalPieces} pieces\nTime {mins}:{secs:00}   ·   Walked {Player.DistanceWalked:0} m   ·   SCPs Logged {logged}";
        }

        public void ToMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            _net.StopNetwork();
            StopAllCoroutines();
            StartCoroutine(ToMenuCo());
        }

        private IEnumerator ToMenuCo()
        {
            yield return Fade(0f, 1f, 0.45f);
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
            bool online = BackroomsNet.IsOnline;
            if (paused && State == Mode.Playing)
            {
                State = Mode.Paused;
                if (!online) { Time.timeScale = 0f; AudioListener.pause = true; }
                Player.ControlsEnabled = false;
                SetCursor(false);
                _ui.ShowPause();
            }
            else if (!paused && State == Mode.Paused)
            {
                State = Mode.Playing;
                Time.timeScale = 1f;
                AudioListener.pause = false;
                Player.ControlsEnabled = true;
                SetCursor(true);
                _ui.ShowHud();
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
            if (kb != null && (kb.f11Key.wasPressedThisFrame || ((kb.leftAltKey.isPressed || kb.rightAltKey.isPressed) && kb.enterKey.wasPressedThisFrame)))
                GraphicsOptions.ToggleFullscreen();
            if (State == Mode.Playing) _runTime += Time.deltaTime;
            if (State == Mode.Menu && Player != null) Player.AddYaw(Time.deltaTime * 4f);
            if (State == Mode.Playing && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursor(true);
            UpdateFear();
        }

        // ------------------------------------------------------------------ Look & Feel
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
                Player.Cam.farClipPlane = t.layout == "pit" ? 450f : 120f;
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

        private bool _effects = true;

        /// <summary>Settings: film grain, chromatic aberration and lens distortion on or off (fear effects included).</summary>
        public void ApplyEffects(bool on)
        {
            _effects = on;
            if (_grain != null) _grain.active = on;
            if (_chroma != null) _chroma.active = on;
            if (_lens != null) _lens.active = on;
        }

        public void ApplyBrightness()
        {
            if (_color != null) _color.postExposure.Override(Mathf.Log(PlayerPrefs.GetFloat("bright", 1f), 2f));
        }

        private void UpdateFear()
        {
            if (Player == null || _vignette == null) return;
            float fear = State == Mode.Playing || State == Mode.Dead ? 1f - Player.Sanity / _cfg.player.sanityMax : 0f;
            if (Player.Downed) fear = Mathf.Max(fear, 0.82f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (2f + fear * 6f));
            _vignette.intensity.Override(0.32f + fear * 0.25f + (fear > 0.6f ? pulse * 0.08f : 0f));
            _grain.intensity.Override(0.25f + fear * 0.5f);
            _chroma.intensity.Override(0.05f + fear * 0.6f);
            _lens.intensity.Override(fear > 0.5f ? -0.25f * (fear - 0.5f) * 2f * (0.7f + 0.3f * pulse) : 0f);
            if (State == Mode.Dead) _vignette.intensity.Override(0.6f);
        }
    }
}
