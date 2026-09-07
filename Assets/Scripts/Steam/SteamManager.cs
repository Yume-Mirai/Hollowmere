using UnityEngine;

namespace Hollowmere.Steam
{
    /// <summary>Achievement API names. These must match the Steamworks partner site exactly.</summary>
    public static class Achievements
    {
        public const string RestorePower = "ACH_RESTORE_POWER";
        public const string Escape       = "ACH_ESCAPE";
        public const string FastEscape   = "ACH_ESCAPE_UNDER_5";
        public const string NeverSeen    = "ACH_NEVER_SEEN";
    }

    /// <summary>
    /// Thin wrapper over Steamworks.NET, compiled out unless STEAMWORKS_NET is defined.
    ///
    /// The game is fully playable without the SDK - every call here degrades to a log
    /// line - so the project builds and runs on a machine that has never seen Steam.
    /// To enable:
    ///   1. Import Steamworks.NET (https://github.com/rlabrecque/Steamworks.NET) into Assets/Plugins.
    ///   2. Add STEAMWORKS_NET to Project Settings > Player > Scripting Define Symbols.
    ///   3. Put your real AppID in steam_appid.txt next to the executable.
    /// </summary>
    public static class SteamManager
    {
        /// <summary>480 is Valve's public test AppID (Spacewar). Replace before shipping.</summary>
        public const uint PlaceholderAppId = 480;

        public static bool Initialised { get; private set; }

        public static void Initialise()
        {
#if STEAMWORKS_NET
            if (Initialised) return;
            try
            {
                if (Steamworks.SteamAPI.RestartAppIfNecessary(new Steamworks.AppId_t(PlaceholderAppId)))
                {
                    Application.Quit();
                    return;
                }

                Initialised = Steamworks.SteamAPI.Init();
                Debug.Log(Initialised
                    ? "[Hollowmere] Steam API initialised."
                    : "[Hollowmere] Steam API failed to initialise - is the client running?");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Hollowmere] Steam init threw: " + e.Message);
                Initialised = false;
            }
#else
            Initialised = false;
            Debug.Log("[Hollowmere] Built without Steamworks. Achievements will be logged only.");
#endif
        }

        public static void Shutdown()
        {
#if STEAMWORKS_NET
            if (!Initialised) return;
            Steamworks.SteamAPI.Shutdown();
            Initialised = false;
#endif
        }

        /// <summary>Must be pumped every frame once the SDK is in; see SteamPump below.</summary>
        public static void RunCallbacks()
        {
#if STEAMWORKS_NET
            if (Initialised) Steamworks.SteamAPI.RunCallbacks();
#endif
        }

        public static void UnlockAchievement(string apiName)
        {
#if STEAMWORKS_NET
            if (!Initialised)
            {
                Debug.Log($"[Hollowmere] (offline) achievement: {apiName}");
                return;
            }
            Steamworks.SteamUserStats.SetAchievement(apiName);
            Steamworks.SteamUserStats.StoreStats();
#else
            Debug.Log($"[Hollowmere] achievement unlocked: {apiName}");
#endif
        }
    }

    /// <summary>
    /// Drives Steam callbacks and shuts the API down cleanly. Created automatically so
    /// nothing else has to remember to do it.
    /// </summary>
    public class SteamPump : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("[Steam]");
            go.AddComponent<SteamPump>();
            DontDestroyOnLoad(go);
        }

        void Update() => SteamManager.RunCallbacks();

        void OnApplicationQuit() => SteamManager.Shutdown();
    }
}
