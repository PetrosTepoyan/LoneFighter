# Elites

Elite enemy variants — high-priority targets the player is drawn to fight.

* Gold-trimmed visuals, 1.4x larger, golden particle aura.
* 3x HP, 1.5x contact damage.
* Drop a guaranteed gold gem plus chance pickups (Health / Magnet / Bomb).
* Announced via a top-screen banner + an off-screen arrow indicator while alive.

All files live under namespace `LoneFighter.Enemies.Elites`.
This folder is **additive only** — no existing scripts in the repo are modified.

## File map

| File | Type | Purpose |
| --- | --- | --- |
| `EliteModifier.cs` | MonoBehaviour | Promotes a host `EnemyBase` to elite when enabled; restores state on disable (pool-safe). |
| `EliteAura.cs` | MonoBehaviour | Golden swirling ParticleSystem child (authored or runtime-built default). |
| `EliteDropTable.cs` | ScriptableObject | Loot recipe — guaranteed gold XPGem + chance HealthPickup/MagnetPickup/BombPickup. |
| `EliteDropOnDeath.cs` | MonoBehaviour | Subscribes to host `EnemyHealth.OnDied` and triggers the drop table. Added automatically by `EliteModifier`. |
| `EliteSpawnDirector.cs` | Singleton MonoBehaviour | Watches `EnemyRegistry` and promotes the next new enemy every 30s OR every 40 kills. |
| `EliteBanner.cs` | MonoBehaviour | "AN ELITE APPEARS!" banner; listens to `EliteSpawnDirector.OnEliteAppeared`. |
| `EliteIndicator.cs` | MonoBehaviour | Off-screen arrow pointing toward each alive elite. |

## Scene setup

To enable elites in the Game scene, add the following (everything is additive — no prefab edits required):

1. **EliteSpawnDirector**
   * Create an empty GameObject `EliteSpawnDirector`.
   * Add the `EliteSpawnDirector` component.
   * Create a new `EliteDropTable` asset (right-click → `Create > LoneFighter > Elite Drop Table`).
     * Drag in your `XPGem` prefab for **Gold Gem Prefab** (`value` is overwritten to 5 at spawn time).
     * Drag in `HealthPickup`, `MagnetPickup`, `BombPickup` prefabs in their respective slots.
     * Leave default chances (25 / 10 / 5 %) or tune in BALANCE.md style.
   * Assign the table to the director.
   * Optional: assign an `Aura Prefab Override` (ParticleSystem) if you have a custom gold aura VFX; otherwise the runtime default is used.

2. **EliteBanner**
   * On the main HUD canvas, add an empty child `EliteBanner` with the `EliteBanner` component.
   * Optional: assign your own `TMP_Text`, `CanvasGroup`, `Image` references for a designer-controlled look.
     Leave them empty for an auto-built banner.

3. **EliteIndicator**
   * On the main HUD canvas, add an empty child `EliteIndicator` with the `EliteIndicator` component.
   * Assign `Arrow Parent` to a `RectTransform` that fills the canvas (a child `RectTransform`
     anchored stretch-stretch works).
   * Optional: assign an `Arrow Prefab` (a `RectTransform` with an `Image` or other graphic).
     A small gold square is built at runtime if you skip it.

## Promotion mechanic

`EliteSpawnDirector` polls `EnemyRegistry.Enemies` each frame (no event API exists on the
registry and we're constrained to "new files only", so we diff instance IDs). It maintains
two arming triggers:

* **Time**: incremented every frame; once it crosses `secondsBetweenElites` (default 30s)
  the director is armed.
* **Kills**: read from `GameManager.Instance.Kills`; once the delta since the last elite
  passes `killsBetweenElites` (default 40), the director is armed.

When armed, the **next** newly-registered enemy is promoted via
`AddComponent<EliteModifier>()`. Both timers reset and arming clears.

## Pool-safety

Enemies in this codebase are recycled through `PoolService`, so any visual / stat changes
applied on promotion must be reversible. `EliteModifier`:

* Caches the host transform's local scale **at OnEnable time** and restores it at OnDisable.
* Iterates every child `SpriteRenderer` (including disabled ones) and caches its `color`
  before applying the gold tint. Originals are restored at OnDisable.
* Destroys the cloned `EnemyData` SO and any aura instance / drop hook on disable.

If a promoted enemy is killed and the same GameObject is later re-spawned as a regular
grunt, no gold tint or 3x HP leaks through.

## Known limitations

### Contact-damage scaling (documented in code)

`EnemyBase` reads `data.contactDamage` from the shared `EnemyData` SO every collision
frame, with no per-instance damage override exposed. Mutating the SO in place would scale
contact damage globally for every enemy of that archetype.

**Workaround used by `EliteModifier`:** clone the SO via `Object.Instantiate`, scale
`contactDamage` (and `projectileDamage`, since some archetypes shoot), and push it into
`EnemyBase` via the only writable public path — `EnemyBase.Configure(EnemyData, GameObject)`.

That call also overwrites the private `xpGemPrefab` field. There is no public getter, so
the elite enemy loses its default XP-gem drop. This is acceptable because elites use
`EliteDropTable` for all rewards (which includes a higher-value gold XPGem). The default
gem path in `EnemyBase.HandleDeath` short-circuits because `xpGemPrefab` is null.

Other SO fields (`contactCooldown`, `aoeRadius`, `fireCooldown`, `moveSpeed`, etc.) are
left at 1.0x to keep elite archetypes recognisably the same enemy, just beefier.

### No registry growth event

`EnemyRegistry` is a static list. The director polls it; a future refactor could expose
`OnEnemyRegistered` / `OnEnemyUnregistered` events for a cleaner subscription model.

### Aura particle cost

Each alive elite has a ParticleSystem child. The runtime-built default is capped at 64
particles + ~18 emissions/sec to stay within the mobile budget. For a final-quality build,
author a pooled aura prefab and assign it via `EliteModifier.auraPrefab` (or the
director's `Aura Prefab Override`).
