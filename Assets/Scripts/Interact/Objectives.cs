using Hollowmere.Core;
using Hollowmere.Gen;
using Hollowmere.Player;
using UnityEngine;

namespace Hollowmere.Interact
{
    /// <summary>
    /// Where fuses go. Installing one is loud on purpose - the objective and the danger
    /// are the same action, so progress always costs you something.
    /// </summary>
    public class FuseBox : MonoBehaviour, IInteractable
    {
        public float installNoiseRadius = 26f;

        Material _panelMaterial;
        Renderer _indicator;

        public string Prompt
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm == null) return "Fuse box";
                if (gm.PowerRestored) return "Power restored";
                if (gm.FusesCarried <= 0) return $"Fuse box ({gm.FusesInstalled}/{gm.FusesRequired}) - no fuse";
                return $"Install fuse ({gm.FusesInstalled}/{gm.FusesRequired})";
            }
        }

        public bool CanInteract(PlayerController player)
        {
            var gm = GameManager.Instance;
            return gm != null && !gm.PowerRestored && gm.FusesCarried > 0;
        }

        public void Interact(PlayerController player)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.InstallFuse();
            AudioSource.PlayClipAtPoint(ProceduralAudio.PowerUp, transform.position, 0.8f);

            // A hard, one-shot noise spike the entity will investigate.
            Horror.NoiseSystem.Emit(transform.position, installNoiseRadius);
            RefreshIndicator();
        }

        void Start() => RefreshIndicator();

        void RefreshIndicator()
        {
            if (_indicator == null) return;
            var gm = GameManager.Instance;
            bool done = gm != null && gm.PowerRestored;
            _indicator.sharedMaterial = ProceduralMaterials.Emissive(
                done ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.25f, 0.2f), 2.2f);
        }

        public static FuseBox Spawn(Vector3 groundPosition, Transform parent)
        {
            var body = LevelGenerator.MakeBox(parent, "FuseBox",
                groundPosition + Vector3.up * 1.3f, new Vector3(1.0f, 1.4f, 0.35f),
                ProceduralMaterials.Locker);

            var box = body.AddComponent<FuseBox>();

            var lamp = LevelGenerator.MakeBox(body.transform, "Indicator",
                groundPosition + new Vector3(0f, 1.85f, -0.25f), new Vector3(0.16f, 0.16f, 0.08f),
                ProceduralMaterials.Emissive(new Color(1f, 0.25f, 0.2f), 2.2f));
            Destroy(lamp.GetComponent<Collider>());
            box._indicator = lamp.GetComponent<Renderer>();

            return box;
        }
    }

    /// <summary>The way out. Inert until the fuse box has every fuse.</summary>
    public class ExitDoor : MonoBehaviour, IInteractable
    {
        Renderer _renderer;

        public string Prompt
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.PowerRestored) return "Open the door";
                return "Sealed - the door has no power";
            }
        }

        public bool CanInteract(PlayerController player) =>
            GameManager.Instance != null && GameManager.Instance.PowerRestored;

        public void Interact(PlayerController player)
        {
            AudioSource.PlayClipAtPoint(ProceduralAudio.DoorCreak, transform.position, 0.9f);
            GameManager.Instance?.OnEscaped();
        }

        void Update()
        {
            if (_renderer == null) return;

            // Goes from dead metal to lit once power is on, so the goal is visible at range.
            bool powered = GameManager.Instance != null && GameManager.Instance.PowerRestored;
            _renderer.sharedMaterial = powered
                ? ProceduralMaterials.Emissive(new Color(0.35f, 0.9f, 0.5f), 1.4f)
                : ProceduralMaterials.RustMetal;
        }

        public static ExitDoor Spawn(Vector3 groundPosition, Transform parent)
        {
            var body = LevelGenerator.MakeBox(parent, "ExitDoor",
                groundPosition + Vector3.up * 1.4f, new Vector3(1.6f, 2.8f, 0.3f),
                ProceduralMaterials.RustMetal);

            var door = body.AddComponent<ExitDoor>();
            door._renderer = body.GetComponent<Renderer>();
            return door;
        }
    }
}
