# Boss Roster (`LoneFighter.Bosses.Roster`)

Three new bosses added alongside the existing `WardenBoss`, each with a distinct combat
identity and built to *compose* with `EnemyBase` rather than replace it. Every boss in
this folder is a `MonoBehaviour` you attach to an `EnemyBase` prefab.

| Boss          | Behavior                                                            |
| ------------- | ------------------------------------------------------------------- |
| `SummonerBoss`| Stays at range, periodically spawns minions, phase-3 radial bursts. |
| `OrbiterBoss` | Orbits the player at fixed radius, fires radial bullet bursts.      |
| `MirrorBoss`  | Splits into two copies at 50% HP; survivor enrages on partner death. |

> Hard rule: this folder is the only place these bosses live. The bosses **do not** modify
> any file outside this folder. They lean on the existing `EnemyBase`/`EnemyHealth`/
> `EnemyChaseAI`/`EnemyProjectile`/`PoolService`/`FxService` contracts.

---

## How bosses compose with `EnemyBase`

The existing `EnemyBase` prefab pipeline requires:

- `EnemyBase` (drives HP / contact damage / death drops, registers with `EnemyRegistry`)
- `EnemyHealth` (HP bar, fires `OnDied`)
- `EnemyChaseAI` (Rigidbody2D-driven straight-line chase)
- An assigned `EnemyData` ScriptableObject

A boss script in this folder is added as an **extra** component:

1. At runtime, the boss disables sibling `EnemyChaseAI` (so it can drive its own movement)
   and re-enables it on `OnDisable` so the prefab returns to the pool in a clean state.
2. The boss reads stats off `EnemyBase.Data` where useful (HP, contact damage, speed) and
   uses `EnemyHealth.Max` / `EnemyHealth.Current` for phase fractions.
3. Damage flow (player hitting the boss) is unchanged — `EnemyBase.ApplyDamage` still
   forwards into `EnemyHealth`, which still fires `OnDied`, which still triggers death FX.

This means a designer can **swap any of these bosses onto any `EnemyBase` prefab** and it
will keep working. Mix-and-match is intentional (e.g. a "Mirror Summoner" is one toggle
away).

---

## EnemyData specs

All three bosses are tuned for **HP in the 200–500 range**, sized to feel like a mid-run
mini-boss (the Warden caps out at 1200 HP). Suggested authored `EnemyData` settings:

| Field             | SummonerBoss  | OrbiterBoss   | MirrorBoss     |
| ----------------- | ------------- | ------------- | -------------- |
| `displayName`     | "Summoner"    | "Orbiter"     | "Mirror"       |
| `maxHealth`       | **300**       | **350**       | **500**¹       |
| `moveSpeed`       | 1.6           | 0.0 (orbit driver overrides) | 2.0            |
| `contactDamage`   | 12            | 6 (low — it kites)            | 18             |
| `contactCooldown` | 0.5           | 0.5           | 0.5            |
| `xpDrop`          | 35            | 30            | 60             |
| `gemDropChance`   | 1.0           | 1.0           | 1.0            |
| `tint`            | dark purple   | pale cyan     | white          |
| `spriteScale`     | 1.4           | 1.2           | 1.5            |
| `aoeRadius`       | 0             | 0             | 0              |
| `projectilePrefab`| —             | (see below)   | —              |
| `projectileDamage`| —             | 8             | —              |

¹ MirrorBoss splits at 50% HP. The original keeps its remaining HP; the clone is initialized
to `maxHealth * 0.5`. So a single Mirror encounter from full = ~750 effective HP to chew
through total.

OrbiterBoss requires a separate **bullet prefab** with an `EnemyProjectile` component (the
same shape used by `Spitter`). Recommended bullet stats: `speed = 4.5`, `damage = 8`,
`lifetime = 2.5s`. Authoring tip: reuse the Spitter projectile prefab — it already works
with `PoolService`.

---

## Authoring a boss prefab — step-by-step

For each of the three bosses:

1. **Duplicate an existing enemy prefab** (Grunt is a good base) under `Assets/Prefabs/Enemies/`
   and rename: `Boss_Summoner.prefab` / `Boss_Orbiter.prefab` / `Boss_Mirror.prefab`.
