using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Kartamania
{
    public enum TrackTheme { Normal, Winter, Desert, City }

    public struct TrackGate
    {
        public int Index;
        public Vector2 Center, Normal, Tangent, A, B;

        public bool Crossed(Vector2 from, Vector2 to, out float lateral)
        {
            lateral = 0f;
            Vector2 d = to - from;
            Vector2 e = B - A;
            Vector2 diff = A - from;
            float den = d.X * e.Y - d.Y * e.X;
            if (Math.Abs(den) < 0.0001f) return false;
            float t = (diff.X * e.Y - diff.Y * e.X) / den;
            float u = (diff.X * d.Y - diff.Y * d.X) / den;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            if (Vector2.Dot(d, Tangent) <= 0f) return false;
            lateral = Vector2.Dot(from + d * t - Center, Normal);
            return true;
        }
    }

    /// <summary>Description d'une piste : une boucle fermée de points de contrôle + décor + obstacles.</summary>
    public sealed class TrackDef
    {
        public string Name;
        public Vector2[] Points;       // points de contrôle (spline Catmull-Rom fermée)
        public float Scale = 1.35f;    // facteur appliqué aux points
        public float Width = 250f;     // largeur de la route
        public Color Grass, Rough, Road;
        public TrackTheme Theme = TrackTheme.Normal;
        public float[] BoxRows;        // positions (0..1) des rangées de boîtes d'armes
        public Vector2[] Obstacles;    // X = position sur la piste (0..1), Y = décalage latéral
        public bool StrictCourse;
        public float[] Checkpoints;
        public Vector2[] CrossingPoints;
        public float LaneLimit = 80f;
    }

    /// <summary>Piste prête à l'emploi : ligne centrale échantillonnée tous les ~24 unités.</summary>
    public sealed class Track
    {
        public const float Shoulder = 100f;   // zone d'herbe entre la route et la barrière invisible

        public readonly TrackDef Def;
        public readonly int N;
        public readonly float Length, Spacing, HalfWidth;
        public readonly Vector2[] Center, Tangent, Normal;
        public readonly Vector2[] ObstaclePos, BoxPos;
        public readonly TrackGate[] Gates;
        public readonly int[] CrossingIndex;
        public readonly Vector2 Min, Max;

        public Track(TrackDef def)
        {
            Def = def;
            HalfWidth = def.Width * 0.5f;

            int n = def.Points.Length;
            const int sub = 40;
            var dense = new Vector2[n * sub];
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = def.Points[(i - 1 + n) % n] * def.Scale;
                Vector2 p1 = def.Points[i] * def.Scale;
                Vector2 p2 = def.Points[(i + 1) % n] * def.Scale;
                Vector2 p3 = def.Points[(i + 2) % n] * def.Scale;
                for (int s = 0; s < sub; s++)
                    dense[i * sub + s] = CatmullRom(p0, p1, p2, p3, s / (float)sub);
            }

            int dn = dense.Length;
            var cum = new float[dn + 1];
            for (int i = 0; i < dn; i++)
                cum[i + 1] = cum[i] + Vector2.Distance(dense[i], dense[(i + 1) % dn]);

            Length = cum[dn];
            N = Math.Max(80, (int)(Length / 24f));
            Spacing = Length / N;

            Center = new Vector2[N];
            Tangent = new Vector2[N];
            Normal = new Vector2[N];

            int seg = 0;
            Vector2 mn = new Vector2(float.MaxValue), mx = new Vector2(float.MinValue);
            for (int k = 0; k < N; k++)
            {
                float target = k * Spacing;
                while (seg < dn - 1 && cum[seg + 1] < target) seg++;
                float segLen = cum[seg + 1] - cum[seg];
                float f = segLen > 0f ? (target - cum[seg]) / segLen : 0f;
                Vector2 c = Vector2.Lerp(dense[seg], dense[(seg + 1) % dn], f);
                Center[k] = c;
                mn = new Vector2(Math.Min(mn.X, c.X), Math.Min(mn.Y, c.Y));
                mx = new Vector2(Math.Max(mx.X, c.X), Math.Max(mx.Y, c.Y));
            }
            Min = mn;
            Max = mx;

            for (int k = 0; k < N; k++)
            {
                Vector2 d = Center[(k + 1) % N] - Center[(k - 1 + N) % N];
                d.Normalize();
                Tangent[k] = d;
                Normal[k] = new Vector2(-d.Y, d.X);   // "droite" de la direction de course (écran Y vers le bas)
            }

            Gates = new TrackGate[def.Checkpoints == null ? 0 : def.Checkpoints.Length];
            for (int k = 0; k < Gates.Length; k++)
            {
                int idx = M.Wrap((int)(def.Checkpoints[k] * N), N);
                float half = HalfWidth + Shoulder * 0.8f;
                Gates[k] = new TrackGate
                {
                    Index = idx,
                    Center = Center[idx],
                    Normal = Normal[idx],
                    Tangent = Tangent[idx],
                    A = Center[idx] - Normal[idx] * half,
                    B = Center[idx] + Normal[idx] * half,
                };
            }

            var crossings = new List<int>();
            if (def.CrossingPoints != null)
            {
                float maxSq = (HalfWidth + Shoulder * 2f) * (HalfWidth + Shoulder * 2f);
                foreach (Vector2 point in def.CrossingPoints)
                {
                    for (int pass = 0; pass < 4; pass++)
                    {
                        int best = -1;
                        float bd = maxSq;
                        for (int i = 0; i < N; i++)
                        {
                            bool used = false;
                            foreach (int c in crossings)
                                if (LoopDistance(i, c) <= 40) { used = true; break; }
                            if (used) continue;
                            float d = Vector2.DistanceSquared(point, Center[i]);
                            if (d < bd) { bd = d; best = i; }
                        }
                        if (best < 0 || bd > maxSq) break;
                        crossings.Add(best);
                    }
                }
            }
            CrossingIndex = crossings.ToArray();

            ObstaclePos = new Vector2[def.Obstacles.Length];
            for (int i = 0; i < ObstaclePos.Length; i++)
            {
                int idx = ((int)(def.Obstacles[i].X * N)) % N;
                ObstaclePos[i] = Center[idx] + Normal[idx] * def.Obstacles[i].Y;
            }

            float[] lanes = { -80f, 0f, 80f };
            BoxPos = new Vector2[def.BoxRows.Length * lanes.Length];
            for (int r = 0; r < def.BoxRows.Length; r++)
            {
                int idx = ((int)(def.BoxRows[r] * N)) % N;
                for (int l = 0; l < lanes.Length; l++)
                    BoxPos[r * lanes.Length + l] = Center[idx] + Normal[idx] * lanes[l];
            }
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public int Forward(int from, int to) => M.Wrap(to - from, N);

        public int LoopDistance(int a, int b)
        {
            int d = M.Wrap(a - b, N);
            return Math.Min(d, N - d);
        }

        public bool IsNearCrossing(int idx, int radius)
        {
            if (CrossingIndex == null || CrossingIndex.Length == 0) return false;
            for (int k = 0; k < CrossingIndex.Length; k++)
                if (LoopDistance(idx, CrossingIndex[k]) <= radius) return true;
            return false;
        }

        /// <summary>Cherche l'échantillon le plus proche de p autour de l'indice "hint".</summary>
        public int Nearest(Vector2 p, int hint, int window)
        {
            int best = hint;
            float bd = float.MaxValue;
            for (int o = -window; o <= window; o++)
            {
                int i = ((hint + o) % N + N) % N;
                float d = Vector2.DistanceSquared(p, Center[i]);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }
    }

    /// <summary>
    /// Les pistes du jeu. Pour en ajouter une : copie une méthode, change les points de contrôle,
    /// les obstacles et les couleurs, puis ajoute-la à la liste All().
    /// </summary>
    public static class TrackLibrary
    {
        static Vector2 V(float x, float y) => new Vector2(x, y);

        public static TrackDef[] All() => new[] { Salon(), Lac(), SousSol(), Desert(), Etoile(), VilleHuit() };

        static TrackDef Salon() => new TrackDef
        {
            Name = "Circuit du Salon",
            Width = 260f,
            Grass = new Color(72, 150, 72), Rough = new Color(58, 125, 58), Road = new Color(78, 78, 90),
            Points = new[] { V(-1300, -650), V(0, -850), V(1300, -650), V(1850, 0), V(1300, 650), V(0, 850), V(-1300, 650), V(-1850, 0) },
            BoxRows = new[] { 0.17f, 0.42f, 0.66f, 0.88f },
            Obstacles = new[] { V(0.10f, -80), V(0.22f, 70), V(0.35f, -50), V(0.48f, 80), V(0.60f, -70), V(0.72f, 40), V(0.82f, -80), V(0.93f, 60) },
        };

        static TrackDef Lac() => new TrackDef
        {
            Name = "Lac du Chalet",
            Width = 240f,
            Theme = TrackTheme.Winter,
            Grass = new Color(205, 225, 238), Rough = new Color(170, 196, 214), Road = new Color(66, 72, 92),
            Points = new[] { V(-1500, -700), V(-500, -950), V(500, -550), V(1500, -950), V(2200, -250), V(1650, 550), V(600, 400), V(-100, 1000), V(-1100, 900), V(-1900, 350), V(-2150, -300) },
            BoxRows = new[] { 0.12f, 0.30f, 0.52f, 0.74f, 0.90f },
            Obstacles = new[]
            {
                V(0.08f, 60), V(0.15f, -70), V(0.21f, 20), V(0.27f, -60), V(0.34f, 70), V(0.41f, -30), V(0.47f, 75),
                V(0.58f, -70), V(0.64f, 40), V(0.70f, -75), V(0.78f, 60), V(0.84f, -40), V(0.89f, 70), V(0.95f, -60),
            },
        };

        static TrackDef SousSol() => new TrackDef
        {
            Name = "Rallye du Sous-sol",
            Width = 230f,
            Grass = new Color(160, 124, 76), Rough = new Color(128, 96, 58), Road = new Color(104, 98, 96),
            Points = new[] { V(-1800, -800), V(-600, -1050), V(400, -700), V(1250, -1050), V(2100, -650), V(2200, 100), V(1300, 380), V(1700, 1150), V(550, 1450), V(-300, 1000), V(-1000, 1400), V(-1900, 1150), V(-2300, 300), V(-1900, -350) },
            BoxRows = new[] { 0.10f, 0.25f, 0.40f, 0.55f, 0.70f, 0.85f },
            Obstacles = new[]
            {
                V(0.05f, 50), V(0.09f, -60), V(0.14f, 30), V(0.19f, -70), V(0.23f, 65), V(0.29f, -35), V(0.33f, 60),
                V(0.37f, -65), V(0.43f, 40), V(0.47f, -55), V(0.52f, 70), V(0.57f, -30), V(0.62f, 55), V(0.66f, -70),
                V(0.72f, 35), V(0.76f, -60), V(0.81f, 65), V(0.87f, -45), V(0.91f, 60), V(0.95f, -35),
            },
        };

        static TrackDef Desert() => new TrackDef
        {
            Name = "Desert du Sud",
            Width = 300f,
            Theme = TrackTheme.Desert,
            Grass = new Color(220, 190, 125), Rough = new Color(198, 160, 95), Road = new Color(120, 105, 92),
            Points = new[]
            {
                V(-2000, -900), V(-1000, -1100), V(1000, -1100), V(2000, -900), V(2300, -200), V(1700, 200),
                V(1800, 900), V(900, 1100), V(-900, 1100), V(-1800, 900), V(-2300, 200), V(-1700, -200),
            },
            BoxRows = new[] { 0.08f, 0.30f, 0.55f, 0.80f },
            Obstacles = new[]
            {
                V(0.14f, -100), V(0.26f, 90), V(0.38f, -100), V(0.50f, 95),
                V(0.62f, -90), V(0.74f, 100), V(0.88f, -95),
            },
        };

        static TrackDef Etoile() => new TrackDef
        {
            Name = "Etoile du Nord",
            Width = 220f,
            Scale = 1.25f,
            Theme = TrackTheme.Winter,
            Grass = new Color(35, 70, 95), Rough = new Color(28, 55, 75), Road = new Color(150, 165, 175),
            Points = new[]
            {
                V(0, -1500), V(900, -1200), V(1600, -600), V(1200, 0), V(2000, 600), V(1500, 1300), V(400, 1100),
                V(-400, 1500), V(-1400, 1100), V(-1200, 400), V(-2000, 0), V(-1400, -800), V(-600, -1200),
            },
            BoxRows = new[] { 0.12f, 0.35f, 0.58f, 0.82f },
            Obstacles = new[]
            {
                V(0.05f, 70), V(0.11f, -60), V(0.18f, 65), V(0.24f, -70), V(0.31f, 60), V(0.37f, -65),
                V(0.44f, 70), V(0.52f, -60), V(0.59f, 65), V(0.66f, -70), V(0.73f, 60), V(0.79f, -65),
                V(0.86f, 70), V(0.93f, -60),
            },
        };

        static TrackDef VilleHuit() => new TrackDef
        {
            Name = "Huit de la Ville",
            Scale = 1f,
            Width = 280f,
            Theme = TrackTheme.City,
            StrictCourse = true,
            LaneLimit = 70f,
            Grass = new Color(48, 52, 60), Rough = new Color(76, 80, 88), Road = new Color(108, 110, 116),
            Points = TrackShapes.FigureEight(2100f, 1150f, 32),
            Checkpoints = new[] { 0f, 0.12f, 0.36f, 0.50f, 0.64f, 0.88f },
            CrossingPoints = new[] { V(0, 0) },
            BoxRows = new[] { 0.06f, 0.33f, 0.56f, 0.83f },
            Obstacles = new Vector2[0],
        };
    }
}
