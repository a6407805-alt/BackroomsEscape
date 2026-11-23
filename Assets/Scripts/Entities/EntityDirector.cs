using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>Decides when and where entities show up on the current level, and plays chase music.</summary>
    public class EntityDirector : MonoBehaviour
    {
        public static EntityDirector Instance { get; private set; }
        public bool Enabled = true;
        public readonly List<EntityAgent> Alive = new List<EntityAgent>();

        private MazeField _field;
        private LevelTheme _theme;
        private PlayerController _player;
        private readonly List<EntityDef> _pool = new List<EntityDef>();
        private float _next;
        private AudioSource _music;
        private EntityAgent _chaser;

        private void Awake()
        {
            Instance = this;
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true; _music.spatialBlend = 0f; _music.volume = 0f;
        }

        public void Begin(MazeField field, LevelTheme theme, PlayerController player)
        {
            Clear();
            _field = field; _theme = theme; _player = player;
            _pool.Clear();
            foreach (var d in EntityLibrary.Instance.Defs)
            {
                bool listed = theme.entities != null && theme.entities.Length > 0
                    ? System.Array.IndexOf(theme.entities, d.id) >= 0
                    : d.levels != null && System.Array.IndexOf(d.levels, theme.id) >= 0;
                if (listed && d.spawnWeight > 0) _pool.Add(d);
            }
            _next = Random.Range(theme.spawnIntervalMin, theme.spawnIntervalMax) * 0.5f;   // first one comes a bit sooner
            // start loading the models now so the first spawn doesn't hitch
            foreach (var d in _pool) _ = EntityLibrary.Instance.Model(d);
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
            foreach (var a in Alive) if (a != null && a.VisibleTo(p, 25f)) return true;
            return false;
        }

        private void Update()
        {
            Alive.RemoveAll(a => a == null);
            if (_player == null || _field == null) return;

            // despawn things left far behind
            foreach (var a in Alive)
                if (!a.IsChasing && (a.transform.position - _player.transform.position).sqrMagnitude > 80f * 80f) Destroy(a.gameObject);

            if (Enabled && !_player.Dead && _pool.Count > 0)
            {
                _next -= Time.deltaTime;
                // fear speeds things up a little
                float sanity01 = _player.Sanity / GameConfig.Current.player.sanityMax;
                if (sanity01 < 0.3f) _next -= Time.deltaTime * 0.5f;
                if (_next <= 0f)
                {
                    _next = Random.Range(_theme.spawnIntervalMin, _theme.spawnIntervalMax);
                    if (Alive.Count < _theme.maxEntities) TrySpawn();
                }
            }
            Music();
        }

        private void TrySpawn()
        {
            // weighted pick among entities that still have room
            float total = 0f;
            var options = new List<EntityDef>();
            foreach (var d in _pool)
            {
                int count = 0;
                foreach (var a in Alive) if (a != null && a.Def == d) count++;
                if (count >= d.maxAlive) continue;
                options.Add(d); total += d.spawnWeight;
            }
            if (options.Count == 0) return;
            float r = Random.value * total;
            EntityDef pick = options[0];
            foreach (var d in options) { r -= d.spawnWeight; if (r <= 0) { pick = d; break; } }
            Spawn(pick);
        }

        public EntityAgent Spawn(EntityDef def)
        {
            if (!FindSpot(def, out Vector3 pos)) return null;
            Vector3 look = _player.transform.position - pos; look.y = 0;
            float yaw = def.behaviour == "shy" ? Random.Range(0f, 360f) : Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward).eulerAngles.y;
            var a = EntityAgent.Create(def, _field, _player, pos, yaw);
            Alive.Add(a);
            Debug.Log($"[Backrooms] {def.name} appeared {Vector3.Distance(pos, _player.transform.position):0}m away");
            return a;
        }

        /// <summary>A reachable cell the right walking distance from the player that the player can't currently see.</summary>
        private bool FindSpot(EntityDef def, out Vector3 pos)
        {
            pos = default;
            float S = _field.S;
            int minSteps = Mathf.Max(1, Mathf.RoundToInt(def.minSpawnDistance / S));
            int maxSteps = Mathf.Max(minSteps + 1, Mathf.RoundToInt(def.maxSpawnDistance / S));
            var cells = Pathfinder.Reachable(_field, _field.CellAt(_player.transform.position), maxSteps, 2500);
            var candidates = new List<Vector2Int>();
            foreach (var (cell, dist) in cells)
                if (dist >= minSteps && !_field.IsExitCell(cell.x, cell.y)) candidates.Add(cell);
            for (int tries = 0; tries < 25 && candidates.Count > 0; tries++)
            {
                var c = candidates[Random.Range(0, candidates.Count)];
                Vector3 p = _field.CellCenter(c);
                if (def.behaviour == "stalker") return Return(p, out pos);   // it rises out of the floor anyway
                Vector3 vp = _player.Cam.WorldToViewportPoint(p + Vector3.up);
                bool onScreen = vp.z > 0 && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
                if (onScreen && !Physics.Linecast(_player.Cam.transform.position, p + Vector3.up, ~0, QueryTriggerInteraction.Ignore)) continue;
                return Return(p, out pos);
            }
            return false;
        }

        private static bool Return(Vector3 p, out Vector3 pos) { pos = p; return true; }

        private void Music()
        {
            EntityAgent chaser = null;
            foreach (var a in Alive) if (a != null && a.IsChasing && a.Def.sounds.chase != null && a.Def.sounds.chase.Length > 0) { chaser = a; break; }
            if (chaser != _chaser && chaser != null)
            {
                var clip = chaser.Def.RandomSound(chaser.Def.sounds.chase);
                if (clip != null && _music.clip != clip) { _music.clip = clip; _music.Play(); }
            }
            _chaser = chaser;
            float target = chaser != null && !_player.Dead ? 0.7f : 0f;
            _music.volume = Mathf.MoveTowards(_music.volume, target, Time.deltaTime * (target > 0 ? 1.5f : 0.4f));
            if (_music.volume <= 0f && _music.isPlaying) _music.Stop();
        }
    }
}
