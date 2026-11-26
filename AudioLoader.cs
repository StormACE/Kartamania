using System;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using NVorbis;

namespace Kartamania
{
    public static class AudioLoader
    {
        public static SoundEffect FromFile(string path)
        {
            if (string.Equals(Path.GetExtension(path), ".ogg", StringComparison.OrdinalIgnoreCase))
                return FromOgg(path);
            return SoundEffect.FromFile(path);
        }

        static SoundEffect FromOgg(string path)
        {
            using (var reader = new VorbisReader(path))
            {
                int rate = reader.SampleRate;
                int channels = reader.Channels;
                int block = Math.Max(1, rate * channels);
                var buffer = new float[block];

                using (var pcm = new MemoryStream())
                {
                    int read;
                    while ((read = reader.ReadSamples(buffer, 0, block)) > 0)
                    {
                        for (int i = 0; i < read; i++)
                        {
                            float v = M.Clamp(buffer[i], -1f, 1f);
                            short s = (short)(v * short.MaxValue);
                            pcm.WriteByte((byte)(s & 0xff));
                            pcm.WriteByte((byte)((s >> 8) & 0xff));
                        }
                    }
                    return new SoundEffect(pcm.ToArray(), rate, channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
                }
            }
        }
    }
}
