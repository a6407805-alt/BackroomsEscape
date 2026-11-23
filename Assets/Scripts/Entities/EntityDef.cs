using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// One entity, read from StreamingAssets/Entities/&lt;folder&gt;/entity.json. Drop a new folder with an entity.json,
    /// a .glb model and some .ogg sounds into the game's StreamingAssets/Entities to add an entity - no Unity needed.
    /// Behaviours: "hunter" (sees you, chases), "listener" (blind, hunts by sound), "shy" (harmless until you see its face),
    /// "stalker" (rises out of the floor, walks through walls).
    /// </summary>
    [Serializable]
    public class EntityDef
    {
        public string id = "";
        public string name = "";
        public string nickname = "";
        public string objectClass = "";
        public string description = "";
        public string behaviour = "hunter";

        // model
        public string model = "model.glb";
        public float[] modelEuler = { 0, 0, 0 };   // rotation applied to the model so it stands up and faces +Z
        public float height = 2f;                  // metres, the model is scaled to this
        public float yOffset = 0f;

        // animation clip names inside the .glb
        public ClipNames clips = new ClipNames();
        public float walkAnimSpeed = 1f;
        public float runAnimSpeed = 1f;

        // movement and senses
        public float walkSpeed = 1.2f;
        public float runSpeed = 4f;
        public float turnSpeed = 300f;
        public float sightRange = 18f;
        public float sightAngle = 120f;
        public float hearing = 1f;               // multiplier on how far the player's noises carry to it
        public float attackRange = 1.3f;
        public float killDelay = 0.7f;           // seconds into the attack clip before you die
        public float triggerTime = 5f;           // shy: how long it screams before it runs
        public bool phaseThroughWalls = false;
        public float loseInterestTime = 20f;
        public float lifetime = 240f;            // seconds before it leaves (0 = never)
        public bool prefersDark = false;         // sees you much further when you're in the dark

        // spawning
        public string[] levels = { "level0" };
        public float spawnWeight = 1f;
        public int maxAlive = 1;
        public float minSpawnDistance = 18f;
        public float maxSpawnDistance = 36f;

        // sounds (.ogg next to entity.json)
        public SoundSet sounds = new SoundSet();

        // attribution (CC BY-SA 3.0): shown on the Credits screen
        public Credit credit = new Credit();

        [NonSerialized] public string Folder;
        [NonSerialized] public readonly Dictionary<string, AudioClip> LoadedSounds = new Dictionary<string, AudioClip>();

        public string ModelPath => Path.Combine(Folder, model);

        public AudioClip Sound(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            LoadedSounds.TryGetValue(file, out var c);
            return c;
        }

        public AudioClip RandomSound(string[] files)
        {
            if (files == null || files.Length == 0) return null;
            return Sound(files[UnityEngine.Random.Range(0, files.Length)]);
        }
    }

    [Serializable]
    public class ClipNames
    {
        public string idle = "Idle";
        public string walk = "Walk";
        public string run = "Run";
        public string attack = "Attack";
        public string special = "";   // shy: the trigger scream; stalker: rising from the floor
    }

    [Serializable]
    public class SoundSet
    {
        public string[] ambient = { };   // looped on the entity while it's around
        public string[] alert = { };     // once, when it notices you
        public string[] chase = { };     // looped music (2D) while it chases you
        public string[] attack = { };    // once, when it catches you
        public string[] idleLines = { };  // played now and then (lures, mutters)
        public float volume = 1f;
    }

    [Serializable]
    public class Credit
    {
        public string article = "";        // e.g. "SCP-096 - The \"Shy Guy\""
        public string author = "";
        public string articleUrl = "";
        public string assets = "";         // where the model / sounds came from
        public string assetAuthors = "";
        public string license = "CC BY-SA 3.0";
        public string licenseUrl = "https://creativecommons.org/licenses/by-sa/3.0/";
        public string changes = "";        // what we changed (required by CC BY-SA)
    }
}
