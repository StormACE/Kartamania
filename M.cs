using System;
using Microsoft.Xna.Framework;

namespace Kartamania
{
    /// <summary>Petits utilitaires mathématiques.</summary>
    public static class M
    {
        public const float Pi = (float)Math.PI;

        public static float WrapAngle(float a)
        {
            while (a > Pi) a -= 2f * Pi;
            while (a < -Pi) a += 2f * Pi;
            return a;
        }

        public static Vector2 Dir(float angle) => new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));

        public static Vector2 Rot(Vector2 v, float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        public static float Angle(Vector2 v) => (float)Math.Atan2(v.Y, v.X);

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        public static int Wrap(int v, int count) => ((v % count) + count) % count;
    }
}
