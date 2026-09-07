using Hollowmere.Core;
using Hollowmere.Gen;
using Hollowmere.Player;
using UnityEngine;

namespace Hollowmere.Interact
{
    /// <summary>Shared presentation for world items: gentle bob, slow spin, faint glow.</summary>
    public abstract class PickupBase : MonoBehaviour, IInteractable
    {
        public abstract string Prompt { get; }

        protected float BobHeight = 0.12f;
        protected float BobSpeed = 1.6f;
        protected float SpinSpeed = 40f;

        Vector3 _basePosition;
        float _phase;

        protected virtual void Start()
        {
            _basePosition = transform.position;
            _phase = Random.value * Mathf.PI * 2f;
        }

        protected virtual void Update()
        {
            float t = Time.time * BobSpeed + _phase;
            transform.position = _basePosition + Vector3.up * (Mathf.Sin(t) * BobHeight);
            transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
        }

        public virtual bool CanInteract(PlayerController player) => true;

        public abstract void Interact(PlayerController player);

        protected void Consume()
        {
            AudioSource.PlayClipAtPoint(ProceduralAudio.Pickup, transform.position, 0.7f);
            Destroy(gameObject);
        }

        /// <summary>
        /// Builds the item body. The collider is a trigger so the player walks through
        /// items rather than shin-barking them, while the interact raycast still hits.
        /// </summary>
        protected static GameObject BuildBody(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            var go = LevelGenerator.MakeBox(parent, name, position, size, material);
            var collider = go.GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;
            return go;
        }
    }

    /// <summary>Restores flashlight charge. The most common thing in the level by design.</summary>
    public class BatteryPickup : PickupBase
    {
        public float charge = 0.45f;

        public override string Prompt => "Take battery";

        public override bool CanInteract(PlayerController player) =>
            Flashlight.Instance != null && Flashlight.Instance.Battery < 0.999f;

        public override void Interact(PlayerController player)
        {
            if (Flashlight.Instance == null) return;
            if (!Flashlight.Instance.AddBattery(charge)) return;

            GameManager.Instance?.Notify("Battery collected");
            Consume();
        }

        public static BatteryPickup Spawn(Vector3 groundPosition, Transform parent)
        {
            var body = BuildBody("Battery", groundPosition + Vector3.up * 0.5f,
                new Vector3(0.16f, 0.34f, 0.16f),
                ProceduralMaterials.Emissive(new Color(0.35f, 0.85f, 0.45f), 1.1f), parent);
            return body.AddComponent<BatteryPickup>();
        }
    }

    /// <summary>One of the fuses required to power the exit.</summary>
    public class FusePickup : PickupBase
    {
        public override string Prompt => "Take fuse";

        protected override void Start()
        {
            base.Start();
            BobHeight = 0.16f;
            SpinSpeed = 60f;
        }

        public override void Interact(PlayerController player)
        {
            GameManager.Instance?.CollectFuse();
            Consume();
        }

        public static FusePickup Spawn(Vector3 groundPosition, Transform parent)
        {
            var body = BuildBody("Fuse", groundPosition + Vector3.up * 0.6f,
                new Vector3(0.22f, 0.3f, 0.22f),
                ProceduralMaterials.Emissive(new Color(1f, 0.72f, 0.2f), 2.0f), parent);
            return body.AddComponent<FusePickup>();
        }
    }
}
