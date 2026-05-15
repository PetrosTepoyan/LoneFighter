# Minimap

Bottom-right corner radar showing nearby enemies, pickups, and bosses for LoneFighter.

Namespace: `LoneFighter.UI.Minimap`
Folder: `Assets/Scripts/UI/Minimap/`

## Files

| File | Role |
| ---- | ---- |
| `MinimapController.cs` | uGUI controller. Owns the radar root + a pool of `MinimapMarker` dots. Every `refreshEveryFrames` frames in `LateUpdate`, hides all live markers and re-plots in-range entities. |
| `MinimapMarker.cs` | Pooled `Image` element. Knows its type (Enemy, Elite, Boss, Xp, Health, Pickup, Player) and draws color + size accordingly. Health markers get a small horizontal bar overlay so they read as a "+". |
| `RadarPulse.cs` | Auto-builds a thin sweep arm pivoted at the radar center that does one full rotation per `periodSeconds` (default 2s). Purely cosmetic. Uses unscaled time so it keeps spinning while the game is paused. |
| `MinimapMaskSetup.cs` | Runtime helper that guarantees the radar root has a circular `Mask` + procedural anti-aliased circle sprite. Sprite is cached per resolution/color/softness combo so multiple radars can share. |
| `DangerProximity.cs` | Pushes the radar background color toward red and adds a subtle alpha pulse when in-range enemy count exceeds the configured threshold. Fed by `MinimapController.ReportEnemyCount`-style hand-off (controller calls `dangerProximity.ReportEnemyCount(n)` each refresh). |
| `OffscreenBossArrow.cs` | Pins an arrow to the screen edge that points at the nearest off-screen boss. Boss detection is a substring check on `EnemyData.displayName` / asset name for `"Warden"` or `"Boss"` (case-insensitive). |

## Wiring (manual scene setup)

1. Add a Screen-Space-Overlay `Canvas` (or use the existing HUD canvas).
2. Add a child `RectTransform` named `Minimap`, anchored bottom-right (anchor min/max = (1,0)), pivot (1,0), `sizeDelta` = e.g. `(192, 192)`, `anchoredPosition` = `(-24, 24)`.
3. Add components to the `Minimap` GameObject (in this order):
    - `MinimapMaskSetup` — auto-creates an `Image` + `Mask` with a procedural circle sprite on `Awake`.
    - `RadarPulse` — auto-creates the sweep arm child.
    - `DangerProximity` — picks up the `Image` on the same GameObject as the background.
    - `MinimapController` — leave `radarRoot` blank to default to this transform.
4. Drag `DangerProximity` and (optionally) `MinimapMaskSetup` into the matching fields on `MinimapController`.
5. For the boss arrow, add another child `RectTransform` on the canvas (anchored center, pivot center) and add `OffscreenBossArrow`. The arrow image is auto-generated if you don't supply one.

Everything is opt-in: each component is independently functional. The controller works on its own and just lights up its subsystems if they're wired.

## Default tuning

- `worldRadius` = 12 world units
- `refreshEveryFrames` = 5 (24Hz at 120 FPS — plenty for a radar)
- `initialPoolSize` = 32 markers, grows on demand, capped at `maxVisibleMarkers` = 256
- Danger threshold: 30 enemies in range -> full red, with a deadzone below 6 enemies
- Radar pulse: 2 second period, unscaled time

## Notes

- No allocations per frame for enemies (uses `EnemyRegistry.Enemies`).
- XP gems and other pickups use `Object.FindObjectsByType<T>(FindObjectsSortMode.None)`. This allocates an array each refresh but is bounded by the scene's active pickup count and only runs every N frames. If you ever need to drive this from a hot loop, mirror the registry pattern from `EnemyRegistry` for `XPGem` etc.
- All UI elements run with `raycastTarget = false` to avoid stealing input from gameplay touches.
- The procedural circle sprite and arrow sprite are cached statically and marked `HideFlags.DontSave` so they don't leak into the scene file.
- Boss detection is intentionally a name-match heuristic — adding a flag to `EnemyData` would mean editing files outside this folder.
