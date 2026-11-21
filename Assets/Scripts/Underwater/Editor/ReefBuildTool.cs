using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnderwaterReef.EditorTools
{
    /// <summary>
    /// Automated Build & One-Click Sharing Tool for Underwater Reef Rescue.
    /// Provides simple top-bar menu items to:
    /// 1. Build Standalone PC executable for instant presentation or streaming to Procus.
    /// 2. Build Android APK for direct installation onto smartphones for Procus VR.
    /// 3. Toggle Procus VR preview in the Editor.
    /// </summary>
    public static class ReefBuildTool
    {
        private static readonly string[] ScenePaths = new string[] { "Assets/Scenes/SampleScene.unity" };

        [MenuItem("Underwater Reef/Procus VR/Toggle Procus SBS Mode (Play Mode)")]
        public static void ToggleProcusVRInPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Procus VR", "Please enter Play Mode first, then select this or press F10.", "OK");
                return;
            }

            if (ProcusVRManager.Instance != null)
            {
                ProcusVRManager.Instance.ToggleProcusVR();
            }
        }

        [MenuItem("Underwater Reef/Build/Build Windows PC Demo (Exe)")]
        public static void BuildWindowsExecutable()
        {
            string buildFolder = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows_Demo");
            Directory.CreateDirectory(buildFolder);
            string exePath = Path.Combine(buildFolder, "UnderwaterReefRescue.exe");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = ScenePaths,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"<color=#00f2fe>[ReefBuildTool]</color> Starting Windows 64-bit build to {exePath}...");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"<color=#38ef7d>[ReefBuildTool]</color> Build SUCCESS: {summary.totalSize / (1024 * 1024)} MB");
                EditorUtility.RevealInFinder(exePath);
                EditorUtility.DisplayDialog("Build Complete", $"Windows executable ready at:\n{exePath}\n\nYou can zip this folder to share with your professor or stream to your Procus phone!", "Open Folder");
            }
            else
            {
                Debug.LogError($"<color=#ff4b72>[ReefBuildTool]</color> Build Failed: {summary.result}");
                EditorUtility.DisplayDialog("Build Error", "Build failed. Check the Unity Console for details.", "OK");
            }
        }

        [MenuItem("Underwater Reef/Build/Build Android APK (Procus VR)")]
        public static void BuildAndroidAPK()
        {
            // Check if Android Playback Engine is installed
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Android Support Required",
                    "The Android Build Support module is not yet installed in Unity Hub for this Unity version.\n\n" +
                    "To install it (1 click):\n" +
                    "1. Open Unity Hub\n" +
                    "2. Go to 'Installs' tab -> Click the gear icon next to Unity 6000.6.6f1\n" +
                    "3. Select 'Add Modules' -> Check 'Android Build Support' (with Android SDK & NDK Tools and OpenJDK)\n" +
                    "4. Click Install, then restart Unity!\n\n" +
                    "Alternatively, you can stream the game directly from PC to your phone in 2 minutes using Moonlight or Steam Link without building an APK!", "Got It");
                return;
            }

            string buildFolder = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android_Procus");
            Directory.CreateDirectory(buildFolder);
            string apkPath = Path.Combine(buildFolder, "ReefRescue_ProcusVR.apk");

#if UNITY_ANDROID
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersion)26;
#endif
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.underwater.reefrescue.procus");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = ScenePaths,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Debug.Log($"<color=#00f2fe>[ReefBuildTool]</color> Starting Android APK build to {apkPath}...");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"<color=#38ef7d>[ReefBuildTool]</color> Android APK SUCCESS: {summary.totalSize / (1024 * 1024)} MB");
                EditorUtility.RevealInFinder(apkPath);
                EditorUtility.DisplayDialog("APK Ready", $"Procus VR Android APK built successfully!\n\nLocation: {apkPath}\n\nSend this APK to your smartphone via WhatsApp, Google Drive, or USB to install and play in Procus VR!", "Open Folder");
            }
            else
            {
                Debug.LogError($"<color=#ff4b72>[ReefBuildTool]</color> Android Build Failed: {summary.result}");
                EditorUtility.DisplayDialog("Build Error", "Android APK build failed. Check Console for details.", "OK");
            }
        }
    }
}
