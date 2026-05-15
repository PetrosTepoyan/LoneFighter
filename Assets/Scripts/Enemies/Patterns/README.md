# Bullet Patterns

`LoneFighter.Enemies.Patterns` — a small bullet-hell library that any enemy or
boss can opt into by attaching one component plus a ScriptableObject. Patterns
sit *on top of* existing AI (chase / keep-distance / dasher / boss), giving the
enemy a ranged attack pattern without modifying its existing behaviour.

## Pieces

| File | Type | Role |
| --- | --- | --- |
| `BulletPattern.cs` | abstract ScriptableObject | Base class. Defines `Execute(origin, target, projectilePrefab)` plus shared helpers (`SpawnBullet`, `DirectionToTarget`, `Rotate`). Owns `damage` and `projectileLifetime`. |
| `PatternEmitter.cs` | MonoBehaviour | Attach to an enemy. Holds a `BulletPattern` SO, a projectile prefab, and a fire interval. Targets `PlayerController.Instance`. |
| `PatternCoroutineRunner.cs` | hidden MonoBehaviour singleton | Runs coroutines for SO patterns that need delayed shots (`AimedBurst`, `Crossfire` second volley). Auto-spawns on first use. |
| `RadialBurst.cs` | pattern | N bullets evenly distributed around 360°. |
| `Spiral.cs` | pattern (stateful) | One shot per tick, angle rotates by `angleStep` each Execute. Supports multiple arms. |
| `AimedBurst.cs` | pattern (coroutine) | 3-shot tight burst at the target with a short delay between shots. |
| `LineShot.cs` | pattern | N bullets fanned across a `±sweep` angle through the target direction. |
| `ConeSpray.cs` | pattern | Wider shotgun spread with jittered angle and speed; designed for fast cadence. |
| `Crossfire.cs` | pattern (coroutine) | 4 bullets in cardinal directions, then 4 more rotated 45° after a brief delay. |
| `Editor/BulletPatternGenerator.cs` | editor menu | `LoneFighter → Enemies → Generate Bullet Patterns` — creates the 6 starter SOs under `Assets/Data/BulletPatterns/`. Idempotent. |

## Quick start

1. **Generate the starter SOs** — from the menu bar pick
   `LoneFighter → Enemies → Generate Bullet Patterns`. This populates
   `Assets/Data/BulletPatterns/` with six ready-to-use assets:
   - `RadialBurst_Ring12`
   - `Spiral_DoubleArm`
   - `AimedBurst_Triple`
   - `LineShot_Sweep60`
   - `ConeSpray_Shotgun`
   - `Crossfire_EightStar`

2. **Attach `PatternEmitter` to an enemy prefab.** Open the prefab (e.g.
   `Assets/Prefabs/Enemies/Spitter.prefab` or a boss prefab) and
   `Add Component → Pattern Emitter`. The component coexists with `RangedEnemy`,
   `EnemyChaseAI`, `WardenBoss`, etc. — it doesn't replace them.

3. **Pick a pattern.** Drag one of the generated SOs into the `Pattern` slot,
   and drag your enemy projectile prefab (anything with an `EnemyProjectile`
   component) into `Projectile Prefab`.

4. **Tune the cadence.** `Fire Interval` is seconds between executions; add
   `Interval Jitter` to desync multiple enemies of the same kind. `Fire Range`
   gates firing on distance to the player (set to 0 to fire regardless).

That's it — Play mode and the enemy will start emitting the chosen pattern at
the player while its existing AI keeps driving its movement.

## Mixing patterns on bosses

Add multiple `PatternEmitter` components to a single GameObject, each pointing
at a different SO and using a different fire interval. For phase-based bosses,
swap the `Pattern` field at runtime:

```csharp
var emitter = GetComponent<PatternEmitter>();
emitter.Pattern = phaseTwoPatternSO;
```

## Notes / gotchas

- **Pooling.** Every pattern spawns via `PoolService.Instance.Get`, so make sure
  the scene has a `PoolService` (the existing `RangedEnemy` requires the same).
  Without it the patterns fall back to `Instantiate`.
- **Spiral is stateful.** The rotating angle lives on the SO instance — two
  enemies pointing at the same `Spiral_DoubleArm` will trace a synchronised
  spiral. Duplicate the asset for independent rotation.
- **Damage lives on the pattern.** A `LineShot` asset whose `damage = 4` applies
  4 damage per bullet regardless of which enemy fires it. To vary damage by
  enemy tier, author multiple SO variants (`LineShot_Sweep60`,
  `LineShot_Sweep60_Heavy`, etc.).
- **`AimedBurst` and `Crossfire` second volley** are driven by
  `PatternCoroutineRunner`, a hidden DontDestroyOnLoad singleton. If the firing
  enemy is destroyed mid-burst the remaining shots are skipped (origin null
  check inside the coroutine).
- **Game state.** `PatternEmitter` pauses on `GameManager.State != Playing` by
  default. Disable `Require Playing State` for sandbox / test scenes that don't
  have a `GameManager` (the reflection lookup will no-op if the type is
  missing).
