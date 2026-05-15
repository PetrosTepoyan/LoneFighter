# LoneFighter — iOS / TestFlight Release Guide

End-to-end checklist for getting `LoneFighter` from the Unity project on your Mac into a TestFlight build that internal/external testers can install.

**Pre-configured assumptions** (set in `Assets/Editor/Build/IOSBuildConfig.cs`):

- Bundle ID: `com.petrostepoyan.lonefighter`
- Minimum iOS: **15.0**
- Device family: **iPhone + iPad (Universal)**, portrait-locked
- Scripting backend: **IL2CPP**, ARM64 only
- ProMotion **120 Hz unlocked** via `CADisableMinimumFrameDurationOnPhone`
- Encryption export compliance: **No** (no custom crypto)

You have an active Apple Developer Program account — that's a prerequisite. If you don't yet, enroll at https://developer.apple.com/programs/enroll/ ($99/yr, 24-48 hr review).

---

## Stage 1 — Apple Developer portal (one-time, ~10 min)

1. Sign in to [developer.apple.com](https://developer.apple.com/account).
2. **Certificates, Identifiers & Profiles → Identifiers → +** → "App IDs" → "App". Bundle ID: `com.petrostepoyan.lonefighter`. Description: `LoneFighter`. Capabilities: leave default (no Game Center / IAP yet — easy to add later).
3. **Certificates → +** → "Apple Distribution". If you've never done this: follow the steps to upload a CSR from Keychain Access (Mac → Keychain Access → Certificate Assistant → Request a Certificate from a Certificate Authority → Save to disk → upload). Download the cert, double-click to install in Keychain.
4. **Profiles → +** → "App Store" → select your bundle ID → select your distribution cert → name it `LoneFighter App Store`. Download (`.mobileprovision`), double-click to install.

You can skip step 3-4 entirely if you use Xcode's **Automatic Signing** (recommended for solo dev). See Stage 4 below.

---

## Stage 2 — App Store Connect (one-time per app, ~10 min)

1. Sign in to [appstoreconnect.apple.com](https://appstoreconnect.apple.com).
2. **My Apps → +  → New App**.
   - Platform: **iOS**
   - Name: **LoneFighter** (this is the display name; can change later)
   - Primary language: **English (U.S.)**
   - Bundle ID: select `com.petrostepoyan.lonefighter`
   - SKU: `lonefighter-001` (internal identifier; any unique string)
   - User Access: Full Access
3. After creation, fill in just enough to enable TestFlight:
   - **App Information** → category: **Games / Action**
   - **Privacy Policy URL** → see [PRIVACY_POLICY.md](./PRIVACY_POLICY.md) — host it on any URL (GitHub Pages, your domain, etc.)
   - **Data Collection** → for the vertical slice with no analytics, answer "No, we do not collect data from this app"

You DO NOT need screenshots, description, or App Review submission to start TestFlight — those are for the public App Store launch later.

---

## Stage 3 — Unity build (the part this repo automates)

Open the project in Unity 6 LTS. Then:

### Generate placeholder content (if not done yet)

Run these four Editor menu items in order. Each takes <30 seconds:

```
LoneFighter → Art → Sprite Generator          → click "Generate All"
LoneFighter → Audio → Generate Placeholder SFX → click "Generate All"
LoneFighter → FX → Generate FX Prefabs         (also Material, Post-Processing, Cinemachine Rig)
LoneFighter → Content → Content Seeder         → click "Seed All", then "Wire Cross-References"
```

### Wire up prefabs and scenes

Follow [SETUP.md](./SETUP.md) sections 4-15. This is the one-time Editor work (~15 min).

### Configure for iOS

```
LoneFighter → Build → Apply iOS Settings        ← sets bundle ID, version, IL2CPP, etc.
LoneFighter → Build → Generate Privacy Manifest ← writes PrivacyInfo.xcprivacy
LoneFighter → Build → Generate App Icon         ← 1024×1024 procedural icon, auto-assigned
```

Verify in **File → Build Settings**: `iOS` is the active platform; all three scenes (`MainMenu`, `Game`, `GameOver`) are listed under "Scenes In Build" in that order.

### Build the Xcode project

Either:

**Option A — From the Editor menu:**
```
LoneFighter → Build → Build iOS Xcode Project
```
This applies settings, bumps the build number, and writes the Xcode project to `Build/iOS/`.

**Option B — From the command line** (faster for iteration; needs Unity installed):
```bash
./build-ios.sh
```

After ~3-10 min you'll have `Build/iOS/Unity-iPhone.xcworkspace`.

---

## Stage 4 — Xcode (sign + archive + upload, ~5 min)

1. Open `Build/iOS/Unity-iPhone.xcworkspace` in Xcode 16+.
2. Select the **Unity-iPhone** target → **Signing & Capabilities**:
   - Check **Automatically manage signing**
   - Team: select your Apple Developer team
   - Xcode auto-creates a development provisioning profile if needed.
3. Set the run destination to **Any iOS Device (arm64)** (next to the play button). Do NOT use a simulator — TestFlight builds must be device archives.
4. **Product → Archive**. Takes ~2-5 min on first build, less on subsequent.
5. When the Organizer window opens, select your archive → **Distribute App** → **App Store Connect** → **Upload** → keep all defaults → **Upload**.

Two failure modes worth knowing:

- "Asset validation: missing icon" → re-run `LoneFighter → Build → Generate App Icon` then rebuild.
- "ITMS-91056: Invalid privacy manifest" → re-run `LoneFighter → Build → Generate Privacy Manifest` then rebuild.

---

## Stage 5 — TestFlight (~5 min + Apple processing)

1. Back at [App Store Connect](https://appstoreconnect.apple.com) → your app → **TestFlight** tab.
2. Wait for processing — usually 5-15 min. The build shows status `Processing` then `Ready to Submit`.
3. **For internal testers** (instant, no review needed):
   - **Internal Testing** → **+** → Create group "Devs"
   - Add up to 100 internal tester emails (must be App Store Connect users on your team)
   - Select the build → save
   - Testers get an invite email + can install via the TestFlight app
4. **For external testers** (requires one-time Beta App Review, ~24 hr):
   - **External Testing** → **+** → Create group
   - Add up to 10,000 tester emails (any Apple ID)
   - Fill in "Test Information": what to test, beta description, feedback email
   - Submit for Beta App Review. Once approved, the same build can be made available to additional external groups without re-review.

---

## Re-uploading (every iteration)

```bash
./build-ios.sh                  # auto-bumps the build number
# then in Xcode: Product → Archive → Distribute → Upload
```

The **build number must strictly increase** for every upload to App Store Connect. The `Bump iOS Build Number` menu item and `build-ios.sh` handle this automatically.

You can change the marketing version freely (`PlayerSettings.bundleVersion`, currently `0.1.0`).

---

## When you're ready for public App Store launch

- Fill in App Store metadata (description, keywords, support URL, marketing URL).
- Upload screenshots: required 6.7" iPhone (1290×2796) and 12.9" iPad (2048×2732) sizes. The simplest path: run the game on the simulator at those resolutions, take screenshots, drag in.
- Set the age rating questionnaire (LoneFighter: cartoon/fantasy violence → likely **9+**).
- Pick a price tier (Free is the easy default for the slice).
- Submit for App Review. Approval usually <48 hr.

See [STORE_LISTING.md](./STORE_LISTING.md) for placeholder marketing copy.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| 120Hz isn't kicking in on a ProMotion iPhone | Confirm `Info.plist` contains `CADisableMinimumFrameDurationOnPhone = YES` (the post-build processor sets this; verify in Xcode). Also confirm `Application.targetFrameRate = 120` is set at runtime (it is, in `GameManager.Awake`). |
| Archive shows "No accounts" | Xcode → Settings → Accounts → add your Apple ID. |
| Code signing error: profile doesn't match bundle ID | Project Settings → check bundle ID matches `com.petrostepoyan.lonefighter` exactly (case-sensitive). |
| TestFlight build "Missing Compliance" | We set `ITSAppUsesNonExemptEncryption = NO` in Info.plist. If you ever add custom crypto (e.g. encrypted save files), flip this to `YES` and complete the export questionnaire in App Store Connect. |
| Build size > 200 MB | Strip mip-mapped textures; in Player Settings → Other → set **Stripping Level: Medium**; consider App Thinning. Not a TestFlight blocker but worth tuning before public launch. |
| Build fails with "Bitcode is deprecated" | Should be auto-disabled by post-build processor. If it persists, in Xcode: Build Settings → set `ENABLE_BITCODE = NO` on every target. |

---

## What the build automation does, in plain English

`Assets/Editor/Build/` adds five Editor scripts:

- **`IOSBuildConfig.cs`** — applies every PlayerSetting needed for iOS in one click.
- **`IOSPostBuildProcessor.cs`** — runs automatically after Unity emits the Xcode project; patches `Info.plist` (120Hz unlock, encryption answer, portrait orientation) and `project.pbxproj` (disable bitcode, ARM64-only, dSYM, deployment target).
- **`PrivacyManifestGenerator.cs`** — writes the App-Store-required `PrivacyInfo.xcprivacy`.
- **`IconGenerator.cs`** — generates a 1024×1024 procedural app icon and assigns it.
- **`IOSBuildScript.cs`** — orchestrates the whole flow with menu items + CLI entry point.

You can re-run any of them safely; they're all idempotent.
