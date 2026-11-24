using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Backrooms
{
    /// <summary>
    /// First-person explorer body: WASD, Shift sprint (stamina), Ctrl/C crouch (silent), Space jump, F flashlight (battery),
    /// V mute mic, E interact/revive, mouse look. Supports Co-Op Downed/Revive state and extra dread sanity drain.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public Light Flashlight { get; private set; }
        public float Stamina { get; private set; }
        public float Battery { get; private set; }
        public float Sanity { get; private set; }
        public bool Crouching { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Downed { get; private set; }
        public bool Dead { get; private set; }
        public float DistanceWalked { get; private set; }
        public string InteractPrompt { get; private set; }
        public bool ControlsEnabled = true;
        public float Exposure { get; private set; }
        public float Yaw => _yaw;
        public float Pitch => _pitch;

        private PlayerTuning P => GameConfig.Current.player;
        private CharacterController _cc;
        private Transform _head;
        private float _yaw, _pitch, _vy, _bob, _staminaWait, _stepAccum, _height;
        private Vector3 _vel;
        private AudioSource _steps, _heart;
        private bool _grounded, _wasGrounded;
        private Transform _deathLook;
        private float _shake, _lightCheck, _measured = 1f, _listenCooldown;
        public float ListenCooldown01 => Mathf.Clamp01(_listenCooldown / Mathf.Max(0.01f, P.listenCooldown));

        public static PlayerController Spawn(Vector3 pos, float yaw)
        {
            var go = new GameObject("Player");
            go.tag = "Player";
            go.transform.position = pos;
            var pc = go.AddComponent<PlayerController>();
            pc._yaw = yaw;
            return pc;
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _height = P.standHeight;
            _cc.height = _height;
            _cc.radius = 0.32f;
            _cc.center = new Vector3(0, _height / 2f, 0);
            _cc.stepOffset = 0.35f;
            _cc.slopeLimit = 50f;

            _head = new GameObject("Head").transform;
            _head.SetParent(transform, false);
            _head.localPosition = new Vector3(0, _height - P.eyeOffset, 0);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(_head, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.fieldOfView = PlayerPrefs.GetFloat("fov", P.fov);
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 120f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.AddComponent<AudioListener>();

            var fl = new GameObject("Flashlight");
            fl.transform.SetParent(camGo.transform, false);
            fl.transform.localPosition = new Vector3(0.18f, -0.15f, 0.1f);
            Flashlight = fl.AddComponent<Light>();
            Flashlight.type = LightType.Spot;
            Flashlight.spotAngle = P.flashlightAngle;
            Flashlight.innerSpotAngle = P.flashlightAngle * 0.45f;
            Flashlight.range = P.flashlightRange;
            Flashlight.intensity = 6f;
            Flashlight.color = new Color(1f, 0.96f, 0.88f);
            Flashlight.shadows = LightShadows.Soft;
            Flashlight.enabled = false;

            _steps = gameObject.AddComponent<AudioSource>();
            _steps.spatialBlend = 0f;
            _steps.volume = 0.45f;
            _heart = gameObject.AddComponent<AudioSource>();
            _heart.clip = ProcAudio.Heartbeat();
            _heart.loop = true;
            _heart.volume = 0f;
            _heart.Play();

            Stamina = P.staminaMax;
            Battery = P.flashlightBatteryMax;
            Sanity = P.sanityMax;
        }

        public void SetSensitivity(float s) => PlayerPrefs.SetFloat("sens", s);

        private void Update()
        {
            if (Dead) { DeathCam(); return; }
            float dt = Time.deltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool ctl = ControlsEnabled && kb != null;

            // ---------------- look
            if (ctl && mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue() * PlayerPrefs.GetFloat("sens", P.mouseSensitivity);
                _yaw += d.x;
                _pitch = Mathf.Clamp(_pitch - d.y, -88f, 88f);
            }
            transform.rotation = Quaternion.Euler(0, _yaw, 0);

            // ---------------- crouch / downed posture
            bool wantCrouch = Downed || (ctl && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed));
            if (!wantCrouch && Crouching)
            {
                if (!Physics.SphereCast(transform.position + Vector3.up * (_cc.radius + 0.05f), _cc.radius * 0.9f, Vector3.up, out _, P.standHeight - _cc.radius, ~0, QueryTriggerInteraction.Ignore))
                    Crouching = false;
            }
            else if (wantCrouch) Crouching = true;

            float targetH = Downed ? 0.68f : Crouching ? P.crouchHeight : P.standHeight;
            _height = Mathf.MoveTowards(_height, targetH, dt * 4f);
            _cc.height = _height;
            _cc.center = new Vector3(0, _height / 2f, 0);

            // ---------------- move
            Vector2 input = Vector2.zero;
            if (ctl)
            {
                if (kb.wKey.isPressed) input.y += 1;
                if (kb.sKey.isPressed) input.y -= 1;
                if (kb.dKey.isPressed) input.x += 1;
                if (kb.aKey.isPressed) input.x -= 1;
                input = Vector2.ClampMagnitude(input, 1f);
            }
            bool moving = input.sqrMagnitude > 0.01f;
            bool wantSprint = ctl && !Downed && kb.leftShiftKey.isPressed && moving && input.y > 0.1f && !Crouching;
            Sprinting = wantSprint && Stamina > 0.5f;
            if (Sprinting) { Stamina = Mathf.Max(0, Stamina - P.staminaDrain * dt); _staminaWait = P.staminaRegenDelay; }
            else
            {
                _staminaWait -= dt;
                if (_staminaWait <= 0) Stamina = Mathf.Min(P.staminaMax, Stamina + P.staminaRegen * dt * (Crouching ? 1.4f : 1f));
            }
            float speed = Downed ? 0.85f : Crouching ? P.crouchSpeed : Sprinting ? P.sprintSpeed : P.walkSpeed;
            Vector3 wish = transform.TransformDirection(new Vector3(input.x, 0, input.y)) * speed;
            float accel = _grounded ? P.acceleration : P.acceleration * P.airControl;
            _vel = Vector3.MoveTowards(_vel, wish, accel * speed * dt);

            _grounded = _cc.isGrounded;
            if (_grounded && _vy < 0) _vy = -2f;
            if (ctl && !Downed && _grounded && !Crouching && kb.spaceKey.wasPressedThisFrame)
                _vy = Mathf.Sqrt(P.jumpHeight * -2f * P.gravity);
            _vy += P.gravity * dt;
            Vector3 before = transform.position;
            _cc.Move((_vel + Vector3.up * _vy) * dt);
            Vector3 moved = transform.position - before; moved.y = 0;
            DistanceWalked += moved.magnitude;
            if (!_wasGrounded && _grounded && _vy < -6f) { NoiseSystem.Emit(transform.position, P.noiseJumpLand); PlayStep(1.2f); }
            _wasGrounded = _grounded;

            // ---------------- footsteps, head bob, noise
            float hspeed = moved.magnitude / Mathf.Max(dt, 1e-4f);
            if (_grounded && hspeed > 0.3f)
            {
                _bob += hspeed * dt * P.headBobSpeed;
                _stepAccum += moved.magnitude;
                float stride = Sprinting ? 2.2f : Crouching ? 1.1f : 1.6f;
                if (_stepAccum >= stride)
                {
                    _stepAccum = 0;
                    PlayStep(Crouching ? 0.22f : Sprinting ? 1f : 0.65f);
                    NoiseSystem.Emit(transform.position, Crouching ? P.noiseCrouch : Sprinting ? P.noiseSprint : P.noiseWalk);
                }
            }
            else _bob = Mathf.MoveTowards(_bob, Mathf.Round(_bob / Mathf.PI) * Mathf.PI, dt * 3f);

            float bobAmt = P.headBobAmount * (Sprinting ? 1.6f : Crouching ? 0.5f : 1f) * GraphicsOptions.HeadBob;
            Vector3 headPos = new Vector3(Mathf.Cos(_bob) * bobAmt * 0.5f, _height - P.eyeOffset + Mathf.Abs(Mathf.Sin(_bob)) * bobAmt, 0);
            _shake = Mathf.MoveTowards(_shake, 0f, dt);
            if (_shake > 0) headPos += Random.insideUnitSphere * _shake * 0.05f;
            _head.localPosition = headPos;
            float rollTilt = Downed ? 14f : Mathf.Sin(_bob * 0.5f) * bobAmt * 8f;
            _head.localRotation = Quaternion.Euler(_pitch, 0, rollTilt);

            // ---------------- flashlight
            if (ctl && kb.fKey.wasPressedThisFrame && Battery > 0) Flashlight.enabled = !Flashlight.enabled;
            if (Flashlight.enabled)
            {
                Battery = Mathf.Max(0, Battery - P.flashlightDrain * dt);
                float k = Battery / P.flashlightBatteryMax;
                Flashlight.intensity = Mathf.Lerp(1.5f, 6f, Mathf.Clamp01(k * 3f));
                if (k < 0.15f && Mathf.PerlinNoise(Time.time * 8f, 1f) < 0.25f) Flashlight.intensity *= 0.1f;
                if (Battery <= 0) Flashlight.enabled = false;
            }

            // ---------------- light exposure + sanity
            _lightCheck -= dt;
            if (_lightCheck <= 0f) { _lightCheck = 0.25f; _measured = MeasureLight(); }
            Exposure = Mathf.MoveTowards(Exposure, _measured, dt * 2f);
            float drain = Downed ? 6f : 0f;
            if (Exposure < 0.25f && !Flashlight.enabled) drain += P.sanityDrainDark;
            if (EntityDirector.Instance != null && EntityDirector.Instance.AnyVisibleTo(this)) drain += P.sanityDrainEntity;
            Sanity = drain > 0 ? Mathf.Max(0, Sanity - drain * dt) : Mathf.Min(P.sanityMax, Sanity + P.sanityRegen * dt);
            float fear = Downed ? 0.9f : (1f - Sanity / P.sanityMax);
            _heart.volume = Mathf.Lerp(_heart.volume, Mathf.Clamp01((fear - 0.35f) * 1.6f) * 0.85f, dt * 2f);
            _heart.pitch = 0.9f + fear * 0.6f;

            // ---------------- listen for the nearest drum piece
            _listenCooldown -= dt;
            if (ctl && kb.qKey.wasPressedThisFrame && _listenCooldown <= 0f)
            {
                _listenCooldown = P.listenCooldown;
                NoiseSystem.Emit(transform.position, P.noiseListen);   // standing still and listening isn't silent: you hum, you breathe
                GameBoot.Instance.Listen();
            }

            // ---------------- interact / revive
            InteractPrompt = Downed ? "DOWNED — CRAWL OR CALL FOR A TEAMMATE ON MIC!" : null;
            if (ctl && !Downed && Physics.Raycast(Cam.transform.position, Cam.transform.forward, out var hit, P.interactDistance + 0.35f, ~0, QueryTriggerInteraction.Collide))
            {
                var it = hit.collider.GetComponentInParent<IInteractable>();
                if (it != null && !string.IsNullOrEmpty(it.Prompt))
                {
                    InteractPrompt = it.Prompt;
                    if (kb.eKey.wasPressedThisFrame) it.Interact(this);
                }
            }
        }

        private float MeasureLight()
        {
            float best = 0f;
            Vector3 p = transform.position + Vector3.up;
            var lights = GameBoot.Instance?.Chunks?.ActiveLights;
            if (lights != null)
            {
                for (int i = 0; i < lights.Count; i++)
                {
                    var l = lights[i];
                    if (l == null || !l.enabled || l.intensity < 0.4f) continue;
                    float d = Vector3.Distance(l.transform.position, p);
                    if (d < l.range) best = Mathf.Max(best, 1f - d / l.range);
                }
            }
            return Mathf.Clamp01(best * 1.6f);
        }

        private void PlayStep(float vol)
        {
            _steps.pitch = Random.Range(0.85f, 1.15f);
            _steps.PlayOneShot(ProcAudio.Step(), vol);
        }

        public void AddBattery(float amount) => Battery = Mathf.Min(P.flashlightBatteryMax, Battery + amount);

        public void DrinkAlmondWater()
        {
            Sanity = Mathf.Min(P.sanityMax, Sanity + 45f);
            Stamina = P.staminaMax;
        }

        public void DrainSanityExtra(float amount) => Sanity = Mathf.Max(0f, Sanity - amount);

        public void AddYaw(float degrees) => _yaw += degrees;

        /// <summary>Something horrible happened nearby.</summary>
        public void SanityHit(float amount) => Sanity = Mathf.Max(0f, Sanity - amount);

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        public void Teleport(Vector3 pos, float yaw)
        {
            _cc.enabled = false;
            transform.position = pos;
            _cc.enabled = true;
            _yaw = yaw; _pitch = 0; _vel = Vector3.zero; _vy = 0;
        }

        public void ResetStats()
        {
            Stamina = P.staminaMax; Battery = P.flashlightBatteryMax; Sanity = P.sanityMax;
            Downed = false; Dead = false; Flashlight.enabled = false; DistanceWalked = 0; _listenCooldown = 0f;
        }

        public void SetDowned(bool downed, string attacker)
        {
            if (Dead) return;
            Downed = downed;
            if (downed)
            {
                Shake(0.9f);
                GameUI.Instance?.Toast($"DOWNED BY {attacker}! CALL YOUR SQUAD ON MIC!", 4.5f);
            }
            else
            {
                Sanity = Mathf.Max(Sanity, P.sanityMax * 0.5f);
                Stamina = Mathf.Max(Stamina, P.staminaMax * 0.6f);
                GameUI.Instance?.Toast("TEAMMATE REVIVED YOU — MOVE!", 3f);
            }
        }

        public void Kill(Transform killer, string killerName)
        {
            if (Dead) return;
            Downed = false;
            Dead = true;
            _deathLook = killer;
            _heart.volume = 0f;
            GameBoot.Instance.PlayerDied(killerName);
        }

        private void DeathCam()
        {
            if (_deathLook == null) return;
            Vector3 target = _deathLook.position + Vector3.up * 1.6f;
            var want = Quaternion.LookRotation(target - _head.position);
            _head.rotation = Quaternion.Slerp(_head.rotation, want, Time.deltaTime * 6f);
            _head.localPosition = Vector3.Lerp(_head.localPosition, new Vector3(0, 0.6f, 0), Time.deltaTime * 1.5f);
        }
    }

    /// <summary>Sounds players make (footsteps, landings, AND live microphone voice), hunted by listeners &amp; mimics.</summary>
    public static class NoiseSystem
    {
        public static Vector3 LastPos;
        public static float LastRadius;
        public static float LastTime = -999f;

        public static void Emit(Vector3 pos, float radius)
        {
            if (radius <= 0f) return;
            // Keep louder overlapping sounds from being overwritten by softer ones in the same instant
            if (Time.time - LastTime < 0.12f && radius < LastRadius) return;
            LastPos = pos; LastRadius = radius; LastTime = Time.time;
        }

        public static bool Heard(Vector3 listener, float hearing, float window, out Vector3 where)
        {
            where = LastPos;
            if (Time.time - LastTime > window) return false;
            return (listener - LastPos).sqrMagnitude <= (LastRadius * hearing) * (LastRadius * hearing);
        }
    }
}
