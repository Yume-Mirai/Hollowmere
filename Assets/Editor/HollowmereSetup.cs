using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
// Deliberately no "using UnityEngine.Rendering": it also declares ShadowResolution,
// which would make the QualitySettings assignment below an ambiguous reference.

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
            PlayerSettings.bundleVersion = "0.1.0";

            // IL2CPP produces a faster, harder-to-decompile build and is what we want to
            // ship. Setting the enum always succeeds even when the toolchain is absent -
            // only the build fails, and confusingly - so probe the install directly.
            if (IsIl2CppInstalled())
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone, Il2CppCompilerConfiguration.Release);
            }
            else
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                Debug.LogWarning("[Hollowmere] IL2CPP toolchain not installed - falling back to Mono. " +
                                 "Add 'Windows Build Support (IL2CPP)' in Unity Hub before shipping.");
            }

            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);
        }

        /// <summary>
        /// True when the Windows IL2CPP variations are actually on disk. The editor ships
        /// Mono standalone support by default; IL2CPP is a separate Hub module.
        /// </summary>
        static bool IsIl2CppInstalled()
        {
            string variations = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines", "windowsstandalonesupport", "Variations");
            if (!Directory.Exists(variations)) return false;

            foreach (string dir in Directory.GetDirectories(variations))
            {
                if (Path.GetFileName(dir).IndexOf("il2cpp", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
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
