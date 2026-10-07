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
            titleMenu = new Menu().Add("CAMPAIGN", "play").Add("GAUNTLET", "restart").Add("HOW TO PLAY", "help").Add("SETTINGS", "settings");
            if (CanQuit) titleMenu.Add("QUIT", "quit");
            pauseMenu = new Menu().Add("RESUME", "resume").Add("SETTINGS", "settings").Add("HOW TO PLAY", "help").Add("RESTART", "restart").Add("MAIN MENU", "home");
            gameOverMenu = new Menu().Add("PLAY AGAIN", "restart").Add("BUILD YOUR DIE", "help").Add("MAIN MENU", "home");
            settingsMenu = new Menu()
                .AddSlider("MUSIC", () => Settings.Music, v => Settings.Music = v)
                .AddSlider("SOUND FX", () => Settings.Sfx, v => Settings.Sfx = v)
                .AddChoice("SCREEN SHAKE", () => Settings.ShakeAmount <= 0f ? "OFF" : Settings.ShakeAmount < 0.9f ? "LOW" : "FULL",
                    d => { float[] o = { 0f, 0.5f, 1f }; int i = Settings.ShakeAmount <= 0f ? 0 : Settings.ShakeAmount < 0.9f ? 1 : 2; Settings.ShakeAmount = o[(i + d + 3) % 3]; if (cam != null) cam.Shake(0.4f); })
                .AddChoice("FULLSCREEN", () => Screen.fullScreen ? "ON" : "OFF", d => Screen.fullScreen = !Screen.fullScreen)
                .AddChoice("ROLL", () => Settings.RollButton ? "BUTTON" : "BUMP (2.0)", d => Settings.RollButton = !Settings.RollButton)
                .AddChoice("HINTS", () => Settings.ShowTutorial ? "ON" : "OFF", d => Settings.ShowTutorial = !Settings.ShowTutorial)
                .AddChoice("ASSIST (CAMPAIGN)", () => Settings.Assist ? "ON: 85% SPEED, +2 HULL" : "OFF", d => Settings.Assist = !Settings.Assist)
                .AddChoice("PLAY LOG", () => Settings.Telemetry ? "ON (LOCAL)" : "OFF", d => Settings.Telemetry = !Settings.Telemetry)
                .Add("BACK", "back");
            if (Application.platform != RuntimePlatform.WebGLPlayer) settingsMenu.Items.Insert(settingsMenu.Items.Count - 1, new Menu.Item { label = "OPEN PLAY LOG FOLDER", icon = "home" });
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
                case Screen2.Loadout:
                    LoadoutInput(udt);
                    break;
                case Screen2.Campaign:
                case Screen2.Story:
                case Screen2.StageClear:
                case Screen2.StageFailed:
                    CampaignInput(udt);
                    break;
                case Screen2.Workshop:
                    WorkshopInput(udt);
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
                    if (index == 0) OpenCampaign();
                    else if (index == 1) EnterLoadout(); // gauntlet: the endless mode, with your die build
                    else if (index == 2) { howToReturn = Screen2.Title; State = Screen2.HowTo; }
                    else if (index == 3) { settingsReturn = Screen2.Title; settingsMenu.Selected = 0; State = Screen2.Settings; }
                    else if (index == 4) Application.Quit();
                    break;
                case Screen2.Paused:
                    if (index == 0) SetPaused(false);
                    else if (index == 1) { settingsReturn = Screen2.Paused; settingsMenu.Selected = 0; State = Screen2.Settings; }
                    else if (index == 2) { howToReturn = Screen2.Paused; State = Screen2.HowTo; }
                    else if (index == 3) Reload(true);
                    else Reload(false);
                    break;
                case Screen2.Settings:
                    if (settingsMenu.Items[index].label == "OPEN PLAY LOG FOLDER") { System.IO.Directory.CreateDirectory(Telemetry.Folder); Application.OpenURL("file:///" + Telemetry.Folder.Replace('\\', '/')); }
                    else if (index == settingsMenu.Items.Count - 1) { State = settingsReturn; Settings.Save(); }
                    break;
                case Screen2.GameOver:
                    if (index == 1) OpenLoadout = true;
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
                case Screen2.Loadout: Dim(w, 0.55f); DrawLoadout(w); break;
                case Screen2.Campaign: Dim(w, 0.6f); DrawCampaign(w); break;
                case Screen2.Workshop: Dim(w, 0.6f); DrawWorkshop(w); break;
                case Screen2.Story: DrawStory(w); break;
                case Screen2.StageClear: Dim(w, 0.72f); DrawStageClear(w); break;
                case Screen2.StageFailed: Dim(w, 0.62f); DrawStageFailed(w); break;
                case Screen2.Playing: DrawTutorial(w); DrawRadio(w); break;
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
            UiKit.DrawIcon(new Rect(em.x - 17, em.y - 17, 34, 34), GunIcon(def), def.color);

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
            UiKit.Line(HudWaveLabel, 60, ly + 10, 22, UiKit.Text, 0f, 2, 1.5f);
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
                // Phase marks at two thirds and one third.
                foreach (float f in new[] { 1f / 3f, 2f / 3f }) UiKit.Rect(new Rect(bossX + bw * f - 1.5f, 58, 3, 18), UiKit.Text);
                var bs = boss.boss;
                // What it wears this phase (or next, while it rerolls) and the gun that gets through.
                var bd = boss.PlannedDefence;
                int cf = Enemy.CounterFace(bd);
                var wd = cf > 0 ? WeaponDef.All[cf] : def;
                bool match = !bs.Rerolling && boss.CanBeHitBy(def);
                var dr = new Rect(bossX + bw + 18, 34, 52, 52);
                Color tint = CoatModels.Tint(bd);
                if (bs.Rerolling) UiKit.Glow(new Rect(dr.x - 30, dr.y - 30, 112, 112), new Color(tint.r, tint.g, tint.b, 0.3f + 0.2f * Mathf.Sin(uiTime * 12f)));
                UiKit.ChamferPanel(dr, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.9f), tint);
                if (cf > 0) UiKit.DrawIcon(new Rect(dr.x + 10, dr.y + 10, 32, 32), GunIcon(wd), wd.color);
                if (bs.Rerolling)
                {
                    UiKit.Line("NEXT: " + Enemy.DefenceName(bd), dr.xMax + 12, 38, 15, HudMuted, 0f, 2);
                    UiKit.Line($"{bs.reroll:0.0}s", dr.xMax + 12, 56, 22, UiKit.Text, 0f, 2);
                }
                else
                {
                    UiKit.Line(match ? "HITTING" : Enemy.DefenceName(bd), dr.xMax + 12, 38, 15, match ? HudMint : tint, 0f, 2);
                    UiKit.Line(bd == Defence.Bare ? "ANY GUN" : wd.name, dr.xMax + 12, 56, 20, wd.color, 0f, 2);
                }
                UiKit.Line($"PHASE {bs.phase + 1}/3", bossX, 82, 15, HudMuted, 0f, 2);
            }

            // Foreman (deck boss): name, bar and what to do right now.
            var fm = Game.Foreman;
            if (fm != null && fm.Alive)
            {
                float bw = Mathf.Min(600f, w - 1100f);
                float bossX = w * 0.5f - bw * 0.5f;
                UiKit.Line("FOREMAN · " + Enemy.Plural(fm.kind), w * 0.5f, 28, 24, UiKit.Text, 0.5f, 2, 2f);
                UiKit.Rect(new Rect(bossX - 2, 60, bw + 4, 14), new Color(0f, 0f, 0f, 0.6f));
                UiKit.Rect(new Rect(bossX, 62, bw * Mathf.Clamp01(fm.BossFraction), 10), HudHull);
                UiKit.Rect(new Rect(bossX + bw * 0.5f - 1.5f, 58, 3, 18), UiKit.Text);
                bool open = fm.CanBeHitBy(def);
                UiKit.Line(fm.ForemanState(), w * 0.5f, 82, 17, open ? HudMint : fm.kind == EnemyKind.Compactor && fm.mode >= 1 ? UiKit.Red : HudMuted, 0.5f, 2, 1.5f);
            }

            DrawEnemyBars(def);
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
                    UiKit.Pixel(bannerTitle, w * 0.5f, 190 - px * 3.5f, px, c, 0.5f, 0.2f);
                    if (bannerSub != null) UiKit.Pixel(bannerSub, w * 0.5f, 262, 3.6f, new Color(1f, 1f, 1f, a), 0.5f);
                }
                else if (bannerSub != null) UiKit.Pixel(bannerSub, w * 0.5f, 150, 4f, c, 0.5f);
            }

            // One line at the bottom: the roll guide when the gun is wrong, otherwise a first-run hint.
            var plan = Advice;
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
            string rollWord = plan.steps == 2 ? "ROLL TWICE" : "ROLL";
            float lw = UiKit.TextWidth(rollWord, 28), fw = UiKit.TextWidth("FOR", 28), nw = UiKit.TextWidth(gun.name, 30);
            float total = lw + gap + tile + gap + fw + gap + tile + gap + nw;
            float x = w * 0.5f - total * 0.5f, ty = UiKit.H - 150f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(uiTime * 6f);
            UiKit.Glow(new Rect(x - 80f, ty - 50f, total + 160f, tile + 140f), new Color(gun.color.r, gun.color.g, gun.color.b, 0.08f + 0.08f * pulse));

            UiKit.Line(rollWord, x, ty + 22, 28, UiKit.Text, 0f, 2, 2f);
            x += lw + gap;
            var at = new Rect(x, ty, tile, tile);
            UiKit.Rect(at, new Color(0.08f, 0.16f, 0.18f, 0.92f));
            UiKit.Rect(new Rect(at.x, at.y, at.width, 2), new Color(0.92f, 0.95f, 0.95f, 0.45f));
            UiKit.DrawIcon(new Rect(at.x + 13, at.y + 13, 40, 40), "ui_arrow", UiKit.Text, ArrowAngle(plan.dir));
            // Button mode: hold the direction and press roll.
            string key = plan.obstacle == null ? KeyFor(plan.dir) + " + SPACE" : KeyFor(plan.dir);
            float kw = UiKit.TextWidth(key, 18) + 12f;
            UiKit.Keycap(at.center.x - Mathf.Max(30f, kw) * 0.5f, at.yMax + 8, key);
            x += tile + gap;

            UiKit.Line("FOR", x, ty + 22, 28, UiKit.Text, 0f, 2, 2f);
            x += fw + gap;
            var gt = new Rect(x, ty, tile, tile);
            UiKit.Rect(gt, new Color(gun.color.r * 0.35f, gun.color.g * 0.35f, gun.color.b * 0.35f, 0.95f));
            UiKit.Rect(new Rect(gt.x, gt.y, gt.width, 2), gun.color);
            UiKit.DrawIcon(new Rect(gt.x + 13, gt.y + 13, 40, 40), GunIcon(gun), gun.color);
            UiKit.Keycap(gt.center.x - 15f, gt.yMax + 8, plan.top.ToString());
            x += tile + gap;

            UiKit.Line(gun.name, x, ty + 21, 30, gun.color, 0f, 2, 2f);

            if (plan.obstacle != null && ToCanvas(plan.obstacle.transform.position, out var p) && OffCanvas(p, w))
                EdgeArrow(w, p, gun.color, 1f);
        }

        // World +Z is screen up (the camera looks down the arena's +Z axis).
        static float ArrowAngle(Vector3 d) => d.z > 0.5f ? -90f : d.z < -0.5f ? 90f : d.x > 0f ? 0f : 180f;
        static string KeyFor(Vector3 d) => d.z > 0.5f ? "↑" : d.z < -0.5f ? "↓" : d.x > 0f ? "→" : "←";
        static string Title(string caps) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(caps.ToLowerInvariant());

        string TutorialHint()
        {
            if (!Settings.ShowTutorial || tutorial != null || Game.Wave > 3 || Game.Lost) return null;
            if (DiceController.ButtonMode)
            {
                if (Game.Rolls == 0 && Game.TimeAlive < 4f) return "MOVE WITH WASD, ARROWS OR THE LEFT STICK";
                if (Game.Rolls == 0) return "SPACE ROLLS THE DIE THE WAY YOU MOVE: IT DODGES AND CHANGES YOUR GUN";
                if (Game.Rolls < 4 && Game.TimeAlive < 30f) return "THE MARKERS AROUND THE DIE SHOW THE GUN EACH ROLL GIVES";
                if (Game.Bombs.All.Count > 0 && Game.BombsDisposed == 0) return "SHOVE BOMBS OFF THE EDGE";
                if (Game.Rolls < 12 && Game.TimeAlive < 70f) return "ROLL OVER A PIPE RACK TO VAULT IT AND LAND ON THE OPPOSITE FACE";
                return null;
            }
            if (Game.Rolls == 0 && Game.TimeAlive < 5f) return "GLIDE WITH WASD, ARROWS OR THE LEFT STICK";
            if (Game.Rolls == 0) return Art.Theme == 3 ? "SLAM INTO A VENT BOX OR PIPE RACK TO ROLL: THE TOP NUMBER PICKS YOUR GUN" : "SLAM INTO A RED BARRIER TO ROLL: THE TOP NUMBER PICKS YOUR GUN";
            if (Game.Bombs.All.Count > 0 && Game.BombsDisposed == 0) return "SHOVE BOMBS OFF THE EDGE";
            if (Game.Rolls < 3 && Game.TimeAlive < 40f) return "SPACE OR A TO DASH";
            return null;
        }

        /// <summary>
        /// A small health bar over every enemy. It greys out while the gun on top can't hurt that enemy, and the icon of
        /// a gun that can (in its colour) appears beside it. A coat (vines, ice, shield) shows as a second bar in its colour.
        /// Mites close to Pip get a ROLL tag: only a landing crushes them.
        /// </summary>
        void DrawEnemyBars(WeaponDef def)
        {
            if (Game.I == null) return;
            foreach (var e in Game.I.Enemies)
            {
                if (!e.Alive || e.IsBig) continue;
                if (e.Low)
                {
                    Vector3 dd = e.pos - Dice.transform.position; dd.y = 0f;
                    if (dd.magnitude < 5f && ToCanvas(e.t.position + Vector3.up * 0.7f, out var mp)) UiKit.Line("ROLL", mp.x, mp.y - 8f, 13, UiKit.Mint, 0.5f, 2);
                    continue;
                }
                if (!ToCanvas(e.t.position + Vector3.up * (e.radius * 2f + 0.55f), out var p)) continue;
                float bw = Mathf.Clamp(e.radius * 70f, 34f, 64f);
                bool open = e.CanBeHitBy(def);
                var r = new Rect(p.x - bw * 0.5f, p.y, bw, 6f);
                UiKit.Rect(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), new Color(0f, 0f, 0f, 0.55f));
                UiKit.Bar(r, e.hp / e.maxHp, open ? HudHull : new Color(0.55f, 0.6f, 0.62f, 0.9f), new Color(1f, 1f, 1f, 0.12f));
                if (e.Coated) UiKit.Bar(new Rect(r.x, r.y - 6f, r.width, 4f), e.coatHp / e.coatMax, CoatModels.Tint(e.coat), new Color(0f, 0f, 0f, 0.4f));
                if (!open)
                {
                    int cf = Enemy.CounterFace(e.CurrentDefence);
                    if (cf > 0)
                    {
                        var g = WeaponDef.All[cf];
                        var ir = new Rect(r.x - 24f, r.y - 9f, 20f, 20f);
                        UiKit.Rect(new Rect(ir.x - 2f, ir.y - 2f, ir.width + 4f, ir.height + 4f), new Color(0f, 0f, 0f, 0.55f));
                        UiKit.DrawIcon(ir, GunIcon(g), g.color);
                    }
                }
            }
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

        // ---------------- Menu screens (style 7: Riftbreaker panels, like the upgrade pick) ----------------

        /// <summary>Title plate: small coloured line over a big title, in a cut-corner panel.</summary>
        static Rect Plate(float w, float y, string sub, Color subColor, string title, int size = 46)
        {
            float tw = Mathf.Max(UiKit.TextWidth(title, size) + 110f, UiKit.TextWidth(sub, 20) + 110f, 460f);
            var r = new Rect(w * 0.5f - tw * 0.5f, y, tw, size + 62f);
            UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.95f), UiKit.Steel);
            UiKit.Line(sub, w * 0.5f, r.y + 18f, 20, subColor, 0.5f, 2);
            UiKit.Line(title, w * 0.5f, r.y + 46f, size, UiKit.Text, 0.5f, 2);
            return r;
        }

        void DrawLogo(float w, float y, float scale)
        {
            float bob = Mathf.Sin(uiTime * 2f) * 3f;
            int size = Mathf.RoundToInt(150f * scale);
            UiKit.Glow(new Rect(w * 0.5f - 560f * scale, y - 120f * scale, 1120f * scale, 480f * scale), new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, 0.10f));
            UiKit.Line("ROLL POWER", w * 0.5f, y + bob, size, UiKit.Text, 0.5f, 2, 4f);
            float lw = UiKit.TextWidth("ROLL POWER", size);
            int face = 1 + (int)(uiTime * 1.5f) % 6;
            float die = size * 0.62f;
            var amber = Palette.Hex("#FFB020");
            UiKit.DieFace(new Rect(w * 0.5f - lw * 0.5f - die - 30f * scale, y + size * 0.08f - bob, die, die), face, amber, UiKit.Ink);
            UiKit.DieFace(new Rect(w * 0.5f + lw * 0.5f + 30f * scale, y + size * 0.08f + bob, die, die), 7 - face, amber, UiKit.Ink);
            // Version tag and tagline under the wordmark.
            float ty = y + size * 0.9f;
            int ts = Mathf.RoundToInt(26f * scale);
            float tlw = UiKit.TextWidth("ROLLING IS YOUR SUPERPOWER", ts), tagW = 84f * scale, gapW = 18f * scale;
            float tx = w * 0.5f - (tlw + gapW + tagW) * 0.5f;
            UiKit.Line("ROLLING IS YOUR SUPERPOWER", tx, ty + 9f * scale, ts, UiKit.Cyan, 0f, 2, 2f);
            var tag = new Rect(tx + tlw + gapW, ty, tagW, 40f * scale);
            UiKit.ChamferPanel(tag, UiKit.Cyan, UiKit.Cyan);
            UiKit.Line("2.0", tag.center.x, tag.y + 9f * scale, ts, UiKit.Ink, 0.5f, 2);
        }

        void DrawTitle(float w)
        {
            UiKit.Rect(new Rect(0, 0, w, UiKit.H), new Color(0.02f, 0.05f, 0.09f, CoverMode ? 0.15f : 0.4f));
            if (CoverMode)
            {
                DrawLogo(w, 330f, 1.3f); // key art for the itch.io cover: logo only
                return;
            }
            DrawLogo(w, 150f, 1f);
            int clicked = titleMenu.Draw(w * 0.5f, 470f, 540f, 80f);
            Activate(Screen2.Title, clicked);

            if (Settings.BestScore > 0)
            {
                var br = new Rect(w * 0.5f - 270f, 470f + titleMenu.Items.Count * 80f + 16f, 540f, 54f);
                UiKit.ChamferPanel(br, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.8f), new Color(UiKit.Steel.r, UiKit.Steel.g, UiKit.Steel.b, 0.6f));
                UiKit.DrawIcon(new Rect(br.x + 24f, br.center.y - 11f, 22f, 22f), "ui_star", Palette.Hex("#FFB020"));
                UiKit.Line("BEST", br.x + 58f, br.center.y - 7f, 18, UiKit.Mutedish, 0f, 2);
                UiKit.Line(Num(Settings.BestScore), br.x + 108f, br.center.y - 10f, 24, Palette.Hex("#FFB020"), 0f, 2);
                UiKit.Line($"WAVE {Settings.BestWave}", br.xMax - 24f, br.center.y - 10f, 24, UiKit.Text, 1f, 2);
            }
            UiKit.Line("A GMTK Game Jam 2022 game, rebuilt  ·  by ZizmanTK  ·  mouse, keyboard or gamepad", w * 0.5f, UiKit.H - 52f, 18, UiKit.Mutedish, 0.5f, 1);
        }

        void DrawPause(float w)
        {
            Plate(w, 80f, $"WAVE {Game.Wave}  ·  SCORE {Num(Game.Score)}", UiKit.Cyan, "PAUSED");
            Activate(Screen2.Paused, pauseMenu.Draw(w * 0.5f, 250f, 540f, 80f));

            // Owned upgrades with their icons and level tracks.
            var owned = new System.Collections.Generic.List<(UpgradeDef def, int level)>(Deck.Owned());
            var r = new Rect(w * 0.5f + 310f, 250f, 400f, 70f + Mathf.Max(1, owned.Count) * 58f);
            UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.92f), UiKit.Steel);
            UiKit.Line("YOUR BUILD", r.x + 24f, r.y + 22f, 18, UiKit.Mutedish, 0f, 2);
            if (owned.Count == 0) UiKit.Line("No upgrades yet: clear a wave.", r.x + 24f, r.y + 70f, 20, UiKit.Soft, 0f, 1);
            for (int i = 0; i < owned.Count; i++)
            {
                var (d, l) = owned[i];
                float yy = r.y + 60f + i * 58f;
                Color cc = CategoryColor(Category(d));
                UiKit.Rect(new Rect(r.x + 24f, yy, 46f, 46f), new Color(0.03f, 0.08f, 0.14f, 1f));
                UiKit.DrawIcon(new Rect(r.x + 27f, yy + 3f, 40f, 40f), d.Face > 0 ? GunIcon(WeaponDef.All[d.Face]) : "up_" + d.id, d.Face > 0 ? cc : Color.white);
                UiKit.Line(d.name, r.x + 84f, yy + 4f, 20, UiKit.Text, 0f, 2);
                float segW = (r.width - 84f - 24f - (d.maxLevel - 1) * 3f) / d.maxLevel;
                for (int s = 0; s < d.maxLevel; s++)
                    UiKit.Rect(new Rect(r.x + 84f + s * (segW + 3f), yy + 32f, segW, 8f), s < l ? cc : new Color(0f, 0f, 0f, 0.45f));
            }
        }

        void DrawSettings(float w)
        {
            Plate(w, 80f, "OPTIONS", UiKit.Cyan, "SETTINGS");
            Activate(Screen2.Settings, settingsMenu.Draw(w * 0.5f, 250f, 760f, 80f));
            float hy = UiKit.H - 70f;
            float x = w * 0.5f - 230f;
            x += UiKit.Keycap(x, hy, "←") + 6f;
            x += UiKit.Keycap(x, hy, "→") + 10f;
            UiKit.Line("change", x, hy + 7f, 19, UiKit.Mutedish, 0f, 1);
            x += 110f;
            x += UiKit.Keycap(x, hy, "ESC") + 10f;
            UiKit.Line("back", x, hy + 7f, 19, UiKit.Mutedish, 0f, 1);
        }

        void DrawHowTo(float w)
        {
            float pw = Mathf.Min(1560f, w - 80f);
            var r = new Rect(w * 0.5f - pw * 0.5f, 60f, pw, UiKit.H - 150f);
            UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.96f), UiKit.Steel);
            UiKit.Line("HOW TO PLAY", r.x + 50f, r.y + 36f, 44, UiKit.Text, 0f, 2);

            float x = r.x + 50f, y = r.y + 120f, col = pw * 0.5f - 80f;
            var body = new GUIStyle(UiKit.TextStyle(21, 0)) { wordWrap = true, richText = true };
            (string head, string icon, string text, Color c)[] rules = DiceController.ButtonMode ? new[]
            {
                ("MOVE", "arrow", "WASD, arrows or left stick.", UiKit.Text),
                ("ROLL", "restart", "SPACE, SHIFT or A tips the die one face the way you are moving. The face on top picks your gun, and the <b>markers around the die</b> show what each roll gives. When your gun can't hurt what's coming, the right marker lights up.", UiKit.Cyan),
                ("DODGE", "play", "You can't be hurt during the first part of a roll, but you can on landing. Roll through danger, not into it. Landing on a bare robot or a mite <b>crushes</b> it.", Palette.Hex("#3BD16F")),
                ("SHOOT", "gun3", "Guns aim and fire on their own. Look at what protects an enemy: <b>steel</b> needs piercing or explosive, <b>fliers</b> seekers or shock, <b>vines</b> and <b>ice</b> fire, <b>shields</b> shock. A grey health bar shows the icon of a gun that gets through.", Palette.Hex("#FF6A3D")),
                ("OBSTACLES", "home", "Vent boxes stop a roll. Roll over a <b>pipe rack</b> to vault it: two tips, onto the opposite face. Shove bombs off the edge.", Palette.Hex("#FFB020")),
            } : new[]
            {
                ("GLIDE", "arrow", "WASD, arrows or left stick. You slide like on ice, so plan your turns.", UiKit.Text),
                ("ROLL", "restart", "Slam into a <b>vent box</b> to roll once, or a <b>pipe rack</b> to roll twice. The number on top picks your gun. When your gun can't hurt what's on the field, the bottom bar says which way to roll.", UiKit.Cyan),
                ("SHOOT", "gun3", "Guns aim and fire on their own. A fresh roll overcharges the gun: double fire rate for a few seconds.", Palette.Hex("#FF6A3D")),
                ("BOMBS", "foe", "Bombs light up green, yellow, then red. Shove them off the edge for points. A blast hurts everything nearby, enemies included.", Palette.Hex("#FFB020")),
                ("DASH", "play", "SPACE, SHIFT or A. Dash into an obstacle for a sure roll, or into a bomb for a big shove.", Palette.Hex("#3BD16F")),
            };
            foreach (var rule in rules)
            {
                UiKit.Rect(new Rect(x, y, 44f, 44f), new Color(rule.c.r, rule.c.g, rule.c.b, 0.16f));
                UiKit.DrawIcon(new Rect(x + 9f, y + 9f, 26f, 26f), "ui_" + rule.icon, rule.c);
                UiKit.Line(rule.head, x + 60f, y + 2f, 22, rule.c, 0f, 2);
                float th = body.CalcHeight(new GUIContent(rule.text), col - 60f);
                UiKit.Label(new Rect(x + 60f, y + 28f, col - 60f, th + 4f), rule.text, body, UiKit.Soft);
                y += Mathf.Max(100f, th + 54f);
            }

            float x2 = r.x + pw * 0.5f + 20f, y2 = r.y + 120f;
            UiKit.Line("SIX FACES, SIX GUNS", x2, y2, 18, UiKit.Mutedish, 0f, 2);
            for (int n = 1; n <= 6; n++)
            {
                var d = WeaponDef.All[n];
                float yy = y2 + 34f + (n - 1) * 62f;
                UiKit.DieFace(new Rect(x2, yy, 44, 44), n, d.color, UiKit.Ink);
                UiKit.Rect(new Rect(x2 + 54f, yy, 44f, 44f), new Color(d.color.r * 0.3f, d.color.g * 0.3f, d.color.b * 0.3f, 1f));
                UiKit.DrawIcon(new Rect(x2 + 60f, yy + 6f, 32f, 32f), GunIcon(d), d.color);
                UiKit.Line(d.name, x2 + 112f, yy + 2f, 22, d.color, 0f, 2);
                UiKit.Line(d.role, x2 + 112f, yy + 26f, 17, UiKit.Soft, 0f, 0);
            }
            float y3 = y2 + 34f + 6f * 62f + 16f;
            UiKit.Line("KNOW YOUR ENEMY", x2, y3, 18, UiKit.Mutedish, 0f, 2);
            UiKit.Label(new Rect(x2, y3 + 28f, col, 150f),
                "<b>Drones</b> fly over flat shots: seekers and shock reach them.   <b>Tanks</b> are steel: piercing or explosive.   <b>Mites</b> are too low: roll onto them.\nEvery 5th wave the <b>High Roller</b> tumbles in, wearing a new defence each phase. Bombs hurt everything; a landing crushes bare robots.",
                body, UiKit.Soft);

            float hy = r.yMax - 56f;
            float kx = w * 0.5f - 120f;
            kx += UiKit.Keycap(kx, hy, "ENTER") + 8f;
            kx += UiKit.Keycap(kx, hy, "ESC") + 10f;
            UiKit.Line("back", kx, hy + 7f, 19, UiKit.Mutedish, 0f, 1);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0) { State = howToReturn; Event.current.Use(); }
        }

        // Upgrade categories and their colours (style 7).
        static string Category(UpgradeDef u)
        {
            if (u.Face > 0) return "FACE " + u.Face;
            switch (u.id)
            {
                case "dmg": case "rate": case "over": case "barrel": return "GUNS";
                case "slam": case "dash": case "loaded": return "ROLLS";
                case "hp": return "DEFENCE";
                default: return "BOMBS";
            }
        }

        static Color CategoryColor(string cat) => cat.StartsWith("FACE") ? WeaponDef.All[cat[5] - '0'].color : cat == "GUNS" ? Palette.Hex("#FF6A3D") : cat == "ROLLS" ? Palette.Hex("#29B6F6")
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
                if (u.Face > 0) UiKit.DrawIcon(new Rect(it.center.x - 56, it.center.y - 56, 112, 112), GunIcon(WeaponDef.All[u.Face]), cc);
                else UiKit.DrawIcon(new Rect(it.center.x - 70, it.center.y - 70, 140, 140), "up_" + u.id, Color.white);

                // Name and description.
                int ns = UiKit.TextWidth(u.name, 34) > inner ? 28 : 34;
                UiKit.Line(u.name, px, r.y + 270f, ns, UiKit.Text, 0f, 2);
                UiKit.Label(new Rect(px, r.y + 316f, inner, 130f), Sentence(u.Desc), new GUIStyle(UiKit.TextStyle(23, 0)) { wordWrap = true }, Text7);

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
            Plate(w, 70f, $"RUN OVER  ·  WAVE {Game.Wave}", Palette.Hex("#FF6A3D"), "DICE DESTROYED", 52);

            UiKit.Line("SCORE", w * 0.5f, 220f, 18, UiKit.Mutedish, 0.5f, 2);
            UiKit.Line(Num(Game.Score), w * 0.5f, 248f, 88, Palette.Hex("#FFB020"), 0.5f, 2, 3f);
            if (newBest && Mathf.Repeat(uiTime * 2f, 1f) > 0.25f) UiKit.Line("NEW BEST!", w * 0.5f, 350f, 26, Palette.Hex("#3BD16F"), 0.5f, 2);
            else if (!newBest) UiKit.Line($"BEST {Num(Settings.BestScore)}", w * 0.5f, 352f, 22, UiKit.Mutedish, 0.5f, 2);

            int m = (int)Game.TimeAlive / 60, s = (int)Game.TimeAlive % 60;
            (string k, string v)[] stats =
            {
                ("WAVE", Game.Wave.ToString()), ("KILLS", Game.Kills.ToString()), ("BOMBS DISPOSED", Game.BombsDisposed.ToString()),
                ("ROLLS", Game.Rolls.ToString()), ("BEST COMBO", "×" + Game.BestCombo), ("TIME", $"{m}:{s:00}"),
            };
            float tw = 250f, th = 84f, gap = 14f;
            float x0 = w * 0.5f - (3 * tw + 2 * gap) * 0.5f;
            for (int i = 0; i < stats.Length; i++)
            {
                var t = new Rect(x0 + (i % 3) * (tw + gap), 410f + (i / 3) * (th + gap), tw, th);
                UiKit.ChamferPanel(t, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.9f), new Color(UiKit.Steel.r, UiKit.Steel.g, UiKit.Steel.b, 0.7f));
                UiKit.Line(stats[i].k, t.x + 20f, t.y + 16f, 16, UiKit.Mutedish, 0f, 2);
                UiKit.Line(stats[i].v, t.x + 20f, t.y + 40f, 32, UiKit.Text, 0f, 2);
            }
            var cr = new Rect(w * 0.5f - 389f, 610f, 778f, 50f);
            UiKit.ChamferPanel(cr, new Color(0.2f, 0.13f, 0.02f, 0.9f), Palette.Hex("#FFB020"));
            UiKit.Line($"+{chipsEarned} CHIPS", cr.x + 24f, cr.y + 13f, 24, Palette.Hex("#FFB020"), 0f, 2);
            UiKit.Line($"TOTAL {Num(Loadout.Chips)}  ·  spend them on new guns in BUILD YOUR DIE", cr.xMax - 24f, cr.y + 16f, 18, UiKit.Soft, 1f, 1);
            Activate(Screen2.GameOver, gameOverMenu.Draw(w * 0.5f, 680f, 540f, 80f));
            float hy = UiKit.H - 66f;
            float kx = w * 0.5f - 110f;
            kx += UiKit.Keycap(kx, hy, "R") + 6f;
            kx += UiKit.Keycap(kx, hy, "Y") + 10f;
            UiKit.Line("restart instantly", kx, hy + 7f, 19, UiKit.Mutedish, 0f, 1);
        }
    }
}
