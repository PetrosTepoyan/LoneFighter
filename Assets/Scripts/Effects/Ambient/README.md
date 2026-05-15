# Ambient FX

Atmospheric world particles that fill the empty Vampire-Survivors-style arena and
reinforce the "intensity climbs over 5 minutes" arc.

Namespace: `LoneFighter.Effects.Ambient`
Folder: `Assets/Scripts/Effects/Ambient/`

## Architecture

```
AmbientFx (GameObject, parent)
  AmbientFxController        <- single driver, reads elapsed time, computes intensity 0..1
  DustMotes (child + PS)     <- subtle drifting motes around camera
  EmbersAndSparks (child+PS) <- rising orange embers, scales with intensity
  LightningStrikes (child+PS)<- screen-edge flashes when intensity > 0.7
  WindGusts (child + PS)     <- directional streaks every 8-15s
  ScreenEdgeFog (child + PS) <- dark vignette particles, closes in late-run
```

Every child is an `AmbientFxModule` and subscribes to the controller in `OnEnable`.
The controller drives all modules every frame from one place; modules never reach
into other game systems and never modify existing files.

## How the intensity curve works

1. `AmbientFxController` tries to read `GameManager.Instance.ElapsedSeconds` via
   reflection (it accepts either a property or a public field, and tries both
   `LoneFighter.Systems.GameManager` and an unqualified `GameManager`).
2. If `GameManager` is missing at runtime, it falls back to its own internal clock
   driven by `Time.deltaTime` (toggle `useFallbackClockIfNoGameManager` to disable).
3. `elapsedSeconds / runDuration` is clamped to 0..1 and passed through
   `intensityCurve` (an `AnimationCurve`, default EaseInOut). The result is the
   `intensity` value each module receives.
4. `runDuration` defaults to 300 seconds (5 minutes) to match the design doc.

You can also subscribe to `AmbientFxController.OnIntensityUpdated` if any other
system later wants to hook into the same curve.

## Module breakdown

| Module             | What it does                                                                 | Intensity behavior                                |
|--------------------|------------------------------------------------------------------------------|---------------------------------------------------|
| `DustMotes`        | Slow drifting motes around the camera with Perlin-wandered emitter position. | Slight density increase from min->max.            |
| `EmbersAndSparks`  | Orange/yellow embers rising from below the camera, additive blended.         | Density scales from ~1/s to ~35/s with exponent.  |
| `LightningStrikes` | One-shot white streaks from the screen edge + optional full-screen flash.    | Disabled until intensity >= 0.7. Interval 4-12s.  |
| `WindGusts`        | Long horizontal streaks across the arena, direction rotates slowly.          | Burst count scales 0.6x..1.4x with intensity.     |
| `ScreenEdgeFog`    | Soft dark particles arranged in a ring around the screen.                    | Density and alpha both ramp up with intensity.    |

## Scene setup (one-time)

1. Create an empty GameObject under your scene root and name it `AmbientFx`.
2. Add `AmbientFxController` to it.
3. For each of the five effect modules:
   - Create an empty child GameObject (e.g. `DustMotes`).
   - Add a `ParticleSystem` component.
   - Add the matching module script. The script's `RequireComponent` ensures the
     ParticleSystem exists, and `OnEnable` configures the system in code so no
     manual inspector tuning is required for a working baseline.
4. (Optional) Drag a Camera reference into each module's `targetCamera` field;
   otherwise modules grab `Camera.main` at runtime.
5. (Optional, `LightningStrikes` only) Wire a full-screen UI `SpriteRenderer` into
   the `flashOverlay` field for the screen flash. Without it, only the particle
   crack fires.

## Tuning knobs

Every module exposes its key knobs in the inspector:

- **Counts / density** — `emissionMin`, `emissionMax`, `burstCountMin/Max`.
- **Look** — colors, sizes, lifetimes.
- **Timing** — `intervalMin/Max` for one-shot modules.
- **Activation** — `LightningStrikes.activationThreshold` (default 0.7).
- **Shape** — `fieldSize`, `stripSize`, `outerHalfSize`, `innerHalfSize`.

The `AmbientFxController.intensityCurve` is the single most useful designer-facing
knob: tweak it to make the run ramp linearly, slow-start-fast-finish, etc.

## Blending

All bright modules (embers, sparks, lightning, wind) attempt to configure their
materials for additive blending. If `Particles/Standard Unlit` is unavailable the
modules fall back to `Sprites/Default` and skip the blend tweaks — you can also
drop an additive-configured material into the `ParticleSystemRenderer` manually
and the runtime config will leave it alone (it only assigns a material when none
is present).

`ScreenEdgeFog` deliberately uses alpha blending (not additive) so it can darken
the screen edges instead of brightening them.

## Performance notes

- Targets mobile portrait 120Hz, so all `maxParticles` ceilings are kept modest
  (<= 512) and emission rates are capped.
- `LightningStrikes` and `WindGusts` are bursty (one-shot), not continuous, to
  keep average particle count low.
- `ScreenEdgeFog` emits manually per frame so it can place particles on a
  rectangle ring without an extra shape module overdraw.
- The controller iterates registered modules from the end of the list, so a
  module disabling itself during `Tick` is safe.

## Hard rules followed

- New files only, in this folder.
- No existing files modified.
- Uses Unity 6 ParticleSystem API.
- All modules talk to the controller only through `AmbientFxModule.Tick`.
