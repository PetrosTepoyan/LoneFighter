# Dash Ability

Player dash / dodge with i-frames. Lives entirely under
`Assets/Scripts/Abilities/Dash/` in the `LoneFighter.Abilities` namespace and is
fully additive - no existing file in the project is modified.

## Scripts

| File                  | Role                                                                 |
| --------------------- | -------------------------------------------------------------------- |
| `DashAbility.cs`      | Core MonoBehaviour. Cooldown-gated burst velocity + i-frame window.  |
| `DashCharges.cs`      | Multi-charge regen state. 1 charge by default, upgradable.           |
| `DashInputHandler.cs` | Programmatically defined `InputAction` for Dash (no asset edits).    |
| `DashTrail.cs`        | Spawns 5 fading afterimage ghosts, blue-cyan tint, 0.4 s fade.       |
| `DashChargeUi.cs`     | Bottom-corner charge HUD. Pulses the next slot while it recharges.   |
| `DashShockwave.cs`    | Small radial pushback on dash end. No damage, just feel.             |

## Scene setup

1. On the **Player** GameObject (the one carrying `PlayerController`,
   `PlayerHealth`, `Rigidbody2D`, `Collider2D`):
   - Add `DashAbility`
   - Add `DashCharges` (auto-added by `DashAbility.Awake` if missing)
   - Add `DashInputHandler`
   - Add `DashTrail` (optional - pure visual)
   - Add `DashShockwave` (optional - pure feel)
2. On a UI Canvas (the existing HUD canvas works fine), create an empty
   `RectTransform` anchored to the bottom corner and attach `DashChargeUi`.
   Leave **Charges Source** empty - it auto-finds the player's `DashCharges`.
3. **Optional, recommended:** create a new Physics2D layer named
   `PlayerDashing`. In **Project Settings -> Physics 2D -> Layer Collision
   Matrix**, uncheck every column where `PlayerDashing` intersects an enemy /
   enemy-projectile layer. `DashAbility` swaps the player's layer to this one
   for the duration of the i-frame window. If the layer doesn't exist the
   dash still works - it just can't physics-ignore enemy contact.

## Bindings

`DashInputHandler` builds an `InputAction` at runtime so we don't need to edit
`InputActions.inputactions`:

- **Keyboard:** Space
- **Gamepad:** South face button (A on Xbox, X on PlayStation)
- **Mobile:** double-tap of any movement axis within 0.25 s
  (release between taps must be at least back to 25 % stick, second tap must
  be in roughly the same direction as the first - dot >= 0.6)

To add more bindings without editing this file, populate
`DashInputHandler.extraButtonBindings` in the Inspector with any
`InputControl` path string (e.g. `<Touchscreen>/primaryTouch/tap`,
`<Gamepad>/leftShoulder`).

## I-frame contract

`PlayerHealth` cannot be modified, so invulnerability is provided two ways:

1. **`DashAbility.IsAnyInvincible`** - public static `bool`. A future patch to
   `PlayerHealth.ApplyDamage` can `if (DashAbility.IsAnyInvincible) return;`
   as its first line and get true logical invulnerability.
2. **Layer swap** - while dashing, the player's collider layer is swapped to
   `PlayerDashing` (configurable). If that layer is set up to not collide
   with enemy layers, contact damage is silently skipped because the trigger
   / collision never fires in the first place. The layer is restored when
   the dash ends.

Both mechanisms are active at the same time so the system still benefits from
whichever one the project ends up wiring up.

## Tuning knobs (defaults in brackets)

`DashAbility`:
- `dashSpeed` (**18**) - burst velocity in units/s.
- `dashDuration` (**0.18 s**) - how long the burst is held.
- `invincibilityFraction` (**1**) - fraction of dash that grants i-frames.
- `chargeRegenSeconds` (**2.5 s**) - per-charge regen time.
- `allowStationaryDash` (**true**) - if false, requires active move input.

`DashCharges`:
- `maxCharges` (**1**)

`DashTrail`:
- `ghostCount` (**5**), `ghostLifetime` (**0.4 s**), `tint` (cyan), `startAlpha` (**0.65**).

`DashShockwave`:
- `radius` (**1.6**), `impulse` (**4.5**), `affectMask` (everything).

## Upgrade hooks

All upgrade hooks are public so `UpgradeService` / `UpgradeData` can wire them
up without touching this folder.

| Upgrade                            | Where to plug in                                                                |
| ---------------------------------- | ------------------------------------------------------------------------------- |
| **Extra charges**                  | `DashCharges.SetMaxCharges(int newMax, bool grantNew = true)`                  |
| **Faster cooldown**                | `DashCharges.SetRegenScale(float scale)` (0.5 = twice as fast)                 |
| **Longer distance**                | `DashAbility.dashSpeed` and/or `dashDuration` (Inspector or via a future setter). Add a public `SetDistance(speed, duration)` if needed - this folder owns it. |
| **Refill on level-up / pickup**    | `DashCharges.Refill()`                                                          |
| **Damages-enemies-on-pass-through**| Subscribe to `DashAbility.OnDashStarted` and run a continuous `OverlapBox` along the dash path applying damage via the existing `EnemyBase.ApplyDamage`. Keep the implementation in this folder (e.g. a new `DashPiercingDamage.cs`) so the hard rule about not modifying other files still holds. |
| **Bigger shockwave / shockwave damage** | Tune `DashShockwave.radius` / `impulse`, or add a new sibling component that listens to `DashAbility.OnDashEnded` and damages enemies in the area. |

## Events

`DashAbility` exposes two events for other systems (FX, audio, haptics) to
hook into without coupling to it:

```csharp
public event Action<Vector2> OnDashStarted; // direction
public event Action          OnDashEnded;
```

Example - play a haptic on dash start without modifying this folder:

```csharp
GetComponent<DashAbility>().OnDashStarted += _ => HapticsService.LightTap();
```

## Notes

- `DashAbility.Update` re-applies the burst velocity every frame during the
  dash. This deliberately overrides any other velocity write (knockback,
  friction) for the dash duration so the dash always feels crisp.
- While dashing, `PlayerController.MoveSpeed` is temporarily set to 0 and
  restored on dash end. This stops `PlayerController.FixedUpdate` from
  overwriting the dash velocity with normal `_moveInput * moveSpeed`.
- The dash is disabled when `GameManager.Instance.State != Playing`, so
  pause / game-over / level-up automatically suppress it.
