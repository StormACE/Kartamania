using System;
using Microsoft.Xna.Framework.Audio;

namespace Kartamania
{
    public static class Sfx
    {
        const int Rate = 22050;
        static readonly Random rng = new Random(9137);

        static SoundEffect countLow, countMid, countHigh, go;
        static SoundEffect pickup, missile, mine, boost, shield;
        static SoundEffect hitKart, hitObstacle, wall, explosion, respawn, lap, win, lose;
        static SoundEffect engine, screech;
        static SoundEffectInstance engineInstance, screechInstance;
        static int lastImpact;
        static bool engineRunning, screechRunning;

        public static void Load()
        {
            countLow = Tone(440f, 0.14f, 0.32f, 1.2f);
            countMid = Tone(554f, 0.14f, 0.32f, 1.2f);
            countHigh = Tone(660f, 0.16f, 0.34f, 1.2f);
            go = Chord(new[] { 660f, 880f, 1320f }, 0.42f, 0.30f, 0.7f);

            pickup = Chord(new[] { 660f, 880f, 1320f }, 0.22f, 0.24f, 1.8f);
            missile = Sweep(280f, 900f, 0.26f, 0.30f, 0.8f);
            mine = Tone(520f, 0.12f, 0.24f, 2.4f);
            boost = Sweep(220f, 1100f, 0.45f, 0.28f, 0.7f);
            shield = Chord(new[] { 440f, 660f, 990f }, 0.34f, 0.20f, 1.1f);

            hitKart = Impact(0.22f, 0.36f, 130f, 70f);
            hitObstacle = Impact(0.20f, 0.38f, 170f, 95f);
            wall = Impact(0.18f, 0.24f, 110f, 55f);
            explosion = Noise(0.48f, 0.42f, 1.4f, 0.11f);
            respawn = Chord(new[] { 523f, 784f }, 0.30f, 0.22f, 1.4f);
            lap = Tone(880f, 0.20f, 0.24f, 1.5f);
            win = Chord(new[] { 523f, 659f, 784f, 1047f }, 0.75f, 0.28f, 0.5f);
            lose = Sweep(330f, 110f, 0.65f, 0.26f, 1.0f);

            engine = Make(0.5f, (i, n) =>
            {
                float t = i / (float)Rate;
                float v = 0.45f * (float)Math.Sin(2f * M.Pi * 110f * t)
                        + 0.28f * (float)Math.Sin(2f * M.Pi * 220f * t)
                        + 0.14f * (float)Math.Sin(2f * M.Pi * 330f * t)
                        + 0.08f * (float)Math.Sin(2f * M.Pi * 440f * t)
                        + 0.05f * (float)Math.Sin(2f * M.Pi * 550f * t);
                v *= 0.86f + 0.14f * (float)Math.Sin(2f * M.Pi * 2f * t);
                return v * 0.34f;
            });

            float filtered = 0f;
            screech = Make(0.5f, (i, n) =>
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                filtered += 0.62f * (noise - filtered);
                float bright = noise - filtered * 0.75f;
                float squeal = 0.12f * (float)Math.Sin(2f * M.Pi * 1650f * t)
                             + 0.08f * (float)Math.Sin(2f * M.Pi * 2200f * t);
                float mod = 0.78f + 0.22f * (float)Math.Sin(2f * M.Pi * 4f * t);
                return (bright * 0.30f + squeal) * mod;
            });
        }

        static SoundEffect Make(float seconds, Func<int, int, float> sample)
        {
            int count = Math.Max(1, (int)(Rate * seconds));
            byte[] data = new byte[count * 2];
            for (int i = 0; i < count; i++)
            {
                float v = M.Clamp(sample(i, count), -1f, 1f);
                short s = (short)(v * short.MaxValue);
                data[i * 2] = (byte)(s & 0xff);
                data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
            }
            return new SoundEffect(data, Rate, AudioChannels.Mono);
        }

        static SoundEffect Tone(float freq, float seconds, float volume, float decay) =>
            Make(seconds, (i, n) =>
            {
                float p = i / (float)n;
                return (float)Math.Sin(2f * M.Pi * freq * i / Rate) * volume * (float)Math.Pow(1f - p, decay);
            });

        static SoundEffect Sweep(float from, float to, float seconds, float volume, float decay) =>
            Make(seconds, (i, n) =>
            {
                float p = i / (float)n;
                float f = from + (to - from) * p;
                return (float)Math.Sin(2f * M.Pi * f * i / Rate) * volume * (float)Math.Pow(1f - p, decay);
            });

        static SoundEffect Chord(float[] freqs, float seconds, float volume, float decay) =>
            Make(seconds, (i, n) =>
            {
                float p = i / (float)n;
                float v = 0f;
                foreach (float f in freqs) v += (float)Math.Sin(2f * M.Pi * f * i / Rate);
                return v / freqs.Length * volume * (float)Math.Pow(1f - p, decay);
            });

        static SoundEffect Noise(float seconds, float volume, float decay, float smooth)
        {
            float prev = 0f;
            return Make(seconds, (i, n) =>
            {
                prev += smooth * ((float)(rng.NextDouble() * 2.0 - 1.0) - prev);
                float p = i / (float)n;
                return prev * volume * (float)Math.Pow(1f - p, decay);
            });
        }

