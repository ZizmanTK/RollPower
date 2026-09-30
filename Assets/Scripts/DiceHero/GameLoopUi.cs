using UnityEngine;

namespace DiceHero
{
    /// <summary>HUD and every menu screen (IMGUI on a 1080-tall virtual canvas, see UiKit).</summary>
    public partial class GameLoop
    {
        Menu titleMenu, pauseMenu, settingsMenu, gameOverMenu;
        int upgradeMenuSel = 1;
        float uiTime;

        static bool CanQuit => Application.platform != RuntimePlatform.WebGLPlayer;

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

        void DrawHud(float w)
        {
            var def = Weapons.Current;

            // Current gun
            var wp = new Rect(24, 24, 440, 172);
            UiKit.Panel(wp, 0.8f, Weapons.Overcharge > 0f ? new Color(def.color.r, def.color.g, def.color.b, 0.5f + 0.4f * Mathf.Sin(uiTime * 14f)) : (Color?)null);
            UiKit.Glow(new Rect(20, 20, 136, 136), new Color(def.color.r, def.color.g, def.color.b, 0.25f));
            UiKit.DieFace(new Rect(42, 42, 92, 92), def.number, def.color, UiKit.Ink);
            UiKit.Pixel(def.name, 154, 48, 4f, def.color);
            UiKit.Label(new Rect(154, 84, 300, 30), def.role, UiKit.Small, UiKit.Text);
            UiKit.Bar(new Rect(154, 124, 290, 8), 1f - Weapons.CooldownFraction, def.color);
            if (Weapons.Overcharge > 0f)
            {
                UiKit.Pixel("OVERCHARGE", 154, 146, 2.5f, def.color);
                UiKit.Bar(new Rect(270, 148, 174, 10), Weapons.Overcharge / Weapons.OverchargeMax, def.color);
            }
            else UiKit.Pixel("ROLL TO OVERCHARGE", 154, 146, 2.5f, UiKit.Muted);

            // Next-roll preview: what each trip direction would give.
            var rp = new Rect(24, 210, 440, 250);
            UiKit.Panel(rp, 0.72f);
            UiKit.Pixel("NEXT ROLL", 40, 226, 3f, UiKit.Muted);
            UiKit.Label(new Rect(210, 218, 240, 30), "slam in that direction", new GUIStyle(UiKit.Tiny) { alignment = TextAnchor.UpperRight }, UiKit.Muted);
            (string label, Vector3 dir)[] dirs = { ("↑", Vector3.forward), ("→", Vector3.right), ("↓", Vector3.back), ("←", Vector3.left) };
            for (int i = 0; i < dirs.Length; i++)
            {
                int n = Dice.PreviewTop(dirs[i].dir);
                float y = 258 + i * 38;
                UiKit.Label(new Rect(42, y - 2, 30, 34), dirs[i].label, UiKit.BodyBold, UiKit.Muted);
                UiKit.DieFace(new Rect(76, y, 30, 30), n, WeaponDef.All[n].color, UiKit.Ink);
                UiKit.Pixel(WeaponDef.All[n].name, 118, y + 7, 2.6f, WeaponDef.All[n].color);
            }
            int two = Dice.PreviewTop(Vector3.forward, 2);
            UiKit.Rect(new Rect(40, 410, 408, 1), new Color(1f, 1f, 1f, 0.08f));
            UiKit.Pixel("CONDUIT", 42, 424, 2.6f, UiKit.Blue);
            UiKit.DieFace(new Rect(146, 417, 30, 30), two, WeaponDef.All[two].color, UiKit.Ink);
            UiKit.Pixel(WeaponDef.All[two].name, 188, 424, 2.6f, WeaponDef.All[two].color);

            // Integrity, score, wave, combo (top right)
            var sp = new Rect(w - 464, 24, 440, 172);
            UiKit.Panel(sp, 0.8f);
            for (int i = 0; i < Game.MaxHp; i++)
            {
                bool full = i < Game.Hp;
                float bob = full && Game.Hp <= 2 ? Mathf.Sin(uiTime * 10f + i) * 2f : 0f;
                UiKit.Pixel("*", w - 444 + i * 40, 42 + bob, 5f, full ? UiKit.Red : new Color(1f, 1f, 1f, 0.12f), 0f, full ? 0.18f : 0f);
            }
            UiKit.Pixel("SCORE", w - 444, 96, 2.5f, UiKit.Muted);
            UiKit.Pixel(Game.Score.ToString("N0").Replace(" ", ","), w - 444, 116, 6f, UiKit.Gold);
            UiKit.Pixel(Game.BossWave ? "BOSS WAVE" : $"WAVE {Mathf.Max(1, Game.Wave)}", w - 40, 96, 3f, Game.BossWave ? UiKit.Red : UiKit.Text, 1f);
            if (Game.Combo > 1 || Game.Chain > 1)
            {
                float k = Mathf.Clamp01(Game.ComboTimer / Game.ComboWindow);
                Color cc = Color.Lerp(UiKit.Gold, UiKit.Red, (Game.Combo - 1) / 4f);
                float pop = 1f + Mathf.Max(0f, Game.ComboTimer - (Game.ComboWindow - 0.15f)) * 2f;
                UiKit.Pixel($"X{Game.Combo}", w - 40, 122, 6f * pop, cc, 1f);
                UiKit.Bar(new Rect(w - 150, 170, 110, 6), k, cc);
            }

            // Boss health
            var boss = Game.Boss;
            if (boss != null && boss.Alive)
            {
                float bw = Mathf.Min(820f, w - 1000f);
                var br = new Rect(w * 0.5f - bw * 0.5f, 24, bw, 96);
                UiKit.Panel(br, 0.82f, new Color(UiKit.Red.r, UiKit.Red.g, UiKit.Red.b, 0.6f));
                UiKit.Pixel("HIGH ROLLER", br.x + 20, br.y + 16, 4f, UiKit.Red);
                UiKit.Bar(new Rect(br.x + 20, br.y + 56, bw - 140, 18), boss.hp / boss.maxHp, UiKit.Red, new Color(1f, 1f, 1f, 0.08f));
                int weak = boss.Weakness;
                bool match = def.number == weak;
                UiKit.Glow(new Rect(br.xMax - 118, br.y - 8, 112, 112), new Color(1f, 0.2f, 0.25f, match ? 0.5f : 0.2f));
                UiKit.DieFace(new Rect(br.xMax - 96, br.y + 14, 68, 68), weak, match ? UiKit.Gold : WeaponDef.All[weak].color, UiKit.Ink);
                UiKit.Pixel(match ? "MATCHED!" : "WEAK TO", br.xMax - 250, br.y + 18, 2.5f, match ? UiKit.Gold : UiKit.Muted);
            }

            // Bomb fuse gauges (and edge arrows for bombs off screen)
            DrawBombGauges(w);

            // Warning when the current gun can't hurt something on the field
            string immune = Game.Immune(def);
            if (immune != null && State == Screen2.Playing)
            {
                float a = 0.75f + 0.25f * Mathf.Sin(uiTime * 8f);
                UiKit.ShadowLabel(new Rect(0, 214, w, 40), $"{def.name} CAN'T HURT {immune}: ROLL FOR A NEW GUN", UiKit.BodyCenter, new Color(1f, 0.82f, 0.25f, a));
            }

            // Banner
            if (bannerTime > 0f)
            {
                float a = Mathf.Clamp01(bannerTime * 2.5f) * Mathf.Clamp01((bannerMax - bannerTime) * 8f + 0.2f);
                float slide = Mathf.Clamp01((bannerMax - bannerTime) * 6f);
                var c = new Color(bannerColor.r, bannerColor.g, bannerColor.b, a);
                if (bannerTitle != null)
                {
                    float px = 11f + (1f - slide) * 4f;
                    UiKit.Pixel(bannerTitle, w * 0.5f, 270 - px * 3.5f, px, c, 0.5f, 0.2f);
                    if (bannerSub != null) UiKit.Pixel(bannerSub, w * 0.5f, 340, 3.6f, new Color(1f, 1f, 1f, a), 0.5f);
                }
                else if (bannerSub != null) UiKit.Pixel(bannerSub, w * 0.5f, 150, 4f, c, 0.5f);
            }

            // Tutorial hints
            string hint = TutorialHint();
            if (hint != null && State == Screen2.Playing)
            {
                float hw = UiKit.PixelWidth(hint, 3.2f) + 60f;
                var hr = new Rect(w * 0.5f - hw * 0.5f, UiKit.H - 200, hw, 52);
                UiKit.Panel(hr, 0.78f, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.5f));
                UiKit.Pixel(hint, w * 0.5f, hr.y + 15, 3.2f, UiKit.Text, 0.5f);
            }

