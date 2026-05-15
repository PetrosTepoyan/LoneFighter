# Cheats

Debug-only cheat toggles for the LoneFighter Unity project.

Namespace: `LoneFighter.Debugging.Cheats`
Folder: `Assets/Scripts/Debug/Cheats/`

All cheats are gated by `BuildGuard.CheatsAllowed`, which returns true only when
`Debug.isDebugBuild || Application.isEditor`. In a Release build every flag is
forced to `false`, the IMGUI panel never draws, and persisted PlayerPrefs keys
are cleared on boot.

## Files

| File | Role |
| --- | --- |
| `BuildGuard.cs` | Static gate: development build / editor only. Logs a critical error if anything tries to enable cheats in release. |
| `CheatService.cs` | Singleton holding the eight cheat flags. Each has a `public bool` and a per-flag `Changed` event, plus a global `AnyChanged` event. |
| `CheatHooks.cs` | `MonoBehaviour` that polls the flags each `Update` / `FixedUpdate` and enforces them. |
| `CheatUi.cs` | IMGUI toggle panel. Opens on `Shift+F1` (desktop) or shake (mobile, `|Input.acceleration| > 2.0`). |
| `CheatPersistence.cs` | Mirrors flags to/from `PlayerPrefs` so they survive scene loads. |
| `CheatBootstrap.cs` | `RuntimeInitializeOnLoadMethod` that auto-spawns a `[Cheats]` DDOL host with `CheatHooks`, `CheatUi`, `CheatPersistence` attached — no scene edits required. |

## Flags

| Flag | Behavior |
| --- | --- |
| `GodMode` | Each `Update`, finds `PlayerController.Instance.GetComponent<PlayerHealth>()` and tops it back up via `Heal(Max - Current)`. |
| `OneHitKills` | Best-effort: each frame the `EnemyRegistry` membership count changes, applies `99999` damage to every live enemy. Limit: we cannot subscribe per-enemy because `EnemyHealth` has no `OnDamaged` event; if an enemy is spawned and damaged in the same frame, our overkill applies on the next frame at the latest. |
| `InfiniteXp` | Tracks `EnemyRegistry.Enemies.Count` between frames. For each unit it shrinks (i.e. each enemy that left the registry), awards `XpToNext` so the player levels up at least once per kill. |
| `NoSpawn` | Finds the `EnemySpawner` (including inactive) and disables its `GameObject`. Re-enables when the flag is turned off. |
| `AutoFire` | No-op marker — weapons in this project already auto-fire via `WeaponBase.Update`. Kept in the API so future hand-fired weapons can subscribe to `AutoFireChanged`. |
| `FreezeEnemies` | Each `FixedUpdate`, walks `EnemyRegistry.Enemies` and zeroes `Rigidbody2D.linearVelocity`. |
| `InstantCooldowns` | Exposed as `CheatHooks.CooldownOverride` (returns `0.05` when active, else `-1`). Weapons can opt in by reading it; documented for future integration. |
| `BigDamage` | Exposed as `CheatHooks.DamageMultiplier` (returns `1000` when active, else `1`). Weapons can opt in by reading it; documented for future integration. |

`InstantCooldowns` and `BigDamage` are surfaced as read-only properties on
`CheatHooks` rather than mutating `WeaponBase` directly because the hard rule of
this contribution is "no edits to existing files". Wiring them through
`WeaponBase` is a one-line follow-up in that file when the project is ready to
adopt cheats end-to-end.

## Activation

- Editor or development build only.
- Desktop: hold `Shift` and press `F1` to open/close the panel.
- Mobile: shake the device (`|Input.acceleration| > 2.0`, 1s debounce).

## Persistence

`CheatPersistence` listens to `CheatService.AnyChanged` and mirrors every flag
to `PlayerPrefs` under the `lf.cheat.` prefix. On boot it loads them back. In a
release build the keys are deleted defensively so a previously-flashed dev
state cannot leak into shipped builds on the same device.

## Bootstrapping

`CheatBootstrap` runs at `RuntimeInitializeLoadType.AfterSceneLoad` and creates
the `[Cheats]` host GameObject. No scene or prefab needs editing.
