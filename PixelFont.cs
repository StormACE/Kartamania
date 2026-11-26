using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Kartamania
{
    /// <summary>
    /// Rendu de texte : utilise une vraie police vectorielle (Segoe UI / Arial / Tahoma) via FontStashSharp,
    /// nette à toutes les tailles et sans Content Pipeline. Si aucune police système n'est trouvée,
    /// on retombe sur l'ancienne petite police « pixel » 3x5 intégrée au code.
    /// </summary>
    public static class PixelFont
    {
        // ------------------------------------------------------------------ ancienne police 3x5 (secours)
        // 15 caractères = 5 lignes de 3 pixels (de haut en bas).
        static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            ['A'] = "010101111101101", ['B'] = "110101110101110", ['C'] = "011100100100011",
            ['D'] = "110101101101110", ['E'] = "111100110100111", ['F'] = "111100110100100",
            ['G'] = "011100101101011", ['H'] = "101101111101101", ['I'] = "111010010010111",
            ['J'] = "001001001101010", ['K'] = "101101110101101", ['L'] = "100100100100111",
            ['M'] = "101111111101101", ['N'] = "110101101101101", ['O'] = "010101101101010",
            ['P'] = "110101110100100", ['Q'] = "010101101110011", ['R'] = "110101110101101",
            ['S'] = "011100010001110", ['T'] = "111010010010010", ['U'] = "101101101101111",
            ['V'] = "101101101101010", ['W'] = "101101111111101", ['X'] = "101101010101101",
            ['Y'] = "101101010010010", ['Z'] = "111001010100111",
            ['0'] = "111101101101111", ['1'] = "010110010010111", ['2'] = "110001010100111",
            ['3'] = "110001010001110", ['4'] = "101101111001001", ['5'] = "111100110001110",
            ['6'] = "011100111101111", ['7'] = "111001010010010", ['8'] = "111101111101111",
            ['9'] = "111101111001110",
            [':'] = "000010000010000", ['/'] = "001001010100100", ['-'] = "000000111000000",
            ['.'] = "000000000000010", ['!'] = "010010010000010", ['<'] = "001010100010001",
            ['>'] = "100010001010100", ['+'] = "000010111010000", ['?'] = "110001010000010",
        };

        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        // ------------------------------------------------------------------ police vectorielle
        // Multiplicateur taille : l'ancienne police avait 5*scale px de haut ; une vraie police a une
        // hauteur de capitale d'environ 0.7*taille, donc taille ≈ 7*scale pour une hauteur visuelle équivalente.
        const float SizeFactor = 7f;

        static FontSystem fontSystem;
        static bool initDone;
        static readonly Dictionary<int, SpriteFontBase> fonts = new Dictionary<int, SpriteFontBase>();

        static void EnsureInit()
        {
            if (initDone) return;
            initDone = true;
            try
            {
                byte[] data = LoadSystemFont();
                if (data != null)
                {
                    fontSystem = new FontSystem();
                    fontSystem.AddFont(data);
                }
            }
            catch { fontSystem = null; }
        }

        static byte[] LoadSystemFont()
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

            string[] preferred = { "segoeuib.ttf", "segoeui.ttf", "arialbd.ttf", "arial.ttf", "tahoma.ttf", "verdana.ttf", "calibri.ttf" };
            foreach (string name in preferred)
            {
                try
                {
                    string p = Path.Combine(dir, name);
                    if (File.Exists(p)) return File.ReadAllBytes(p);
                }
                catch { }
            }
            try
            {
                foreach (string p in Directory.GetFiles(dir, "*.ttf"))
                {
                    try { return File.ReadAllBytes(p); } catch { }
                }
            }
            catch { }
            return null;
        }

        static SpriteFontBase GetFont(int scale)
        {
            EnsureInit();
            if (fontSystem == null) return null;
            int size = (int)Math.Round(scale * SizeFactor);
            if (size < 11) size = 11;
            if (size > 240) size = 240;
            if (!fonts.TryGetValue(size, out SpriteFontBase f))
            {
                f = fontSystem.GetFont(size);
                fonts[size] = f;
            }
            return f;
        }

        /// <summary>À appeler quand le périphérique graphique est réinitialisé (changement de résolution/écran).</summary>
        public static void Reset()
        {
            fonts.Clear();
            try { fontSystem?.Reset(); } catch { }
        }

        // ------------------------------------------------------------------ API publique (inchangée)

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (cache.TryGetValue(s, out string hit)) return hit;
            string r = s.ToUpperInvariant().Normalize(NormalizationForm.FormC);
            if (cache.Count > 800) cache.Clear();
            cache[s] = r;
            return r;
        }

        public static int Measure(string text, int scale)
        {
            SpriteFontBase f = GetFont(scale);
            string t = Clean(text);
            if (t.Length == 0) return 0;
            if (f == null) return Math.Max(0, t.Length * 4 * scale - scale);

            Vector2 size = f.MeasureString(t);
            return (int)Math.Ceiling(size.X);
        }

        /// <summary>Hauteur d'une ligne de texte pour l'échelle donnée (sert à espacer correctement les blocs).</summary>
        public static int LineHeight(int scale)
        {
            SpriteFontBase f = GetFont(scale);
            if (f == null) return Math.Max(1, 6 * scale);
            return (int)Math.Ceiling(f.MeasureString("Ag").Y);
        }

        public static void Draw(SpriteBatch sb, Texture2D pixel, string text, int x, int y, int scale, Color color,
                                bool center = false, bool shadow = true)
        {
            string t = Clean(text);
            if (t.Length == 0) return;

            SpriteFontBase f = GetFont(scale);
            if (f == null)
            {
                int px0 = center ? x - Math.Max(0, t.Length * 4 * scale - scale) / 2 : x;
                if (shadow) Render(sb, pixel, t, px0 + scale, y + scale, scale, Color.Black * 0.6f);
                Render(sb, pixel, t, px0, y, scale, color);
                return;
            }

            float width = f.MeasureString(t).X;
            float px = center ? x - width * 0.5f : x;
            if (shadow)
                sb.DrawString(f, t, new Vector2(px + Math.Max(1f, scale), y + Math.Max(1f, scale)), Color.Black * 0.6f);
            sb.DrawString(f, t, new Vector2(px, y), color);
        }

        static readonly Dictionary<char, char> fallbackLetters = new Dictionary<char, char>();

        static char FallbackLetter(char c)
        {
            if (Glyphs.ContainsKey(c)) return c;
            char result;
            if (fallbackLetters.TryGetValue(c, out result)) return result;
            result = c;
            string d = c.ToString().Normalize(NormalizationForm.FormD);
            foreach (char part in d)
                if (CharUnicodeInfo.GetUnicodeCategory(part) != UnicodeCategory.NonSpacingMark && Glyphs.ContainsKey(part))
                {
                    result = part;
                    break;
                }
            fallbackLetters[c] = result;
            return result;
        }

        static void Render(SpriteBatch sb, Texture2D pixel, string t, int x, int y, int scale, Color color)
        {
            int pen = x;
            for (int n = 0; n < t.Length; n++)
            {
                if (Glyphs.TryGetValue(FallbackLetter(t[n]), out string g))
                {
                    for (int i = 0; i < 15; i++)
                        if (g[i] == '1')
                            sb.Draw(pixel, new Rectangle(pen + (i % 3) * scale, y + (i / 3) * scale, scale, scale), color);
                }
                pen += 4 * scale;
            }
        }
    }
}