            // Dash (bottom centre)
            float bx = w * 0.5f - 180f;
            var dr = new Rect(bx, UiKit.H - 110, 360, 56);
            UiKit.Panel(dr, 0.72f);
            float dash = 1f - Mathf.Clamp01(Dice.DashCooldownLeft / Dice.DashCooldownTotal);
            UiKit.Pixel(dash >= 1f ? "DASH READY" : "DASH", bx + 18, dr.y + 20, 2.8f, dash >= 1f ? UiKit.Mint : UiKit.Muted);
            UiKit.Bar(new Rect(bx + 170, dr.y + 23, 170, 10), dash, dash >= 1f ? UiKit.Mint : new Color(1f, 1f, 1f, 0.35f));

            UiKit.Label(new Rect(24, UiKit.H - 44, w - 48, 30), "WASD / stick  glide     SPACE / A  dash     slam barriers to roll     ESC / START  pause", UiKit.Tiny, new Color(0.7f, 0.8f, 0.95f, 0.6f));
        }

        string TutorialHint()
        {
            if (!Settings.ShowTutorial || Game.Wave > 3 || Game.Lost) return null;
            if (Game.Rolls == 0 && Game.TimeAlive < 5f) return "GLIDE WITH WASD, ARROWS OR THE LEFT STICK";
            if (Game.Rolls == 0) return "SLAM INTO A RED BARRIER TO ROLL: THE TOP NUMBER PICKS YOUR GUN";
            if (Game.Bombs.All.Count > 0 && Game.BombsDisposed == 0) return "SHOVE BOMBS OFF THE EDGE. LANDING A ROLL NEXT TO ONE LAUNCHES IT";
            if (Game.Rolls < 3 && Game.TimeAlive < 40f) return "BLUE CONDUITS ROLL YOU TWICE. DASH WITH SPACE OR A";
            return null;
        }

