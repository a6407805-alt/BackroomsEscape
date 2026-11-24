using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Drafts a deterministic Rotating Roster of 18 SCP anomalies per floor out of the 350+ SCP catalog, spawns &amp; despawns
    /// them around the Co-Op squad, replicates entities to Co-Op clients, tracks encountered SCPs, and drives chase music!
    /// </summary>
    public class EntityDirector : MonoBehaviour
    {
        public static EntityDirector Instance { get; private set; }
        public bool Enabled = true;
        public readonly List<EntityAgent> Alive = new List<EntityAgent>();
        public readonly List<EntityDef> ActiveRoster = new List<EntityDef>();
        public readonly HashSet<string> EncounteredIds = new HashSet<string>();

        public const int RosterPerFloor = 18;
        /// <summary>Names of this floor's line-up (pause menu).</summary>
        public List<string> Roster => ActiveRoster.ConvertAll(d => d.name);

        private MazeField _field;
        private LevelTheme _theme;
        private PlayerController _player;
        private readonly List<PlayerTarget> _targets = new List<PlayerTarget>(8);
        private float _next;
        private AudioSource _music;
        private EntityAgent _chaser;
        private static AudioClip _procChaseMusic;

        private void Awake()
        {
            Instance = this;
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.spatialBlend = 0f;
            _music.volume = 0f;
        }

        public void ResetRunDossier() => EncounteredIds.Clear();

        public void MarkEncountered(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (EncounteredIds.Add(id))
            {
                var d = EntityLibrary.Instance?.Get(id);
                if (d != null)
                    GameUI.Instance?.Toast($"ANOMALY LOGGED: {d.name.ToUpperInvariant()} — {(string.IsNullOrEmpty(d.nickname) ? d.objectClass : d.nickname.ToUpperInvariant())}", 3.5f);
            }
        }

        public void Begin(MazeField field, LevelTheme theme, PlayerController player)
        {
            Clear();
            _field = field;
            _theme = theme;
            _player = player;

            DraftRotatingRoster(field.Seed, theme);

            _next = Random.Range(theme.spawnIntervalMin, theme.spawnIntervalMax) * 0.45f;
            foreach (var d in ActiveRoster)
                if (EntityLibrary.UsesGltf(d)) _ = EntityLibrary.Instance.Model(d);
        }

        /// <summary>
        /// Deterministically drafts 18 terrifying SCPs from the 350+ catalog using the floor's seed so:
        /// 1. Host and all Co-Op Clients share the exact same active floor breach roster.
        /// 2. Every single floor and run rotates fresh SCPs players haven't seen before, while guaranteeing
        ///    voice-stealing mimics, weeping statues, light-fearing umbras, doorway stalkers, and glTF horrors!
        /// </summary>
        private void DraftRotatingRoster(int floorSeed, LevelTheme theme)
        {
            ActiveRoster.Clear();
            var all = EntityLibrary.Instance.Defs;
            if (all.Count == 0) return;

            var picked = new HashSet<string>();
            var rng = new System.Random(floorSeed ^ 0x5A17C3);

            var gltfPool = new List<EntityDef>();
            var voicePool = new List<EntityDef>();
            var weepingPool = new List<EntityDef>();
            var darkDoorPool = new List<EntityDef>();
            var wildPool = new List<EntityDef>();

            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (d.spawnWeight <= 0f || d.role == "npc") continue;   // Aaron and other characters are never spawned as monsters
                bool matchesLevel = d.levels == null || d.levels.Length == 0 || System.Array.IndexOf(d.levels, theme.id) >= 0 || System.Array.IndexOf(d.levels, "any") >= 0;
                if (!matchesLevel) continue;

                if (EntityLibrary.UsesGltf(d)) gltfPool.Add(d);
                else
                {
                    wildPool.Add(d);
                    if (d.stealsVoices) voicePool.Add(d);
                    if (d.freezesWhenWatched) weepingPool.Add(d);
                    if (d.fearsFlashlight || d.behaviour == "doorman" || d.flickersLights) darkDoorPool.Add(d);
                }
            }

            void PickN(List<EntityDef> bucket, int count)
            {
                var copy = new List<EntityDef>(bucket);
                for (int i = copy.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (copy[i], copy[j]) = (copy[j], copy[i]);
                }
                int added = 0;
                for (int i = 0; i < copy.Count && added < count; i++)
                {
                    if (picked.Add(copy[i].id))
                    {
                        ActiveRoster.Add(copy[i]);
                        added++;
                    }
                }
            }

            PickN(gltfPool, Mathf.Max(2, theme.rosterSize));   // the real SCP - Containment Breach models (rosterSize per floor)
            PickN(voicePool, 4);     // 4 voice-recording / mimic SCPs so mic baiting is always present!
            PickN(weepingPool, 3);   // 3 freeze-when-watched SCPs
            PickN(darkDoorPool, 4);  // 4 shadow / doorway / light-breaker SCPs
            PickN(wildPool, RosterPerFloor - ActiveRoster.Count); // Fill to 18 from the 350+ pool!
        }

        public void Clear()
        {
            foreach (var a in Alive) if (a != null) Destroy(a.gameObject);
            Alive.Clear();
            _chaser = null;
            if (_music != null) { _music.Stop(); _music.volume = 0f; }
        }

        public bool AnyVisibleTo(PlayerController p)
        {
            foreach (var a in Alive) if (a != null && a.VisibleTo(p, 26f)) return true;
            return false;
        }

        private void Update()
        {
            Alive.RemoveAll(a => a == null);
            if (_player == null || _field == null) return;

            // Clients receive authoritative entity snapshots from Host
            if (BackroomsNet.IsClient)
            {
                Music();
                return;
            }

            BackroomsNet.GatherTargets(_player, _targets);

            // Despawn entities left >85m from EVERY living player
            for (int i = Alive.Count - 1; i >= 0; i--)
            {
                var a = Alive[i];
                if (a == null || a.IsChasing) continue;
                bool nearAnyone = false;
                for (int t = 0; t < _targets.Count; t++)
                    if ((a.transform.position - _targets[t].pos).sqrMagnitude <= 85f * 85f) { nearAnyone = true; break; }
                if (!nearAnyone && _targets.Count > 0)
                    Destroy(a.gameObject);
            }

            int extraCoopCap = BackroomsNet.IsOnline ? Mathf.Min(3, BackroomsNet.Instance.Avatars.Count) : 0;
            int maxEntities = _theme.maxEntities + 1 + extraCoopCap;

            if (Enabled && !_player.Dead && ActiveRoster.Count > 0)
            {
                _next -= Time.deltaTime;
                float sanity01 = _player.Sanity / GameConfig.Current.player.sanityMax;
                if (sanity01 < 0.35f) _next -= Time.deltaTime * 0.5f;
                if (VoiceMimicSystem.Instance != null && VoiceMimicSystem.Instance.IsAudibleToMonsters)
                    _next -= Time.deltaTime * 0.65f; // Talking loudly draws anomalies out faster!

                if (_next <= 0f)
                {
                    _next = Random.Range(_theme.spawnIntervalMin, _theme.spawnIntervalMax) * 0.85f;
                    if (Alive.Count < maxEntities) TrySpawn();
                }
            }
            Music();
        }

        private void TrySpawn()
        {
            float total = 0f;
            var options = new List<EntityDef>();
            foreach (var d in ActiveRoster)
            {
                int count = 0;
                foreach (var a in Alive) if (a != null && a.Def == d) count++;
                if (count >= d.maxAlive) continue;
                options.Add(d);
                total += d.spawnWeight;
            }
            if (options.Count == 0) return;
            float r = Random.value * total;
            EntityDef pick = options[0];
            foreach (var d in options) { r -= d.spawnWeight; if (r <= 0) { pick = d; break; } }
            var first = Spawn(pick);
            for (int i = 1; i < pick.groupSize && first != null; i++)
            {
                Vector3 p = first.transform.position + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                if (GameBoot.Instance.Pit != null) p.y = GameBoot.Instance.Pit.HeightAt(p); else p = _field.CellCenter(_field.CellAt(p));
                Alive.Add(EntityAgent.Create(pick, _field, _player, p, first.transform.eulerAngles.y));
            }
        }

        public EntityAgent Spawn(EntityDef def)
        {
            if (!FindSpot(def, out Vector3 pos, out Vector3 anchorPlayerPos)) return null;
            Vector3 look = anchorPlayerPos - pos;
            look.y = 0;
            float yaw = def.behaviour == "shy"
                ? Random.Range(0f, 360f)
                : Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward).eulerAngles.y;
            var a = EntityAgent.Create(def, _field, _player, pos, yaw);
            Alive.Add(a);
            Debug.Log($"[Backrooms] {def.name} ({def.nickname}) breached {Vector3.Distance(pos, anchorPlayerPos):0}m away [{def.behaviour}]");
            return a;
        }

        private static bool Return(Vector3 p, out Vector3 pos) { pos = p; return true; }

        private bool FindSpot(EntityDef def, out Vector3 pos, out Vector3 anchorPos)
        {
            pos = default;
            anchorPos = _player.transform.position;
            if (_targets.Count > 0)
                anchorPos = _targets[Random.Range(0, _targets.Count)].pos;

            var pit = GameBoot.Instance.Pit;
            if (pit != null)
            {
                for (int tries = 0; tries < 20; tries++)
                {
                    Vector3 p = pit.RandomSpot(anchorPos, def.behaviour == "apparition" ? 6f : def.minSpawnDistance, def.behaviour == "apparition" ? 14f : def.maxSpawnDistance);
                    if (def.behaviour == "stalker" || def.behaviour == "trap") return Return(p, out pos);
                    Vector3 vp = _player.Cam.WorldToViewportPoint(p + Vector3.up);
                    bool onScreen = vp.z > 0 && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
                    if (onScreen && !Physics.Linecast(_player.Cam.transform.position, p + Vector3.up, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return Return(p, out pos);
                }
                return false;
            }
            float S = _field.S;
            if (def.behaviour == "trap")
            {
                // somewhere along the way you're facing
                var ahead = _field.CellAt(_player.transform.position + _player.transform.forward * Random.Range(10f, 22f));
                foreach (var (cell, dist) in Pathfinder.Reachable(_field, ahead, 2, 30))
                    if (!_field.IsExitCell(cell.x, cell.y)) return Return(_field.CellCenter(cell), out pos);
            }
            int minSteps = Mathf.Max(2, Mathf.RoundToInt(def.minSpawnDistance / S));
            int maxSteps = Mathf.Max(minSteps + 2, Mathf.RoundToInt(def.maxSpawnDistance / S));
            var cells = Pathfinder.Reachable(_field, _field.CellAt(anchorPos), maxSteps, 2500);
            var candidates = new List<Vector2Int>();
            foreach (var (cell, dist) in cells)
                if (dist >= minSteps && !_field.IsExitCell(cell.x, cell.y)) candidates.Add(cell);

            for (int tries = 0; tries < 28 && candidates.Count > 0; tries++)
            {
                var c = candidates[Random.Range(0, candidates.Count)];
                Vector3 p = _field.CellCenter(c);
                if (def.behaviour == "stalker") { pos = p; return true; }

                // Never pop into existence right in front of ANY Co-Op player's eyes
                bool seenByAnyone = false;
                for (int i = 0; i < _targets.Count; i++)
                {
                    var t = _targets[i];
                    Vector3 toP = (p + Vector3.up) - t.head;
                    float d = toP.magnitude;
                    if (d < 9f) { seenByAnyone = true; break; }
                    if (d < 34f && Vector3.Angle(t.lookDir, toP / d) < 58f
                        && !Physics.Linecast(t.head, p + Vector3.up, ~0, QueryTriggerInteraction.Ignore))
                    {
                        seenByAnyone = true;
                        break;
                    }
                }
                if (seenByAnyone) continue;
                pos = p;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ Co-Op Snapshot Replication
        public void WriteHostSnapshots(BinaryWriter bw)
        {
            int valid = 0;
            for (int i = 0; i < Alive.Count; i++)
                if (Alive[i] != null && EntityLibrary.Instance.IndexOf(Alive[i].Def) >= 0) valid++;

            bw.Write(valid);
            for (int i = 0; i < Alive.Count; i++)
            {
                var a = Alive[i];
                if (a == null) continue;
                int defIdx = EntityLibrary.Instance.IndexOf(a.Def);
                if (defIdx < 0) continue;
                Vector3 p = a.transform.position;
                bw.Write(a.NetId);
                bw.Write((ushort)defIdx);
                bw.Write(p.x); bw.Write(p.y); bw.Write(p.z);
                bw.Write(a.transform.eulerAngles.y);
                bw.Write((byte)a.Current);
                bw.Write(a.CurrentMotionSpeed);
            }
        }

        public void ApplyClientSnapshots(BinaryReader br)
        {
            if (_field == null || _player == null) return;
            int count = br.ReadInt32();
            var seenIds = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                int netId = br.ReadInt32();
                int defIdx = br.ReadUInt16();
                Vector3 pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                float yaw = br.ReadSingle();
                var st = (EntityAgent.State)br.ReadByte();
                float spd = br.ReadSingle();

                seenIds.Add(netId);
                EntityAgent agent = Alive.Find(x => x != null && x.NetId == netId);
                if (agent == null)
                {
                    var def = EntityLibrary.Instance.GetByIndex(defIdx);
                    if (def == null) continue;
                    agent = EntityAgent.Create(def, _field, _player, pos, yaw, forcedNetId: netId, clientPuppet: true);
                    Alive.Add(agent);
                }
                agent.ApplyPuppetSnapshot(pos, yaw, st, spd);
                if (Vector3.Distance(pos, _player.transform.position) < 16f || agent.VisibleTo(_player, 26f))
                    MarkEncountered(agent.Def.id);
            }

            for (int i = Alive.Count - 1; i >= 0; i--)
            {
                if (Alive[i] != null && !seenIds.Contains(Alive[i].NetId))
                {
                    Destroy(Alive[i].gameObject);
                    Alive.RemoveAt(i);
                }
            }
        }

        public void PlayClientEntityCue(int netId, byte cueKind, int clipId)
        {
            var agent = Alive.Find(x => x != null && x.NetId == netId);
            agent?.PlayNetworkCue(cueKind, clipId);
        }

        // ------------------------------------------------------------------ Dynamic Chase Music
        private void Music()
        {
            EntityAgent chaser = null;
            float bestDist = 42f;
            foreach (var a in Alive)
            {
                if (a == null || !a.IsChasing) continue;
                float d = Vector3.Distance(a.transform.position, _player.transform.position);
                if (d < bestDist) { bestDist = d; chaser = a; }
            }

            if (chaser != _chaser && chaser != null)
            {
                var clip = chaser.Def.RandomSound(chaser.Def.sounds.chase) ?? ProceduralChasePulse();
                if (clip != null && _music.clip != clip) { _music.clip = clip; _music.Play(); }
                else if (!_music.isPlaying && _music.clip != null) _music.Play();
            }
            _chaser = chaser;
            float target = chaser != null && !_player.Dead ? 0.68f : 0f;
            _music.volume = Mathf.MoveTowards(_music.volume, target, Time.deltaTime * (target > 0 ? 1.5f : 0.45f));
            if (_music.volume <= 0f && _music.isPlaying) _music.Stop();
        }

        private static AudioClip ProceduralChasePulse()
        {
            if (_procChaseMusic != null) return _procChaseMusic;
            const int rate = 22050;
            int n = rate * 4; // 4-second 120 BPM industrial horror chase loop
            var d = new float[n];
            var rng = new System.Random(909);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float beat = (t * 2f) % 1f;
                float kick = Mathf.Exp(-beat * 14f) * Mathf.Sin(2f * Mathf.PI * (68f - beat * 26f) * t);
                float offBeat = ((t * 2f + 0.5f) % 1f);
                float clang = (offBeat < 0.18f ? Mathf.Exp(-offBeat * 24f) : 0f) *
                              (Mathf.Sin(2f * Mathf.PI * 417f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 631f * t) * 0.3f);
                float riser = 0.16f * Mathf.Sin(2f * Mathf.PI * (55f + 4f * Mathf.Sin(2f * Mathf.PI * 0.25f * t)) * t);
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * 0.12f;
                d[i] = Mathf.Clamp(kick * 0.68f + clang * 0.35f + riser + lp * 0.08f, -0.95f, 0.95f);
            }
            _procChaseMusic = AudioClip.Create("ProceduralChaseLoop", n, 1, rate, false);
            _procChaseMusic.SetData(d, 0);
            return _procChaseMusic;
        }
    }
}
