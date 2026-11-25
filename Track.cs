using System;
using Microsoft.Xna.Framework;

namespace Kartamania
{
    /// <summary>Description d'une piste : une boucle fermée de points de contrôle + décor + obstacles.</summary>
    public sealed class TrackDef
    {
        public string Name;
        public Vector2[] Points;       // points de contrôle (spline Catmull-Rom fermée)
        public float Scale = 1.35f;    // facteur appliqué aux points
        public float Width = 250f;     // largeur de la route
        public Color Grass, Rough, Road;
        public float[] BoxRows;        // positions (0..1) des rangées de boîtes d'armes
        public Vector2[] Obstacles;    // X = position sur la piste (0..1), Y = décalage latéral
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

        public static TrackDef[] All() => new[] { Salon(), Lac(), SousSol(), Desert(), Etoile() };

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
    }
}
