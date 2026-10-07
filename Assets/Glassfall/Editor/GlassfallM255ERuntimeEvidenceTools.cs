using System;
using System.IO;
using System.Linq;
using Glassfall.Diagnostics;
using Glassfall.Gameplay.Run;
using Glassfall.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Glassfall.Editor
{
    public static class GlassfallM255ERuntimeEvidenceTools
    {
        private const string PrototypeScenePath =
            "Assets/Glassfall/Scenes/Prototype.unity";
        private const string BuildRoot =
            "Builds/M25_5E";
        private const string DirtyGlassShaderPath =
            "Assets/Glassfall/Presentation/DirtyGlass.shader";
        private const string WetnessShaderPath =
            "Assets/Glassfall/Presentation/WetnessTrail.shader";

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/25.5E - Apply Runtime Evidence Checkpoint")]
        public static void ApplyRuntimeEvidenceCheckpoint()
        {
            if (EditorSceneManager.GetActiveScene().path !=
                PrototypeScenePath)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - M25.5E",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity " +
                    "sahnesini aç.",
                    "Tamam");
                return;
            }

            if (!TryConfigureRuntimeEvidenceProbe(out string error))
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - M25.5E",
                    error,
                    "Tamam");
                return;
            }

            EditorUtility.DisplayDialog(
                "Glassfall - M25.5E Runtime Evidence",
                "Runtime evidence probe MicroRunController objesine " +
                "bağlandı.\n\n" +
                "Ctrl+S ile sahneyi kaydet.\n\n" +
                "Development Build kontrolleri:\n" +
                "F8  = diagnostic overlay aç/kapat\n" +
                "F9  = manuel evidence snapshot\n" +
                "F10 = aynı process içinde scene reload / yeni run\n\n" +
                "Overlay varsayılan olarak kapalıdır. Temiz profiler " +
                "ölçümü için kapalı tut.",
                "Tamam");
        }

        public static void ApplyRuntimeEvidenceCheckpointBatch()
        {
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.OpenScene(
                    PrototypeScenePath,
                    OpenSceneMode.Single);
            if (!TryConfigureRuntimeEvidenceProbe(out string error))
                throw new InvalidOperationException(error);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Prototype scene could not be saved after M25.5E setup.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "[Glassfall M25.5E] Runtime evidence probe is wired and " +
                "Prototype.unity was saved.");
        }

        private static bool TryConfigureRuntimeEvidenceProbe(
            out string error)
        {
            MicroRunController run =
                UnityEngine.Object.FindAnyObjectByType<MicroRunController>();
            if (run == null)
            {
                error = "MicroRunController bulunamadı.";
                return false;
            }

            RuntimeEvidenceProbe probe =
                run.GetComponent<RuntimeEvidenceProbe>();
            if (probe == null)
            {
                probe = Undo.AddComponent<RuntimeEvidenceProbe>(
                    run.gameObject);
            }

            Undo.RecordObject(
                probe,
                "Configure M25.5E Runtime Evidence Probe");
            probe.Configure(run);
            EditorUtility.SetDirty(probe);

            if (!EnsureRuntimeShaderReferences(out error))
                return false;

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = run.gameObject;
            error = string.Empty;
            return true;
        }

        private static bool EnsureRuntimeShaderReferences(out string error)
        {
            Shader dirtyGlassShader =
                AssetDatabase.LoadAssetAtPath<Shader>(DirtyGlassShaderPath);
            Shader wetnessShader =
                AssetDatabase.LoadAssetAtPath<Shader>(WetnessShaderPath);
            if (dirtyGlassShader == null || wetnessShader == null)
            {
                error =
                    "Required runtime presentation shaders could not be " +
                    "loaded. Check DirtyGlass.shader and WetnessTrail.shader.";
                return false;
            }

            SurfaceZoneVisual[] surfaceVisuals =
                UnityEngine.Object.FindObjectsByType<SurfaceZoneVisual>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < surfaceVisuals.Length; i++)
            {
                AssignObjectReferenceIfNeeded(
                    surfaceVisuals[i],
                    "dirtyGlassShader",
                    dirtyGlassShader);
            }

            WetnessVisualRenderer[] wetnessVisuals =
                UnityEngine.Object.FindObjectsByType<WetnessVisualRenderer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < wetnessVisuals.Length; i++)
            {
                AssignObjectReferenceIfNeeded(
                    wetnessVisuals[i],
                    "wetnessShader",
                    wetnessShader);
            }

            error = string.Empty;
            return true;
        }

        private static void AssignObjectReferenceIfNeeded(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value)
                return;

            Undo.RecordObject(target, "Bind runtime shader " + propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/25.5E - Build Development Player")]
        public static void BuildDevelopmentPlayer()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - M25.5E Build",
                    "Build Settings içinde enabled scene bulunamadı.",
                    "Tamam");
                return;
            }

            BuildTarget target =
                EditorUserBuildSettings.activeBuildTarget;
            string location = ResolveBuildLocation(target);
            if (string.IsNullOrEmpty(location))
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - M25.5E Build",
                    "M25.5E helper şu anda yalnız standalone desktop " +
                    "build target'larını destekliyor. Active target: " +
                    target,
                    "Tamam");
                return;
            }

            string directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = target,
                options =
                    BuildOptions.Development |
                    BuildOptions.ConnectWithProfiler
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(location);
                EditorUtility.DisplayDialog(
                    "Glassfall - M25.5E Build",
                    "Development Build tamamlandı.\n\n" +
                    location + "\n\n" +
                    "Build Connect With Profiler ile üretildi. " +
                    "Standalone player ayrıca kendi binary .raw profiler " +
                    "capture'ını ve CSV/metadata evidence dosyalarını " +
                    "persistentDataPath altında M25_5E_Evidence içine " +
                    "yazacaktır.",
                    "Tamam");
                return;
            }

            EditorUtility.DisplayDialog(
                "Glassfall - M25.5E Build FAILED",
                "Build sonucu: " + summary.result +
                "\nError count: " + summary.totalErrors +
                "\nWarning count: " + summary.totalWarnings +
                "\n\nConsole ve Build Report'u kontrol et.",
                "Tamam");
        }

        public static void BuildDevelopmentPlayerBatch()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException(
                    "Build Settings contains no enabled scene.");
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string location = ResolveBuildLocation(target);
            if (string.IsNullOrEmpty(location))
            {
                throw new InvalidOperationException(
                    "M25.5E development build only supports standalone " +
                    "desktop targets. Active target: " + target);
            }

            string directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = location,
                    target = target,
                    options =
                        BuildOptions.Development |
                        BuildOptions.ConnectWithProfiler
                });

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "M25.5E development build failed: " +
                    summary.result +
                    ", errors=" + summary.totalErrors +
                    ", warnings=" + summary.totalWarnings);
            }

            Debug.Log(
                "[Glassfall M25.5E] Development build completed: " +
                location);
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/25.5E - Open Evidence Folder")]
        public static void OpenEvidenceFolder()
        {
            string path = Path.Combine(
                Application.persistentDataPath,
                "M25_5E_Evidence");
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        private static string ResolveBuildLocation(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(
                        BuildRoot,
                        "Windows",
                        "Glassfall_M25_5E.exe");

                case BuildTarget.StandaloneLinux64:
                    return Path.Combine(
                        BuildRoot,
                        "Linux",
                        "Glassfall_M25_5E");

                case BuildTarget.StandaloneOSX:
                    return Path.Combine(
                        BuildRoot,
                        "macOS",
                        "Glassfall_M25_5E.app");

                default:
                    return null;
            }
        }
    }
}
