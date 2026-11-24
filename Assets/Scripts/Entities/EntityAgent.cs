using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// One SCP anomaly in the Backrooms. Supports 10 distinct horror behaviours (hunter, listener, shy, stalker, weeping,
    /// doorman, umbra, mimic, ambusher, cognito), real-time microphone voice baiting (stealsVoices), light interference,
    /// flashlight recoil, multi-player Co-Op targeting, and Client Puppet replication!
    /// </summary>
    public class EntityAgent : MonoBehaviour
    {
        public enum State : byte { Loading = 0, Spawning = 1, Idle = 2, Wander = 3, Investigate = 4, Chase = 5, Triggered = 6, Attack = 7, Leaving = 8, Frozen = 9 }

        public const byte CueAlert = 1;
        public const byte CueAttack = 2;
        public const byte CueIdle = 3;
        public const byte CueStolenVoice = 4;

        private static int _nextNetId = 1;

        public int NetId { get; private set; }
        public bool IsClientPuppet { get; private set; }
        public EntityDef Def { get; private set; }
        public State Current { get; private set; } = State.Loading;
        public bool IsChasing => Current == State.Chase || Current == State.Triggered || Current == State.Attack;
        public float CurrentMotionSpeed { get; private set; }

        private MazeField _field;
        private PlayerController _player;
        private Animation _anim;
        private ProceduralScpRig _procRig;
        private bool _visualsReady;
        private AudioSource _voice, _loop;
        private List<Vector2Int> _path;
        private int _pathIdx;
        private Vector2Int _pathGoal;
        private float _repath, _stateTime, _age, _sinceSeen, _nextLine, _nextBait, _idleFor;
        private Vector3 _investigate;
        private string _playing;
        private bool _killed;

        // Multi-player target tracking
        private readonly List<PlayerTarget> _targets = new List<PlayerTarget>(8);
        private PlayerTarget _focusTarget;
        private bool _hasFocus;

        // Client puppet interpolation
        private Vector3 _puppetPos;
        private float _puppetYaw;

        public static EntityAgent Create(EntityDef def, MazeField field, PlayerController player, Vector3 pos, float yaw, int forcedNetId = 0, bool clientPuppet = false)
        {
            var go = new GameObject(def.name);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var a = go.AddComponent<EntityAgent>();
            a.NetId = forcedNetId != 0 ? forcedNetId : _nextNetId++;
            a.IsClientPuppet = clientPuppet;
            a.Def = def;
            a._field = field;
            a._player = player;
            a._puppetPos = pos;
            a._puppetYaw = yaw;

            a._voice = go.AddComponent<AudioSource>();
            a._voice.spatialBlend = 1f;
            a._voice.minDistance = 3.2f;
            a._voice.maxDistance = 46f;
            a._voice.rolloffMode = AudioRolloffMode.Logarithmic;
            a._voice.volume = def.sounds.volume;

            a._loop = go.AddComponent<AudioSource>();
            a._loop.spatialBlend = 1f;
            a._loop.minDistance = 2f;
            a._loop.maxDistance = 26f;
            a._loop.loop = true;
            a._loop.rolloffMode = AudioRolloffMode.Linear;
            a._loop.volume = def.sounds.volume * 0.78f;

            a.LoadVisuals();
            return a;
        }

        private async void LoadVisuals()
        {
            var (anim, rig) = await EntityLibrary.Instance.SpawnVisuals(Def, transform);
            if (this == null) return;
            _anim = anim;
            _procRig = rig;
            _visualsReady = true;

            var amb = Def.RandomSound(Def.sounds.ambient);
            if (amb == null && !string.IsNullOrEmpty(Def.audioProfile))
                amb = ProceduralScpAudio.GetCue(Def.audioProfile, ProceduralScpAudio.CueKind.Ambient);
            if (amb != null) { _loop.clip = amb; _loop.Play(); }

            _nextLine = Random.Range(7f, 17f);
            _nextBait = Random.Range(5f, 11f);

            if (IsClientPuppet)
            {
                Current = State.Wander;
                ApplyStateVisuals(Current);
                return;
            }

            State initial = Def.behaviour == "stalker" ? State.Spawning
                          : (Def.behaviour == "shy" || Def.behaviour == "doorman" || Def.behaviour == "ambusher") ? State.Idle
                          : State.Wander;
            Enter(initial);
        }

        // ------------------------------------------------------------------ Client Puppet Sync
        public void ApplyPuppetSnapshot(Vector3 pos, float yaw, State st, float motionSpeed)
        {
            _puppetPos = pos;
            _puppetYaw = yaw;
            CurrentMotionSpeed = motionSpeed;
            if (Current != st && _visualsReady)
            {
                Current = st;
                ApplyStateVisuals(st);
            }
        }

        public void PlayNetworkCue(byte cueKind, int stolenClipId)
        {
            if (cueKind == CueStolenVoice)
            {
                var phrase = VoiceMimicBank.GetById(stolenClipId) ?? VoiceMimicBank.PickBaitPhrase(BackroomsNet.Instance != null ? BackroomsNet.Instance.MySlot : 0);
                var bait = VoiceMimicBank.GetOrBuildEerieClip(phrase);
                if (bait != null)
                {
                    _voice.pitch = Random.Range(0.93f, 1.04f);
                    _voice.PlayOneShot(bait, Mathf.Min(1.25f, Def.sounds.volume * 1.15f));
                }
                return;
            }

            _voice.pitch = Random.Range(0.95f, 1.05f);
            if (cueKind == CueAlert) PlaySoundOrSynth(Def.sounds.alert, ProceduralScpAudio.CueKind.Alert);
            else if (cueKind == CueAttack) PlaySoundOrSynth(Def.sounds.attack, ProceduralScpAudio.CueKind.Attack);
            else if (cueKind == CueIdle) PlaySoundOrSynth(Def.sounds.idleLines, ProceduralScpAudio.CueKind.Idle);
        }

        // ------------------------------------------------------------------ State & Animation
        private void Enter(State s)
        {
            Current = s;
            _stateTime = 0f;
            _path = null;
            if (s == State.Attack) _killed = false;
            ApplyStateVisuals(s);

            if (!IsClientPuppet)
            {
                if (s == State.Triggered || (s == State.Chase && Def.behaviour != "shy" && Def.behaviour != "weeping"))
                    EmitCue(CueAlert);
                else if (s == State.Attack)
                    EmitCue(CueAttack);
                if (s == State.Idle) _idleFor = Random.Range(2f, 5.5f);
                if (s == State.Chase) _sinceSeen = 0f;
            }
        }

        private void ApplyStateVisuals(State s)
        {
            switch (s)
            {
                case State.Spawning:
                    PlayLegacy(Def.clips.special, false, 1f);
                    _procRig?.SetMotion("idle", 0.5f);
                    break;
                case State.Frozen:
                    if (_anim != null) foreach (AnimationState st in _anim) st.speed = 0f;
                    _procRig?.SetMotion("frozen", 0f);
                    break;
                case State.Idle:
                    PlayLegacy(Def.clips.idle, true, 1f);
                    _procRig?.SetMotion("idle", 0f);
                    break;
                case State.Wander:
                    PlayLegacy(Def.clips.walk, true, Def.walkAnimSpeed);
                    _procRig?.SetMotion("walk", Def.walkSpeed);
                    break;
                case State.Investigate:
                    PlayLegacy(Def.clips.run, true, Def.runAnimSpeed);
                    _procRig?.SetMotion("run", Def.runSpeed);
                    break;
                case State.Triggered:
                    PlayLegacy(Def.clips.special, false, 1f);
                    _procRig?.SetMotion("triggered", 1f);
                    break;
                case State.Chase:
                    bool fast = Def.behaviour != "stalker";
                    PlayLegacy(fast ? Def.clips.run : Def.clips.walk, true, fast ? Def.runAnimSpeed : Def.walkAnimSpeed);
                    _procRig?.SetMotion(fast ? "run" : "walk", fast ? Def.runSpeed : Def.walkSpeed);
                    break;
                case State.Attack:
                    PlayLegacy(Def.clips.attack, false, 1f);
                    _procRig?.SetMotion("attack", Def.runSpeed);
                    break;
                case State.Leaving:
                    PlayLegacy(Def.clips.idle, true, 1f);
                    _procRig?.SetMotion("idle", 0f);
                    break;
            }
        }

        private void PlayLegacy(string clip, bool loop, float speed)
        {
            if (_anim == null || string.IsNullOrEmpty(clip) || _anim[clip] == null) return;
            var st = _anim[clip];
            st.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            st.speed = speed;
            if (_playing == clip && loop) return;
            if (!loop) st.time = 0f;
            _anim.CrossFade(clip, 0.2f);
            _playing = clip;
        }

        private void EmitCue(byte cueKind, int stolenClipId = 0)
        {
            PlayNetworkCue(cueKind, stolenClipId);
            BackroomsNet.Instance?.BroadcastEntityCue(NetId, cueKind, stolenClipId);
        }

        private void PlaySoundOrSynth(string[] files, ProceduralScpAudio.CueKind fallback)
        {
            var c = Def.RandomSound(files);
            if (c == null && !string.IsNullOrEmpty(Def.audioProfile))
                c = ProceduralScpAudio.GetCue(Def.audioProfile, fallback);
            if (c != null) _voice.PlayOneShot(c, Def.sounds.volume);
        }

        // ------------------------------------------------------------------ Senses & Multi-Player Visibility
        private Vector3 Eye => transform.position + Vector3.up * Def.height * 0.85f;
        private Vector3 Chest => transform.position + Vector3.up * Def.height * 0.72f;

        private static bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.02f) return true;
            foreach (var h in Physics.RaycastAll(a, d / len, len, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<PlayerController>() != null) continue;
                if (h.collider.GetComponentInParent<RemotePlayerAvatar>() != null) continue;
                return false;
            }
            return true;
        }

        public bool VisibleTo(PlayerController p, float maxDist = 40f)
        {
            if (p == null || p.Cam == null || !_visualsReady) return false;
            Vector3 vp = p.Cam.WorldToViewportPoint(Chest);
            if (vp.z <= 0 || vp.z > maxDist || vp.x < 0.04f || vp.x > 0.96f || vp.y < 0.04f || vp.y > 0.96f) return false;
            return Clear(p.Cam.transform.position, Chest);
        }

        private bool IsWatchedByAnyTarget(float maxDist)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                // Can't observe a weeping statue in pitch blackness without a light!
                if (!t.flashlightOn && t.exposure < 0.12f) continue;
                Vector3 toEntity = Chest - t.head;
                float dist = toEntity.magnitude;
                if (dist > maxDist || dist < 0.2f) continue;
                if (Vector3.Angle(t.lookDir, toEntity / dist) <= 52f && Clear(t.head, Chest))
                    return true;
            }
            return false;
        }

        private bool IsHitByAnyFlashlight()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                if (!t.flashlightOn) continue;
                Vector3 toEntity = Chest - t.head;
                float dist = toEntity.magnitude;
                if (dist > t.flashlightRange * 0.95f || dist < 0.1f) continue;
                if (Vector3.Angle(t.lookDir, toEntity / dist) <= t.flashlightAngle * 0.45f && Clear(t.head, Chest))
                    return true;
            }
            return false;
        }

        private bool FindSeenTarget(out PlayerTarget seen)
        {
            seen = default;
            float bestSq = float.MaxValue;
            bool found = false;

            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                float range = Def.sightRange;
                if (Def.prefersDark) range *= (t.exposure < 0.3f && !t.flashlightOn) ? 1.6f : 0.65f;
                if (t.flashlightOn) range *= 1.25f;
                if (t.crouching) range *= 0.72f;

                Vector3 to = t.head - Eye;
                float sq = to.sqrMagnitude;
                if (sq > range * range || sq >= bestSq) continue;
                Vector3 flat = new Vector3(to.x, 0, to.z);
                if (Vector3.Angle(transform.forward, flat) > Def.sightAngle * 0.5f && sq > 6.25f) continue;
                if (!Clear(Eye, t.head)) continue;

                bestSq = sq;
                seen = t;
                found = true;
            }
            return found;
        }

        private bool NearestTarget(out PlayerTarget nearest, out float flatDist)
        {
            nearest = default;
            flatDist = float.MaxValue;
            if (_targets.Count == 0) return false;
            Vector2 me = new Vector2(transform.position.x, transform.position.z);
            for (int i = 0; i < _targets.Count; i++)
            {
                float d = Vector2.Distance(me, new Vector2(_targets[i].pos.x, _targets[i].pos.z));
                if (d < flatDist)
                {
                    flatDist = d;
                    nearest = _targets[i];
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ Light Interference & Voice Baiting
        private void TickLightInterference()
        {
            if (!Def.flickersLights || GameBoot.Instance?.Chunks == null) return;
            var lights = GameBoot.Instance.Chunks.ActiveLights;
            Vector3 p = transform.position + Vector3.up * 1.5f;
            float rSq = IsChasing ? 14f * 14f : 10f * 10f;
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (l == null || !l.enabled) continue;
                float sq = (l.transform.position - p).sqrMagnitude;
                if (sq > rSq) continue;
                float k = 1f - Mathf.Sqrt(sq / rSq);
                float wave = Mathf.PerlinNoise(Time.time * 18f, i * 3.7f);
                if (wave < 0.45f * k)
                    l.intensity = Mathf.Min(l.intensity, 0.08f);
            }
        }

        private void TryVoiceBait(float nearestDist, int nearestSlot)
        {
            if (!Def.stealsVoices || VoiceMimicBank.Phrases.Count == 0) return;
            if (IsChasing || Current == State.Spawning || Current == State.Leaving) return;
            // Bait works best when lurking 8m..42m down an unseen corridor!
            if (nearestDist < 7.5f || nearestDist > 42f) return;
            if (_player != null && VisibleTo(_player, 30f)) return;

            var phrase = VoiceMimicBank.PickBaitPhrase(nearestSlot);
            if (phrase != null)
            {
                EmitCue(CueStolenVoice, phrase.clipId);
                // Slowly creep closer to the intersection after calling out in their friend's voice
                if (Current == State.Idle) Enter(State.Wander);
            }
        }

        // ------------------------------------------------------------------ Movement
        private bool MoveTo(Vector3 target, float speed, bool phase)
        {
            CurrentMotionSpeed = speed;
            if (speed <= 0.01f) return false;

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
                    _repath = 0.55f;
                    _pathGoal = goal;
                    _path = Pathfinder.Find(_field, here, goal);
                    _pathIdx = 0;
                }
                if (_path == null || _path.Count == 0) return true;
                while (_pathIdx < _path.Count - 1 && _path[_pathIdx] == here) _pathIdx++;
                Vector3 wp = _pathIdx >= _path.Count - 1 ? target : _field.CellCenter(_path[_pathIdx]);
                if (here == goal) wp = target;
                step = wp - pos;
            }
            step.y = 0;
            float dist = step.magnitude;
            if (dist < 0.05f) return new Vector2(target.x - pos.x, target.z - pos.z).magnitude < 0.6f;

            Vector3 dir = step / dist;
            var want = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, Def.turnSpeed * Time.deltaTime);
            float move = Mathf.Min(dist, speed * Time.deltaTime);
            transform.position = new Vector3(pos.x + dir.x * move, 0f, pos.z + dir.z * move);
            _procRig?.SetMotion(speed > Def.walkSpeed + 1.2f ? "run" : "walk", speed);
            return new Vector2(target.x - transform.position.x, target.z - transform.position.z).magnitude < 0.6f;
        }

        private void Face(Vector3 worldPos)
        {
            Vector3 d = worldPos - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), Def.turnSpeed * 2f * Time.deltaTime);
        }

        private Vector3 RandomNearbyCell()
        {
            var cells = Pathfinder.Reachable(_field, _field.CellAt(transform.position), 6, 400);
            var c = cells[Random.Range(0, cells.Count)].cell;
            return _field.CellCenter(c);
        }

        // ------------------------------------------------------------------ Brain Update
        private void Update()
        {
            if (!_visualsReady) return;
            float dt = Time.deltaTime;

            TickLightInterference();

            // Client Puppet Mode: smooth transform + rig animation
            if (IsClientPuppet)
            {
                float d = Vector3.Distance(transform.position, _puppetPos);
                transform.position = d > 12f ? _puppetPos : Vector3.Lerp(transform.position, _puppetPos, Mathf.Clamp01(dt * 14f));
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, _puppetYaw, 0), Mathf.Clamp01(dt * 14f));
                return;
            }

            if (Current == State.Loading || _player == null || _field == null) return;
            _stateTime += dt;
            _age += dt;

            BackroomsNet.GatherTargets(_player, _targets);
            bool anyTargets = NearestTarget(out var nearest, out float nearestDist);
            if (!_hasFocus && anyTargets) { _focusTarget = nearest; _hasFocus = true; }
            else if (anyTargets)
            {
                // Refresh focus target's latest coordinates if still standing, else switch to nearest
                bool stillValid = false;
                for (int i = 0; i < _targets.Count; i++)
                    if (_targets[i].slot == _focusTarget.slot) { _focusTarget = _targets[i]; stillValid = true; break; }
                if (!stillValid) _focusTarget = nearest;
            }

            // Record in SCP Field Dossier once close or seen!
            if ((nearestDist < 16f || VisibleTo(_player, 28f)) && EntityDirector.Instance != null)
                EntityDirector.Instance.MarkEncountered(Def.id);

            // Weeping Statue mechanic: freeze stone-still whenever ANY player observes it!
            if (Def.freezesWhenWatched && Current != State.Spawning && Current != State.Leaving)
            {
                bool watched = IsWatchedByAnyTarget(36f);
                if (watched)
                {
                    if (Current != State.Frozen)
                    {
                        Enter(State.Frozen);
                        PlaySoundOrSynth(null, ProceduralScpAudio.CueKind.Idle);
                    }
                    CurrentMotionSpeed = 0f;
                    return;
                }
                else if (Current == State.Frozen)
                {
                    Enter(State.Chase);
                }
            }

            // Melee Attack Trigger against ANY standing player in range
            if (anyTargets && Current != State.Attack && Current != State.Frozen && Current != State.Spawning && Current != State.Leaving
                && !(Def.behaviour == "shy" && Current == State.Idle))
            {
                if (nearestDist < Def.attackRange && Mathf.Abs(nearest.pos.y - transform.position.y) < 1.6f)
                {
                    _focusTarget = nearest;
                    Enter(State.Attack);
                }
            }

            // Stolen Voice Mimicry Baiting!
            if (Def.stealsVoices && anyTargets)
            {
                _nextBait -= dt;
                if (_nextBait <= 0f)
                {
                    _nextBait = Random.Range(7.5f, 15f);
                    TryVoiceBait(nearestDist, nearest.slot);
                }
            }

            // Idle horror chatter / synthesized clicks & wheezes
            _nextLine -= dt;
            if (_nextLine <= 0f && Current != State.Attack && Current != State.Frozen)
            {
                _nextLine = Random.Range(10f, 22f);
                EmitCue(CueIdle);
            }

            // Extra dread aura for doorman & cognito anomalies
            if ((Def.behaviour == "doorman" || Def.behaviour == "cognito") && _player != null && !_player.Dead && VisibleTo(_player, 18f))
            {
                _player.DrainSanityExtra(dt * 4.5f);
                if (Def.behaviour == "doorman" && Vector3.Distance(_player.transform.position, transform.position) < 9f)
                    _player.Shake(0.18f);
            }

            switch (Current)
            {
                case State.Spawning: TickSpawning(); break;
                case State.Idle: TickIdle(anyTargets, nearest, nearestDist); break;
                case State.Wander: TickWander(anyTargets, nearest, nearestDist); break;
                case State.Investigate: TickInvestigate(anyTargets, nearest, nearestDist); break;
                case State.Triggered: TickTriggered(); break;
                case State.Chase: TickChase(anyTargets, nearest); break;
                case State.Attack: TickAttack(); break;
                case State.Leaving: TickLeaving(); break;
            }

            if (Def.lifetime > 0 && _age > Def.lifetime && !IsChasing && Current != State.Leaving && Current != State.Frozen)
                Enter(State.Leaving);
        }

        private void TickSpawning()
        {
            float t = Mathf.Clamp01(_stateTime / 2.3f);
            transform.position = new Vector3(transform.position.x, Mathf.Lerp(-Def.height, 0f, Mathf.SmoothStep(0, 1, t)), transform.position.z);
            if (_hasFocus) Face(_focusTarget.pos);
            if (t >= 1f) Enter(State.Chase);
        }

        private void TickIdle(bool anyTargets, PlayerTarget nearest, float nearestDist)
        {
            CurrentMotionSpeed = 0f;
            if (!anyTargets) return;

            if (Def.behaviour == "shy")
            {
                if (IsWatchedByAnyTarget(Def.sightRange)) { _focusTarget = nearest; Enter(State.Triggered); }
                return;
            }
            if (Def.behaviour == "doorman")
            {
                // Blocks doorway wheezing; lunges if someone pushes past (< 4.2m)
                Face(nearest.pos);
                if (nearestDist < 4.2f && Clear(Eye, nearest.head)) { _focusTarget = nearest; Enter(State.Chase); return; }
                if (_stateTime > 16f) Enter(State.Wander);
                return;
            }
            if (Def.behaviour == "ambusher")
            {
                if (nearestDist < 10.5f && (Clear(Eye, nearest.head) || NoiseSystem.Heard(transform.position, Def.hearing, 0.35f, out _)))
                {
                    _focusTarget = nearest;
                    Enter(State.Chase);
                }
                else if (_stateTime > 18f) Enter(State.Wander);
                return;
            }

            if (CheckSenses(nearest, nearestDist)) return;
            if (_stateTime > _idleFor) Enter(State.Wander);
        }

        private void TickWander(bool anyTargets, PlayerTarget nearest, float nearestDist)
        {
            if (anyTargets && CheckSenses(nearest, nearestDist)) return;
            if (Def.behaviour == "weeping" && anyTargets)
            {
                // Unobserved weeping statues immediately advance toward the nearest victim!
                _focusTarget = nearest;
                Enter(State.Chase);
                return;
            }
            if (_path == null && _stateTime < 0.1f) _investigate = RandomNearbyCell();
            float speed = Def.fearsFlashlight && IsHitByAnyFlashlight() ? Def.walkSpeed * 0.2f : Def.walkSpeed;
            if (MoveTo(_investigate, speed, Def.phaseThroughWalls)) Enter(State.Idle);
            if (_stateTime > 20f) Enter(State.Idle);
        }

        private void TickInvestigate(bool anyTargets, PlayerTarget nearest, float nearestDist)
        {
            if ((Def.behaviour == "listener" || Def.stealsVoices) && NoiseSystem.Heard(transform.position, Def.hearing, 0.35f, out var w))
                _investigate = w;
            else if (anyTargets && CheckSenses(nearest, nearestDist))
                return;

            float speed = Def.fearsFlashlight && IsHitByAnyFlashlight() ? Def.walkSpeed * 0.25f : Def.runSpeed;
            if (MoveTo(_investigate, speed, Def.phaseThroughWalls))
            {
                _procRig?.SetMotion("idle", 0f);
                PlayLegacy(Def.clips.idle, true, 1f);
                if (_stateTime > 1.2f) Enter(State.Idle);
            }
            if (_stateTime > 24f) Enter(State.Wander);
        }

        private bool CheckSenses(PlayerTarget nearest, float nearestDist)
        {
            // Every voice-stealing / listener anomaly reacts to footsteps AND live microphone speech!
            if (Def.behaviour == "listener" || Def.behaviour == "mimic")
            {
                if (NoiseSystem.Heard(transform.position, Def.hearing, 0.35f, out var where))
                {
                    _investigate = where;
                    if (Def.behaviour == "mimic" && nearestDist < 12f && FindSeenTarget(out var seenMimic))
                    {
                        _focusTarget = seenMimic;
                        Enter(State.Chase);
                        return true;
                    }
                    if (Current != State.Investigate) { EmitCue(CueAlert); Enter(State.Investigate); }
                    return true;
                }
                if (Def.behaviour == "mimic" && FindSeenTarget(out var seenClose) && nearestDist < 10f)
                {
                    _focusTarget = seenClose;
                    Enter(State.Chase);
                    return true;
                }
                return false;
            }

            if (Def.behaviour == "cognito")
            {
                if (FindSeenTarget(out var seenCog) && (seenCog.sanity01 < 0.58f || nearestDist < 8.5f))
                {
                    _focusTarget = seenCog;
                    Enter(State.Chase);
                    return true;
                }
                return false;
            }

            if (FindSeenTarget(out var seen))
            {
                _focusTarget = seen;
                Enter(State.Chase);
                return true;
            }

            // Secondary earshot check for hunters/umbras/ambushers if someone yells right next to them
            if (NoiseSystem.Heard(transform.position, Def.hearing * 0.7f, 0.25f, out var loudPos))
            {
                _investigate = loudPos;
                Enter(State.Investigate);
                return true;
            }
            return false;
        }

        private void TickTriggered()
        {
            CurrentMotionSpeed = 0f;
            if (_hasFocus) Face(_focusTarget.pos);
            if (_player != null && Vector3.Distance(_player.transform.position, transform.position) < 28f)
                _player.Shake(0.6f);
            if (_stateTime > Def.triggerTime) Enter(State.Chase);
        }

        private void TickChase(bool anyTargets, PlayerTarget nearest)
        {
            if (!anyTargets) { Enter(State.Wander); return; }

            bool alwaysKnows = Def.behaviour == "shy" || Def.behaviour == "stalker" || Def.behaviour == "weeping";
            if (alwaysKnows)
            {
                _focusTarget = nearest;
                _sinceSeen = 0f;
                _investigate = _focusTarget.pos;
            }
            else if (FindSeenTarget(out var seen))
            {
                _focusTarget = seen;
                _sinceSeen = 0f;
                _investigate = seen.pos;
            }
            else if (NoiseSystem.Heard(transform.position, Def.hearing, 0.3f, out var heardPos))
            {
                _sinceSeen = 0f;
                _investigate = heardPos;
            }
            else
            {
                _sinceSeen += Time.deltaTime;
            }

            float speed = Def.behaviour == "stalker" ? Def.walkSpeed : Def.runSpeed;
            // Umbra / light-fearing SCPs recoil & slow dramatically when pinned in a player's flashlight beam!
            if (Def.fearsFlashlight && IsHitByAnyFlashlight())
                speed *= 0.18f;

            MoveTo(_investigate, speed, Def.phaseThroughWalls);

            if (!alwaysKnows && _sinceSeen > Def.loseInterestTime)
                Enter(State.Wander);
        }

        private void TickAttack()
        {
            CurrentMotionSpeed = 0f;
            if (_hasFocus) Face(_focusTarget.pos);
            if (!_killed && _stateTime >= Def.killDelay)
            {
                _killed = true;
                float dist = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(_focusTarget.pos.x, _focusTarget.pos.z));

                if (dist < Def.attackRange + 1.25f)
                {
                    if (BackroomsNet.IsOnline)
                    {
                        BackroomsNet.Instance.OnMonsterCaughtPlayer(_focusTarget.slot, transform, $"{Def.name} ({Def.nickname})");
                        Enter(State.Wander);
                    }
                    else
                    {
                        _player.Kill(transform, string.IsNullOrEmpty(Def.nickname) ? Def.name : $"{Def.name} - {Def.nickname}");
                    }
                }
                else
                {
                    Enter(State.Chase);
                }
            }
        }

        private void TickLeaving()
        {
            if (Def.behaviour == "stalker")
            {
                float t = Mathf.Clamp01(_stateTime / 2.5f);
                transform.position = new Vector3(transform.position.x, Mathf.Lerp(0f, -Def.height - 0.5f, t), transform.position.z);
                if (t >= 1f) Destroy(gameObject);
            }
            else if (!IsWatchedByAnyTarget(45f) || _stateTime > 15f)
            {
                Destroy(gameObject);
            }
        }
    }
}
