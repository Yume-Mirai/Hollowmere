using Hollowmere.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowmere.UI
{
    /// <summary>
    /// Owns the canvas, the HUD and every menu screen. Screens are plain GameObjects
    /// toggled one at a time; with four of them a state enum beats a scene-per-menu
    /// setup and keeps the whole flow readable in one file.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        public static UIRoot Instance { get; private set; }

        public HUD Hud { get; private set; }

        GameObject _mainMenu;
        GameObject _pauseMenu;
        GameObject _settingsMenu;
        GameObject _endScreen;
        Text _endTitle;
        Text _endBody;

        GameObject _returnTo;   // which screen Settings goes back to

        public static UIRoot Create()
        {
            var canvas = UIBuilder.CreateCanvas("UI", 100);
            var root = canvas.gameObject.AddComponent<UIRoot>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            root.Build(canvas.transform);
            return root;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Build(Transform parent)
        {
            Hud = HUD.Create(parent);
            _mainMenu = BuildMainMenu(parent);
            _pauseMenu = BuildPauseMenu(parent);
            _settingsMenu = BuildSettingsMenu(parent);
            _endScreen = BuildEndScreen(parent);

            HideAll();
        }

        // --- screens ---------------------------------------------------------

        static GameObject BuildScreen(Transform parent, string name, float dim)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            UIBuilder.Stretch(go);

            var backdrop = UIBuilder.MakePanel(go.transform, "Backdrop", new Color(0.02f, 0.02f, 0.03f, dim));
            UIBuilder.Stretch(backdrop.gameObject);

            return go;
        }

        GameObject BuildMainMenu(Transform parent)
        {
            var screen = BuildScreen(parent, "MainMenu", 1f);

            var title = UIBuilder.MakeLabel(screen.transform, "Title", "HOLLOWMERE", 96, TextAnchor.MiddleCenter);
            UIBuilder.Place(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 240f), new Vector2(1200f, 120f));

            var tagline = UIBuilder.MakeLabel(screen.transform, "Tagline",
                "Find the fuses. Restore the power. Do not be heard.", 26, TextAnchor.MiddleCenter, UIBuilder.Dim);
            UIBuilder.Place(tagline.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 160f), new Vector2(1200f, 40f));

            UIBuilder.MakeButton(screen.transform, "New Run", new Vector2(0f, 40f), new Vector2(340f, 62f),
                () => GameManager.Instance?.StartNewRun());
            UIBuilder.MakeButton(screen.transform, "Settings", new Vector2(0f, -34f), new Vector2(340f, 62f),
                () => ShowSettings(_mainMenu));
            UIBuilder.MakeButton(screen.transform, "Quit", new Vector2(0f, -108f), new Vector2(340f, 62f),
                () => GameManager.Instance?.QuitApplication());

            var controls = UIBuilder.MakeLabel(screen.transform, "Controls",
                "WASD move   SHIFT sprint   CTRL crouch   F torch   E interact   RMB hold breath   ESC pause",
                20, TextAnchor.LowerCenter, UIBuilder.Dim);
            UIBuilder.Place(controls.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(1600f, 30f));

            return screen;
        }

        GameObject BuildPauseMenu(Transform parent)
        {
            var screen = BuildScreen(parent, "PauseMenu", 0.82f);

            var title = UIBuilder.MakeLabel(screen.transform, "Title", "PAUSED", 64, TextAnchor.MiddleCenter);
            UIBuilder.Place(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 190f), new Vector2(900f, 90f));

            UIBuilder.MakeButton(screen.transform, "Resume", new Vector2(0f, 50f), new Vector2(340f, 62f),
                () => GameManager.Instance?.ResumeGame());
            UIBuilder.MakeButton(screen.transform, "Settings", new Vector2(0f, -24f), new Vector2(340f, 62f),
                () => ShowSettings(_pauseMenu));
            UIBuilder.MakeButton(screen.transform, "Abandon Run", new Vector2(0f, -98f), new Vector2(340f, 62f),
                () => GameManager.Instance?.QuitToMenu());

            return screen;
        }

        GameObject BuildSettingsMenu(Transform parent)
        {
            var screen = BuildScreen(parent, "SettingsMenu", 0.94f);
            var settings = GameSettings.Current;

            var title = UIBuilder.MakeLabel(screen.transform, "Title", "SETTINGS", 56, TextAnchor.MiddleCenter);
            UIBuilder.Place(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 280f), new Vector2(900f, 80f));

            float y = 170f;
            AddSlider(screen.transform, "Mouse sensitivity", ref y, 0.5f, 8f, settings.mouseSensitivity,
                v => { GameSettings.Current.mouseSensitivity = v; GameSettings.Save(); });
            AddSlider(screen.transform, "Field of view", ref y, 60f, 110f, settings.fieldOfView,
                v => { GameSettings.Current.fieldOfView = v; GameSettings.Save(); });
            AddSlider(screen.transform, "Master volume", ref y, 0f, 1f, settings.masterVolume,
                v => { GameSettings.Current.masterVolume = v; GameSettings.Save(); });
            AddSlider(screen.transform, "Brightness", ref y, 0.4f, 2f, settings.brightness,
                v => { GameSettings.Current.brightness = v; GameSettings.Save(); });

            AddToggle(screen.transform, "Invert Y", ref y, () => GameSettings.Current.invertY,
                v => { GameSettings.Current.invertY = v; GameSettings.Save(); });
            AddToggle(screen.transform, "Head bob", ref y, () => GameSettings.Current.headBob,
                v => { GameSettings.Current.headBob = v; GameSettings.Save(); });
            AddToggle(screen.transform, "Fullscreen", ref y, () => GameSettings.Current.fullscreen,
                v => { GameSettings.Current.fullscreen = v; GameSettings.Save(); });

            UIBuilder.MakeButton(screen.transform, "Back", new Vector2(0f, y - 40f), new Vector2(240f, 56f), CloseSettings);

            return screen;
        }

        void AddSlider(Transform parent, string label, ref float y, float min, float max, float value,
            System.Action<float> onChanged)
        {
            var text = UIBuilder.MakeLabel(parent, "Label_" + label, label, 24, TextAnchor.MiddleRight, UIBuilder.Dim);
            UIBuilder.Place(text.gameObject, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-30f, y), new Vector2(340f, 34f));

            var valueText = UIBuilder.MakeLabel(parent, "Value_" + label, Format(value), 22, TextAnchor.MiddleLeft, UIBuilder.Ink);
            UIBuilder.Place(valueText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(390f, y), new Vector2(120f, 34f));

            UIBuilder.MakeSlider(parent, new Vector2(190f, y), new Vector2(360f, 22f), min, max, value, v =>
            {
                valueText.text = Format(v);
                onChanged(v);
            });

            y -= 56f;
        }

        static string Format(float v) => v >= 10f ? v.ToString("F0") : v.ToString("F2");

        void AddToggle(Transform parent, string label, ref float y, System.Func<bool> get, System.Action<bool> set)
        {
            var text = UIBuilder.MakeLabel(parent, "Label_" + label, label, 24, TextAnchor.MiddleRight, UIBuilder.Dim);
            UIBuilder.Place(text.gameObject, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-30f, y), new Vector2(340f, 34f));

            Button button = null;
            button = UIBuilder.MakeButton(parent, get() ? "ON" : "OFF", new Vector2(190f, y), new Vector2(120f, 40f), () =>
            {
                bool next = !get();
                set(next);
                var t = button.GetComponentInChildren<Text>();
                if (t != null) t.text = next ? "ON" : "OFF";
            });

            y -= 56f;
        }

        GameObject BuildEndScreen(Transform parent)
        {
            var screen = BuildScreen(parent, "EndScreen", 0.93f);

            _endTitle = UIBuilder.MakeLabel(screen.transform, "Title", "", 82, TextAnchor.MiddleCenter);
            UIBuilder.Place(_endTitle.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 190f), new Vector2(1200f, 110f));

            _endBody = UIBuilder.MakeLabel(screen.transform, "Body", "", 26, TextAnchor.MiddleCenter, UIBuilder.Dim);
            UIBuilder.Place(_endBody.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 90f), new Vector2(1200f, 90f));

            UIBuilder.MakeButton(screen.transform, "Try Again", new Vector2(0f, -20f), new Vector2(340f, 62f),
                () => GameManager.Instance?.StartNewRun());
            UIBuilder.MakeButton(screen.transform, "Main Menu", new Vector2(0f, -94f), new Vector2(340f, 62f),
                () => GameManager.Instance?.QuitToMenu());

            return screen;
        }

        // --- navigation ------------------------------------------------------

        public void HideAll()
        {
            _mainMenu.SetActive(false);
            _pauseMenu.SetActive(false);
            _settingsMenu.SetActive(false);
            _endScreen.SetActive(false);
        }

        public void ShowMainMenu()
        {
            HideAll();
            _mainMenu.SetActive(true);
        }

        public void ShowPause()
        {
            HideAll();
            _pauseMenu.SetActive(true);
        }

        public void ShowGameplay() => HideAll();

        public void ShowEnd(bool escaped, string body)
        {
            HideAll();
            _endTitle.text = escaped ? "ESCAPED" : "TAKEN";
            _endTitle.color = escaped ? new Color(0.55f, 0.9f, 0.6f) : UIBuilder.Danger;
            _endBody.text = body;
            _endScreen.SetActive(true);
        }

        void ShowSettings(GameObject returnTo)
        {
            _returnTo = returnTo;
            HideAll();
            _settingsMenu.SetActive(true);
        }

        void CloseSettings()
        {
            HideAll();
            (_returnTo != null ? _returnTo : _mainMenu).SetActive(true);
        }
    }
}
