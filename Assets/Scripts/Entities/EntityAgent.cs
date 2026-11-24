using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// One entity in the world. Movement follows maze cells (A*) unless it phases through walls. Behaviour comes from
    /// EntityDef.behaviour: hunter, listener, shy, stalker.
    /// </summary>
    public class EntityAgent : MonoBehaviour
    {
        public enum State { Loading, Spawning, Idle, Wander, Investigate, Chase, Triggered, Attack, Leaving }

        public EntityDef Def { get; private set; }
        public State Current { get; private set; } = State.Loading;
        public bool IsChasing => Current == State.Chase || Current == State.Triggered || Current == State.Attack;

        private MazeField _field;
        private PlayerController _player;
        private Animation _anim;
        private AudioSource _voice, _loop;
        private List<Vector2Int> _path;
        private int _pathIdx;
        private Vector2Int _pathGoal;
        private float _repath, _stateTime, _age, _sinceSeen, _nextLine, _idleFor;
        private Vector3 _investigate;
        private string _playing;
        private bool _killed, _hidden;
        private Renderer[] _renderers;
        private Vector3 _home;

        private static PitLevel Pit => GameBoot.Instance != null ? GameBoot.Instance.Pit : null;
        private static float GroundAt(Vector3 p) => Pit != null ? Pit.HeightAt(p) : 0f;
        private bool Phase => Def.phaseThroughWalls || Pit != null;   // the pit has no maze: things just come at you

        public static EntityAgent Create(EntityDef def, MazeField field, PlayerController player, Vector3 pos, float yaw)
        {
            var go = new GameObject(def.name);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var a = go.AddComponent<EntityAgent>();
            a.Def = def; a._field = field; a._player = player;
            a._voice = go.AddComponent<AudioSource>();
            a._voice.spatialBlend = 1f; a._voice.minDistance = 3f; a._voice.maxDistance = 40f; a._voice.rolloffMode = AudioRolloffMode.Logarithmic;
            a._voice.volume = def.sounds.volume;
            a._loop = go.AddComponent<AudioSource>();
            a._loop.spatialBlend = 1f; a._loop.minDistance = 2f; a._loop.maxDistance = 25f; a._loop.loop = true; a._loop.rolloffMode = AudioRolloffMode.Linear;
            a._loop.volume = def.sounds.volume * 0.8f;
            a.LoadModel();
            return a;
        }

        private async void LoadModel()
        {
            var anim = await EntityLibrary.Instance.Spawn(Def, transform);
            if (this == null) return;
            _anim = anim;
            var amb = Def.RandomSound(Def.sounds.ambient);
            if (amb != null) { _loop.clip = amb; _loop.Play(); }
            _nextLine = Random.Range(8f, 20f);
            _renderers = GetComponentsInChildren<Renderer>();
            _home = transform.position;
            switch (Def.behaviour)
            {
                case "stalker": Enter(State.Spawning); break;
                case "shy": case "apparition": Enter(State.Idle); break;
                case "trap": SetHidden(true); Enter(State.Idle); break;
                default: Enter(Def.spawnClipTime > 0f ? State.Spawning : State.Wander); break;
            }
        }

        // ------------------------------------------------------------------ helpers
        private void Enter(State s)
        {
            Current = s; _stateTime = 0f; _path = null;
            switch (s)
            {
                case State.Spawning:
                    Play(Def.clips.special, false, 1f);
                    break;
                case State.Idle:
                    Play(Def.clips.idle, true, 1f);
                    _idleFor = Random.Range(2f, 5f);
                    break;
                case State.Wander:
                    Play(Def.clips.walk, true, Def.walkAnimSpeed);
                    break;
                case State.Investigate:
                    Play(Def.clips.run, true, Def.runAnimSpeed);
                    break;
                case State.Triggered:
                    Play(Def.clips.special, false, 1f);
                    Say(Def.sounds.alert);
                    break;
                case State.Chase:
                    bool fast = Def.behaviour != "stalker";
                    Play(fast ? Def.clips.run : Def.clips.walk, true, fast ? Def.runAnimSpeed : Def.walkAnimSpeed);
                    if (Def.behaviour != "shy") Say(Def.sounds.alert);
                    _sinceSeen = 0f;
                    break;
                case State.Attack:
                    Play(Def.clips.attack, false, 1f);
                    Say(Def.sounds.attack);
                    break;
                case State.Leaving:
                    Play(Def.clips.idle, true, 1f);
                    break;
            }
        }

        private void Play(string clip, bool loop, float speed)
        {
            if (_anim == null || string.IsNullOrEmpty(clip) || _anim[clip] == null) return;
            var st = _anim[clip];
            st.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            st.speed = speed;
            if (_playing == clip && loop) return;
            if (!loop) st.time = 0f;
            _anim.CrossFade(clip, 0.25f);
            _playing = clip;
        }

        private void SetHidden(bool h)
        {
            _hidden = h;
            if (_renderers != null) foreach (var r in _renderers) if (r != null) r.enabled = !h;
        }

        /// <summary>visibility "flashlight": only shows up inside your beam (or when your sanity is nearly gone).</summary>
        private void UpdateVisibility()
        {
            if (Def.visibility != "flashlight" || Def.behaviour == "trap") return;
            bool show = _player.Sanity < GameConfig.Current.player.sanityMax * 0.2f;
            if (!show && _player.Flashlight.enabled)
            {
                Vector3 to = transform.position + Vector3.up * Def.height * 0.6f - _player.Flashlight.transform.position;
                show = to.magnitude < _player.Flashlight.range && Vector3.Angle(_player.Flashlight.transform.forward, to) < _player.Flashlight.spotAngle * 0.5f;
            }
            if (show == _hidden) SetHidden(!show);
        }

        private void Say(string[] files)
        {
            var c = Def.RandomSound(files);
            if (c != null) _voice.PlayOneShot(c, Def.sounds.volume);
        }

        private Vector3 Eye => transform.position + Vector3.up * Def.height * 0.85f;
        private Vector3 PlayerHead => _player.Cam.transform.position;
        private float FlatDist => Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(_player.transform.position.x, _player.transform.position.z));

        private static bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            // walls/floors/doors block; the player's own capsule and triggers don't
            foreach (var h in Physics.RaycastAll(a, d / len, len, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponentInParent<PlayerController>() == null) return false;
            return true;
        }

        /// <summary>Can this entity see the player?</summary>
        private bool SeesPlayer()
        {
            if (_player == null || _player.Dead) return false;
            float range = Def.sightRange;
            if (Def.prefersDark) range *= _player.Exposure < 0.3f && !_player.Flashlight.enabled ? 1.6f : 0.6f;
            if (_player.Flashlight.enabled) range *= 1.25f;
            if (_player.Crouching) range *= 0.75f;
            Vector3 to = PlayerHead - Eye;
            if (to.sqrMagnitude > range * range) return false;
            Vector3 flat = new Vector3(to.x, 0, to.z);
            if (Vector3.Angle(transform.forward, flat) > Def.sightAngle * 0.5f && to.magnitude > 2.5f) return false;
            return Clear(Eye, PlayerHead);
        }

        /// <summary>Is the entity on the player's screen with nothing in between? (sanity, and 096's trigger)</summary>
        public bool VisibleTo(PlayerController p, float maxDist = 40f)
        {
            if (p == null || p.Cam == null || _anim == null || _hidden) return false;
            Vector3 chest = transform.position + Vector3.up * Def.height * 0.75f;
            Vector3 vp = p.Cam.WorldToViewportPoint(chest);
            if (vp.z <= 0 || vp.z > maxDist || vp.x < 0.05f || vp.x > 0.95f || vp.y < 0.05f || vp.y > 0.95f) return false;
            return Clear(p.Cam.transform.position, chest);
        }

        // ------------------------------------------------------------------ movement
        private bool MoveTo(Vector3 target, float speed, bool phase)
        {
            Vector3 pos = transform.position;
            Vector3 step;
            if (phase)
            {
                step = target - pos;
            }
            else
            {
                var here = _field.CellAt(pos);
                var goal = _field.CellAt(target);
                _repath -= Time.deltaTime;
                if (_path == null || _repath <= 0f || goal != _pathGoal)
                {
                    _repath = 0.6f;
                    _pathGoal = goal;
                    _path = Pathfinder.Find(_field, here, goal);
                    _pathIdx = 0;
                }
                if (_path == null || _path.Count == 0) return true;
                while (_pathIdx < _path.Count - 1 && _path[_pathIdx] == here) _pathIdx++;
                Vector3 wp = _pathIdx >= _path.Count - 1 ? target : _field.CellCenter(_path[_pathIdx]);
                // cut straight to the target once we share its cell
                if (here == goal) wp = target;
                step = wp - pos;
            }
            step.y = 0;
            float dist = step.magnitude;
            if (dist < 0.05f) return (new Vector3(target.x - pos.x, 0, target.z - pos.z)).magnitude < 0.6f;
            Vector3 dir = step / dist;
            var want = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, Def.turnSpeed * Time.deltaTime);
            float move = Mathf.Min(dist, speed * Time.deltaTime);
            var np = new Vector3(pos.x + dir.x * move, 0f, pos.z + dir.z * move);
            np.y = GroundAt(np);
            transform.position = np;
            return (new Vector3(target.x - transform.position.x, 0, target.z - transform.position.z)).magnitude < 0.6f;
        }

        private void FacePlayer()
        {
            Vector3 d = _player.transform.position - transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), Def.turnSpeed * 2f * Time.deltaTime);
        }

        private Vector3 RandomNearbyCell()
        {
            if (Pit != null) return Pit.RandomSpot(transform.position, 2f, 20f);
            var cells = Pathfinder.Reachable(_field, _field.CellAt(transform.position), 6, 400);
            var c = cells[Random.Range(0, cells.Count)].cell;
            return _field.CellCenter(c);
        }

        // ------------------------------------------------------------------ brain
        private void Update()
        {
            if (Current == State.Loading || _player == null) return;
            float dt = Time.deltaTime;
            _stateTime += dt; _age += dt;

            UpdateVisibility();
            if (Def.sanityDrain > 0f && !_hidden && FlatDist < 10f) _player.SanityHit(Def.sanityDrain * dt);

            if (!Def.harmless && Def.behaviour != "trap" && Current != State.Attack && !_player.Dead && FlatDist < Def.attackRange && Mathf.Abs(_player.transform.position.y - transform.position.y) < 1.5f
                && Current != State.Spawning && Current != State.Leaving && !(Def.behaviour == "shy" && Current == State.Idle))
                Enter(State.Attack);

            // idle chatter / lures
            _nextLine -= dt;
            if (_nextLine <= 0f && Current != State.Attack) { _nextLine = Random.Range(10f, 25f); Say(Def.sounds.idleLines); }

            switch (Current)
            {
                case State.Spawning: TickSpawning(); break;
                case State.Idle: TickIdle(); break;
                case State.Wander: TickWander(); break;
                case State.Investigate: TickInvestigate(); break;
                case State.Triggered: TickTriggered(); break;
                case State.Chase: TickChase(); break;
                case State.Attack: TickAttack(); break;
                case State.Leaving: TickLeaving(); break;
            }

            // leave when it has been around long enough and isn't busy with you
            if (Def.lifetime > 0 && _age > Def.lifetime && !IsChasing && Current != State.Leaving) Enter(State.Leaving);
        }

        private void TickSpawning()
        {
            if (Def.behaviour != "stalker")
            {
                // e.g. getting up off the floor
                if (_stateTime >= Def.spawnClipTime) Enter(State.Wander);
                return;
            }
            // rise out of the floor
            float ground = GroundAt(transform.position);
            float t = Mathf.Clamp01(_stateTime / 2.5f);
            transform.position = new Vector3(transform.position.x, ground + Mathf.Lerp(-Def.height, 0f, Mathf.SmoothStep(0, 1, t)), transform.position.z);
            FacePlayer();
            if (t >= 1f) Enter(State.Chase);
        }

        private void TickIdle()
        {
            switch (Def.behaviour)
            {
                case "apparition": TickApparition(); return;
                case "trap": TickTrap(); return;
                case "noisemaker": TickNoisemaker(); return;
            }
            if (Def.behaviour == "shy")
            {
                // harmless until you look at it
                if (VisibleTo(_player, Def.sightRange)) Enter(State.Triggered);
                return;
            }
            if (CheckSenses()) return;
            if (_stateTime > _idleFor) Enter(State.Wander);
        }

        private void TickWander()
        {
            if (Def.behaviour == "noisemaker") { TickNoisemaker(); return; }
            if (CheckSenses()) return;
            if (_path == null && _stateTime < 0.1f) _investigate = RandomNearbyCell();
            if (MoveTo(_investigate, Def.walkSpeed, Phase)) Enter(State.Idle);
            if (_stateTime > 20f) Enter(State.Idle);
        }

        private void TickInvestigate()
        {
            if (Def.behaviour == "listener" && NoiseSystem.Heard(transform.position, Def.hearing, 0.3f, out var w)) _investigate = w;
            else if (CheckSenses()) return;
            if (MoveTo(_investigate, Def.runSpeed, Phase))
            {
                Play(Def.clips.idle, true, 1f);
                if (_stateTime > 1f) Enter(State.Idle);
            }
            if (_stateTime > 25f) Enter(State.Wander);
        }

        /// <summary>Hunter: sight. Listener: sound. Returns true if it changed state.</summary>
        private bool CheckSenses()
        {
            if (Def.behaviour == "listener")
            {
                if (NoiseSystem.Heard(transform.position, Def.hearing, 0.3f, out var where))
                {
                    _investigate = where;
                    if (Current != State.Investigate) { Say(Def.sounds.alert); Enter(State.Investigate); }
                    return Current != State.Investigate;
                }
                return false;
            }
            if (Def.behaviour == "hunter" && SeesPlayer()) { Enter(State.Chase); return true; }
            return false;
        }

        // ------------------------------------------------------------------ the stranger ones
        /// <summary>Stands at the edge of your vision. Look straight at it and it's gone - and so is some of your sanity.</summary>
        private void TickApparition()
        {
            FacePlayer();
            if (_hidden) return;
            Vector3 vp = _player.Cam.WorldToViewportPoint(transform.position + Vector3.up * Def.height * 0.6f);
            bool lookedAt = vp.z > 0 && Mathf.Abs(vp.x - 0.5f) < 0.18f && Mathf.Abs(vp.y - 0.5f) < 0.3f && VisibleTo(_player, 35f);
            if (lookedAt || _stateTime > 16f)
            {
                if (lookedAt) { _player.SanityHit(10f); _player.Shake(0.3f); Say(Def.sounds.alert); }
                Destroy(gameObject, 0.05f);
                SetHidden(true);
                return;
            }
            // when you're not looking, it moves closer
            if (!VisibleTo(_player, 40f) && _stateTime > 3f && Random.value < Time.deltaTime * 0.6f)
            {
                Vector3 back = _player.transform.position - _player.transform.forward * Random.Range(4f, 8f) + _player.transform.right * Random.Range(-5f, 5f);
                back.y = GroundAt(back);
                transform.position = back;
                Say(Def.sounds.idleLines);
            }
        }

        /// <summary>Waits under the floor. Get close and it comes up for you.</summary>
        private void TickTrap()
        {
            float d = FlatDist;
            if (_hidden)
            {
                if (d < Def.attackRange + 1.6f)
                {
                    SetHidden(false);
                    Play(Def.clips.special, false, 1.4f);
                    Say(Def.sounds.alert);
                    _stateTime = 0f;
                }
                return;
            }
            FacePlayer();
            if (_stateTime > 0.9f && !_killed)
            {
                if (d < Def.attackRange) { Enter(State.Attack); return; }
                if (_stateTime > 6f) { Destroy(gameObject); }
            }
        }

        /// <summary>Rolls up to you and makes an awful racket - harmless, but everything that hunts by sound hears it.</summary>
        private void TickNoisemaker()
        {
            float d = FlatDist;
            if (d < 4.5f)
            {
                Say(Def.sounds.alert);
                NoiseSystem.Emit(transform.position, 38f);
                _player.SanityHit(12f);
                _player.Shake(0.4f);
                Enter(State.Leaving);
                return;
            }
            Play(Def.clips.walk, true, Def.walkAnimSpeed);
            MoveTo(_player.transform.position, Def.walkSpeed, Phase);
        }

        private void TickTriggered()
        {
            FacePlayer();
            _player.Shake(0.6f);
            if (_stateTime > Def.triggerTime) Enter(State.Chase);
        }

        private void TickChase()
        {
            bool sees = Def.behaviour == "shy" || Def.behaviour == "stalker" || SeesPlayer();
            if (sees) { _sinceSeen = 0f; _investigate = _player.transform.position; }
            else _sinceSeen += Time.deltaTime;
            float speed = Def.behaviour == "stalker" ? Def.walkSpeed : Def.runSpeed;
            MoveTo(_investigate, speed, Phase);
            if (Def.behaviour == "hunter" && _sinceSeen > Def.loseInterestTime) Enter(State.Wander);
        }

        private void TickAttack()
        {
            FacePlayer();
            if (!_killed && _stateTime >= Def.killDelay)
            {
                _killed = true;
                if (!Def.harmless && FlatDist < Def.attackRange + 1.2f) _player.Kill(transform, Def.name);
                else Enter(State.Chase);   // you slipped away mid-swing
            }
        }

        private void TickLeaving()
        {
            // sink away (stalker) or just vanish when out of sight
            if (Def.behaviour == "stalker")
            {
                float t = Mathf.Clamp01(_stateTime / 2.5f);
                transform.position = new Vector3(transform.position.x, GroundAt(transform.position) + Mathf.Lerp(0f, -Def.height - 0.5f, t), transform.position.z);
                if (t >= 1f) Destroy(gameObject);
            }
            else
            {
                if (Def.behaviour == "noisemaker")
                {
                    Play(Def.clips.walk, true, Def.walkAnimSpeed);
                    Vector3 away = transform.position + (transform.position - _player.transform.position).normalized * 5f;
                    MoveTo(away, Def.walkSpeed * 1.5f, true);
                }
                if (!VisibleTo(_player) || _stateTime > 15f) Destroy(gameObject);
            }
        }
    }
}
