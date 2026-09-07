using UnityEngine;

namespace Hollowmere.Player
{
    /// <summary>
    /// Single choke point for input. Uses the legacy Input manager so the project works
    /// on a fresh Unity install with no package or Active Input Handling changes; swapping
    /// to the new Input System later means rewriting only this file.
    /// </summary>
    public static class PlayerInput
    {
        /// <summary>Set while a menu owns the input, so gameplay ignores everything but Pause.</summary>
        public static bool Suspended { get; set; }

        public static Vector2 Move
        {
            get
            {
                if (Suspended) return Vector2.zero;
                return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            }
        }

        public static Vector2 Look
        {
            get
            {
                if (Suspended) return Vector2.zero;
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            }
        }

        public static bool SprintHeld => !Suspended && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        public static bool CrouchHeld => !Suspended && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C));
        public static bool InteractPressed => !Suspended && Input.GetKeyDown(KeyCode.E);
        public static bool FlashlightPressed => !Suspended && Input.GetKeyDown(KeyCode.F);
        public static bool HoldBreathHeld => !Suspended && Input.GetMouseButton(1);

        /// <summary>Deliberately ignores <see cref="Suspended"/> - this is what un-suspends.</summary>
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape);
    }
}
