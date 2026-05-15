# Weapon Evolutions

Vampire-Survivors-style super-weapon system. When a weapon hits its max upgrade rank AND
the player owns a specific paired passive upgrade, the next level-up offers an evolution
into a super-weapon.

All scripts live in `Assets/Scripts/Weapons/Evolutions/` under the namespace
`LoneFighter.Weapons.Evolutions`. No existing files were modified.

## Files

| File | Role |
| --- | --- |
| `WeaponEvolutionRecipe.cs` | ScriptableObject: one recipe = base weapon + passive name + evolved weapon. |
| `EvolutionRecipeRegistry.cs` | ScriptableObject list of every recipe in the build. Static `Instance` accessor. |
| `EvolutionService.cs` | Runtime singleton. Subscribes to `PlayerLevel.OnLevelUp`, evaluates recipes, raises events. |
| `EvolutionModal.cs` | UI modal shown on `OnEvolutionAvailable`. Sets `Time.timeScale = 0`, "Evolve" / "Skip" buttons. |
| `EvolutionToast.cs` | HUD badge shown on `OnEvolutionReady`. Pulses "READY TO EVOLVE" near the weapon slot. |
| `Editor/EvolutionGenerator.cs` | Menu `LoneFighter -> Weapons -> Generate Evolution Recipes`. Creates 3 starter recipes + evolved WeaponData SOs. |

## Quick start

1. In Unity, open **LoneFighter -> Weapons -> Generate Evolution Recipes**. This writes:
   - `Assets/Data/Weapons/Evolved/HandCannon.asset`
   - `Assets/Data/Weapons/Evolved/AutoShotgun.asset`
   - `Assets/Data/Weapons/Evolved/TeslaCoil.asset`
   - `Assets/Data/Evolutions/Evolution_*.asset` (the three recipes)
   - `Assets/Data/Evolutions/EvolutionRecipeRegistry.asset`
   - `Assets/Resources/EvolutionRecipeRegistry.asset` (alias for `Resources.Load`).
2. Inspect each evolved WeaponData and adjust stats - the generator seeds plausible values
   by copying from the base weapon and applying multipliers, but final tuning is yours.
3. Place an `EvolutionModal` panel in the gameplay UI scene (inactive by default), wire its
   `Evolve` / `Skip` buttons and text labels.
4. Add one `EvolutionToast` per weapon HUD slot. Optionally set `filterWeapon` to the
   matching base WeaponData so a slot only lights up for its own recipe.

`EvolutionService.Instance` is created on first scene load (`RuntimeInitializeOnLoadMethod`)
and self-subscribes to `PlayerLevel.OnLevelUp`. No additional bootstrap wiring is required.

## Authoring a new recipe (3 fields)

1. **Create the evolved WeaponData** asset (`Create -> LoneFighter -> Weapon Data` or
   equivalent) under `Assets/Data/Weapons/Evolved/`.
2. **Create the recipe asset:** right-click in `Assets/Data/Evolutions/` and choose
   **Create -> LoneFighter -> Evolutions -> Weapon Evolution Recipe**.
3. Fill three fields:
   - `Base Weapon` -> drag the base WeaponData.
   - `Required Passive Upgrade Name` -> exact display name of the UpgradeData (case-insensitive).
   - `Evolved Weapon` -> drag the new evolved WeaponData.
4. Add the recipe to the `EvolutionRecipeRegistry` asset's `recipes` list.

That's it. Next time the player satisfies the recipe and levels up, the modal will offer
the evolution.

## Events

- `EvolutionService.OnEvolutionAvailable(WeaponEvolutionRecipe)` -- fired on level-up when a
  recipe is satisfied. Drives `EvolutionModal`.
- `EvolutionService.OnEvolutionReady(WeaponEvolutionRecipe)` -- fired the first time a
  recipe becomes satisfied (not gated by level-up). Drives `EvolutionToast`.
- `EvolutionService.RefreshReadyState()` -- call after passive pickups so the toast can
  fire immediately rather than waiting for the next level-up.

## Known limitations

- `EvolutionService` resolves the existing `WeaponInventory` / `UpgradeService` /
  `PlayerLevel` APIs through reflection so this folder can compile and run without
  modifying any existing files. If those classes don't expose a public
  `RemoveWeapon(WeaponData)` API, the modal will grant the evolved weapon but the base
  weapon will keep firing. A one-time warning is logged: see
  `EvolutionModal.OnEvolveClicked`. The cleanest fix (out of scope for this task) is to
  add a `RemoveWeapon` overload to `WeaponInventory`.
- `EvolutionRecipeRegistry.Instance` prefers an asset at
  `Assets/Resources/EvolutionRecipeRegistry.asset`. The generator creates this alias for
  you; if you build the registry by hand, drop a copy under `Resources/`.
- Stat tuning in `EvolutionGenerator` is best-effort by field name (`damage`,
  `baseDamage`, `cooldown`, etc.). If your `WeaponData` uses different field names the
  numbers will be left at the base weapon's values - tune by hand in the inspector.
