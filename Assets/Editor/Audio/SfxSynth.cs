using System;

namespace LoneFighter.EditorTools.Audio
{
    /// Tiny pure-C# DSP toolbox. Every function returns a fresh float[] in [-1, 1] (mono),
    /// or mutates in place when the API name says so. Used by AudioSynthGenerator to bake
    /// placeholder SFX/music WAVs entirely offline — no Unity runtime AudioSource needed.
    public static class SfxSynth
    {
        public enum OscType { Sine, Square, Saw, Triangle }

        // -----------------------------------------------------------------------------------------
        // Oscillators
        // -----------------------------------------------------------------------------------------

        public static float[] Sine(float freq, float duration, int sampleRate)
        {
            int n = (int)(duration * sampleRate);
            var buf = new float[n];
            double twoPiF = 2.0 * Math.PI * freq;
            for (int i = 0; i < n; i++)
            {
                buf[i] = (float)Math.Sin(twoPiF * i / sampleRate);
            }
            return buf;
        }

        public static float[] Square(float freq, float duration, int sampleRate)
        {
            int n = (int)(duration * sampleRate);
            var buf = new float[n];
            double period = sampleRate / (double)freq;
            for (int i = 0; i < n; i++)
            {
                // 1.0 for first half of period, -1.0 for second half — classic 50% duty square.
                buf[i] = ((i % period) < (period * 0.5)) ? 1f : -1f;
            }
            return buf;
        }

        public static float[] Saw(float freq, float duration, int sampleRate)
        {
            int n = (int)(duration * sampleRate);
            var buf = new float[n];
            double period = sampleRate / (double)freq;
            for (int i = 0; i < n; i++)
            {
                // Ramp from -1 -> +1 over each period.
                double phase = (i % period) / period;
                buf[i] = (float)(2.0 * phase - 1.0);
            }
            return buf;
        }

        public static float[] Triangle(float freq, float duration, int sampleRate)
        {
            int n = (int)(duration * sampleRate);
            var buf = new float[n];
            double period = sampleRate / (double)freq;
            for (int i = 0; i < n; i++)
            {
                double phase = (i % period) / period; // 0..1
                // Tent shape: 0..1 -> -1..1..-1
                buf[i] = (float)(4.0 * Math.Abs(phase - 0.5) - 1.0);
            }
            return buf;
        }

        // -----------------------------------------------------------------------------------------
        // Noise / sweeps
        // -----------------------------------------------------------------------------------------

        public static float[] Noise(float duration, int sampleRate, int seed)
        {
            int n = (int)(duration * sampleRate);
            var buf = new float[n];
            var rng = new Random(seed);
            for (int i = 0; i < n; i++)
            {
                // Uniform in [-1, 1].
                buf[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            }
            return buf;
        }

        public static float[] Sweep(float startFreq, float endFreq, float duration, int sampleRate, OscType oscType)
        {
            // Linear frequency sweep using accumulated phase so there are no phase pops between samples.
            int n = (int)(duration * sampleRate);
            var buf = new float[n];
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)n; // 0..1 across the buffer
                double freq = startFreq + (endFreq - startFreq) * t;
                phase += 2.0 * Math.PI * freq / sampleRate;
                buf[i] = Sample(oscType, phase);
            }
            return buf;
        }

        private static float Sample(OscType type, double phase)
        {
            // Wrap phase to [0, 2pi) for the non-sine generators.
            double wrapped = phase % (2.0 * Math.PI);
            if (wrapped < 0) wrapped += 2.0 * Math.PI;
            switch (type)
            {
                case OscType.Sine:     return (float)Math.Sin(phase);
                case OscType.Square:   return wrapped < Math.PI ? 1f : -1f;
                case OscType.Saw:      return (float)(wrapped / Math.PI - 1.0);
                case OscType.Triangle: return (float)(1.0 - 2.0 * Math.Abs(wrapped / Math.PI - 1.0));
                default:               return 0f;
            }
        }

        // -----------------------------------------------------------------------------------------
        // Envelope
        // -----------------------------------------------------------------------------------------

