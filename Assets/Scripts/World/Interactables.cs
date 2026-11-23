using UnityEngine;

namespace Backrooms
{
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact(PlayerController player);
    }

    /// <summary>The way out: a door standing on its own in a cell, light pouring out of it, humming so you can find it by ear.</summary>
    public class ExitDoor : MonoBehaviour, IInteractable
    {
        public string Prompt => "[E] Go through the door";
        private Light _glow;

        public static ExitDoor Create(Transform parent, Vector3 pos, float yaw, LevelTheme theme, float wallH)
        {
            var root = new GameObject("ExitDoor");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var frameMat = Mats.Lit(new Color(0.35f, 0.27f, 0.18f), null, 0.3f);
            var doorMat = Mats.Lit(new Color(0.42f, 0.32f, 0.2f), null, 0.25f);
            var lightMat = Mats.Emissive(new Color(1f, 0.98f, 0.92f), 6f);
            var signMat = Mats.Emissive(new Color(0.2f, 1f, 0.35f), 4f);

            // wall slab with the door in it, so it reads as a door and not a prop
            Box(root.transform, "WallL", new Vector3(-1.05f, wallH / 2, 0), new Vector3(1.1f, wallH, 0.22f), Mats.Lit(theme.wallColor, ProcTex.ForWall(theme.wallStyle), 0.08f), true);
            Box(root.transform, "WallR", new Vector3(1.05f, wallH / 2, 0), new Vector3(1.1f, wallH, 0.22f), Mats.Lit(theme.wallColor, ProcTex.ForWall(theme.wallStyle), 0.08f), true);
            Box(root.transform, "WallTop", new Vector3(0, 2.1f + (wallH - 2.1f) / 2, 0), new Vector3(1.0f, wallH - 2.1f, 0.22f), Mats.Lit(theme.wallColor, ProcTex.ForWall(theme.wallStyle), 0.08f), true);
            Box(root.transform, "FrameL", new Vector3(-0.53f, 1.05f, 0), new Vector3(0.08f, 2.1f, 0.3f), frameMat, false);
            Box(root.transform, "FrameR", new Vector3(0.53f, 1.05f, 0), new Vector3(0.08f, 2.1f, 0.3f), frameMat, false);
            Box(root.transform, "FrameT", new Vector3(0, 2.12f, 0), new Vector3(1.14f, 0.08f, 0.3f), frameMat, false);
            Box(root.transform, "Light", new Vector3(0, 1.05f, 0.05f), new Vector3(0.98f, 2.08f, 0.02f), lightMat, false);
            // the door itself, swung open toward you
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(-0.49f, 0, -0.05f);
            hinge.localRotation = Quaternion.Euler(0, -70f, 0);
            Box(hinge, "Door", new Vector3(0.49f, 1.04f, 0), new Vector3(0.96f, 2.06f, 0.05f), doorMat, false);
            Box(root.transform, "Sign", new Vector3(0, 2.4f, -0.13f), new Vector3(0.5f, 0.18f, 0.04f), signMat, false);
            // trigger volume for the interact ray
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.05f, -0.1f);
            col.size = new Vector3(1.0f, 2.1f, 0.4f);

            var door = root.AddComponent<ExitDoor>();
            var lgo = new GameObject("Glow");
            lgo.transform.SetParent(root.transform, false);
            lgo.transform.localPosition = new Vector3(0, 1.2f, -0.6f);
            door._glow = lgo.AddComponent<Light>();
            door._glow.type = LightType.Point;
            door._glow.color = new Color(1f, 0.97f, 0.9f);
            door._glow.intensity = 3f;
            door._glow.range = 7f;
            var a = root.AddComponent<AudioSource>();
            a.clip = ProcAudio.DoorChime(); a.loop = true; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = 2f; a.maxDistance = 45f; a.volume = 0.8f; a.Play();
            return door;
        }

        private static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collide)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collide) Destroy(go.GetComponent<Collider>());
        }

        private void Update()
        {
            if (_glow != null) _glow.intensity = 2.6f + 0.6f * Mathf.PerlinNoise(Time.time * 0.7f, 3f);
        }

        public void Interact(PlayerController player) => GameBoot.Instance.ExitReached();
    }

    /// <summary>Batteries for the flashlight and Almond Water (restores sanity and stamina).</summary>
    public class Pickup : MonoBehaviour, IInteractable
    {
        public PickupKind Kind;
        private Vector2Int _cell;
        private ChunkManager _owner;
        private Vector3 _base;

        public string Prompt => Kind == PickupKind.Battery ? "[E] Take battery" : "[E] Drink Almond Water";

        public static Pickup Create(Transform parent, Vector3 pos, PickupKind kind, Vector2Int cell, ChunkManager owner)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 0.05f;
            if (kind == PickupKind.Battery)
            {
                Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, 0.07f, 0), new Vector3(0.05f, 0.07f, 0.05f), Mats.Lit(new Color(0.15f, 0.15f, 0.15f), null, 0.6f));
                Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, 0.1f, 0), new Vector3(0.052f, 0.04f, 0.052f), Mats.Lit(new Color(0.85f, 0.55f, 0.1f), null, 0.6f));
            }
            else
            {
                Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, 0.12f, 0), new Vector3(0.08f, 0.12f, 0.08f), Mats.Lit(new Color(0.85f, 0.9f, 0.95f), null, 0.85f));
                Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, 0.26f, 0), new Vector3(0.035f, 0.025f, 0.035f), Mats.Lit(new Color(0.2f, 0.4f, 0.9f), null, 0.5f));
            }
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.35f; col.center = new Vector3(0, 0.15f, 0); col.isTrigger = true;
            var p = go.AddComponent<Pickup>();
            p.Kind = kind; p._cell = cell; p._owner = owner; p._base = go.transform.position;
            var l = new GameObject("Hint").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0, 0.3f, 0);
            l.type = LightType.Point; l.range = 1.2f; l.intensity = 0.6f;
            l.color = kind == PickupKind.Battery ? new Color(1f, 0.7f, 0.2f) : new Color(0.5f, 0.7f, 1f);
            return p;
        }

        private static void Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(t);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Destroy(go.GetComponent<Collider>());
        }

        private void Update()
        {
            transform.position = _base + Vector3.up * (0.03f * Mathf.Sin(Time.time * 2f));
            transform.Rotate(0, 40f * Time.deltaTime, 0);
        }

        public void Interact(PlayerController player)
        {
            if (Kind == PickupKind.Battery) player.AddBattery(50f);
            else player.DrinkAlmondWater();
            _owner?.MarkCollected(_cell);
            Destroy(gameObject);
        }
    }
}
