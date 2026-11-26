using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;

namespace Kartamania
{
    public static class MusicPlayer
    {
        const string MenuFile = "MetalRoll.ogg";
        static readonly string[] Extensions = { ".wav", ".ogg" };
        static readonly List<string> files = new List<string>();
        static readonly Random rng = new Random();
        static string lastPath = "";
        static SoundEffect effect, menuEffect;
        static SoundEffectInstance instance, menuInstance;

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

        public static void PlayMenu()
        {
            StopRace();
            StopMenu();
            string path = files.Find(f => string.Equals(Path.GetFileName(f), MenuFile, StringComparison.OrdinalIgnoreCase));
            if (path == null) return;

            try
            {
                menuEffect = AudioLoader.FromFile(path);
                menuInstance = menuEffect.CreateInstance();
                menuInstance.IsLooped = true;
                menuInstance.Volume = 0.45f;
                menuInstance.Play();
            }
            catch
            {
                StopMenu();
            }
        }

        public static void PlayRandom()
        {
            StopRace();
            StopMenu();
            if (files.Count == 0) return;

            int pick = rng.Next(files.Count);
            if (files.Count > 1 && files[pick] == lastPath) pick = (pick + 1) % files.Count;
            string path = files[pick];
            lastPath = path;

            try
            {
                effect = AudioLoader.FromFile(path);
                instance = effect.CreateInstance();
                instance.IsLooped = true;
                instance.Volume = 0.55f;
                instance.Play();
            }
            catch
            {
                StopRace();
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

        public static void StopRace()
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

        public static void StopMenu()
        {
            if (menuInstance != null)
            {
                menuInstance.Stop();
                menuInstance.Dispose();
                menuInstance = null;
            }
            if (menuEffect != null)
            {
                menuEffect.Dispose();
                menuEffect = null;
            }
        }

        public static void Stop()
        {
            StopRace();
            StopMenu();
        }
    }
}
