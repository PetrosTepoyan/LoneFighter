# LoneFighter.Combat.Elements

Elemental damage layer for LoneFighter. Tags weapons with an element, lets enemies resist or be
weak to elements, and inflicts an element-specific status effect on hit (the status MonoBehaviours
themselves live in `Assets/Scripts/Combat/Status/`, owned by a sibling agent).

All types live under the namespace `LoneFighter.Combat.Elements`.

## Contents

| File | Role |
| --- | --- |
| `Element.cs` | The `Element` enum (`Physical`, `Fire`, `Ice`, `Lightning`, `Poison`). |
| `ElementProfile.cs` | `ScriptableObject` describing one element: display name, HDR tint, particle prefab, status-effect type name (e.g. `"BurnEffect"`). |
| `ElementRegistry.cs` | `ScriptableObject` holding the list of all `ElementProfile`s plus a static `Get(Element)` accessor. Drop the asset into a `Resources/` folder for autoload, or reference it from a scene/prefab. |
| `ElementalDamage.cs` | `struct ElementalDamage { float baseDamage; Element element; float elementalMultiplier; }`. |
| `WeaponElementBinding.cs` | `MonoBehaviour` on a weapon. `Get()` returns the weapon's element. |
| `EnemyResistance.cs` | `MonoBehaviour` on an enemy. `GetMultiplier(Element)` returns per-element resistance (`<1` resistant, `>1` weak, default `1`). |
| `ElementalDamageDispatcher.cs` | Static `Apply(...)` helper that reads resistance, calls `EnemyBase.ApplyDamage`, then inflicts the element's status effect (resolved via reflection). |
| `Editor/ElementProfileGenerator.cs` | Editor menu `LoneFighter / Combat / Generate Element Profiles`. Creates the five canonical profiles and a registry at `Assets/Data/Elements/`. Idempotent. |

## Usage

```csharp
using LoneFighter.Combat.Elements;

// On a projectile / weapon hit handler:
void OnHit(GameObject enemyGo, float baseDamage)
{
    var element = TryGetComponent<WeaponElementBinding>(out var b) ? b.Get() : Element.Physical;
    ElementalDamageDispatcher.Apply(enemyGo, baseDamage, element, sourceWeapon: gameObject);
}
```

The dispatcher does **all** of the following:

1. Looks up `EnemyResistance` on the target (defaults to a 1.0 multiplier when absent).
2. Looks up `WeaponElementBinding.Multiplier` on the source weapon (defaults to 1.0).
3. Computes `final = baseDamage * weaponMul * resistanceMul`.
4. Reflectively invokes `EnemyBase.ApplyDamage(...)` (or any duck-typed `ApplyDamage` method) on the target.
5. Spawns the element's particle prefab (if any).
6. If the active `ElementProfile.StatusEffectTypeName` resolves to a `MonoBehaviour` type anywhere in the loaded assemblies, either:
   - calls its public parameterless `Refresh()` method if the component already exists, otherwise
   - `AddComponent`s a fresh instance.

This last step is reflective so the sibling Status agent can ship its types independently without
forcing an assembly-reference cycle.

## Editor workflow

1. Open Unity and pick **LoneFighter -> Combat -> Generate Element Profiles**.
2. Confirm `Assets/Data/Elements/` now contains:
   - `Element_Physical.asset` ... `Element_Poison.asset`
   - `ElementRegistry.asset`
3. Move `ElementRegistry.asset` into any `Resources/` folder (or reference it from a bootstrap scene)
   so `ElementRegistry.Active` resolves at runtime.

## Conventions

- HDR tints are intentionally above 1.0 on some channels to read well under URP 2D bloom.
- Status-effect names are *type names*, not asset names. Examples: `BurnEffect`, `ChillEffect`,
  `ShockEffect`, `PoisonEffect`. `Physical` is intentionally empty (no status).
- Treat `Element` enum values as persisted ordinals - do **not** renumber.

## Assemblies

- `LoneFighter.Combat.Elements.asmdef` - runtime, no external references.
- `Editor/LoneFighter.Combat.Elements.Editor.asmdef` - editor-only, references the runtime asmdef.
