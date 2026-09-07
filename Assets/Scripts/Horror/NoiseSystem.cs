using System.Collections.Generic;
using UnityEngine;

namespace Hollowmere.Horror
{
    /// <summary>
    /// One-shot world noises (installing a fuse, a door, a dropped item). Continuous
    /// player noise is not routed through here - the entity reads
    /// <see cref="Player.PlayerController.NoiseRadius"/> directly, because a footstep
    /// stream would flood this list.
    /// </summary>
    public static class NoiseSystem
    {
        struct NoiseEvent
        {
            public Vector3 Position;
            public float Radius;
            public float ExpiresAt;
        }

        const float Lifetime = 6f;
        static readonly List<NoiseEvent> Events = new List<NoiseEvent>(16);

        public static void Emit(Vector3 position, float radius)
        {
            Events.Add(new NoiseEvent
            {
                Position = position,
                Radius = radius,
                ExpiresAt = Time.time + Lifetime
            });
        }

        /// <summary>
        /// Loudest noise the listener is currently inside the radius of. "Loudest" is
        /// scored by how far into the radius the listener is, so a small noise underfoot
        /// beats a big one at the edge of earshot.
        /// </summary>
        public static bool TryGetLoudest(Vector3 listener, out Vector3 position)
        {
            position = default;
            float bestScore = 0f;
            bool found = false;

            for (int i = Events.Count - 1; i >= 0; i--)
            {
                if (Time.time > Events[i].ExpiresAt)
                {
                    Events.RemoveAt(i);
                    continue;
                }

                float distance = Vector3.Distance(listener, Events[i].Position);
                if (distance > Events[i].Radius) continue;

                float score = 1f - distance / Events[i].Radius;
                if (score <= bestScore) continue;

                bestScore = score;
                position = Events[i].Position;
                found = true;
            }

            return found;
        }

        public static void Clear() => Events.Clear();
    }
}
