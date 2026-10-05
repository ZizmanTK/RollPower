using UnityEngine;

namespace DiceHero
{
    /// <summary>HUD and every menu screen (IMGUI on a 1080-tall virtual canvas, see UiKit).</summary>
    public partial class GameLoop
    {
        Menu titleMenu, pauseMenu, settingsMenu, gameOverMenu;
        int upgradeMenuSel = 1;
        float uiTime;

        /// <summary>Capture mode: title screen shows only the logo (itch.io cover art).</summary>
        public bool CoverMode;

        static bool CanQuit => Application.platform != RuntimePlatform.WebGLPlayer;
        static string Num(int v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        void BuildMenus()
        {
            titleMenu = new Menu().Add("PLAY").Add("HOW TO PLAY").Add("SETTINGS");
            if (CanQuit) titleMenu.Add("QUIT");
            pauseMenu = new Menu().Add("RESUME").Add("SETTINGS").Add("HOW TO PLAY").Add("RESTART").Add("MAIN MENU");
            gameOverMenu = new Menu().Add("PLAY AGAIN").Add("MAIN MENU");
            settingsMenu = new Menu()
                .AddSlider("MUSIC", () => Settings.Music, v => Settings.Music = v)
                .AddSlider("SOUND FX", () => Settings.Sfx, v => Settings.Sfx = v)
                .AddChoice("SCREEN SHAKE", () => Settings.ShakeAmount <= 0f ? "OFF" : Settings.ShakeAmount < 0.9f ? "LOW" : "FULL",
                    d => { float[] o = { 0f, 0.5f, 1f }; int i = Settings.ShakeAmount <= 0f ? 0 : Settings.ShakeAmount < 0.9f ? 1 : 2; Settings.ShakeAmount = o[(i + d + 3) % 3]; if (cam != null) cam.Shake(0.4f); })
                .AddChoice("FULLSCREEN", () => Screen.fullScreen ? "ON" : "OFF", d => Screen.fullScreen = !Screen.fullScreen)
                .AddChoice("HINTS", () => Settings.ShowTutorial ? "ON" : "OFF", d => Settings.ShowTutorial = !Settings.ShowTutorial)
                .Add("BACK");
        }

        // ---------------- Menu input (keyboard / gamepad) ----------------

        void MenuInput(float udt)
        {
            uiTime += udt;
            switch (State)
            {
                case Screen2.Title:
                    Activate(State, titleMenu.UpdateInput(udt));
                    break;
                case Screen2.Paused:
                    if (Controls.Back || Controls.Pause) { SetPaused(false); Sound.Play(Sfx.UiConfirm, 0.6f, 0f); break; }
                    Activate(State, pauseMenu.UpdateInput(udt));
                    break;
                case Screen2.Settings:
                    if (Controls.Back) { State = settingsReturn; Settings.Save(); break; }
                    Activate(State, settingsMenu.UpdateInput(udt));
                    break;
                case Screen2.HowTo:
                    if (Controls.Back || Controls.Confirm) { State = howToReturn; Sound.Play(Sfx.UiConfirm, 0.6f, 0f); }
                    break;
                case Screen2.GameOver:
                    if (Controls.Restart) { Reload(true); break; }
                    Activate(State, gameOverMenu.UpdateInput(udt));
                    break;
                case Screen2.Upgrade:
                {
                    if (hand == null || hand.Count == 0) break;
                    var nav = Controls.Nav(udt);
                    if (nav.x != 0) { upgradeMenuSel = Mathf.Clamp(upgradeMenuSel + nav.x, 0, hand.Count - 1); Sound.Play(Sfx.UiMove, 0.5f, 0f); }
                    int key = Controls.NumberKey();
                    if (key >= 1 && key <= hand.Count) { PickUpgrade(hand[key - 1]); break; }
                    if (Controls.Confirm) PickUpgrade(hand[Mathf.Clamp(upgradeMenuSel, 0, hand.Count - 1)]);
                    break;
                }
            }
        }

        void Activate(Screen2 from, int index)
        {
            if (index < 0) return;
            switch (from)
            {
                case Screen2.Title:
                    if (index == 0) StartRun();
                    else if (index == 1) { howToReturn = Screen2.Title; State = Screen2.HowTo; }
                    else if (index == 2) { settingsReturn = Screen2.Title; settingsMenu.Selected = 0; State = Screen2.Settings; }
                    else if (index == 3) Application.Quit();
                    break;
                case Screen2.Paused:
                    if (index == 0) SetPaused(false);
                    else if (index == 1) { settingsReturn = Screen2.Paused; settingsMenu.Selected = 0; State = Screen2.Settings; }
                    else if (index == 2) { howToReturn = Screen2.Paused; State = Screen2.HowTo; }
                    else if (index == 3) Reload(true);
                    else Reload(false);
                    break;
                case Screen2.Settings:
                    if (index == settingsMenu.Items.Count - 1) { State = settingsReturn; Settings.Save(); }
                    break;
                case Screen2.GameOver:
                    Reload(index == 0);
                    break;
            }
        }

        // ---------------- Drawing ----------------

        void OnGUI()
        {
            if (Dice == null) return;
            UiKit.Begin();
            float w = UiKit.W;

            if (State == Screen2.Playing || State == Screen2.Paused || State == Screen2.Upgrade || (State == Screen2.Settings && settingsReturn == Screen2.Paused) || (State == Screen2.HowTo && howToReturn == Screen2.Paused))
                DrawHud(w);

            if (screenFlash > 0f) UiKit.Rect(new Rect(0, 0, w, UiKit.H), new Color(1f, 0.96f, 0.85f, screenFlash * 0.5f));
            if (Game.HurtFlash > 0f) UiKit.Rect(new Rect(0, 0, w, UiKit.H), new Color(1f, 0.1f, 0.15f, Game.HurtFlash * 0.35f));

            switch (State)
            {
                case Screen2.Title: DrawTitle(w); break;
                case Screen2.Paused: Dim(w, 0.55f); DrawPause(w); break;
                case Screen2.Settings: Dim(w, 0.6f); DrawSettings(w); break;
                case Screen2.HowTo: Dim(w, 0.7f); DrawHowTo(w); break;
                case Screen2.Upgrade: Dim(w, 0.55f); DrawUpgrade(w); break;
                case Screen2.GameOver: Dim(w, 0.45f); DrawGameOver(w); break;
            }
            UiKit.End();
        }

        static void Dim(float w, float a) => UiKit.Rect(new Rect(0, 0, w, UiKit.H), new Color(0.02f, 0.03f, 0.06f, a));

        // ---------------- HUD ----------------

        // HUD palette (style 6 "Starship", from Everspace 2; upgrade screen style 7 "Mech ops", from The Riftbreaker).
        static readonly Color HudMuted = Palette.Hex("#AFC2C4"), HudTeal = Palette.Hex("#1F7F78"), HudMint = Palette.Hex("#46D3A6"),
            HudAmber = Palette.Hex("#F2B565"), HudHull = Palette.Hex("#E8323C"), HudOrange = Palette.Hex("#F29A2E"), HudCyan = Palette.Hex("#29B6F6");

        void DrawHud(float w)
        {
            var def = Weapons.Current;
            bool playing = State == Screen2.Playing;

            // Emblem: gold diamond with the current gun's icon. It pulses while a fresh roll overcharges the gun.
            var em = new Vector2(63f, 63f);
            if (Weapons.Overcharge > 0f)
                UiKit.Glow(new Rect(em.x - 70, em.y - 70, 140, 140), new Color(def.color.r, def.color.g, def.color.b, 0.35f + 0.2f * Mathf.Sin(uiTime * 14f)));
            UiKit.Emblem(em, 80f);
            UiKit.DrawIcon(new Rect(em.x - 17, em.y - 17, 34, 34), "ui_gun" + def.number, def.color);

            // Hull bar (one cell per heart), then the combo timer bar.
            float bx = 112f;
            UiKit.Line("HULL", bx, 30, 15, HudMuted, 0f, 1);
            UiKit.Line($"{Game.Hp}/{Game.MaxHp}", bx + 48, 26, 21, Game.Hp <= 2 ? HudHull : UiKit.Text, 0f, 2);
            float cellW = (330f - (Game.MaxHp - 1) * 3f) / Game.MaxHp;
            for (int i = 0; i < Game.MaxHp; i++)
            {
                bool full = i < Game.Hp;
                float flash = full && Game.Hp <= 2 ? 0.6f + 0.4f * Mathf.Sin(uiTime * 10f) : 1f;
                UiKit.Rect(new Rect(bx + i * (cellW + 3f), 54, cellW, 9), full ? new Color(HudHull.r, HudHull.g, HudHull.b, flash) : new Color(1f, 1f, 1f, 0.12f));
            }
            bool combo = Game.Combo > 1;
            UiKit.Bar(new Rect(bx, 68, 310, 7), combo ? Game.ComboTimer / Game.ComboWindow : 0f, HudOrange, new Color(1f, 1f, 1f, 0.1f));
            if (combo) UiKit.Line($"COMBO ×{Game.Combo}", bx, 82, 15, HudAmber, 0f, 2);

            // Objective list: wave (highlighted), enemies left, score, gun.
            float ly = 112f;
            UiKit.FadeStrip(new Rect(24, ly, 440, 34), new Color(HudTeal.r, HudTeal.g, HudTeal.b, 0.8f));
            UiKit.Diamond(new Vector2(42, ly + 17), 16, HudMint);
            UiKit.Line(Game.BossWave ? $"WAVE {Game.Wave} · BOSS" : $"WAVE {Game.Wave}", 60, ly + 10, 22, UiKit.Text, 0f, 2, 1.5f);
            (string text, Color c)[] rows =
            {
                (Game.EnemiesLeft == 1 ? "1 enemy left" : $"{Game.EnemiesLeft} enemies left", UiKit.Text),
                ("Score  " + Num(Game.Score), HudAmber),
                (Title(def.name), def.color),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = ly + 40 + i * 30;
                UiKit.Diamond(new Vector2(42, y + 12), 13, new Color(0.9f, 0.95f, 0.95f, 0.9f));
                UiKit.Diamond(new Vector2(42, y + 12), 7, new Color(0.04f, 0.08f, 0.1f, 1f));
                UiKit.Line(rows[i].text, 60, y + 6, 20, rows[i].c, 0f, 1, 1.5f);
            }

            // Pause prompt, top right.
            var pc = new Vector2(w - 60f, 60f);
            UiKit.Emblem(pc, 70f);
            UiKit.DrawIcon(new Rect(pc.x - 13, pc.y - 13, 26, 26), "ui_pause", UiKit.Text);
            float kw = UiKit.TextWidth("ESC", 18) + 12f;
            UiKit.Keycap(pc.x - 48f - kw, pc.y - 15f, "ESC");

            // Boss: name, bar and the number it's weak to.
            var boss = Game.Boss;
            if (boss != null && boss.Alive)
            {
                float bw = Mathf.Min(600f, w - 1100f);
                float bossX = w * 0.5f - bw * 0.5f;
                UiKit.Line("HIGH ROLLER", w * 0.5f, 28, 24, UiKit.Text, 0.5f, 2, 2f);
                UiKit.Rect(new Rect(bossX - 2, 60, bw + 4, 14), new Color(0f, 0f, 0f, 0.6f));
                UiKit.Rect(new Rect(bossX, 62, bw * Mathf.Clamp01(boss.hp / boss.maxHp), 10), HudHull);
                int weak = boss.Weakness;
                UiKit.DieFace(new Rect(bossX + bw + 18, 40, 46, 46), weak, def.number == weak ? UiKit.Gold : WeaponDef.All[weak].color, UiKit.Ink);
                UiKit.Line("WEAK TO", bossX + bw + 70, 46, 14, HudMuted, 0f, 1);
                UiKit.Line(weak.ToString(), bossX + bw + 70, 62, 20, UiKit.Text, 0f, 2);
            }

            DrawBombAlerts(w);
            if (!playing) return;

            // Wave banner
            if (bannerTime > 0f)
            {
                float a = Mathf.Clamp01(bannerTime * 2.5f) * Mathf.Clamp01((bannerMax - bannerTime) * 8f + 0.2f);
                float slide = Mathf.Clamp01((bannerMax - bannerTime) * 6f);
                var c = new Color(bannerColor.r, bannerColor.g, bannerColor.b, a);
                if (bannerTitle != null)
                {
                    float px = 11f + (1f - slide) * 4f;
                    UiKit.Pixel(bannerTitle, w * 0.5f, 250 - px * 3.5f, px, c, 0.5f, 0.2f);
                    if (bannerSub != null) UiKit.Pixel(bannerSub, w * 0.5f, 320, 3.6f, new Color(1f, 1f, 1f, a), 0.5f);
                }
                else if (bannerSub != null) UiKit.Pixel(bannerSub, w * 0.5f, 150, 4f, c, 0.5f);
            }

            // One line at the bottom: the roll guide when the gun is wrong, otherwise a first-run hint.
            var plan = Guide.Plan;
            if (plan != null) DrawRollGuide(w, plan);
            else
            {
                string hint = TutorialHint();
                if (hint != null)
                {
                    float hw = UiKit.PixelWidth(hint, 3.2f) + 60f;
                    var hr = new Rect(w * 0.5f - hw * 0.5f, UiKit.H - 130, hw, 52);
                    UiKit.Panel(hr, 0.72f);
                    UiKit.Pixel(hint, w * 0.5f, hr.y + 15, 3.2f, UiKit.Text, 0.5f);
                }
            }
        }

        /// <summary>Everspace-style ability row: ROLL [arrow tile] FOR [gun tile] GUN NAME, with key prompts under the tiles.</summary>
        void DrawRollGuide(float w, RollPlan plan)
        {
            var gun = plan.Gun;
            const float tile = 66f, gap = 16f;
            float lw = UiKit.TextWidth("ROLL", 28), fw = UiKit.TextWidth("FOR", 28), nw = UiKit.TextWidth(gun.name, 30);
            float total = lw + gap + tile + gap + fw + gap + tile + gap + nw;
            float x = w * 0.5f - total * 0.5f, ty = UiKit.H - 150f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(uiTime * 6f);
            UiKit.Glow(new Rect(x - 80f, ty - 50f, total + 160f, tile + 140f), new Color(gun.color.r, gun.color.g, gun.color.b, 0.08f + 0.08f * pulse));

            UiKit.Line("ROLL", x, ty + 22, 28, UiKit.Text, 0f, 2, 2f);
            x += lw + gap;
            var at = new Rect(x, ty, tile, tile);
            UiKit.Rect(at, new Color(0.08f, 0.16f, 0.18f, 0.92f));
            UiKit.Rect(new Rect(at.x, at.y, at.width, 2), new Color(0.92f, 0.95f, 0.95f, 0.45f));
            UiKit.DrawIcon(new Rect(at.x + 13, at.y + 13, 40, 40), "ui_arrow", UiKit.Text, ArrowAngle(plan.dir));
            float kw = UiKit.TextWidth(KeyFor(plan.dir), 18) + 12f;
            UiKit.Keycap(at.center.x - Mathf.Max(30f, kw) * 0.5f, at.yMax + 8, KeyFor(plan.dir));
            x += tile + gap;

            UiKit.Line("FOR", x, ty + 22, 28, UiKit.Text, 0f, 2, 2f);
            x += fw + gap;
            var gt = new Rect(x, ty, tile, tile);
            UiKit.Rect(gt, new Color(gun.color.r * 0.35f, gun.color.g * 0.35f, gun.color.b * 0.35f, 0.95f));
            UiKit.Rect(new Rect(gt.x, gt.y, gt.width, 2), gun.color);
            UiKit.DrawIcon(new Rect(gt.x + 13, gt.y + 13, 40, 40), "ui_gun" + gun.number, gun.color);
            UiKit.Keycap(gt.center.x - 15f, gt.yMax + 8, plan.top.ToString());
            x += tile + gap;

            UiKit.Line(gun.name, x, ty + 21, 30, gun.color, 0f, 2, 2f);

            if (ToCanvas(plan.obstacle.transform.position, out var p) && OffCanvas(p, w))
                EdgeArrow(w, p, gun.color, 1f);
        }

        // World +Z is screen up (the camera looks down the arena's +Z axis).
        static float ArrowAngle(Vector3 d) => d.z > 0.5f ? -90f : d.z < -0.5f ? 90f : d.x > 0f ? 0f : 180f;
        static string KeyFor(Vector3 d) => d.z > 0.5f ? "↑" : d.z < -0.5f ? "↓" : d.x > 0f ? "→" : "←";
        static string Title(string caps) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(caps.ToLowerInvariant());

        string TutorialHint()
        {
            if (!Settings.ShowTutorial || Game.Wave > 3 || Game.Lost) return null;
            if (Game.Rolls == 0 && Game.TimeAlive < 5f) return "GLIDE WITH WASD, ARROWS OR THE LEFT STICK";
            if (Game.Rolls == 0) return "SLAM INTO A RED BARRIER TO ROLL: THE TOP NUMBER PICKS YOUR GUN";
            if (Game.Bombs.All.Count > 0 && Game.BombsDisposed == 0) return "SHOVE BOMBS OFF THE EDGE";
            if (Game.Rolls < 3 && Game.TimeAlive < 40f) return "SPACE OR A TO DASH";
            return null;
        }

        /// <summary>A countdown over bombs about to blow, and an edge arrow for bombs off screen.</summary>
        void DrawBombAlerts(float w)
        {
            foreach (var b in Game.Bombs.All)
            {
                if (b.falling || !ToCanvas(b.pos + Vector3.up * 1.35f, out var p)) continue;
                float k = b.drop > 0f ? 1f : b.fuse / b.maxFuse;
                Color c = k > 0.6f ? Palette.Hex("#3DFF7A") : k > 0.3f ? Palette.Hex("#FFD23D") : UiKit.Red;
                if (!OffCanvas(p, w))
                {
                    if (b.drop <= 0f && b.fuse < 3f) UiKit.Pixel(Mathf.CeilToInt(b.fuse).ToString(), p.x, p.y - 10f, 4f, c, 0.5f);
                }
                else EdgeArrow(w, p, c, k < 0.3f ? (Mathf.Repeat(uiTime * 6f, 1f) > 0.5f ? 1f : 0.35f) : 1f);
            }
        }

        /// <summary>World point → virtual canvas position (false when behind the camera).</summary>
        bool ToCanvas(Vector3 world, out Vector2 p)
        {
            p = default;
            var camera = Camera.main;
            if (camera == null) return false;
            Vector3 s = camera.WorldToScreenPoint(world);
            if (s.z < 0f) return false;
            float scale = Screen.height / UiKit.H;
            p = new Vector2(s.x / scale, (Screen.height - s.y) / scale);
            return true;
        }

        static bool OffCanvas(Vector2 p, float w) => p.x < 20f || p.x > w - 20f || p.y < 20f || p.y > UiKit.H - 20f;

        void EdgeArrow(float w, Vector2 p, Color c, float alpha)
        {
            Vector2 centre = new Vector2(w * 0.5f, UiKit.H * 0.5f);
            Vector2 d = (p - centre).normalized;
            float t = Mathf.Min((w * 0.5f - 60f) / Mathf.Max(0.001f, Mathf.Abs(d.x)), (UiKit.H * 0.5f - 60f) / Mathf.Max(0.001f, Mathf.Abs(d.y)));
            Vector2 e = centre + d * t;
            UiKit.Glow(new Rect(e.x - 34f, e.y - 34f, 68f, 68f), new Color(c.r, c.g, c.b, 0.45f * alpha));
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, e * (Screen.height / UiKit.H));
            UiKit.Pixel("→", e.x, e.y - 10.5f, 3f, new Color(c.r, c.g, c.b, alpha), 0.5f);
            GUI.matrix = m;
        }

