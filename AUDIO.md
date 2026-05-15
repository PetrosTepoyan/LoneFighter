# LoneFighter — Audio

This project ships **no audio binaries**. Instead, an Editor tool procedurally synthesizes a complete set of placeholder SFX + a loopable music track so the build is audibly alive from the first run — no asset downloads required.

## Quickstart

1. Open the project in Unity 6.
2. Menu: **LoneFighter -> Audio -> Generate Placeholder SFX**.
3. Click **Generate All**.
4. Eleven `.wav` files appear in `Assets/Audio/Generated/`.
5. On the `AudioManager` GameObject (see `SETUP.md` step 14), expand **Cue Bindings** and drag each generated clip into the matching cue slot. Set per-clip `volume` (try `0.8`) and `pitchJitter` (try `0.93, 1.07` for nice variation).
6. Press Play. Hit an enemy, level up, die — every event should make a sound.

You can re-run **Generate All** any time; the importer settings are re-applied on each pass.

## Cue list

| Cue | File | Description |
| --- | --- | --- |
| `EnemyHit` | `EnemyHit.wav` | 0.08s noise burst + low-pass falloff. Generic "thwack" on every enemy damage tick. |
| `EnemyDeath` | `EnemyDeath.wav` | 0.15s descending square sweep 220 -> 55 Hz + noise tail. |
| `PlayerHit` | `PlayerHit.wav` | 0.22s distorted low-square + noise thump, heavy LPF. |
| `PlayerDeath` | `PlayerDeath.wav` | 0.6s descending saw sweep + sub-sine + filtered noise, dramatic. |
| `ProjectileFire` | `ProjectileFire.wav` | 0.05s short square pew at ~900 Hz with a quick pitch slide. |
| `XpPickup` | `XpPickup.wav` | 0.18s three-note sine arpeggio (C5, E5, G5). |
| `LevelUp` | `LevelUp.wav` | 0.45s bright C-major chord (C5/E5/G5/C6) plus filtered-noise sparkle. |
| `ButtonClick` | `ButtonClick.wav` | 0.04s clean transient + short 1.2 kHz tone. PCM-imported for minimum latency. |
| `BossSpawn` | `BossSpawn.wav` | 0.8s low saw + sub-saw + LPF noise; slow attack, ominous. |
| `BossSlam` | `BossSlam.wav` | 0.4s sine thump + dual-band noise for fake reverb. |
| `MusicMain` | `MusicMain.wav` | ~19s loopable bassline + 16th-note arpeggio + offbeat hi-hat at 100 BPM in C minor. 8 bars, seam-crossfaded. |

All clips are 44.1 kHz / 16-bit mono PCM WAV. The generator pre-normalizes mixes to peak 0.95, so they will not clip.

## How the audio bus works

`AudioManager.Play(AudioCue cue, float volumeScale = 1f)` is the gameplay entry point. It:

1. Looks up the `AudioCueBinding` for the cue in an internal `Dictionary<AudioCue, AudioCueBinding>` (built lazily, cached, zero per-call allocations).
2. If the cue name starts with `Music` (e.g. `MusicMain`), routes to `musicSource`.
3. Otherwise, applies per-binding pitch jitter (`Random.Range(pitchJitter.x, pitchJitter.y)`; falls back to `defaultPitchJitter` of `0.93..1.07` if both fields are zero) and calls `sfxSource.PlayOneShot(clip, volume)`.

Music cues should be started via `PlayMusicCue(AudioCue cue, float fadeSeconds = 0.5f)` for a crossfade, or stopped via `StopMusic(float fadeSeconds = 0.5f)`. Both are coroutine-driven and cancel-safe — back-to-back calls cancel any in-flight fade before starting the new one.

The legacy `PlayMusic(AudioClip)` / `PlaySfx(AudioClip, float)` API still works untouched, so if some code path holds an explicit `AudioClip` reference (e.g. a one-off jingle bound on a prefab), it continues to play.

## Pitch jitter

For every binding, set `pitchJitter` to a `(min, max)` pair. e.g. `(0.93, 1.07)` randomizes pitch within +/- 7% on each play, which kills the "machine-gun identical sample" effect on repeated hits. Set `(0, 0)` to use the AudioManager's `defaultPitchJitter`. Set `(1, 1)` to disable jitter for a single cue (e.g. a UI click that should sound consistent).

## Swapping in your own SFX

Two equally good options:

1. **Filename-match drop-in.** Replace the file at `Assets/Audio/Generated/<CueName>.wav` with your own WAV/OGG/MP3 of the same name. Unity re-imports the asset; the existing clip reference in `AudioManager.cueBindings` still points to it.
2. **Inspector swap.** On `AudioManager`, open the **Cue Bindings** list, find the cue, drop your clip into the `clip` field. Adjust `volume` and `pitchJitter` to taste.

For licensed/final audio, you may want to delete `Assets/Audio/Generated/` and use clips from your own folder — the bindings don't care where the clip asset lives.

## Recommended CC0 / royalty-free sources

For production audio, pull from these (all permit commercial use; verify license per asset):

- **[Sonniss GDC bundles](https://sonniss.com/gameaudiogdc)** — huge yearly bundles of pro game-audio assets, CC0.
- **[Freesound](https://freesound.org)** — community uploads, filter by CC0 license.
- **[Kenney Audio](https://kenney.nl/assets?q=audio)** — small, curated, CC0 packs (UI clicks, sci-fi, retro arcade).
- **[Free Music Archive](https://freemusicarchive.org)** — full-length music tracks under Creative Commons; favor CC-BY or CC0 entries.
- **[OpenGameArt](https://opengameart.org/art-search-advanced?field_art_type_tid%5B%5D=12)** — community audio with explicit license tagging.

Drop your chosen WAV/OGG into `Assets/Audio/` (or any subfolder), then bind it on the AudioManager.

## Generator internals

The generator is pure Unity Editor C# — no native plugins, no external dependencies. Files:

- `Assets/Editor/Audio/WavWriter.cs` — emits a standard 44-byte PCM WAV header + clamped int16 samples.
- `Assets/Editor/Audio/SfxSynth.cs` — synthesis primitives: `Sine`, `Square`, `Saw`, `Triangle`, `Noise`, `Sweep`, `Adsr`, `LowPass`, `Mix`, `Gain`, `PitchSlideDown`.
- `Assets/Editor/Audio/AudioSynthGenerator.cs` — `EditorWindow` with the per-cue recipes; sets `AudioImporter` settings (load type, compression, mono/stereo, normalize) after writing each file.

Generated WAVs are intentionally **not committed** to the repo — run the generator once after cloning, or anytime you want to refresh the placeholders.
