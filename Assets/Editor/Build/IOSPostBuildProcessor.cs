#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace LoneFighter.EditorTools.Build
{
    /// Runs automatically after Unity emits the Xcode project. Patches Info.plist
    /// and project.pbxproj with the App-Store-required and 120Hz-required keys
    /// that Unity does not set itself.
    public static class IOSPostBuildProcessor
    {
        [PostProcessBuild(999)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            PatchInfoPlist(pathToBuiltProject);
            PatchXcodeProject(pathToBuiltProject);
            EnsurePrivacyManifest(pathToBuiltProject);

            Debug.Log($"[iOS] Post-build patches applied to {pathToBuiltProject}");
        }

        static void PatchInfoPlist(string projectPath)
        {
            string plistPath = Path.Combine(projectPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

            // Unlock 120Hz on iPhone ProMotion devices. Without this key, iOS hard-caps every game
            // to 60fps regardless of Application.targetFrameRate. THIS is the iOS-side gate for the
            // "120Hz fun and expensive" feel.
            root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);

            // Encryption export compliance — answer "no" up-front so TestFlight processing doesn't
            // ask the question on every build. Set this to false ONLY if the app uses standard
            // HTTPS / TLS provided by the system and no custom crypto. LoneFighter qualifies.
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            // Portrait-only, immersive game feel
            root.SetBoolean("UIRequiresFullScreen", true);
            root.SetBoolean("UIStatusBarHidden", true);
            root.SetString("UIStatusBarStyle", "UIStatusBarStyleDefault");

            // Allowed orientations (iPhone + iPad). Portrait-locked across both families.
            ReplaceArrayOfStrings(root, "UISupportedInterfaceOrientations",
                "UIInterfaceOrientationPortrait");
            ReplaceArrayOfStrings(root, "UISupportedInterfaceOrientations~ipad",
                "UIInterfaceOrientationPortrait");

            // Required for App Store screenshots / category metadata. Adjust LSApplicationCategoryType
            // later in App Store Connect; this just declares it as a public.app-category.action-games.
            root.SetString("LSApplicationCategoryType", "public.app-category.action-games");

            // Game-friendly: keep accelerometer off, no background audio, no location.
            // If you add Game Center later, set GKGameCenterCapability here.

            // High-refresh hint for Metal (belt-and-suspenders alongside CADisable... key)
            root.SetBoolean("UIApplicationSupportsIndirectInputEvents", true);

            plist.WriteToFile(plistPath);
        }

        static void PatchXcodeProject(string projectPath)
        {
            string pbxPath = PBXProject.GetPBXProjectPath(projectPath);
            var pbx = new PBXProject();
            pbx.ReadFromFile(pbxPath);

            string mainTarget = pbx.GetUnityMainTargetGuid();
            string frameworkTarget = pbx.GetUnityFrameworkTargetGuid();

            foreach (var t in new[] { mainTarget, frameworkTarget })
            {
                // Bitcode was removed in Xcode 14+; leaving it enabled fails the build.
                pbx.SetBuildProperty(t, "ENABLE_BITCODE", "NO");

                // Pin deployment target — Unity sets this from PlayerSettings, but post-build
                // pins it everywhere it matters (per-target overrides ignored otherwise).
                pbx.SetBuildProperty(t, "IPHONEOS_DEPLOYMENT_TARGET",
                    IOSBuildConfig.MinimumIosVersion);

                // Architectures: ARM64 only (App Store requirement)
                pbx.SetBuildProperty(t, "ARCHS", "arm64");
                pbx.SetBuildProperty(t, "VALID_ARCHS", "arm64");
                pbx.SetBuildProperty(t, "EXCLUDED_ARCHS[sdk=iphonesimulator*]", "arm64");

                // Strip unused symbols to shrink the IPA
                pbx.SetBuildProperty(t, "DEPLOYMENT_POSTPROCESSING", "YES");
                pbx.SetBuildProperty(t, "STRIP_INSTALLED_PRODUCT", "YES");
                pbx.SetBuildProperty(t, "STRIP_STYLE", "all");
            }

            // dSYM upload to App Store Connect for symbolicated crash reports
            pbx.SetBuildProperty(mainTarget, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

            pbx.WriteToFile(pbxPath);
        }

        /// Apple has required PrivacyInfo.xcprivacy for new App Store submissions since May 2024.
        /// If the file at Assets/Resources/Privacy/PrivacyInfo.xcprivacy exists, Unity will copy it
        /// into the Xcode project automatically. This method is a safety net: it verifies the file
        /// shipped, and logs a clear error if missing.
        static void EnsurePrivacyManifest(string projectPath)
        {
            string targetPath = Path.Combine(projectPath, "PrivacyInfo.xcprivacy");
            string sourceAsset = "Assets/Resources/Privacy/PrivacyInfo.xcprivacy";

            if (File.Exists(targetPath))
            {
                Debug.Log("[iOS] PrivacyInfo.xcprivacy is present in the Xcode project.");
                return;
            }

            if (File.Exists(sourceAsset))
            {
                File.Copy(sourceAsset, targetPath);
                Debug.Log($"[iOS] Copied PrivacyInfo.xcprivacy from {sourceAsset}");
                return;
            }

            Debug.LogError("[iOS] PrivacyInfo.xcprivacy is missing. Run " +
                "LoneFighter > Build > Generate Privacy Manifest before building. " +
                "App Store Connect will reject the upload otherwise.");
        }

        static void ReplaceArrayOfStrings(PlistElementDict root, string key, params string[] values)
        {
            var arr = root.CreateArray(key);
            foreach (var v in values) arr.AddString(v);
        }
    }
}
#endif
