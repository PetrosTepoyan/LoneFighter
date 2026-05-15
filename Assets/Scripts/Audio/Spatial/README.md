# LoneFighter — Spatial Audio (`LoneFighter.Audio.Spatial`)

3D-positional SFX layer for LoneFighter's top-down arena. Lives entirely in
`Assets/Scripts/Audio/Spatial/` under the namespace `LoneFighter.Audio.Spatial`.
**This module is parallel to `AudioManager`, not a replacement.**

## What problem does this solve?

The base `AudioManager` plays 2D SFX, music, and UI sounds. With everything
mono / centered, the player can't hear *where* enemies are. This module adds:

- An `AudioListener` that follows the player (not the camera).
- A pool of 3D `AudioSource`s configured for short-range linear rolloff.
- Per-enemy emitters: footsteps, occasional vocalizations, and boss rumble.
- A global blend setting so accessibility / 2D-fallback users can mix out 3D.

`AudioManager` keeps owning music, UI clicks, level-up stingers, player SFX
(weapon swings, pickups), etc. **Do not route those through `WorldAudioPool`.**
Route only *world-positioned* events (enemy footsteps/vocals, boss rumble,
projectile impacts at a point in space, etc.) through this module.

## Components

| Component | Purpose |
| --- | --- |
| `SpatialAudioSettings` | Static accessor for the master spatial blend (`LF.Audio.Spatial.Blend` in PlayerPrefs). |
| `SpatialAudioSource`   | Pooled wrapper around an `AudioSource` pre-configured for 3D playback. |
| `WorldAudioPool`       | Singleton, pre-allocates 24 `SpatialAudioSource`s. Auto-recycles after clip duration. |
| `AudioListenerFollower`| Drives the scene's `AudioListener` to track `PlayerController.Instance` every `LateUpdate`. |
| `EnemyFootstepEmitter` | Plays footstep clips at the enemy position while it moves (`linearVelocity` over threshold). |
| `EnemyVocalEmitter`    | Plays grunt/growl clips at random 8–20s intervals. |
| `BossRumbleEmitter`    | Continuous looping rumble that follows a boss. |

### `AudioSource` configuration applied by `SpatialAudioSource`

```
spatialBlend = 1   (multiplied by SpatialAudioSettings.Blend at Play time)
rolloffMode  = Linear
minDistance  = 1
maxDistance  = 18
dopplerLevel = 0
playOnAwake  = false
```

## Integration

### 1. Place an `AudioListenerFollower`

Add the `AudioListenerFollower` component to **any** persistent GameObject
in your gameplay scene (e.g. a `[Audio]` bootstrap object, or directly on
the main camera GameObject — it does not modify the camera, only the
`AudioListener` transform). It will:

1. Find the scene's `AudioListener` (preferring the one on `Camera.main`).
2. Find the player via `PlayerController.Instance` (reflectively, so this
   module has zero compile-time dependency on the player module), or via
   the `"Player"` tag as a fallback.
3. Move the listener to the player's position every `LateUpdate`, forcing
   `z = -10` by default (configurable in the inspector — set to `0` for
   pure top-down setups).

`AudioListenerFollower` does **not** touch the camera transform and does
**not** touch `GameManager`. It only writes to the listener's transform.

### 2. `WorldAudioPool` is auto-created

The first access to `WorldAudioPool.Instance` spawns a hidden
`[WorldAudioPool]` GameObject (marked `DontDestroyOnLoad`) with 24 pooled
`SpatialAudioSource` children. You don't need to place it manually, but
if you prefer a scene-placed copy for inspector debugging, drop the
component anywhere — `Awake` honors that one instead.

### 3. Attach emitters to enemy prefabs

Footsteps:

```csharp
// On an enemy prefab inspector:
var footsteps = enemyPrefab.AddComponent<EnemyFootstepEmitter>();
// Assign _footstepClips, _interval, _volume in the inspector.
```

Vocals:

```csharp
// On an enemy prefab inspector:
var vocals = enemyPrefab.AddComponent<EnemyVocalEmitter>();
// Assign _vocalClips, leave _minInterval=8 / _maxInterval=20.
```

Boss rumble:

```csharp
// On a boss prefab inspector:
var rumble = bossPrefab.AddComponent<BossRumbleEmitter>();
// Assign _rumbleClip (looping low frequency drone).
```

All three emitters use `WorldAudioPool.Instance` under the hood and require
no further wiring.

### 4. Exposing the blend in a settings menu

```csharp
using LoneFighter.Audio.Spatial;

// On a slider value-changed event:
public void OnSpatialBlendChanged(float v)
{
    SpatialAudioSettings.Blend = v;   // clamped 0..1, written to PlayerPrefs
}

public void OnApplySettings()
{
    SpatialAudioSettings.Save();      // flush PlayerPrefs to disk
}
```

`1.0` = full 3D directional cues. `0.0` = full 2D fallback (good for mono
headphones or accessibility). Existing one-shots already playing keep
their original blend; new plays pick up the new value.

## Relationship to `AudioManager`

- `AudioManager` owns: music, UI SFX, player-centric 2D SFX, level-up
  stingers, anything not tied to a world position.
- `WorldAudioPool` (this module) owns: enemy footsteps, enemy vocals,
  boss rumble, and any other event that has a meaningful `Vector3`
  origin in the arena.

Both pipelines feed the same `AudioListener`, so a single `MasterVolume`
slider on `AudioManager` still controls overall loudness. You can add a
dedicated "World SFX" `AudioMixerGroup` later and route every
`SpatialAudioSource.Source.outputAudioMixerGroup` to it without changing
this module's public API.

## File layout

```
Assets/Scripts/Audio/Spatial/
├── AudioListenerFollower.cs
├── BossRumbleEmitter.cs
├── EnemyFootstepEmitter.cs
├── EnemyVocalEmitter.cs
├── README.md                 (this file)
├── SpatialAudioSettings.cs
├── SpatialAudioSource.cs
└── WorldAudioPool.cs
```

No files outside this folder are created or modified.
