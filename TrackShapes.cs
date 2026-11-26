using System;
using Microsoft.Xna.Framework;

namespace Kartamania
{
    public static class TrackShapes
    {
        public static Vector2[] FigureEight(float a, float b, int count)
        {
            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float t = i * 2f * (float)Math.PI / count;
                points[i] = new Vector2((float)(a * Math.Cos(t)), (float)(b * Math.Sin(2.0 * t)));
            }
            return points;
        }
    }
}
