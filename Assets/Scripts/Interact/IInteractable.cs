using Hollowmere.Player;

namespace Hollowmere.Interact
{
    /// <summary>Anything the player can look at and press E on.</summary>
    public interface IInteractable
    {
        /// <summary>Text shown in the reticle prompt, e.g. "Take battery".</summary>
        string Prompt { get; }

        /// <summary>False greys the prompt out - used for locked doors and full batteries.</summary>
        bool CanInteract(PlayerController player);

        void Interact(PlayerController player);
    }
}
