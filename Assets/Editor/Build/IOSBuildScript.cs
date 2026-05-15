#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LoneFighter.EditorTools.Build
{
    /// One-click iOS build that:
    ///   1) Applies iOS settings (IOSBuildConfig)
    ///   2) Bumps the build number
    ///   3) Ensures the privacy manifest exists
    ///   4) Builds the Xcode project to Build/iOS/
    ///
    /// Menu: LoneFighter > Build > Build iOS Xcode Project
    /// CLI:  -executeMethod LoneFighter.EditorTools.Build.IOSBuildScript.BuildFromCli
    public static class IOSBuildScript
    {
        public const string OutputDir = "Build/iOS";

        [MenuItem("LoneFighter/Build/Build iOS Xcode Project")]
        public static void BuildXcodeProject()
        {
            var report = Build(bumpBuildNumber: true);
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(Path.Combine(OutputDir, "Unity-iPhone.xcworkspace"));
            }
        }

        [MenuItem("LoneFighter/Build/Build iOS (no bump)")]
        public static void BuildXcodeProjectNoBump() => Build(bumpBuildNumber: false);

        // CLI entry point. Caller can pass +arg buildNumber=42 or +arg version=0.2.0
        public static void BuildFromCli()
        {
            string version = null;
            string buildNumber = null;
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("version=")) version = arg.Substring("version=".Length);
                else if (arg.StartsWith("buildNumber=")) buildNumber = arg.Substring("buildNumber=".Length);
            }

            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            if (!string.IsNullOrEmpty(buildNumber)) PlayerSettings.iOS.buildNumber = buildNumber;

            var report = Build(bumpBuildNumber: string.IsNullOrEmpty(buildNumber));
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport Build(bool bumpBuildNumber)
        {
            // Always apply iOS settings before building — guarantees PlayerSettings match expected config.
            IOSBuildConfig.Apply();
            if (bumpBuildNumber) IOSBuildConfig.BumpBuildNumber();

            // Privacy manifest must exist or App Store Connect will reject the upload.
            if (!File.Exists("Assets/Resources/Privacy/PrivacyInfo.xcprivacy"))
            {
                PrivacyManifestGenerator.Generate();
            }

            // Collect enabled scenes from the build settings
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.enabled) scenes.Add(s.path);
            }

            if (scenes.Count == 0)
            {
                Debug.LogError("[iOS] No scenes in Build Settings. " +
                               "Open File > Build Profiles and add MainMenu, Game, GameOver.");
                return null;
            }

            Directory.CreateDirectory(OutputDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = OutputDir,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None
            };

            Debug.Log($"[iOS] Building to {OutputDir} | version {PlayerSettings.bundleVersion} | " +
                      $"build {PlayerSettings.iOS.buildNumber} | scenes: {scenes.Count}");

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[iOS] Build {summary.result} in {summary.totalTime} | size {summary.totalSize / 1024 / 1024} MB");
            return report;
        }
    }
}
#endif
