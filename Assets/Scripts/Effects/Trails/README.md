# LoneFighter.Effects.Trails

Drop-in trail / afterimage / alt-lightning visuals for the LoneFighter arena
survivor scaffold. Everything in this folder is purely cosmetic — no gameplay
side-effects, no allocations on the hot path, no modifications to existing
systems. All components subscribe to / read from existing services
(`PoolService`, `PlayerController.Instance`) rather than replacing them.

## Files

| File | Purpose |
|---|---|
| `TrailProfile.cs` | ScriptableObject visual recipe: gradient, width curve, fade time, additive flag. One asset per weapon archetype. |
| `BulletTrailController.cs` | MonoBehaviour. Applies a `TrailProfile` to a `TrailRenderer` and resets correctly when the projectile returns to the pool. |
| `PlayerMovementTrail.cs` | Emits pooled afterimage sprites behind the player when its `Rigidbody2D.linearVelocity` exceeds a threshold. 20 Hz sampling, 0.4 s ghost lifetime by default. |
| `LightningArcRenderer.cs` | Alt chain-lightning visual: 5-7 jittered intermediate vertices, perpendicular perturbation, linear fade. API-compatible with the existing `LightningBolt`. |
| `Editor/TrailProfileGenerator.cs` | `LoneFighter -> FX -> Generate Trail Profiles` menu. Materializes the four starter profiles under `Assets/Data/TrailProfiles/`. |

## Wire-up

### 1. Generate the starter profiles

In the Unity editor, open
**LoneFighter -> FX -> Generate Trail Profiles**.

This creates (or updates in place):

```
Assets/Data/TrailProfiles/Trail_Pistol.asset
Assets/Data/TrailProfiles/Trail_SpreadShotgun.asset
Assets/Data/TrailProfiles/Trail_OrbitalBlade.asset
Assets/Data/TrailProfiles/Trail_ChainLightning.asset
```

Re-running the menu updates existing assets in place — prefab references survive.

### 2. Add trails to a projectile prefab

For each projectile prefab (pistol bullet, shotgun pellet, etc.):

1. Add a child GameObject named `Trail`.
2. Add a `TrailRenderer` component to that child.
3. Add the `BulletTrailController` component (it auto-finds the `TrailRenderer`).
4. Assign the matching `TrailProfile` asset.
5. *(Optional)* Assign an additive material override in the `Additive Material`
   slot. The same `Fx_Additive.mat` produced by `MaterialGenerator` works well.

Pool reset is automatic: `OnDisable` clears emission, `OnEnable` clears any
leftover vertices. The projectile's existing `Projectile.ReturnToPool()` path
disables the GameObject which propagates to the child trail.

### 3. Player movement trail

Drop a single `PlayerMovementTrail` MonoBehaviour anywhere in the Game scene
(it does not need to be parented to the player — it auto-binds via
`PlayerController.Instance` at `Start`, retrying each frame until the player
appears).

Defaults match the spec:

- Sample interval: **0.05 s**
- Velocity threshold: **3.5 units/s** (player base move speed is 5)
- Ghost lifetime: **0.4 s**
- Pool size: **12** (room for 0.6 s of history at 20 Hz)

If you crank `PlayerController.MoveSpeed` via upgrades, raise the threshold or
lower it so the trail only triggers on the "fast" feeling moments.

### 4. Lightning arc renderer (optional alternative to LightningBolt)

`LightningArcRenderer` is API-compatible with the existing `LightningBolt`
(same `Configure(start, end, lifetime)` signature). To use it instead of the
default bolt:

1. Create a new prefab with a `LineRenderer` + `LightningArcRenderer`.
2. Assign an additive material to the `LineRenderer`.
3. In `ChainLightningWeapon` inspector, swap the `lightningBoltPrefab` field
   to your new prefab.
4. *(Optional)* Toggle `Animate Jitter` on the renderer for an even noisier
   crackle.

The bolt is fully poolable — `PoolService.Get` -> `Configure(...)` -> the
renderer self-fades over `lifetime`, then the owning weapon (or a generic
auto-release helper) releases it back to the pool.

## Performance notes

- `BulletTrailController` does no per-frame work. The underlying `TrailRenderer`
  handles emission entirely on the Unity side.
- `PlayerMovementTrail` runs O(poolSize) per frame for the fade pass (12 slots)
  and amortizes one `EmitGhost` call every 0.05 s. Zero allocations on the hot
  path — the slot list and SpriteRenderer pool are built once.
- `LightningArcRenderer` makes `intermediateVertices` calls to `Random.Range`
  per `Configure`. With `animateJitter = false` (the default), there is no
  further random sampling after the bolt is configured.

## Hard rules respected

- All four scripts live under `Assets/Scripts/Effects/Trails/` in the
  `LoneFighter.Effects.Trails` namespace.
- The editor generator lives in the nested `Editor/` folder (still under
  this folder), so it is excluded from player builds automatically by Unity.
- No existing file is modified.
- `Rigidbody2D.linearVelocity` is used (Unity 6 API).
- UI / always-on systems use `Time.deltaTime`; nothing in this folder runs on
  scaled time except the projectile's own update which already obeys
  `Time.timeScale` for hit-stop sync.
