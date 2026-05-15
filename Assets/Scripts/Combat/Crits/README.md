# Crits

Externally consultable critical-hit system. Lives entirely in
`Assets/Scripts/Combat/Crits/` under the `LoneFighter.Combat.Crits` namespace and
does **not** touch any existing weapon, upgrade, or enemy code. Integrations are
opt-in.

## Files

| File | Role |
| --- | --- |
| `CritProfile.cs` | `ScriptableObject` with the base crit chance (default 5%), base multiplier (default 2.0), and an optional list of per-weapon overrides keyed by `WeaponData.displayName`. |
| `CritService.cs` | Singleton `MonoBehaviour`. Owns run-time crit chance/multiplier deltas, exposes `Roll(...)`, listens to the `OnCritChanceModified(float delta)` and `OnCritMultiplierModified(float delta)` events. |
| `CritResolver.cs` | Static helper. `ResolveDamage(WeaponData data, float baseDamage, out bool wasCrit)`. The thing a weapon calls. Also fires the sibling FX track via reflection so it gracefully degrades when not installed. |
| `CritUpgradeAdapter.cs` | Singleton `MonoBehaviour` that translates upgrade choices (`UpgradeData` by name, or arbitrary external string-named kinds) into events on `CritService`. |
| `EnemyCritWeakness.cs` | Optional `MonoBehaviour` for enemy prefabs that grants a per-target extra crit chance. |

## Scene setup

1. Create a `CritProfile` asset via **Assets &rarr; Create &rarr; LoneFighter &rarr; Combat &rarr; Crit Profile**.
   The defaults (5% chance / 2.0x) work out of the box. Add per-weapon
   overrides in the **Per-weapon overrides** list as needed (entries match
   `WeaponData.displayName` case-insensitively).
2. Add `CritService` to a long-lived GameObject (e.g. the same root as the
   existing `UpgradeService` / `FxService`). Assign the `CritProfile` asset.
   If you leave the field empty the service will try to load a
   `Resources/CritProfile` asset and finally fall back to an in-memory default.
3. Optionally add `CritUpgradeAdapter` to the same GameObject. Tweak its
   `critChanceUpgradeNames` / `critMultiplierUpgradeNames` lists so they match
   the `displayName` of any crit-related `UpgradeData` assets you author.
4. Optionally drop `EnemyCritWeakness` on individual enemy prefabs.

No prefab edits or scene migrations are required beyond the above.

## How a new weapon opts in

In the new weapon's `Fire` path, replace the raw `data.damage` use with the
resolver. Example:

```csharp
using LoneFighter.Combat.Crits;

public class MyNewWeapon : WeaponBase
{
    protected override void Fire()
    {
        // ... pick target, position, direction ...

        var baseDamage = data.damage;
        var weaknessBonus = EnemyCritWeakness.GetBonus(target.gameObject);
        var dealt = CritResolver.ResolveDamage(
            data, baseDamage, target.transform.position, weaknessBonus, out var wasCrit);

        target.ApplyDamage(dealt);
    }
}
```

`CritResolver`:
* Calls `CritService.Instance.Roll(data.displayName, weaknessBonus, out mult)`.
* Multiplies the base damage when the roll succeeds.
* Fires `LoneFighter.Effects.CriticalHits.CritFxService.OnCrit(...)` via
  reflection if that sibling module is present.

If `CritService` isn't in the scene the resolver simply returns `baseDamage`
unchanged and reports `wasCrit = false`.

## How to retrofit *existing* weapons without modifying them

The hard rule is that we can't edit existing weapon scripts. Two recommended
options:

### Option A &mdash; upgrade-flag approach (preferred)

Mint a new `UpgradeData` whose `displayName` is "Bloodthirst" (or anything in
`critChanceUpgradeNames`). Add it to `UpgradeService.pool`. When the player
picks it, the existing `UpgradeService.RecordChoice(...)` path runs as usual,
and you forward the same `UpgradeData` to the adapter from one (any) external
hook:

```csharp
// Wherever the level-up UI confirms a choice (new code, not an edit):
CritUpgradeAdapter.Instance?.ApplyUpgrade(chosenUpgrade);
```

That call routes the upgrade through `CritService.RaiseCritChanceModified(...)`
which mutates the global chance bonus. Every weapon that *does* call
`CritResolver.ResolveDamage` gains the upgrade for free; weapons that don't are
unaffected. This is intentional &mdash; the upgrade reads on the tin as "increases
crit chance for crit-capable weapons", which matches typical genre conventions.

### Option B &mdash; damage decorator

Add a sibling component to the player that listens to weapon-fire events (if
your weapon emits one) and post-multiplies damage. Less invasive than editing
weapon code, but only viable if there is a global damage-applied hook to
intercept.

## UpgradeKind extension

Because the existing `UpgradeKind` enum is closed, this module supports two
forwarding strategies that *don't* require editing it:

* **Name matching** &mdash; configure `critChanceUpgradeNames` on
  `CritUpgradeAdapter` so any `UpgradeData` whose `displayName` matches is
  routed automatically. This is the recommended path for designers who can
  freely add new `UpgradeData` assets.
* **External-kind tokens** &mdash; if the project eventually does grow an
  `UpgradeKind.PlayerCritChance` value, the integrator can call
  `CritUpgradeAdapter.Instance.ApplyExternalKind("PlayerCritChance", magnitude)`
  from the new dispatch site. The token list is editable on the component.

## Public event contract

```csharp
// Defined on CritService:
public static event Action<float> OnCritChanceModified;     // delta (e.g. +0.05)
public static event Action<float> OnCritMultiplierModified; // delta (e.g. +0.5)
```

Raise them via:

```csharp
CritService.RaiseCritChanceModified(0.05f);   // +5% crit chance
CritService.RaiseCritMultiplierModified(0.5f); // +0.5x crit multiplier
```

`CritService` itself subscribes to both events and accumulates the deltas into
its `ChanceBonus` / `MultiplierBonus`. Call `CritService.Instance.ResetRun()`
at the start of each run.
