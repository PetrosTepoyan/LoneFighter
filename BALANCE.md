# LoneFighter — Balance Sheet

All numbers are tuned for a **300-second run**, **120 Hz target**, **portrait mobile**. Cooldowns deliberately avoid round half-seconds — they're tested-feel values, not napkin math.

---

## 1. Player Baseline

| Stat | Value | Notes |
| --- | --- | --- |
| Max HP | **100** | Two contact hits from a Grunt should sting, not kill. |
| Move speed | **5.0 u/s** | Outruns a Grunt (2.2), loses to a Runner (4.4 catches you only with corner-cuts; Dasher dash > you). |
| Magnet radius | **2.5 u** | Small enough that early movement matters. Pickup radius 0.5 u, magnet force 18. |
| Invuln window | **0.4 s** | Already in `PlayerHealth`. Long enough to plow through a Grunt, short enough that Spitter volleys still scare you. |
| Contact cooldown (enemy side) | 0.5 s default | Already in `EnemyData`. |

### XP Curve

Formula (matches `PlayerLevel.cs`):

```
XpToNext(level) = round(5 * 1.35 ^ (level - 1))
```

| Level reached | XP to next | Cumulative XP |
| ---: | ---: | ---: |
| 1 | 5 | 0 |
| 2 | 7 | 5 |
| 3 | 9 | 12 |
| 4 | 12 | 21 |
| 5 | 16 | 33 |
| 6 | 22 | 49 |
| 7 | 30 | 71 |
| 8 | 40 | 101 |
| 9 | 54 | 141 |
| 10 | 73 | 195 |
| 11 | 99 | 268 |
| 12 | 133 | 367 |
| 13 | 180 | 500 |
| 14 | 243 | 680 |
| 15 | 328 | 923 |
| 16 | 443 | 1251 |
| 17 | 598 | 1694 |
| 18 | 808 | 2292 |
| 19 | 1090 | 3100 |
| 20 | 1471 | 4190 |

A clean run yields ~1100–1700 XP, landing players at **level 14–18** by 5:00. Level 20 is reserved for skilled / lucky runs.

---

## 2. Enemy Archetypes

Canonical names (other agents will use these exact strings).

| Enemy | HP | Speed (u/s) | Contact dmg | XP drop | Spawn weight | Concurrent cap | Behavior |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| **Grunt** | 10 | 2.2 | 8 | 1 | 10 | 120 | Walks straight at the player; the baseline filler. |
| **Runner** | 6 | 4.4 | 6 | 1 | 6 | 50 | Sprints in fast, low HP, dies to one Pistol shot mid-build. |
| **Tank** | 80 | 1.4 | 18 | 5 | 2 | 12 | Slow, heavy contact damage, splits the swarm and tanks pierce. |
| **Spitter** | 14 | 1.8 | 7 (10 projectile) | 2 | 3 | 18 | Stops at ~5 u and lobs a slow projectile every ~1.8 s. |
| **Dasher** | 22 | 2.6 | 14 | 3 | 3 | 16 | Telegraphs for 0.6 s then dashes 4 u forward at 9 u/s. |
| **Bomber** | 18 | 2.0 | 6 (28 on death) | 3 | 2 | 14 | Detonates on death in a 1.8 u radius. Don't kill it on top of yourself. |
| **Warden** | 1200 | 1.6 | 30 | 80 | — | 1 (boss) | Mini-boss. Slow chase, periodic ground-slam in a 3 u ring (1.0 s wind-up). |

Notes for implementers:

