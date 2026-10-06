using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Campaign flow: story slides, the station map (decks and stages), a stage run with Vega on the radio, the
    /// stage-clear screen (stars, chips, upgrade pick) and the retry screen. Upgrades are kept for the whole deck and
    /// carried between stages through Campaign.StageStartStats. Also logs play to Telemetry.
    /// </summary>
    public partial class GameLoop
    {
        Menu stageClearMenu, stageFailMenu;
        int mapDeck, mapStage, storyIndex, stageStars, stageChips;
        (string title, string text)[] story;
        bool storyToMap;
        string deathLine;

        // Radio (Vega) lines: one at a time, typed out.
        readonly Queue<string> radio = new Queue<string>();
        string radioLine;
        float radioT;

        // Per-stage measurements (telemetry and stars).
        float fightTime, wrongTime, hintShownAt = -1f;
        int rolls, usefulRolls, dodges, hintsShown, hintsFollowed, hintTop;
        bool dodgedThisRoll;

        bool InStage => Campaign.Active != null && Game.Stage != null;
        Tutorial tutorial;
        /// <summary>Roll to recommend this frame: the tutorial's own advice when it has one, else the roll guide's.</summary>
        RollPlan Advice => tutorial?.PlanOverride ?? Guide.Plan;
        /// <summary>Autopilot hooks for the tutorial.</summary>
        public Vector3? TutorialGoal => tutorial?.Goal;
        public RollPlan TutorialAdvice => tutorial?.PlanOverride;
        public int TutorialStep => tutorial != null ? tutorial.Step : -1;

        /// <summary>Mounts a module on a face mid-stage (the tutorial pickup): gun, Pip's socket and the floor outline.</summary>
        public void MountModule(int face, string id)
        {
            var faces = new string[7];
            for (int f = 1; f <= 6; f++) faces[f] = WeaponDef.All[f].id;
            faces[face] = id;
            WeaponDef.Apply(faces);
            Weapons.RebuildGun(face);
            if (Dice.Model.IsPip) PipBuilder.RebuildFace(Palette, Dice.Model, face);
            Compass.RefreshFace(face);
            var w = WeaponDef.All[face];
            Fx.Text(Dice.transform.position + Vector3.up * 2.4f, w.name + " MOUNTED", w.color, 1.2f, 1f);
            Fx.Flash(Palette, Dice.transform.position + Vector3.up * 0.8f, w.color, 0.7f, 0.25f);
            Sound.Play(Sfx.Upgrade, 0.9f, 0f);
            Telemetry.Log("module", "gun", id, "face", face, "stage", Campaign.Active?.id);
        }

        // Hooks for the demo director's campaign walk-through.
        public void DemoOpenCampaign() => OpenCampaign();
        public void DemoAdvanceStory() { storyIndex++; if (storyIndex >= story.Length) FinishStory(); }
        public void DemoLaunchFirstStage() => LaunchStage(Campaign.Decks[0].stages[0], true);
        public void DemoStageMenu(int index) { if (State == Screen2.StageClear) ActivateStageClear(index); else if (State == Screen2.StageFailed) ActivateStageFail(index); }

        public void Radio(string line) { if (!string.IsNullOrEmpty(line)) radio.Enqueue(line); }

        /// <summary>A line that must be heard now (the tutorial's): drops whatever was still queued.</summary>
        public void RadioNow(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            radio.Clear();
            radioLine = null; radioT = 0f;
            radio.Enqueue(line);
        }

        /// <summary>HUD top-left: the stage and its wave, the tutorial, or the gauntlet's wave.</summary>
        string HudWaveLabel => tutorial != null ? "TUTORIAL"
            : InStage ? (Game.Foreman != null ? $"STAGE {StageLabel(Campaign.Active)} · FOREMAN" : $"STAGE {StageLabel(Campaign.Active)} · WAVE {Mathf.Max(1, Game.Wave)}/{Campaign.Active.waves.Length}")
            : Game.BossWave ? $"WAVE {Game.Wave} · BOSS" : $"WAVE {Game.Wave}";

        void BuildCampaignMenus()
        {
            stageClearMenu = new Menu().Add("CONTINUE", "play").Add("REPLAY FOR STARS", "restart").Add("STATION MAP", "home");
            stageFailMenu = new Menu().Add("RETRY STAGE", "restart").Add("STATION MAP", "home");
        }

        /// <summary>Called at the end of Init: starts a pending campaign stage, or wires the gauntlet's telemetry.</summary>
        void InitCampaign()
        {
            BuildCampaignMenus();
            Dice.TopChanged += OnRolled;
            Game.Hurt += cause => Telemetry.Log("hurt", "cause", cause, "hp", Game.Hp, "stage", Campaign.Active?.id, "wave", Game.Wave);
            Game.Dodged += () => { if (!dodgedThisRoll) { dodgedThisRoll = true; dodges++; } };
            Dice.Tripped += _ => dodgedThisRoll = false;
            if (Campaign.Pending == null) { Campaign.Active = null; return; }

            Campaign.Active = Campaign.Pending;
            Campaign.Pending = null;
            Game.Stage = Campaign.Active;
            RunStats.Current = (Campaign.StageStartStats ?? new RunStats()).Clone();
            Deck.Restore(Campaign.StageStartLevels);
            Game.RepairFull();
            Game.StageCleared += OnStageCleared;
            if (Campaign.Active.tutorial) tutorial = new Tutorial(this, Game, Dice, Palette);
            var st = Campaign.Active;
            if (st.cache != null && !Campaign.CacheFound(st.cache))
                cachePickup = Tutorial.ModulePickup(Palette, new Vector3(st.cachePos.x, 0.35f, st.cachePos.y), st.cache);
            foreach (var l in Campaign.Active.startRadio) Radio(l);
            Telemetry.Log("stage_start", "stage", Campaign.Active.id, "attempt", Campaign.Deaths + 1, "par", Campaign.Active.parTime);
        }

        void OnRolled(int from, int to)
        {
            rolls++;
            bool useful = dodgedThisRoll || RollAdvisor.Value(WeaponDef.All[to], Game) > RollAdvisor.Value(WeaponDef.All[from], Game);
            if (useful) usefulRolls++;
            if (hintShownAt >= 0f && to == hintTop)
            {
                hintsFollowed++;
                Telemetry.Log("hint_followed", "after", Game.TimeAlive - hintShownAt, "gun", WeaponDef.All[to].id);
                hintShownAt = -1f;
            }
            Telemetry.Log("roll", "from", WeaponDef.All[from].id, "to", WeaponDef.All[to].id, "useful", useful, "dodged", dodgedThisRoll, "stage", Campaign.Active?.id);
        }

        /// <summary>Per-tick measurements while fighting (called from Step).</summary>
        Transform cachePickup;

        void StepCache(float dt)
        {
            if (cachePickup == null) return;
            cachePickup.Rotate(0f, 90f * dt, 0f);
            Vector3 d = cachePickup.position - Dice.transform.position; d.y = 0f;
            if (d.magnitude > 1.1f) return;
            var id = Campaign.Active.cache;
            Campaign.FindCache(id);
            var w = WeaponDef.Find(id);
            Destroy(cachePickup.gameObject);
            cachePickup = null;
            Fx.Text(Dice.transform.position + Vector3.up * 2.4f, "SECRET MODULE: " + w.name, w.color, 1.2f, 1.4f);
            Fx.Flash(Palette, Dice.transform.position + Vector3.up * 0.8f, w.color, 0.7f, 0.25f);
            Sound.Play(Sfx.Upgrade, 1f, 0f);
            RadioNow($"A spare {Title7(w.name)}! I'll keep it in the Workshop: you can swap it onto a face between decks.");
            Telemetry.Log("cache", "gun", id, "stage", Campaign.Active.id);
        }

        static string Title7(string caps) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(caps.ToLowerInvariant());

        void MeasureStep(float dt)
        {
            tutorial?.Update(dt);
            StepCache(dt);
            if (Game.Intermission || Game.Lost || Game.Won || Game.EnemiesLeft == 0) return;
            fightTime += dt;
            if (RollAdvisor.Needed(Dice, Game)) wrongTime += dt;
            var plan = Guide.Plan;
            if (plan != null && hintShownAt < 0f)
            {
                hintShownAt = Game.TimeAlive; hintTop = plan.top; hintsShown++;
                Telemetry.Log("hint_shown", "gun", plan.Gun.id, "steps", plan.steps, "stage", Campaign.Active?.id);
            }
            else if (plan == null && hintShownAt >= 0f && Game.TimeAlive - hintShownAt > 3f) hintShownAt = -1f;
        }

        void LogStageEnd(string result)
        {
            Telemetry.Log("stage_end", "stage", Campaign.Active?.id, "result", result, "time", Game.TimeAlive, "hits", Game.HitsTaken,
                "stars", stageStars, "wrong_share", fightTime > 0f ? wrongTime / fightTime : 0f, "rolls", rolls, "useful_rolls", usefulRolls,
                "dodges", dodges, "hints", hintsShown, "hints_followed", hintsFollowed, "kills", Game.Kills);
        }

        void OnStageCleared()
        {
            var s = Campaign.Active;
            stageStars = 1 | (Game.HitsTaken == 0 ? 2 : 0) | (Game.TimeAlive <= s.parTime ? 4 : 0);
            int before = Campaign.Stars(s);
            Campaign.Record(s, stageStars);
            stageChips = 20 + 10 * Campaign.StarCount(stageStars & ~before);
            Loadout.Chips += stageChips;
            Campaign.Deaths = 0;
            foreach (var l in s.clearRadio) Radio(l);
            LogStageEnd("clear");
            State = Screen2.StageClear;
            stageClearMenu.Selected = 0;
            Sound.Duck(true);
        }

        void StageFailed()
        {
            Campaign.Deaths++;
            deathLine = Campaign.DeathLines[(Campaign.Deaths - 1) % Campaign.DeathLines.Length];
            stageStars = 0;
            LogStageEnd("death");
            State = Screen2.StageFailed;
            stageFailMenu.Selected = 0;
            Sound.Play(Sfx.GameOver, 0.9f, 0f);
            Sound.Duck(true);
            if (cam != null) cam.Orbit = true;
        }

        void OpenCampaign()
        {
            if (!Campaign.SeenIntro) { story = Campaign.Intro; storyIndex = 0; storyToMap = true; State = Screen2.Story; return; }
            EnterMap();
        }

        void EnterMap()
        {
            State = Screen2.Campaign;
            if (cam != null) cam.Orbit = true;
            // Start on the first stage not cleared yet.
            for (int d = 0; d < Campaign.Decks.Length; d++)
            {
                var deck = Campaign.Decks[d];
                if (!deck.playable) continue;
                mapDeck = d; mapStage = 0;
                for (int i = 0; i < deck.stages.Length; i++) if (!Campaign.Cleared(deck.stages[i])) { mapStage = i; return; }
                mapStage = deck.stages.Length - 1;
            }
        }

        void LaunchStage(StageDef s, bool newDeckRun)
        {
            if (newDeckRun) Campaign.BeginDeck(s);
            else Campaign.Pending = s;
            Sound.Play(Sfx.UiConfirm, 0.7f, 0f);
            Reload(true);
        }

        /// <summary>After the upgrade pick at the end of a stage: carry the upgrades into the next stage.</summary>
        void AfterStageCard()
        {
            Campaign.StageStartStats = RunStats.Current.Clone();
            Campaign.StageStartLevels = Deck.Levels();
            var next = Campaign.Next(Campaign.Active);
            if (next != null) LaunchStage(next, false);
            else { Campaign.Active = null; Campaign.OpenMap = true; Reload(false); } // deck done: back to the map
        }

        // ---------------- Input ----------------

        void CampaignInput(float udt)
        {
            switch (State)
            {
                case Screen2.Story:
                    if (Controls.Back) { FinishStory(); break; }
                    if (Controls.Confirm) { storyIndex++; Sound.Play(Sfx.UiMove, 0.6f, 0f); if (storyIndex >= story.Length) FinishStory(); }
                    break;
                case Screen2.Campaign:
                {
                    if (Controls.Back) { EnterTitle(); Sound.Play(Sfx.UiConfirm, 0.6f, 0f); break; }
                    var nav = Controls.Nav(udt);
                    var deck = Campaign.Decks[mapDeck];
                    if (nav.x != 0) { mapDeck = (mapDeck + nav.x + Campaign.Decks.Length) % Campaign.Decks.Length; mapStage = 0; Sound.Play(Sfx.UiMove, 0.5f, 0f); }
                    if (nav.y != 0 && deck.stages.Length > 0) { mapStage = (mapStage - nav.y + deck.stages.Length) % deck.stages.Length; Sound.Play(Sfx.UiMove, 0.5f, 0f); }
                    if (Controls.Confirm) TryLaunch(mapDeck, mapStage);
                    break;
                }
                case Screen2.StageClear:
                    ActivateStageClear(stageClearMenu.UpdateInput(udt));
                    break;
                case Screen2.StageFailed:
                    if (Controls.Restart) { ActivateStageFail(0); break; }
                    ActivateStageFail(stageFailMenu.UpdateInput(udt));
                    break;
            }
        }

        void FinishStory()
        {
            if (story == Campaign.Intro) Campaign.SeenIntro = true;
            Settings.Save();
            if (storyToMap) EnterMap(); else EnterTitle();
        }

        void TryLaunch(int d, int i)
        {
            var deck = Campaign.Decks[d];
            if (!deck.playable || i >= deck.stages.Length) { Toast("THIS DECK IS NOT BUILT YET", 2f); Sound.Play(Sfx.UiMove, 0.5f, 0f); return; }
            var s = deck.stages[i];
            if (!Campaign.Unlocked(s)) { Toast("CLEAR THE STAGE BEFORE IT FIRST", 2f); Sound.Play(Sfx.UiMove, 0.5f, 0f); return; }
            // Starting from the map begins a fresh deck run (no upgrades), whatever stage it is.
            LaunchStage(s, true);
        }

        void ActivateStageClear(int index)
        {
            if (index < 0) return;
            if (index == 0)
            {
                hand = Deck.Deal(3);
                if (hand.Count == 0) { AfterStageCard(); return; }
                State = Screen2.Upgrade; upgradeMenuSel = 1;
            }
            else if (index == 1) { Campaign.Pending = Campaign.Active; Reload(true); } // same upgrades as at its start
            else { Campaign.Active = null; Campaign.OpenMap = true; Reload(false); }
        }

        void ActivateStageFail(int index)
        {
            if (index < 0) return;
            if (index == 0) { Campaign.Pending = Campaign.Active; Reload(true); }
            else { Campaign.Active = null; Campaign.OpenMap = true; Reload(false); }
        }

        // ---------------- Drawing ----------------

        void DrawCampaign(float w)
        {
            loadoutMsgTime -= Time.unscaledDeltaTime;
            Plate(w, 40f, "PIP AND THE HOUSE", UiKit.Cyan, "STATION HEXA-7", 44);
            var e = Event.current;
            // Left: the five decks.
            float lx = w * 0.5f - 700f, ly = 200f, lw = 560f, rowH = 112f;
            UiKit.Line("DECKS", lx, ly - 34f, 18, UiKit.Mutedish, 0f, 2);
            for (int d = 0; d < Campaign.Decks.Length; d++)
            {
                var deck = Campaign.Decks[d];
                var r = new Rect(lx, ly + d * rowH, lw, rowH - 12f);
                bool sel = d == mapDeck;
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { mapDeck = d; mapStage = 0; e.Use(); }
                float a = deck.playable ? 1f : 0.45f;
                UiKit.ChamferPanel(r, new Color(sel ? 0.06f : UiKit.Ink.r, sel ? 0.16f : UiKit.Ink.g, sel ? 0.25f : UiKit.Ink.b, 0.94f), sel ? UiKit.Cyan : new Color(UiKit.Steel.r, UiKit.Steel.g, UiKit.Steel.b, 0.6f));
                UiKit.Line($"DECK {d + 1}", r.x + 22f, r.y + 14f, 16, new Color(UiKit.Mutedish.r, UiKit.Mutedish.g, UiKit.Mutedish.b, a), 0f, 2);
                UiKit.Line(deck.name, r.x + 22f, r.y + 34f, 30, new Color(1f, 1f, 1f, a), 0f, 2);
                UiKit.Line(deck.mechanic, r.x + 22f, r.y + 70f, 16, new Color(UiKit.Soft.r, UiKit.Soft.g, UiKit.Soft.b, a), 0f, 1);
                int got = 0, max = deck.stages.Length * 3;
                foreach (var s in deck.stages) got += Campaign.StarCount(Campaign.Stars(s));
                UiKit.Line(deck.playable ? $"{got}/{max} ★" : "LOCKED", r.xMax - 22f, r.y + 38f, 20, deck.playable ? Palette.Hex("#FFB020") : UiKit.Mutedish, 1f, 2);
            }

            // Right: the stages of the selected deck.
            var dk = Campaign.Decks[mapDeck];
            float rx = w * 0.5f - 100f, ry = 200f, rw = 800f;
            UiKit.Line(dk.playable ? $"{dk.name}: STAGES" : $"{dk.name}: NOT BUILT YET", rx, ry - 34f, 18, UiKit.Mutedish, 0f, 2);
            if (!dk.playable)
            {
                var pr = new Rect(rx, ry, rw, 150f);
                UiKit.ChamferPanel(pr, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.9f), new Color(UiKit.Steel.r, UiKit.Steel.g, UiKit.Steel.b, 0.6f));
                UiKit.Line(dk.look, pr.x + 26f, pr.y + 30f, 24, UiKit.Soft, 0f, 1);
                UiKit.Line("New idea: " + dk.mechanic, pr.x + 26f, pr.y + 72f, 20, UiKit.Mutedish, 0f, 1);
            }
            for (int i = 0; i < dk.stages.Length; i++)
            {
                var s = dk.stages[i];
                var r = new Rect(rx, ry + i * 132f, rw, 120f);
                bool sel = i == mapStage, open = Campaign.Unlocked(s);
                if (r.Contains(e.mousePosition) && e.type == EventType.MouseMove) mapStage = i;
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { mapStage = i; e.Use(); TryLaunch(mapDeck, i); }
                float a = open ? 1f : 0.45f;
                UiKit.ChamferPanel(r, new Color(sel ? 0.06f : UiKit.Ink.r, sel ? 0.16f : UiKit.Ink.g, sel ? 0.25f : UiKit.Ink.b, 0.94f), sel ? UiKit.Cyan : new Color(UiKit.Steel.r, UiKit.Steel.g, UiKit.Steel.b, 0.6f));
                UiKit.Line($"{mapDeck + 1}-{i + 1}", r.x + 24f, r.y + 18f, 20, new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, a), 0f, 2);
                UiKit.Line(s.name, r.x + 90f, r.y + 14f, 30, new Color(1f, 1f, 1f, a), 0f, 2);
                UiKit.Line(s.teaches, r.x + 90f, r.y + 52f, 18, new Color(UiKit.Soft.r, UiKit.Soft.g, UiKit.Soft.b, a), 0f, 1);
                UiKit.Line($"{s.objective}  ·  par {Mathf.RoundToInt(s.parTime)} s", r.x + 90f, r.y + 82f, 16, new Color(UiKit.Mutedish.r, UiKit.Mutedish.g, UiKit.Mutedish.b, a), 0f, 1);
                int st = Campaign.Stars(s);
                string[] names = { "CLEAR", "NO HIT", "PAR" };
                for (int k = 0; k < 3; k++)
                {
                    bool on = (st & (1 << k)) != 0;
                    UiKit.DrawIcon(new Rect(r.xMax - 250f + k * 80f, r.y + 26f, 30f, 30f), "ui_star", on ? Palette.Hex("#FFB020") : new Color(1f, 1f, 1f, 0.15f));
                    UiKit.Line(names[k], r.xMax - 235f + k * 80f, r.y + 64f, 13, on ? Palette.Hex("#FFB020") : UiKit.Mutedish, 0.5f, 2);
                }
                if (!open) UiKit.Line("LOCKED", r.xMax - 24f, r.y + 92f, 14, UiKit.Mutedish, 1f, 2);
            }
            if (loadoutMsgTime > 0f && loadoutMsg != null) UiKit.Line(loadoutMsg, w * 0.5f, UiKit.H - 130f, 22, Palette.Hex("#FFB020"), 0.5f, 2, 2f);

            float hy = UiKit.H - 52f, kx = 40f;
            kx += UiKit.Keycap(kx, hy, "←") + 4f; kx += UiKit.Keycap(kx, hy, "→") + 8f;
            UiKit.Line("deck", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1); kx += 70f;
            kx += UiKit.Keycap(kx, hy, "↑") + 4f; kx += UiKit.Keycap(kx, hy, "↓") + 8f;
            UiKit.Line("stage", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1); kx += 76f;
            kx += UiKit.Keycap(kx, hy, "ENTER") + 8f;
            UiKit.Line("play the stage", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1); kx += 140f;
            kx += UiKit.Keycap(kx, hy, "ESC") + 8f;
            UiKit.Line("back", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1);
        }

        void DrawStory(float w)
        {
            if (story == null || storyIndex >= story.Length) return;
            UiKit.Rect(new Rect(0, 0, w, UiKit.H), new Color(0.01f, 0.03f, 0.06f, 0.82f));
            var (title, text) = story[storyIndex];
            var r = new Rect(w * 0.5f - 640f, 300f, 1280f, 420f);
            UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.96f), UiKit.Steel);
            UiKit.Line($"{storyIndex + 1} / {story.Length}", r.x + 50f, r.y + 40f, 18, UiKit.Mutedish, 0f, 2);
            UiKit.Line(title, r.x + 50f, r.y + 70f, 64, UiKit.Text, 0f, 2);
            var body = new GUIStyle(UiKit.TextStyle(30, 0)) { wordWrap = true };
            UiKit.Label(new Rect(r.x + 50f, r.y + 170f, r.width - 100f, 200f), text, body, UiKit.Soft);
            UiKit.Line("Placeholder slide: the illustrated stills come with the art phases.", w * 0.5f, r.yMax + 24f, 16, UiKit.Mutedish, 0.5f, 1);
            float hy = UiKit.H - 66f, kx = w * 0.5f - 140f;
            kx += UiKit.Keycap(kx, hy, "ENTER") + 8f;
            UiKit.Line("next", kx, hy + 7f, 19, UiKit.Mutedish, 0f, 1); kx += 70f;
            kx += UiKit.Keycap(kx, hy, "ESC") + 8f;
            UiKit.Line("skip", kx, hy + 7f, 19, UiKit.Mutedish, 0f, 1);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0) { Event.current.Use(); storyIndex++; if (storyIndex >= story.Length) FinishStory(); }
        }

        void DrawStageClear(float w)
        {
            var s = Campaign.Active;
            if (s == null) return; // leaving for the map: the scene is reloading
            Plate(w, 70f, $"STAGE {StageLabel(s)}  ·  CLEARED", Palette.Hex("#3BD16F"), s.name, 52);
            string[] names = { "CLEARED", "NO DAMAGE", $"UNDER {Mathf.RoundToInt(s.parTime)} S" };
            for (int k = 0; k < 3; k++)
            {
                bool on = (stageStars & (1 << k)) != 0;
                var t = new Rect(w * 0.5f - 420f + k * 290f, 250f, 260f, 150f);
                UiKit.ChamferPanel(t, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.92f), on ? Palette.Hex("#FFB020") : new Color(UiKit.Steel.r, UiKit.Steel.g, UiKit.Steel.b, 0.6f));
                UiKit.DrawIcon(new Rect(t.center.x - 28f, t.y + 22f, 56f, 56f), "ui_star", on ? Palette.Hex("#FFB020") : new Color(1f, 1f, 1f, 0.15f));
                UiKit.Line(names[k], t.center.x, t.y + 100f, 20, on ? UiKit.Text : UiKit.Mutedish, 0.5f, 2);
            }
            int m = (int)Game.TimeAlive / 60, sec = (int)Game.TimeAlive % 60;
            UiKit.Line($"TIME {m}:{sec:00}   ·   HITS TAKEN {Game.HitsTaken}   ·   KILLS {Game.Kills}   ·   +{stageChips} CHIPS", w * 0.5f, 440f, 22, UiKit.Soft, 0.5f, 2);
            var next = Campaign.Next(s);
            UiKit.Line(next != null ? $"CONTINUE: pick an upgrade, then {StageLabel(next)} {next.name}" : "CONTINUE: deck complete, back to the station map",
                w * 0.5f, 486f, 18, UiKit.Mutedish, 0.5f, 1);
            ActivateStageClear(stageClearMenu.Draw(w * 0.5f, 540f, 560f, 80f));
            DrawRadio(w);
        }

        void DrawStageFailed(float w)
        {
            var s = Campaign.Active;
            if (s == null) return; // leaving for the map: the scene is reloading
            string where = tutorial != null ? $"STEP {tutorial.Step + 1} / {Tutorial.Steps}" : $"WAVE {Game.Wave} / {s.waves.Length}";
            Plate(w, 70f, $"STAGE {StageLabel(s)}  ·  {where}", Palette.Hex("#FF6A3D"), "PIP OFFLINE", 52);
            var r = new Rect(w * 0.5f - 460f, 250f, 920f, 130f);
            UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.94f), Palette.Hex("#FFB020"));
            UiKit.Line(Campaign.Vega, r.x + 28f, r.y + 20f, 16, Palette.Hex("#FFB020"), 0f, 2);
            var body = new GUIStyle(UiKit.TextStyle(26, 0)) { wordWrap = true };
            UiKit.Label(new Rect(r.x + 28f, r.y + 50f, r.width - 56f, 80f), deathLine ?? "", body, UiKit.Text);
            UiKit.Line($"You keep your upgrades from the start of this stage.  ·  Attempt {Campaign.Deaths + 1} next.", w * 0.5f, 410f, 18, UiKit.Mutedish, 0.5f, 1);
            ActivateStageFail(stageFailMenu.Draw(w * 0.5f, 470f, 540f, 80f));
            float hy = UiKit.H - 66f, kx = w * 0.5f - 90f;
            kx += UiKit.Keycap(kx, hy, "R") + 10f;
            UiKit.Line("retry instantly", kx, hy + 7f, 19, UiKit.Mutedish, 0f, 1);
        }

        static string StageLabel(StageDef s)
        {
            var d = Campaign.DeckOf(s);
            return $"{System.Array.IndexOf(Campaign.Decks, d) + 1}-{System.Array.IndexOf(d.stages, s) + 1}";
        }

        /// <summary>Tutorial: the current step's instruction under the banner, and its stronger hint once stuck.</summary>
        void DrawTutorial(float w)
        {
            if (tutorial == null || tutorial.Instruction == null || Game.Won || Game.Lost) return;
            float iw = Mathf.Max(UiKit.TextWidth(tutorial.Instruction, 30) + 80f, 520f);
            var r = new Rect(w * 0.5f - iw * 0.5f, 104f, iw, 92f); // top of the screen: clear of Pip and its pop-up text
            UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.9f), UiKit.Cyan);
            UiKit.Line($"TUTORIAL  ·  STEP {tutorial.Step + 1} / {Tutorial.Steps}", w * 0.5f, r.y + 14f, 15, UiKit.Cyan, 0.5f, 2);
            UiKit.Line(tutorial.Instruction, w * 0.5f, r.y + 40f, 30, UiKit.Text, 0.5f, 2, 2f);
            if (tutorial.IsStuck && tutorial.StuckHint != null)
                UiKit.Line(tutorial.StuckHint, w * 0.5f, r.yMax + 12f, 20, Palette.Hex("#FFB020"), 0.5f, 1, 2f);
        }

        /// <summary>Vega on the radio: bottom-left panel, the line typed out, a few seconds each.</summary>
        void DrawRadio(float w)
        {
            radioT -= Time.unscaledDeltaTime;
            if ((radioLine == null || radioT <= 0f) && radio.Count > 0) { radioLine = radio.Dequeue(); radioT = 2.2f + radioLine.Length * 0.045f; Sound.Play(Sfx.UiMove, 0.4f, 0f); }
            if (radioLine == null || radioT <= 0f) { radioLine = null; return; }
            float total = 2.2f + radioLine.Length * 0.045f, age = total - radioT;
            int shown = Mathf.Clamp(Mathf.FloorToInt(age * 60f), 0, radioLine.Length);
            float a = Mathf.Clamp01(radioT * 3f);
            var r = new Rect(40f, UiKit.H - 330f, 620f, 116f);
            UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.9f * a), new Color(1f, 0.69f, 0.13f, a));
            UiKit.Line(Campaign.Vega, r.x + 22f, r.y + 14f, 15, new Color(1f, 0.69f, 0.13f, a), 0f, 2);
            var body = new GUIStyle(UiKit.TextStyle(21, 0)) { wordWrap = true };
            UiKit.Label(new Rect(r.x + 22f, r.y + 40f, r.width - 44f, 72f), radioLine.Substring(0, shown), body, new Color(1f, 1f, 1f, a));
        }
    }
}
