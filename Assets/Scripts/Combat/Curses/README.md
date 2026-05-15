# Curse System (`LoneFighter.Combat.Curses`)

Pre-run negative modifiers that boost rewards. Inspired by Hades' **Heat** and
Vampire Survivors' **Inverse Mode**. Players pick 0–3 curses from the main
menu before a run. Each curse makes the run harder in a specific way and adds
a percentage to the run-wide reward multiplier (XP gain, gem drops, gold,
etc).

## Files

| File | Purpose |
| --- | --- |
| `Curse.cs` | `ScriptableObject` definition: id, displayName, description, icon, kind, magnitude, rewardBonus. |
| `CurseKind.cs` | Enum of effect families (`Bloodlust`, `Swarm`, `Fragile`, `Slowed`, `NoCrits`, `GlassCannon`, `DenseFog`, `ExtraBosses`). |
| `CurseManager.cs` | Singleton. Owns the active set, applies effects on the `Playing` state transition, exposes static signal flags / multipliers. |
| `CurseSelectionUi.cs` | Main-menu UI. Toggleable cards, capped at 3 picks, commits to `CurseManager` on **Begin Run**. |
| `CurseHud.cs` | In-run HUD strip showing icons for active curses. Also drives the `DenseFog` vignette. |
| `CurseRewardCalculator.cs` | Stateless helper. `Multiplier = 1 + Σ rewardBonus`. |
| `Editor/CurseStarterAssetsMenu.cs` | `LoneFighter → Curses → Generate Starter Curses` menu item — creates / updates eight starter assets under `Assets/Data/Curses/`. |

## Wiring up (one-time, in the Unity Editor)

### 1. Generate starter curses

`LoneFighter → Curses → Generate Starter Curses`. Creates eight `.asset`
files under `Assets/Data/Curses/`. Re-runnable — existing assets are
updated in place. **Icons are not overwritten on re-run**, so you can drag
your own sprites in and re-run later without losing them.

### 2. MainMenu scene