        // ---------------- Screens ----------------

        void DrawLogo(float w, float y, float px)
        {
            // Two stacked words in chunky gold pixels, with a die on each side.
            float bob = Mathf.Sin(uiTime * 2f) * 4f;
            UiKit.Glow(new Rect(w * 0.5f - 520f, y - 120f, 1040f, 520f), new Color(1f, 0.75f, 0.2f, 0.12f));
            UiKit.Pixel("ROLL", w * 0.5f, y + bob, px, UiKit.Gold, 0.5f, 0.25f, UiKit.GoldDeep);
            UiKit.Pixel("POWER", w * 0.5f, y + px * 8.5f + bob, px, UiKit.Gold, 0.5f, 0.25f, UiKit.GoldDeep);
            float pw = UiKit.PixelWidth("POWER", px);
            int face = 1 + (int)(uiTime * 1.5f) % 6;
            float die = px * 5.2f;
            UiKit.DieFace(new Rect(w * 0.5f - pw * 0.5f - die - px * 3f, y + px * 1.5f - bob, die, die), face, UiKit.Gold, UiKit.Ink);
            UiKit.DieFace(new Rect(w * 0.5f + pw * 0.5f + px * 3f, y + px * 9f - bob, die, die), 7 - face, UiKit.Gold, UiKit.Ink);
            // "2.0" badge
            var badge = new Rect(w * 0.5f + pw * 0.5f - 70f, y + px * 16f + 6f, 130f, 50f);
            UiKit.Panel(badge, 1f, UiKit.Red);
            UiKit.Rect(new Rect(badge.x + 3, badge.y + 3, badge.width - 6, badge.height - 6), new Color(UiKit.Red.r, UiKit.Red.g, UiKit.Red.b, 0.85f));
            UiKit.Pixel("2.0", badge.center.x, badge.y + 11f, 4f, Color.white, 0.5f);
        }

