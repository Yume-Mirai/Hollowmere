using System.Collections.Generic;
using Hollowmere.Gen;
using UnityEngine;

namespace Hollowmere.Player
{
    /// <summary>
    /// The player's meters: stamina, breath, and fear. Fear is the interesting one -
    /// it climbs in darkness and near the entity, and it is what drives the heartbeat,
    /// the breathing, and the vignette. It is a pressure gauge, not a health bar; it
    /// never kills you on its own, because a meter that kills you for hiding teaches
    /// players to stop hiding.
    /// </summary>
    public class PlayerStatus : MonoBehaviour
    {
        [Header("Stamina")]
        public float staminaDrainPerSecond = 0.22f;
        public float staminaRegenPerSecond = 0.14f;
        public float staminaRegenDelay = 1.2f;

        [Header("Breath")]
        public float breathDrainPerSecond = 0.30f;
        public float breathRegenPerSecond = 0.45f;

        [Header("Fear")]
        public float fearRiseInDark = 0.055f;
        public float fearFallInLight = 0.16f;
        public float fearFromProximity = 0.42f;

        public float Stamina { get; private set; } = 1f;
        public float Breath { get; private set; } = 1f;
        public float Fear { get; private set; }
        public float Illumination { get; private set; }
        public bool IsHoldingBreath { get; private set; }
        public bool IsHidden { get; set; }

        float _staminaCooldown;
        Light[] _sceneLights;
        float _lightRefreshTimer;

        AudioSource _heartbeat;
        AudioSource _breathing;

        void Start()
        {
            _heartbeat = CreateLoop(ProceduralAudio.Heartbeat, 0f);
            _breathing = CreateLoop(ProceduralAudio.Breath, 0f);
            RefreshLights();
        }

        AudioSource CreateLoop(AudioClip clip, float volume)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.volume = volume;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.Play();
            return src;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateStamina(dt);
            UpdateBreath(dt);
            UpdateIllumination(dt);
            UpdateFear(dt);
            UpdateAudio();
        }

        // --- meters ----------------------------------------------------------

        void UpdateStamina(float dt)
        {
            if (_staminaCooldown > 0f)
            {
                _staminaCooldown -= dt;
                return;
            }
            Stamina = Mathf.Clamp01(Stamina + staminaRegenPerSecond * dt);
        }

        /// <summary>Returns false when exhausted, which is how sprinting actually ends.</summary>
        public bool TrySpendStamina(float dt)
        {
            if (Stamina <= 0f) return false;
            Stamina = Mathf.Clamp01(Stamina - staminaDrainPerSecond * dt);
            _staminaCooldown = staminaRegenDelay;
            return true;
        }

        void UpdateBreath(float dt)
        {
            IsHoldingBreath = PlayerInput.HoldBreathHeld && Breath > 0f;

            if (IsHoldingBreath) Breath = Mathf.Clamp01(Breath - breathDrainPerSecond * dt);
            else Breath = Mathf.Clamp01(Breath + breathRegenPerSecond * dt);
        }

        /// <summary>
        /// Cheap analytic light probe. A real one would render a probe camera; this sums
        /// inverse-square falloff from nearby lights, which is close enough to decide
        /// "am I standing somewhere lit" and costs nothing.
        /// </summary>
        void UpdateIllumination(float dt)
        {
            _lightRefreshTimer -= dt;
            if (_lightRefreshTimer <= 0f)
            {
                _lightRefreshTimer = 5f;
                RefreshLights();
            }

            float total = 0f;
            var pos = transform.position + Vector3.up * 1.4f;

            for (int i = 0; i < _sceneLights.Length; i++)
            {
                var light = _sceneLights[i];
                if (light == null || !light.isActiveAndEnabled || light.intensity <= 0.01f) continue;

                float distance = Vector3.Distance(pos, light.transform.position);
                if (distance > light.range) continue;

                float falloff = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, light.range));
                total += light.intensity * falloff * falloff;
            }

            Illumination = Mathf.Lerp(Illumination, Mathf.Clamp01(total), dt * 5f);
        }

        void RefreshLights()
        {
            _sceneLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        }

        void UpdateFear(float dt)
        {
            // Being in the dark raises fear; standing in light bleeds it off.
            float lightFactor = Mathf.Clamp01(Illumination * 2.2f);
            float delta = Mathf.Lerp(fearRiseInDark, -fearFallInLight, lightFactor);

            // Proximity to the entity dominates everything else when it is close.
            var entity = Horror.Entity.Instance;
            if (entity != null)
            {
                float distance = Vector3.Distance(transform.position, entity.transform.position);
                float proximity = 1f - Mathf.Clamp01(distance / 22f);
                if (entity.IsChasing) proximity = Mathf.Max(proximity, 0.85f);
                delta += proximity * proximity * fearFromProximity;
            }

            // Hiding calms you down, but only slowly, and never all the way.
            if (IsHidden) delta -= 0.10f;

            Fear = Mathf.Clamp01(Fear + delta * dt);
        }

        void UpdateAudio()
        {
            if (_heartbeat != null)
            {
                _heartbeat.volume = Mathf.Lerp(_heartbeat.volume, Mathf.Clamp01(Fear - 0.25f) * 0.85f, Time.deltaTime * 3f);
                _heartbeat.pitch = Mathf.Lerp(0.85f, 1.6f, Fear);
            }

            if (_breathing != null)
            {
                float target = IsHoldingBreath ? 0f : Mathf.Clamp01(Fear - 0.15f) * 0.5f;
                _breathing.volume = Mathf.Lerp(_breathing.volume, target, Time.deltaTime * 4f);
                _breathing.pitch = Mathf.Lerp(0.9f, 1.45f, Fear);
            }
        }

        public void ResetForNewRun()
        {
            Stamina = 1f;
            Breath = 1f;
            Fear = 0f;
            IsHidden = false;
            _staminaCooldown = 0f;
            RefreshLights();
        }
    }
}
