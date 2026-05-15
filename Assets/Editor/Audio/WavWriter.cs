using System;
using System.IO;

namespace LoneFighter.EditorTools.Audio
{
    /// Minimal PCM 16-bit little-endian WAV writer. Pure C#, no dependencies.
    /// Layout (44-byte canonical header + interleaved samples):
    ///   "RIFF" <size-8> "WAVE"
    ///   "fmt " 16 1 channels sampleRate byteRate blockAlign 16
    ///   "data" <dataSize> <samples...>
    public static class WavWriter
    {
        public static void Write(string path, float[] samples, int sampleRate = 44100, int channels = 1)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (channels < 1 || channels > 2) throw new ArgumentOutOfRangeException(nameof(channels));

            const int bitsPerSample = 16;
            int byteRate = sampleRate * channels * (bitsPerSample / 8);
            int blockAlign = channels * (bitsPerSample / 8);
            int dataBytes = samples.Length * (bitsPerSample / 8);
            int chunkSize = 36 + dataBytes;

            // Ensure directory exists — caller can drop us a brand-new subfolder under Assets/.
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                // RIFF header
                bw.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                bw.Write(chunkSize);
                bw.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });

                // fmt subchunk
                bw.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                bw.Write(16);                          // Subchunk1Size for PCM
                bw.Write((short)1);                    // AudioFormat = 1 (PCM)
                bw.Write((short)channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((short)blockAlign);
                bw.Write((short)bitsPerSample);

                // data subchunk
                bw.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                bw.Write(dataBytes);

                // Samples — clamp to [-1, 1] then quantize to signed 16-bit little-endian.
                // BinaryWriter on .NET is always little-endian, which is what WAV requires.
                for (int i = 0; i < samples.Length; i++)
                {
                    float s = samples[i];
                    if (s > 1f) s = 1f;
                    else if (s < -1f) s = -1f;
                    short v = (short)Math.Round(s * 32767f);
                    bw.Write(v);
                }
            }
        }
    }
}
