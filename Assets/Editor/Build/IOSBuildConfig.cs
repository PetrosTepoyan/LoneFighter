#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace LoneFighter.EditorTools.Build
{
    /// One-click iOS player-settings configurator.
    /// Menu: LoneFighter > Build > Apply iOS Settings
    ///
    /// Sets every PlayerSetting that controls how Unity emits the Xcode project,
    /// so that the only iOS-side work left is signing + archive + upload in Xcode.
    public static class IOSBuildConfig
    {
        public const string BundleIdentifier = "com.petrostepoyan.lonefighter";
        public const string CompanyName = "Petros Tepoyan";
        public const string ProductName = "LoneFighter";
        public const string MinimumIosVersion = "15.0";
        public const string DefaultBundleVersion = "0.1.0";
        public const int DefaultBuildNumber = 1;

        [MenuItem("LoneFighter/Build/Apply iOS Settings")]
        public static void Apply()
        {
            // Identity
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleIdentifier);

            // Versioning. CFBundleShortVersionString = bundleVersion. CFBundleVersion = iOS.buildNumber.
            // App Store Connect demands that buildNumber strictly increase across uploads — bump via the
            // "Bump iOS Build Number" menu item below before every TestFlight upload.
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1") {
                PlayerSettings.bundleVersion = DefaultBundleVersion;
            }
            if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0") {
                PlayerSettings.iOS.buildNumber = DefaultBuildNumber.ToString();
            }

            // Scripting backend + architecture — IL2CPP ARM64 is the only App-Store-allowed configuration.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1); // 1 = ARM64
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.iOS, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);

            // Deployment target + device family
            PlayerSettings.iOS.targetOSVersionString = MinimumIosVersion;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresPersistentWiFi = false;
            PlayerSettings.iOS.statusBarStyle = iOSStatusBarStyle.Default;
            PlayerSettings.iOS.hideHomeButton = true;

            // Orientation — portrait-locked (the game is designed for portrait; iPad also locks portrait).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false; // disabled so iPhone doesn't flip
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Splash / dark theme
            PlayerSettings.SplashScreen.show = false; // skip Unity splash if Plus/Pro license is active; harmless on Personal
            PlayerSettings.iOS.applicationDisplayName = ProductName;

            // Rendering
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.useHDRDisplay = false; // start safe; flip later if you ship HDR output

            // Misc
            PlayerSettings.muteOtherAudioSources = false;
            PlayerSettings.iOS.appInBackgroundBehavior = iOSAppInBackgroundBehavior.Suspend;

            // Switch active build target if not already iOS — required for the settings to actually persist
            // into the right serialized fields and for build scripts to act on them.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS) {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[iOS] Settings applied. Bundle: {BundleIdentifier}, " +
                      $"Version: {PlayerSettings.bundleVersion} (build {PlayerSettings.iOS.buildNumber}), " +
                      $"Min iOS: {MinimumIosVersion}, Device: iPhone+iPad, Portrait.");
        }

        [MenuItem("LoneFighter/Build/Bump iOS Build Number")]
        public static void BumpBuildNumber()
        {
            if (!int.TryParse(PlayerSettings.iOS.buildNumber, out int current)) current = 0;
            PlayerSettings.iOS.buildNumber = (current + 1).ToString();
            AssetDatabase.SaveAssets();
            Debug.Log($"[iOS] Build number bumped to {PlayerSettings.iOS.buildNumber} " +
                      $"(version {PlayerSettings.bundleVersion}).");
        }
    }
}
#endif
