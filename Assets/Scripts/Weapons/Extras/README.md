# Weapons / Extras

Three additional weapons that plug into the existing `WeaponInventory.GrantWeapon` resolver via `WeaponData.weaponComponent`. All live in `LoneFighter.Weapons.Extras` and derive from `LoneFighter.Weapons.WeaponBase`.

Because the resolver in `WeaponInventory.AddResolvedWeaponComponent` auto-prefixes short names with `LoneFighter.Weapons.`, set `WeaponData.weaponComponent` to the sub-namespaced short form (e.g. `Extras.BoomerangWeapon`) — or to the fully-qualified name (`LoneFighter.Weapons.Extras.BoomerangWeapon`). A bare short name like `BoomerangWeapon` will **not** resolve here, only inside the parent `LoneFighter.Weapons` namespace.

All upgrades inherited from `WeaponBase` flow through automatically (`AddDamageMultiplier`, `AddCooldownMultiplier`, `AddProjectileSpeedMultiplier`, `AddPierce`), with weapon-specific reinterpretations noted below.

---

## 1. BoomerangWeapon + Boomerang

- Files: `BoomerangWeapon.cs`, `Boomerang.cs`
- Behavior: every `cooldown` seconds, finds the nearest enemy via `EnemyRegistry`, spawns a pooled `Boomerang` in that direction. The boomerang flies outward at `projectileSpeed` for `outboundDuration` seconds (default **0.8 s**), decelerates over the second half, then chases the owner back along an arc. Damages enemies on **both** legs (outbound + return) — the hit-set is cleared once at the pivot so a single target can be damaged twice by one throw.
- Inspector fields on `BoomerangWeapon`:
  - `outboundDuration` (default **0.8 s**)
  - `maxLifetime` (default **3.0 s** — safety clamp)
  - `returnSpeedMultiplier` (default **1.15**)
- Inspector fields on `Boomerang`:
  - `spinDegPerSec` (default **720** — visual only)
  - `catchDistance` (default **0.35**)
- Upgrade mapping:
  - Damage / cooldown / projectileSpeed / pierce flow through `WeaponBase` unchanged.
  - `pierce` is a **per-leg** budget (both legs reset to the same total), so each pierce point lets the boomerang hit one more enemy on each leg.
- WeaponData fields to set:
  - `damage = 18`
  - `cooldown = 1.1`
  - `projectileSpeed = 9`
  - `projectileLifetime = 1.8` (informational; `Boomerang` owns its own lifetime via `maxLifetime`)
  - `pierce = 2`
  - `range = 7`
  - `projectilePrefab` -> a Boomerang prefab (see prefab notes below).
- `WeaponData.weaponComponent`: `Extras.BoomerangWeapon`.

### Boomerang prefab requirements

Empty GameObject ->
- `Rigidbody2D` (Body Type = Dynamic, Gravity Scale = 0, Freeze Rotation Z = on)
- `Collider2D` (trigger; small radius ~0.3-0.4)
- `SpriteRenderer` (boomerang sprite)
- `Boomerang` component
- Optional: `TrailRenderer` with an additive material for arc juice.

Layer: `Projectile` (so the existing physics matrix already lets it hit Enemy and excludes Projectile<->Projectile).

---

## 2. BlackHoleWeapon + BlackHole

- Files: `BlackHoleWeapon.cs`, `BlackHole.cs`
- Behavior: every `cooldown` seconds (long — ~6 s), samples enemy positions from `EnemyRegistry`, picks the **densest** cluster centroid within `data.range`, and spawns a pooled `BlackHole` at that centroid. The hole lives `lifetime` seconds (default **2.5 s**), pulling enemies within `pullRadius` inward each `FixedUpdate` with a `(1 - distance/radius)` falloff. At the end of life it detonates: `Physics2D.OverlapCircleNonAlloc` within `detonationRadius` deals `damage` to every enemy hit, plus `FxService.LevelUpBurst` and `FxService.HeavyShake`.
- Inspector fields on `BlackHoleWeapon`:
  - `densitySampleRadius` (default **2.5**)
  - `minClusterSize` (default **1**)
  - `lifetime` (default **2.5 s**)
  - `pullRadius` (default **3.5**)
  - `detonationRadius` (default **2.8**)
  - `pullStrength` (default **5.5**)
  - `enemyLayers` (default **Everything**; restrict to your Enemy layer in production)
- Cluster-search algorithm: each live enemy in range is a candidate centroid; score = number of enemies within `densitySampleRadius`; the highest-scoring candidate's average position is used. O(n^2) over enemy count, but runs at most once per cooldown.
- Upgrade mapping:
  - Damage upgrades scale the detonation AOE damage (via `WeaponBase.CurrentDamage`).
  - Cooldown upgrades fire the black hole more often (via `WeaponBase`).
  - **ProjectileSpeed** upgrades scale the per-tick pull strength (a "fast" black hole sucks harder).
  - **Pierce** upgrades widen the detonation radius (+25% per pierce point).
