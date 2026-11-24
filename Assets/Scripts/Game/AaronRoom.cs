using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Aaron's corner of the Backrooms, at the start of every level: a riser, whatever is left of his drum set, Aaron
    /// himself (very angry), and the door onward that stays locked until this level's pieces are back on the kit.
    /// Aaron bangs on whatever he has, loud enough to hear from far away - follow the noise to find your way back.
    /// </summary>
    public class AaronRoom : MonoBehaviour, IInteractable
    {
        private Transform _kit, _aaron;
        private ExitDoor _door;
        private AudioSource _drums;
        private readonly Dictionary<string, GameObject> _built = new Dictionary<string, GameObject>();
        private float _step, _lineTimer = 3f, _rage = 1f;
        private int _stepIndex, _bar;
        private string[] _levelPieces = { };

        public Vector3 Position => transform.position;
        public bool LevelDone { get; private set; }

        private static readonly string[] AngryLines =
        {
            "WHERE IS MY {0}?!", "SOMEBODY TOOK MY {0}. FIND IT.", "I CAN'T PLAY WITHOUT MY {0}!", "DO YOU KNOW HOW MUCH A {0} COSTS?!",
            "GO. FIND. MY. {0}.", "I SWEAR, IF ANYONE TOUCHED MY {0}...", "THE {0}! I NEED THE {0}!"
        };
        private static readonly string[] HappyLines = { "...okay. Okay. That's better.", "Now we're talking.", "Door's open. Go find the rest." };
        private static readonly string[] DoneLines = { "IT'S ALL HERE. IT'S ALL HERE!", "Listen to that. LISTEN TO THAT." };

        public static AaronRoom Create(Vector3 center, string[] levelPieces)
        {
            var go = new GameObject("AaronRoom");
            go.transform.position = center;
            var room = go.AddComponent<AaronRoom>();
            room._levelPieces = levelPieces ?? new string[0];
            room.Build();
            return room;
        }

        private void Build()
        {
            // carpet riser
            var riser = GameObject.CreatePrimitive(PrimitiveType.Cube);
            riser.name = "Riser";
            riser.transform.SetParent(transform, false);
            riser.transform.localPosition = new Vector3(0, 0.075f, 0.1f);
            riser.transform.localScale = new Vector3(2.8f, 0.15f, 2.6f);
            riser.GetComponent<Renderer>().sharedMaterial = Mats.Lit(new Color(0.18f, 0.16f, 0.2f), ProcTex.Carpet(), 0.05f);

            // the kit faces the player, who arrives from -z
            _kit = new GameObject("Kit").transform;
            _kit.SetParent(transform, false);
            _kit.localPosition = new Vector3(0, 0.15f, 0.1f);
            _kit.localRotation = Quaternion.Euler(0, 180f, 0);

            // interact volume around the kit
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true; col.center = new Vector3(0, 1f, 0.2f); col.size = new Vector3(2.8f, 2f, 3f);

            // a dim spotlight on the kit so the room reads from a distance
            var l = new GameObject("KitLight").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = new Vector3(0, 2.8f, -0.4f);
            l.transform.localRotation = Quaternion.Euler(70, 0, 0);
            l.type = LightType.Spot; l.spotAngle = 75f; l.range = 6f; l.intensity = 6f; l.color = new Color(1f, 0.82f, 0.62f);

            _drums = gameObject.AddComponent<AudioSource>();
            _drums.spatialBlend = 1f; _drums.rolloffMode = AudioRolloffMode.Logarithmic; _drums.minDistance = 5f; _drums.maxDistance = 160f;
            _drums.dopplerLevel = 0f;

            // the way on: behind Aaron, locked until he has this level's pieces
            _door = ExitDoor.Create(transform, transform.position + new Vector3(0, 0, 5.6f), 0f, GameBoot.Instance.Field.Theme, GameConfig.Current.world.wallHeight);
            _door.Locked = true;

            Refresh();
            SpawnAaron();
        }

        private async void SpawnAaron()
        {
            var lib = EntityLibrary.Instance;
            var def = lib != null ? lib.Get("aaron") : null;
            _aaron = new GameObject("Aaron").transform;
            _aaron.SetParent(transform, false);
            _aaron.localPosition = new Vector3(0, 0.15f, 1.3f);
            _aaron.localRotation = Quaternion.Euler(0, 180f, 0);
            if (def != null) await lib.Spawn(def, _aaron);
        }

        /// <summary>Rebuild the kit to match what has been handed back so far.</summary>
        public void Refresh()
        {
            var placed = GameBoot.Instance.Placed;
            foreach (var p in DrumKit.All)
            {
                bool want = placed.Contains(p.Id);
                _built.TryGetValue(p.Id, out var go);
                if (want && go == null)
                {
                    go = new GameObject(p.Id);
                    go.transform.SetParent(_kit, false);
                    go.transform.localPosition = p.KitPos;
                    go.transform.localRotation = Quaternion.Euler(p.KitEuler);
                    DrumKit.Build(p.Id, go.transform);
                    _built[p.Id] = go;
                }
                else if (!want && go != null) { Destroy(go); _built.Remove(p.Id); }
            }
            LevelDone = true;
            foreach (var id in _levelPieces) if (!placed.Contains(id)) LevelDone = false;
            _door.Locked = !LevelDone;
            _door.LockedText = LevelDone ? "" : "Locked. Aaron still wants his " + Missing().ToLowerInvariant() + ".";
            _rage = LevelDone ? 0.2f : 1f;
        }

        private string Missing()
        {
            var list = new List<string>();
            foreach (var id in _levelPieces) if (!GameBoot.Instance.Placed.Contains(id)) list.Add(DrumKit.NameOf(id));
            return list.Count == 0 ? "" : string.Join(", ", list);
        }

        public string Prompt
        {
            get
            {
                var carried = GameBoot.Instance.Carried;
                if (carried.Count > 0) return "[E] Give Aaron the " + string.Join(", ", carried.ConvertAll(x => DrumKit.NameOf(x))).ToLowerInvariant();
                return LevelDone ? "Aaron: \"Go on. Door's open.\"" : "Aaron wants his " + Missing().ToLowerInvariant();
            }
        }

        public void Interact(PlayerController player)
        {
            if (GameBoot.Instance.Carried.Count == 0) { Say(); return; }
            GameBoot.Instance.DeliverPieces();
            Refresh();
            GameBoot.Instance.UI.Subtitle("AARON", LevelDone ? (GameBoot.Instance.KitComplete ? Pick(DoneLines) : Pick(HappyLines)) : "...FINE. NOW THE " + FirstMissing() + ".");
            _lineTimer = 12f;
        }

        private string FirstMissing()
        {
            foreach (var id in _levelPieces) if (!GameBoot.Instance.Placed.Contains(id)) return DrumKit.NameOf(id).ToUpperInvariant();
            return "REST";
        }

        private static string Pick(string[] a) => a[Random.Range(0, a.Length)];

        private void Say()
        {
            if (LevelDone) GameBoot.Instance.UI.Subtitle("AARON", GameBoot.Instance.KitComplete ? Pick(DoneLines) : Pick(HappyLines));
            else GameBoot.Instance.UI.Subtitle("AARON", string.Format(Pick(AngryLines), FirstMissing()));
            _lineTimer = Random.Range(9f, 14f);
        }

        private void Update()
        {
            var player = GameBoot.Instance != null ? GameBoot.Instance.Player : null;
            if (player == null) return;
            float dist = Vector3.Distance(player.transform.position, transform.position);

            // Aaron yells when you're in earshot
            _lineTimer -= Time.deltaTime;
            if (_lineTimer <= 0f && dist < 14f) Say();

            // angry shaking, calmer when the level's pieces are back
            if (_aaron != null)
            {
                Vector3 to = player.transform.position - _aaron.position; to.y = 0;
                if (to.sqrMagnitude > 0.01f && dist < 12f)
                    _aaron.rotation = Quaternion.Slerp(_aaron.rotation, Quaternion.LookRotation(to), Time.deltaTime * 2f);
                _aaron.localPosition = new Vector3(Mathf.Sin(Time.time * 37f) * 0.015f * _rage, 0.15f, 1.3f);
            }

            Drum();
        }

        // ------------------------------------------------------------------ Aaron's playing: only what's on the kit
        private void Drum()
        {
            var placed = GameBoot.Instance.Placed;
            bool sloppy = !placed.Contains("throne");
            float bpm = placed.Count == 0 ? 60f : LevelDone ? 104f : 132f;
            _step -= Time.deltaTime;
            if (_step > 0f) return;
            _step = 60f / bpm / 4f * (sloppy ? Random.Range(0.8f, 1.2f) : 1f);
            int s = _stepIndex;
            _stepIndex = (_stepIndex + 1) % 16;
            if (_stepIndex == 0) _bar++;
            float vol = placed.Contains("sticks") ? 1f : 0.55f;   // hands instead of sticks

            if (placed.Count == 0)
            {
                // nothing to play: he stomps and slams the riser
                if (s % 4 == 0) Hit("stomp", 0.9f);
                return;
            }
            bool fill = _bar % 4 == 3 && s >= 12;
            if (placed.Contains("kick") && (s == 0 || s == 8 || (placed.Contains("pedal") && (s == 10 || s == 3)))) Hit("kick", 1f);
            if (placed.Contains("snare") && (s == 4 || s == 12) && !fill) Hit("snare", vol);
            bool hasHat = placed.Contains("hihat"), hasRide = placed.Contains("ride");
            if ((hasHat || hasRide) && s % 2 == 0 && !fill) Hit(hasRide && _bar % 8 >= 4 ? "ride" : hasHat ? "hat" : "ride", vol * 0.6f);
            if (placed.Contains("crash") && s == 0 && _bar % 4 == 0) Hit("crash", vol * 0.8f);
            if (fill)
            {
                if (placed.Contains("racktom") && (s == 12 || s == 13)) Hit("tomhi", vol);
                else if (placed.Contains("floortom") && (s == 14 || s == 15)) Hit("tomlo", vol);
                else if (placed.Contains("snare") && s % 1 == 0) Hit("snare", vol * 0.7f);
            }
        }

        private void Hit(string kind, float vol)
        {
            _drums.PlayOneShot(DrumKit.Sound(kind), vol * Random.Range(0.85f, 1f));
        }
    }
}
