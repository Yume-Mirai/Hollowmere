using Hollowmere.Core;
using Hollowmere.Gen;
using Hollowmere.Player;
using UnityEngine;

namespace Hollowmere.Interact
{
    /// <summary>
    /// A locker the player can climb into. Hiding blinds the entity's vision, but it is
    /// not a safe room: if the entity watched you get in, it comes straight to the door.
    /// Without that rule the locker is an "I win" button.
    /// </summary>
    public class HidingSpot : MonoBehaviour, IInteractable
    {
        public static HidingSpot Occupied { get; private set; }

        public string Prompt => Occupied == this ? "Get out" : "Hide";

        public bool CanInteract(PlayerController player) => Occupied == null || Occupied == this;

        Vector3 _interiorPosition;
        Vector3 _exitPosition;
        int _lastToggleFrame = -1;

        public void Interact(PlayerController player)
        {
            if (Time.frameCount == _lastToggleFrame) return;
            _lastToggleFrame = Time.frameCount;

            if (Occupied == this) Exit(player);
            else Enter(player);
        }

        void Update()
        {
            // While inside, the player is usually not looking at any collider, so the
            // normal interact raycast cannot get them out again.
            if (Occupied != this) return;
            if (!PlayerInput.InteractPressed) return;
            if (Time.frameCount == _lastToggleFrame) return;

            _lastToggleFrame = Time.frameCount;
            Exit(PlayerController.Instance);
        }

        void Enter(PlayerController player)
        {
            if (player == null || Occupied != null) return;

            Occupied = this;
            _exitPosition = player.transform.position;

            Teleport(player, _interiorPosition);
            player.Frozen = true;

            var status = player.GetComponent<PlayerStatus>();
            if (status != null) status.IsHidden = true;

            AudioSource.PlayClipAtPoint(ProceduralAudio.DoorCreak, transform.position, 0.4f);
            GameManager.Instance?.Notify("Hidden - E to get out");
        }

        void Exit(PlayerController player)
        {
            if (player == null) return;

            Occupied = null;
            Teleport(player, _exitPosition);
            player.Frozen = false;

            var status = player.GetComponent<PlayerStatus>();
            if (status != null) status.IsHidden = false;

            AudioSource.PlayClipAtPoint(ProceduralAudio.DoorCreak, transform.position, 0.4f);
        }

        /// <summary>CharacterController overwrites transform writes, so it must be off to move.</summary>
        static void Teleport(PlayerController player, Vector3 position)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = position;
            if (cc != null) cc.enabled = true;
        }

        public static void ClearOccupant()
        {
            Occupied = null;
        }

        public static HidingSpot Spawn(Vector3 groundPosition, Transform parent)
        {
            var body = LevelGenerator.MakeBox(parent, "Locker",
                groundPosition + Vector3.up * 1.0f, new Vector3(0.9f, 2.0f, 0.75f),
                ProceduralMaterials.Locker);

            var spot = body.AddComponent<HidingSpot>();
            // Sit the player low in the locker so the view reads as "crouched inside".
            spot._interiorPosition = groundPosition + Vector3.up * 0.05f;
            return spot;
        }
    }
}