        void DrawBombGauges(float w)
        {
            var camera = Camera.main;
            if (camera == null) return;
            float scale = Screen.height / UiKit.H;
            foreach (var b in Game.Bombs.All)
            {
                if (b.falling) continue;
                Vector3 world = (b.drop > 0f ? b.pos : b.pos) + Vector3.up * 1.35f;
                Vector3 s = camera.WorldToScreenPoint(world);
                if (s.z < 0f) continue;
                Vector2 p = new Vector2(s.x / scale, (Screen.height - s.y) / scale);
                float k = b.drop > 0f ? 1f : b.fuse / b.maxFuse;
                Color c = k > 0.6f ? Palette.Hex("#3DFF7A") : k > 0.3f ? Palette.Hex("#FFD23D") : UiKit.Red;
                bool off = p.x < 20f || p.x > w - 20f || p.y < 20f || p.y > UiKit.H - 20f;
                if (!off)
                {
                    UiKit.Bar(new Rect(p.x - 32f, p.y, 64f, 8f), k, c, new Color(0f, 0f, 0f, 0.5f));
                    if (b.drop <= 0f && b.fuse < 3f) UiKit.Pixel(b.fuse.ToString("0.0"), p.x, p.y - 22f, 2.6f, c, 0.5f);
                }
                else
                {
                    // Arrow at the screen edge pointing to the bomb.
                    Vector2 centre = new Vector2(w * 0.5f, UiKit.H * 0.5f);
                    Vector2 d = (p - centre).normalized;
                    float t = Mathf.Min((w * 0.5f - 60f) / Mathf.Max(0.001f, Mathf.Abs(d.x)), (UiKit.H * 0.5f - 60f) / Mathf.Max(0.001f, Mathf.Abs(d.y)));
                    Vector2 e = centre + d * t;
                    float blink = k < 0.3f ? (Mathf.Repeat(uiTime * 6f, 1f) > 0.5f ? 1f : 0.35f) : 1f;
                    UiKit.Glow(new Rect(e.x - 34f, e.y - 34f, 68f, 68f), new Color(c.r, c.g, c.b, 0.45f * blink));
                    var m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, e * scale);
                    UiKit.Pixel(">", e.x, e.y - 10.5f, 3f, new Color(c.r, c.g, c.b, blink), 0.5f);
                    GUI.matrix = m;
                }
            }
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
            UiKit.Rect(new Rect(0, 0, w, UiKit.H), new Color(0.02f, 0.03f, 0.06f, 0.35f));
            DrawLogo(w, 110f, 21f);
            UiKit.Pixel("ROLLING IS YOUR SUPERPOWER", w * 0.5f, 520f, 4f, UiKit.Text, 0.5f);
            int clicked = titleMenu.Draw(w * 0.5f, 600f, 520f, 76f);
            Activate(Screen2.Title, clicked);