        void DrawTitle(float w)
        {
            UiKit.Rect(new Rect(0, 0, w, UiKit.H), new Color(0.02f, 0.03f, 0.06f, CoverMode ? 0.15f : 0.35f));
            if (CoverMode)
            {
                // Key art for the itch.io cover: logo centred, no menu.
                DrawLogo(w, 250f, 23f);
                UiKit.Pixel("ROLLING IS YOUR SUPERPOWER", w * 0.5f, 700f, 5f, UiKit.Text, 0.5f);
                return;
            }
            DrawLogo(w, 110f, 21f);
            UiKit.Pixel("ROLLING IS YOUR SUPERPOWER", w * 0.5f, 520f, 4f, UiKit.Text, 0.5f);
            int clicked = titleMenu.Draw(w * 0.5f, 600f, 520f, 76f);
            Activate(Screen2.Title, clicked);

            if (Settings.BestScore > 0)
                UiKit.Pixel($"BEST {Num(Settings.BestScore)}   WAVE {Settings.BestWave}", w * 0.5f, UiKit.H - 110f, 3.2f, UiKit.Gold, 0.5f);
            UiKit.Label(new Rect(0, UiKit.H - 62, w, 30), "A GMTK Game Jam 2022 game, rebuilt  •  by ZizmanTK  •  mouse, keyboard or gamepad", UiKit.SmallCenter, UiKit.Muted);
        }

