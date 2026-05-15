# Weapon HUD

Horizontal strip at the top of the screen showing each active weapon with its
icon, a radial-fill cooldown indicator, and an upgrade-count badge. Designed to
sit directly under the level/HP bar in portrait, mobile-first layouts.

Namespace: `LoneFighter.UI.WeaponHud`.

## Files

| File | Type | Role |
|---|---|---|
| `WeaponHudController.cs` | `MonoBehaviour` | Top-level UI root. Polls `PlayerController.Instance`'s `WeaponInventory` once per second; rebuilds the slot list only when the weapon set changes. |
| `WeaponSlotUi.cs` | `MonoBehaviour` | One slot. Reads cooldown from `WeaponBase` and updates the radial fill; detects firing and pulses the flash. |
| `WeaponBaseExtensions.cs` | `static class` | Reflection helpers that read the protected `_cooldownTimer`, `_cooldownMultiplier`, `_damageMultiplier`, `_projectileSpeedMultiplier`, `_bonusPierce` fields on `WeaponBase`. No mutation — the HUD only *observes*. |
| `WeaponLevelBadge.cs` | `MonoBehaviour` | The bottom-right "+N" badge. Computes a coarse upgrade tier from the reflected multipliers. |
| `WeaponHudFlash.cs` | `MonoBehaviour` | 0.08 s white-pulse on fire. Uses unscaled time so it stays snappy through any time-scale wobble. |
| `WeaponIconRegistry.cs` | `ScriptableObject` | `(weaponName → Sprite)` map with a default fallback. `Get(WeaponData)` is the single lookup entry-point used by `WeaponSlotUi`. |
| `../../../Editor/UI/WeaponHud/WeaponIconRegistryGenerator.cs` | `static class` | Adds `LoneFighter → UI → Generate Weapon Icon Registry`. Creates `Assets/Data/UI/WeaponIconRegistry.asset` and procedurally generates 64×64 circle PNGs for any weapon whose `WeaponData.icon` is empty. |

## How the cooldown number is derived

`WeaponBase._cooldownTimer` is `protected`, and the gameplay code we're not
allowed to touch never exposes an event when a weapon fires. The HUD therefore
*reads* (never writes) the timer via reflection:

1. `WeaponBaseExtensions.TryGetCooldownTimer(weapon)` returns the protected
   `_cooldownTimer` value, or `NaN` if reflection fails (e.g. WeaponBase is
   refactored).
2. `WeaponSlotUi.Update()` computes `progress = 1 - timer / (data.cooldown * cooldownMult)`.
3. A "fire" is detected as a sharp upward edge on the timer (it was ~0 last
   frame, now ~ `effectiveCooldown`). On detection we call
   `WeaponHudFlash.Pulse()`.
4. If reflection fails, the slot falls back to a local elapsed-time clock keyed
   off `WeaponData.cooldown`. Less accurate against runtime cooldown multipliers,
   but never visually wrong.

This whole "observe via reflection" trick lives behind `WeaponBaseExtensions`
so the rest of the HUD can pretend `WeaponBase` has clean `CooldownTimer` /
`DamageMultiplier` / `BonusPierce` properties.

## Upgrade-count badge

`WeaponInventory.Apply*` mutates `WeaponBase`'s protected multipliers in 0.1
steps (10% damage, 10% projectile speed, 10% cooldown reduction) plus integer
pierce. There is no externally visible "upgrade count", so the badge counts
discrete 0.1-step deltas across all four multipliers and shows the sum as
`"+N"`. At tier 0 the badge is hidden.

`WeaponLevelBadge.perStep` is exposed in the inspector so you can retune if the
upgrade-system step ever changes.

## Wiring it into a scene

1. Run **`LoneFighter → UI → Generate Weapon Icon Registry`** once.
   That creates `Assets/Data/UI/WeaponIconRegistry.asset` and generates default
   placeholder circle sprites for every `WeaponData` in the project.
2. In the HUD canvas (the one that already hosts `HudController`), add a child
   GameObject under the HP/level bar. Anchor it top-center, full-width.
3. Add a `HorizontalLayoutGroup` (Child Alignment = MiddleCenter, spacing ~12)
   and a `ContentSizeFitter` (Horizontal Fit = PreferredSize) — this gives a
   centered, variable-width strip.
4. Add the `WeaponHudController` component. Drag the `HorizontalLayoutGroup`'s
   `RectTransform` into **Slot Root**. Drag the registry asset into
   **Icon Registry**.
5. Author one **slot prefab**:
   * Empty UI GameObject (~64×64) with an `Image` child for the **Icon**.
   * A second `Image` child set to `Image Type = Filled`, `Fill Method = Radial 360`,
     `Fill Origin = Top`, `Clockwise = on`. This is the **Cooldown Fill**.
   * A small `TMP_Text` plus optional background `Image` in the bottom-right
     corner — wrap them with a `WeaponLevelBadge` and assign the **Label** and
     **Background** references.
   * A `WeaponHudFlash` on the slot root, targeting the icon Image.
   * A `WeaponSlotUi` on the slot root with all four references wired
     (icon image, cooldown fill, level badge, flash).
6. Drag the slot prefab into the controller's **Slot Prefab** field.

The controller leaves `Update()` empty by design — all per-frame work happens
inside the slots, which are the only things that need to tick every frame.

## Performance notes

* `WeaponHudController` polls the inventory once per second via a coroutine and
  rebuilds slots **only** when the list identity actually changes.
  Day-to-day cost: a `Count` compare and an index walk. No allocations after
  steady-state.
* `WeaponSlotUi.Update()` does a handful of float operations and one reflected
  field read per frame. The `FieldInfo` is cached statically — no per-call
  reflection lookup cost.
* `WeaponLevelBadge.Refresh()` early-outs when the integer tier hasn't changed,
  so the TMP text only re-strings on actual upgrades.
* `WeaponHudFlash` only runs a coroutine while a pulse is active.

## Constraints honored

* **No modifications outside this folder.** Reading the protected fields on
  `WeaponBase` uses reflection from this folder, so `WeaponBase.cs` is
  untouched.
* **New files only.** All gameplay scripts (`WeaponInventory`, `WeaponBase`,
  `WeaponData`, `PlayerController`) are unmodified.