        /// ADSR envelope applied in place. All times are in seconds (sustain time is
        /// whatever's left between (attack+decay) and (total - release)).
        public static void Adsr(float[] samples, float attack, float decay, float sustainLevel, float release)
        {
            int n = samples.Length;
            if (n == 0) return;
            int sampleRate = 44100; // approximate — caller passes seconds, we treat them at 44.1k.
            // We can't know SR here without a parameter; the generator always uses 44.1k so this is fine.

            int aN = Math.Max(0, (int)(attack * sampleRate));
            int dN = Math.Max(0, (int)(decay * sampleRate));
            int rN = Math.Max(0, (int)(release * sampleRate));

            // Clamp segment lengths to buffer size.
            if (aN > n) aN = n;
            if (aN + dN > n) dN = n - aN;
            if (rN > n) rN = n;

            int sStart = aN + dN;
            int sEnd = Math.Max(sStart, n - rN);

            for (int i = 0; i < n; i++)
            {
                float env;
                if (i < aN)
                {
                    env = aN > 0 ? (float)i / aN : 1f;
                }
                else if (i < sStart)
                {
                    float t = dN > 0 ? (float)(i - aN) / dN : 1f;
                    env = 1f + (sustainLevel - 1f) * t;
                }
                else if (i < sEnd)
                {
                    env = sustainLevel;
                }
                else
                {
                    float t = rN > 0 ? (float)(i - sEnd) / rN : 1f;
                    env = sustainLevel * (1f - t);
                }
                samples[i] *= env;
            }
        }

        // -----------------------------------------------------------------------------------------
        // Filters / utility
        // -----------------------------------------------------------------------------------------

        /// Single-pole one-zero low-pass (RC filter). Cheap and good enough for placeholder vibe.
        /// y[n] = y[n-1] + alpha * (x[n] - y[n-1])  with alpha = dt / (RC + dt).
        public static void LowPass(float[] samples, float cutoffFreq, int sampleRate)
        {
            if (samples.Length == 0 || cutoffFreq <= 0f) return;
            double dt = 1.0 / sampleRate;
            double rc = 1.0 / (2.0 * Math.PI * cutoffFreq);
            float alpha = (float)(dt / (rc + dt));
            float prev = samples[0];
            for (int i = 0; i < samples.Length; i++)
            {
                prev = prev + alpha * (samples[i] - prev);
                samples[i] = prev;
            }
        }

        /// Sum all tracks (zero-padded to the longest), then normalize so peak = 0.95.
        public static float[] Mix(params float[][] tracks)
        {
            if (tracks == null || tracks.Length == 0) return new float[0];

            int maxLen = 0;
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i] != null && tracks[i].Length > maxLen) maxLen = tracks[i].Length;
            }
            var mix = new float[maxLen];

            for (int i = 0; i < tracks.Length; i++)
            {
                var t = tracks[i];
                if (t == null) continue;
                for (int j = 0; j < t.Length; j++) mix[j] += t[j];
            }

            // Peak normalize to 0.95 to leave a hair of headroom and avoid hard clipping at the WAV stage.
            float peak = 0f;
            for (int i = 0; i < mix.Length; i++)
            {
                float a = Math.Abs(mix[i]);
                if (a > peak) peak = a;
            }
            if (peak > 0f)
            {
                float scale = 0.95f / peak;
                for (int i = 0; i < mix.Length; i++) mix[i] *= scale;
            }
            return mix;
        }

        /// Multiply every sample by `factor` in place.
        public static void Gain(float[] samples, float factor)
        {
            for (int i = 0; i < samples.Length; i++) samples[i] *= factor;
        }

        /// Quick-and-dirty pitch-envelope: resample the buffer with a step that grows linearly
        /// from 1.0 to `factor` (factor > 1 = slide down/faster playback = lower duration & higher pitch
        /// equivalent for our purposes). Output length is preserved by zero-padding.
        public static float[] PitchSlideDown(float[] samples, float factor)
        {
            int n = samples.Length;
            var output = new float[n];
            double idx = 0.0;
            for (int i = 0; i < n; i++)
            {
                int j = (int)idx;
                if (j >= n) { output[i] = 0f; continue; }
                int j1 = Math.Min(j + 1, n - 1);
                double frac = idx - j;
                output[i] = (float)(samples[j] * (1.0 - frac) + samples[j1] * frac);

                // Step grows linearly from 1 -> factor across the buffer.
                double progress = (double)i / n;
                double step = 1.0 + (factor - 1.0) * progress;
                idx += step;
            }
            return output;
        }
    }
}