        void DrawPause(float w)
        {
            UiKit.Pixel("PAUSED", w * 0.5f, 170f, 14f, UiKit.Gold, 0.5f, 0.25f, UiKit.GoldDeep);
            Activate(Screen2.Paused, pauseMenu.Draw(w * 0.5f, 330f, 520f, 76f));

            // Owned upgrades
            var owned = new System.Collections.Generic.List<(UpgradeDef def, int level)>(Deck.Owned());
            if (owned.Count > 0)
            {
                var r = new Rect(w * 0.5f + 320f, 330f, 380f, 60f + owned.Count * 40f);
                UiKit.Panel(r, 0.8f);
                UiKit.Pixel("UPGRADES", r.x + 20, r.y + 18, 3f, UiKit.Muted);
                for (int i = 0; i < owned.Count; i++)
                {
                    var (d, l) = owned[i];
                    UiKit.Rect(new Rect(r.x + 20, r.y + 56 + i * 40, 6, 26), d.color);
                    UiKit.Pixel(d.name, r.x + 38, r.y + 62 + i * 40, 2.6f, d.color);
                    UiKit.Pixel($"{l}/{d.maxLevel}", r.xMax - 20, r.y + 62 + i * 40, 2.6f, UiKit.Muted, 1f);
                }
            }
        }