        static SoundEffect Impact(float seconds, float volume, float startFreq, float endFreq)
        {
            float prev = 0f;
            return Make(seconds, (i, n) =>
            {
                float p = i / (float)n;
                float f = startFreq + (endFreq - startFreq) * p;
                prev += 0.22f * ((float)(rng.NextDouble() * 2.0 - 1.0) - prev);
                float body = (float)Math.Sin(2f * M.Pi * f * i / Rate) * 0.75f;
                float env = (float)Math.Pow(1f - p, 2.2f);
                return (body + prev * 0.6f) * volume * env;
            });
        }

        static void Play(SoundEffect e, float volume = 1f, float pitch = 0f)
        {
            if (e != null) e.Play(M.Clamp(volume, 0f, 1f), M.Clamp(pitch, -1f, 1f), 0f);
        }

        static float Jitter() => (float)(rng.NextDouble() * 0.12 - 0.06);

        static bool ImpactReady(int ms)
        {
            int now = Environment.TickCount;
            if (now - lastImpact < ms) return false;
            lastImpact = now;
            return true;
        }

        public static void Countdown(int n) => Play(n == 1 ? countHigh : n == 2 ? countMid : countLow);
        public static void Go() => Play(go);

        public static void Pickup(bool player) => Play(pickup, player ? 1f : 0.35f);

        public static void UseItem(ItemType item)
        {
            switch (item)
            {
                case ItemType.Missile: Play(missile); break;
                case ItemType.Mine: Play(mine); break;
                case ItemType.Boost: Play(boost); break;
                case ItemType.Shield: Play(shield); break;
            }
        }

        public static void KartCollision(float strength, bool player)
        {
            if (!ImpactReady(70)) return;
            Play(hitKart, (player ? 1f : 0.45f) * M.Clamp(0.45f + strength, 0.45f, 1f), Jitter());
        }

        public static void ObstacleCollision(bool player)
        {
            if (!ImpactReady(70)) return;
            Play(hitObstacle, player ? 1f : 0.35f, Jitter());
        }

        public static void Wall(bool player)
        {
            if (!ImpactReady(100)) return;
            Play(wall, player ? 0.9f : 0.25f, Jitter());
        }

        public static void Explosion(bool player) => Play(explosion, player ? 0.9f : 0.35f, Jitter());
        public static void Shield(bool player) => Play(shield, player ? 1f : 0.4f);
        public static void Respawn(bool player) => Play(respawn, player ? 0.9f : 0.3f);
        public static void Lap() => Play(lap, 0.9f);
        public static void Finish(bool victory) => Play(victory ? win : lose);

        public static void StartEngine()
        {
            if (engine == null) return;
            if (engineInstance == null) engineInstance = engine.CreateInstance();
            engineInstance.IsLooped = true;
            engineInstance.Volume = 0.18f;
            engineInstance.Play();
            engineRunning = true;

            if (screech == null) return;
            if (screechInstance == null) screechInstance = screech.CreateInstance();
            screechInstance.IsLooped = true;
            screechInstance.Volume = 0f;
            screechInstance.Play();
            screechRunning = true;
        }

        public static void UpdateEngine(float speed, bool boosting)
        {
            if (!engineRunning || engineInstance == null) return;
            float norm = M.Clamp(speed / Race.BaseSpeed, 0f, 1.4f);
            engineInstance.Pitch = M.Clamp(-0.12f + norm * 0.85f + (boosting ? 0.16f : 0f), -1f, 1f);
            engineInstance.Volume = M.Clamp(0.18f + norm * 0.16f + (boosting ? 0.08f : 0f), 0f, 0.42f);
        }

        public static void UpdateScreech(float intensity)
        {
            if (!screechRunning || screechInstance == null) return;
            float amount = M.Clamp(intensity, 0f, 1f);
            screechInstance.Pitch = M.Clamp(0.05f + amount * 0.18f, -1f, 1f);
            screechInstance.Volume = M.Clamp(amount * 0.30f, 0f, 0.30f);
        }

        public static void PauseEngine()
        {
            if (engineRunning && engineInstance != null && engineInstance.State == SoundState.Playing) engineInstance.Pause();
            if (screechRunning && screechInstance != null && screechInstance.State == SoundState.Playing) screechInstance.Pause();
        }

        public static void ResumeEngine()
        {
            if (engineRunning && engineInstance != null && engineInstance.State == SoundState.Paused) engineInstance.Resume();
            if (screechRunning && screechInstance != null && screechInstance.State == SoundState.Paused) screechInstance.Resume();
        }

        public static void StopEngine()
        {
            engineRunning = false;
            screechRunning = false;
            if (engineInstance != null)
            {
                engineInstance.Stop();
                engineInstance.Dispose();
                engineInstance = null;
            }
            if (screechInstance != null)
            {
                screechInstance.Stop();
                screechInstance.Dispose();
                screechInstance = null;
            }
        }

        public static void Stop()
        {
            StopEngine();
        }
    }
}