            if (Settings.BestScore > 0)
                UiKit.Pixel($"BEST {Settings.BestScore:N0}   WAVE {Settings.BestWave}".Replace(" ", ","), w * 0.5f, UiKit.H - 110f, 3.2f, UiKit.Gold, 0.5f);
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
                ("ROLL", "Slam into a <b>red barrier</b> to roll once, or a <b>blue conduit</b> to roll twice. The number on top picks your gun, and every landing sends out a shockwave.", UiKit.Red),
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
                float yy = y2 + 40f + (n - 1) * 56f;
                UiKit.DieFace(new Rect(x2, yy, 42, 42), n, d.color, UiKit.Ink);
                UiKit.Pixel(d.name, x2 + 60f, yy + 4f, 3f, d.color);
                UiKit.Label(new Rect(x2 + 60f, yy + 22f, col, 26f), d.role, UiKit.Tiny, UiKit.Text);
            }
            float y3 = y2 + 40f + 6f * 56f + 16f;
            UiKit.Pixel("KNOW YOUR ENEMY", x2, y3, 3.4f, UiKit.Muted);
            UiKit.Label(new Rect(x2, y3 + 30f, col, 150f),
                "<b>Drones</b> fly: only 3 or 6 reach them.   <b>Tanks</b> are armoured: use 1 or 4.\n<b>Bombers</b> plant bombs.   Every 5th wave, the <b>High Roller</b> tumbles in: only the gun that matches its top number can hurt it. Slams and bombs hurt everything.",
                UiKit.Small, UiKit.Text);

            UiKit.Label(new Rect(0, r.yMax - 50f, w, 30), "press ENTER, ESC, A or B to go back", UiKit.SmallCenter, UiKit.Muted);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0) { State = howToReturn; Event.current.Use(); }
        }

        void DrawUpgrade(float w)
        {
            if (hand == null || hand.Count == 0) return;
            UiKit.Pixel(Game.BossWave ? "HIGH ROLLER DOWN!" : $"WAVE {Game.Wave} CLEARED", w * 0.5f, 150f, 10f, UiKit.Gold, 0.5f, 0.25f, UiKit.GoldDeep);
            UiKit.Pixel("CHOOSE AN UPGRADE", w * 0.5f, 250f, 4f, UiKit.Text, 0.5f);

            float cw = 400f, ch = 460f, gap = 40f;
            float total = hand.Count * cw + (hand.Count - 1) * gap;
            float x0 = w * 0.5f - total * 0.5f;
            var e = Event.current;
            for (int i = 0; i < hand.Count; i++)
            {
                var u = hand[i];
                bool sel = upgradeMenuSel == i;
                float lift = sel ? -14f - Mathf.Sin(uiTime * 4f) * 3f : 0f;
                var r = new Rect(x0 + i * (cw + gap), 330f + lift, cw, ch);
                if (r.Contains(e.mousePosition) && (e.type == EventType.MouseMove || e.type == EventType.Repaint) && upgradeMenuSel != i && e.delta.sqrMagnitude > 0f)
                { upgradeMenuSel = i; Sound.Play(Sfx.UiMove, 0.4f, 0f); }
                if (sel) UiKit.Glow(new Rect(r.x - 60, r.y - 60, r.width + 120, r.height + 120), new Color(u.color.r, u.color.g, u.color.b, 0.22f));
                UiKit.Panel(r, 0.94f, sel ? u.color : new Color(1f, 1f, 1f, 0.12f));
                UiKit.Rect(new Rect(r.x + 3, r.y + 3, r.width - 6, 12), u.color);

                // Icon: a die face in the upgrade's colour, pips = next level.
                int lvl = Deck.Level(u) + 1;
                UiKit.Glow(new Rect(r.center.x - 90, r.y + 40, 180, 180), new Color(u.color.r, u.color.g, u.color.b, 0.3f));
                UiKit.DieFace(new Rect(r.center.x - 52, r.y + 78, 104, 104), Mathf.Clamp(lvl, 1, 6), u.color, UiKit.Ink);

                float px = Mathf.Min(4f, (cw - 40f) / UiKit.PixelWidth(u.name, 1f));
                UiKit.Pixel(u.name, r.center.x, r.y + 226, px, u.color, 0.5f);
                UiKit.Label(new Rect(r.x + 30, r.y + 270, cw - 60, 100), u.desc, new GUIStyle(UiKit.Body) { alignment = TextAnchor.UpperCenter }, UiKit.Text);
                UiKit.Pixel(u.maxLevel > 1 ? $"LEVEL {lvl} / {u.maxLevel}" : "UNIQUE", r.center.x, r.yMax - 76, 2.6f, UiKit.Muted, 0.5f);
                UiKit.Pixel($"[{i + 1}]", r.center.x, r.yMax - 44, 3f, sel ? UiKit.Gold : UiKit.Muted, 0.5f);

                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    e.Use();
                    PickUpgrade(u);
                    return;
                }
            }
            UiKit.Label(new Rect(0, UiKit.H - 80, w, 30), "click a card, press 1-3, or use left / right and ENTER / A", UiKit.SmallCenter, UiKit.Muted);
        }

        void DrawGameOver(float w)
        {
            UiKit.Pixel("DICE DESTROYED", w * 0.5f, 110f, 11f, UiKit.Red, 0.5f, 0.25f, new Color(0.3f, 0f, 0.05f));
            UiKit.Pixel("SCORE", w * 0.5f, 225f, 3.4f, UiKit.Muted, 0.5f);
            UiKit.Pixel(Game.Score.ToString("N0").Replace(" ", ","), w * 0.5f, 258f, 11f, UiKit.Gold, 0.5f, 0.25f, UiKit.GoldDeep);
            if (newBest && Mathf.Repeat(uiTime * 2f, 1f) > 0.25f) UiKit.Pixel("NEW BEST!", w * 0.5f, 350f, 4.5f, UiKit.Mint, 0.5f);
            else if (!newBest) UiKit.Pixel($"BEST {Settings.BestScore:N0}".Replace(" ", ","), w * 0.5f, 350f, 3.4f, UiKit.Muted, 0.5f);

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
