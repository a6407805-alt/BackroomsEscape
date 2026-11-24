using System;
using System.IO;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Every tunable number in the game, loaded from StreamingAssets/Config/game.json so it can be modded without Unity.
    /// If the file is missing, the defaults below are used (and written out in the editor so there is a file to edit).
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public const int CurrentVersion = 3;
        public int version = CurrentVersion;     // older game.json files are replaced with the new defaults
        public PlayerTuning player = new PlayerTuning();
        public WorldSettings world = new WorldSettings();
        public LevelTheme[] levels =
        {
            LevelTheme.Lobby(), LevelTheme.HabitableZone(), LevelTheme.PorcelainPit(), LevelTheme.PipeDreams()
        };

        public static GameConfig Current { get; private set; }

        public static string ConfigPath => Path.Combine(Application.streamingAssetsPath, "Config", "game.json");

        public static GameConfig Load()
        {
            GameConfig cfg = null;
            try
            {
                if (File.Exists(ConfigPath)) cfg = JsonUtility.FromJson<GameConfig>(File.ReadAllText(ConfigPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backrooms] game.json could not be read, using defaults: " + e.Message);
            }
            if (cfg == null || cfg.levels == null || cfg.levels.Length == 0 || cfg.version < CurrentVersion)
            {
                cfg = new GameConfig();
#if UNITY_EDITOR
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                    File.WriteAllText(ConfigPath, JsonUtility.ToJson(cfg, true));
                }
                catch (Exception) { }
#endif
            }
            Current = cfg;
            return cfg;
        }
    }

    /// <summary>First-person movement. Matches the feel of a classic PC horror walker (tune here after play-testing).</summary>
    [Serializable]
    public class PlayerTuning
    {
        public float walkSpeed = 3.2f;
        public float sprintSpeed = 6.0f;
        public float crouchSpeed = 1.6f;
        public float acceleration = 12f;
        public float airControl = 0.35f;
        public float jumpHeight = 0.9f;
        public float gravity = -19f;
        public float standHeight = 1.8f;
        public float crouchHeight = 1.05f;
        public float eyeOffset = 0.12f;           // camera sits this far below the top of the capsule
        public float mouseSensitivity = 0.12f;    // degrees per mouse count
        public float fov = 75f;
        public float headBobAmount = 0.045f;
        public float headBobSpeed = 1.9f;          // cycles per metre walked * speed
        public float staminaMax = 100f;
        public float staminaDrain = 18f;           // per second while sprinting
        public float staminaRegen = 14f;
        public float staminaRegenDelay = 1.2f;
        public float flashlightBatteryMax = 100f;
        public float flashlightDrain = 0.9f;       // per second while on
        public float flashlightRange = 22f;
        public float flashlightAngle = 58f;
        public float interactDistance = 2.6f;
        public float sanityMax = 100f;
        public float sanityDrainDark = 1.2f;       // per second standing in the dark
        public float sanityDrainEntity = 6f;       // per second while an entity is on screen
        public float sanityRegen = 0.6f;
        // how loud each movement is for entities that hunt by sound (metres of hearing range)
        public float noiseWalk = 9f;
        public float noiseSprint = 22f;
        public float noiseCrouch = 0f;
        public float noiseJumpLand = 16f;
        public float listenCooldown = 18f;        // Q: listen for the nearest drum piece
        public float noiseListen = 12f;
    }

    [Serializable]
    public class WorldSettings
    {
        public int seed = 0;                 // 0 = random each run
        public float cellSize = 4f;          // metres per maze cell
        public int chunkCells = 8;           // cells per chunk side
        public int loadRadius = 2;           // chunks around the player kept built
        public float wallHeight = 3.0f;
        public float lightLimitDistance = 30f;
    }

    /// <summary>One level of the Backrooms: look, sound, layout density and which entities live there.</summary>
    [Serializable]
    public class LevelTheme
    {
        public string id = "level0";
        public string displayName = "Level 0";
        public string subtitle = "The Lobby";
        public string layout = "maze";              // maze (endless) | pit (the toilet pyramid)
        public string wallStyle = "wallpaper";      // wallpaper | concrete | tile
        public Color wallColor = new Color(0.78f, 0.69f, 0.38f);
        public Color floorColor = new Color(0.48f, 0.41f, 0.22f);
        public Color ceilingColor = new Color(0.84f, 0.81f, 0.64f);
        public Color lightColor = new Color(1f, 0.97f, 0.82f);
        public float lightIntensity = 2.2f;
        public float lightRange = 9f;
        public int lightEveryCells = 2;             // a ceiling light every N cells
        public float lightBrokenChance = 0.08f;     // fixture that is dead
        public float lightFlickerChance = 0.06f;
        public float darkZoneChance = 0.12f;        // whole chunks with the power out
        public Color fogColor = new Color(0.55f, 0.5f, 0.3f);
        public float fogDensity = 0.045f;
        public Color ambientColor = new Color(0.28f, 0.25f, 0.16f);
        public float wallChance = 0.36f;            // per cell edge
        public float roomChance = 0.18f;            // chance a cell is an open hall (no walls)
        public float pillarChance = 0.08f;
        public int exitMinDistanceCells = 22;       // exits never spawn closer than this to where you arrive
        public int exitRegionCells = 18;            // one exit per region of this many cells
        public float exitChancePerRegion = 0f;    // 0 = the only way on is Aaron's door
        public float humVolume = 0.5f;
        public float spawnIntervalMin = 35f;
        public float spawnIntervalMax = 70f;
        public int maxEntities = 2;
        public float pickupChance = 0.035f;         // per cell
        // pit layout only: an upside-down stepped pyramid of toilets
        public float pitSize = 200f;               // width at the rim (the Great Pyramid's base is 230 m)
        public float tierHeight = 3.66f;           // 12 feet per tier
        public float terraceDepth = 2.6f;
        public float toiletSpacing = 1.4f;
        public string[] entities = { };             // ids from StreamingAssets/Entities; empty = every entity that lists this level (or "any")
        public int rosterSize = 4;                  // how many different entities are rolled for this level each run
        public string[] drumPieces = { "snare", "hihat", "kick" };   // Aaron's drum set pieces hidden on this level
        public int pieceMinCells = 9;               // pieces are hidden this many cells from Aaron or further
        public int pieceMaxCells = 26;

        public static LevelTheme Lobby() => new LevelTheme();

        public static LevelTheme HabitableZone() => new LevelTheme
        {
            id = "level1", displayName = "Level 1", subtitle = "Habitable Zone", wallStyle = "concrete",
            wallColor = new Color(0.52f, 0.52f, 0.5f), floorColor = new Color(0.3f, 0.3f, 0.29f), ceilingColor = new Color(0.45f, 0.45f, 0.44f),
            lightColor = new Color(0.86f, 0.93f, 1f), lightIntensity = 2.6f, lightRange = 11f, lightEveryCells = 3, lightBrokenChance = 0.18f,
            lightFlickerChance = 0.12f, darkZoneChance = 0.2f, fogColor = new Color(0.22f, 0.24f, 0.26f), fogDensity = 0.05f,
            ambientColor = new Color(0.12f, 0.13f, 0.14f), wallChance = 0.3f, roomChance = 0.28f, pillarChance = 0.22f,
            exitMinDistanceCells = 28, humVolume = 0.35f, spawnIntervalMin = 28f, spawnIntervalMax = 55f, maxEntities = 3, pickupChance = 0.05f,
            rosterSize = 5, drumPieces = new[] { "sticks", "crash", "racktom" }, pieceMinCells = 10, pieceMaxCells = 30
        };

        public static LevelTheme PorcelainPit() => new LevelTheme
        {
            id = "pit", displayName = "Level ???", subtitle = "The Porcelain Pit", layout = "pit", wallStyle = "tile",
            wallColor = new Color(0.82f, 0.84f, 0.86f), floorColor = new Color(0.62f, 0.64f, 0.66f), ceilingColor = new Color(0.5f, 0.5f, 0.5f),
            lightColor = new Color(0.88f, 0.95f, 1f), lightIntensity = 3f, lightRange = 14f, fogColor = new Color(0.33f, 0.36f, 0.4f), fogDensity = 0.012f,
            ambientColor = new Color(0.32f, 0.34f, 0.37f), humVolume = 0.3f, spawnIntervalMin = 30f, spawnIntervalMax = 60f, maxEntities = 3,
            rosterSize = 5, drumPieces = new[] { "throne", "floortom" }
        };

        public static LevelTheme PipeDreams() => new LevelTheme
        {
            id = "level2", displayName = "Level 2", subtitle = "Pipe Dreams", wallStyle = "tile",
            wallColor = new Color(0.36f, 0.3f, 0.26f), floorColor = new Color(0.2f, 0.18f, 0.16f), ceilingColor = new Color(0.22f, 0.2f, 0.18f),
            lightColor = new Color(1f, 0.62f, 0.4f), lightIntensity = 2.4f, lightRange = 8f, lightEveryCells = 3, lightBrokenChance = 0.3f,
            lightFlickerChance = 0.25f, darkZoneChance = 0.3f, fogColor = new Color(0.12f, 0.08f, 0.06f), fogDensity = 0.07f,
            ambientColor = new Color(0.07f, 0.05f, 0.04f), wallChance = 0.48f, roomChance = 0.08f, pillarChance = 0.05f,
            exitMinDistanceCells = 30, humVolume = 0.25f, spawnIntervalMin = 22f, spawnIntervalMax = 45f, maxEntities = 4, pickupChance = 0.06f,
            rosterSize = 6, drumPieces = new[] { "ride", "pedal" }, pieceMinCells = 12, pieceMaxCells = 34
        };
    }
}