        void DrawSettings(float w)
        {
            UiKit.Pixel("SETTINGS", w * 0.5f, 170f, 12f, UiKit.Gold, 0.5f, 0.25f, UiKit.GoldDeep);
            Activate(Screen2.Settings, settingsMenu.Draw(w * 0.5f, 320f, 760f, 80f));
            UiKit.Label(new Rect(0, UiKit.H - 80, w, 30), "left / right to change   •   ESC / B to go back", UiKit.SmallCenter, UiKit.Muted);
        }

        void DrawHowTo(float w)
        {
            float pw = Mathf.Min(1500f, w - 80f);
            var r = new Rect(w * 0.5f - pw * 0.5f, 70f, pw, UiKit.H - 170f);
            UiKit.Panel(r, 0.92f, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.4f));
            UiKit.Pixel("HOW TO PLAY", w * 0.5f, r.y + 34f, 8f, UiKit.Gold, 0.5f);

            float x = r.x + 50f, y = r.y + 130f, col = pw * 0.5f - 60f;
            (string head, string body, Color c)[] rules =
            {
                ("GLIDE", "WASD, arrows or left stick. You slide like on ice, so plan your turns.", UiKit.Text),
                ("ROLL", "Slam into a <b>red barrier</b> to roll once, or a <b>blue conduit</b> to roll twice. The number on top picks your gun. When your gun can't hurt what's on the field, a glowing arrow marks the barrier to slam.", UiKit.Red),
                ("SHOOT", "Guns aim and fire on their own. A fresh roll overcharges the gun: double fire rate for a few seconds.", UiKit.Gold),
                ("BOMBS", "Barrel bombs light up green, yellow, then red. Shove them off the edge of the platform for points. A blast hurts everything nearby, enemies included.", UiKit.Red),
                ("DASH", "SPACE, SHIFT or A. Dash into barriers for a sure roll, or into bombs for a big shove.", UiKit.Mint),
            };
            foreach (var rule in rules)
            {
                UiKit.Pixel(rule.head, x, y + 4f, 3.4f, rule.c);
                UiKit.Label(new Rect(x + 130f, y - 4f, col - 130f, 90f), rule.body, UiKit.Small, UiKit.Text);
                y += 104f;
            }

