# LoneFighter.Effects.CriticalHits

Visual + audio feedback layer that makes a critical hit feel like a JACKPOT.

This namespace is **strictly cosmetic**. It does not decide whether a hit is a
crit or how much extra damage to apply — that lives in the sibling crit-roll
system under `Assets/Scripts/Combat/Crits/`. When another system has already
classified a hit as critical, it calls one method here:

```csharp
CritFxService.Instance.OnCrit(worldPos, finalDamage);
```

That single call triggers every component in this folder.

## Files

| File | Role |
| --- | --- |
| `CritFxService.cs` | Singleton facade. The only entry point external systems should touch. |
| `CritDamagePopup.cs` | TextMeshPro popup. Larger than the regular damage number, yellow→red color sweep, `0 → 1.5 → 1.2` bouncy scale pulse, appends `" CRIT!"`. Pooled via `PoolService`. |
| `CritFlash.cs` | Full-screen UI Image overlay, 0.12s unscaled-time flash. Stretch a white (or radial-gradient) Image across a Canvas and slot it. |
| `CritFreezeFrame.cs` | Static helper. 0.04s micro hit-stop. Defers to `FxService.HitStop` when present, otherwise self-manages a fallback runner. |
| `CritImpactBurst.cs` | Pooled radial spark particles at the hit position. Distinct from `FxService.ProjectileImpact` so it can be tuned independently. Has a procedural fallback for editor testing without a prefab. |
| `Editor/CritPopupPrefabGenerator.cs` | Editor menu — see below. |

## Editor menu

`LoneFighter → FX → Generate Crit Popup Prefab`

Builds `Assets/Prefabs/UI/CritDamagePopup.prefab` via
`PrefabUtility.SaveAsPrefabAsset`. The prefab is a world-space TMP text
GameObject with `CritDamagePopup` attached and sorting order 100. Re-running
the menu overwrites the prefab in place (idempotent), so any references from
scenes/prefabs survive regeneration.

## Scene wiring

Minimum scene set-up to make crits feel good:

1. Run `LoneFighter → FX → Generate Crit Popup Prefab`.
2. Add a GameObject `CritFxService` and attach `CritFxService`. Drop the
   generated prefab into the **Crit Popup Prefab** slot.
3. Under your main UI Canvas, add a Stretch-anchored child Image, set
   raycast target off, and attach `CritFlash`. (Optional but recommended:
   use a radial-gradient sprite so the flash reads as a directional pop.)
4. Add a GameObject `CritImpactBurst` and attach `CritImpactBurst`. Assign a
   ParticleSystem prefab (or leave empty to use the procedural fallback).
5. Ensure a `FxService` instance exists in the scene if you want camera shake
   and centralized hit-stop. Both fall back gracefully if it's missing.

## Design tuning knobs

All durations / colors / curves are serialized fields on their respective
components — no need to recompile to retune. Notable defaults pulled from the
brief:

- Popup scale pulse: `0 → 1.5 → 1.2` with a small rebound at `t=0.32 → 1.15`.
- Flash duration: `0.12s` unscaled.
- Hit-stop duration: `0.04s` (constant `CritFreezeFrame.DEFAULT_DURATION`).
- Heavy camera shake is routed through `FxService.HeavyShake()` so the global
  shake tuning still applies.

## Hard constraint

This folder is the **only** location these scripts may live. The other
constraint from the agent brief: never modify files outside this folder. The
namespace `LoneFighter.Effects.CriticalHits` is reserved for this layer.
