using System.Collections.Generic;
using UnityEngine;

namespace Hollowmere.Gen
{
    /// <summary>
    /// Synthesizes every sound in the game as raw samples. Horror audio is mostly noise,
    /// low sines and envelopes, which synthesize convincingly and keep the repo asset-free.
    /// Clips are generated once and cached.
    /// </summary>
    public static class ProceduralAudio
    {
        const int SampleRate = 44100;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static AudioClip Footstep(int variant) => Get($"step{variant}", () =>
        {
            var rng = new System.Random(variant * 7919);
            return Render("footstep", 0.16f, (t, n) =>
            {
                // Short noise burst through a decaying envelope - a scuff, not a click.
                float env = Mathf.Exp(-t * 34f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float body = Mathf.Sin(2f * Mathf.PI * (90f + variant * 11f) * t) * 0.5f;
                return (noise * 0.7f + body) * env * 0.45f;
            });
        });

        public static AudioClip Heartbeat => Get("heartbeat", () => Render("heartbeat", 1.0f, (t, n) =>
        {
            // Two thumps: lub at 0.0, dub at 0.24, then silence for the rest of the beat.
            float s = Thump(t, 0.00f, 58f) + Thump(t, 0.24f, 46f) * 0.75f;
            return s * 0.9f;
        }));

        public static AudioClip Drone => Get("drone", () => Render("drone", 8.0f, (t, n) =>
        {
            // Detuned low sines beating against each other, plus filtered noise on top.
            float a = Mathf.Sin(2f * Mathf.PI * 41.2f * t);
            float b = Mathf.Sin(2f * Mathf.PI * 43.1f * t) * 0.8f;
            float c = Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.25f;
            float wobble = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.07f * t);
            // Fade the head and tail so the loop point is inaudible.
            float loopFade = Mathf.Min(1f, Mathf.Min(t, 8.0f - t) / 0.5f);
            return (a + b + c) * 0.11f * wobble * loopFade;
        }));

        public static AudioClip Breath => Get("breath", () => Render("breath", 2.4f, (t, n) =>
        {
            var rng = new System.Random(n);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            // In for 0.9s, out for 1.1s, with a pause between.
            float inhale = Envelope(t, 0.05f, 0.9f) * 0.8f;
            float exhale = Envelope(t, 1.15f, 1.1f);
            return noise * (inhale + exhale) * 0.18f;
        }));

        public static AudioClip Growl => Get("growl", () => Render("growl", 1.8f, (t, n) =>
        {
            var rng = new System.Random(n * 31);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float pitch = 62f - t * 12f;
            float tone = Mathf.Sin(2f * Mathf.PI * pitch * t);
            float rasp = Mathf.Sin(2f * Mathf.PI * pitch * 2.7f * t) * 0.4f;
            float env = Envelope(t, 0.0f, 1.8f);
            return (tone + rasp + noise * 0.35f) * env * 0.5f;
        }));

        public static AudioClip Scream => Get("scream", () => Render("scream", 1.4f, (t, n) =>
        {
            var rng = new System.Random(n * 17);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            // Rising then collapsing pitch is what sells a shriek.
            float pitch = 320f + Mathf.Sin(t * 9f) * 60f + t * 240f;
            float tone = Mathf.Sin(2f * Mathf.PI * pitch * t);
            float env = Envelope(t, 0.0f, 1.4f);
            return (tone * 0.7f + noise * 0.5f) * env * 0.7f;
        }));

        public static AudioClip Pickup => Get("pickup", () => Render("pickup", 0.28f, (t, n) =>
        {
            float env = Mathf.Exp(-t * 12f);
            float a = Mathf.Sin(2f * Mathf.PI * 660f * t);
            float b = Mathf.Sin(2f * Mathf.PI * 990f * t) * 0.5f;
            return (a + b) * env * 0.3f;
        }));

        public static AudioClip Click => Get("click", () => Render("click", 0.07f, (t, n) =>
        {
            var rng = new System.Random(n * 3);
            float env = Mathf.Exp(-t * 90f);
            return (float)(rng.NextDouble() * 2.0 - 1.0) * env * 0.35f;
        }));

        public static AudioClip DoorCreak => Get("creak", () => Render("creak", 1.6f, (t, n) =>
        {
            var rng = new System.Random(n * 13);
            // Stick-slip: a wobbling mid tone gated by noise bursts.
            float pitch = 180f + Mathf.Sin(t * 5.5f) * 70f + t * 40f;
            float tone = Mathf.Sin(2f * Mathf.PI * pitch * t);
            float grit = Mathf.Abs(Mathf.Sin(t * 23f)) > 0.6f ? 1f : 0.25f;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.3f;
            return (tone + noise) * grit * Envelope(t, 0f, 1.6f) * 0.22f;
        }));

        public static AudioClip PowerUp => Get("powerup", () => Render("powerup", 2.2f, (t, n) =>
        {
            // Generator spinning up: pitch and amplitude both ramp.
            float pitch = 40f + t * 55f;
            float tone = Mathf.Sin(2f * Mathf.PI * pitch * t);
            float harm = Mathf.Sin(2f * Mathf.PI * pitch * 2f * t) * 0.3f;
            return (tone + harm) * Mathf.Clamp01(t / 1.4f) * 0.35f;
        }));

        // --- helpers -------------------------------------------------------

        static float Thump(float t, float start, float freq)
        {
            if (t < start) return 0f;
            float local = t - start;
            float env = Mathf.Exp(-local * 18f);
            return Mathf.Sin(2f * Mathf.PI * freq * local) * env;
        }

        /// <summary>Smooth attack/release window starting at <paramref name="start"/>.</summary>
        static float Envelope(float t, float start, float length)
        {
            float local = t - start;
            if (local < 0f || local > length) return 0f;
            float x = local / length;
            return Mathf.Sin(x * Mathf.PI);
        }

        static AudioClip Get(string key, System.Func<AudioClip> factory)
        {
            if (Cache.TryGetValue(key, out var existing) && existing != null) return existing;
            var clip = factory();
            Cache[key] = clip;
            return clip;
        }

        static AudioClip Render(string name, float seconds, System.Func<float, int, float> sample)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = Mathf.Clamp(sample(i / (float)SampleRate, i), -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