- WeaponData fields to set:
  - `damage = 40`
  - `cooldown = 6.0`
  - `projectileSpeed = 1` (multiplier on pull strength; 1 = use `pullStrength` as-is)
  - `projectileLifetime = 2.5` (informational; `BlackHole` owns its own lifetime)
  - `pierce = 0`
  - `range = 8`
  - `projectilePrefab` -> a BlackHole prefab.
- `WeaponData.weaponComponent`: `Extras.BlackHoleWeapon`.

### BlackHole prefab requirements

Empty GameObject ->
- `SpriteRenderer` (an additive/HDR singularity sprite; circular alpha mask works well with Bloom)
- `BlackHole` component
- Optional: animated child SpriteRenderer with `transform.Rotate` for the accretion-disk look.
- **No** Collider2D is required — the script uses `Physics2D.OverlapCircleNonAlloc` for the detonation.

---

## 3. FlamethrowerWeapon + FlameCone

- Files: `FlamethrowerWeapon.cs`, `FlameCone.cs`
- Behavior: continuous fire — `TryFire` runs every `cooldown` (default **0.1 s** = 10 Hz). Each tick aims at the nearest enemy in `data.range` and asks a persistent `FlameCone` child to apply damage in that direction. The cone is approximated by **three overlapping `Physics2D.OverlapBox` queries** (center / +halfAngle / -halfAngle), dedup'd so a single enemy takes damage at most once per tick. Visual: a particle system on the `FlameCone` child whose shape angle + start-lifetime are pushed each tick to match the current cone size.
- The `FlameCone` is **not** pooled — it persists for the weapon's lifetime, only emitting on each Tick call. `PoolService` is for transient spawns.
- Inspector fields on `FlamethrowerWeapon`:
  - `baseConeRange` (default **3.5**)
  - `baseConeHalfAngleDeg` (default **22**)
  - `halfAngleDegPerPierce` (default **6**)
  - `flameConePrefab` (optional; if null and `flameCone` isn't wired, a bare child is created at runtime)
- Inspector fields on `FlameCone`:
  - `particlesPerTick` (default **12**)
  - `driveParticleShapeAngle` (default **true**)
  - `enemyLayers` (default **Everything**; restrict to Enemy in production)
  - `maxHitsPerBox` (default **16**)
- Upgrade mapping:
  - **Damage** upgrades scale per-tick damage (via `WeaponBase.CurrentDamage`).
  - **Cooldown** upgrades shrink the tick interval (via `WeaponBase`).
  - **ProjectileSpeed** upgrades extend the cone range (`baseConeRange * projectileSpeedMultiplier`).
  - **Pierce** upgrades widen the cone half-angle (`baseConeHalfAngleDeg + pierce * halfAngleDegPerPierce`).
- WeaponData fields to set:
  - `damage = 3` (per tick; at 10 Hz that's 30 base DPS)
  - `cooldown = 0.1`
  - `projectileSpeed = 1` (multiplier on cone range — 1 = use `baseConeRange` as-is)
  - `projectileLifetime = 0` (unused)
  - `pierce = 0`
  - `range = 3.5` (target-aim search radius)
  - `projectilePrefab` -> **unused** (the FlameCone is a child of the weapon, not pooled).
- `WeaponData.weaponComponent`: `Extras.FlamethrowerWeapon`.

### FlameCone child / prefab requirements

Create a child GameObject under the weapon (or assign a prefab to `flameConePrefab` and the weapon will instantiate it on `Initialize`) ->
- `ParticleSystem` (Shape = Cone, Renderer = additive flame texture, Emission rate-over-time = 0 — the script uses `Emit(int)` per tick)
- `FlameCone` component (added automatically if absent)
- **No** Collider2D — the cone uses `Physics2D.OverlapBoxNonAlloc` queries.

---

## Authoring checklist (per new Extras weapon)

1. **Create the WeaponData SO**: `Assets/Data/Weapons/<Name>.asset` (right-click -> Create -> LoneFighter -> Weapon). Fill in the numbers above and set `weaponComponent` to e.g. `Extras.BoomerangWeapon`.
2. **Create the runtime prefab** under `Assets/Prefabs/Weapons/<Name>.prefab`. Empty GameObject + the matching `WeaponBase`-derived component from `Extras/`. For `BoomerangWeapon`, wire `data.projectilePrefab` to a Boomerang prefab. For `BlackHoleWeapon`, wire `data.projectilePrefab` to a BlackHole prefab. For `FlamethrowerWeapon`, either add a `FlameCone` child manually or assign a `flameConePrefab`.
3. **Wire the WeaponData**: drag the WeaponData SO into the component's `data` slot (inherited from `WeaponBase`).
4. **Hook into the upgrade pool**: add a `GrantWeapon` `UpgradeData` SO referencing the WeaponData so `UpgradeService` exposes it on level-up.

After authoring, the in-game level-up modal should offer the new weapons as `Unlock: <Name>` cards; selecting one grants the weapon via `WeaponInventory.GrantWeapon` -> resolver -> `host.AddComponent(LoneFighter.Weapons.Extras.<Name>Weapon)`.
