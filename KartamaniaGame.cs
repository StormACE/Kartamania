using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Kartamania
{
    public sealed class KartamaniaGame : Game
    {
        enum State { Menu, Racing, Paused, Results }
        enum Row { Play, Settings, Track, Player, KartColor, Difficulty, Laps, Pilots, Resolution, Display, VSync, Fps, Language, Back, Quit }

        static readonly Row[] MainRows = { Row.Play, Row.Settings, Row.Quit };
        static readonly Row[] SettingsRows =
        {
            Row.Track, Row.Player, Row.KartColor, Row.Difficulty, Row.Laps, Row.Pilots,
            Row.Resolution, Row.Display, Row.VSync, Row.Fps, Row.Language, Row.Back,
        };
        static readonly float[] DifficultyStrength = { 0.88f, 1.16f, 1.33f };
        static readonly float[] DifficultyAiStrength = { 0.90f, 0.90f, 1.062f };
        static readonly float[] DifficultyAiAggression = { 0f, 0f, 1f };
        static readonly Color[] KartColors =
        {
            new Color(220, 50, 50), new Color(60, 110, 230), new Color(60, 180, 80), new Color(240, 200, 40),
            new Color(150, 80, 200), new Color(240, 130, 30), new Color(40, 190, 200), new Color(235, 110, 170),
            new Color(235, 235, 240), new Color(150, 160, 175),
        };

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr SDL_CreateRGBSurfaceFrom(IntPtr pixels, int width, int height, int depth, int pitch,
                                                      uint rmask, uint gmask, uint bmask, uint amask);

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void SDL_SetWindowIcon(IntPtr window, IntPtr icon);

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void SDL_FreeSurface(IntPtr surface);

        readonly GraphicsDeviceManager gfx;
        SpriteBatch sb;
        Texture2D pixel, disc, heart, menuKart;

        GameSettings cfg;
        readonly List<Point> resolutions = new List<Point>();
        readonly List<int> refreshRates = new List<int>();
        int resIdx;

        List<FaceInfo> faces;
        Texture2D[] botFaces;
        string faceWarning = "";
        bool hasPhotos;
        Track[] tracks;

        State state = State.Menu;
        int sel;
        bool inSettings;
        int settingsSel;
        Race race;
        bool newRecord;
        int countdownBeep;
        float penaltyBannerTimer;
        bool penaltyBannerWasActive;

        KeyboardState kb, pkb;
        GamePadState pad, ppad;
        float steerSmooth;
        float anim;
        float stickRepeatX, stickRepeatY;   // temporisation de répétition du stick dans le menu

        Vector2 camPos;
        float camAngle, camRot, zoom = 1f;
        Matrix camMatrix = Matrix.Identity;
        Kart camTarget;

        readonly Random rng = new Random();
        readonly Stopwatch fpsWatch = Stopwatch.StartNew();
        int fpsFrames, fps;

        int W => GraphicsDevice.Viewport.Width;
        int H => GraphicsDevice.Viewport.Height;
        /// <summary>Taille de texte proportionnelle à la hauteur de l'écran (base = 1080p).</summary>
        int T(float basePx) => Math.Max(1, (int)Math.Round(basePx * H / 1080f));

        public KartamaniaGame()
        {
            SetCurrentProcessExplicitAppUserModelID("Kartamania.Game");
            gfx = new GraphicsDeviceManager(this);
            // Plein écran "sans bordure" au lieu du mode exclusif : évite l'écran noir
            // et la fenêtre qui se minimise toute seule sur les écrans 4K / haute densité.
            gfx.HardwareModeSwitch = false;
            // La police vectorielle garde une texture d'atlas : on la réinitialise si le périphérique change.
            gfx.DeviceResetting += (s, e) => PixelFont.Reset();
            IsMouseVisible = false;
            Window.Title = "Kartamania";
            Exiting += (s, e) => { MusicPlayer.Stop(); Sfx.Stop(); cfg?.Save(); };
        }

        // ================================================================== initialisation

        protected override void BeginRun()
        {
            base.BeginRun();
            SetWindowIcon();
        }

        protected override void Initialize()
        {
            cfg = GameSettings.Load();
            Loc.Set(cfg.Language);
            BuildResolutionList();
            ApplyGraphics();
            base.Initialize();
        }

        void BuildResolutionList()
        {
            DisplayMode desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            int fallback = Math.Max(60, DisplayRefresh.Current());
            var rates = DisplayRefresh.AllModes();
            foreach (DisplayMode m in GraphicsAdapter.DefaultAdapter.SupportedDisplayModes)
            {
                if (m.Width < 1024 || m.Height < 576) continue;
                var p = new Point(m.Width, m.Height);
                if (!rates.ContainsKey(p)) rates[p] = fallback;
            }
            Point[] standard = { new Point(1280, 720), new Point(1600, 900), new Point(1920, 1080), new Point(2560, 1440), new Point(3840, 2160) };
            foreach (Point p in standard)
                if (p.X <= desktop.Width && p.Y <= desktop.Height && !rates.ContainsKey(p)) rates[p] = fallback;
            if (rates.Count == 0) rates[new Point(1280, 720)] = fallback;

            var modes = rates.ToList();
            modes.Sort((a, b) =>
            {
                Point x = a.Key, y = b.Key;
                int c = (x.X * x.Y).CompareTo(y.X * y.Y);
                return c != 0 ? c : x.X.CompareTo(y.X);
            });
            foreach (var mode in modes)
            {
                resolutions.Add(mode.Key);
                refreshRates.Add(Math.Max(60, mode.Value));
            }

            resIdx = resolutions.FindIndex(p => p.X == cfg.Width && p.Y == cfg.Height);
            if (resIdx < 0)
            {
                // Résolution sauvegardée introuvable : on prend la plus proche de 1080p.
                int best = int.MaxValue;
                for (int i = 0; i < resolutions.Count; i++)
                {
                    int d = Math.Abs(resolutions[i].X * resolutions[i].Y - 1920 * 1080);
                    if (d < best) { best = d; resIdx = i; }
                }
                cfg.Width = resolutions[resIdx].X;
                cfg.Height = resolutions[resIdx].Y;
            }
        }

        void ApplyGraphics()
        {
            bool borderless = cfg.DisplayMode == 1 && !cfg.Fullscreen;
            DisplayMode desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            int rw = cfg.Fullscreen || borderless ? desktop.Width : resolutions[resIdx].X;
            int rh = cfg.Fullscreen || borderless ? desktop.Height : resolutions[resIdx].Y;
            int workW, workH;
            bool hasWork = DisplayRefresh.TryGetWorkArea(out workW, out workH);

            if (!cfg.Fullscreen && !borderless && hasWork)
            {
                float scale = Math.Min((workW - 48f) / rw, (workH - 96f) / rh);
                scale = M.Clamp(scale, 0.25f, 1f);
                rw = Math.Max(640, (int)Math.Round(rw * scale));
                rh = Math.Max(360, (int)Math.Round(rh * scale));
            }

            gfx.PreferredBackBufferWidth = rw;
            gfx.PreferredBackBufferHeight = rh;
            gfx.IsFullScreen = cfg.Fullscreen;
            gfx.SynchronizeWithVerticalRetrace = cfg.VSync;
            gfx.ApplyChanges();

            if (!cfg.Fullscreen) Window.IsBorderless = borderless;
            if (borderless)
            {
                try { Window.Position = Point.Zero; } catch { }
            }
            else if (!cfg.Fullscreen && hasWork)
            {
                try
                {
                    Window.Position = new Point(Math.Max(0, (workW - rw) / 2), Math.Max(0, (workH - rh) / 2));
                }
                catch { }
            }

            int cap = GameSettings.FpsCaps[cfg.FpsIndex];
            if (cap == 0 && resIdx >= 0 && resIdx < refreshRates.Count) cap = refreshRates[resIdx];
            if (cap > 0)
            {
                IsFixedTimeStep = true;
                TargetElapsedTime = TimeSpan.FromSeconds(1.0 / cap);
            }
            else
            {
                IsFixedTimeStep = false;
            }
        }

        void CycleDisplay(int step)
        {
            cfg.DisplayMode = M.Wrap(cfg.DisplayMode + step, 3);
            ApplyGraphics();
        }

        protected override void LoadContent()
        {
            sb = new SpriteBatch(GraphicsDevice);
            SetWindowIcon();

            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            disc = MakeDisc(128);
            heart = MakeHeart(128);

            string kartPath = Path.Combine(AppContext.BaseDirectory, "Images", "kart.png");
            if (File.Exists(kartPath))
            {
                using (var fs = File.OpenRead(kartPath))
                    menuKart = Texture2D.FromStream(GraphicsDevice, fs);
            }

            faces = FaceLoader.LoadAll(GraphicsDevice, out faceWarning);
            hasPhotos = faces.Count > 0;
            if (!hasPhotos)
                faces.Add(new FaceInfo { Name = Loc.Get("PLAYER"), Texture = FaceLoader.MakePlaceholder(GraphicsDevice, KartColors[0]), IsPlaceholder = true });

            botFaces = new Texture2D[6];
            for (int i = 0; i < botFaces.Length; i++)
                botFaces[i] = FaceLoader.MakePlaceholder(GraphicsDevice, KartColors[(i + 1) % KartColors.Length]);

            MusicPlayer.Load();
            MusicPlayer.PlayMenu();
            Sfx.Load();

            tracks = TrackLibrary.All().Select(d => new Track(d)).ToArray();
            cfg.Track = M.Wrap(cfg.Track, tracks.Length);
            cfg.Player = M.Wrap(cfg.Player, faces.Count);
            cfg.KartColor = M.Wrap(cfg.KartColor, KartColors.Length);
            UpdatePlayerPlaceholder();
        }

        void SetWindowIcon()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Images", "kart.rgba");
            if (!File.Exists(path)) return;
            byte[] pixels = File.ReadAllBytes(path);
            const int size = 256;
            if (pixels.Length != size * size * 4) return;

            GCHandle pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                IntPtr surface = SDL_CreateRGBSurfaceFrom(pin.AddrOfPinnedObject(), size, size, 32, size * 4,
                                                          0x000000FFu, 0x0000FF00u, 0x00FF0000u, 0xFF000000u);
                if (surface != IntPtr.Zero)
                {
                    SDL_SetWindowIcon(Window.Handle, surface);
                    SDL_FreeSurface(surface);
                }
            }
            finally
            {
                pin.Free();
            }
        }

        Texture2D MakeDisc(int size)
        {
            var data = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float a = M.Clamp(r - (float)Math.Sqrt(dx * dx + dy * dy), 0f, 1f);
                    data[y * size + x] = Color.White * a;
                }
            var t = new Texture2D(GraphicsDevice, size, size);
            t.SetData(data);
            return t;
        }

        Texture2D MakeHeart(int size)
        {
            var data = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float px = (x + (sx + 0.5f) / 2f) / size;
                            float py = (y + (sy + 0.5f) / 2f) / size;
                            float nx = (px * 2f - 1f) * 1.2f;
                            float ny = 1.2f - py * 2.4f;
                            float v = nx * nx + ny * ny - 1f;
                            if (v * v * v - nx * nx * ny * ny * ny <= 0f) inside++;
                        }
                    }
                    data[y * size + x] = Color.White * (inside / 4f);
                }
            }
            var t = new Texture2D(GraphicsDevice, size, size);
            t.SetData(data);
            return t;
        }

        void UpdatePlayerPlaceholder()
        {
            if (hasPhotos) return;
            FaceInfo me = faces[cfg.Player];
            if (me == null || !me.IsPlaceholder) return;
            Texture2D old = me.Texture;
            me.Texture = FaceLoader.MakePlaceholder(GraphicsDevice, KartColors[cfg.KartColor]);
            if (old != null) old.Dispose();
        }

        // ================================================================== mise à jour

        bool Press(Keys k) => kb.IsKeyDown(k) && !pkb.IsKeyDown(k);
        bool PadPress(Buttons b) => pad.IsConnected && pad.IsButtonDown(b) && !ppad.IsButtonDown(b);

        protected override void Update(GameTime gameTime)
        {
            float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 0.05f);
            anim += dt;
            kb = Keyboard.GetState();
            pad = GamePad.GetState(PlayerIndex.One);

            bool alt = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
            if (Press(Keys.F11) || (alt && Press(Keys.Enter)))
            {
                cfg.Fullscreen = !cfg.Fullscreen;
                ApplyGraphics();
            }
            else
            {
                switch (state)
                {
                    case State.Menu: UpdateMenu(dt); break;
                    case State.Racing: UpdateRace(dt); break;
                    case State.Paused: UpdatePause(); break;
                    case State.Results: UpdateResults(); break;
                }
            }

            pkb = kb;
            ppad = pad;
            base.Update(gameTime);
        }

        /// <summary>Lecture du stick gauche comme un bouton : -1 / 0 / +1, avec délai puis répétition quand on maintient.</summary>
        int StickStep(float value, float prev, ref float repeatTimer, float dt)
        {
            const float threshold = 0.55f;
            int sign = value > threshold ? 1 : (value < -threshold ? -1 : 0);
            int prevSign = prev > threshold ? 1 : (prev < -threshold ? -1 : 0);

            if (sign == 0) { repeatTimer = 0f; return 0; }
            if (sign != prevSign) { repeatTimer = 0.40f; return sign; }   // nouveau mouvement : réaction immédiate
            repeatTimer -= dt;                                            // maintenu : on répète
            if (repeatTimer <= 0f) { repeatTimer = 0.11f; return sign; }
            return 0;
        }

        int StickEdge(float value, float prev)
        {
            const float threshold = 0.55f;
            int sign = value > threshold ? 1 : (value < -threshold ? -1 : 0);
            int prevSign = prev > threshold ? 1 : (prev < -threshold ? -1 : 0);
            return sign != 0 && sign != prevSign ? sign : 0;
        }

        void UpdateMenu(float dt)
        {
            int stickY = StickStep(pad.ThumbSticks.Left.Y, ppad.ThumbSticks.Left.Y, ref stickRepeatY, dt);
            int stickX = StickStep(pad.ThumbSticks.Left.X, ppad.ThumbSticks.Left.X, ref stickRepeatX, dt);
            int stickXEdge = StickEdge(pad.ThumbSticks.Left.X, ppad.ThumbSticks.Left.X);

            bool back = Press(Keys.Escape) || PadPress(Buttons.B);
            if (back)
            {
                cfg.Save();
                if (inSettings) inSettings = false;
                else Exit();
                return;
            }

            Row[] rows = inSettings ? SettingsRows : MainRows;
            ref int selection = ref (inSettings ? ref settingsSel : ref sel);

            if (Press(Keys.Up) || PadPress(Buttons.DPadUp) || stickY > 0) selection = M.Wrap(selection - 1, rows.Length);
            if (Press(Keys.Down) || PadPress(Buttons.DPadDown) || stickY < 0) selection = M.Wrap(selection + 1, rows.Length);

            int dir = 0;
            if (Press(Keys.Left) || PadPress(Buttons.DPadLeft) || stickX < 0) dir = -1;
            if (Press(Keys.Right) || PadPress(Buttons.DPadRight) || stickX > 0) dir = 1;
            int dirEdge = 0;
            if (Press(Keys.Left) || PadPress(Buttons.DPadLeft) || stickXEdge < 0) dirEdge = -1;
            if (Press(Keys.Right) || PadPress(Buttons.DPadRight) || stickXEdge > 0) dirEdge = 1;
            bool enter = Press(Keys.Enter) || Press(Keys.Space) || PadPress(Buttons.A);

            switch (rows[selection])
            {
                case Row.Play: if (enter) StartRace(); break;
                case Row.Settings:
                    if (enter) { inSettings = true; settingsSel = 0; }
                    break;
                case Row.Back:
                    if (enter) { inSettings = false; cfg.Save(); }
                    break;
                case Row.Track:
                    if (dir != 0) cfg.Track = M.Wrap(cfg.Track + dir, tracks.Length);
                    break;
                case Row.Player:
                    if (dir != 0) cfg.Player = M.Wrap(cfg.Player + dir, faces.Count);
                    break;
                case Row.KartColor:
                    if (dir != 0)
                    {
                        cfg.KartColor = M.Wrap(cfg.KartColor + dir, KartColors.Length);
                        UpdatePlayerPlaceholder();
                    }
                    break;
                case Row.Difficulty:
                    if (dir != 0) cfg.Difficulty = M.Wrap(cfg.Difficulty + dir, DifficultyStrength.Length);
                    break;
                case Row.Laps:
                    if (dir != 0) cfg.Laps = (int)M.Clamp(cfg.Laps + dir, 1, 20);
                    break;
                case Row.Pilots:
                    if (dir != 0) cfg.RacerCount = (int)M.Clamp(cfg.RacerCount + dir, 1, 10);
                    break;
                case Row.Resolution:
                    if (dir != 0)
                    {
                        resIdx = M.Wrap(resIdx + dir, resolutions.Count);
                        cfg.Width = resolutions[resIdx].X;
                        cfg.Height = resolutions[resIdx].Y;
                        ApplyGraphics();
                    }
                    break;
                case Row.Display:
                    if (dirEdge != 0 || enter) CycleDisplay(dirEdge < 0 ? -1 : 1);
                    break;
                case Row.VSync:
                    if (dirEdge != 0 || enter) { cfg.VSync = !cfg.VSync; ApplyGraphics(); }
                    break;
                case Row.Fps:
                    if (dir != 0)
                    {
                        cfg.FpsIndex = M.Wrap(cfg.FpsIndex + dir, GameSettings.FpsCaps.Length);
                        ApplyGraphics();
                    }
                    break;
                case Row.Language:
                    if (dirEdge != 0 || enter)
                    {
                        cfg.Language = M.Wrap(cfg.Language + 1, 2);
                        Loc.Set(cfg.Language);
                        cfg.Save();
                    }
                    break;
                case Row.Quit:
                    if (enter) { cfg.Save(); Exit(); }
                    break;
            }

            if (PadPress(Buttons.Y)) CycleDisplay(1);
        }

        void StartRace()
        {
            cfg.Save();
            var racers = new List<RacerInfo>();
            FaceInfo me = faces[cfg.Player];
            int playerColor = M.Wrap(cfg.KartColor, KartColors.Length);
            racers.Add(new RacerInfo { Name = me.IsPlaceholder ? Loc.Get("PLAYER") : me.Name, Face = me.Texture, Color = KartColors[playerColor], IsPlayer = true });

            int colorIdx = 0;
            Color NextColor()
            {
                int offset = 1 + colorIdx++ % (KartColors.Length - 1);
                return KartColors[(playerColor + offset) % KartColors.Length];
            }

            var others = faces.Where(f => f != me && !f.IsPlaceholder).OrderBy(f => rng.Next()).Take(cfg.RacerCount - 1).ToList();
            foreach (FaceInfo f in others)
                racers.Add(new RacerInfo { Name = f.Name, Face = f.Texture, Color = NextColor() });

            int bot = 0;
            while (racers.Count < cfg.RacerCount)
            {
                racers.Add(new RacerInfo
                {
                    Name = Loc.Get("ROBOT") + " " + (bot + 1),
                    Face = botFaces[bot % botFaces.Length],
                    Color = NextColor(),
                });
                bot++;
            }

            race = new Race(tracks[cfg.Track], racers, cfg.Laps, DifficultyStrength[cfg.Difficulty], DifficultyAiStrength[cfg.Difficulty], DifficultyAiAggression[cfg.Difficulty], rng);
            camTarget = null;
            camAngle = race.Player.Angle;
            camPos = race.Player.Pos + race.Player.Heading * 260f;
            steerSmooth = 0f;
            newRecord = false;
            countdownBeep = 4;
            penaltyBannerTimer = 0f;
            penaltyBannerWasActive = false;
            Sfx.StopEngine();
            Sfx.StartEngine();
            MusicPlayer.PlayRandom();
            state = State.Racing;
        }

        PlayerInput ReadInput(float dt)
        {
            float thr = 0f, st = 0f;
            if (kb.IsKeyDown(Keys.Up) || kb.IsKeyDown(Keys.W)) thr += 1f;
            if (kb.IsKeyDown(Keys.Down) || kb.IsKeyDown(Keys.S)) thr -= 1f;
            if (kb.IsKeyDown(Keys.Left) || kb.IsKeyDown(Keys.A)) st -= 1f;
            if (kb.IsKeyDown(Keys.Right) || kb.IsKeyDown(Keys.D)) st += 1f;
            if (pad.IsConnected)
            {
                thr += pad.Triggers.Right - pad.Triggers.Left;
                if (pad.IsButtonDown(Buttons.A)) thr += 1f;
                if (pad.IsButtonDown(Buttons.B)) thr -= 1f;
                st += pad.ThumbSticks.Left.X;
            }

            // Direction progressive au clavier (plus agréable qu'un tout-ou-rien).
            float diff = st - steerSmooth;
            float step = 7f * dt;
            steerSmooth += M.Clamp(diff, -step, step);

            return new PlayerInput
            {
                Throttle = M.Clamp(thr, -1f, 1f),
                Steer = M.Clamp(steerSmooth, -1f, 1f),
                UseItem = Press(Keys.Space) || Press(Keys.LeftControl) || Press(Keys.Enter)
                          || PadPress(Buttons.X) || PadPress(Buttons.RightShoulder) || PadPress(Buttons.Y),
            };
        }

        Kart ViewKart()
        {
            if (camTarget != null && !camTarget.Eliminated) return camTarget;
            return race.Player;
        }

        void CycleView(int dir)
        {
            var karts = new List<Kart>();
            foreach (Kart k in race.Ranking)
                if (!k.Eliminated) karts.Add(k);
            if (karts.Count <= 1) return;
            int idx = karts.IndexOf(ViewKart());
            camTarget = karts[M.Wrap(idx + dir, karts.Count)];
        }

        void UpdateRace(float dt)
        {
            if (Press(Keys.Escape) || PadPress(Buttons.Start) || !IsActive)
            {
                MusicPlayer.Pause();
                Sfx.PauseEngine();
                state = State.Paused;
                return;
            }

            race.Update(dt, ReadInput(dt));

            bool penalized = race.Player != null && race.Player.OutsideCourse;
            if (penalized && !penaltyBannerWasActive) penaltyBannerTimer = Race.PenaltyMessageDuration;
            penaltyBannerWasActive = penalized;
            if (race.ShortcutPenaltyTimer > penaltyBannerTimer) penaltyBannerTimer = race.ShortcutPenaltyTimer;
            if (penaltyBannerTimer > 0f) penaltyBannerTimer -= dt;

            int count = (int)Math.Ceiling(race.Countdown);
            if (race.Countdown > 0f && count < countdownBeep && count >= 1)
            {
                countdownBeep = count;
                Sfx.Countdown(count);
            }
            else if (countdownBeep > 0 && race.Countdown <= 0f)
            {
                countdownBeep = 0;
                Sfx.Go();
            }
            Sfx.UpdateEngine(race.Player.Vel.Length(), race.Player.BoostTimer > 0f);

            Kart p = race.Player;
            bool waitingView = !race.Over && (p.Finished || p.Eliminated);
            if (waitingView && Press(Keys.Tab))
                CycleView(kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift) ? -1 : 1);
            if (waitingView && PadPress(Buttons.DPadRight)) CycleView(1);
            if (waitingView && PadPress(Buttons.DPadLeft)) CycleView(-1);

            Kart view = ViewKart();
            Vector2 target = view.Pos + view.Heading * 260f;
            camPos = Vector2.Lerp(camPos, target, 1f - (float)Math.Exp(-9f * dt));
            camAngle += M.WrapAngle(view.Angle - camAngle) * (1f - (float)Math.Exp(-5f * dt));

            if (waitingView)
            {
                bool waiting = false;
                foreach (Kart k in race.Karts)
                    if (!k.Finished && !k.Eliminated) { waiting = true; break; }
                float doneAt = p.Finished ? p.FinishTime : p.ElimTime;
                if (waiting && race.Time - doneAt > 0.5f && (Press(Keys.Enter) || PadPress(Buttons.A)))
                    race.Over = true;
            }

            if (race.Over)
            {
                newRecord = false;
                if (p.Finished && !p.Eliminated && p.Lap >= race.TotalLaps)
                    newRecord = cfg.RecordTime(race.Track.Def.Name, race.TotalLaps, cfg.Difficulty, p.FinishTime);
                Sfx.Finish(!p.Eliminated && p.Place == 1);
                Sfx.StopEngine();
                MusicPlayer.Stop();
                state = State.Results;
            }
        }

        void UpdatePause()
        {
            if (Press(Keys.Escape) || PadPress(Buttons.Start) || Press(Keys.Enter))
            {
                MusicPlayer.Resume();
                Sfx.ResumeEngine();
                state = State.Racing;
            }
            if (Press(Keys.M) || PadPress(Buttons.B))
            {
                Sfx.StopEngine();
                MusicPlayer.PlayMenu();
                state = State.Menu;
            }
            if (Press(Keys.R) || PadPress(Buttons.Y)) StartRace();
        }

        void UpdateResults()
        {
            if (Press(Keys.Enter) || PadPress(Buttons.A) || Press(Keys.Escape))
            {
                MusicPlayer.PlayMenu();
                state = State.Menu;
            }
            if (Press(Keys.R) || PadPress(Buttons.Y)) StartRace();
        }

        // ================================================================== dessin : helpers

        void Rect(float x, float y, float w, float h, Color c) =>
            sb.Draw(pixel, new Rectangle((int)x, (int)y, (int)w, (int)h), c);

        void RectC(Vector2 center, Vector2 size, float rot, Color c) =>
            sb.Draw(pixel, center, null, c, rot, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);

        void Disc(Vector2 center, float diameter, Color c) =>
            sb.Draw(disc, center, null, c, 0f, new Vector2(64f, 64f), diameter / 128f, SpriteEffects.None, 0f);

        void Heart(Vector2 center, float diameter, Color c) =>
            sb.Draw(heart, center, null, c, 0f, new Vector2(64f, 64f), diameter / 128f, SpriteEffects.None, 0f);

        void Ellipse(Vector2 center, Vector2 size, float rot, Color c) =>
            sb.Draw(disc, center, null, c, rot, new Vector2(64f, 64f), new Vector2(size.X / 128f, size.Y / 128f), SpriteEffects.None, 0f);

        void DrawTree(Vector2 p, float scale, TrackTheme theme, Vector2 up, Vector2 right, float rot, int variant)
        {
            Vector2 O(float x, float y) => p + right * (x * scale) + up * (y * scale);
            Color bark = theme == TrackTheme.Desert ? new Color(150, 112, 74) : new Color(95, 65, 42);
            Color barkDark = theme == TrackTheme.Desert ? new Color(105, 76, 50) : new Color(70, 46, 30);
            Color barkLight = theme == TrackTheme.Desert ? new Color(185, 145, 100) : new Color(120, 84, 55);
            int shape = Math.Abs(variant) % (theme == TrackTheme.Desert ? 6 : 4);

            if (theme == TrackTheme.Desert)
            {
                Color cactus = new Color(58, 122, 62);
                Color cactusLight = new Color(92, 158, 80);
                Color cactusDark = new Color(34, 88, 46);
                Color spine = new Color(226, 214, 164);
                Color flower = new Color(236, 106, 118);

                void Column(float cx, float cy, float cw, float ch)
                {
                    Ellipse(O(cx, cy), new Vector2(cw, ch) * scale, rot, cactus);
                    RectC(O(cx, cy - ch * 0.28f), new Vector2(cw, ch * 0.72f) * scale, rot, cactus);
                    Disc(O(cx, cy + ch * 0.5f), cw * 0.5f * scale, cactusLight);
                    for (int rib = -1; rib <= 1; rib++)
                        RectC(O(cx + rib * cw * 0.24f, cy), new Vector2(2.5f, ch * 0.86f) * scale, rot,
                              rib == 0 ? cactusLight : cactusDark);
                    for (int s = 0; s < 6; s++)
                    {
                        float yy = cy - ch * 0.34f + s * ch * 0.14f;
                        Disc(O(cx - cw * 0.42f, yy), 2.2f * scale, spine);
                        Disc(O(cx + cw * 0.42f, yy + 3f), 2.2f * scale, spine);
                    }
                }

                void Arm(float sx, float sy, float dir, float len)
                {
                    RectC(O(sx + dir * len * 0.5f, sy), new Vector2(len, 10f) * scale, rot, cactus);
                    RectC(O(sx + dir * len * 0.5f, sy + 3f), new Vector2(len * 0.9f, 2.5f) * scale, rot, cactusLight);
                    RectC(O(sx + dir * len, sy + len * 0.34f), new Vector2(11f, len * 0.75f) * scale, rot, cactus);
                    Disc(O(sx + dir * len, sy + len * 0.7f), 5.5f * scale, cactusLight);
                    for (int s = 0; s < 3; s++)
                        Disc(O(sx + dir * (len * (0.35f + s * 0.25f)), sy + 7f), 2f * scale, spine);
                }

                void Twig(float tx, float ty, float angle, float len, Color c)
                {
                    RectC(O(tx + (float)Math.Cos(angle) * len * 0.5f,
                            ty + (float)Math.Sin(angle) * len * 0.5f),
                          new Vector2(len, 4f) * scale, rot + angle, c);
                }

                Ellipse(O(0f, -22f), new Vector2(70f, 20f) * scale, rot, Color.Black * 0.16f);

                if (shape == 0)
                {
                    Column(0f, 2f, 18f, 62f);
                    Arm(-9f, 10f, -1f, 30f);
                    Arm(9f, 18f, 1f, 26f);
                    Disc(O(0f, 35f), 5f * scale, flower);
                }
                else if (shape == 1)
                {
                    Column(5f, -2f, 17f, 58f);
                    Column(-11f, -12f, 11f, 32f);
                    Arm(11f, 12f, 1f, 24f);
                    Disc(O(5f, 30f), 4f * scale, flower);
                }
                else if (shape == 2)
                {
                    Column(-13f, -8f, 12f, 38f);
                    Column(12f, -6f, 14f, 44f);
                    Column(0f, 2f, 13f, 54f);
                    Disc(O(0f, 32f), 4f * scale, flower);
                    Disc(O(-13f, 14f), 3f * scale, flower);
                }
                else if (shape == 3)
                {
                    Ellipse(O(0f, 4f), new Vector2(52f, 58f) * scale, rot, cactus);
                    Ellipse(O(0f, 12f), new Vector2(34f, 38f) * scale, rot, cactusLight);
                    for (int rib = -2; rib <= 2; rib++)
                        Ellipse(O(rib * 7f, 10f), new Vector2(3f, 34f) * scale, rot, cactusDark);
                    for (int s = 0; s < 12; s++)
                    {
                        float a = s * M.Pi / 6f;
                        Disc(O((float)Math.Cos(a) * 25f, 12f + (float)Math.Sin(a) * 28f), 2.5f * scale, spine);
                    }
                    Disc(O(0f, 36f), 6f * scale, flower);
                }
                else
                {
                    Color brush = new Color(156, 126, 82);
                    Color brushMid = new Color(132, 104, 64);
                    Color brushDark = new Color(98, 74, 44);
                    Color brushLight = new Color(198, 168, 116);
                    if (shape == 4)
                    {
                        Disc(O(0f, 8f), 20f * scale, brushDark);
                        for (int s = 0; s < 26; s++)
                        {
                            float a = s * 2.399963f;
                            float r = 5f + (s % 7) * 4.4f;
                            float px = (float)Math.Cos(a) * r * 1.15f;
                            float py = 12f + (float)Math.Sin(a) * r * 0.8f;
                            float rr = 8f + (s % 5) * 2.4f;
                            Disc(O(px, py), rr * scale, s % 3 == 0 ? brushDark : (s % 3 == 1 ? brush : brushLight));
                        }
                        for (int s = 0; s < 10; s++)
                        {
                            float a = s * M.Pi / 5f + 0.2f;
                            float len = 28f + (s % 3) * 6f;
                            Twig(0f, 10f, a, len, brushMid);
                            Disc(O((float)Math.Cos(a) * len, 10f + (float)Math.Sin(a) * len), 3f * scale, brushLight);
                        }
                    }
                    else
                    {
                        RectC(O(0f, -8f), new Vector2(9f, 34f) * scale, rot, brushDark);
                        for (int s = 0; s < 22; s++)
                        {
                            float a = s * 2.399963f;
                            float t = (s % 7) / 6f;
                            float px = (float)Math.Cos(a) * (8f + t * 20f);
                            float py = 2f + t * 44f + (float)Math.Sin(a) * 7f;
                            float rw = 9f + (s % 4) * 2.6f;
                            float rh = 7f + (s % 3) * 2.2f;
                            Ellipse(O(px, py), new Vector2(rw, rh) * scale, rot + a * 0.15f,
                                    s % 4 == 0 ? brushDark : (s % 4 == 1 ? brushLight : brush));
                        }
                        for (int s = 0; s < 8; s++)
                        {
                            float a = -1.05f + s * 0.3f;
                            Twig(0f, 4f + (s % 3) * 12f, a, 26f + (s % 4) * 5f, brushMid);
                        }
                        for (int s = 0; s < 12; s++)
                        {
                            float a = s * 2.399963f + 0.7f;
                            float r = 18f + (s % 5) * 5f;
                            Disc(O((float)Math.Cos(a) * r, 28f + (float)Math.Sin(a) * r * 0.7f),
                                 2.8f * scale, brushLight);
                        }
                    }
                }
                return;
            }
            int tint = Math.Abs(variant * 13) % 19;
            Color leafA = new Color(30 + tint, 90 + tint, 45);
            Color leafB = new Color(45 + tint, 115 + tint, 55);
            Color leafC = new Color(58 + tint, 138 + tint, 64);

            RectC(O(0f, -16f), new Vector2(12f, 42f) * scale, rot, barkDark);
            RectC(O(1f, -16f), new Vector2(4f, 40f) * scale, rot, bark);

            if (shape == 0)
            {
                Disc(O(0f, 20f), 72f * scale, leafA);
                Disc(O(-20f, 4f), 54f * scale, leafB);
                Disc(O(20f, 2f), 52f * scale, leafC);
                Disc(O(-6f, 30f), 46f * scale, leafB);
            }
            else if (shape == 1)
            {
                Ellipse(O(0f, 34f), new Vector2(88f, 120f) * scale, rot, leafA);
                Ellipse(O(0f, 78f), new Vector2(62f, 104f) * scale, rot, leafB);
                Ellipse(O(0f, 114f), new Vector2(34f, 72f) * scale, rot, leafC);
            }
            else if (shape == 2)
            {
                Disc(O(-18f, 22f), 56f * scale, leafA);
                Disc(O(20f, 26f), 54f * scale, leafB);
                Disc(O(0f, 44f), 62f * scale, leafC);
                Disc(O(-4f, 60f), 36f * scale, leafA);
            }
            else
            {
                Disc(O(-26f, 12f), 44f * scale, leafA);
                Disc(O(24f, 16f), 46f * scale, leafB);
                Disc(O(0f, 34f), 50f * scale, leafC);
                Disc(O(-8f, 48f), 32f * scale, leafB);
            }

            if (theme == TrackTheme.Winter)
            {
                Color snow = new Color(240, 248, 255);
                Disc(O(0f, 44f + shape * 12f), 54f * scale, snow);
                Disc(O(-24f, 22f), 36f * scale, snow);
                Disc(O(22f, 20f), 34f * scale, snow);
                Disc(O(-6f, 50f + shape * 12f), 28f * scale, Color.White);
            }
        }

        void DrawChalet(Vector2 p, float scale, Vector2 up, Vector2 right, float rot, int seed, bool snow = false)
        {
            Vector2 O(float x, float y) => p + right * (x * scale) + up * (y * scale);
            int variant = Math.Abs(seed) % 3;
            Color wood = variant == 0 ? new Color(126, 84, 50) : (variant == 1 ? new Color(142, 96, 58) : new Color(116, 76, 46));
            Color woodDark = new Color(82, 52, 30);
            Color woodLight = new Color(168, 116, 72);
            Color roof = new Color(74, 46, 32);
            Color roofLight = new Color(104, 64, 42);
            float w = 150f;
            float ht = 110f;

            RectC(O(0f, ht * 0.5f), new Vector2(w, ht) * scale, rot, wood);
            for (int row = 0; row < 5; row++)
                RectC(O(0f, row * ht / 5f), new Vector2(w + 6f, 7f) * scale, rot, row % 2 == 0 ? woodDark : woodLight);

            RectC(O(-w * 0.28f, ht + 34f), new Vector2(w * 0.72f, 16f) * scale, rot - 0.62f, roof);
            RectC(O(w * 0.28f, ht + 34f), new Vector2(w * 0.72f, 16f) * scale, rot + 0.62f, roofLight);
            RectC(O(0f, ht + 62f), new Vector2(20f, 18f) * scale, rot, roof);
            RectC(O(w * 0.22f, ht + 70f), new Vector2(16f, 54f) * scale, rot, woodDark);

            RectC(O(0f, ht * 0.18f), new Vector2(34f, 58f) * scale, rot, woodDark);
            Disc(O(11f, ht * 0.18f), 4f * scale, Color.Gold);

            RectC(O(-w * 0.28f, ht * 0.62f), new Vector2(30f, 28f) * scale, rot, new Color(255, 214, 130));
            RectC(O(w * 0.28f, ht * 0.62f), new Vector2(30f, 28f) * scale, rot, new Color(255, 214, 130));
            RectC(O(-w * 0.28f, ht * 0.62f), new Vector2(34f, 4f) * scale, rot, woodDark);
            RectC(O(w * 0.28f, ht * 0.62f), new Vector2(34f, 4f) * scale, rot, woodDark);

            if (snow)
            {
                Color snowWhite = new Color(240, 248, 255);
                RectC(O(-w * 0.28f, ht + 43f), new Vector2(w * 0.74f, 8f) * scale, rot - 0.62f, snowWhite);
                RectC(O(w * 0.28f, ht + 43f), new Vector2(w * 0.74f, 8f) * scale, rot + 0.62f, snowWhite);
                RectC(O(0f, ht + 74f), new Vector2(26f, 8f) * scale, rot, snowWhite);
                RectC(O(w * 0.22f, ht + 100f), new Vector2(20f, 6f) * scale, rot, snowWhite);
                Disc(O(-w * 0.42f, ht + 8f), 12f * scale, snowWhite);
                Disc(O(w * 0.42f, ht + 12f), 10f * scale, snowWhite);
                Disc(O(-w * 0.28f, ht * 0.62f + 20f), 13f * scale, snowWhite);
                Disc(O(w * 0.28f, ht * 0.62f + 20f), 13f * scale, snowWhite);
            }
        }

        void DrawSnowBank(Vector2 p, float size, int seed, Vector2 up, Vector2 right, float rot)
        {
            float H(int s, int m) => (Math.Abs(s * 9301 + 49297) % m) / (float)m;
            Vector2 O(float x, float y) => p + right * x + up * y;
            int shape = Math.Abs(seed) % 3;
            float lean = (H(seed, 7) - 3f) * 0.16f;
            float wide = shape == 2 ? 2.05f : (shape == 1 ? 1.15f : 1.55f);
            float tall = shape == 1 ? 0.78f : (shape == 2 ? 0.42f : 0.60f);

            Ellipse(O(0f, size * 0.18f), new Vector2(size * (wide + 0.15f), size * (tall + 0.06f)), rot + lean, new Color(152, 178, 198) * 0.55f);
            Ellipse(O(0f, 0f), new Vector2(size * wide, size * tall), rot + lean, new Color(224, 237, 247));

            if (shape == 0)
            {
                Ellipse(O(-size * 0.42f, -size * 0.14f), new Vector2(size * 0.95f, size * 0.48f), rot + lean - 0.22f, new Color(238, 246, 252));
                Ellipse(O(size * 0.38f, -size * 0.18f), new Vector2(size * 1.05f, size * 0.46f), rot + lean + 0.20f, new Color(246, 251, 255));
                Ellipse(O(-size * 0.08f, -size * 0.30f), new Vector2(size * 0.86f, size * 0.34f), rot + lean, Color.White);
            }
            else if (shape == 1)
            {
                Ellipse(O(-size * 0.22f, -size * 0.28f), new Vector2(size * 0.66f, size * 0.62f), rot + lean - 0.18f, new Color(238, 246, 252));
                Ellipse(O(size * 0.26f, -size * 0.34f), new Vector2(size * 0.72f, size * 0.58f), rot + lean + 0.16f, new Color(246, 251, 255));
                Ellipse(O(0f, -size * 0.48f), new Vector2(size * 0.52f, size * 0.34f), rot + lean, Color.White);
            }
            else
            {
                Ellipse(O(-size * 0.62f, -size * 0.10f), new Vector2(size * 0.92f, size * 0.34f), rot + lean - 0.12f, new Color(238, 246, 252));
                Ellipse(O(size * 0.58f, -size * 0.12f), new Vector2(size * 0.96f, size * 0.32f), rot + lean + 0.14f, new Color(246, 251, 255));
                Ellipse(O(0f, -size * 0.22f), new Vector2(size * 1.22f, size * 0.24f), rot + lean, Color.White);
            }

            Ellipse(O(size * 0.10f, size * 0.10f), new Vector2(size * (wide * 0.55f), size * 0.08f), rot + lean, new Color(190, 212, 230) * 0.85f);
            Disc(O(-size * 0.48f, -size * 0.30f), size * 0.11f, Color.White);
            Disc(O(size * 0.30f, -size * 0.36f), size * 0.08f, Color.White);
            Disc(O(size * 0.62f, -size * 0.18f), size * 0.06f, Color.White * 0.9f);
        }

        void DrawBuilding(Vector2 p, float scale, int seed, Vector2 up, Vector2 right, float rot)
        {
            Vector2 O(float x, float y) => p + right * (x * scale) + up * (y * scale);
            int variant = Math.Abs(seed) % 5;
            float height = 160f + variant * 32f;
            float width = 92f + (variant % 3) * 16f;
            Color wall = variant % 2 == 0 ? new Color(96, 104, 118) : new Color(122, 118, 108);
            Color roof = variant % 3 == 0 ? new Color(62, 70, 82) : new Color(78, 74, 68);

            RectC(O(0f, height * 0.5f), new Vector2(width + 12f, 12f) * scale, rot, roof);
            RectC(O(0f, 0f), new Vector2(width, height) * scale, rot, wall);
            for (int row = 0; row < 3; row++)
                for (int col = -1; col <= 1; col++)
                {
                    Color glass = ((seed + row * 3 + col + 9) % 4 == 0)
                        ? new Color(255, 216, 128)
                        : new Color(44, 58, 72);
                    RectC(O(col * width * 0.27f, -height * 0.28f + row * height * 0.27f),
                          new Vector2(width * 0.16f, height * 0.13f) * scale, rot, glass);
                }
        }

        void DrawLamp(Vector2 p, float scale, Vector2 up, Vector2 right, float rot, bool flip = false)
        {
            Vector2 arm = flip ? -right : right;
            Color poleColor = new Color(40, 44, 50);
            RectC(p + up * (34f * scale), new Vector2(6f, 68f) * scale, rot, poleColor);
            RectC(p + up * (66f * scale) + arm * (13f * scale), new Vector2(26f, 5f) * scale, rot, poleColor);
            Disc(p + up * (62f * scale) + arm * (24f * scale), 18f * scale, new Color(255, 224, 150));
        }

        void Face(Texture2D tex, Vector2 center, float diameter, float rotation, Color tint) =>
            sb.Draw(tex, center, null, tint, rotation, new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    diameter / tex.Width, SpriteEffects.None, 0f);

        void Text(string s, float x, float y, int scale, Color c, bool center = false) =>
            PixelFont.Draw(sb, pixel, s, (int)x, (int)y, scale, c, center);

        /// <summary>Panneau sombre derrière des commandes pour qu'elles restent bien lisibles.</summary>
        void HintPanel(string[] lines, float centerX, float bottomY, int scale, Color color)
        {
            if (lines == null || lines.Length == 0) return;
            float padX = T(16), padY = T(12), gap = T(8);
            int maxW = 0;
            foreach (string l in lines) maxW = Math.Max(maxW, PixelFont.Measure(l, scale));
            float lineH = scale * 5f + gap;
            float bw = maxW + padX * 2f;
            float bh = lines.Length * scale * 5f + (lines.Length - 1) * gap + padY * 2f;
            float bx = centerX - bw * 0.5f;
            float by = bottomY - bh;
            Rect(bx, by, bw, bh, Color.Black * 0.6f);
            Rect(bx, by, bw, Math.Max(1f, T(1)), color * 0.5f);
            for (int i = 0; i < lines.Length; i++)
                Text(lines[i], centerX, by + padY + i * lineH, scale, color, true);
        }

        // Commandes affichées : boutons d'une manette Xbox si une manette est branchée, sinon clavier.
        string[] MenuHint() => pad.IsConnected
            ? new[] { Loc.Get("HINT_MENU_PAD_1"), Loc.Get(inSettings ? "HINT_MENU_BACK_PAD" : "HINT_MENU_PAD_2") }
            : new[] { Loc.Get("HINT_MENU_KB_1"), Loc.Get(inSettings ? "HINT_MENU_BACK_KB" : "HINT_MENU_KB_2") };

        string RaceHint() => pad.IsConnected ? Loc.Get("HINT_RACE_PAD") : Loc.Get("HINT_RACE_KB");

        string[] PauseHint() => pad.IsConnected
            ? new[] { Loc.Get("HINT_PAUSE_PAD_1"), Loc.Get("HINT_PAUSE_PAD_2"), Loc.Get("HINT_PAUSE_PAD_3") }
            : new[] { Loc.Get("HINT_PAUSE_KB_1"), Loc.Get("HINT_PAUSE_KB_2"), Loc.Get("HINT_PAUSE_KB_3") };

        string ResultsHint() => pad.IsConnected ? Loc.Get("HINT_RESULTS_PAD") : Loc.Get("HINT_RESULTS_KB");

        static string FormatTime(float t) =>
            string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00.0}", (int)(t / 60f), t % 60f);

        Vector2 MapPoint(Track t, Rectangle r, Vector2 p)
        {
            Vector2 span = t.Max - t.Min;
            float s = Math.Min(r.Width / span.X, r.Height / span.Y);
            Vector2 off = new Vector2(r.X + (r.Width - span.X * s) * 0.5f, r.Y + (r.Height - span.Y * s) * 0.5f);
            return off + (p - t.Min) * s;
        }

        void DrawMiniTrack(Track t, Rectangle r, float dot)
        {
            for (int i = 0; i < t.N; i += 2)
                Disc(MapPoint(t, r, t.Center[i]), dot, Color.White * 0.75f);
            Disc(MapPoint(t, r, t.Center[t.StartIndex]), dot * 2.2f, Color.Red);   // ligne de départ
        }

        void DrawSnowfall(int w, int h)
        {
            float H(int s, int m) => (Math.Abs(s * 9301 + 49297) % m) / (float)m;
            for (int i = 0; i < 1000; i++)
            {
                float speed = 130f + H(i + 13, 211) * 130f;
                float drift = 8f + H(i + 7, 97) * 28f;
                float phase = i * 1.7f + H(i + 29, 1000) * 6.28f;
                float sway = (float)Math.Sin(anim * (0.5f + H(i + 3, 7) * 0.5f) + phase) * 10f;
                float sx = (H(i * 3 + 11, 1000) * w + anim * drift + sway + w) % w;
                float sy = (H(i * 7 + 97, 1000) * (h + 240f) + anim * speed) % (h + 240f) - 120f;
                float size = 3f + H(i + 31, 5) * 6f;
                Disc(new Vector2(sx, sy), size, Color.White * (0.40f + H(i + 53, 5) * 0.40f));
            }
        }

        // ================================================================== dessin

        protected override void Draw(GameTime gameTime)
        {
            fpsFrames++;
            if (fpsWatch.ElapsedMilliseconds >= 500)
            {
                fps = (int)Math.Round(fpsFrames * 1000.0 / fpsWatch.ElapsedMilliseconds);
                fpsFrames = 0;
                fpsWatch.Restart();
                Window.Title = "Kartamania - " + fps + " FPS";
            }

            if (state == State.Menu || race == null)
            {
                DrawMenu();
            }
            else
            {
                DrawWorld();
                sb.Begin(samplerState: SamplerState.LinearClamp);
                if (race.Track.Def.Name == "Étoile du Nord") DrawSnowfall(W, H);
                DrawHud();
                if (state == State.Racing) DrawPenalty();
                if (state == State.Racing) DrawFinishBanner();
                if (state == State.Paused) DrawPause();
                if (state == State.Results) DrawResults();
                sb.End();
            }

            base.Draw(gameTime);
        }

        // ------------------------------------------------------------------ menu

        string RowText(Row r)
        {
            switch (r)
            {
                case Row.Play: return Loc.Get("ROW_PLAY");
                case Row.Settings: return Loc.Get("ROW_SETTINGS");
                case Row.Back: return Loc.Get("ROW_BACK");
                case Row.Track: return Loc.Get("LABEL_TRACK") + ": < " + Loc.TrackName(tracks[cfg.Track].Def.Name) + " >";
                case Row.Player: return Loc.Get("LABEL_PLAYER") + ": < " + (faces[cfg.Player].IsPlaceholder ? Loc.Get("PLAYER") : faces[cfg.Player].Name) + " >";
                case Row.KartColor: return Loc.Get("LABEL_KART_COLOR") + ": < " + Loc.Get("COLOR_" + cfg.KartColor) + " >";
                case Row.Difficulty: return Loc.Get("LABEL_DIFFICULTY") + ": < " + Loc.Get("DIFFICULTY_" + cfg.Difficulty) + " >";
                case Row.Laps: return Loc.Get("LABEL_LAPS") + ": < " + cfg.Laps + " >";
                case Row.Pilots: return Loc.Get("LABEL_PILOTS") + ": < " + cfg.RacerCount + " >";
                case Row.Resolution: return "RESOLUTION: < " + resolutions[resIdx].X + "X" + resolutions[resIdx].Y + " >";
                case Row.Display: return Loc.Get("LABEL_DISPLAY") + ": < " + Loc.Get(cfg.DisplayMode == 2 ? "FULLSCREEN" : cfg.DisplayMode == 1 ? "BORDERLESS" : "WINDOW") + " >";
                case Row.VSync: return "VSYNC: < " + Loc.Get(cfg.VSync ? "YES" : "NO") + " >";
                case Row.Fps:
                    int cap = GameSettings.FpsCaps[cfg.FpsIndex];
                    return Loc.Get("LABEL_FPS_LIMIT") + ": < " + (cap > 0 ? cap.ToString() : Loc.Get("UNLIMITED")) + " >";
                case Row.Language: return Loc.Get("LANGUAGE") + ": < " + (cfg.Language == 0 ? "FRANÇAIS" : "ENGLISH") + " >";
                default: return Loc.Get("QUIT");
            }
        }

        void DrawMenu()
        {
            GraphicsDevice.Clear(new Color(22, 26, 46));
            int w = W, h = H;
            sb.Begin(samplerState: SamplerState.LinearClamp);

            // Bande à damier en haut et en bas.
            int cell = T(24);
            for (int i = 0; i * cell < w; i++)
            {
                Color c = (i % 2 == 0) ? Color.White * 0.9f : Color.Black * 0.6f;
                Rect(i * cell, 0, cell, cell, c);
                Rect(i * cell, h - cell, cell, cell, c);
                Rect(i * cell, cell, cell, cell, (i % 2 == 0) ? Color.Black * 0.6f : Color.White * 0.9f);
                Rect(i * cell, h - 2 * cell, cell, cell, (i % 2 == 0) ? Color.Black * 0.6f : Color.White * 0.9f);
            }

            float lx = w * 0.06f;
            float subY = h * 0.12f + T(20) * 6.5f;
            Text("KARTAMANIA", lx, h * 0.12f, T(20), new Color(255, 215, 60));
            Text(Loc.Get("SUBTITLE"), lx, subY, T(3), Color.White * 0.85f);

            float kartY = 0f, kartH = 0f;
            if (menuKart != null)
            {
                float kartW = T(210f);
                kartH = kartW * menuKart.Height / menuKart.Width;
                kartY = subY + PixelFont.LineHeight(T(3)) + T(10);
                sb.Draw(menuKart, new Vector2(lx, kartY), null, Color.White, 0f, Vector2.Zero,
                        new Vector2(kartW / menuKart.Width, kartH / menuKart.Height), SpriteEffects.None, 0f);
            }

            int ts = T(4);
            float y = menuKart != null ? kartY + kartH + T(18) : h * 0.34f;
            float lineH = ts * 9f;
            Row[] rows = inSettings ? SettingsRows : MainRows;
            int selection = inSettings ? settingsSel : sel;
            for (int i = 0; i < rows.Length; i++)
            {
                bool on = i == selection;
                string line = (on ? "> " : "  ") + RowText(rows[i]);
                Text(line, lx, y + i * lineH, ts, on ? new Color(255, 215, 60) : Color.White * 0.85f);
            }

            Text(Loc.Get("MUSIC_CREDIT"), T(12f), h - T(24f) * 2f - PixelFont.LineHeight(T(2)) - T(4f),
                 T(2), Color.White * 0.6f);
            HintPanel(MenuHint(), w * 0.5f, h - T(24) * 2f - T(12), T(3), Color.White);

            // Panneau de droite : aperçu de la piste + visages.
            var panel = new Rectangle((int)(w * 0.56f), (int)(h * 0.12f), (int)(w * 0.38f), (int)(h * 0.74f));
            Rect(panel.X, panel.Y, panel.Width, panel.Height, Color.Black * 0.35f);

            Track tr = tracks[cfg.Track];
            Text(Loc.TrackName(tr.Def.Name), panel.X + panel.Width * 0.5f, panel.Y + T(14), T(4), Color.White, true);
            var mapRect = new Rectangle(panel.X + T(30), panel.Y + T(60), panel.Width - T(60), (int)(panel.Height * 0.42f));
            DrawMiniTrack(tr, mapRect, T(9));

            FaceInfo me = faces[cfg.Player];
            float faceY = panel.Y + panel.Height * 0.62f;
            Face(me.Texture, new Vector2(panel.X + panel.Width * 0.5f, faceY), T(130), 0f, Color.White);
            Text(me.IsPlaceholder ? Loc.Get("PLAYER") : me.Name, panel.X + panel.Width * 0.5f, faceY + T(80), T(4), Color.White, true);

            if (!hasPhotos)
            {
                Text(Loc.Get("ADD_PHOTOS"), panel.X + panel.Width * 0.5f, panel.Y + panel.Height - T(64), T(2.5f), new Color(255, 215, 60), true);
                Text(Loc.Get("PHOTO_FORMAT"), panel.X + panel.Width * 0.5f, panel.Y + panel.Height - T(44), T(2.5f), Color.White * 0.8f, true);
            }
            else
            {
                float sx = panel.X + T(30);
                int shown = Math.Min(faces.Count, 8);
                float step = (panel.Width - T(60)) / 8f;
                for (int i = 0; i < shown; i++)
                    Face(faces[i].Texture, new Vector2(sx + step * (i + 0.5f), panel.Y + panel.Height - T(105)), Math.Min(step * 0.85f, T(70)), 0f,
                         i == cfg.Player ? Color.White : Color.White * 0.55f);
            }

            string musicLine = MusicPlayer.Count > 0
                ? Loc.Get("MUSIC_COUNT") + MusicPlayer.Count + Loc.Get("MUSIC_RANDOM")
                : Loc.Get("ADD_MUSIC");
            Text(musicLine, panel.X + panel.Width * 0.5f, panel.Y + panel.Height - T(23), T(2.5f),
                 MusicPlayer.Count > 0 ? Color.White * 0.8f : new Color(255, 215, 60), true);

            if (faceWarning.Length > 0)
                Text(faceWarning, panel.X + panel.Width * 0.5f, panel.Y + panel.Height + T(10), T(2.5f), Color.OrangeRed, true);

            sb.End();
        }

        // ------------------------------------------------------------------ monde

        void DrawWorld()
        {
            Track tr = race.Track;
            TrackDef def = tr.Def;
            bool city = def.Theme == TrackTheme.City;
            bool desert = def.Theme == TrackTheme.Desert;
            bool scattered = def.Name == "Circuit Ovale" || def.Name == "Montagnes du québec";
            int n = tr.N;
            int w = W, h = H;

            zoom = h / 1000f;
            camRot = -M.Pi / 2f - camAngle;
            camMatrix = Matrix.CreateTranslation(-camPos.X, -camPos.Y, 0f)
                      * Matrix.CreateRotationZ(camRot)
                      * Matrix.CreateScale(zoom, zoom, 1f)
                      * Matrix.CreateTranslation(w * 0.5f, h * 0.5f, 0f);

            float cullR = (float)Math.Sqrt((double)w * w + (double)h * h) * 0.5f / zoom + 260f;
            float cull2 = cullR * cullR;
            float treeRot = -camRot;
            Vector2 treeUp = new Vector2(-(float)Math.Sin(camRot), -(float)Math.Cos(camRot));
            Vector2 treeRight = new Vector2((float)Math.Cos(camRot), -(float)Math.Sin(camRot));

            GraphicsDevice.Clear(def.Grass);
            sb.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: camMatrix);

            float rough = (tr.HalfWidth + Track.Shoulder + 10f) * 2f;
            for (int i = 0; i < n; i++)
                if (Vector2.DistanceSquared(tr.Center[i], camPos) <= cull2) Disc(tr.Center[i], rough, def.Rough);

            if (def.Name == "Étoile du Nord")
            {
                for (int i = 0; i < n; i += 10)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int seed = i * 19 + (side + 1) * 31;
                        float along = (Rnd(seed, 211) - 0.5f) * 160f;
                        float lateral = tr.HalfWidth + Track.Shoulder + 70f + Rnd(seed + 7, 271) * 360f;
                        Vector2 sp = tr.Center[i] + tr.Tangent[i] * along + tr.Normal[i] * (side * lateral);
                        if (Vector2.DistanceSquared(sp, camPos) > cull2) continue;
                        int nearSnow = tr.NearestAny(sp);
                        if ((sp - tr.Center[nearSnow]).Length() < tr.HalfWidth + Track.Shoulder + 40f) continue;
                        float sr = 70f + Rnd(seed + 19, 7) * 50f;
                        DrawSnowBank(sp, sr, seed, treeUp, treeRight, treeRot);
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (Vector2.DistanceSquared(tr.Center[i], camPos) > cull2) continue;
                if (city && tr.IsNearCrossing(i, 10)) continue;
                Disc(tr.Center[i], def.Width + 34f, ((i / 3) % 2 == 0) ? new Color(210, 40, 40) : Color.White);
            }

            for (int i = 0; i < n; i++)
                if (Vector2.DistanceSquared(tr.Center[i], camPos) <= cull2) Disc(tr.Center[i], def.Width, def.Road);

            if (city)
                foreach (int c in tr.CrossingIndex)
                    if (Vector2.DistanceSquared(tr.Center[c], camPos) <= cull2)
                        RectC(tr.Center[c], new Vector2(def.Width * 2.4f, def.Width * 2.4f),
                              M.Angle(tr.Tangent[c]), def.Road);

            for (int i = 0; i < n; i++)
            {
                if (Vector2.DistanceSquared(tr.Center[i], camPos) > cull2) continue;
                if (city && tr.IsNearCrossing(i, 10)) continue;
                if (def.RoadMarks && i % 6 < 3) RectC(tr.Center[i], new Vector2(30f, 6f), M.Angle(tr.Tangent[i]), Color.White * 0.5f);
                if (def.RockDots && i % 4 == 0)
                {
                    float lane = (((i * 37) % 17) - 8f) / 8f * (def.Width * 0.38f);
                    Disc(tr.Center[i] + tr.Normal[i] * lane, 4f + (i % 3) * 1.5f, Color.Black * 0.55f);
                }
            }

            // Ligne de départ en damier.
            {
                int start = tr.StartIndex;
                float c = def.Width / 14f;
                float rot = M.Angle(tr.Tangent[start]);
                for (int a = 0; a < 14; a++)
                    for (int b = 0; b < 2; b++)
                    {
                        Vector2 p = tr.Center[start] + tr.Normal[start] * ((a - 6.5f) * c) + tr.Tangent[start] * ((b - 0.5f) * c);
                        RectC(p, new Vector2(c, c), rot, ((a + b) % 2 == 0) ? Color.White : Color.Black);
                    }
            }

            if (city)
            {
                int start = tr.StartIndex;
                float c = def.Width / 14f;
                float rot = M.Angle(tr.Tangent[start]);
                for (int a = 0; a < 8; a++)
                {
                    Vector2 p = tr.Center[start] + tr.Tangent[start] * (c * 3.5f) + tr.Normal[start] * ((a - 3.5f) * c * 1.7f);
                    RectC(p, new Vector2(c * 1.3f, c * 3.2f), rot, Color.White * 0.7f);
                }
            }

            // Barrières (pneus) de chaque côté : absentes de la piste en huit.
            float wall = tr.HalfWidth + Track.Shoulder;
            if (!def.StrictCourse)
            {
                for (int i = 0; i < n; i += 2)
                {
                    if (Vector2.DistanceSquared(tr.Center[i], camPos) > cull2) continue;
                    if (city && tr.IsNearCrossing(i, 12)) continue;
                    Color bc = ((i / 2) % 2 == 0) ? new Color(210, 40, 40) : Color.White;
                    Disc(tr.Center[i] + tr.Normal[i] * wall, 22f, bc);
                    Disc(tr.Center[i] - tr.Normal[i] * wall, 22f, bc);
                }
            }


            float Rnd(int s, int m) => (Math.Abs(s * 9301 + 49297) % m) / (float)m;
            var chaletSpots = new List<(Vector2 Pos, float Radius, float Scale, int Seed)>();
            var citySpots = new List<(Vector2 Pos, float Radius)>();
            if (def.Forest || def.DenseTrees)
            {
                int chaletStep = def.Forest ? 40 : 80;
                for (int i = 0; i < n; i += chaletStep)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int seed = i * 17 + (side + 1) * 11 + 5;
                        float along = (Rnd(seed + 1, 211) - 0.5f) * 170f;
                        float lateral = def.Forest
                            ? wall + 340f + Rnd(seed + 2, 271) * 140f
                            : wall + 320f + Rnd(seed + 2, 271) * 120f;
                        Vector2 p = tr.Center[i] + tr.Tangent[i] * along + tr.Normal[i] * (side * lateral);
                        if (Vector2.DistanceSquared(p, camPos) > cull2) continue;
                        int nearTrack = tr.NearestAny(p);
                        if ((p - tr.Center[nearTrack]).Length() < wall + 150f) continue;
                        float scale = def.Forest
                            ? 0.65f + Rnd(seed + 3, 7) * 0.20f
                            : 0.70f + Rnd(seed + 3, 7) * 0.18f;
                        float clear = 105f * scale;
                        bool overlaps = false;
                        foreach (var c in chaletSpots)
                        {
                            if ((p - c.Pos).Length() < c.Radius + clear * 1.6f) { overlaps = true; break; }
                        }
                        if (overlaps) continue;
                        chaletSpots.Add((p, clear, scale, seed));
                    }
                }
            }

            int treeStep = def.Forest ? 2 : (city ? 3 : (desert ? 7 : (def.DenseTrees ? 5 : (scattered ? 3 : 8))));
            int treeLayers = def.Forest || city || desert || def.DenseTrees || scattered ? 2 : 1;
            for (int i = 0; i < n; i += treeStep)
            {
                if (city && tr.IsNearCrossing(i, 14)) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int layer = 0; layer < treeLayers; layer++)
                    {
                        int seed = i * 7 + (side + 1) * 13 + layer * 29;
                        Vector2 p;
                        if (city)
                        {
                            float along = (Rnd(seed, 211) - 0.5f) * 240f;
                            float lateral = wall + 150f + Rnd(seed + 31 + layer * 47, 271) * 380f;
                            p = tr.Center[i] + tr.Tangent[i] * along + tr.Normal[i] * (side * lateral);
                        }
                        else if (def.Forest)
                        {
                            float along = (Rnd(seed, 211) - 0.5f) * 260f;
                            float lateral = wall + 60f + Rnd(seed + 31 + layer * 47, 271) * 700f;
                            p = tr.Center[i] + tr.Tangent[i] * along + tr.Normal[i] * (side * lateral);
                        }
                        else if (desert)
                        {
                            float jitter = ((seed * 37) % 17 - 8) * 5f;
                            float along = (Rnd(seed + 7, 211) - 0.5f) * 130f;
                            float lateral = wall + 95f + jitter + layer * 280f;
                            p = tr.Center[i] + tr.Tangent[i] * along + tr.Normal[i] * (side * lateral);
                        }
                        else if (def.DenseTrees)
                        {
                            float along = (Rnd(seed, 211) - 0.5f) * 160f;
                            float lateral = wall + 80f + Rnd(seed + 31, 271) * 230f + layer * 170f;
                            p = tr.Center[i] + tr.Tangent[i] * along + tr.Normal[i] * (side * lateral);
                        }
                        else if (scattered)
                        {
                            float along = (Rnd(seed, 211) - 0.5f) * 220f;
                            float lateral = wall + 70f + Rnd(seed + 31 + layer * 47, 271) * 420f;
                            p = tr.Center[i] + tr.Tangent[i] * along + tr.Normal[i] * (side * lateral);
                        }
                        else
                        {
                            float jitter = ((seed * 37) % 17 - 8) * 5f;
                            p = tr.Center[i] + tr.Normal[i] * (side * (wall + 95f + jitter));
                        }
                        if (Vector2.DistanceSquared(p, camPos) > cull2) continue;
                        if (def.Forest || city || desert || def.DenseTrees || scattered)
                        {
                            int nearTrack = tr.NearestAny(p);
                            float pad = desert ? 50f : (city ? 100f : 80f);
                            if ((p - tr.Center[nearTrack]).Length() < wall + pad) continue;
                        }
                        float scale = desert
                            ? (0.72f + Rnd(seed + 5, 7) * 0.20f) * (layer == 1 ? 0.88f : 1f)
                            : (0.85f + ((seed * 11) % 9) * 0.045f) * ((def.DenseTrees && layer == 1) ? 0.88f : ((def.Forest && layer == 1) ? 0.92f : ((city && layer == 1) ? 0.88f : ((scattered && layer == 1) ? 0.90f : 1f))));
                        if (def.Forest || def.DenseTrees)
                        {
                            float treeRadius = 78f * scale;
                            bool nearChalet = false;
                            foreach (var c in chaletSpots)
                            {
                                if ((p - c.Pos).Length() < c.Radius + treeRadius + 30f) { nearChalet = true; break; }
                            }
                            if (nearChalet) continue;
                        }
                        if (city)
                        {
                            if (layer == 0 && i % 32 == 0)
                            {
                                Vector2 lampPos = tr.Center[i] + tr.Normal[i] * (side * (wall + 80f));
                                DrawLamp(lampPos, scale, treeUp, treeRight, treeRot, side > 0);
                            }
                            float clear = 95f * scale;
                            bool overlaps = false;
                            foreach (var b in citySpots)
                            {
                                if ((p - b.Pos).Length() < b.Radius + clear) { overlaps = true; break; }
                            }
                            if (overlaps) continue;
                            citySpots.Add((p, clear));
                            DrawBuilding(p, scale, seed, treeUp, treeRight, treeRot);
                        }
                        else DrawTree(p, scale, def.Theme, treeUp, treeRight, treeRot, seed + layer * 17);
                    }
                }
            }

            foreach (var c in chaletSpots)
                DrawChalet(c.Pos, c.Scale, treeUp, treeRight, treeRot, c.Seed, def.DenseTrees);

            // Obstacles (cônes).
            foreach (Vector2 o in tr.ObstaclePos)
            {
                if (Vector2.DistanceSquared(o, camPos) > cull2) continue;
                Disc(o + new Vector2(4f, 6f), Race.ObstacleRadius * 2f, Color.Black * 0.25f);
                Disc(o, Race.ObstacleRadius * 2f, new Color(240, 90, 20));
                Disc(o, Race.ObstacleRadius * 1.3f, Color.White);
                Disc(o, Race.ObstacleRadius * 0.6f, new Color(240, 90, 20));
            }

            // Boîtes d'armes.
            foreach (ItemBox b in race.Boxes)
            {
                if (b.Respawn > 0f || Vector2.DistanceSquared(b.Pos, camPos) > cull2) continue;
                float pulse = 1f + 0.1f * (float)Math.Sin(anim * 6f);
                RectC(b.Pos, new Vector2(46f, 46f) * pulse, anim * 2f, new Color(255, 215, 60));
                RectC(b.Pos, new Vector2(32f, 32f) * pulse, -anim * 2f, new Color(60, 90, 220));
                RectC(b.Pos, new Vector2(14f, 14f), anim * 4f, Color.White);
            }

            // Mines.
            foreach (Mine m in race.Mines)
            {
                if (Vector2.DistanceSquared(m.Pos, camPos) > cull2) continue;
                Disc(m.Pos, 42f, Color.Black * 0.85f);
                Disc(m.Pos, 18f, (((int)(anim * 4f)) % 2 == 0) ? Color.Red : new Color(120, 0, 0));
            }

            // Missiles.
            foreach (Missile m in race.Missiles)
            {
                Vector2 d = M.Dir(m.Angle);
                Disc(m.Pos - d * 30f, 22f, Color.Orange * 0.9f);
                RectC(m.Pos, new Vector2(46f, 14f), m.Angle, new Color(220, 40, 40));
                Disc(m.Pos + d * 22f, 15f, Color.White);
            }

            // Karts (le joueur en dernier pour qu'il soit au-dessus).
            foreach (Kart k in race.Karts)
                if (!k.IsPlayer && Vector2.DistanceSquared(k.Pos, camPos) <= cull2) DrawKart(k);
            DrawKart(race.Player);

            // Particules.
            foreach (Particle p in race.Particles)
            {
                float f = p.Age / p.Life;
                Disc(p.Pos, p.Size * (1f - 0.6f * f), p.Color * (1f - f));
            }

            sb.End();
        }

        void DrawKart(Kart k)
        {
            if (k.Eliminated) return;

            float rot = k.Angle + k.SpinVisual;
            const float s = 0.9f;
            Vector2 f = M.Dir(rot) * s;
            Vector2 r = new Vector2(-f.Y, f.X);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float D(float v) => v * s;
            float alpha = (k.Invuln > 0f && (((int)(anim * 14f)) & 1) == 0) ? 0.45f : 1f;

            Ellipse(k.Pos + V(5f, 7f), V(86f, 52f), rot, Color.Black * 0.25f);

            if (k.BoostTimer > 0f)
            {
                float flame = D(20f + 8f * (float)Math.Sin(anim * 40f));
                Disc(k.Pos - f * 50f + r * 11f, flame, Color.Orange * alpha);
                Disc(k.Pos - f * 50f - r * 11f, flame, Color.Orange * alpha);
                Disc(k.Pos - f * 44f + r * 11f, flame * 0.55f, Color.Yellow * alpha);
                Disc(k.Pos - f * 44f - r * 11f, flame * 0.55f, Color.Yellow * alpha);
            }

            Color wc = new Color(30, 30, 34) * alpha;
            Color hub = new Color(180, 185, 195) * alpha;
            float frontRot = rot + k.Steer * 0.4f * (k.Speed >= 0f ? 1f : -1f);
            Vector2 frontA = k.Pos + f * 24f + r * 25f;
            Vector2 frontB = k.Pos + f * 24f - r * 25f;
            Vector2 rearA = k.Pos - f * 22f + r * 28f;
            Vector2 rearB = k.Pos - f * 22f - r * 28f;
            RectC(frontA, V(18f, 11f), frontRot, wc);
            RectC(frontB, V(18f, 11f), frontRot, wc);
            RectC(rearA, V(24f, 14f), rot, wc);
            RectC(rearB, V(24f, 14f), rot, wc);
            Disc(frontA, D(6f), hub);
            Disc(frontB, D(6f), hub);
            Disc(rearA, D(8f), hub);
            Disc(rearB, D(8f), hub);

            Ellipse(k.Pos, V(80f, 44f), rot, k.Color * 0.72f * alpha);
            Ellipse(k.Pos + f * 3f, V(68f, 36f), rot, k.Color * alpha);
            Ellipse(k.Pos + f * 31f, V(34f, 26f), rot, k.Color * alpha);
            Ellipse(k.Pos + r * 26f - f * 3f, V(30f, 16f), rot, k.Color * 0.85f * alpha);
            Ellipse(k.Pos - r * 26f - f * 3f, V(30f, 16f), rot, k.Color * 0.85f * alpha);
            Ellipse(k.Pos - f * 3f, V(34f, 24f), rot, new Color(20, 24, 30) * (0.5f * alpha));

            RectC(k.Pos + f * 40f, V(9f, 56f), rot, k.Color * 0.9f * alpha);
            RectC(k.Pos - f * 36f, V(8f, 58f), rot, k.Color * 0.8f * alpha);
            RectC(k.Pos - f * 28f + r * 19f, V(16f, 5f), rot, Color.Black * (0.6f * alpha));
            RectC(k.Pos - f * 28f - r * 19f, V(16f, 5f), rot, Color.Black * (0.6f * alpha));

            Disc(k.Pos + f * 40f + r * 15f, D(8f), Color.Yellow * alpha);
            Disc(k.Pos + f * 40f - r * 15f, D(8f), Color.Yellow * alpha);
            Disc(k.Pos - f * 42f + r * 11f, D(7f), new Color(150, 155, 165) * alpha);
            Disc(k.Pos - f * 42f - r * 11f, D(7f), new Color(150, 155, 165) * alpha);

            if (k.ShieldTimer > 0f)
            {
                float blink = (k.ShieldTimer < 2f && (((int)(anim * 10f)) & 1) == 0) ? 0.5f : 1f;
                Disc(k.Pos, D(104f), Color.Cyan * (0.20f * blink));
                Disc(k.Pos, D(96f), Color.Cyan * (0.15f * blink));
            }

            // Le visage reste toujours droit à l'écran, même quand la caméra tourne.
            Vector2 up = M.Rot(new Vector2(0f, -1f), -camRot);
            Face(k.Face, k.Pos + up * D(14f), D(43f), -camRot, Color.White * alpha);
        }

        // ------------------------------------------------------------------ HUD

        void DrawHud()
        {
            int w = W, h = H;
            float u = h / 1080f;
            float m = 24f * u;
            Kart p = race.Player;
            Kart view = ViewKart();
            Vector2 up = M.Rot(new Vector2(0f, -1f), -camRot);

            // Noms et points de vie au-dessus des karts.
            foreach (Kart k in race.Karts)
            {
                if (k.Eliminated) continue;
                Vector2 s = Vector2.Transform(k.Pos + up * 14f, camMatrix);
                if (s.X < -100 || s.X > w + 100 || s.Y < -100 || s.Y > h + 100) continue;
                float ty = s.Y - 62f * zoom;
                Text(k.Name, s.X, ty, T(2), k.IsPlayer ? new Color(255, 215, 60) : Color.White, true);
                for (int i = 0; i < k.Hp; i++)
                    Disc(new Vector2(s.X + (i - (k.Hp - 1) * 0.5f) * 14f * u, ty - 10f * u), 9f * u, Color.Red);
            }

            // Haut gauche : tour, position, vie.
            {
                int ts = T(5);
                int lh = PixelFont.LineHeight(ts);
                float gap = 6f * u;
                Text(Loc.Get("HUD_LAP") + Math.Min(Math.Max(view.Lap + 1, 1), race.TotalLaps) + "/" + race.TotalLaps, m, m, ts, Color.White);
                float posY = m + lh + gap;
                Text("POS " + view.Place + "/" + race.Karts.Count, m, posY, ts, new Color(255, 215, 60));
                int ls = T(2.5f);
                float lblY = posY + lh + gap;
                Text(Loc.Get("HUD_HEALTH"), m, lblY, ls, Color.White * 0.85f);
                float dotY = lblY + PixelFont.LineHeight(ls) + 4f * u + 13f * u;
                for (int i = 0; i < Kart.MaxHp; i++)
                    Disc(new Vector2(m + 14f * u + i * 34f * u, dotY), 26f * u, i < view.Hp ? Color.Red : Color.White * 0.25f);
            }

            // Haut centre : arme en main.
            {
                float bs = 120f * u;
                float cx = w * 0.5f;
                Rect(cx - bs * 0.5f, m, bs, bs, Color.Black * 0.45f);
                Rect(cx - bs * 0.5f, m, bs, 3f * u, Color.White);
                Rect(cx - bs * 0.5f, m + bs - 3f * u, bs, 3f * u, Color.White);
                Rect(cx - bs * 0.5f, m, 3f * u, bs, Color.White);
                Rect(cx + bs * 0.5f - 3f * u, m, 3f * u, bs, Color.White);
                Vector2 ic = new Vector2(cx, m + bs * 0.42f);
                string name = "";
                switch (view.Item)
                {
                    case ItemType.Missile: Disc(ic, 50f * u, new Color(220, 40, 40)); Disc(ic + new Vector2(0f, -22f * u), 22f * u, Color.White); name = "MISSILE"; break;
                    case ItemType.Mine: Disc(ic, 54f * u, Color.Black); Disc(ic, 22f * u, Color.Red); name = "MINE"; break;
                    case ItemType.Boost: Disc(ic, 54f * u, Color.Orange); Disc(ic, 28f * u, Color.Yellow); name = "TURBO"; break;
                    case ItemType.Shield: Disc(ic, 54f * u, Color.Cyan * 0.8f); Disc(ic, 34f * u, Color.White * 0.5f); name = Loc.Get("ITEM_SHIELD"); break;
                    case ItemType.Heart:
                        Heart(ic, 78f * u, new Color(255, 70, 80));
                        name = Loc.Get("ITEM_HEART"); break;
                }
                if (name.Length > 0) Text(name, cx, m + bs * 0.78f, T(2.5f), Color.White, true);
                else Text("-", cx, m + bs * 0.38f, T(6), Color.White * 0.4f, true);
            }

            // Haut droite : FPS et temps.
            {
                int fpsScale = T(3);
                Text("FPS " + fps, w - m - PixelFont.Measure("FPS " + fps, fpsScale), m, fpsScale, fps >= 115 ? new Color(120, 255, 120) : Color.White);
                string tm = FormatTime(race.Time);
                float timeY = m + PixelFont.LineHeight(fpsScale) + 4f * u;
                Text(tm, w - m - PixelFont.Measure(tm, T(4)), timeY, T(4), Color.White);
            }

            // Bas droite : vitesse.
            string spd = (int)(view.Vel.Length() * 0.5f) + " KM/H";
            Text(spd, w - m - PixelFont.Measure(spd, T(5)), h - m - T(5) * 5f, T(5), Color.White);

            // Bas gauche : mini-carte.
            {
                var r = new Rectangle((int)m, (int)(h - m - 250f * u), (int)(300f * u), (int)(250f * u));
                Rect(r.X, r.Y, r.Width, r.Height, Color.Black * 0.45f);
                var inner = new Rectangle(r.X + (int)(14f * u), r.Y + (int)(14f * u), r.Width - (int)(28f * u), r.Height - (int)(28f * u));
                DrawMiniTrack(race.Track, inner, 4f * u);
                foreach (Kart k in race.Karts)
                {
                    if (k.Eliminated) continue;
                    Vector2 mp = MapPoint(race.Track, inner, k.Pos);
                    if (k.IsPlayer) Disc(mp, 16f * u, Color.White);
                    Disc(mp, (k.IsPlayer ? 11f : 9f) * u, k.Color);
                }
            }

            // Droite : messages (éliminations...).
            for (int i = 0; i < race.Feed.Count; i++)
            {
                FeedMessage fm = race.Feed[i];
                float a = M.Clamp(5f - fm.Age, 0f, 1f);
                int sc = T(3);
                Text(fm.Text, w - m - PixelFont.Measure(fm.Text, sc), h * 0.30f + i * sc * 8f, sc, Color.White * a);
            }

            // Compte à rebours / GO.
            if (race.Countdown > 0f)
            {
                int num = (int)Math.Ceiling(race.Countdown);
                Text(num.ToString(), w * 0.5f, h * 0.30f, T(28), new Color(255, 215, 60), true);
                if (race.Countdown <= Race.RocketStartWindow)
                    Text(Loc.Get("HINT_ROCKET"), w * 0.5f, h * 0.30f + T(18), T(3),
                         new Color(120, 255, 160), true);
            }
            else if (race.Time < 0.9f)
            {
                Text("GO!", w * 0.5f, h * 0.30f, T(28), new Color(120, 255, 120), true);
            }

            if (p.Finished || p.Eliminated)
            {
                int remaining = 0;
                foreach (Kart k in race.Karts)
                    if (!k.Finished && !k.Eliminated) remaining++;
                if (remaining > 0)
                {
                    string msg = Loc.Get("WAITING") + remaining + Loc.Get("RACING");
                    string hint = pad.IsConnected ? Loc.Get("END_RACE_PAD") : Loc.Get("END_RACE_KB");
                    string viewHint = pad.IsConnected ? Loc.Get("VIEW_HINT_PAD") : Loc.Get("VIEW_HINT_KB");
                    Text(msg, w * 0.5f, h * 0.30f, T(4), new Color(255, 215, 60), true);
                    Text(hint, w * 0.5f, h * 0.30f + T(4) * 8f, T(3), Color.White * 0.9f, true);
                    Text(viewHint, w * 0.5f, h * 0.30f + T(4) * 16f, T(3), Color.White * 0.85f, true);
                    Text(Loc.Get("VIEW_LABEL") + ViewKart().Name, w * 0.5f, h * 0.30f + T(4) * 24f, T(3), new Color(255, 215, 60), true);
                }
            }

            // Aide aux commandes au début de course : grande, sur fond sombre, adaptée au périphérique.
            if (state == State.Racing && race.Time < 9f)
                HintPanel(new[] { RaceHint() }, w * 0.5f, h - T(24), T(3), Color.White);
        }

        void DrawPause()
        {
            Rect(0, 0, W, H, Color.Black * 0.6f);
            Text("PAUSE", W * 0.5f, H * 0.22f, T(14), Color.White, true);
            Text(Loc.Get("CONTROLS"), W * 0.5f, H * 0.44f, T(4), new Color(255, 215, 60), true);
            HintPanel(new[] { RaceHint() }, W * 0.5f, H * 0.63f, T(3), Color.White);

            string[] opts = PauseHint();
            float oy = H * 0.70f;
            for (int i = 0; i < opts.Length; i++)
                Text(opts[i], W * 0.5f, oy + i * T(4) * 8f, T(4), Color.White, true);
        }

        void DrawPenalty()
        {
            if (penaltyBannerTimer <= 0f) return;
            float fade = M.Clamp(penaltyBannerTimer * 2f, 0f, 1f);

            int title = T(24);
            float y = H * 0.12f;
            Rect(0, y, W, H * 0.32f, Color.Black * 0.8f * fade);
            Rect(0, y, W, T(6), Color.OrangeRed * fade);
            Text(Loc.Get("PENALTY_TITLE"), W * 0.5f, y + H * 0.03f, title, Color.OrangeRed * fade, true);
            Text(Loc.Get("PENALTY_RETURN"), W * 0.5f, y + H * 0.03f + title * 7f, T(5), Color.White * fade, true);
        }

        void DrawFinishBanner()
        {
            Kart p = race.Player;
            if (p.Eliminated)
            {
                float age = race.Time - p.ElimTime;
                if (age < 0f || age > 3.2f) return;
                float fade = M.Clamp(Math.Min(age * 4f, (3.2f - age) * 2f), 0f, 1f);
                Rect(0, H * 0.28f, W, H * 0.18f, Color.Black * 0.55f * fade);
                Text(Loc.Get("RESULT_ELIMINATED"), W * 0.5f, H * 0.30f, T(15), Color.OrangeRed * fade, true);
                return;
            }

            if (!p.Finished) return;

            float finishAge = race.Time - p.FinishTime;
            if (finishAge < 0f || finishAge > 3.2f) return;

            bool won = p.Place == 1;
            string title = won ? Loc.Get("RESULT_WIN") : Loc.ResultPlace(p.Place);
            float finishFade = M.Clamp(Math.Min(finishAge * 4f, (3.2f - finishAge) * 2f), 0f, 1f);
            Color color = (won ? new Color(255, 215, 60) : Color.White) * finishFade;

            Rect(0, H * 0.28f, W, H * 0.18f, Color.Black * 0.55f * finishFade);
            Text(title, W * 0.5f, H * 0.30f, T(15), color, true);
        }

        void DrawResults()
        {
            int w = W, h = H;
            Rect(0, 0, w, h, Color.Black * 0.68f);
            Kart p = race.Player;

            bool won = !p.Eliminated && p.Place == 1;
            string title = p.Eliminated ? Loc.Get("RESULT_ELIMINATED")
                          : won ? Loc.Get("RESULT_WIN")
                          : Loc.ResultPlace(p.Place);
            Color tc = p.Eliminated ? Color.OrangeRed : (won ? new Color(255, 215, 60) : Color.White);
            Text(title, w * 0.5f, h * 0.035f, T(14), tc, true);

            float best = cfg.GetBestTime(race.Track.Def.Name, race.TotalLaps, cfg.Difficulty);
            string bestLine = Loc.Get("BEST_TIME") + race.TotalLaps + Loc.Get("BEST_LAPS") + (best > 0f ? FormatTime(best) : "--:--.-");
            Text(bestLine, w * 0.5f, h * 0.115f, T(4), new Color(255, 215, 60), true);
            if (newRecord) Text(Loc.Get("NEW_RECORD"), w * 0.5f, h * 0.155f, T(3), new Color(120, 255, 120), true);

            int count = Math.Max(1, race.Ranking.Count);
            float top = h * 0.20f;
            float bottom = h * 0.86f;
            float row = Math.Min(T(104), (bottom - top) / count);
            float faceD = Math.Min(row * 0.72f, T(66));
            int ts = T(4);
            float textYOff = -ts * 2.5f;

            float boxLeft = w * 0.16f;
            float boxRight = w * 0.84f;
            float boxW = boxRight - boxLeft;
            float faceX = boxLeft + T(85);
            float nameX = boxLeft + T(150);
            float statusRight = boxRight - T(24);

            for (int i = 0; i < race.Ranking.Count; i++)
            {
                Kart k = race.Ranking[i];
                float yy = top + row * i;
                float cy = yy + row * 0.5f;

                if (k.IsPlayer)
                    Rect(boxLeft, yy + row * 0.06f, boxW, row * 0.88f, Color.White * 0.14f);

                Text((i + 1) + ".", boxLeft + T(16), cy + textYOff, ts, Color.White);
                Face(k.Face, new Vector2(faceX, cy), faceD, 0f, Color.White);
                Text(k.Name, nameX, cy + textYOff, ts, k.IsPlayer ? new Color(255, 215, 60) : Color.White);

                string status;
                if (k.Finished) status = FormatTime(k.FinishTime);
                else if (k.Eliminated) status = Loc.Get("ELIMINATED");
                else status = Loc.Get("RESULT_LAP") + Math.Min(Math.Max(k.Lap + 1, 1), race.TotalLaps);
                string kills = k.Kills > 0 ? "  X" + k.Kills : "";
                string right = status + kills;
                Text(right, statusRight - PixelFont.Measure(right, ts), cy + textYOff, ts,
                     k.Eliminated ? Color.OrangeRed : Color.White);
            }

            HintPanel(new[] { ResultsHint() }, w * 0.5f, h * 0.97f, T(3), Color.White);
        }
    }
}

