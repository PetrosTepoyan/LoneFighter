# Settings UI

PlayerPrefs-backed game settings: master/music/sfx volumes, haptics toggle, target FPS, quality level.

## Files

- `GameSettings.cs` — static class with strongly-typed accessors (`MasterVolume`, `MusicVolume`, `SfxVolume`, `HapticsEnabled`, `TargetFps`, `QualityLevel`). Every setter persists to `PlayerPrefs` and fires `GameSettings.Changed`. `Apply()` pushes current values into `AudioListener.volume`, `QualitySettings.SetQualityLevel`, and `Application.targetFrameRate`.
- `SettingsPanel.cs` — `MonoBehaviour` UI controller for the settings panel. Reads from `GameSettings` on `OnEnable`, writes through on every control change.
- `SettingsBootstrap.cs` — loads + applies settings on the very first frame via `RuntimeInitializeOnLoadMethod`. Also re-applies in `Awake` when dropped onto a GameObject for safety in "Play from current scene" flows.

## Wiring into the existing scenes

### 1. Bootstrap

Drop a `SettingsBootstrap` component onto **any** persistent GameObject in your earliest scene (e.g. the GameObject that holds `GameManager`, or a dedicated `_Bootstrap` GameObject in `MainMenu`). The `[RuntimeInitializeOnLoadMethod]` static path will normally cover this for you, but adding the component is a cheap belt-and-braces measure for editor runs.

### 2. MainMenu — add a Settings button + panel

The existing `MainMenuController.cs` only has Play / Quit buttons; do **not** modify it. Instead:

1. In the `MainMenu` scene, add a new `Button` (label: "Settings") alongside Play / Quit.
2. Create a child `Panel` GameObject named `SettingsPanel` (start it disabled).
3. Inside the panel, add the UI controls listed below.
4. Add the `SettingsPanel` component to the panel root and drag in the references.
5. Wire the Settings button's `OnClick` to `SettingsPanel.gameObject.SetActive(true)`.
6. Wire the `SettingsPanel.onClose` UnityEvent to `SettingsPanel.gameObject.SetActive(false)`.

### 3. Required `SettingsPanel` references

| Field | Type | Notes |
|---|---|---|
| `masterSlider` | `Slider` | Min 0, Max 1. |
| `musicSlider` | `Slider` | Min 0, Max 1. |
| `sfxSlider` | `Slider` | Min 0, Max 1. |
| `hapticsToggle` | `Toggle` | — |
| `fpsDropdown` | `TMP_Dropdown` | Options auto-populated to "60 fps", "90 fps", "120 fps", "Uncapped" (values 60/90/120/-1). |
| `qualityDropdown` | `TMP_Dropdown` | Options auto-populated to "Low", "Medium", "High" (indices 0/1/2). |
| `closeButton` | `Button` | — |
| `onClose` | `UnityEvent` | Wire to whatever "close" means in your scene (deactivate panel, swap canvases, etc.). |
| `previewClickClip` | `AudioClip` (optional) | Played on master/sfx slider release for instant feedback. Skipped if null or `AudioManager.Instance` is null. |

The dropdown options are populated by `SettingsPanel.OnEnable` — you don't need to author them in the inspector.

## Behavior notes

- Volume sliders write through `GameSettings.Apply()`, which sets `AudioListener.volume = MasterVolume`. Per-source music/sfx attenuation is owned by `AudioManager` and is **not** force-overwritten — pair this panel with a future `AudioManager` tweak if you want music/sfx volumes to apply to in-game audio. Today, only Master gates everything.
- `Apply()` also calls `QualitySettings.SetQualityLevel(QualityLevel, applyExpensiveChanges: true)` and `Application.targetFrameRate = TargetFps`. Make sure the URP project has at least 3 quality levels configured.
- Haptics is persisted but the actual vibration call is gameplay code's responsibility — read `GameSettings.HapticsEnabled` from your `Handheld.Vibrate()` call site.
