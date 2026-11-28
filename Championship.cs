using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Kartamania
{
    public sealed class Championship
    {
        public sealed class Driver
        {
            public string Name;
            public Texture2D Face;
            public Color Color;
            public bool IsPlayer;
            public readonly int[] Places;
            public readonly int[] Points;

            public Driver(RacerInfo info, int rounds)
            {
                Name = info.Name;
                Face = info.Face;
                Color = info.Color;
                IsPlayer = info.IsPlayer;
                Places = new int[rounds];
                Points = new int[rounds];
            }

            public int Total
            {
                get
                {
                    int total = 0;
                    foreach (int points in Points) total += points;
                    return total;
                }
            }

            public int CountPlace(int place)
            {
                int count = 0;
                foreach (int p in Places)
                    if (p == place) count++;
                return count;
            }
        }

        public static readonly int[] PointsTable = { 25, 18, 15, 12, 10, 8 };

        public readonly List<RacerInfo> Racers;
        public readonly List<Driver> Drivers = new List<Driver>();
        public readonly int RoundCount;
        public int Round;

        public Championship(IList<RacerInfo> racers, int roundCount)
        {
            Racers = new List<RacerInfo>(racers);
            RoundCount = roundCount;
            foreach (RacerInfo r in Racers) Drivers.Add(new Driver(r, roundCount));
        }

        public bool IsLastRace => Round >= RoundCount - 1;

        public void RecordRace(Race race)
        {
            int round = (int)M.Clamp(Round, 0f, RoundCount - 1);
            for (int i = 0; i < race.Ranking.Count; i++)
            {
                int index = race.Karts.IndexOf(race.Ranking[i]);
                if (index < 0 || index >= Drivers.Count) continue;
                Driver driver = Drivers[index];
                driver.Places[round] = i + 1;
                driver.Points[round] = i < PointsTable.Length ? PointsTable[i] : 0;
            }
        }

        public List<int> GridFromLastRace()
        {
            if (Round <= 0) return null;
            int previous = Round - 1;
            for (int i = 0; i < Drivers.Count; i++)
                if (Drivers[i].Places[previous] == 0) return null;

            var order = new List<int>();
            for (int i = 0; i < Drivers.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int cmp = Drivers[a].Places[previous].CompareTo(Drivers[b].Places[previous]);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });
            return order;
        }

        public int PointsFor(int driverIndex, int round) =>
            driverIndex >= 0 && driverIndex < Drivers.Count && round >= 0 && round < RoundCount
                ? Drivers[driverIndex].Points[round] : 0;

        public List<Driver> Standings()
        {
            var list = new List<Driver>(Drivers);
            list.Sort((a, b) =>
            {
                int cmp = b.Total.CompareTo(a.Total);
                if (cmp != 0) return cmp;
                for (int place = 1; place <= RoundCount; place++)
                {
                    cmp = b.CountPlace(place).CompareTo(a.CountPlace(place));
                    if (cmp != 0) return cmp;
                }
                return 0;
            });
            return list;
        }
    }
}
