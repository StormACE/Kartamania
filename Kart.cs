using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Kartamania
{
    public enum ItemType { None, Missile, Mine, Boost, Shield }

    /// <summary>Infos nécessaires pour inscrire un pilote à une course.</summary>
    public sealed class RacerInfo
    {
        public string Name;
        public Texture2D Face;
        public Color Color;
        public bool IsPlayer;
    }

    public sealed class Kart
    {
        public const float Radius = 30f;
        public const int MaxHp = 4;

        public string Name;
        public Texture2D Face;
        public Color Color;
        public bool IsPlayer;

        public Vector2 Pos, Vel;
        public float Angle, Speed;
        public float Throttle, Steer;

        public int Idx, Lap = -1;
        public float Progress;
        public int Place;

        public int Hp = MaxHp;
        public bool Eliminated, Finished, OffRoad;
        public int FinishOrder, Kills;
        public float FinishTime, ElimTime;

        public float SpinTimer, SpinVisual, Invuln, BoostTimer, ShieldTimer, SlowTimer, StuckTimer;
        public ItemType Item;

        // IA
        public float AiLane, AiSkill, AiItemDelay;

        public Vector2 Heading => M.Dir(Angle);
    }
}
