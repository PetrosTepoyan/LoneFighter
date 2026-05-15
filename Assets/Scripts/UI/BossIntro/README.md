# Boss Intro

Cinematic boss reveal sequence — slow-mo, spotlight, banner with title + subtitle,
optional audio stinger, then a smooth restore back to normal play.

Namespace: `LoneFighter.UI.BossIntro`.

## Files

| File | Role |
|---|---|
| `BossMeta.cs` | ScriptableObject. Identity + presentation data for a single boss: `enemyDataName` (matches `EnemyData.displayName`), `title`, `subtitle`, `accentColor`, optional `stinger` audio clip. |
| `BossMetaRegistry.cs` | ScriptableObject list of `BossMeta` entries. Exposes `static BossMeta Get(string enemyDataName)` and a static `Active` reference. |
| `BossIntroController.cs` | Scene singleton. Polls `EnemyRegistry.Enemies` once per second; on first sighting of a boss whose `Data.displayName` is in the registry, launches `BossIntroSequence`. |
| `BossIntroSequence.cs` | Coroutine body — slow-mo ramp to 0.2x, banner slide-in, spotlight, stinger, hold, then restore timescale + slide banner out over 0.4s. All waits are unscaled. |
| `BossNameBanner.cs` | UI banner anchored bottom-left. Title + subtitle TMP texts, clip-mask reveal, scale-bounce on land, accent-coloured background/underline/glow. |
| `BossSpotlight.cs` | Fullscreen dark cover with a feathered circle that follows the boss's screen position. Cheap, mobile-friendly path. |
| `BossStingerPlayer.cs` | Plays the stinger via `AudioManager.Instance.PlaySfx` with a high `volumeScale`. Falls back to a local `AudioSource` if `AudioManager` isn't present. |
| `Editor/BossMetaGenerator.cs` | Editor menu `LoneFighter → UI → Generate Boss Meta` — creates `Assets/Data/BossIntro/Warden.asset` (purple, "WARDEN" / "Keeper of the Arena") and a `BossMetaRegistry.asset` containing it. |

## Quick start

1. Run **`LoneFighter → UI → Generate Boss Meta`** in the editor menu. This
   creates:
   - `Assets/Data/BossIntro/Warden.asset`
   - `Assets/Data/BossIntro/BossMetaRegistry.asset` (with Warden inserted)
2. Open the gameplay scene.
3. Add an empty GameObject `BossIntroController` to the scene and assign:
   - A `Canvas` (Screen Space - Overlay or Camera) for the banner + spotlight UI.
   - Children for the banner and spotlight (see "UI wiring" below).
4. Drag the generated `BossMetaRegistry` asset into the controller's `registry`
   slot. (If you skip this, the registry still self-registers as `Active` on
   load — but the explicit reference is faster and more deterministic.)
5. Play. When an enemy whose `EnemyData.displayName` is `"Warden"` spawns, the
   intro plays once per scene load.

## UI wiring (one-time scene setup)

The script side does **not** create any GameObjects — by design, since every
project tunes its canvas hierarchy differently. Build these once and drag refs:

### `BossNameBanner`

- A `RectTransform` anchored bottom-left (`anchor (0,0)`, `pivot (0,0)`).
- A child `content` `RectTransform` for the scale-bounce.
- A `RectMask2D` (`clipMask`) inside `content` whose width is animated from 0 to
  `fullClipWidth` for the reveal wipe.
- Two `TMP_Text` labels under the mask: title (large, bold) + subtitle (smaller).
- Optional `Image` background, `Image` underline, `Image` glow — all tinted by
  `BossMeta.accentColor` at runtime.

### `BossSpotlight`

- A fullscreen child `Image` (`darkCover`) — black, alpha animated up to
  `coverAlpha` (default 0.72).
- A second `Image` (`spotlightImage`) using a circular radial-gradient sprite,
  parented to a `RectTransform` (`spotlightHost`) that follows the boss screen
  position every `LateUpdate`. Pair it with a subtractive / multiply material if
  you want a true "hole punched" effect; otherwise an additive halo over the
  dark cover reads fine for a mobile-arcade vibe.
- The component degrades gracefully — if either `darkCover` or `spotlightImage`
  is missing, the sequence still runs.

### `BossStingerPlayer`

- No wiring required. The controller auto-adds one to its own GameObject if you
  don't pre-place it.

## Behaviour notes

- **Polling cadence**: `BossIntroController.pollInterval` defaults to 1 second.
  Bosses are rare; we deliberately don't poll every frame.
- **Single-shot per boss**: each `enemyDataName` triggers at most one intro per
  scene load. Call `BossIntroController.Instance.ResetSeen()` to allow re-fires.
- **Time scale**: the sequence captures the current `Time.timeScale` on entry
  and restores it on exit. If the player pauses mid-cinematic, the sequence
  still restores back to the value it captured (1.0 during gameplay).
- **Unscaled time everywhere**: every wait inside the sequence and every tween
  inside the banner / spotlight uses `Time.unscaledDeltaTime` or
  `WaitForSecondsRealtime`, so the cinematic remains crisp under slow-mo.
- **Manual triggers**: `BossIntroController.TriggerManually(BossMeta, Transform)`
  bypasses the seen-set, handy for debug menus and scripted moments.

## Hard rules compliance

This package is **new files only** under `Assets/Scripts/UI/BossIntro/` and a
single companion editor folder. Nothing outside that path is touched — the
generated `Warden.asset` and `BossMetaRegistry.asset` are produced at runtime by
the editor menu, not committed in this branch.
