# Explosions

Tiered explosion library for LoneFighter. One call (`ExplosionService.Instance.Detonate`) dispatches across every juice layer: pooled flash, additive shockwave, radial debris, lingering smoke, full-screen UI flash, Cinemachine shake, hit-stop, haptics, and optional radius damage. Six tiers ramp from `Tiny` (16 particles, no shake) to `Nuke` (400+ particles, triple shake, screen white-out).

Namespace: `LoneFighter.Effects.Explosions`. All files under `Assets/Scripts/Effects/Explosions/`.

## Quick start

1. **Generate profiles** — Unity menu `LoneFighter → FX → Generate Explosion Profiles`. Writes six `ExplosionProfile` SOs under `Assets/Data/Explosions/`.
2. **Add a service** — empty GameObject in `Game.unity`, attach `ExplosionService`. Drag the six generated profiles into its `profiles` array.
3. **Fire one off** — anywhere in gameplay code:

   ```csharp
   ExplosionService.Instance.Detonate(transform.position, ExplosionTier.Medium);

   // With AoE damage (Physics2D.OverlapCircle, linear distance falloff):
   ExplosionService.Instance.Detonate(hitPoint, ExplosionTier.Large, damage: 50f);
   ```

The service auto-creates fallback runtime "prefabs" if you leave the prefab slots empty, so it works from a clean checkout without any prefab authoring. Drop in your own prefabs whenever you want art.

## Tiers

| Tier | Particles | Ring radius | Shake calls | Hit-stop | Screen flash |
|------|-----------|-------------|-------------|----------|--------------|
| Tiny | 16 | 0.6u | 0 | 0 | none |
| Small | 32 | 1.1u | 0 | 0.02s | none |
| Medium | 64 | 1.8u | 1 | 0.04s | none |
| Large | 130 | 2.8u | 1 | 0.06s | warm 0.30 |
| Mega | 230 | 4.2u | 2 | 0.10s | warm 0.55 |
| Nuke | 420+ | 6.5u | 3 | 0.18s | white 0.95 |

Edit the SO assets to retune any of these knobs without recompiling.

## Files

| File | Purpose |
|------|---------|
| `ExplosionTier.cs` | `Tiny..Nuke` enum |
| `ExplosionProfile.cs` | ScriptableObject — every per-tier knob |
| `ExplosionService.cs` | Singleton facade. `Detonate(pos, tier, damage)` |
| `ExplosionFlash.cs` | Pooled white-hot core (decay-power animated) |
| `ExplosionShockwave.cs` | Pooled additive expanding ring |
| `ExplosionDebris.cs` | Pooled spinning sprite chunk launched radially |
| `ExplosionSmoke.cs` | Pooled lingering grey puff (1-3s) |
| `ScreenFlashOverlay.cs` | Full-screen UI flash. Auto-creates Canvas + Image if absent |
| `ExplosionPrimitiveSprites.cs` | Procedural fallback sprites (white square, soft disc, ring) |
| `ChainExplosion.cs` | Sequence of detonations stepping outward — "everything dies" |
| `BossDeathMegaExplosion.cs` | 6-stage cinematic: slowmo → 3 mediums → pause → nuke → whiteout → resume |
| `Editor/ExplosionProfileGenerator.cs` | `LoneFighter → FX → Generate Explosion Profiles` menu |

## Special moments

**Chain (everything dies):**

```csharp
ChainExplosion.Run(startPos, ExplosionTier.Medium, linkCount: 8, secondsBetweenLinks: 0.06f);
```

**Boss death cinematic:**

```csharp
BossDeathMegaExplosion.Play(boss.transform.position);
```

Both are static convenience helpers — they spawn a temporary host GameObject and auto-dispose at the end.

## Hard rules respected

- No existing file modified.
- New files only under `Assets/Scripts/Effects/Explosions/` (namespace `LoneFighter.Effects.Explosions`).
- All transient spawns route through `PoolService` when available.
- Unity 6 API only — no obsolete `Object.FindObjectOfType` overloads.
- Mobile-friendly: zero-allocation overlap buffer, pooled visuals, capped particle counts per tier.
