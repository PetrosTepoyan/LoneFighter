#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.EditorTools.Build
{
    /// Generates PrivacyInfo.xcprivacy at Assets/Resources/Privacy/.
    /// Apple has required this manifest for App Store submissions since May 2024.
    /// Menu: LoneFighter > Build > Generate Privacy Manifest
    ///
    /// Required-Reason API declarations cover the APIs LoneFighter actually touches:
    ///   - UserDefaults (PlayerPrefs)         -> reason CA92.1
    ///   - File Timestamp (atomic save swap)  -> reason C617.1
    ///   - System Boot Time (Unity engine)    -> reason 35F9.1
    ///   - Disk Space (Unity engine)          -> reason E174.1
    ///
    /// We declare zero data collection and zero tracking. If you ever add analytics, ads,
    /// crash reporting, or IAP, you MUST extend NSPrivacyCollectedDataTypes accordingly.
    public static class PrivacyManifestGenerator
    {
        const string OutputDir = "Assets/Resources/Privacy";
        const string OutputPath = "Assets/Resources/Privacy/PrivacyInfo.xcprivacy";

        const string Manifest =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>NSPrivacyTracking</key>
    <false/>
    <key>NSPrivacyTrackingDomains</key>
    <array/>
    <key>NSPrivacyCollectedDataTypes</key>
    <array/>
    <key>NSPrivacyAccessedAPITypes</key>
    <array>
        <dict>
            <key>NSPrivacyAccessedAPIType</key>
            <string>NSPrivacyAccessedAPICategoryUserDefaults</string>
            <key>NSPrivacyAccessedAPIReasons</key>
            <array>
                <string>CA92.1</string>
            </array>
        </dict>
        <dict>
            <key>NSPrivacyAccessedAPIType</key>
            <string>NSPrivacyAccessedAPICategoryFileTimestamp</string>
            <key>NSPrivacyAccessedAPIReasons</key>
            <array>
                <string>C617.1</string>
            </array>
        </dict>
        <dict>
            <key>NSPrivacyAccessedAPIType</key>
            <string>NSPrivacyAccessedAPICategorySystemBootTime</string>
            <key>NSPrivacyAccessedAPIReasons</key>
            <array>
                <string>35F9.1</string>
            </array>
        </dict>
        <dict>
            <key>NSPrivacyAccessedAPIType</key>
            <string>NSPrivacyAccessedAPICategoryDiskSpace</string>
            <key>NSPrivacyAccessedAPIReasons</key>
            <array>
                <string>E174.1</string>
            </array>
        </dict>
    </array>
</dict>
</plist>
";

        [MenuItem("LoneFighter/Build/Generate Privacy Manifest")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(OutputPath, Manifest);
            AssetDatabase.ImportAsset(OutputPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[iOS] Privacy manifest written to {OutputPath}");
        }
    }
}
#endif
