using System;
using System.IO;

namespace Kartamania
{
    /// <summary>Réglages sauvegardés dans settings.txt (à côté de l'exécutable).</summary>
    public sealed class GameSettings
    {
        public static readonly int[] FpsCaps = { 60, 120, 144, 240, 0 };   // 0 = illimité

        public int Width = 1920, Height = 1080;
        public bool Fullscreen = true;
        public bool VSync = false;
        public int FpsIndex = 1;            // 120 FPS par défaut
        public int Track, Player, Difficulty = 1, Laps = 3, RacerCount = 6;

        static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.txt");

        public static GameSettings Load()
        {
            var s = new GameSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    int n;
                    bool isInt = int.TryParse(val, out n);
                    switch (key)
                    {
                        case "width": if (isInt) s.Width = n; break;
                        case "height": if (isInt) s.Height = n; break;
                        case "fullscreen": s.Fullscreen = val == "1"; break;
                        case "vsync": s.VSync = val == "1"; break;
                        case "fps": if (isInt) s.FpsIndex = Math.Max(0, Math.Min(FpsCaps.Length - 1, n)); break;
                        case "track": if (isInt) s.Track = Math.Max(0, n); break;
                        case "player": if (isInt) s.Player = Math.Max(0, n); break;
                        case "difficulty": if (isInt) s.Difficulty = Math.Max(0, Math.Min(2, n)); break;
                        case "laps": if (isInt) s.Laps = Math.Max(1, Math.Min(9, n)); break;
                        case "pilots": if (isInt) s.RacerCount = Math.Max(1, Math.Min(10, n)); break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                File.WriteAllLines(FilePath, new[]
                {
                    "width=" + Width, "height=" + Height,
                    "fullscreen=" + (Fullscreen ? 1 : 0), "vsync=" + (VSync ? 1 : 0), "fps=" + FpsIndex,
                    "track=" + Track, "player=" + Player, "difficulty=" + Difficulty, "laps=" + Laps,
                    "pilots=" + RacerCount,
                });
            }
            catch { }
        }
    }
}
