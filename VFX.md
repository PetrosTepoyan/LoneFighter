# LoneFighter — VFX Generators

This project ships with one-click Editor menu generators that build every visual-effect asset
`FxService` expects. Instead of the manual particle-system / material / volume-profile work
listed in `SETUP.md` sections 10, 11, and 16, you can now run a menu item and get the same
output in seconds.

All generators live under `Assets/Editor/Fx/` in namespace `LoneFighter.EditorTools.Fx`.

## What gets generated

| Menu item | Output |
| --- | --- |
| `LoneFighter > FX > Generate FX Prefabs` | `Assets/Prefabs/Fx/Fx_EnemyExplosion.prefab`, `Fx_ProjectileImpact.prefab`, `Fx_PlayerHit.prefab`, `Fx_XpPickup.prefab`, `Fx_LevelUp.prefab` |
| `LoneFighter > FX > Generate Additive Material` | `Assets/Settings/Fx_Additive.mat` + `Assets/Settings/Fx_SoftCircle.png` (a 64x64 soft-circle texture used as the particle base map) |
| `LoneFighter > FX > Generate Post-Processing Profile` | `Assets/Settings/PostProcessing.asset` (a `VolumeProfile` with Bloom / Vignette / Chromatic Aberration / Color Adjustments overrides) |
| `LoneFighter > FX > Generate Cinemachine Rig` | `Assets/Prefabs/CM_PlayerFollow.prefab` (CinemachineCamera + CinemachineFollow) and `Assets/Prefabs/Fx/FxRig.prefab` (FxService + CinemachineImpulseSource) |

The `Generate FX Prefabs` menu transparently calls the material generator on first run, so you
do not need to run them in a specific order — generating prefabs alone is enough to get all
particle assets plus the shared material and texture.

## Menu items, in order, for a fresh project

1. `LoneFighter > FX > Generate FX Prefabs` — builds the soft-circle texture, the additive
   material, and all five FX prefabs.
2. `LoneFighter > FX > Generate Post-Processing Profile` — builds the URP `VolumeProfile`.
3. `LoneFighter > FX > Generate Cinemachine Rig` — builds the `CM_PlayerFollow` camera prefab
   and the `FxRig` prefab.

After running them, the only manual steps left are:

- Drag `CM_PlayerFollow.prefab` into `Game.unity` and set its **Tracking Target** to the Player.
- Drag `FxRig.prefab` into `Game.unity` under your `Systems` parent and wire the five
  `Fx_*.prefabs` and a `DamagePopup` prefab into its serialized slots.
- Add a `CinemachineBrain` to your Main Camera and a `CinemachineImpulseListener` for screen shake.
- Add a `Global Volume` (Component > Volume > Global Volume) and drag
  `Assets/Settings/PostProcessing.asset` into its Profile field.
- In your URP Asset, tick **HDR** and **Post Processing**.

That replaces the manual setup in `SETUP.md` sections 10, 11, and 16.

## Each prefab at a glance

All prefabs share:

- World-space simulation (so they stay where they spawned even if the spawner moves).
- Stop Action = Disable (works with `ParticleAutoRelease` and `PoolService` for pooling).
- The shared `Fx_Additive` material with HDR-bright start colors so Bloom kicks in.
- A `ParticleAutoRelease` component so the pool reclaims the GameObject when emission ends.

| Prefab | Burst | Lifetime | Notes |
| --- | --- | --- | --- |
| `Fx_EnemyExplosion` | 24 | 0.4 s | Speed 4-8, white > HDR orange > red > transparent |
| `Fx_ProjectileImpact` | 8 | 0.18 s | Stretched billboard sparks, white > yellow > transparent |
| `Fx_PlayerHit` | 1 + 16 | 0.25 s + 0.3 s | Large red flash that scales 0 > 2 > 1, plus a yellow spark burst on a child PS |
| `Fx_XpPickup` | 6 | 0.3 s | Green > cyan tint, upward drift via velocityOverLifetime |
| `Fx_LevelUp` | 60 | 0.8 s | Ring burst (Circle shape, Arc Mode = Loop), stretched billboard, yellow > cyan, dampened expansion |

## How to tweak

Particle counts, speeds, sizes, colors, lifetimes, HDR intensities, etc. all live as plain
literals in `FxPrefabGenerator.cs`. Open the file, edit the number you want, re-run the menu —
the prefabs overwrite in place because `PrefabUtility.SaveAsPrefabAsset` is idempotent.

Color intensities use a small helper `BuildHDRColor(baseColor, intensity)`. Raise the intensity
to make a color bloom harder. Drop it (or set it to 1.0) for muted hits. The colors are stored
linearly so an intensity of 5 means "5x as bright as white" in the lit framebuffer — this is the
brightness that survives Bloom's threshold of 0.9 and produces the streaks.

Swapping the material is also one-line: change `MaterialGenerator.GetOrCreate()` to return a
different material asset, or just point the prefabs' renderers at a hand-tuned material in the
inspector. You can run `LoneFighter > FX > Generate Additive Material` on its own to rebuild
just `Fx_Additive.mat` without touching prefabs.

The soft-circle texture (`Assets/Settings/Fx_SoftCircle.png`) is generated procedurally as a
radial gradient. Replace the PNG with your own art if you want a different particle shape — the
generator will not overwrite an existing texture once it exists.

## Why this looks expensive

The "fun and expensive" feel is the sum of three cheap things:

1. **Additive HDR colors.** Every particle's start color has linear components above 1.0
   (orange x 3, yellow x 5, red x 4, etc.). At LDR these clip to white. With HDR + the additive
   material, the framebuffer accumulates beyond 1.0 where bursts overlap, so dense particles
   become superbright cores instead of muddy white blobs.
2. **Bloom with a high threshold.** The post-processing profile sets Bloom's threshold to 0.9,
   so only those HDR highlights bloom — the rest of the scene stays crisp. Scatter 0.7 gives
   the long, glowy streaks that read as "VFX-heavy game".
3. **Dark vignette + saturated color grade.** Vignette darkens the edges of the framebuffer so
   bloom highlights pop against deeper blacks. Color Adjustments adds +15 saturation and a small
   contrast bump so colored sparks read crisply instead of blowing out to white.

The shared additive material with a single soft-circle texture is also a draw-call win — every
FX prefab batches against itself and against every other FX prefab on screen.

## Performance notes

- Burst counts are deliberately kept low (24 / 8 / 17 / 6 / 60) so even ~100 simultaneous
  effects stay under a few thousand particles. The URP particle Unlit shader is cheap on mobile.
- Soft Particles are explicitly disabled in `MaterialGenerator` (`_SOFTPARTICLES_ON` keyword
  off) because the depth-fetch cost is not worth it on mobile.
- All FX prefabs go through `PoolService` via `FxService.Spawn`, and `ParticleAutoRelease`
  returns them to the pool the frame after `IsAlive(true)` goes false.
