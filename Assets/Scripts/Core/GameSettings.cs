using System;
using UnityEngine;

namespace Hollowmere.Core
{
    /// <summary>Player-facing options. Serialized to JSON via <see cref="SaveSystem"/>.</summary>
    [Serializable]
    public class GameSettings
    {
        public float mouseSensitivity = 2.0f;
        public float fieldOfView = 75f;
        public float masterVolume = 0.8f;
        public float brightness = 1.0f;
        public bool invertY = false;
        public bool headBob = true;
        public bool subtitles = true;
        public bool fullscreen = true;

        public static GameSettings Current { get; private set; } = new GameSettings();

        /// <summary>Ambient colour at brightness 1.0. <see cref="Apply"/> scales this.</summary>
        public static readonly Color BaseAmbient = new Color(0.035f, 0.038f, 0.05f);

        const string FileName = "settings.json";

        public static void Load()
        {
            Current = SaveSystem.Load<GameSettings>(FileName) ?? new GameSettings();
            Current.Apply();
        }

        public static void Save()
        {
            Current.Apply();
            SaveSystem.Save(FileName, Current);
        }

        /// <summary>Pushes settings into the engine. Called on load and on every change.</summary>
        public void Apply()
        {
            mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.1f, 10f);
            fieldOfView = Mathf.Clamp(fieldOfView, 60f, 110f);
            masterVolume = Mathf.Clamp01(masterVolume);
            brightness = Mathf.Clamp(brightness, 0.4f, 2.0f);

            AudioListener.volume = masterVolume;

            // Brightness lifts the ambient floor rather than washing the image out with a
            // white overlay - in a game this dark, the ambient term is what players are
            // actually reaching for when they say they cannot see anything.
            RenderSettings.ambientLight = BaseAmbient * brightness;

            if (Screen.fullScreen != fullscreen)
            {
                Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }
        }
    }
}
