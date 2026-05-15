using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.EditorTools.Audio
{
    /// Procedurally synthesizes every placeholder SFX + a loopable music track as proper
    /// 16-bit PCM WAV files under Assets/Audio/Generated/. Open via:
    ///   LoneFighter > Audio > Generate Placeholder SFX
    /// One click and the project is audibly alive — no asset packs required to demo the build.
    public class AudioSynthGenerator : EditorWindow
    {
        private const int SampleRate = 44100;
        private const string OutputDir = "Assets/Audio/Generated";

        // Per-cue metadata (filename, importer settings). Keep in sync with AudioCue enum.
        private struct CueSpec
        {
            public AudioCue cue;
            public string fileName;
            public bool isMusic;
            public AudioCompressionFormat compression;
            public AudioClipLoadType loadType;
            public bool preload;

            public CueSpec(AudioCue c, string f, bool music = false,
                AudioCompressionFormat comp = AudioCompressionFormat.Vorbis,
                AudioClipLoadType lt = AudioClipLoadType.DecompressOnLoad,
                bool pre = true)
            {
                cue = c; fileName = f; isMusic = music;
                compression = comp; loadType = lt; preload = pre;
            }
        }

        private static readonly CueSpec[] Specs = new[]
        {
            new CueSpec(AudioCue.EnemyHit,       "EnemyHit.wav"),
            new CueSpec(AudioCue.EnemyDeath,     "EnemyDeath.wav"),
            new CueSpec(AudioCue.PlayerHit,      "PlayerHit.wav"),
            new CueSpec(AudioCue.PlayerDeath,    "PlayerDeath.wav"),
            new CueSpec(AudioCue.ProjectileFire, "ProjectileFire.wav"),
            new CueSpec(AudioCue.XpPickup,       "XpPickup.wav"),
            new CueSpec(AudioCue.LevelUp,        "LevelUp.wav"),
            // ButtonClick uses PCM to keep latency near-zero — Vorbis adds decode lag on UI taps.
            new CueSpec(AudioCue.ButtonClick,    "ButtonClick.wav", false,
                AudioCompressionFormat.PCM, AudioClipLoadType.DecompressOnLoad, true),
            new CueSpec(AudioCue.BossSpawn,      "BossSpawn.wav"),
            new CueSpec(AudioCue.BossSlam,       "BossSlam.wav"),
            // Music streams from disk and is normalized; never preloaded into RAM.
            new CueSpec(AudioCue.MusicMain,      "MusicMain.wav", true,
                AudioCompressionFormat.Vorbis, AudioClipLoadType.Streaming, false),
        };

        [MenuItem("LoneFighter/Audio/Generate Placeholder SFX")]
        public static void ShowWindow()
        {
            var w = GetWindow<AudioSynthGenerator>("Audio Synth");
            w.minSize = new Vector2(320, 420);
        }

        private Vector2 _scroll;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Procedural Placeholder Audio", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Writes WAVs to " + OutputDir + " then sets AudioImporter settings.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Generate All", GUILayout.Height(32)))
            {
                GenerateAll();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Individual cues", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < Specs.Length; i++)
            {
                var spec = Specs[i];
                if (GUILayout.Button(spec.cue + "  ->  " + spec.fileName))
                {
                    GenerateOne(spec);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void GenerateAll()
        {
            try
            {
                EnsureOutputDir();
                for (int i = 0; i < Specs.Length; i++)
                {
                    var spec = Specs[i];
                    EditorUtility.DisplayProgressBar(
                        "Generating placeholder audio",
                        spec.cue + " -> " + spec.fileName,
                        (float)i / Specs.Length);
                    GenerateOne(spec);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("[AudioSynthGenerator] Generated " + Specs.Length + " WAV(s) in " + OutputDir);
        }

        private static void EnsureOutputDir()
        {
            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }
        }

        private static void GenerateOne(CueSpec spec)
        {
            EnsureOutputDir();
            string path = OutputDir + "/" + spec.fileName;
            float[] samples = Synthesize(spec.cue);
            WavWriter.Write(path, samples, SampleRate, channels: 1);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ApplyImporterSettings(path, spec);
        }

        private static void ApplyImporterSettings(string path, CueSpec spec)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;

            importer.preloadAudioData = spec.preload;
            importer.loadInBackground = false;
            importer.forceToMono = !spec.isMusic ? true : false;

            var settings = importer.defaultSampleSettings;
            settings.loadType = spec.loadType;
            settings.compressionFormat = spec.compression;
            // Vorbis quality is 0..1; 0.7 is a sensible mid for placeholder.
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;

            // Streaming music gets normalization on; one-shots don't need it (we pre-normalize in Mix).
            // SerializedObject hop because `normalize` isn't exposed on AudioImporter directly.
            var so = new SerializedObject(importer);
            var normProp = so.FindProperty("m_Normalize");
            if (normProp != null)
            {
                normProp.boolValue = spec.isMusic;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            importer.SaveAndReimport();
        }

        // =========================================================================================
        // Per-cue synthesis recipes — kept small and readable; tweak freely.
        // =========================================================================================

        private static float[] Synthesize(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.EnemyHit:       return MakeEnemyHit();
                case AudioCue.EnemyDeath:     return MakeEnemyDeath();
                case AudioCue.PlayerHit:      return MakePlayerHit();
                case AudioCue.PlayerDeath:    return MakePlayerDeath();
                case AudioCue.ProjectileFire: return MakeProjectileFire();
                case AudioCue.XpPickup:       return MakeXpPickup();
                case AudioCue.LevelUp:        return MakeLevelUp();
                case AudioCue.ButtonClick:    return MakeButtonClick();
                case AudioCue.BossSpawn:      return MakeBossSpawn();
                case AudioCue.BossSlam:       return MakeBossSlam();
                case AudioCue.MusicMain:      return MakeMusicMain();
            }
            return new float[0];
        }

        // 0.08s noise burst with snappy low-pass falloff — a satisfying thump.
        private static float[] MakeEnemyHit()
        {
            var noise = SfxSynth.Noise(0.08f, SampleRate, 1);
            SfxSynth.LowPass(noise, 1800f, SampleRate);
            SfxSynth.Adsr(noise, 0.001f, 0.02f, 0.6f, 0.05f);
            SfxSynth.Gain(noise, 0.9f);
            return noise;
        }

        // 0.15s descending square sweep 220 -> 55 Hz + short noise tail.
        private static float[] MakeEnemyDeath()
        {
            var sweep = SfxSynth.Sweep(220f, 55f, 0.15f, SampleRate, SfxSynth.OscType.Square);
            SfxSynth.Adsr(sweep, 0.002f, 0.04f, 0.6f, 0.10f);
            var noise = SfxSynth.Noise(0.06f, SampleRate, 2);
            SfxSynth.LowPass(noise, 1200f, SampleRate);
            SfxSynth.Adsr(noise, 0.001f, 0.02f, 0.3f, 0.04f);
            SfxSynth.Gain(noise, 0.35f);
            return SfxSynth.Mix(sweep, noise);
        }

        // 0.22s distorted thump: low square + noise + heavy LPF -> meaty player-hit.
        private static float[] MakePlayerHit()
        {
            var sq = SfxSynth.Square(85f, 0.22f, SampleRate);
            SfxSynth.Adsr(sq, 0.003f, 0.06f, 0.7f, 0.15f);
            var nz = SfxSynth.Noise(0.22f, SampleRate, 3);
            SfxSynth.Adsr(nz, 0.001f, 0.05f, 0.4f, 0.16f);
            SfxSynth.Gain(nz, 0.5f);
            var mix = SfxSynth.Mix(sq, nz);
            // Heavy LPF squashes the highs into a muffled "oof".
            SfxSynth.LowPass(mix, 600f, SampleRate);
            return mix;
        }

        // 0.6s dramatic descending sweep, lower pitch — player just died.
        private static float[] MakePlayerDeath()
        {
            var sweep = SfxSynth.Sweep(180f, 35f, 0.6f, SampleRate, SfxSynth.OscType.Saw);
            SfxSynth.Adsr(sweep, 0.01f, 0.1f, 0.7f, 0.45f);
            var sub  = SfxSynth.Sine(50f, 0.6f, SampleRate);
            SfxSynth.Adsr(sub, 0.02f, 0.1f, 0.8f, 0.4f);
            SfxSynth.Gain(sub, 0.5f);
            var nz = SfxSynth.Noise(0.6f, SampleRate, 4);
            SfxSynth.LowPass(nz, 800f, SampleRate);
            SfxSynth.Gain(nz, 0.25f);
            SfxSynth.Adsr(nz, 0.05f, 0.1f, 0.4f, 0.45f);
            return SfxSynth.Mix(sweep, sub, nz);
        }

        // 0.05s pitched click — short square ~900 Hz with fast decay.
        private static float[] MakeProjectileFire()
        {
            var sq = SfxSynth.Square(900f, 0.05f, SampleRate);
            SfxSynth.Adsr(sq, 0.001f, 0.01f, 0.4f, 0.035f);
            // Slight pitch slide gives a snappier "pew".
            sq = SfxSynth.PitchSlideDown(sq, 1.6f);
            SfxSynth.Gain(sq, 0.7f);
            return sq;
        }

        // 0.18s three-note rising sine arpeggio C5 -> E5 -> G5 with quick ADSR per note.
        private static float[] MakeXpPickup()
        {
            const float noteDur = 0.06f;
            float[] c5 = SfxSynth.Sine(523.25f, noteDur, SampleRate);
            float[] e5 = SfxSynth.Sine(659.25f, noteDur, SampleRate);
            float[] g5 = SfxSynth.Sine(783.99f, noteDur, SampleRate);
            SfxSynth.Adsr(c5, 0.003f, 0.01f, 0.7f, 0.04f);
            SfxSynth.Adsr(e5, 0.003f, 0.01f, 0.7f, 0.04f);
            SfxSynth.Adsr(g5, 0.003f, 0.01f, 0.7f, 0.04f);
            int n = (int)(noteDur * SampleRate);
            var buf = new float[n * 3];
            Array.Copy(c5, 0, buf, 0,     n);
            Array.Copy(e5, 0, buf, n,     n);
            Array.Copy(g5, 0, buf, 2 * n, n);
            SfxSynth.Gain(buf, 0.85f);
            return buf;
        }

        // 0.45s bright major chord C5+E5+G5+C6 plus light noise sparkle on top.
        private static float[] MakeLevelUp()
        {
            const float dur = 0.45f;
            var c5 = SfxSynth.Sine(523.25f, dur, SampleRate);
            var e5 = SfxSynth.Sine(659.25f, dur, SampleRate);
            var g5 = SfxSynth.Sine(783.99f, dur, SampleRate);
            var c6 = SfxSynth.Sine(1046.5f, dur, SampleRate);
            SfxSynth.Adsr(c5, 0.005f, 0.05f, 0.8f, 0.35f);
            SfxSynth.Adsr(e5, 0.005f, 0.05f, 0.8f, 0.35f);
            SfxSynth.Adsr(g5, 0.005f, 0.05f, 0.8f, 0.35f);
            SfxSynth.Adsr(c6, 0.005f, 0.05f, 0.7f, 0.35f);

            // Sparkle: brief filtered noise on top of the chord.
            var sparkle = SfxSynth.Noise(dur, SampleRate, 7);
            // High-frequency-ish — single-pole LPF, set high cutoff so we keep some shimmer.
            SfxSynth.LowPass(sparkle, 6000f, SampleRate);
            SfxSynth.Adsr(sparkle, 0.005f, 0.1f, 0.2f, 0.3f);
            SfxSynth.Gain(sparkle, 0.18f);

            return SfxSynth.Mix(c5, e5, g5, c6, sparkle);
        }

        // 0.04s clean click: impulse + a very short tone.
        private static float[] MakeButtonClick()
        {
            const float dur = 0.04f;
            int n = (int)(dur * SampleRate);
            var impulse = new float[n];
            // Tiny exponential decay impulse for the "tk" transient.
            for (int i = 0; i < Math.Min(120, n); i++) impulse[i] = (float)Math.Exp(-i / 30.0);
            var tone = SfxSynth.Sine(1200f, dur, SampleRate);
            SfxSynth.Adsr(tone, 0.001f, 0.005f, 0.4f, 0.03f);
            SfxSynth.Gain(tone, 0.4f);
            return SfxSynth.Mix(impulse, tone);
        }

        // 0.8s low rumble — saw + noise, slow ADSR attack for dread.
        private static float[] MakeBossSpawn()
        {
            const float dur = 0.8f;
            var saw  = SfxSynth.Saw(55f, dur, SampleRate);
            SfxSynth.LowPass(saw, 400f, SampleRate);
            SfxSynth.Adsr(saw, 0.2f, 0.1f, 0.85f, 0.4f);
            var saw2 = SfxSynth.Saw(41f, dur, SampleRate); // a fifth-ish below for fatness
            SfxSynth.LowPass(saw2, 350f, SampleRate);
            SfxSynth.Adsr(saw2, 0.25f, 0.1f, 0.8f, 0.4f);
            SfxSynth.Gain(saw2, 0.8f);
            var nz = SfxSynth.Noise(dur, SampleRate, 9);
            SfxSynth.LowPass(nz, 250f, SampleRate);
            SfxSynth.Adsr(nz, 0.3f, 0.1f, 0.6f, 0.4f);
            SfxSynth.Gain(nz, 0.4f);
            return SfxSynth.Mix(saw, saw2, nz);
        }

        // 0.4s explosive low thump + multi-band noise (LPF + a brighter band) for fake reverb.
        private static float[] MakeBossSlam()
        {
            const float dur = 0.4f;
            var thump = SfxSynth.Sweep(120f, 30f, 0.15f, SampleRate, SfxSynth.OscType.Sine);
            SfxSynth.Adsr(thump, 0.002f, 0.03f, 0.9f, 0.11f);
            SfxSynth.Gain(thump, 1.2f);

            // Body noise (low band)
            var nzLow = SfxSynth.Noise(dur, SampleRate, 11);
            SfxSynth.LowPass(nzLow, 300f, SampleRate);
            SfxSynth.Adsr(nzLow, 0.001f, 0.05f, 0.7f, 0.34f);
            SfxSynth.Gain(nzLow, 0.7f);

            // Sparse "reverb" tail — duplicated noise band, delayed via array offset.
            var nzMid = SfxSynth.Noise(dur, SampleRate, 13);
            SfxSynth.LowPass(nzMid, 1500f, SampleRate);
            SfxSynth.Adsr(nzMid, 0.05f, 0.15f, 0.4f, 0.2f);
            SfxSynth.Gain(nzMid, 0.35f);

            return SfxSynth.Mix(thump, nzLow, nzMid);
        }

        // -----------------------------------------------------------------------------------------
        // MUSIC: 16-second loopable bassline + arpeggio at 100 BPM in C minor, plus hi-hat.
        // 100 BPM -> 0.6s per beat. 16s = ~26.67 beats; we lay out exactly 8 bars of 4/4 = 32 beats
        // and trim the buffer to the bar-aligned length so it loops seamlessly.
        // -----------------------------------------------------------------------------------------
        private static float[] MakeMusicMain()
        {
            const float bpm = 100f;
            const float secPerBeat = 60f / bpm;     // 0.6s
            const int beats = 32;                   // 8 bars
            int beatSamples = (int)(secPerBeat * SampleRate);
            int totalSamples = beatSamples * beats;

            var bass = new float[totalSamples];
            var arp  = new float[totalSamples];
            var hat  = new float[totalSamples];

            // Bassline: C2 C2 G2 Eb2 (Hz) cycling per beat — propulsive 4-beat loop x 8.
            float[] bassPattern = { 65.41f, 65.41f, 98.00f, 77.78f };

            // Arpeggio notes — 16th notes inside each beat, walking up C minor pentatonic-ish.
            float[] arpPattern = { 523.25f, 622.25f, 783.99f, 1046.50f }; // C5 Eb5 G5 C6

            // Build bass — one square-wave note per beat, soft LPF, ADSR pluck.
            for (int b = 0; b < beats; b++)
            {
                float freq = bassPattern[b % bassPattern.Length];
                var note = SfxSynth.Square(freq, secPerBeat, SampleRate);
                SfxSynth.LowPass(note, 800f, SampleRate);
                SfxSynth.Adsr(note, 0.005f, 0.08f, 0.6f, 0.45f);
                SfxSynth.Gain(note, 0.55f);
                Array.Copy(note, 0, bass, b * beatSamples, Math.Min(note.Length, totalSamples - b * beatSamples));
            }

            // Build arpeggio — four 16th-note sines per beat (4 * 32 = 128 notes total).
            int sixteenthSamples = beatSamples / 4;
            for (int b = 0; b < beats; b++)
            {
                for (int s = 0; s < 4; s++)
                {
                    float freq = arpPattern[s % arpPattern.Length];
                    var note = SfxSynth.Sine(freq, sixteenthSamples / (float)SampleRate, SampleRate);
                    SfxSynth.Adsr(note, 0.003f, 0.02f, 0.4f, sixteenthSamples / (float)SampleRate * 0.6f);
                    SfxSynth.Gain(note, 0.3f);
                    int offset = b * beatSamples + s * sixteenthSamples;
                    Array.Copy(note, 0, arp, offset, Math.Min(note.Length, totalSamples - offset));
                }
            }

            // Hi-hat: filtered noise burst on the off-beats (beats 2 and 4 of each bar = beat%2==1).
            int hatLen = (int)(0.05f * SampleRate);
            int seed = 41;
            for (int b = 0; b < beats; b++)
            {
                if ((b % 2) == 0) continue; // off-beats only
                var hatNote = SfxSynth.Noise(hatLen / (float)SampleRate, SampleRate, seed++);
                SfxSynth.LowPass(hatNote, 9000f, SampleRate); // keep some grit but tame
                SfxSynth.Adsr(hatNote, 0.001f, 0.01f, 0.4f, 0.04f);
                SfxSynth.Gain(hatNote, 0.22f);
                int offset = b * beatSamples;
                Array.Copy(hatNote, 0, hat, offset, Math.Min(hatNote.Length, totalSamples - offset));
            }

            var mix = SfxSynth.Mix(bass, arp, hat);

            // Loop-seam crossfade: take the last `fadeSamples` and crossfade them with the first
            // `fadeSamples` of the buffer so sample[0] connects smoothly to sample[N-1].
            int fadeSamples = (int)(0.02f * SampleRate); // 20ms
            if (fadeSamples * 2 < mix.Length)
            {
                for (int i = 0; i < fadeSamples; i++)
                {
                    float t = (float)i / fadeSamples;
                    int tailIdx = mix.Length - fadeSamples + i;
                    float head = mix[i];
                    float tail = mix[tailIdx];
                    // Linear crossfade — write the blend into BOTH positions so that when the buffer
                    // wraps, head and tail are interpolating to the same value.
                    float blended = head * t + tail * (1f - t);
                    mix[tailIdx] = blended;
                    mix[i] = blended;
                }
            }

            return mix;
        }
    }
}
