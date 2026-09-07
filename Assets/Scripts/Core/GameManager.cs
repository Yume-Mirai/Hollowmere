using Hollowmere.Gen;
using Hollowmere.Horror;
using Hollowmere.Interact;
using Hollowmere.Player;
using Hollowmere.Steam;
using Hollowmere.UI;
using UnityEngine;

namespace Hollowmere.Core
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        Ended
    }

    /// <summary>
    /// Run lifecycle and objective tracking. Owns the level, the player and the entity,
    /// and tears all three down between runs - every run is a fresh seed, so nothing is
    /// reused across them.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Run")]
        public int fusesRequired = 4;

        public GameState State { get; private set; } = GameState.MainMenu;
        public bool IsPlaying => State == GameState.Playing;

        public int FusesCarried { get; private set; }
        public int FusesInstalled { get; private set; }
        public int FusesRequired => fusesRequired;
        public bool PowerRestored => FusesInstalled >= fusesRequired;
        public float RunTime { get; private set; }

        public LevelData Level { get; private set; }

        UIRoot _ui;
        PlayerController _player;
        Entity _entity;
        Transform _itemRoot;
        AudioSource _ambience;

        public string ObjectiveText
        {
            get
            {
                if (PowerRestored) return "Power restored - get to the exit door";
                if (FusesCarried > 0) return $"Fuses {FusesInstalled}/{fusesRequired} installed - carrying {FusesCarried}";
                return $"Fuses {FusesInstalled}/{fusesRequired} installed - find another";
            }
        }

        // --- lifecycle -------------------------------------------------------

        void Awake()
        {
            Instance = this;
            GameSettings.Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            ConfigureAtmosphere();

            _ui = UIRoot.Create();
            _ui.ShowMainMenu();

            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.clip = ProceduralAudio.Drone;
            _ambience.loop = true;
            _ambience.volume = 0.35f;
            _ambience.spatialBlend = 0f;
            _ambience.Play();

            SetState(GameState.MainMenu);
            SteamManager.Initialise();
        }

        /// <summary>Scene-level render settings, applied from code since the scene is empty.</summary>
        static void ConfigureAtmosphere()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.022f, 0.03f);
            RenderSettings.fogDensity = 0.055f;
            RenderSettings.skybox = null;

            // Sets ambientLight from BaseAmbient * the player's brightness preference.
            GameSettings.Current.Apply();
        }

        void Update()
        {
            if (State == GameState.Playing)
            {
                RunTime += Time.deltaTime;
                if (PlayerInput.PausePressed) PauseGame();
            }
            else if (State == GameState.Paused)
            {
                if (PlayerInput.PausePressed) ResumeGame();
            }
        }

        // --- state -----------------------------------------------------------

        void SetState(GameState state)
        {
            State = state;

            bool playing = state == GameState.Playing;
            PlayerInput.Suspended = !playing;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
            Time.timeScale = state == GameState.Paused ? 0f : 1f;

            if (_ambience != null) _ambience.volume = playing ? 0.35f : 0.15f;
        }

        public void StartNewRun()
        {
            TeardownRun();

            int seed = Random.Range(int.MinValue, int.MaxValue);
            Level = LevelGenerator.Generate(seed, fusesRequired);

            FusesCarried = 0;
            FusesInstalled = 0;
            RunTime = 0f;
            HidingSpot.ClearOccupant();
            NoiseSystem.Clear();

            SpawnItems();

            _player = PlayerController.Create(Level.PlayerSpawn);
            _entity = Entity.Spawn(Level);

            _ui.ShowGameplay();
            SetState(GameState.Playing);
            Notify("Find the fuses");
        }

        void SpawnItems()
        {
            _itemRoot = new GameObject("Items").transform;

            for (int i = 0; i < Level.FuseSpots.Count; i++)
                FusePickup.Spawn(Level.FuseSpots[i], _itemRoot);

            for (int i = 0; i < Level.BatterySpots.Count; i++)
                BatteryPickup.Spawn(Level.BatterySpots[i], _itemRoot);

            for (int i = 0; i < Level.HidingSpots.Count; i++)
                HidingSpot.Spawn(Level.HidingSpots[i], _itemRoot);

            ExitDoor.Spawn(Level.ExitPosition, _itemRoot);

            // Put the fuse box a few metres off the exit so the last leg is a real walk.
            var boxCell = Level.Grid.NearestWalkable(Level.ExitPosition + new Vector3(LevelGenerator.CellSize * 2f, 0f, 0f));
            FuseBox.Spawn(Level.Grid.CellToWorld(boxCell.x, boxCell.y), _itemRoot);
        }

        void TeardownRun()
        {
            if (Level != null && Level.Root != null) Destroy(Level.Root.gameObject);
            if (_itemRoot != null) Destroy(_itemRoot.gameObject);
            if (_player != null) Destroy(_player.gameObject);
            if (_entity != null) Destroy(_entity.gameObject);

            Level = null;
            _player = null;
            _entity = null;
            _itemRoot = null;
        }

        public void PauseGame()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Paused);
            _ui.ShowPause();
        }

        public void ResumeGame()
        {
            if (State != GameState.Paused) return;
            _ui.ShowGameplay();
            SetState(GameState.Playing);
        }

        public void QuitToMenu()
        {
            TeardownRun();
            HidingSpot.ClearOccupant();
            SetState(GameState.MainMenu);
            _ui.ShowMainMenu();
        }

        public void QuitApplication()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // --- objectives ------------------------------------------------------

        public void CollectFuse()
        {
            FusesCarried++;
            Notify($"Fuse taken ({FusesInstalled + FusesCarried}/{fusesRequired} found)");
        }

        public void InstallFuse()
        {
            if (FusesCarried <= 0 || PowerRestored) return;

            FusesCarried--;
            FusesInstalled++;

            if (PowerRestored)
            {
                Notify("Power restored - the door is live");
                SteamManager.UnlockAchievement(Achievements.RestorePower);
            }
            else
            {
                Notify($"Fuse installed ({FusesInstalled}/{fusesRequired})");
            }
        }

        public void OnPlayerCaught()
        {
            if (State != GameState.Playing) return;

            _ui.Hud?.FlashDamage();
            AudioSource.PlayClipAtPoint(ProceduralAudio.Scream, _player.transform.position, 1f);

            EndRun(false, $"You lasted {FormatTime(RunTime)} and installed {FusesInstalled} of {fusesRequired} fuses.");
        }

        public void OnEscaped()
        {
            if (State != GameState.Playing) return;

            SteamManager.UnlockAchievement(Achievements.Escape);
            if (RunTime <= 300f) SteamManager.UnlockAchievement(Achievements.FastEscape);

            EndRun(true, $"Out in {FormatTime(RunTime)}.");
        }

        void EndRun(bool escaped, string body)
        {
            SetState(GameState.Ended);

            if (_player != null) _player.Frozen = true;
            _ui.ShowEnd(escaped, body);
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }

        public void Notify(string message) => _ui?.Hud?.Notify(message);
    }
}
