using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollowmere.EditorTools
{
    /// <summary>
    /// Applies every project setting the game needs, and creates the boot scene.
    /// Runs once automatically the first time the project is opened so the game is
    /// playable without anyone clicking through the Unity UI. Safe to re-run.
    /// </summary>
    [InitializeOnLoad]
    public static class HollowmereSetup
    {
        public const string ProductName = "Hollowmere";
        public const string CompanyName = "Nolan";
        public const string MainScenePath = "Assets/Scenes/Main.unity";

        static HollowmereSetup()
        {
            // Defer: the asset database and scene manager are not reliable inside a
            // static constructor during domain reload.
            EditorApplication.delayCall += AutoRunOnce;
        }

        static void AutoRunOnce()
        {
            // Cheap idempotency check - if the product name already took, we are configured.
            bool configured = PlayerSettings.productName == ProductName;
            bool sceneExists = File.Exists(MainScenePath);
            if (configured && sceneExists) return;

            Configure();
        }

        [MenuItem("Hollowmere/Configure Project", priority = 0)]
        public static void Configure()
        {
            ApplyPlayerSettings();
            ApplyQualitySettings();
            EnsureAlwaysIncludedShaders();
            EnsureTags();
            EnsureMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Hollowmere] Project configured. Press Play.");
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = false;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.captureSingleScreen = false;
            PlayerSettings.bundleVersion = "0.1.0";

            try
            {
                // IL2CPP produces a faster, harder-to-decompile build for shipping.
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);
            }
            catch (System.Exception e)
            {
                // Missing IL2CPP module - the game still runs and builds under Mono.
                Debug.LogWarning("[Hollowmere] Could not set IL2CPP backend (module may not be installed): " + e.Message);
            }
        }

        static void ApplyQualitySettings()
        {
            // Horror leans on shadows. Push them further than Unity's defaults.
            QualitySettings.shadowDistance = 60f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.pixelLightCount = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.vSyncCount = 1;
        }

        /// <summary>
        /// Every material in this game is created at runtime with Shader.Find, so the
        /// shaders must be force-included or they get stripped from the build and the
        /// game ships as a field of magenta.
        /// </summary>
        static void EnsureAlwaysIncludedShaders()
        {
            string[] wanted = { "Standard", "Sprites/Default", "UI/Default", "Legacy Shaders/Particles/Additive" };

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;

            var so = new SerializedObject(assets[0]);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;

            foreach (string name in wanted)
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;

                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
                }
                if (present) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            so.ApplyModifiedProperties();
        }

        static void EnsureTags()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;

            var so = new SerializedObject(assets[0]);
            var tags = so.FindProperty("tags");
            if (tags == null) return;

            foreach (string tag in new[] { "Player", "Entity", "Interactable" })
            {
                bool present = false;
                for (int i = 0; i < tags.arraySize; i++)
                {
                    if (tags.GetArrayElementAtIndex(i).stringValue == tag) { present = true; break; }
                }
                // "Player" is a Unity builtin and is not in this list; adding it would duplicate.
                if (present || tag == "Player") continue;

                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// The scene is deliberately empty - GameBootstrap builds the entire game from
        /// code at runtime. This keeps the whole project diffable text with no binary
        /// scene assets to merge.
        /// </summary>
        static void EnsureMainScene()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");

            if (!File.Exists(MainScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, MainScenePath);
                AssetDatabase.Refresh();
            }

            bool inBuild = false;
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == MainScenePath) { inBuild = true; break; }
            }
            if (!inBuild)
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            }
        }
    }
}
