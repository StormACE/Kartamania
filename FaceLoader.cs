using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Kartamania
{
    public sealed class FaceInfo
    {
        public string Name;
        public Texture2D Texture;
        public bool IsPlaceholder;
    }

    /// <summary>
    /// Charge les images du dossier "Faces" (png, jpg, bmp, gif). Les PNG avec transparence gardent
    /// leur fond transparent ; les photos opaques deviennent des médaillons ronds. Le nom du fichier devient le nom du pilote.
    /// </summary>
    public static class FaceLoader
    {
        public const int Size = 128;
        static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        public static string FolderPath => Path.Combine(AppContext.BaseDirectory, "Faces");

        public static List<FaceInfo> LoadAll(GraphicsDevice gd, out string warning)
        {
            var result = new List<FaceInfo>();
            int failed = 0;
            warning = "";
            try
            {
                Directory.CreateDirectory(FolderPath);
                string[] files = Directory.GetFiles(FolderPath);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string path in files)
                {
                    if (Array.IndexOf(Extensions, Path.GetExtension(path).ToLowerInvariant()) < 0) continue;
                    try
                    {
                        string name = Path.GetFileNameWithoutExtension(path).Replace('_', ' ').Replace('-', ' ').Trim();
                        if (name.Length > 12) name = name.Substring(0, 12);
                        result.Add(new FaceInfo { Name = name, Texture = LoadRound(gd, path) });
                    }
                    catch
                    {
                        failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                warning = Loc.Get("FACES_UNREADABLE") + ex.Message;
            }
            if (failed > 0) warning = failed + Loc.Get("PHOTOS_UNREADABLE");
            return result;
        }

        /// <summary>Photo -> carré centré (légèrement vers le haut) -> médaillon rond avec bordure blanche.</summary>
        static Texture2D LoadRound(GraphicsDevice gd, string path)
        {
            Color[] src;
            int w, h;
            using (var fs = File.OpenRead(path))
            using (var tex = Texture2D.FromStream(gd, fs))
            {
                w = tex.Width;
                h = tex.Height;
                src = new Color[w * h];
                tex.GetData(src);
            }

            bool transparent = false;
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i].A < 255) { transparent = true; break; }
            }

            var dst = new Color[Size * Size];
            if (transparent)
            {
                float scale = Math.Min((float)Size / w, (float)Size / h);
                int dw = Math.Max(1, (int)(w * scale));
                int dh = Math.Max(1, (int)(h * scale));
                int ox = (Size - dw) / 2;
                int oy = (Size - dh) / 2;
                for (int y = 0; y < dh; y++)
                {
                    int sy = Math.Min(h - 1, (int)(y / scale));
                    for (int x = 0; x < dw; x++)
                    {
                        int sx = Math.Min(w - 1, (int)(x / scale));
                        dst[(y + oy) * Size + x + ox] = src[sy * w + sx];
                    }
                }
            }
            else
            {
                int side = Math.Min(w, h);
                int ox = (w - side) / 2;
                int oy = (int)((h - side) * 0.3f);
                float block = side / (float)Size;
                int n = Math.Max(1, Math.Min(4, (int)Math.Ceiling(block)));

                float r = Size * 0.5f;
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float dx = x + 0.5f - r, dy = y + 0.5f - r;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                        float mask = M.Clamp(r - dist, 0f, 1f);
                        if (mask <= 0f) continue;

                        if (dist > r - 5f)
                        {
                            dst[y * Size + x] = Color.White * mask;
                            continue;
                        }

                        float sr = 0f, sg = 0f, sb = 0f;
                        for (int j = 0; j < n; j++)
                        {
                            for (int i = 0; i < n; i++)
                            {
                                int px = ox + (int)((x + (i + 0.5f) / n) * block);
                                int py = oy + (int)((y + (j + 0.5f) / n) * block);
                                if (px >= w) px = w - 1;
                                if (py >= h) py = h - 1;
                                Color c = src[py * w + px];
                                sr += c.R; sg += c.G; sb += c.B;
                            }
                        }
                        float k = 1f / (n * n * 255f);
                        dst[y * Size + x] = new Color(sr * k * mask, sg * k * mask, sb * k * mask, mask);
                    }
                }
            }

            var result = new Texture2D(gd, Size, Size);
            result.SetData(dst);
            return result;
        }

        /// <summary>Visage "robot" dessiné en code, utilisé quand il y a moins de photos que de pilotes.</summary>
        public static Texture2D MakePlaceholder(GraphicsDevice gd, Color body)
        {
            var dst = new Color[Size * Size];
            float r = Size * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float mask = M.Clamp(r - dist, 0f, 1f);
                    if (mask <= 0f) continue;

                    Color c = body;
                    if (dist > r - 5f) c = Color.White;

                    float le = (float)Math.Sqrt((dx + 0.28f * r) * (dx + 0.28f * r) + (dy + 0.15f * r) * (dy + 0.15f * r));
                    float re = (float)Math.Sqrt((dx - 0.28f * r) * (dx - 0.28f * r) + (dy + 0.15f * r) * (dy + 0.15f * r));
                    if (le < 0.13f * r || re < 0.13f * r) c = new Color(30, 30, 40);

                    float sm = (float)Math.Sqrt(dx * dx + (dy - 0.05f * r) * (dy - 0.05f * r));
                    if (dy > 0.15f * r && Math.Abs(sm - 0.5f * r) < 0.07f * r) c = new Color(30, 30, 40);

                    dst[y * Size + x] = c * mask;
                }
            }
            var tex = new Texture2D(gd, Size, Size);
            tex.SetData(dst);
            return tex;
        }
    }
}
