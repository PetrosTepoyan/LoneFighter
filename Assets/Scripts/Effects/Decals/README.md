# LoneFighter — Decal System

Floor decals (blood splatters, scorch marks, bullet holes) that linger after combat
events. Hard performance cap so they stay mobile-friendly even at 120Hz.

Namespace: `LoneFighter.Effects.Decals`
Location:  `Assets/Scripts/Effects/Decals/`

## Files

| File | Purpose |
| --- | --- |
| `DecalRequest.cs` | Struct describing a single decal spawn (position, rotation, sprite, tint, fade, alpha, scale). |
| `DecalManager.cs` | Singleton MonoBehaviour. Owns a fixed-size ring of pooled `SpriteRenderer`s. FIFO recycling once the cap is exceeded. Linear fade in `Update`. |
| `DecalSpriteLibrary.cs` | Inspector-assignable holder for the three sprite arrays (blood / scorch / bullet). |
| `BloodSplatter.cs` | Static helper. Red tint, ~10 s fade, random rotation + scale. |
| `ScorchMark.cs` | Static helper. Dark grey tint, ~12 s fade, scaled to AOE radius. |
| `BulletHole.cs` | Static helper. Small floor chip, ~6 s fade. |
| `DecalAutoSubscriber.cs` | Top-level glue. Polls `EnemyRegistry` deltas per frame to detect deaths; spawns blood at each enemy's last position. Probes `BomberBehavior` via `TryGetComponent` and adds a scorch on bomber kills (graceful no-op if absent). |
| `Editor/DecalSpriteGenerator.cs` | Editor menu `LoneFighter → FX → Generate Decal Sprites`. Procedurally writes 4 blood splatters, 2 scorch marks, 2 bullet holes as PNGs under `Assets/Sprites/Generated/Decals/`, configured at PPU 32, Point filter, Center pivot. |

## Setup checklist (in the Editor)

1. `LoneFighter → FX → Generate Decal Sprites` — generates the 8 placeholder PNGs.
2. In the **Game** scene, create an empty GameObject (e.g. `DecalSystem`) and add:
    - `DecalManager` — set `maxDecals` (default 50) and `sortingOrder` (default `-50`, below enemies).
    - `DecalSpriteLibrary` — assign the four `Blood_*` to `Blood Splatters`, both `Scorch_*` to `Scorch Marks`, both `Bullet_*` to `Bullet Holes`.
    - `DecalAutoSubscriber` — keep defaults to spawn blood on every kill + scorch on bomber kills.
3. Press play. Kill some enemies. Floor stains accumulate; the 51st replaces the oldest.

## Design notes

- **Hard cap = 50** decals. Mobile GPUs can chew through 50 small additive sprites without breaking a sweat, and the ring stops the worst-case "death blob spam" from causing pop / fill-rate cliff.
- **No GC after warm-up.** All slots are allocated in `Awake`; `Spawn` only assigns fields. The auto-subscriber uses pre-warmed `Dictionary`/`HashSet`/`List` scratch containers.
- **Sort below enemies.** `SpriteRenderer.sortingOrder = -50` by default (`StarfieldBackground` already uses `-100`, so decals sit between background and gameplay).
- **Hit-stop friendly.** Fades use `Time.deltaTime` (scaled), so decals freeze when the game pauses — feels right for floor stains.
- **No existing files modified.** The auto-subscriber detects deaths by polling `EnemyRegistry.Enemies` and diffing the live set against the previous frame's snapshot, then resolving each death to its cached last position. Bomber-specific scorch marks are obtained via `TryGetComponent<BomberBehavior>()` on first sighting of an enemy — if the component isn't there (or is renamed in the future), the system silently degrades to blood-only.

## Calling the API directly

```csharp
using LoneFighter.Effects.Decals;

BloodSplatter.Spawn(transform.position);                   // default red, 10s fade
ScorchMark.Spawn(explosionCenter, radiusScale: 1.2f);      // bigger scorch for big bomb
BulletHole.Spawn(impactPoint);                             // small chip

// Or fully custom:
DecalManager.GetOrCreate().Spawn(new DecalRequest
{
    position    = pos,
    rotation    = Quaternion.Euler(0, 0, 45),
    sprite      = mySprite,
    tint        = new Color(0.4f, 0.7f, 0.2f),
    fadeSeconds = 5f,
    maxAlpha    = 0.8f,
    scale       = 1.5f,
});
```