            float x2 = r.x + pw * 0.5f + 20f, y2 = r.y + 130f;
            UiKit.Pixel("SIX FACES, SIX GUNS", x2, y2, 3.4f, UiKit.Muted);
            for (int n = 1; n <= 6; n++)
            {
                var d = WeaponDef.All[n];
                float yy = y2 + 40f + (n - 1) * 64f;
                UiKit.DieFace(new Rect(x2, yy, 42, 42), n, d.color, UiKit.Ink);
                UiKit.Pixel(d.name, x2 + 60f, yy, 3f, d.color);
                UiKit.Label(new Rect(x2 + 60f, yy + 26f, col, 26f), d.role, UiKit.Tiny, UiKit.Text);
            }
            float y3 = y2 + 40f + 6f * 64f + 12f;
            UiKit.Pixel("KNOW YOUR ENEMY", x2, y3, 3.4f, UiKit.Muted);
            UiKit.Label(new Rect(x2, y3 + 30f, col, 150f),
                "<b>Drones</b> fly: only 3 or 6 reach them.   <b>Tanks</b> are armoured: use 1 or 4.\n<b>Bombers</b> plant bombs.   Every 5th wave, the <b>High Roller</b> tumbles in: only the gun that matches its top number can hurt it. Slams and bombs hurt everything.",
                UiKit.Small, UiKit.Text);

