using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Backrooms
{
    /// <summary>Compact 8-bit ITU-T G.711 mu-law codec (8 kHz = 8 KB/s) for proximity voice &amp; stolen monster bait clips.</summary>
    public static class MuLawCodec
    {
        private static readonly float[] DecodeTable = BuildDecodeTable();

        private static float[] BuildDecodeTable()
        {
            var t = new float[256];
            for (int i = 0; i < 256; i++)
            {
                int v = ~i & 0xFF;
                int sign = v & 0x80;
                int exponent = (v >> 4) & 0x07;
                int mantissa = v & 0x0F;
                int sample = ((mantissa << 3) + 0x84) << exponent;
                sample -= 0x84;
                float pcm = (sign != 0 ? -sample : sample) / 32768f;
                t[i] = Mathf.Clamp(pcm, -1f, 1f);
            }
            return t;
        }

        public static byte EncodeSample(float f)
        {
            int pcm = Mathf.Clamp(Mathf.RoundToInt(f * 32767f), -32767, 32767);
            int sign = (pcm >> 8) & 0x80;
            if (sign != 0) pcm = -pcm;
            if (pcm > 32635) pcm = 32635;
            pcm += 0x84;

            int exponent = 7;
            for (int expMask = 0x4000; (pcm & expMask) == 0 && exponent > 0; exponent--, expMask >>= 1) { }
            int mantissa = (pcm >> (exponent + 3)) & 0x0F;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        public static float DecodeSample(byte b) => DecodeTable[b];

        public static byte[] Encode(float[] samples, int offset, int count)
        {
            var res = new byte[count];
            for (int i = 0; i < count; i++) res[i] = EncodeSample(samples[offset + i]);
            return res;
        }

        public static float[] Decode(byte[] bytes)
        {
            if (bytes == null) return Array.Empty<float>();
            var res = new float[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) res[i] = DecodeTable[bytes[i]];
            return res;
        }
    }

    /// <summary>
    /// Shared repository of recorded real player voice phrases. Voice-stealing SCPs (SCP-939, mimics, listeners, crooked stalkers)
    /// pull clips from here — preferring a teammate's voice over your own in Co-Op — to lure players into dark ambushes.
    /// </summary>
    public static class VoiceMimicBank
    {
        public const int SampleRate = 8000;
        private const int MaxPhrases = 24;

        public class StolenPhrase
        {
            public int clipId;
            public int speakerSlot;
            public string speakerName;
            public byte[] mulaw;
            public AudioClip eerieClip;
        }

        public static readonly List<StolenPhrase> Phrases = new List<StolenPhrase>();
        public static float LastRecordedFlashTime { get; private set; } = -99f;

        public static StolenPhrase AddPhrase(int clipId, int speakerSlot, string speakerName, byte[] mulaw, bool localCapture = false)
        {
            if (mulaw == null || mulaw.Length < SampleRate / 3) return null;
            foreach (var existing in Phrases)
                if (existing.clipId == clipId) return existing;

            if (Phrases.Count >= MaxPhrases)
            {
                if (Phrases[0].eerieClip != null) UnityEngine.Object.Destroy(Phrases[0].eerieClip);
                Phrases.RemoveAt(0);
            }

            var p = new StolenPhrase
            {
                clipId = clipId,
                speakerSlot = speakerSlot,
                speakerName = string.IsNullOrEmpty(speakerName) ? "EXPLORER" : speakerName,
                mulaw = mulaw
            };
            Phrases.Add(p);
            if (localCapture) LastRecordedFlashTime = Time.unscaledTime;
            return p;
        }

        public static StolenPhrase GetById(int clipId)
        {
            for (int i = 0; i < Phrases.Count; i++)
                if (Phrases[i].clipId == clipId) return Phrases[i];
            return null;
        }

        /// <summary>Picks a recorded player phrase, strongly preferring a DIFFERENT player's voice in Co-Op to bait teammates.</summary>
        public static StolenPhrase PickBaitPhrase(int targetSlot)
        {
            if (Phrases.Count == 0) return null;
            var otherVoices = new List<StolenPhrase>();
            for (int i = 0; i < Phrases.Count; i++)
                if (Phrases[i].speakerSlot != targetSlot) otherVoices.Add(Phrases[i]);

            if (otherVoices.Count > 0)
                return otherVoices[UnityEngine.Random.Range(0, otherVoices.Count)];
            return Phrases[UnityEngine.Random.Range(0, Phrases.Count)];
        }

        public static AudioClip GetOrBuildEerieClip(StolenPhrase p)
        {
            if (p == null) return null;
            if (p.eerieClip != null) return p.eerieClip;

            float[] raw = MuLawCodec.Decode(p.mulaw);
            int tail = SampleRate / 3; // +0.33s corridor echo tail
            var outBuf = new float[raw.Length + tail];
            int tap1 = Mathf.RoundToInt(SampleRate * 0.062f);
            int tap2 = Mathf.RoundToInt(SampleRate * 0.138f);

            // Subtle uncanny vocal tremor + dampened Backrooms hallway slapback
            for (int i = 0; i < outBuf.Length; i++)
            {
                float t = i / (float)SampleRate;
                float dry = i < raw.Length ? raw[i] : 0f;
                // Slight throat-rasp ring modulation so it sounds ALMOST human, but slightly wrong up close
                float warble = 0.90f + 0.10f * Mathf.Sin(2f * Mathf.PI * 6.5f * t);
                float s = dry * warble;
                if (i >= tap1) s += outBuf[i - tap1] * 0.28f;
                if (i >= tap2) s += outBuf[i - tap2] * 0.15f;
                outBuf[i] = Mathf.Clamp(s, -0.98f, 0.98f);
            }

            var clip = AudioClip.Create("BaitVoice_" + p.clipId, outBuf.Length, 1, SampleRate, false);
            clip.SetData(outBuf, 0);
            p.eerieClip = clip;
            return clip;
        }
    }

    /// <summary>
    /// Captures the player's real microphone:
    /// 1. Emits real-time game noise into NoiseSystem so SCPs hear you talking/screaming in the mic.
    /// 2. Streams compressed proximity voice to Co-Op teammates.
    /// 3. Slices spoken phrases when you talk and stores/syncs them into VoiceMimicBank so monsters use your voice as bait!
    /// </summary>
    public class VoiceMimicSystem : MonoBehaviour
    {
        public static VoiceMimicSystem Instance { get; private set; }

        public bool HasMic { get; private set; }
        public bool IsMuted { get; private set; }
        public float MicLevel01 { get; private set; }
        public bool IsSpeaking { get; private set; }
        public bool IsAudibleToMonsters => !IsMuted && MicLevel01 > 0.14f;
        public bool RecentlyRecorded => Time.unscaledTime - VoiceMimicBank.LastRecordedFlashTime < 2.6f;

        private const int Rate = VoiceMimicBank.SampleRate;
        private const int RecordSeconds = 10;
        private const int ChunkSamples = 800; // 100 ms chunks

        private string _device;
        private AudioClip _micClip;
        private int _readPos;
        private readonly float[] _chunkBuf = new float[ChunkSamples];
        private readonly List<float> _phraseSamples = new List<float>(Rate * 4);
        private float _speechHoldTimer;
        private float _silenceInPhrase;
        private bool _capturingPhrase;
        private float _noiseEmitCooldown;
        private float _retryMicTimer = 2f;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            TryStartMic();
        }

        private void OnDestroy()
        {
            if (HasMic && !string.IsNullOrEmpty(_device) && Microphone.IsRecording(_device))
                Microphone.End(_device);
        }

        private void TryStartMic()
        {
            try
            {
                var devs = Microphone.devices;
                if (devs == null || devs.Length == 0) { HasMic = false; return; }
                _device = devs[0];
                _micClip = Microphone.Start(_device, true, RecordSeconds, Rate);
                _readPos = 0;
                HasMic = _micClip != null;
                if (HasMic) Debug.Log($"[Backrooms] Microphone active: '{_device}' ({Rate} Hz)");
            }
            catch (Exception e)
            {
                HasMic = false;
                Debug.LogWarning("[Backrooms] Microphone unavailable: " + e.Message);
            }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame && GameBoot.Instance != null && GameBoot.Instance.State == GameBoot.Mode.Playing)
            {
                IsMuted = !IsMuted;
                GameUI.Instance?.Toast(IsMuted ? "MIC MUTED [V]" : "MIC LIVE [V] — ENTITIES HEAR YOUR VOICE", 2.5f);
            }

            if (!HasMic || _micClip == null)
            {
                MicLevel01 = 0f;
                IsSpeaking = false;
                _retryMicTimer -= Time.unscaledDeltaTime;
                if (_retryMicTimer <= 0f) { _retryMicTimer = 5f; TryStartMic(); }
                return;
            }

            int writePos = Microphone.GetPosition(_device);
            if (writePos < 0) return;

            int totalSamples = _micClip.samples;
            int avail = writePos - _readPos;
            if (avail < 0) avail += totalSamples;

            // If a hitch caused buffer wraparound, skip to most recent chunk
            if (avail > Rate * 2)
            {
                _readPos = (writePos - ChunkSamples + totalSamples) % totalSamples;
                avail = ChunkSamples;
            }

            while (avail >= ChunkSamples)
            {
                ReadCircular(_readPos, ChunkSamples, _chunkBuf);
                _readPos = (_readPos + ChunkSamples) % totalSamples;
                avail -= ChunkSamples;
                ProcessChunk(_chunkBuf);
            }

            _noiseEmitCooldown -= Time.deltaTime;
            if (IsAudibleToMonsters && _noiseEmitCooldown <= 0f && GameBoot.Instance != null
                && GameBoot.Instance.State == GameBoot.Mode.Playing && GameBoot.Instance.Player != null && !GameBoot.Instance.Player.Dead)
            {
                _noiseEmitCooldown = 0.16f;
                float radius = Mathf.Lerp(5.5f, 30f, MicLevel01);
                NoiseSystem.Emit(GameBoot.Instance.Player.transform.position, radius);
            }
        }

        private void ReadCircular(int pos, int count, float[] dst)
        {
            int cap = _micClip.samples;
            if (pos + count <= cap)
            {
                _micClip.GetData(dst, pos);
            }
            else
            {
                int first = cap - pos;
                var tmpA = new float[first];
                var tmpB = new float[count - first];
                _micClip.GetData(tmpA, pos);
                _micClip.GetData(tmpB, 0);
                Array.Copy(tmpA, 0, dst, 0, first);
                Array.Copy(tmpB, 0, dst, first, count - first);
            }
        }

        private void ProcessChunk(float[] chunk)
        {
            if (IsMuted)
            {
                MicLevel01 = Mathf.MoveTowards(MicLevel01, 0f, 0.25f);
                IsSpeaking = false;
                if (_capturingPhrase) FinalizePhrase();
                return;
            }

            float sumSq = 0f;
            for (int i = 0; i < chunk.Length; i++) sumSq += chunk[i] * chunk[i];
            float rms = Mathf.Sqrt(sumSq / chunk.Length);
            float level = Mathf.Clamp01((rms - 0.007f) * 14f);
            MicLevel01 = Mathf.Lerp(MicLevel01, level, level > MicLevel01 ? 0.65f : 0.30f);

            bool voiceNow = level > 0.11f;
            if (voiceNow) _speechHoldTimer = 0.28f;
            else _speechHoldTimer = Mathf.Max(0f, _speechHoldTimer - 0.10f);

            IsSpeaking = _speechHoldTimer > 0f;

            // Live proximity voice stream in Co-Op
            if (IsSpeaking && BackroomsNet.IsOnline)
            {
                byte[] livePacket = MuLawCodec.Encode(chunk, 0, chunk.Length);
                BackroomsNet.Instance.SendLiveVoice(livePacket);
            }

            // Record spoken phrases during active gameplay so monsters can steal & mimic them!
            bool inRun = GameBoot.Instance != null && GameBoot.Instance.State == GameBoot.Mode.Playing;
            if (!inRun)
            {
                _capturingPhrase = false;
                _phraseSamples.Clear();
                return;
            }

            if (voiceNow)
            {
                _capturingPhrase = true;
                _silenceInPhrase = 0f;
                for (int i = 0; i < chunk.Length; i++) _phraseSamples.Add(chunk[i]);
                if (_phraseSamples.Count >= Mathf.RoundToInt(Rate * 2.9f))
                    FinalizePhrase();
            }
            else if (_capturingPhrase)
            {
                _silenceInPhrase += 0.10f;
                for (int i = 0; i < chunk.Length; i++) _phraseSamples.Add(chunk[i]);
                if (_silenceInPhrase >= 0.32f)
                    FinalizePhrase();
            }
        }

        private void FinalizePhrase()
        {
            _capturingPhrase = false;
            _silenceInPhrase = 0f;
            int minSamples = Mathf.RoundToInt(Rate * 0.65f);
            if (_phraseSamples.Count < minSamples)
            {
                _phraseSamples.Clear();
                return;
            }

            var arr = _phraseSamples.ToArray();
            _phraseSamples.Clear();

            // Peak-normalize spoken phrase so playback in 3D corridors is clear and chilling
            float peak = 0.01f;
            for (int i = 0; i < arr.Length; i++)
            {
                float a = Mathf.Abs(arr[i]);
                if (a > peak) peak = a;
            }
            if (peak < 0.018f) return; // Reject quiet breathing / mic hiss
            float gain = Mathf.Min(6.5f, 0.86f / peak);
            for (int i = 0; i < arr.Length; i++) arr[i] = Mathf.Clamp(arr[i] * gain, -0.98f, 0.98f);

            byte[] mulaw = MuLawCodec.Encode(arr, 0, arr.Length);
            int slot = BackroomsNet.Instance != null ? BackroomsNet.Instance.MySlot : 0;
            string speaker = BackroomsNet.Instance != null ? BackroomsNet.Instance.MyName : "YOU";
            int clipId = UnityEngine.Random.Range(1000, int.MaxValue - 1);

            VoiceMimicBank.AddPhrase(clipId, slot, speaker, mulaw, localCapture: true);
            if (BackroomsNet.IsOnline)
                BackroomsNet.Instance.BroadcastStolenVoice(clipId, slot, speaker, mulaw);
        }
    }
}
