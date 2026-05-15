# Weapons

All weapons live in the `LoneFighter.Weapons` namespace and derive from `WeaponBase`. The scaffold ships with **Pistol** (`ProjectileWeapon`). This folder adds three more weapons.

## How `WeaponInventory.GrantWeapon` resolves a component

`WeaponInventory.GrantWeapon(data)` decides which `WeaponBase` to attach as follows:

1. If `projectileWeaponPrefab` is assigned on `WeaponInventory` and the prefab already has a `WeaponBase`-derived component, use that component (this is the "one prefab per weapon" workflow).
2. Otherwise, read `WeaponData.weaponComponent` and `AddComponent` the type `LoneFighter.Weapons.<weaponComponent>` (short names auto-prefixed; fully-qualified names also accepted).
3. If `weaponComponent` is empty or the type can't be resolved / isn't a `WeaponBase`, fall back to `ProjectileWeapon`.

Either authoring path works; pick the one that fits the weapon. See "Authoring checklist" below.

## 1. ProjectileWeapon (Pistol — existing)

- File: `ProjectileWeapon.cs`
- Behavior: every `cooldown` seconds, finds the nearest enemy within `range`, fires one `Projectile` from the player toward it.
- Per-shot stats come from `WeaponData`: `damage`, `cooldown`, `projectileSpeed`, `projectileLifetime`, `pierce`, `range`.
- Upgrades affect the obvious things via `WeaponBase` (damage / cooldown / projectile speed / pierce).
- `WeaponData.weaponComponent`: `ProjectileWeapon` (or empty — it's the fallback).

## 2. SpreadShotgunWeapon

- File: `SpreadShotgunWeapon.cs`
- Behavior: same as `ProjectileWeapon`, but fires `pelletCount` pellets in a `spreadDegrees` cone toward the nearest enemy on each shot.
- Reuses the same `Projectile` class and `WeaponData.projectilePrefab`. No new prefab type needed.
- Inspector fields (on the component, NOT on `WeaponData`):
  - `pelletCount` (default **5**)
  - `spreadDegrees` (default **30** — BALANCE.md uses 35)
- WeaponData fields to set (per BALANCE.md):
  - `damage = 6`, `cooldown = 0.95`, `projectileSpeed = 11`, `projectileLifetime = 0.9`, `pierce = 1`, `range = 7`
  - `projectilePrefab` -> the same projectile prefab the Pistol uses (or a shotgun-flavored variant).
- `WeaponData.weaponComponent`: `SpreadShotgunWeapon`.
- Upgrades inherited unchanged from `WeaponBase` — every pellet gets the boosted damage / pierce, the whole burst is gated by the shorter cooldown, and all pellets move faster with projectile-speed upgrades.

## 3. OrbitalBladeWeapon + OrbitalBlade

- Files: `OrbitalBladeWeapon.cs`, `OrbitalBlade.cs`
- Behavior: spawns `bladeCount` persistent blade entities that orbit the player at `orbitRadius`. They damage any `EnemyBase` they touch and re-hit each enemy at most once per `perTargetHitCooldown` seconds (default **0.4 s**).
- `OrbitalBladeWeapon.TryFire` is a no-op — `WeaponBase.Update` still runs to detect upgrade changes (blade count / damage / orbit speed), but no projectiles are spawned periodically.
- Inspector fields on `OrbitalBladeWeapon`:
  - `bladeCount` (default **2**)
  - `orbitRadius` (default **1.5**)
  - `orbitSpeedDegPerSec` (default **240**)
  - `bladePrefab` -> a prefab with the `OrbitalBlade` component, a trigger `Collider2D`, a sprite, and (optionally) a TrailRenderer for juice.
- Inspector fields on `OrbitalBlade`:
  - `perTargetHitCooldown` (default **0.4 s**)
- Upgrade re-interpretations (because cooldown-based shots don't apply):
  - Damage bonus -> scales blade damage.
  - Cooldown reduction -> increases orbit angular speed (the orbit "fires faster").
  - Projectile speed bonus -> also increases orbit angular speed.
  - Pierce upgrades -> add an extra blade (and the weapon re-distributes blade angles evenly).
- WeaponData fields (per BALANCE.md):
  - `damage = 14`, `cooldown = 0` (unused — the weapon never gates on it), `pierce = 999` (informational; the actual blade count uses the component's `bladeCount` plus upgrade-applied pierce), `range = 1.8` (informational only).
  - `projectilePrefab` is unused.
- `WeaponData.weaponComponent`: `OrbitalBladeWeapon`.

### OrbitalBlade prefab requirements

Empty GameObject ->
- `SpriteRenderer` (a small blade sprite)
- `CircleCollider2D` (Is Trigger on, radius ~0.35-0.5)
- `OrbitalBlade` component
- Optional: TrailRenderer with an additive/HDR material for the orbit streak.

Layer: `Projectile` (so the physics matrix already excludes Projectile<->Projectile and lets it hit Enemy).

## 4. ChainLightningWeapon + LightningBolt

- Files: `ChainLightningWeapon.cs`, `LightningBolt.cs`
- Behavior: every `cooldown` seconds, strikes the nearest enemy within `data.range`, then chains to up to `maxJumps` more enemies within `chainRange`. Damage halves on each jump (`baseDamage * 0.5^jumpIndex`). Each segment spawns a pooled `LightningBolt` flash. The final jump triggers `FxService.LightShake`.
- Inspector fields on `ChainLightningWeapon`:
  - `maxJumps` (default **3**)
  - `chainRange` (default **4** — BALANCE.md uses 3.5)
  - `lightningBoltPrefab` -> a prefab with `LightningBolt` + `LineRenderer`.
  - `baseBoltLifetime` (default **0.12 s**)
- Upgrade re-interpretations:
  - Damage bonus -> scales base damage (and every jump that derives from it).
  - Cooldown reduction -> inherited from `WeaponBase` (fires more often).
  - Projectile speed bonus -> visual only: lengthens the bolt flash lifetime.
  - Pierce upgrades -> add `+1` to `maxJumps`.
- WeaponData fields (per BALANCE.md):
  - `damage = 22`, `cooldown = 1.45`, `range = 6`.
  - `projectilePrefab` is unused.
- `WeaponData.weaponComponent`: `ChainLightningWeapon`.

### LightningBolt prefab requirements

Empty GameObject ->
- `LineRenderer` (any material; an HDR additive Sprite-Unlit material with a soft texture pops with Bloom)
- `LightningBolt` component
- No collider — the weapon already applied damage when it spawned the bolt.

The component drives `positionCount`, widths, colors, and per-vertex jitter at runtime via `Configure(start, end, lifetime)`.

---

## Authoring checklist (per new weapon)

1. **Create the WeaponData SO**: `Assets/Data/Weapons/<Name>.asset` (right-click -> Create -> LoneFighter -> Weapon). Fill in the numbers from BALANCE.md and set `weaponComponent` to the short class name (e.g. `SpreadShotgunWeapon`).
2. **Create the runtime prefab** under `Assets/Prefabs/Weapons/<Name>.prefab`. Empty GameObject + the matching `WeaponBase`-derived component. For `OrbitalBladeWeapon`, also wire up `bladePrefab`. For `ChainLightningWeapon`, wire up `lightningBoltPrefab`.
3. **Wire the WeaponData**: drag the WeaponData SO into the component's `data` slot (inherited from `WeaponBase`).
4. **Hook into the upgrade pool**: add a `GrantWeapon` `UpgradeData` SO referencing the WeaponData. `UpgradeService` then exposes it through level-up rolls.
5. **(Optional)** If you want a one-prefab-per-weapon workflow for the starting weapon slot, drag the weapon prefab into `WeaponInventory.projectileWeaponPrefab`. The component-resolution fallback handles every other case.

That's the whole loop. After authoring, in-game the level-up modal should offer the new weapons as `Unlock: <Name>` cards; selecting one grants the weapon via `WeaponInventory.GrantWeapon`.