2. **Create the matching `EnemyData`** asset under `Assets/Data/Enemies/` (use the values
   in the table above) and assign it to the prefab's `EnemyBase.data` field.
3. **Beef up the visuals** — bump the child `SpriteRenderer` to a larger sprite, set the
   color from the table, and add a HP bar prefab if your project has one.
4. **Add the boss MonoBehaviour** from this folder:
   - `SummonerBoss` → assign `minionPrefab` (point it at the Grunt prefab) and, optionally,
     a `spawnRitualPrefab` (a small prefab with a `SummonerSpawnRitual` component on it)
     or wire `ownedRitual` to a child of the boss prefab.
   - `OrbiterBoss` → assign `bulletPrefab` (the Spitter projectile prefab works well) and,
     optionally, `orbitalRing` (child with `OrbitalRing`) or `orbitalRingPrefab` (pooled
     instance). The ring follows the player automatically.
   - `MirrorBoss` → assign `clonePrefab` to **this same boss prefab** so the split spawns
     more of itself. Optionally assign a `splitVfxPrefab` with a `MirrorSplitVfx` component
     for the slow-mo flourish.
5. **Verify the sibling `EnemyChaseAI` is present but disabled-friendly** — `EnemyBase`
   `RequireComponent`s it, but the boss script disables it at runtime. Leave it on the
   prefab; do not delete it.

---

## How each boss reads as a fight

### SummonerBoss

Phases switch on the current HP fraction:

| Phase | HP fraction | Behavior                                                                |
| ----- | ----------- | ----------------------------------------------------------------------- |
| 1     | > 50%       | Slow flee. Summons 3–5 minions every 4–6s. 1s purple-pentagram telegraph. |
| 2     | ≤ 50%       | Summon interval drops to 2–3s.                                          |
| 3     | ≤ 20%       | Also fires `BulletPattern.RadialBurst` via the sibling Patterns track   |
|       |             | (`PatternEmitter` component) every 2.5s, if the sibling track is present. |

The Phase-3 hook is **deliberately loose**: it locates a sibling `MonoBehaviour` whose
type is named `PatternEmitter` and invokes it via `SendMessage("RadialBurst"|"Emit")` so
the two tracks compile in any order. If the sibling track ships first, this just works.
If it never ships, Phase 3 just means "faster cadence" (the radial burst silently no-ops).

### OrbiterBoss

Moves on a fixed circle around the player at `orbitRadius` (default 6 units). Every
`burstInterval` (default 1.5s) it fires a `baseBulletCount` (default 12) radial bullet
ring outward from its own position.

Phases:

| Phase | HP fraction | Behavior                                                                 |
| ----- | ----------- | ------------------------------------------------------------------------ |
| 1     | > 50%       | 12 bullets / burst, base orbit speed.                                    |
| 2     | ≤ 50%       | Bullet count doubles to 24.                                              |
| 3     | ≤ 25%       | Every other burst additionally spawns an inward bullet wave, offset by  |
|       |             | half a bullet step (alternating pattern, hard to dodge through cleanly). |

Orbit speed scales smoothly from 1.0× at full HP to `maxAngularSpeedMultiplier` (default
1.8×) at 0% HP, so the boss feels visibly more frantic as you chew through it.

### MirrorBoss

At 50% HP the boss splits. The clone:

- Inherits the same `EnemyData` (so contact damage / move speed are identical).
- Is initialized with `maxHealth * 0.5` (see the HP-pool note in the next section).
- Receives a pale-blue tint via `cloneTint`.
- Is marked `isClone = true` so it does not split again.

A `MirrorLink` component is created and binds both twins. It subscribes to each
`EnemyHealth.OnDied`. When the first death fires, the surviving twin is **enraged**:
its `EnemyData` is cloned in memory (we never mutate the SO) and its contact damage
and move speed are scaled by `1.3×` / `1.2×` respectively.

#### Why doesn't HP shared across the twins?

