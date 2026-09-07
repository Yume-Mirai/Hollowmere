using Hollowmere.Gen;
using UnityEngine;

namespace Hollowmere.Player
{
    /// <summary>
    /// The flashlight is the game's real resource. Battery drains only while it is on,
    /// so the tension is "do I need to see right now" rather than a timer you cannot
    /// influence. Below 25% it starts failing, which telegraphs the problem before it
    /// becomes fatal.
    /// </summary>
    public class Flashlight : MonoBehaviour
    {
        public static Flashlight Instance { get; private set; }

        [Header("Battery")]
        public float drainPerSecond = 0.0125f;   // ~80 seconds of continuous light
        public float lowBatteryThreshold = 0.25f;

        [Header("Beam")]
        public float spotAngle = 55f;
        public float range = 26f;
        public float intensity = 3.2f;

        public float Battery { get; private set; } = 1f;
        public bool IsOn { get; private set; }

        Light _light;
        AudioSource _click;
        float _flickerTimer;
        float _swayX, _swayY;
        Transform _followTarget;

        public static Flashlight Attach(PlayerController player)
        {
            var go = new GameObject("Flashlight");
            go.transform.SetParent(player.Cam.transform, false);

            var flashlight = go.AddComponent<Flashlight>();
            flashlight._followTarget = player.Cam.transform;
            return flashlight;
        }

        void Awake()
        {
            Instance = this;

            _light = gameObject.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.spotAngle = spotAngle;
            _light.innerSpotAngle = spotAngle * 0.45f;
            _light.range = range;
            _light.intensity = 0f;
            _light.color = new Color(0.96f, 0.95f, 0.86f);
            _light.shadows = LightShadows.Hard;
            _light.shadowStrength = 0.9f;
            _light.enabled = false;

            _click = gameObject.AddComponent<AudioSource>();
            _click.spatialBlend = 0f;
            _click.playOnAwake = false;
            _click.volume = 0.5f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (PlayerInput.FlashlightPressed) Toggle();

            if (IsOn)
            {
                Battery = Mathf.Clamp01(Battery - drainPerSecond * Time.deltaTime);
                if (Battery <= 0f) SetOn(false);
            }

            UpdateBeam();
            UpdateSway();
        }

        public void Toggle() => SetOn(!IsOn && Battery > 0f);

        public void SetOn(bool on)
        {
            if (on == IsOn) return;
            IsOn = on && Battery > 0f;

            _light.enabled = IsOn;
            if (_click != null) _click.PlayOneShot(ProceduralAudio.Click);
        }

        void UpdateBeam()
        {
            if (!IsOn)
            {
                _light.intensity = 0f;
                return;
            }

            float target = intensity;

            if (Battery < lowBatteryThreshold)
            {
                // Failure gets more violent as the cell dies: longer dropouts, dimmer peaks.
                float severity = 1f - Battery / lowBatteryThreshold;
                _flickerTimer -= Time.deltaTime;

                if (_flickerTimer <= 0f)
                {
                    _flickerTimer = Random.Range(0.04f, Mathf.Lerp(1.1f, 0.12f, severity));
                    target *= Random.value < Mathf.Lerp(0.06f, 0.55f, severity) ? Random.Range(0.0f, 0.25f) : 1f;
                }
                target *= Mathf.Lerp(1f, 0.55f, severity);
            }

            _light.intensity = Mathf.Lerp(_light.intensity, target, Time.deltaTime * 18f);
            _light.range = Mathf.Lerp(range * 0.55f, range, Battery);
        }

        /// <summary>Lags the beam behind the camera so it feels held rather than welded on.</summary>
        void UpdateSway()
        {
            if (_followTarget == null) return;

            var look = PlayerInput.Look;
            _swayX = Mathf.Lerp(_swayX, Mathf.Clamp(-look.x * 1.6f, -6f, 6f), Time.deltaTime * 6f);
            _swayY = Mathf.Lerp(_swayY, Mathf.Clamp(look.y * 1.6f, -6f, 6f), Time.deltaTime * 6f);

            transform.localRotation = Quaternion.Euler(_swayY, _swayX, 0f);
            transform.localPosition = new Vector3(0.16f, -0.12f, 0.1f);
        }

        /// <summary>Returns false when already full, so pickups are not wasted.</summary>
        public bool AddBattery(float amount)
        {
            if (Battery >= 0.999f) return false;
            Battery = Mathf.Clamp01(Battery + amount);
            return true;
        }

        public void ResetForNewRun()
        {
            Battery = 1f;
            SetOn(false);
        }
    }
}
