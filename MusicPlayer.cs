using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;

namespace Kartamania
{
    public static class MusicPlayer
    {
        static readonly string[] Extensions = { ".wav", ".ogg" };
        static readonly List<string> files = new List<string>();
        static readonly Random rng = new Random();
        static string lastPath = "";
        static SoundEffect effect;
        static SoundEffectInstance instance;

        public static string FolderPath => Path.Combine(AppContext.BaseDirectory, "Music");

        public static int Count => files.Count;

        public static void Load()
        {
            files.Clear();
            try
            {
                Directory.CreateDirectory(FolderPath);
                foreach (string path in Directory.GetFiles(FolderPath))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (Array.IndexOf(Extensions, ext) >= 0) files.Add(path);
                }
                files.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                files.Clear();
            }
        }

        public static void PlayRandom()
        {
            Stop();
            if (files.Count == 0) return;

            int pick = rng.Next(files.Count);
            if (files.Count > 1 && files[pick] == lastPath) pick = (pick + 1) % files.Count;
            string path = files[pick];
            lastPath = path;

            try
            {
                effect = SoundEffect.FromFile(path);
                instance = effect.CreateInstance();
                instance.IsLooped = true;
                instance.Volume = 0.55f;
                instance.Play();
            }
            catch
            {
                if (instance != null) { instance.Dispose(); instance = null; }
                if (effect != null) { effect.Dispose(); effect = null; }
            }
        }

        public static void Pause()
        {
            if (instance != null && instance.State == SoundState.Playing) instance.Pause();
        }

        public static void Resume()
        {
            if (instance != null && instance.State == SoundState.Paused) instance.Resume();
        }

        public static void Stop()
        {
            if (instance != null)
            {
                instance.Stop();
                instance.Dispose();
                instance = null;
            }
            if (effect != null)
            {
                effect.Dispose();
                effect = null;
            }
        }
    }
}
