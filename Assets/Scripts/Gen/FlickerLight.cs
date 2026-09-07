using UnityEngine;

namespace Hollowmere.Gen
{
    /// <summary>
    /// Drives a light with layered noise so it reads as failing fluorescent rather than
    /// as a sine wave. "Broken" lights spend most of their time dark and stutter on.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        Light _light;
        float _baseIntensity = 1f;
        float _phase;
        bool _broken;
        float _nextStutter;

        void Awake()
        {
            _light = GetComponent<Light>();
            if (_baseIntensity <= 0f) _baseIntensity = _light.intensity;
        }

        public void Configure(float baseIntensity, float phase, bool broken)
        {
            _baseIntensity = baseIntensity;
            _phase = phase;
            _broken = broken;
            if (_light != null) _light.intensity = baseIntensity;
        }

        void Update()
        {
            if (_light == null) return;
            float t = Time.time + _phase;

            if (_broken)
            {
                // Mostly off, with short violent bursts at irregular intervals.
                if (t > _nextStutter)
                {
                    _nextStutter = t + Random.Range(0.4f, 4.5f);
                    _light.intensity = _baseIntensity * Random.Range(0.8f, 1.6f);
                }
                else
                {
                    _light.intensity = Mathf.Lerp(_light.intensity, _baseIntensity * 0.04f, Time.deltaTime * 14f);
                }
                return;
            }

            // Two incommensurate noise samples keep the pattern from ever repeating audibly.
            float slow = Mathf.PerlinNoise(t * 0.7f, 0f);
            float fast = Mathf.PerlinNoise(t * 11f, 3.3f);
            float amount = Mathf.Lerp(0.86f, 1.06f, slow * 0.65f + fast * 0.35f);
            _light.intensity = _baseIntensity * amount;
        }
    }
}
