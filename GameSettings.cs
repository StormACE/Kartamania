using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Kartamania
{
    /// <summary>Réglages sauvegardés dans settings.txt (à côté de l'exécutable).</summary>
    public sealed class GameSettings
    {
        public static readonly int[] FpsCaps = { 60, 120, 144, 240, 0 };   // 0 = illimité

        public int Width = 1920, Height = 1080;
        public bool Fullscreen = true;
        public bool Borderless;
        public int DisplayMode
        {
            get { return Fullscreen ? 2 : (Borderless ? 1 : 0); }
            set { Fullscreen = value == 2; Borderless = value == 1; }
        }
        public bool VSync = false;
        public int FpsIndex = 1;            // 120 FPS par défaut
        public int Track, Player, Difficulty = 1, Laps = 3, RacerCount = 6, Language, KartColor;
        public readonly Dictionary<string, float> BestTimes = new Dictionary<string, float>();

        static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.txt");

        public static string BestKey(string trackName, int laps, int difficulty)
        {
            difficulty = Math.Max(0, Math.Min(2, difficulty));
            return trackName + "|" + laps + "|" + difficulty;
        }

        public float GetBestTime(string trackName, int laps, int difficulty)
        {
            float time;
            return BestTimes.TryGetValue(BestKey(trackName, laps, difficulty), out time) ? time : 0f;
        }

        public bool RecordTime(string trackName, int laps, int difficulty, float time)
        {
            if (time <= 0f) return false;
            string key = BestKey(trackName, laps, difficulty);
            float previous;
            if (BestTimes.TryGetValue(key, out previous) && previous <= time) return false;
            BestTimes[key] = time;
            Save();
            return true;
        }

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
                        case "borderless": s.Borderless = val == "1"; break;
                        case "display": if (isInt) s.DisplayMode = Math.Max(0, Math.Min(2, n)); break;
                        case "vsync": s.VSync = val == "1"; break;
                        case "fps": if (isInt) s.FpsIndex = Math.Max(0, Math.Min(FpsCaps.Length - 1, n)); break;
                        case "track": if (isInt) s.Track = Math.Max(0, n); break;
                        case "player": if (isInt) s.Player = Math.Max(0, n); break;
                        case "kartcolor": if (isInt) s.KartColor = Math.Max(0, Math.Min(9, n)); break;
                        case "difficulty": if (isInt) s.Difficulty = Math.Max(0, Math.Min(2, n)); break;
                        case "laps": if (isInt) s.Laps = Math.Max(1, Math.Min(20, n)); break;
                        case "pilots": if (isInt) s.RacerCount = Math.Max(1, Math.Min(10, n)); break;
                        case "language": if (isInt) s.Language = Math.Max(0, Math.Min(1, n)); break;
                        case "best":
                            {
                                string[] parts = val.Split('|');
                                float best;
                                if ((parts.Length == 3 || parts.Length == 4) && parts[0].Length > 0 &&
                                    float.TryParse(parts[parts.Length - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out best) && best > 0f)
                                {
                                    string track = parts[0] == "Desert du Sud" ? "Désert du Sud"
                                                 : parts[0] == "Etoile du Nord" ? "Étoile du Nord" : parts[0];
                                    int[] diffs;
                                    if (parts.Length == 4)
                                    {
                                        int d;
                                        if (!int.TryParse(parts[2], out d) || d < 0 || d > 2) break;
                                        diffs = new[] { d };
                                    }
                                    else diffs = new[] { 0, 1, 2 };

                                    foreach (int d in diffs)
                                    {
                                        string bestKey = track + "|" + parts[1] + "|" + d;
                                        float old;
                                        if (!s.BestTimes.TryGetValue(bestKey, out old) || best < old) s.BestTimes[bestKey] = best;
                                    }
                                }
                            }
                            break;
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
                var lines = new List<string>
                {
                    "width=" + Width, "height=" + Height,
                    "display=" + DisplayMode, "fullscreen=" + (Fullscreen ? 1 : 0), "borderless=" + (Borderless ? 1 : 0),
                    "vsync=" + (VSync ? 1 : 0), "fps=" + FpsIndex,
                    "track=" + Track, "player=" + Player, "kartcolor=" + KartColor, "difficulty=" + Difficulty, "laps=" + Laps,
                    "pilots=" + RacerCount, "language=" + Language,
                };
                foreach (var entry in BestTimes)
                    lines.Add("best=" + entry.Key + "|" + entry.Value.ToString("R", CultureInfo.InvariantCulture));
                File.WriteAllLines(FilePath, lines);
            }
            catch { }
        }
    }
}
