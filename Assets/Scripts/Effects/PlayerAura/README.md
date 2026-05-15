# Player Aura

Layer of premium "feel" rendered around the player. Five additive scripts, all
in the `LoneFighter.Effects.PlayerAura` namespace, plus two ScriptableObject
assets you author in the project.

## What's in here

| File | Type | Role |
| --- | --- | --- |
| `WeaponAuraProfile.cs` | ScriptableObject | Per-weapon glow config: sprite, HDR color, pulse Hz, base scale, and how scale/intensity grow with weapon "level". |
| `WeaponAuraRegistry.cs` | ScriptableObject | Lookup table keyed by `WeaponData.displayName -> WeaponAuraProfile`. Decouples auras from direct WeaponData asset refs. |
| `PlayerAuraController.cs` | MonoBehaviour | Lives on the player. Polls `WeaponInventory.Weapons` once a second, diffs the set, and spawns / destroys child `SpriteRenderer` auras behind the player using the registry. Drives sine-wave pulse for scale and alpha. |
| `HitFlashAura.cs` | MonoBehaviour | Subscribes to `PlayerHealth.OnHealthChanged`. Flashes a red expanding ring whenever current HP decreases. |
| `LevelUpAura.cs` | MonoBehaviour | Subscribes to `PlayerLevel.OnLevelUp`. Plays a white -> cyan expanding burst ring. |
| `LowHealthAura.cs` | MonoBehaviour | Reads HP via `OnHealthChanged`. When `Current/Max < 0.3`, pulses a red ring whose frequency and amplitude ramp up as HP drops further. |

Everything is event-driven (HitFlash, LevelUp, LowHealth) or cheap-polled
(PlayerAuraController at 1 Hz). Nothing modifies existing files.

## Authoring flow

1. **Create profiles.** For each weapon you want to glow:
   `Assets -> Create -> LoneFighter -> Effects -> Weapon Aura Profile`.
   Drop in a soft radial glow sprite, pick a (HDR) tint, set pulse Hz, base
   scale, and scale/intensity-per-level. The profile assumes the sprite is
   authored centered with alpha falling to the edges.

2. **Create a registry.**
   `Assets -> Create -> LoneFighter -> Effects -> Weapon Aura Registry`.
   Fill the entries list: each entry maps a weapon's `displayName` string (e.g.
   `"Pistol"`, `"Spread Shotgun"`, `"Orbital Blade"`, `"Chain Lightning"`) to a
   profile. Set `defaultProfile` if you want a fallback glow for unmapped
   weapons (or leave null to suppress).

3. **Wire up the player.** On the Player root (or a child with the
   `WeaponInventory`, `PlayerHealth`, `PlayerLevel` components in its parent
   chain) add:
   - `PlayerAuraController` -> assign the registry (`weaponInventory` and
     `playerLevel` auto-resolve via `GetComponentInParent` at `Start`).
   - `HitFlashAura` -> assign a hollow ring sprite.
   - `LevelUpAura` -> assign a hollow ring sprite.
   - `LowHealthAura` -> assign a hollow ring sprite.

   Multiple components may share the same ring sprite asset; tint and animation
   parameters differ per component.

4. **Bloom.** Auras lean on URP 2D Bloom for the "premium" feel. Author colors
   above `1.0` in HDR to push them into the bloom threshold; if you don't have
   a Volume with Bloom in the scene the visuals still work, they just look
   flatter.

## Why polling for weapon inventory?

`WeaponInventory` does not expose an `OnWeaponGranted` event in the current
codebase, and the hard rules of this contribution forbid modifying existing
files. The active-weapon set is tiny (<10 entries) and changes only on level-up
upgrades, so a 1 Hz diff over `WeaponInventory.Weapons` is effectively free and
side-effect-clean. The diff is allocation-free for stable sets; it only
allocates a small pending-removal list when a weapon actually leaves the
inventory (which never happens today but is supported).

## Sorting

All five visuals expose `sortingLayerName` and `sortingOrder` on their inspector
(profile fields for the per-weapon auras, direct fields on the ring scripts).
Defaults are negative orders so the rings sit behind the player sprite. If your
project uses a custom sorting layer for player FX, set it on each.

## Weapon level note

There is no per-weapon level system in the current codebase
(`WeaponBase` exposes multipliers rather than a discrete level), so
`PlayerAuraController` feeds `PlayerLevel.Level` (clamped to
`maxLevelForAura`) into the profile's `GetScaleForLevel` /
`GetIntensityForLevel` as a generic "power" proxy. When a real per-weapon level
field lands, swap the level source in `PlayerAuraController.CurrentLevel()` or
extend the controller via a new file.