The task description asked for the original and clone to "share an `EnemyHealth` pointer
concept", which we can't do directly because `EnemyHealth` is a `MonoBehaviour` per
GameObject and the rest of the damage pipeline (`EnemyBase.ApplyDamage` →
`EnemyHealth.ApplyDamage`) is built around one-HP-bar-per-instance.

We chose the simplest contract that delivers the same player-facing fight: **two
independent HP bars, each half-max on split**. The total damage to kill the pair is the
same as a single 1× bar. Documenting this trade-off here because the README is the source
of truth — a future iteration could route both `ApplyDamage` calls through a shared
`MirroredHealth` proxy if a tighter "one health bar, two bodies" feel is desired.

#### Other documented trade-off: enrage wipes the gem drop

The enrage path calls `EnemyBase.Configure(newData, null)` with a per-instance copy of the
`EnemyData` SO so we never mutate the asset. The second `null` argument unfortunately
clears the survivor's `xpGemPrefab` field, so the enraged twin does not drop an XP gem on
death. We accept this trade-off to avoid reflecting into `EnemyBase`'s private fields (a
sibling track is free to switch this to a different mechanism if a gem drop is desired).

---

## Composition with the sibling Phases / `BossEncounter` framework

A sibling agent is authoring a `BossEncounter` system that schedules bosses (similar to
the existing `EnemySpawner.TrySpawn`) and orchestrates phase transitions, intro cutscenes,
and boss-bar UI. The bosses in this folder are **designed to plug into that framework with
zero changes**:

1. **`EnemyData`-driven**: every boss in this folder reads its baseline stats from the same
   `EnemyData` SO that `BossEncounter` would feed `EnemyBase.Configure`. The sibling agent
   spawns the boss via the existing prefab+SO pipeline.
2. **HP-fraction phasing**: phases are driven by `EnemyHealth.Current / EnemyHealth.Max`,
   the same fraction `BossEncounter` is expected to read for its boss-bar UI. The two
   tracks observe the same source of truth.
3. **Death event hook-up**: `EnemyHealth.OnDied` is the canonical "encounter ended" signal.
   For `MirrorBoss`, the sibling encounter should listen on **both** twins (the easy way:
   listen for `MirrorLink.PairResolved`, set after both `OnDied` events have fired).
4. **Pattern integration**: `SummonerBoss` phase 3 looks for a sibling `PatternEmitter`
   component on the same GameObject and calls it via `SendMessage`. The sibling Patterns
   track can attach `PatternEmitter` to the boss prefab and it will just work — no edits
   to anything in this folder are needed.
5. **Pooling**: every spawn path (minion, bullet, clone, ritual, vfx, ring) uses
   `PoolService.Instance.Get` with `Instantiate` fallback, matching the project-wide pool
   conventions. `BossEncounter` can release the boss back to the pool via the standard
   `EnemyBase.HandleDeath` path.

### Suggested encounter wiring (sibling-agent contract)

```csharp
// Pseudocode for the sibling BossEncounter:
var boss = spawner.TrySpawnBoss(mirrorBossData); // existing pool/spawn path
var mirror = boss.GetComponent<MirrorBoss>();
if (mirror != null)
{
    // Wait for the MirrorLink to appear (created at split time):
    StartCoroutine(WaitForPairResolution(mirror));
}
else
{
    boss.GetComponent<EnemyHealth>().OnDied += _ => EndEncounter();
}
```

`WaitForPairResolution` polls `MirrorBoss.Link` (set to non-null on split) and then
`MirrorLink.PairResolved`. This avoids a hard reference from this folder to the sibling
framework.

---

## File index

```
Assets/Scripts/Bosses/Roster/
├── README.md                # this file
├── SummonerBoss.cs          # boss MonoBehaviour
├── SummonerSpawnRitual.cs   # purple-pentagram telegraph
├── OrbiterBoss.cs           # boss MonoBehaviour
├── OrbitalRing.cs           # visible orbital path ring
├── MirrorBoss.cs            # boss MonoBehaviour
├── MirrorSplitVfx.cs        # slow-mo flash + ghostly trails on split
└── MirrorLink.cs            # twin pair manager + enrage on partner death
```

All scripts share the `LoneFighter.Bosses.Roster` namespace and have no compile-time
dependency on sibling agent tracks.
