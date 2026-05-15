# Adaptive Music — `LoneFighter.Audio.Adaptive`

Multi-stem adaptive music for LoneFighter. Drives a set of looping audio layers
(bass / drums / lead / pads etc.) from a live combat-intensity signal so the
soundtrack reacts to what the player is doing without ever stopping or
re-syncing.

This system is **parallel** to `AudioManager.PlayMusicCue` — not a replacement.
You can keep using `AudioManager` for one-shot music cues (menu music, victory
sting, etc.) and run `AdaptiveMusicController` on top for the in-run loop. If
you want only adaptive music during gameplay, either route the AudioManager
music cue to a silent clip or drop `AudioManager.musicVolume` to 0 while the
controller is active.

## Files

| File | Purpose |
| --- | --- |
| `MusicStemDefinition.cs` | `ScriptableObject` describing one stem: id, clip, intensity threshold, fade in/out. |
| `AdaptiveMusicProfile.cs` | `ScriptableObject` grouping a list of stems plus the track's BPM and total length. |
| `IntensityResolver.cs` | Singleton `MonoBehaviour`. Computes a smoothed `0..1` intensity each frame. |
| `AdaptiveMusicController.cs` | Singleton `MonoBehaviour`. Spawns one `AudioSource` per stem, all started together and looping. Drives volumes from `IntensityResolver.Intensity` every frame. |
| `Editor/AdaptiveMusicEditor.cs` | Editor window at **LoneFighter → Audio → Adaptive Music** for authoring profiles and previewing stem behavior. |

## Hard authoring rule — STEM ALIGNMENT

**Every clip referenced by a profile MUST be exactly the same length and BPM.**

The controller starts every stem on the same DSP timestamp and lets them loop
freely. There is no resampling, no time-stretching, no per-stem retrigger. If
two stems are slightly different lengths they will drift out of phase within a
loop and the layering will sound wrong.

Concretely, every clip for a single profile must share:

- the same authored **BPM** (e.g. 120),
- the same authored **length in seconds** (e.g. 32.0 s — typically a whole
  number of bars at the chosen BPM),
- the same **sample rate** (so Unity doesn't resample one and not the other).

The editor window runs a validation pass that flags the first stem whose clip
length doesn't match the profile's `lengthSeconds` (within 10 ms tolerance).
Fix the export, not the profile.

## Intensity model

`IntensityResolver` produces a 0..1 value each frame by summing:

| Contribution | Source | Strength |
| --- | --- | --- |
| Time pressure | `GameManager.Instance.ElapsedSeconds / 300` (clamped 0..1) | up to **+0.3** |
| Swarm | `EnemyRegistry.Count > 30` | **+0.3** |
| Low HP | `PlayerHealth.Current / PlayerHealth.Max < 0.3` | **+0.2** |
| Boss alive | Any live enemy whose name contains "Boss" or "Warden" | **+0.2** |

The sum is clamped to `[0, 1]` and lerped toward via `intensitySmoothing = 0.5s`
so transient events don't pop the stems on and off. The lerp uses
**unscaled time**, so paused / level-up screens still smoothly resolve toward
the current static state.

The resolver only **reads** existing public API — it never modifies
`GameManager`, `EnemyRegistry`, or `PlayerHealth`. It does not subscribe to
events either; the cost of polling these tiny inputs is dwarfed by the cost of
the AudioSources themselves.

## Stem fade model

Each stem has:

- `intensityThreshold` (0..1): below this, target volume = 0. At or above, target
  volume = the stem's `targetVolume * profile.masterVolume * controllerVolume`.
- `fadeInSeconds`: linear ramp time when the target is **above** the current volume.
- `fadeOutSeconds`: linear ramp time when the target is **below** the current volume.

A linear ramp is intentional: parallel music stems mixed equal-power produce a
nice gradual swell; an exponential ramp tends to "dump" the last 80% in the
final few percent and feels late.

## Scene setup

1. Add an empty GameObject **AdaptiveMusic** under your Systems parent.
2. Add components:
   - `IntensityResolver`
   - `AdaptiveMusicController` (assign a profile in `profile`, leave `playOnStart` checked)
3. Open **LoneFighter → Audio → Adaptive Music** in the editor.
4. Click **Generate Starter Profile** to drop a 4-stem template into
   `Assets/Data/Audio/Adaptive/StarterAdaptiveProfile.asset`. Stems are wired
   up at thresholds **0.0 / 0.3 / 0.6 / 0.9** (bass / drums / lead / pads).
5. If `Assets/Audio/Generated/` exists and contains audio files whose names
   contain `bass`, `drums`, `lead`, `pads`, those clips are auto-wired into
   the matching stems. Otherwise the clip slots are left null and a warning is
   logged — you'll need to drop the clips in manually.

## Running alongside `AudioManager`

`AdaptiveMusicController` creates its own `AudioSource` children — it does not
touch `AudioManager`'s `musicSource`. You can have both playing at once if you
want a long melodic bed (AudioManager) under reactive drums/leads (this system).

In practice, pick one or the other for the in-run loop. The simplest pattern:

- **Adaptive only**: set `AudioManager.musicVolume` to 0 or never call
  `AudioManager.PlayMusicCue(AudioCue.MusicMain)` during the run.
- **Legacy only**: don't add the `AdaptiveMusicController` component, or call
  `AdaptiveMusicController.Instance.Stop()` when entering the run.

## API at a glance

```csharp
// Read-only intensity, smoothed across frames.
float intensity = IntensityResolver.Instance.Intensity;

// Switch to a new profile (rebuilds all stem AudioSources).
AdaptiveMusicController.Instance.Play(someOtherProfile);

// Stop everything.
AdaptiveMusicController.Instance.Stop();
```

## Constraints respected

- New files only. Nothing under `Assets/Scripts/Systems`, `Assets/Scripts/Enemies`,
  `Assets/Scripts/Player`, or `Assets/Scripts/Audio` (apart from this new folder)
  is modified.
- The resolver subscribes to no events. It reads existing public state each
  frame from `GameManager`, `EnemyRegistry`, and `PlayerHealth`.
- All editor-only code is gated behind `#if UNITY_EDITOR` and lives in the
  `Editor/` subfolder so it never ships in player builds.
