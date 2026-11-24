using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// 3D Co-Op Hazmat Explorer avatar representing a remote teammate in the Backrooms:
    /// articulated hazmat suit, live volumetric-feeling Spot flashlight, footsteps, 3D proximity voice stream,
    /// floating callsign, and [E] Revive interaction when downed by an SCP!
    /// </summary>
    public class RemotePlayerAvatar : MonoBehaviour, IInteractable
    {
        public int Slot { get; private set; }
        public string PlayerName { get; private set; } = "EXPLORER";
        public Vector3 TargetPos;
        public float TargetYaw;
        public float TargetPitch;
        public bool Crouching;
        public bool Sprinting;
        public bool FlashlightOn;
        public bool Speaking;
        public bool Downed;
        public bool Dead;

        public Transform Head { get; private set; }
        public Light Flashlight { get; private set; }
        public Vector3 EyePos => Head != null ? Head.position : transform.position + Vector3.up * 1.55f;
        public Vector3 LookForward => Head != null ? Head.forward : transform.forward;

        private Transform _bodyRoot, _legL, _legR, _armL, _armR, _namePivot;
        private TextMesh _nameMesh;
        private AudioSource _voiceSource, _stepSource;
        private AudioClip _streamClip;
        private const int VoiceRate = VoiceMimicBank.SampleRate;
        private const int RingSamples = VoiceRate * 3; // 3s ring buffer
        private readonly float[] _zeroBuf = new float[800];
        private int _writeSamplePos;
        private int _lastClearedSamplePos;
        private float _lastVoicePushTime = -10f;
        private float _walkCycle;
        private float _stepAccum;

        public string Prompt => Downed && !Dead ? $"[E] Revive {PlayerName}" : null;

        public static RemotePlayerAvatar Create(int slot, string name, Vector3 pos)
        {
            var go = new GameObject($"Explorer_{slot}_{name}");
            go.transform.position = pos;
            var av = go.AddComponent<RemotePlayerAvatar>();
            av.Slot = slot;
            av.SetName(name);
            av.TargetPos = pos;
            av.BuildVisuals(slot);
            av.SetupAudio();
            return av;
        }

        public void SetName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                PlayerName = name.Trim().Length > 16 ? name.Trim().Substring(0, 16) : name.Trim();
            if (_nameMesh != null) UpdateNametagText();
        }

        private void BuildVisuals(int slot)
        {
            Color[] suitTints =
            {
                new Color(0.84f, 0.72f, 0.16f), // Async yellow
                new Color(0.78f, 0.56f, 0.14f), // Amber hazmat
                new Color(0.68f, 0.74f, 0.24f), // Olive-yellow hazmat
                new Color(0.82f, 0.68f, 0.32f), // Sand hazmat
            };
            Color suit = suitTints[Mathf.Abs(slot) % suitTints.Length];
            var suitMat = Mats.Lit(suit, null, 0.28f);
            var darkMat = Mats.Lit(new Color(0.12f, 0.12f, 0.13f), null, 0.45f);
            var visorMat = Mats.Lit(new Color(0.06f, 0.08f, 0.10f), null, 0.88f);
            var lensMat = Mats.Emissive(new Color(1f, 0.96f, 0.85f), 3.5f);

            _bodyRoot = new GameObject("Suit").transform;
            _bodyRoot.SetParent(transform, false);

            // Torso + Oxygen Backpack
            Part(_bodyRoot, "Torso", PrimitiveType.Capsule, new Vector3(0, 1.05f, 0), new Vector3(0.42f, 0.36f, 0.26f), suitMat);
            Part(_bodyRoot, "Pack", PrimitiveType.Cube, new Vector3(0, 1.10f, -0.19f), new Vector3(0.28f, 0.38f, 0.14f), darkMat);
            Part(_bodyRoot, "Tank", PrimitiveType.Cylinder, new Vector3(0.08f, 1.10f, -0.25f), new Vector3(0.09f, 0.18f, 0.09f), suitMat);

            // Head + Hazmat Hood + Visor + Flashlight
            Head = new GameObject("Head").transform;
            Head.SetParent(_bodyRoot, false);
            Head.localPosition = new Vector3(0, 1.54f, 0);
            Part(Head, "Helmet", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.30f, 0.33f, 0.31f), suitMat);
            Part(Head, "Visor", PrimitiveType.Cube, new Vector3(0, 0.01f, 0.12f), new Vector3(0.22f, 0.14f, 0.09f), visorMat);

            var flGo = new GameObject("FlashlightBeam");
            flGo.transform.SetParent(Head, false);
            flGo.transform.localPosition = new Vector3(0.16f, -0.14f, 0.22f);
            Flashlight = flGo.AddComponent<Light>();
            Flashlight.type = LightType.Spot;
            Flashlight.spotAngle = GameConfig.Current.player.flashlightAngle;
            Flashlight.innerSpotAngle = Flashlight.spotAngle * 0.45f;
            Flashlight.range = GameConfig.Current.player.flashlightRange;
            Flashlight.intensity = 5.2f;
            Flashlight.color = new Color(1f, 0.96f, 0.88f);
            Flashlight.shadows = LightShadows.None; // fast for multiple co-op lights
            Flashlight.enabled = false;

            // Arms
            _armL = new GameObject("ArmL").transform;
            _armL.SetParent(_bodyRoot, false);
            _armL.localPosition = new Vector3(-0.26f, 1.32f, 0);
            Part(_armL, "Limb", PrimitiveType.Capsule, new Vector3(0, -0.26f, 0), new Vector3(0.12f, 0.26f, 0.12f), suitMat);

            _armR = new GameObject("ArmR").transform;
            _armR.SetParent(_bodyRoot, false);
            _armR.localPosition = new Vector3(0.26f, 1.32f, 0);
            Part(_armR, "Limb", PrimitiveType.Capsule, new Vector3(0, -0.24f, 0.08f), new Vector3(0.12f, 0.25f, 0.12f), suitMat);
            Part(_armR, "Torch", PrimitiveType.Cylinder, new Vector3(0, -0.42f, 0.20f), new Vector3(0.05f, 0.10f, 0.05f), darkMat).localRotation = Quaternion.Euler(90f, 0, 0);
            Part(_armR, "Lens", PrimitiveType.Sphere, new Vector3(0, -0.42f, 0.29f), new Vector3(0.045f, 0.045f, 0.02f), lensMat);

            // Legs
            _legL = new GameObject("LegL").transform;
            _legL.SetParent(_bodyRoot, false);
            _legL.localPosition = new Vector3(-0.11f, 0.72f, 0);
            Part(_legL, "Limb", PrimitiveType.Capsule, new Vector3(0, -0.35f, 0), new Vector3(0.14f, 0.35f, 0.14f), suitMat);
            Part(_legL, "Boot", PrimitiveType.Cube, new Vector3(0, -0.68f, 0.04f), new Vector3(0.15f, 0.10f, 0.23f), darkMat);

            _legR = new GameObject("LegR").transform;
            _legR.SetParent(_bodyRoot, false);
            _legR.localPosition = new Vector3(0.11f, 0.72f, 0);
            Part(_legR, "Limb", PrimitiveType.Capsule, new Vector3(0, -0.35f, 0), new Vector3(0.14f, 0.35f, 0.14f), suitMat);
            Part(_legR, "Boot", PrimitiveType.Cube, new Vector3(0, -0.68f, 0.04f), new Vector3(0.15f, 0.10f, 0.23f), darkMat);

            // Trigger collider for [E] Revive raycasts without blocking walls/entity LOS
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.48f;
            col.height = 1.85f;
            col.center = new Vector3(0, 0.92f, 0);

            // Floating 3D Nametag
            _namePivot = new GameObject("Nametag").transform;
            _namePivot.SetParent(transform, false);
            _namePivot.localPosition = new Vector3(0, 1.98f, 0);
            _nameMesh = _namePivot.gameObject.AddComponent<TextMesh>();
            _nameMesh.font = UIKit.Font;
            var mr = _namePivot.GetComponent<MeshRenderer>();
            if (mr != null && UIKit.Font != null) mr.sharedMaterial = UIKit.Font.material;
            _nameMesh.fontSize = 32;
            _nameMesh.characterSize = 0.032f;
            _nameMesh.anchor = TextAnchor.LowerCenter;
            _nameMesh.alignment = TextAlignment.Center;
            UpdateNametagText();
        }

        private static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        private void SetupAudio()
        {
            _stepSource = gameObject.AddComponent<AudioSource>();
            _stepSource.spatialBlend = 1f;
            _stepSource.minDistance = 2f;
            _stepSource.maxDistance = 24f;
            _stepSource.volume = 0.45f;

            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.spatialBlend = 1f;
            _voiceSource.minDistance = 2.5f;
            _voiceSource.maxDistance = 36f;
            _voiceSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _voiceSource.loop = true;
            _voiceSource.volume = 1.0f;

            _streamClip = AudioClip.Create("ProximityVoice_" + Slot, RingSamples, 1, VoiceRate, false);
            _voiceSource.clip = _streamClip;
            _voiceSource.Play();
        }

        public void PushVoicePacket(byte[] mulaw)
        {
            if (mulaw == null || mulaw.Length == 0 || _streamClip == null) return;
            float[] pcm = MuLawCodec.Decode(mulaw);

            // If voice stream was idle, re-anchor write pointer 90ms ahead of playback head for low latency
            if (Time.unscaledTime - _lastVoicePushTime > 0.35f)
            {
                _writeSamplePos = (_voiceSource.timeSamples + VoiceRate / 11) % RingSamples;
            }
            _lastVoicePushTime = Time.unscaledTime;
            Speaking = true;

            WriteRing(_writeSamplePos, pcm);
            _writeSamplePos = (_writeSamplePos + pcm.Length) % RingSamples;
        }

        private void WriteRing(int pos, float[] samples)
        {
            if (pos + samples.Length <= RingSamples)
            {
                _streamClip.SetData(samples, pos);
            }
            else
            {
                int first = RingSamples - pos;
                var a = new float[first];
                var b = new float[samples.Length - first];
                System.Array.Copy(samples, 0, a, 0, first);
                System.Array.Copy(samples, first, b, 0, b.Length);
                _streamClip.SetData(a, pos);
                _streamClip.SetData(b, 0);
            }
        }

        private void ClearOldSamples()
        {
            if (_streamClip == null || _voiceSource == null) return;
            int playPos = _voiceSource.timeSamples;
            // Clear behind the playhead so unspoken buffer stays silent
            int safeEnd = (playPos - 400 + RingSamples) % RingSamples;
            int guard = 0;
            while (DistRing(_lastClearedSamplePos, safeEnd) >= _zeroBuf.Length && guard++ < 8)
            {
                // Don't overwrite freshly written ahead-of-playhead audio
                if (Time.unscaledTime - _lastVoicePushTime < 0.4f && DistRing(playPos, _lastClearedSamplePos) < VoiceRate)
                    break;
                WriteRing(_lastClearedSamplePos, _zeroBuf);
                _lastClearedSamplePos = (_lastClearedSamplePos + _zeroBuf.Length) % RingSamples;
            }
        }

        private static int DistRing(int from, int to) => (to - from + RingSamples) % RingSamples;

        private void UpdateNametagText()
        {
            if (_nameMesh == null) return;
            if (Dead) { _nameMesh.text = $"{PlayerName}\n[DEAD]"; _nameMesh.color = UIKit.Danger; }
            else if (Downed) { _nameMesh.text = $"{PlayerName}\n[DOWNED - REVIVE]"; _nameMesh.color = new Color(1f, 0.35f, 0.2f); }
            else if (Speaking) { _nameMesh.text = $"(( {PlayerName} ))"; _nameMesh.color = UIKit.Accent; }
            else { _nameMesh.text = PlayerName; _nameMesh.color = UIKit.Ink; }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            ClearOldSamples();
            if (Speaking && Time.unscaledTime - _lastVoicePushTime > 0.32f)
                Speaking = false;

            UpdateNametagText();

            Vector3 prev = transform.position;
            float snapDist = Vector3.Distance(prev, TargetPos);
            transform.position = snapDist > 12f ? TargetPos : Vector3.Lerp(prev, TargetPos, Mathf.Clamp01(dt * 14f));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, TargetYaw, 0), Mathf.Clamp01(dt * 14f));

            if (Head != null)
                Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(TargetPitch, 0, 0), Mathf.Clamp01(dt * 14f));

            if (Flashlight != null)
                Flashlight.enabled = FlashlightOn && !Dead;

            // Body pose: standing / crouching / downed
            if (_bodyRoot != null)
            {
                Vector3 targetScale = Downed || Dead ? new Vector3(1f, 0.85f, 1f) : Crouching ? new Vector3(1f, 0.66f, 1f) : Vector3.one;
                Quaternion targetRot = Downed || Dead ? Quaternion.Euler(78f, 0f, 0f) : Quaternion.identity;
                Vector3 targetLocalPos = Downed || Dead ? new Vector3(0, 0.22f, -0.45f) : Vector3.zero;
                _bodyRoot.localScale = Vector3.Lerp(_bodyRoot.localScale, targetScale, dt * 8f);
                _bodyRoot.localRotation = Quaternion.Slerp(_bodyRoot.localRotation, targetRot, dt * 8f);
                _bodyRoot.localPosition = Vector3.Lerp(_bodyRoot.localPosition, targetLocalPos, dt * 8f);
            }

            // Limb stride + footsteps
            Vector3 flatMove = transform.position - prev;
            flatMove.y = 0;
            float speed = flatMove.magnitude / Mathf.Max(dt, 1e-4f);
            if (!Downed && !Dead && speed > 0.25f)
            {
                _walkCycle += speed * dt * 3.8f;
                float swing = Mathf.Sin(_walkCycle) * Mathf.Clamp(speed * 7f, 10f, 38f);
                _legL.localRotation = Quaternion.Euler(swing, 0, 0);
                _legR.localRotation = Quaternion.Euler(-swing, 0, 0);
                _armL.localRotation = Quaternion.Euler(-swing * 0.6f, 0, 0);
                _armR.localRotation = Quaternion.Euler(FlashlightOn ? -24f : swing * 0.4f, 0, 0);

                _stepAccum += flatMove.magnitude;
                float stride = Sprinting ? 2.2f : Crouching ? 1.15f : 1.65f;
                if (_stepAccum >= stride)
                {
                    _stepAccum = 0f;
                    _stepSource.pitch = Random.Range(0.88f, 1.14f);
                    _stepSource.PlayOneShot(ProcAudio.Step(), Crouching ? 0.2f : Sprinting ? 0.85f : 0.5f);
                }
            }
            else if (_legL != null)
            {
                _legL.localRotation = Quaternion.Slerp(_legL.localRotation, Quaternion.identity, dt * 8f);
                _legR.localRotation = Quaternion.Slerp(_legR.localRotation, Quaternion.identity, dt * 8f);
                _armL.localRotation = Quaternion.Slerp(_armL.localRotation, Quaternion.identity, dt * 8f);
                _armR.localRotation = Quaternion.Slerp(_armR.localRotation, Quaternion.Euler(FlashlightOn ? -24f : 0f, 0, 0), dt * 8f);
            }

            // Face nametag toward local camera
            var cam = Camera.main;
            if (_namePivot != null && cam != null)
            {
                Vector3 toCam = _namePivot.position - cam.transform.position;
                if (toCam.sqrMagnitude > 0.01f)
                    _namePivot.rotation = Quaternion.LookRotation(toCam);
                _namePivot.gameObject.SetActive(!Dead || Vector3.Distance(cam.transform.position, transform.position) < 18f);
            }
        }

        public void Interact(PlayerController player)
        {
            if (Downed && !Dead && BackroomsNet.IsOnline)
            {
                BackroomsNet.Instance.RequestRevive(Slot);
                GameUI.Instance?.Toast($"REVIVED {PlayerName}!", 2.5f);
            }
        }

        private void OnDestroy()
        {
            if (_streamClip != null) Destroy(_streamClip);
        }
    }
}
