# Camera Juice

An extra layer of camera "feel" on top of `FxService`'s impulse-based shake.
All scripts in this folder live in the `LoneFighter.Effects.CameraJuice`
namespace and add up to the kind of camera language that makes a survivor-style
game feel premium: a punchy zoom on level-up, a directional kick when you take
damage or fire, a tiny rumble on continuous weapons, chromatic + lens-distortion
pulses on big events, and a brief motion-smear trail on huge moments.

## Files

| File | Role |
| --- | --- |
| `CameraPunchZoom.cs` | Singleton. Snaps `CinemachineCamera.Lens.OrthographicSize` inward by an intensity for the first half of a duration, then eases out for the second half. Auto-subscribes to `PlayerLevel.OnLevelUp` for a small punch. Public preset methods for boss-spawn (medium) and boss-death (heavy). Unscaled time. |
| `CameraKick.cs` | Singleton. Spring-damped local-position offset of the camera. Auto-subscribes to `PlayerHealth.OnHealthChanged` and kicks in a random direction whenever HP drops (the project doesn't expose a "last damage source" so direction is randomized — call `KickFromDamage` manually to pass a real source). Public `KickFromFire(Vector2)` for weapon recoil. |
| `CameraRecoil.cs` | Singleton. High-frequency tiny rumble for continuous weapons (Flamethrower, beam). Per-frame additive jitter on the camera transform; decays after one pulse-lifetime (default 0.05s). Call `Rumble()` every frame the weapon is emitting. |
| `ChromaticBurst.cs` | Singleton. Pulses URP `ChromaticAberration.intensity` on big events (explosion, boss spawn, level-up, low-HP entry). Auto-subscribes to `PlayerLevel.OnLevelUp` and to the low-HP edge on `PlayerHealth.OnHealthChanged`. Reads the first scene `Volume`; gracefully no-ops if missing. |
| `RadialBlurPulse.cs` | Singleton. Briefly increases URP `LensDistortion.intensity` then eases back over 0.4s. Used as a "fake radial blur" on huge moments (explosions, boss spawn). Gracefully no-ops if no Volume / no LensDistortion override is present. |
| `MotionTrails.cs` | Singleton. Records a small ring buffer of camera positions and drives N "ghost" follower transforms at fixed per-ghost delays. Two modes: auto-spawn N copies of an assigned `ghostPrefab`, or pre-wire ghost transforms in the inspector. Burst-driven (`Burst(seconds)`). |
| `CameraJuiceService.cs` | Singleton facade. Other systems call `OnLevelUp()`, `OnBossSpawn()`, `OnBossDeath()`, `OnExplosion(intensity)`, `OnDamage(dir)`, `OnFire(dir)`, `OnContinuousFire()`, `OnVictory()`. Dispatches to whichever modules are present in the scene. |

## Wiring up a scene

1. In the Game scene, create a single GameObject called **CameraJuice**.
2. Add `CameraJuiceService` plus whichever of the six modules you want enabled
   (typically: all of them).
3. Each module looks for a `CinemachineCamera` automatically:
   - first on the same GameObject,
   - then via `FindFirstObjectByType<CinemachineCamera>()` at `Awake`.
   For a different camera, drag it into the module's `targetCamera` slot.
4. `ChromaticBurst` and `RadialBlurPulse` look for the first `Volume` in the
   scene with a profile. If you want them to pulse a specific Volume, drag it
   into the `volume` slot — otherwise the lookup is fine.
5. (Optional) For `MotionTrails`, either assign a `ghostPrefab` (any GameObject
   — typically a faded sprite of the player or a full-screen quad) to have N
   copies auto-spawned, or pre-fill the `ghosts` list with Transforms you've
   placed in the scene.

The service composes safely with `FxService`'s impulse shake and with
`SlowMoTrigger`'s timescale ease — all camera-juice timing is unscaled.

## How to call from other systems

```csharp
using LoneFighter.Effects.CameraJuice;

// Level-up auto-fires through CameraPunchZoom + ChromaticBurst event subs,
// but you can also call the facade explicitly:
CameraJuiceService.Instance?.OnLevelUp();

// On a boss-spawn moment from a wave manager or boss controller:
CameraJuiceService.Instance?.OnBossSpawn();

// On a projectile detonating:
CameraJuiceService.Instance?.OnExplosion(intensity: 1f);

// Inside a Flamethrower-style weapon's Update() while emitting:
CameraJuiceService.Instance?.OnContinuousFire();

// Whenever a weapon fires a projectile, pass the outgoing direction:
CameraJuiceService.Instance?.OnFire(direction);

// If/when a damage pipeline can pass the incoming direction from a hit:
CameraJuiceService.Instance?.OnDamage(incomingDir);

// On run completion:
CameraJuiceService.Instance?.OnVictory();
```

## Notes & design choices

- **No existing files modified.** The folder is fully additive. Boss-spawn /
  boss-death are not events in the current codebase, so those punches are not
  auto-subscribed — they fire only through `CameraJuiceService.OnBossSpawn()` /
  `OnBossDeath()` from whatever system spawns/kills the boss.
- **Last damage source.** `PlayerHealth.OnHealthChanged` doesn't include the
  damage source, and the spec accepts a random-direction fallback. `CameraKick`
  ships with that fallback but exposes `KickFromDamage(Vector2)` for callers
  that *do* know the source (e.g. a custom damage pipeline can pass
  `playerPos - sourcePos`).
- **Cinemachine 3.1.x.** Lens writes go through the `CinemachineCamera.Lens`
  struct copy-modify-assign pattern, which matches the current
  `com.unity.cinemachine@3.1.2` API.
- **URP gracefulness.** `ChromaticBurst` and `RadialBlurPulse` capture the
  baseline value at `Start` and restore on `OnDestroy`. If no Volume / no
  override is present, the pulse calls simply no-op — no exceptions, no log
  spam.
- **Composition with other camera writers.** `CameraKick` and `CameraRecoil`
  both write `transform.localPosition`. They cooperate by capturing a base pose
  and writing offsets rather than absolute positions. If you add a third
  position-writing layer, follow the same pattern.
- **Performance.** Everything is allocation-free per frame (the ring buffer in
  `MotionTrails` is a `struct[]`). On a 60-120 Hz mobile target this folder
  costs effectively nothing: a few spring-damper integrations and a couple of
  per-event coroutines.