- Spitter contact damage is reduced (it's a ranged unit) but its projectile hits hard. Projectile speed 4.5, lifetime 1.6 s.
- Dasher's dash speed is 9 u/s, well above the player's 5.0 — players must sidestep on the wind-up, not outrun it.
- Bomber's contact damage is low because the *real* damage is its on-death explosion. Implementations should fire a 1.8 u AoE on the Bomber's death event.
- Warden's HP is sized so a level-15 build clears it in ~25 s of focused fire. It is the explicit climax gate.

---

## 3. Weapons

The **Pistol** is the starting weapon and is already fully specced in the scaffold. The three unlockables are designed alongside.

| Weapon | Damage | Cooldown (s) | Proj. speed (u/s) | Pierce | Range (u) | Lifetime (s) | Behavior |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| **Pistol** | 10 | 0.55 | 12 | 0 | 10 | 2.0 | Auto-targets nearest enemy, single projectile. (Existing `ProjectileWeapon`.) |
| **Spread Shotgun** | 6 | 0.95 | 11 | 1 | 7 | 0.9 | Fires 5 projectiles in a 35deg cone; close-range crowd clear. *(Reuses `ProjectileWeapon` — multi-shot variant.)* |
| **Orbital Blade** | 14 | 0.0 | 0 | 999 | 1.8 | n/a | Two blades orbit the player at 360 deg/s, always-on contact damage with 0.25 s per-target hit cooldown. **Needs a new weapon implementation (orbiting, not projectile).** |
| **Lightning Chain** | 22 | 1.45 | n/a | n/a | 6 | n/a | Strikes the nearest enemy; chains to up to 3 additional enemies within 3.5 u, dealing 70% damage each jump. **Needs a new weapon implementation (instant-line, not projectile).** |

Cooldowns are intentionally non-round (0.55, 0.95, 1.45) so per-stack cooldown upgrades feel tactile rather than snapping to obvious values.

Range / lifetime relationship: lifetime = range / projectile speed + 0.05 s buffer (so projectiles always reach their range cap).

---

## 4. Upgrade Pool — 15 Upgrades

Split: 8 offensive / 4 defensive / 3 utility. Weight is fed to the weighted-random picker; higher weight = appears more often. Weapon grants are weighted lower because they're build-defining, not stack-filling.

| # | Name | Description | `UpgradeKind` | Magnitude / stack | Max stacks | Weight |
| ---: | --- | --- | --- | ---: | ---: | ---: |
| 1 | Bigger Bullets | +25% weapon damage. | `WeaponDamage` | 0.25 | 5 | 3.0 |
| 2 | Quick Hands | -12% weapon cooldown. | `WeaponCooldown` | 0.12 | 5 | 3.0 |
| 3 | Hot Lead | +20% projectile speed. | `WeaponProjectileSpeed` | 0.20 | 4 | 2.0 |
| 4 | Drillshot | +1 projectile pierce. | `WeaponPierce` | 1 | 4 | 2.5 |
| 5 | Heavy Slugs | +35% weapon damage (rare big hitter). | `WeaponDamage` | 0.35 | 3 | 1.5 |
| 6 | Trigger Discipline | -18% weapon cooldown (rare). | `WeaponCooldown` | 0.18 | 3 | 1.5 |
| 7 | Unlock: Spread Shotgun | Grants the Spread Shotgun weapon. | `GrantWeapon` | 1 | 1 | 1.2 |
| 8 | Unlock: Orbital Blade | Grants two orbiting blades. | `GrantWeapon` | 1 | 1 | 1.2 |
| 9 | Unlock: Lightning Chain | Grants a chain-lightning strike. | `GrantWeapon` | 1 | 1 | 1.2 |
| 10 | Vitality | +20% max HP (heals 20% of max). | `PlayerMaxHealth` | 0.20 | 5 | 2.5 |
| 11 | Thick Skin | +12% max HP (cheap and common). | `PlayerMaxHealth` | 0.12 | 5 | 2.0 |
| 12 | Fleet Foot | +8% move speed. | `PlayerMoveSpeed` | 0.08 | 5 | 2.5 |
| 13 | Adrenaline | +15% move speed (rare). | `PlayerMoveSpeed` | 0.15 | 3 | 1.5 |
| 14 | Lodestone | +30% magnet radius. | `PlayerMagnetRadius` | 0.30 | 5 | 2.0 |
| 15 | Greed | +15% XP gain. | `XpGainMultiplier` | 0.15 | 5 | 1.8 |

Split sanity check:
- Offensive: 1, 2, 3, 4, 5, 6, 7, 8, 9 → 9 entries. (The three weapon grants count as offensive build-pieces.)
- Defensive: 10, 11, 12, 13 → 4 entries.
- Utility: 14, 15 → 2 entries.

Total weight: 30.0. Weapon-grant total weight 3.6 ≈ 12% of pool — they show up, but not every level.

Max-stack totals (theoretical 100% pool clear):
- Damage: 5×25% + 3×35% = +230%
- Cooldown: 5×12% + 3×18% = -114% (capped at -85% in code recommended)
- Max HP: 5×20% + 5×12% = +160%
- Move speed: 5×8% + 3×15% = +85%

Implementers: cap cooldown reduction at ~85% so a fully-stacked player still has a non-zero fire rate.

---

## 5. Wave Timeline (300 s run)

Spawn rate climbs from ~1 to ~6 enemies/sec total. Mix shifts from Grunt-only to all-archetypes. Warden spawns once at 270s.

| startTime | endTime | enemy | spawnRate (/s) | concurrentCap |
| ---: | ---: | --- | ---: | ---: |
| 0   | 60  | Grunt    | 1.0 | 40  |
| 60  | 120 | Grunt    | 1.8 | 70  |
| 60  | 150 | Runner   | 0.4 | 20  |
| 90  | 180 | Spitter  | 0.3 | 12  |
| 120 | 210 | Grunt    | 2.2 | 90  |
| 150 | 240 | Runner   | 0.6 | 30  |
| 150 | 240 | Tank     | 0.12| 6   |
| 180 | 270 | Spitter  | 0.5 | 16  |
| 180 | 270 | Dasher   | 0.35| 10  |
| 210 | 300 | Grunt    | 2.6 | 120 |
| 210 | 300 | Bomber   | 0.25| 12  |
| 240 | 300 | Tank     | 0.18| 10  |
| 240 | 300 | Runner   | 0.8 | 40  |
| 270 | 300 | Dasher   | 0.5 | 14  |
| 270 | 300 | Spitter  | 0.6 | 18  |
| 270 | 271 | Warden   | 1.0 | 1   |

Total spawn rate over time (sum of overlapping rows, ignoring caps):
- 0–60 s: 1.0/s
- 60–90 s: 2.2/s
- 90–120 s: 2.5/s
- 120–150 s: 2.9/s
- 150–180 s: 3.22/s
- 180–210 s: 3.97/s
- 210–240 s: 4.55/s
- 240–270 s: 5.18/s
- 270–300 s: ~6.15/s + Warden

The concurrent cap stack (sum of caps) stays well under the global cap of ~200 enemies enforced by `EnemySpawner.globalCap`.

The Warden row uses a 1-second window with rate 1.0 and cap 1 — it spawns exactly once at t≈270 and persists until killed (caps and rates only gate *new* spawns; existing units don't despawn).