1. Add an empty GameObject named `CurseManager` and attach `CurseManager`.
   (Optional — `CurseSelectionUi.Start()` will call `CurseManager.GetOrCreate()` if one isn't present, but having it in the scene is easier to inspect.)
2. Build a "Curse Selection" panel under your menu canvas. Attach
   `CurseSelectionUi` and assign:
   - `availableCurses` → the eight assets generated in step 1.
   - `cardPrefab` → a prefab containing a `Toggle`, an `Image` (for the
     icon), and 1–3 `TMP_Text` widgets (name / description / reward bonus).
   - `cardContainer` → a transform with a `VerticalLayoutGroup` or
     `GridLayoutGroup`.
   - `startRunButton` → your **Play with Curses** button.
   - Optional: `clearButton`, `rewardSummaryText`, `selectionCountText`.
3. Optionally hook your existing **Play** button on `MainMenuController` to
   show the curse panel instead of calling `LoadGameScene` directly. The
   simplest path is to add a second button labeled **Play with Curses** and
   wire it to the panel, leaving the original Play button alone.

### 3. Game scene

1. Build a "Curse HUD" panel under your in-run canvas (top-left or
   top-right). Attach `CurseHud` and assign:
   - `iconPrefab` → a prefab containing an `Image`.
   - `iconContainer` → a transform with a `HorizontalLayoutGroup`.
   - Optional: `vignetteOverlay` → a full-screen stretched `Image` with
     starting alpha 0 (used by `DenseFog`).

## Reading the reward multiplier from other systems

```csharp
using LoneFighter.Combat.Curses;

// Anywhere you grant a reward (XP, gold, drop roll):
int scaledXp = Mathf.RoundToInt(baseXp * CurseRewardCalculator.Multiplier);
```

`CurseRewardCalculator.Multiplier` returns `1.0` when no curses are active
or no manager exists yet, so it is safe to call from any scene.

## Integration gaps (TODO — future patches to sibling systems)

The brief explicitly disallowed modifying existing files for this slice, so
several curse effects can't reach all the way down to the systems they need
to influence. Each affected handler logs a warning on application and
exposes a public static signal that the sibling system should read once it
is updated. Suggested follow-ups:

### `Bloodlust` — needs `EnemyBase` patch

`CurseManager.EnemyDamageMultiplier` (default `1.0`) is set to
`(1 + magnitude)` per active Bloodlust curse. **Today nothing reads it.**
The minimal change is in `EnemyBase.TryDamagePlayer`:

```csharp
health.ApplyDamage(data.contactDamage * CurseManager.EnemyDamageMultiplier);
```

Same multiplier should also be applied in `EnemyProjectile` / `RangedEnemy`
when their damage values are resolved.

### `Swarm` — needs `WaveManager` patch

`CurseManager.SpawnRateMultiplier` is set to `(1 + magnitude)` per active
Swarm curse. The minimal change is in `WaveManager.Update`:

```csharp
acc += Time.deltaTime * entry.spawnRate * CurseManager.SpawnRateMultiplier;
```

### `ExtraBosses` — needs `WaveManager` (or boss-spawn) patch

`CurseManager.BossCountMultiplier` is set to `2.0` when active. There is no
single "boss spawn count" field in `WaveConfig` today — the warden boss is
just another `WaveConfig.Entry`. The cleanest fix is to add a
`bool isBoss` flag (or `tag`) on the `Entry` and multiply that entry's
`concurrentCap` / total spawn budget by `BossCountMultiplier`.

### `NoCrits` — needs `CritService` (sibling agent's work)

`CurseManager.NoCrits` is a public `static bool`. The sibling crit service
should check it inside its roll function and return a non-crit when true.
We deliberately use a static flag rather than an event so the crit-roll hot
path is a single field load.

### `GlassCannon` (damage side) — needs weapon patch

The HP-loss half of GlassCannon is fully applied today via `PlayerHealth.SetMax`.
The damage-bonus half is surfaced as `CurseManager.PlayerDamageMultiplier`.
Weapons should fold that scalar in when computing outgoing damage. For the
existing `ChainLightningWeapon` / projectile weapons, multiply the damage
field at fire time:

```csharp
float damage = weaponData.damage * CurseManager.PlayerDamageMultiplier;
```

### Fully working today

- **Fragile** — calls `PlayerHealth.SetMax` directly.
- **Slowed** — sets `PlayerController.MoveSpeed` directly.
- **DenseFog** — `CurseHud` watches `CurseManager.DenseFog` and fades a
  vignette `Image` in.

## Lifecycle notes

- `CurseManager` survives scene loads (`DontDestroyOnLoad`).
- Effects are applied **once per run**, on the transition into
  `GameState.Playing` from any state *other* than `Paused` / `LevelUp` (so
  pausing and resuming does not re-stack).
- Static factors (`EnemyDamageMultiplier` etc.) are reset to defaults on
  `GameOver` / `Victory`. The chosen curse list is **not** cleared, so the
  player can immediately restart with the same selection.
- `CurseRewardCalculator.Multiplier` always returns `>= 1.0`. Reward
  systems can multiply blindly without sign checks.

## Design knobs

Default values on the generated starter assets:

| Curse | Magnitude | Reward bonus |
| --- | --- | --- |
| Bloodlust | +30% enemy damage | +20% |
| Swarm | +50% spawn rate | +25% |
| Fragile | -25% max HP | +20% |
| Slowed | -15% move speed | +15% |
| No Crits | (flag) | +20% |
| Glass Cannon | +50% dmg / -50% HP | +30% |
| Dense Fog | (visual) | +15% |
| Extra Bosses | 2× boss count | +35% |

Picking all three of (Glass Cannon, Extra Bosses, Swarm) gives a reward
multiplier of **x1.90** for a brutal run.
