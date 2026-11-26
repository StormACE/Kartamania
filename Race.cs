using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Kartamania
{
    public struct PlayerInput
    {
        public float Throttle, Steer;
        public bool UseItem;
    }

    public sealed class Missile { public Vector2 Pos; public float Angle, Life, Turn; public Kart Owner, Target; }
    public sealed class Mine { public Vector2 Pos; public float Arm, Life; public Kart Owner; }
    public sealed class ItemBox { public Vector2 Pos; public float Respawn; }
    public sealed class Particle { public Vector2 Pos, Vel; public float Age, Life, Size; public Color Color; }
    public sealed class FeedMessage { public string Text; public float Age; }

    /// <summary>
    /// Toute la logique d'une course (physique, IA, armes, collisions, classement).
    /// Cette classe ne dessine rien : le rendu est dans KartamaniaGame.
    /// </summary>
    public sealed class Race
    {
        public const float BaseSpeed = 480f;       // vitesse max d'un kart (unités/seconde)
        public const float MissileSpeed = 980f;
        public const float ObstacleRadius = 26f;
        public const float BoxRadius = 30f;
        public const float CollisionSlowFactor = 0.5f;
        public const float KartSlowDuration = 1f;
        public const float ObstacleSlowDuration = 1f;
        public const float RearPushMin = 240f;
        public const float RearPushMax = 520f;
        public const float RearPushScale = 0.5625f;
        public const float StuckDelay = 5f;
        public const float RespawnClearance = 120f;
        public const float GridSideOffset = 95f;
        public const int GridRowSpacing = 5;
        public const float RocketStartWindow = 0.4f;
        public const float RocketStartDuration = 1.2f;
        public const float PenaltyMessageDuration = 3.5f;

        public readonly Track Track;
        public readonly List<Kart> Karts = new List<Kart>();
        public readonly List<Missile> Missiles = new List<Missile>();
        public readonly List<Mine> Mines = new List<Mine>();
        public readonly List<ItemBox> Boxes = new List<ItemBox>();
        public readonly List<Particle> Particles = new List<Particle>();
        public readonly List<FeedMessage> Feed = new List<FeedMessage>();
        public readonly int TotalLaps;
        public readonly Kart Player;

        public float Countdown = 3f;
        public float Time;
        public float ShortcutPenaltyTimer;
        public bool Over;

        public IReadOnlyList<Kart> Ranking => ranking;

        readonly float difficultySpeed;
        readonly Random rng;
        readonly List<Kart> ranking = new List<Kart>();
        float overTimer = -1f;
        int finishCounter;
        bool rocketStart;
        bool startThrottleHeld;

        public Race(Track track, IList<RacerInfo> racers, int laps, float difficultySpeed, Random rng)
        {
            Track = track;
            TotalLaps = laps;
            this.difficultySpeed = difficultySpeed;
            this.rng = rng;

            // Place sur la grille de départ (mélangée).
            var slots = new List<int>();
            for (int i = 0; i < racers.Count; i++) slots.Add(i);
            for (int i = slots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = slots[i]; slots[i] = slots[j]; slots[j] = tmp;
            }

            for (int i = 0; i < racers.Count; i++)
            {
                RacerInfo r = racers[i];
                var k = new Kart { Name = r.Name, Face = r.Face, Color = r.Color, IsPlayer = r.IsPlayer };

                int slot = slots[i];
                int row = slot / 2;
                float side = (slot % 2 == 0) ? -1f : 1f;
                int idx = ((track.N - 4 - row * GridRowSpacing) % track.N + track.N) % track.N;

                k.Idx = idx;
                k.Lap = -1;
                k.Pos = track.Center[idx] + track.Normal[idx] * (side * GridSideOffset);
                k.Angle = M.Angle(track.Tangent[idx]);
                float laneLimit = track.Def.StrictCourse ? track.Def.LaneLimit : 80f;
                k.AiLane = ((float)rng.NextDouble() * 2f - 1f) * laneLimit;
                k.AiSkill = 0.94f + (float)rng.NextDouble() * 0.06f;
                k.AiItemDelay = 1f;
                k.NextGate = 0;
                k.LastGateIndex = track.Gates.Length > 0 ? track.Gates[track.Gates.Length - 1].Index : idx;
                k.PrevPos = k.Pos;
                Karts.Add(k);
                if (k.IsPlayer) Player = k;
            }

            foreach (Vector2 p in track.BoxPos) Boxes.Add(new ItemBox { Pos = p });

            foreach (Kart k in Karts) UpdateProgress(k);
            UpdatePlaces();
        }

        // ------------------------------------------------------------------ boucle principale

        public void Update(float dt, PlayerInput input)
        {
            UpdateFeed(dt);
            UpdateParticles(dt);

            if (Countdown > 0f)
            {
                bool pressing = input.Throttle > 0.5f;
                if (Player != null && pressing && !startThrottleHeld && Countdown <= RocketStartWindow)
                    rocketStart = true;
                startThrottleHeld = pressing;

                Countdown -= dt;
                if (Countdown > 0f) return;

                if (rocketStart && Player != null && !Player.Finished)
                {
                    Player.BoostTimer = RocketStartDuration;
                    Sfx.UseItem(ItemType.Boost);
                    Burst(Player.Pos, new Color(255, 220, 80), 18, 260f);
                    Say(Loc.Get("MSG_BOOST"));
                }
                rocketStart = false;
            }

            Time += dt;
            if (ShortcutPenaltyTimer > 0f) ShortcutPenaltyTimer -= dt;

            for (int i = 0; i < Karts.Count; i++)
            {
                Kart k = Karts[i];
                if (k.Eliminated) continue;

                if (k.IsPlayer && !k.Finished)
                {
                    k.Throttle = input.Throttle;
                    k.Steer = input.Steer;
                    if (input.UseItem) UseItem(k);
                }
                else
                {
                    Think(k, dt);   // IA (le joueur passe en pilote automatique une fois l'arrivée franchie)
                }
                StepKart(k, dt);
            }

            KartCollisions();
            ObstacleCollisions(dt);
            BoxPickups(dt);
            UpdateMissiles(dt);
            UpdateMines(dt);
            UpdatePlaces();
            CheckEnd(dt);
        }

        void CheckEnd(float dt)
        {
            if (Over) return;

            if (overTimer < 0f)
            {
                if (!Player.Finished && !Player.Eliminated)
                {
                    bool anyOpponent = false;
                    foreach (Kart k in Karts)
                        if (k != Player && !k.Eliminated) { anyOpponent = true; break; }
                    if (Karts.Count > 1 && !anyOpponent)
                    {
                        Player.Finished = true;
                        Player.FinishOrder = ++finishCounter;
                        Player.FinishTime = Time;
                        Say(Loc.Get("FEAT_VICTORY_ELIM"));
                    }
                }

                bool allDone = true;
                foreach (Kart k in Karts)
                    if (!k.Finished && !k.Eliminated) { allDone = false; break; }
                if (allDone) overTimer = 2.5f;
            }

            if (overTimer >= 0f)
            {
                overTimer -= dt;
                if (overTimer <= 0f) Over = true;
            }
        }

        // ------------------------------------------------------------------ physique d'un kart

        void StepKart(Kart k, float dt)
        {
            if (k.Invuln > 0f) k.Invuln -= dt;
            if (k.BoostTimer > 0f) k.BoostTimer -= dt;
            if (k.ShieldTimer > 0f) k.ShieldTimer -= dt;
            if (k.SlowTimer > 0f) k.SlowTimer -= dt;

            bool spinning = k.SpinTimer > 0f;
            float throttle = spinning ? 0f : k.Throttle;
            float steer = spinning ? 0f : k.Steer;

            float maxSpeed = BaseSpeed * difficultySpeed * (k.IsPlayer ? 1f : k.AiSkill);
            if (k.BoostTimer > 0f) maxSpeed *= 1.45f;
            if (k.OffRoad && !k.OutsideCourse) maxSpeed *= 0.5f;
            if (k.SlowTimer > 0f) maxSpeed *= CollisionSlowFactor;

            if (throttle > 0f)
            {
                if (k.Speed < maxSpeed)
                    k.Speed = Math.Min(maxSpeed, k.Speed + 420f * throttle * dt * (k.BoostTimer > 0f ? 2.5f : 1f));
            }
            else if (throttle < 0f)
            {
                k.Speed -= (k.Speed > 0f ? 1300f : 380f) * -throttle * dt;
                if (k.Speed < -maxSpeed * 0.35f) k.Speed = -maxSpeed * 0.35f;
            }
            else
            {
                float f = 260f * dt;
                if (k.Speed > f) k.Speed -= f;
                else if (k.Speed < -f) k.Speed += f;
                else k.Speed = 0f;
            }
            if (k.Speed > maxSpeed) k.Speed = Math.Max(maxSpeed, k.Speed - 650f * dt);

            float speedFactor = M.Clamp(Math.Abs(k.Speed) / (BaseSpeed * 0.3f), 0f, 1f);
            float turn = 2.7f * (1f - 0.3f * M.Clamp(Math.Abs(k.Speed) / (BaseSpeed * 1.2f), 0f, 1f));
            k.Angle += steer * turn * speedFactor * (k.Speed >= 0f ? 1f : -1f) * dt;

            if (k.IsPlayer)
            {
                float speedN = M.Clamp((Math.Abs(k.Speed) - BaseSpeed * 0.35f) / (BaseSpeed * 0.5f), 0f, 1f);
                float steerN = M.Clamp((Math.Abs(steer) - 0.5f) / 0.5f, 0f, 1f);
                Sfx.UpdateScreech(speedN * steerN);
            }

            if (spinning)
            {
                k.SpinTimer -= dt;
                k.SpinVisual += 15f * dt;
                k.Speed *= Math.Max(0f, 1f - 2.2f * dt);
            }
            else if (k.SpinVisual != 0f)
            {
                k.SpinVisual = 0f;
            }

            Vector2 desired = M.Dir(k.Angle) * k.Speed;
            k.Vel = Vector2.Lerp(k.Vel, desired, M.Clamp(8f * dt, 0f, 1f));   // un peu de dérapage
            Vector2 prev = k.Pos;
            k.PrevPos = prev;
            k.Pos += k.Vel * dt;

            TrackState(k, dt, prev);
        }

        void TrackState(Kart k, float dt, Vector2 prev)
        {
            if (Track.Def.StrictCourse) TrackStateStrict(k, dt, prev);
            else TrackStateLegacy(k, dt);
        }

        void TrackStateLegacy(Kart k, float dt)
        {
            int old = k.Idx;
            int idx = Track.Nearest(k.Pos, old, 28);
            int d = idx - old;
            if (d < -Track.N / 2) { k.Lap++; if (k.IsPlayer) Sfx.Lap(); }
            else if (d > Track.N / 2) k.Lap--;
            k.Idx = idx;

            Vector2 off = k.Pos - Track.Center[idx];
            float dist = off.Length();
            k.OffRoad = dist > Track.HalfWidth;

            float limit = Track.HalfWidth + Track.Shoulder;   // barrière invisible
            if (dist > limit)
            {
                Vector2 n = off / dist;
                k.Pos = Track.Center[idx] + n * limit;
                float vn = Vector2.Dot(k.Vel, n);
                if (vn > 0f) k.Vel -= n * vn * 1.3f;
                k.Speed *= Math.Max(0f, 1f - 3f * dt);
                Sfx.Wall(k.IsPlayer);
            }

            UpdateProgress(k);
            CheckFinish(k);
        }

        void TrackStateStrict(Kart k, float dt, Vector2 prev)
        {
            int old = k.Idx;
            bool junction = false;
            float crossR = Track.HalfWidth + Track.Shoulder * 2f;
            for (int c = 0; c < Track.CrossingIndex.Length; c++)
            {
                if ((k.Pos - Track.Center[Track.CrossingIndex[c]]).Length() <= crossR) { junction = true; break; }
            }

            int idx = junction ? Track.NearestAny(k.Pos) : Track.Nearest(k.Pos, old, 10);
            k.Idx = idx;

            Vector2 off = k.Pos - Track.Center[idx];
            float dist = off.Length();

            if (junction && Track.LoopDistance(old, idx) > Track.N / 8)
            {
                if (k.WrongBranch) k.WrongBranch = false;
                else if (!k.OutsideCourse)
                {
                    k.WrongBranch = true;
                    k.WrongBranchPos = k.Pos;
                }
            }
            if (k.WrongBranch && !k.OutsideCourse &&
                (k.Pos - k.WrongBranchPos).Length() > Track.HalfWidth + Track.Shoulder)
            {
                k.WrongBranch = false;
                PenalizeShortcut(k);
            }

            k.OffRoad = dist > Track.HalfWidth && !junction;

            float limit = Track.HalfWidth + Track.Shoulder;
            if (dist > limit && !junction)
            {
                Vector2 n = off / dist;
                k.Pos = Track.Center[idx] + n * limit;
                float vn = Vector2.Dot(k.Vel, n);
                if (vn > 0f) k.Vel -= n * vn * 1.3f;
            }

            if (Track.Gates.Length > 0)
            {
                int next = k.NextGate % Track.Gates.Length;
                TrackGate gate = Track.Gates[next];
                float lateral;
                if (gate.Crossed(prev, k.Pos, out lateral))
                {
                    if (next == 0)
                    {
                        k.Lap++;
                        k.OutsideCourse = false;
                        k.WrongBranch = false;
                        if (k.IsPlayer) Sfx.Lap();
                    }
                    k.LastGateIndex = gate.Index;
                    k.NextGate = (next + 1) % Track.Gates.Length;
                }
            }

            k.PrevPos = k.Pos;
            UpdateProgress(k);
            CheckFinish(k);
        }

        void PenalizeShortcut(Kart k)
        {
            k.OutsideCourse = true;
            k.WrongBranch = false;
            if (k.Finished || k.Eliminated) return;

            if (k.Lap > -1) k.Lap--;
            k.NextGate = 0;
            k.LastGateIndex = Track.Gates.Length > 0 ? Track.Gates[Track.Gates.Length - 1].Index : k.Idx;
            if (k.IsPlayer) ShortcutPenaltyTimer = PenaltyMessageDuration;
            Burst(k.Pos, Color.OrangeRed, 18, 320f);
            Sfx.Wall(k.IsPlayer);
        }

        void CheckFinish(Kart k)
        {
            if (!k.Finished && k.Lap >= TotalLaps)
            {
                k.Finished = true;
                k.FinishOrder = ++finishCounter;
                k.FinishTime = Time;
                Say(k.Name + Loc.Get("MSG_FINISHED") + Loc.Ordinal(k.FinishOrder) + "!");
            }
        }

        void UpdateProgress(Kart k)
        {
            float along = Vector2.Dot(k.Pos - Track.Center[k.Idx], Track.Tangent[k.Idx]) / Track.Spacing;
            k.Progress = k.Lap * Track.N + k.Idx + along;
        }



        // ------------------------------------------------------------------ IA

        void Think(Kart k, float dt)
        {
            int look = 6 + (int)(Math.Abs(k.Speed) / 75f);
            int ti = (k.Idx + look) % Track.N;
            if (Track.Def.StrictCourse && Track.Gates.Length > 0)
            {
                int gate = Track.Gates[k.NextGate % Track.Gates.Length].Index;
                if (Track.Forward(k.Idx, gate) <= look + 10) ti = gate;
            }
            Vector2 target = Track.Center[ti] + Track.Normal[ti] * k.AiLane;
            Vector2 to = target - k.Pos;
            float diff = M.WrapAngle(M.Angle(to) - k.Angle);
            float steer = M.Clamp(diff * 2.4f, -1f, 1f);
            if (Track.Def.StrictCourse)
            {
                float lane = Vector2.Dot(k.Pos - Track.Center[k.Idx], Track.Normal[k.Idx]);
                steer += M.Clamp((k.AiLane - lane) * 0.006f, -0.3f, 0.3f);
            }
            float sharp = Math.Abs(diff);

            float throttle = 1f;

            // Évite les obstacles et les mines devant lui.
            Vector2 fwd = k.Heading;
            Vector2 right = new Vector2(-fwd.Y, fwd.X);
            foreach (Vector2 o in Track.ObstaclePos) steer += Avoid(k.Pos, fwd, right, o, 70f);
            foreach (Mine m in Mines) steer += Avoid(k.Pos, fwd, right, m.Pos, 80f);
            steer = M.Clamp(steer, -1f, 1f);

            k.Steer = steer;
            k.Throttle = throttle;

            // Utilisation des armes.
            k.AiItemDelay -= dt;
            if (!k.Finished && k.Item != ItemType.None && k.AiItemDelay <= 0f)
            {
                bool use = false;
                switch (k.Item)
                {
                    case ItemType.Boost: use = sharp < 0.15f; break;
                    case ItemType.Shield: use = true; break;
                    case ItemType.Missile: use = FindTarget(k, 0.95f) != null && rng.NextDouble() < 0.02; break;
                    case ItemType.Mine: use = SomeoneBehind(k); break;
                }
                if (use)
                {
                    UseItem(k);
                    k.AiItemDelay = 2.5f + (float)rng.NextDouble() * 3f;
                }
            }
        }

        static float Avoid(Vector2 pos, Vector2 fwd, Vector2 right, Vector2 obj, float halfWidth)
        {
            Vector2 v = obj - pos;
            float ahead = Vector2.Dot(v, fwd);
            if (ahead < 20f || ahead > 320f) return 0f;
            float lat = Vector2.Dot(v, right);
            if (Math.Abs(lat) > halfWidth) return 0f;
            return (lat >= 0f ? -1f : 1f) * (1f - ahead / 320f) * 1.4f;
        }

        bool SomeoneBehind(Kart k)
        {
            Vector2 fwd = k.Heading;
            Vector2 right = new Vector2(-fwd.Y, fwd.X);
            foreach (Kart o in Karts)
            {
                if (o == k || o.Eliminated || o.Finished) continue;
                Vector2 v = o.Pos - k.Pos;
                float back = -Vector2.Dot(v, fwd);
                if (back > 80f && back < 500f && Math.Abs(Vector2.Dot(v, right)) < 100f) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ armes

        public Kart FindTarget(Kart k, float minCos)
        {
            Kart best = null;
            float bd = 1500f;
            Vector2 fwd = k.Heading;
            foreach (Kart o in Karts)
            {
                if (o == k || o.Eliminated || o.Finished) continue;
                Vector2 v = o.Pos - k.Pos;
                float d = v.Length();
                if (d < 60f || d >= bd) continue;
                if (Vector2.Dot(v / d, fwd) < minCos) continue;
                best = o;
                bd = d;
            }
            return best;
        }

        public void UseItem(Kart k)
        {
            if (k.Item == ItemType.None) return;
            Sfx.UseItem(k.Item);
            switch (k.Item)
            {
                case ItemType.Missile:
                    Kart target = FindTarget(k, k.IsPlayer ? 0.7f : 0.9f);
                    Missiles.Add(new Missile
                    {
                        Pos = k.Pos + k.Heading * (Kart.Radius + 24f),
                        Angle = k.Angle,
                        Life = 3.5f,
                        Turn = k.IsPlayer ? 3.4f : 2.0f,
                        Owner = k,
                        Target = target,
                    });
                    break;
                case ItemType.Mine:
                    Mines.Add(new Mine { Pos = k.Pos - k.Heading * (Kart.Radius + 24f), Arm = 0.7f, Life = 45f, Owner = k });
                    break;
                case ItemType.Boost:
                    k.BoostTimer = 1.8f;
                    k.Speed = Math.Max(k.Speed, BaseSpeed * 1.1f);
                    Burst(k.Pos - k.Heading * 40f, Color.Orange, 8, 200f);
                    break;
                case ItemType.Shield:
                    k.ShieldTimer = 8f;
                    break;
                default:
                    return;
            }
            k.Item = ItemType.None;
        }

        ItemType RollItem()
        {
            int r = rng.Next(100);
            if (r < 25) return ItemType.Missile;
            if (r < 45) return ItemType.Mine;
            if (r < 80) return ItemType.Boost;
            return ItemType.Shield;
        }

        void BoxPickups(float dt)
        {
            float rr = (Kart.Radius + BoxRadius) * (Kart.Radius + BoxRadius);
            foreach (ItemBox b in Boxes)
            {
                if (b.Respawn > 0f) { b.Respawn -= dt; continue; }
                foreach (Kart k in Karts)
                {
                    if (k.Eliminated || k.Finished || k.OutsideCourse || k.Item != ItemType.None) continue;
                    if (Vector2.DistanceSquared(k.Pos, b.Pos) > rr) continue;
                    k.Item = RollItem();
                    k.AiItemDelay = 1.5f + (float)rng.NextDouble() * 3f;
                    b.Respawn = 8f;
                    Burst(b.Pos, Color.Gold, 8, 160f);
                    Sfx.Pickup(k.IsPlayer);
                    break;
                }
            }
        }

        void UpdateMissiles(float dt)
        {
            for (int i = Missiles.Count - 1; i >= 0; i--)
            {
                Missile m = Missiles[i];
                if (m.Target != null && (m.Target.Eliminated || m.Target.Finished)) m.Target = null;
                if (m.Target != null)
                {
                    float want = M.Angle(m.Target.Pos - m.Pos);
                    float maxTurn = m.Turn * dt;
                    m.Angle += M.Clamp(M.WrapAngle(want - m.Angle), -maxTurn, maxTurn);
                }
                m.Pos += M.Dir(m.Angle) * MissileSpeed * dt;
                m.Life -= dt;

                bool dead = m.Life <= 0f;
                if (!dead)
                {
                    float rr = (Kart.Radius + 16f) * (Kart.Radius + 16f);
                    foreach (Kart k in Karts)
                    {
                        if (k.Eliminated || k.Finished || k == m.Owner) continue;
                        if (Vector2.DistanceSquared(k.Pos, m.Pos) < rr)
                        {
                            Hit(k, m.Owner, "MISSILE");
                            dead = true;
                            break;
                        }
                    }
                }
                if (!dead)
                {
                    float ro = (ObstacleRadius + 12f) * (ObstacleRadius + 12f);
                    foreach (Vector2 o in Track.ObstaclePos)
                        if (Vector2.DistanceSquared(o, m.Pos) < ro) { dead = true; break; }
                }
                if (dead)
                {
                    Burst(m.Pos, Color.OrangeRed, 10, 260f);
                    Sfx.Explosion(m.Owner != null && m.Owner.IsPlayer);
                    Missiles.RemoveAt(i);
                }
            }
        }

        void UpdateMines(float dt)
        {
            for (int i = Mines.Count - 1; i >= 0; i--)
            {
                Mine m = Mines[i];
                m.Arm -= dt;
                m.Life -= dt;
                bool dead = m.Life <= 0f;
                if (!dead)
                {
                    float rr = (Kart.Radius + 20f) * (Kart.Radius + 20f);
                    foreach (Kart k in Karts)
                    {
                        if (k.Eliminated || k.Finished) continue;
                        if (k == m.Owner && m.Arm > 0f) continue;
                        if (Vector2.DistanceSquared(k.Pos, m.Pos) < rr)
                        {
                            Hit(k, m.Owner, "MINE");
                            dead = true;
                            break;
                        }
                    }
                }
                if (dead)
                {
                    Burst(m.Pos, Color.OrangeRed, 14, 300f);
                    Sfx.Explosion(m.Owner != null && m.Owner.IsPlayer);
                    Mines.RemoveAt(i);
                }
            }
        }

        /// <summary>Un kart est touché : bouclier -> bloque ; sinon perd 1 point de vie ; à 0 il est éliminé.</summary>
        void Hit(Kart v, Kart attacker, string weapon)
        {
            if (v.Eliminated || v.Finished || v.Invuln > 0f) return;

            if (v.ShieldTimer > 0f)
            {
                v.ShieldTimer = 0f;
                v.Invuln = 0.8f;
                Burst(v.Pos, Color.Cyan, 12, 220f);
                Sfx.Shield(v.IsPlayer);
                Say(v.Name + Loc.Get("MSG_BLOCKED") + weapon + "!");
                return;
            }

            v.Hp--;
            Burst(v.Pos, Color.OrangeRed, 16, 300f);
            Sfx.Explosion(v.IsPlayer);

            if (v.Hp <= 0)
            {
                v.Eliminated = true;
                v.ElimTime = Time;
                v.Speed = 0f;
                v.Vel = Vector2.Zero;
                Burst(v.Pos, Color.Orange, 30, 420f);
                if (attacker != null && attacker != v)
                {
                    attacker.Kills++;
                    Say(attacker.Name + Loc.Get("MSG_ELIMINATED") + v.Name + "!");
                }
                else
                {
                    Say(v.Name + Loc.Get("MSG_SELF_ELIM"));
                }
            }
            else
            {
                v.SpinTimer = 1.4f;
                v.Invuln = 3.5f;
                v.Speed *= 0.15f;
                v.Vel *= 0.3f;
                if (attacker != null && attacker != v) Say(attacker.Name + Loc.Get("MSG_HIT") + v.Name);
            }
        }

        // ------------------------------------------------------------------ collisions

        void ApplyCollisionSlow(Kart k, float duration)
        {
            k.SlowTimer = Math.Max(k.SlowTimer, duration);
            k.Speed = Math.Min(k.Speed, BaseSpeed * CollisionSlowFactor);
        }

        static Kart BehindOf(Kart a, Kart b, Vector2 n)
        {
            Vector2 fwd = a.Vel + b.Vel;
            if (fwd.LengthSquared() < 400f) fwd = a.Heading + b.Heading;
            if (fwd.LengthSquared() < 0.01f) return null;
            float along = Vector2.Dot(n, Vector2.Normalize(fwd));
            if (Math.Abs(along) < 0.4f) return null;
            return along > 0f ? a : b;
        }

        void PushFromBehind(Kart behind, Kart ahead, float impact)
        {
            float force = M.Clamp(impact * 1.2f + behind.Speed * 0.5f, RearPushMin, RearPushMax) * RearPushScale;
            ahead.SlowTimer = 0f;
            ahead.SpinTimer = 0f;
            ahead.Speed = Math.Max(ahead.Speed, Math.Min(behind.Speed + force, BaseSpeed * 1.45f * RearPushScale));
            ahead.Vel += ahead.Heading * force;
            ApplyCollisionSlow(behind, KartSlowDuration);
        }

        void KartCollisions()
        {
            const float minD = Kart.Radius * 1.7f;
            for (int i = 0; i < Karts.Count; i++)
            {
                Kart a = Karts[i];
                if (a.Eliminated || a.Finished) continue;
                for (int j = i + 1; j < Karts.Count; j++)
                {
                    Kart b = Karts[j];
                    if (b.Eliminated || b.Finished) continue;
                    Vector2 d = b.Pos - a.Pos;
                    float dist = d.Length();
                    if (dist >= minD || dist < 0.001f) continue;

                    Vector2 n = d / dist;
                    float push = (minD - dist) * 0.5f;
                    a.Pos -= n * push;
                    b.Pos += n * push;

                    float rel = Vector2.Dot(a.Vel - b.Vel, n);
                    if (rel > 0f)
                    {
                        Sfx.KartCollision(rel / BaseSpeed, a.IsPlayer || b.IsPlayer);
                        Vector2 imp = n * (rel * 0.6f);
                        a.Vel -= imp;
                        b.Vel += imp;

                        Kart behind = BehindOf(a, b, n);
                        if (behind != null) PushFromBehind(behind, behind == a ? b : a, rel);
                        else
                        {
                            ApplyCollisionSlow(a, KartSlowDuration);
                            ApplyCollisionSlow(b, KartSlowDuration);
                        }
                    }
                }
            }
        }

        void ObstacleCollisions(float dt)
        {
            float min = Kart.Radius * 0.9f + ObstacleRadius;
            float contact = min + 10f;
            foreach (Kart k in Karts)
            {
                if (k.Eliminated || k.Finished)
                {
                    k.StuckTimer = 0f;
                    continue;
                }

                bool touching = false;
                foreach (Vector2 o in Track.ObstaclePos)
                {
                    Vector2 d = k.Pos - o;
                    float dist = d.Length();
                    if (dist < contact) touching = true;
                    if (dist >= min || dist < 0.001f) continue;

                    Vector2 n = d / dist;
                    k.Pos = o + n * min;
                    float vn = Vector2.Dot(k.Vel, n);
                    if (vn < 0f) k.Vel -= n * (vn * 1.5f);
                    ApplyCollisionSlow(k, ObstacleSlowDuration);
                    if (k.SpinTimer <= 0f)
                    {
                        k.SpinTimer = 0.7f;
                        Burst(o, Color.Orange, 8, 160f);
                        Sfx.ObstacleCollision(k.IsPlayer);
                    }
                }

                if (touching)
                {
                    k.StuckTimer += dt;
                    if (k.StuckTimer >= StuckDelay) Respawn(k);
                }
                else k.StuckTimer = 0f;
            }
        }

        bool SafeRespawn(int idx)
        {
            float clear = RespawnClearance * RespawnClearance;
            foreach (Vector2 o in Track.ObstaclePos)
                if (Vector2.DistanceSquared(Track.Center[idx], o) < clear) return false;
            return true;
        }

        int FindSafeRespawn(int idx)
        {
            for (int off = 0; off <= Track.N / 2; off++)
            {
                int ahead = (idx + off) % Track.N;
                int behind = ((idx - off) % Track.N + Track.N) % Track.N;
                if (SafeRespawn(ahead)) return ahead;
                if (behind != ahead && SafeRespawn(behind)) return behind;
            }
            return idx;
        }

        void Respawn(Kart k)
        {
            int idx = FindSafeRespawn(k.Idx);
            k.Idx = idx;
            k.Pos = Track.Center[idx];
            k.Angle = M.Angle(Track.Tangent[idx]);
            k.Speed = 0f;
            k.Vel = Vector2.Zero;
            k.SpinTimer = 0f;
            k.SpinVisual = 0f;
            k.SlowTimer = 0f;
            k.StuckTimer = 0f;
            k.OffRoad = false;
            k.OutsideCourse = false;
            k.WrongBranch = false;
            UpdateProgress(k);
            Burst(k.Pos, Color.Cyan, 12, 200f);
            Sfx.Respawn(k.IsPlayer);
            Say(k.Name + Loc.Get("MSG_RESPAWN"));
        }

        // ------------------------------------------------------------------ classement, effets

        void UpdatePlaces()
        {
            ranking.Clear();
            ranking.AddRange(Karts);
            ranking.Sort(CompareRank);
            for (int i = 0; i < ranking.Count; i++) ranking[i].Place = i + 1;
        }

        static int Group(Kart k) => k.Finished ? 0 : (k.Eliminated ? 2 : 1);

        static int CompareRank(Kart a, Kart b)
        {
            int ga = Group(a), gb = Group(b);
            if (ga != gb) return ga.CompareTo(gb);
            switch (ga)
            {
                case 0: return a.FinishOrder.CompareTo(b.FinishOrder);
                case 1: return b.Progress.CompareTo(a.Progress);
                default: return b.ElimTime.CompareTo(a.ElimTime);
            }
        }

        void Say(string text)
        {
            Feed.Add(new FeedMessage { Text = text });
            if (Feed.Count > 5) Feed.RemoveAt(0);
        }

        void UpdateFeed(float dt)
        {
            for (int i = Feed.Count - 1; i >= 0; i--)
            {
                Feed[i].Age += dt;
                if (Feed[i].Age > 5f) Feed.RemoveAt(i);
            }
        }

        void Burst(Vector2 pos, Color color, int count, float speed)
        {
            if (Particles.Count > 600) return;
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * 2f * M.Pi;
                float s = speed * (0.3f + 0.7f * (float)rng.NextDouble());
                Particles.Add(new Particle
                {
                    Pos = pos,
                    Vel = M.Dir(a) * s,
                    Life = 0.45f + (float)rng.NextDouble() * 0.4f,
                    Size = 14f + (float)rng.NextDouble() * 18f,
                    Color = color,
                });
            }
        }

        void UpdateParticles(float dt)
        {
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                Particle p = Particles[i];
                p.Age += dt;
                p.Pos += p.Vel * dt;
                p.Vel *= Math.Max(0f, 1f - 3f * dt);
                if (p.Age >= p.Life) Particles.RemoveAt(i);
            }
        }
    }
}
