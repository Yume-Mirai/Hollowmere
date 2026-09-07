using System.Collections.Generic;
using UnityEngine;

namespace Hollowmere.Gen
{
    /// <summary>
    /// Builds every material the game uses from code - noise textures on the Standard
    /// shader. No imported art, so the repo stays text-only and the look is tunable
    /// by numbers rather than by a DCC round-trip.
    /// </summary>
    public static class ProceduralMaterials
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        static Shader StandardShader
        {
            get
            {
                var s = Shader.Find("Standard");
                if (s == null)
                {
                    Debug.LogError("[Hollowmere] Standard shader not found. Run Hollowmere > Configure Project.");
                    s = Shader.Find("Diffuse");
                }
                return s;
            }
        }

        public static Material Concrete => Get("concrete", () => Build(new Color(0.30f, 0.30f, 0.32f), 0.55f, 0.0f, 24f, 0.35f));
        public static Material Floor    => Get("floor",    () => Build(new Color(0.20f, 0.19f, 0.18f), 0.42f, 0.0f, 18f, 0.45f));
        public static Material Ceiling  => Get("ceiling",  () => Build(new Color(0.13f, 0.13f, 0.14f), 0.30f, 0.0f, 20f, 0.30f));
        public static Material RustMetal=> Get("rust",     () => Build(new Color(0.34f, 0.20f, 0.13f), 0.35f, 0.65f, 30f, 0.55f));
        public static Material Locker   => Get("locker",   () => Build(new Color(0.22f, 0.26f, 0.26f), 0.45f, 0.70f, 12f, 0.20f));

        public static Material Emissive(Color color, float intensity = 2f)
        {
            string key = $"emis_{ColorUtility.ToHtmlStringRGB(color)}_{intensity:F2}";
            return Get(key, () =>
            {
                var m = new Material(StandardShader);
                m.color = color;
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", color * intensity);
                return m;
            });
        }

        public static Material Flat(Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            string key = $"flat_{ColorUtility.ToHtmlStringRGBA(color)}_{smoothness:F2}_{metallic:F2}";
            return Get(key, () =>
            {
                var m = new Material(StandardShader);
                m.color = color;
                m.SetFloat("_Glossiness", smoothness);
                m.SetFloat("_Metallic", metallic);
                return m;
            });
        }

        static Material Get(string key, System.Func<Material> factory)
        {
            if (Cache.TryGetValue(key, out var existing) && existing != null) return existing;
            var created = factory();
            created.name = "Proc_" + key;
            Cache[key] = created;
            return created;
        }

        static Material Build(Color tint, float smoothness, float metallic, float noiseScale, float contrast)
        {
            var m = new Material(StandardShader);
            m.color = tint;
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.mainTexture = NoiseTexture(256, noiseScale, contrast);
            m.mainTextureScale = new Vector2(2f, 2f);
            return m;
        }

        /// <summary>Tiling fbm noise, used as a grunge albedo so flat surfaces read as surfaces.</summary>
        static Texture2D NoiseTexture(int size, float scale, float contrast)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float v = 0f, amp = 0.5f, freq = scale / size;
                    for (int octave = 0; octave < 4; octave++)
                    {
                        // Sampling with an offset keeps octaves from lining up into a grid.
                        v += Mathf.PerlinNoise(x * freq + octave * 13.7f, y * freq + octave * 7.3f) * amp;
                        amp *= 0.5f;
                        freq *= 2.07f;
                    }
                    v = Mathf.Lerp(1f - contrast, 1f, Mathf.Clamp01(v));
                    byte b = (byte)(Mathf.Clamp01(v) * 255f);
                    pixels[y * size + x] = new Color32(b, b, b, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(true);
            return tex;
        }
    }
}
