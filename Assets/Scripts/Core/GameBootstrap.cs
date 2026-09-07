using UnityEngine;

namespace Hollowmere.Core
{
    /// <summary>
    /// Entry point. The shipped scene is deliberately empty - everything is constructed
    /// here at runtime, which keeps the whole game in reviewable text files and makes
    /// scene merge conflicts impossible.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            // Guard against a hot reload or a second scene load stacking managers.
            if (GameManager.Instance != null) return;

            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;

            var go = new GameObject("[Game]");
            go.AddComponent<GameManager>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