            UiKit.Label(new Rect(0, r.yMax - 50f, w, 30), "press ENTER, ESC, A or B to go back", UiKit.SmallCenter, UiKit.Muted);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0) { State = howToReturn; Event.current.Use(); }
        }

        // Upgrade categories and their colours (style 7).
        static string Category(UpgradeDef u)
        {
            switch (u.id)
            {
                case "dmg": case "rate": case "over": case "barrel": return "GUNS";
                case "slam": case "dash": case "loaded": return "ROLLS";
                case "hp": return "DEFENCE";
                default: return "BOMBS";
            }
        }

        static Color CategoryColor(string cat) => cat == "GUNS" ? Palette.Hex("#FF6A3D") : cat == "ROLLS" ? Palette.Hex("#29B6F6")
            : cat == "DEFENCE" ? Palette.Hex("#3BD16F") : Palette.Hex("#FFB020");

        static readonly Color Panel7 = Palette.Hex("#0B1A2A"), Border7 = Palette.Hex("#2F8FC0"), Text7 = Palette.Hex("#B9D2E2"), Muted7 = Palette.Hex("#8FB4CC");

        void DrawUpgrade(float w)
        {
            if (hand == null || hand.Count == 0) return;

            // Title plate.
            string sub = Game.BossWave ? "HIGH ROLLER DOWN" : $"WAVE {Game.Wave} CLEARED";
            float tw = Mathf.Max(UiKit.TextWidth("PICK ONE UPGRADE", 46) + 100f, 520f);
            var tr = new Rect(w * 0.5f - tw * 0.5f, 84f, tw, 104f);
            UiKit.ChamferPanel(tr, new Color(Panel7.r, Panel7.g, Panel7.b, 0.94f), Border7);
            UiKit.Line(sub, w * 0.5f, tr.y + 18, 20, Palette.Hex("#3BD16F"), 0.5f, 2);
            UiKit.Line("PICK ONE UPGRADE", w * 0.5f, tr.y + 48, 46, UiKit.Text, 0.5f, 2);

            float cw = 400f, ch = 590f, gap = 40f;
            float total = hand.Count * cw + (hand.Count - 1) * gap;
            float x0 = w * 0.5f - total * 0.5f;
            var e = Event.current;
            for (int i = 0; i < hand.Count; i++)
            {
                var u = hand[i];
                bool sel = upgradeMenuSel == i;
                string cat = Category(u);
                Color cc = CategoryColor(cat);
                int lvl = Deck.Level(u);
                float lift = sel ? -10f - Mathf.Sin(uiTime * 4f) * 3f : 0f;
                var r = new Rect(x0 + i * (cw + gap), 236f + lift, cw, ch);
                if (r.Contains(e.mousePosition) && (e.type == EventType.MouseMove || e.type == EventType.Repaint) && upgradeMenuSel != i && e.delta.sqrMagnitude > 0f)
                { upgradeMenuSel = i; Sound.Play(Sfx.UiMove, 0.4f, 0f); }

                if (sel) UiKit.Glow(new Rect(r.x - 50, r.y - 50, r.width + 100, r.height + 100), new Color(HudCyan.r, HudCyan.g, HudCyan.b, 0.16f));
                UiKit.ChamferPanel(r, new Color(Panel7.r, Panel7.g, Panel7.b, 0.97f), sel ? HudCyan : Border7);
                if (sel) UiKit.ChamferPanel(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), new Color(0f, 0f, 0f, 0f), HudCyan);

                float px = r.x + 26f, py = r.y + 26f, inner = cw - 52f;
                UiKit.Line(cat, px, py, 18, cc, 0f, 2);
                string badge = lvl == 0 ? "NEW" : $"LV {lvl} → {lvl + 1}";
                Color bc = lvl == 0 ? Palette.Hex("#3BD16F") : Palette.Hex("#FFB020");
                float bw = UiKit.TextWidth(badge, 16) + 16f;
                var br = new Rect(r.xMax - 26f - bw, py - 4f, bw, 24f);
                UiKit.Rect(new Rect(br.x, br.y, br.width, 1.5f), bc); UiKit.Rect(new Rect(br.x, br.yMax - 1.5f, br.width, 1.5f), bc);
                UiKit.Rect(new Rect(br.x, br.y, 1.5f, br.height), bc); UiKit.Rect(new Rect(br.xMax - 1.5f, br.y, 1.5f, br.height), bc);
                UiKit.Line(badge, br.center.x, br.y + 6f, 16, bc, 0.5f, 2);

                // Icon tile.
                var it = new Rect(px, r.y + 66f, inner, 180f);
                UiKit.Rect(it, new Color(0.03f, 0.08f, 0.14f, 1f));
                UiKit.Glow(new Rect(it.center.x - 140, it.center.y - 110, 280, 220), new Color(cc.r, cc.g, cc.b, 0.28f));
                UiKit.Rect(new Rect(it.x, it.y, it.width, 1), new Color(Border7.r, Border7.g, Border7.b, 0.7f));
                UiKit.Rect(new Rect(it.x, it.yMax - 1, it.width, 1), new Color(Border7.r, Border7.g, Border7.b, 0.7f));
                UiKit.DrawIcon(new Rect(it.center.x - 70, it.center.y - 70, 140, 140), "up_" + u.id, Color.white);

                // Name and description.
                int ns = UiKit.TextWidth(u.name, 34) > inner ? 28 : 34;
                UiKit.Line(u.name, px, r.y + 270f, ns, UiKit.Text, 0f, 2);
                UiKit.Label(new Rect(px, r.y + 316f, inner, 130f), Sentence(u.desc), new GUIStyle(UiKit.TextStyle(23, 0)) { wordWrap = true }, Text7);

                // Level track: owned levels, then the one this card adds.
                float ty = r.yMax - 118f;
                UiKit.Line("LEVEL", px, ty, 16, Muted7, 0f, 2);
                UiKit.Line(u.maxLevel > 1 ? $"{lvl} → {lvl + 1} / {u.maxLevel}" : "UNIQUE", r.xMax - 26f, ty - 2f, 19, UiKit.Text, 1f, 2);
                float segW = (inner - (u.maxLevel - 1) * 4f) / u.maxLevel;
                for (int s = 0; s < u.maxLevel; s++)
                {
                    Color sc = s < lvl ? Border7 : s == lvl ? cc : new Color(0f, 0f, 0f, 0.45f);
                    UiKit.Rect(new Rect(px + s * (segW + 4f), ty + 26f, segW, 12f), sc);
                }
                float kx = UiKit.Keycap(px, r.yMax - 58f, (i + 1).ToString(), 28f);
                UiKit.Line("SELECT", px + kx + 10f, r.yMax - 51f, 18, sel ? HudCyan : Muted7, 0f, 2);

                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    e.Use();
                    PickUpgrade(u);
                    return;
                }
            }
            float hy = UiKit.H - 46f;
            UiKit.Line("Click a card, press 1-3, or use ← → and ENTER / A", w * 0.5f, hy, 19, Muted7, 0.5f, 1);
        }

        /// <summary>"+25% weapon damage" → "+25% weapon damage." (descriptions are stored as short phrases).</summary>
        static string Sentence(string s) => s.Length > 0 && !s.EndsWith(".") ? char.ToUpperInvariant(s[0]) + s.Substring(1) + "." : s;

        void DrawGameOver(float w)
        {
            UiKit.Pixel("DICE DESTROYED", w * 0.5f, 110f, 11f, UiKit.Red, 0.5f, 0.25f, new Color(0.3f, 0f, 0.05f));
            UiKit.Pixel("SCORE", w * 0.5f, 225f, 3.4f, UiKit.Muted, 0.5f);
            UiKit.Pixel(Num(Game.Score), w * 0.5f, 258f, 11f, UiKit.Gold, 0.5f, 0.25f, UiKit.GoldDeep);
            if (newBest && Mathf.Repeat(uiTime * 2f, 1f) > 0.25f) UiKit.Pixel("NEW BEST!", w * 0.5f, 350f, 4.5f, UiKit.Mint, 0.5f);
            else if (!newBest) UiKit.Pixel($"BEST {Num(Settings.BestScore)}", w * 0.5f, 350f, 3.4f, UiKit.Muted, 0.5f);

            var r = new Rect(w * 0.5f - 380f, 410f, 760f, 200f);
            UiKit.Panel(r, 0.85f);
            int m = (int)Game.TimeAlive / 60, s = (int)Game.TimeAlive % 60;
            (string k, string v)[] stats =
            {
                ("WAVE", Game.Wave.ToString()), ("KILLS", Game.Kills.ToString()), ("BOMBS DISPOSED", Game.BombsDisposed.ToString()),
                ("ROLLS", Game.Rolls.ToString()), ("BEST COMBO", "X" + Game.BestCombo), ("TIME", $"{m}:{s:00}"),
            };
            for (int i = 0; i < stats.Length; i++)
            {
                float cx = r.x + 40f + (i % 2) * 370f, cy = r.y + 30f + (i / 2) * 54f;
                UiKit.Pixel(stats[i].k, cx, cy, 3f, UiKit.Muted);
                UiKit.Pixel(stats[i].v, cx + 330f, cy - 3f, 4f, UiKit.Text, 1f);
            }
            Activate(Screen2.GameOver, gameOverMenu.Draw(w * 0.5f, 650f, 520f, 76f));
            UiKit.Label(new Rect(0, UiKit.H - 70, w, 30), "R or Y to restart instantly", UiKit.SmallCenter, UiKit.Muted);
        }
    }
}
