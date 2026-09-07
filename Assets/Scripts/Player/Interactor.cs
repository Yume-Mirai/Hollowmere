using Hollowmere.Interact;
using UnityEngine;

namespace Hollowmere.Player
{
    /// <summary>
    /// Camera-forward raycast that finds the current <see cref="IInteractable"/> and
    /// runs it on E. Also publishes the prompt the HUD reads, so the HUD does no
    /// scene queries of its own.
    /// </summary>
    public class Interactor : MonoBehaviour
    {
        public float range = 2.6f;
        public LayerMask mask = ~0;

        /// <summary>Null when nothing is targeted. The HUD polls this.</summary>
        public IInteractable Current { get; private set; }
        public bool CurrentIsAvailable { get; private set; }

        PlayerController _player;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        void Awake() => _player = GetComponent<PlayerController>();

        void Update()
        {
            Current = FindTarget();
            CurrentIsAvailable = Current != null && Current.CanInteract(_player);

            if (Current != null && CurrentIsAvailable && PlayerInput.InteractPressed)
            {
                Current.Interact(_player);
            }
        }

        IInteractable FindTarget()
        {
            if (_player == null || _player.Cam == null) return null;

            var ray = new Ray(_player.Cam.transform.position, _player.Cam.transform.forward);

            // NonAlloc + manual sort: a plain Raycast would stop at the trigger volume of
            // whichever collider happened to be first, not the nearest interactable.
            int count = Physics.RaycastNonAlloc(ray, _hits, range, mask, QueryTriggerInteraction.Collide);
            if (count == 0) return null;

            IInteractable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var candidate = _hits[i].collider.GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                if (_hits[i].distance >= bestDistance) continue;

                best = candidate;
                bestDistance = _hits[i].distance;
            }

            return best;
        }
    }
}
