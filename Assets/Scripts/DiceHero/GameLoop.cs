using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DiceHero
{
    public enum Screen2 { Title, Playing, Upgrade, Paused, Settings, HowTo, GameOver, Loadout, Campaign, Story, StageClear, StageFailed }

    /// <summary>
    /// Drives the whole simulation from one place (dice, guns, projectiles, bombs, effects), owns the
    /// screen flow (title → play → upgrade pick → game over) and the game-feel layer (hit-stop,
    /// slow motion, screen shake, post-processing pulses). The UI lives in GameLoopUi.cs.
    /// </summary>
    public partial class GameLoop : MonoBehaviour
    {
        /// <summary>Set before reloading the scene to jump straight into a new run.</summary>
        public static bool SkipTitle;

        public DiceController Dice { get; private set; }
        public WeaponSystem Weapons { get; private set; }
        public Palette Palette { get; private set; }
        public Game Game { get; private set; }
        public UpgradeDeck Deck { get; private set; }
        /// <summary>Shows where to roll when the current gun can't hurt what's on the field.</summary>
        public RollGuide Guide { get; private set; }
        public RollCompass Compass { get; private set; }
        public PipFace Face { get; private set; }
        public Screen2 State { get; private set; } = Screen2.Title;

        /// <summary>Batch playtests: pick upgrades automatically instead of showing the cards.</summary>
        public bool AutoPickUpgrades;
        /// <summary>The demo director drives the dice (don't reset its input override).</summary>
        public bool ExternalInput;

        // Hooks for the demo / capture director.
        public void DemoStart() => StartRun();
        public void DemoPickUpgrade(int i) { if (hand != null && hand.Count > 0) PickUpgrade(hand[Mathf.Clamp(i, 0, hand.Count - 1)]); }
        public void DemoSetState(Screen2 s)
        {
            if (s == Screen2.Paused || (s == Screen2.Playing && State == Screen2.Paused)) { SetPaused(s == Screen2.Paused); return; }
            if (s == Screen2.HowTo) howToReturn = State;
            State = s;
        }

        CameraFollow cam;
        float hitStop, slowMo = 1f, deathTimer, upgradeDelay, heartbeat, screenFlash;
        List<UpgradeDef> hand;
        Screen2 settingsReturn, howToReturn;
        bool newBest;

        // Post-processing pulses
        Vignette vignette;
        ChromaticAberration chroma;
        float baseVignette, baseChroma;

        public void Init(DiceController dice, Palette pal)
        {
            RunStats.Current = new RunStats();
            Dice = dice;
            Palette = pal;
            Weapons = new WeaponSystem(dice, pal);
            Guide = new RollGuide(pal);
            Compass = new RollCompass(pal);
            Face = dice.Model.IsPip ? new PipFace(dice.Model) : null;
            Game = new Game(dice, pal, this, System.Environment.TickCount);
            Deck = new UpgradeDeck(System.Environment.TickCount + 7);
            Weapons.Fired += d => Sound.Play(Sound.GunSound(d.model), d.model == 2 ? 0.45f : 0.7f);
            dice.Dashed += () => Sound.Play(Sfx.Dash, 0.7f);
            dice.Tripped += ob => Sound.Play(Sfx.Roll, 0.8f);
            dice.RollBlocked += () => Sound.Play(Sfx.Clonk, 0.5f);
            // Button rolls are free, so only a roll to a gun that hurts more of the field earns the overcharge.
            Weapons.OverchargeIf = (o, n) => !DiceController.ButtonMode || RollAdvisor.Value(WeaponDef.All[n], Game) > RollAdvisor.Value(WeaponDef.All[o], Game);
            dice.TopChanged += (o, n) => Fx.Text(dice.transform.position + Vector3.up * 2.4f, WeaponDef.All[n].name, WeaponDef.All[n].color, 1f, 0.8f);
            Game.WaveCleared += w => { upgradeDelay = 0.9f; hand = Deck.Deal(3); };
            cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            BuildMenus();
            InitCampaign();
            FindPostFx();
            if (Application.isPlaying)
            {
                Fx.Density = Application.platform == RuntimePlatform.WebGLPlayer ? 0.6f : 1f;
                if (DemoDirector.Requested) gameObject.AddComponent<DemoDirector>().Begin(this);
                if (SkipTitle) { SkipTitle = false; StartRun(); }
                else
                {
                    EnterTitle();
                    if (OpenLoadout) { OpenLoadout = false; EnterLoadout(); }
                    else if (Campaign.OpenMap) { Campaign.OpenMap = false; EnterMap(); }
                }
            }
        }

        void FindPostFx()
        {
            var vol = FindAnyObjectByType<Volume>();
            if (vol == null || vol.sharedProfile == null || !Application.isPlaying) return;
            var profile = vol.profile; // runtime instance, so pulses never touch the asset
            if (profile.TryGet(out vignette)) baseVignette = vignette.intensity.value;
            if (profile.TryGet(out chroma)) baseChroma = chroma.intensity.value;
        }

        // ---------------- Flow ----------------

        void EnterTitle()
        {
            State = Screen2.Title;
            if (cam != null) cam.Orbit = true;
            Sound.Duck(false);
        }

        void StartRun()
        {
            if (!InStage) Telemetry.Log("run_start", "mode", "gauntlet", "loadout", string.Join(",", Loadout.Faces, 1, 6));
            State = Screen2.Playing;
            if (cam != null) cam.Orbit = false;
            Sound.Duck(false);
        }

        void SetPaused(bool on)
        {
            State = on ? Screen2.Paused : Screen2.Playing;
            pauseMenu.Selected = 0;
            Sound.Duck(on);
        }

        static void Reload(bool skipTitle)
        {
            Settings.Save();
            SkipTitle = skipTitle;
            Projectiles.Clear();
            Targets.All.Clear();
            Fx.Clear();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }

        void PickUpgrade(UpgradeDef u)
        {
            Deck.Take(u, RunStats.Current);
            if (u.id == "hp") Game.RepairFull();
            Sound.Play(Sfx.Upgrade, 0.8f, 0f);
            Fx.Text(Dice.transform.position + Vector3.up * 2.2f, u.name, u.color, 1.2f, 0.75f);
            hand = null;
            Telemetry.Log("card_pick", "card", u.id, "stage", Campaign.Active?.id, "wave", Game.Wave);
            if (InStage && Game.Won) { AfterStageCard(); return; } // campaign: the card comes between stages
            Game.StartNextWave();
            if (State == Screen2.Upgrade) { State = Screen2.Playing; Sound.Duck(false); }
        }

        Color seamBase;
        float shieldGlow = -1f;

        /// <summary>The die's edges flare white while it can't be hurt (rolling, dashing, just landed).</summary>
        void ShieldGlow(float dt)
        {
            var seam = Dice.Model.SeamMaterial;
            if (Dice.Model.IsPip) return; // PipFace colours Pip's seams
            if (seam == null || !seam.HasProperty("_EmissionColor")) return;
            if (shieldGlow < 0f) { seamBase = seam.GetColor("_EmissionColor"); shieldGlow = 0f; }
            shieldGlow = Mathf.MoveTowards(shieldGlow, Dice.Shielded ? 1f : 0f, dt * (Dice.Shielded ? 12f : 4f));
            seam.SetColor("_EmissionColor", Color.Lerp(seamBase, Color.white * 3f * Palette.GlowScale, shieldGlow));
        }

        public void DemoEndRun() => EndRun();

        void EndRun()
        {
            if (InStage) { StageFailed(); return; }
            Telemetry.Log("run_end", "mode", "gauntlet", "wave", Game.Wave, "time", Game.TimeAlive, "hits", Game.HitsTaken,
                "wrong_share", fightTime > 0f ? wrongTime / fightTime : 0f, "rolls", rolls, "useful_rolls", usefulRolls, "dodges", dodges,
                "hints", hintsShown, "hints_followed", hintsFollowed, "kills", Game.Kills);
            State = Screen2.GameOver;
            gameOverMenu.Selected = 0;
            newBest = Game.Score > Settings.BestScore;
            if (newBest) Settings.BestScore = Game.Score;
            if (Game.Wave > Settings.BestWave) Settings.BestWave = Game.Wave;
            if (Game.Wave > 3) Settings.ShowTutorial = false;
            chipsEarned = Loadout.ChipsFor(Game.Wave, Game.BossesBeaten, Game.Kills);
            Loadout.Chips += chipsEarned;
            Settings.Save();
            Sound.Play(Sfx.GameOver, 0.9f, 0f);
            Sound.Duck(true);
            if (cam != null) cam.Orbit = true;
        }

        // ---------------- Juice ----------------

        public void Juice(float shake, float stop)
        {
            if (cam != null) cam.Shake(shake);
            if (shake > 0.5f && cam != null) cam.Kick(shake);
            hitStop = Mathf.Max(hitStop, stop);
        }

        public void FlashScreen(float amount) => screenFlash = Mathf.Max(screenFlash, amount);

        // ---------------- Update ----------------

        void Update()
        {
            if (Dice == null) return;
            float udt = Time.unscaledDeltaTime;
            var before = State;
            MenuInput(udt);
            if (State != before) { UpdatePostFx(udt); return; } // don't let one key press act twice

            switch (State)
            {
                case Screen2.Title:
                case Screen2.Loadout:
                case Screen2.Campaign:
                case Screen2.Story:
                    Dice.InputOverride = Vector2.zero;
                    Dice.Step(udt);
                    Weapons.Step(udt, false);
                    Face?.Step(udt, Dice, null, null);
                    Fx.Step(udt);
                    break;
                case Screen2.Playing:
                    if (!ExternalInput) Dice.InputOverride = null;
                    if (Controls.Pause && !Game.Lost) { SetPaused(true); break; }
                    if (Controls.Dash) Dice.Dash();
                    float dt = Mathf.Min(udt, 1f / 20f);
                    if (hitStop > 0f) { hitStop -= udt; dt = 0f; }
                    if (Game.Lost)
                    {
                        slowMo = Mathf.MoveTowards(slowMo, 0.25f, udt * 3f);
                        deathTimer += udt;
                        if (deathTimer > 1.6f) EndRun();
                    }
                    Step(dt * slowMo, true);
                    if (upgradeDelay > 0f && Game.Intermission)
                    {
                        upgradeDelay -= udt;
                        if (upgradeDelay <= 0f && hand != null)
                        {
                            if (hand.Count == 0) { Game.StartNextWave(); hand = null; } // everything maxed
                            else { State = Screen2.Upgrade; upgradeMenuSel = 1; Sound.Duck(true); Sound.Play(Sfx.UiConfirm, 0.6f, 0f); }
                        }
                    }
                    Heartbeat(udt);
                    break;
                case Screen2.GameOver:
                case Screen2.StageFailed:
                    Dice.InputOverride = Vector2.zero;
                    Step(udt * 0.3f, false);
                    break;
                default:
                    break; // paused, upgrade pick, settings: the world is frozen
            }

            UpdatePostFx(udt);
            screenFlash = Mathf.Max(0f, screenFlash - udt * 2.5f);
            bannerTime -= udt;
        }

        void Heartbeat(float udt)
        {
            if (Game.Lost || Game.Hp > 2) return;
            heartbeat -= udt;
            if (heartbeat <= 0f) { heartbeat = Game.Hp == 1 ? 0.75f : 1.1f; Sound.Play(Sfx.Heartbeat, 0.7f, 0f); }
        }

        void UpdatePostFx(float udt)
        {
            float hurt = Mathf.Clamp01(Game.HurtFlash / 0.45f);
            float low = State == Screen2.Playing && !Game.Lost && Game.Hp <= 2 ? (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (Game.Hp == 1 ? 8f : 5f))) * (Game.Hp == 1 ? 0.18f : 0.1f) : 0f;
            if (chroma != null) chroma.intensity.value = Mathf.Clamp01(baseChroma + hurt * 0.6f + (hitStop > 0f ? 0.3f : 0f));
            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Clamp01(baseVignette + hurt * 0.2f + low);
                vignette.color.value = Color.Lerp(Color.black, new Color(0.6f, 0f, 0.05f), Mathf.Clamp01(hurt + low * 4f));
            }
        }

        /// <summary>One simulation tick. Guns aim and fire on their own; 'allowFire' lets tests hold fire.</summary>
        public void Step(float dt, bool allowFire)
        {
            bool over = Game.Lost || Game.Won;
            if (over) Dice.InputOverride = Vector2.zero;
            Dice.Step(dt);
            ShieldGlow(dt);
            Face?.Step(dt, Dice, Game, Guide.Plan);
            Weapons.Step(dt, allowFire && !over && !Game.Intermission);
            Projectiles.Step(Palette, dt);
            Game.Step(dt);
            Fx.Step(dt);
            Guide.Step(dt, Dice, Game, !Game.Intermission);
            Compass.Step(dt, Dice, Guide.Plan, !Game.Lost);
            if (allowFire) MeasureStep(dt);
            if (AutoPickUpgrades && Game.Intermission && hand != null)
            {
                if (hand.Count > 0) PickUpgrade(hand[0]);
                else { hand = null; Game.StartNextWave(); }
            }
        }

        // ---------------- Banners ----------------

        string bannerTitle, bannerSub;
        Color bannerColor = Color.white;
        float bannerTime, bannerMax = 1f;

        /// <summary>Big centre message. A null title shows only the subtitle line (roll notices).</summary>
        public void ShowBanner(string title, string subtitle, float seconds, Color color)
        {
            // Don't let roll notices cover a wave / boss announcement that's still showing.
            if (title == null && bannerTitle != null && bannerTime > 0.8f) return;
            bannerTitle = title;
            bannerSub = subtitle;
            bannerTime = bannerMax = seconds;
            bannerColor = color;
        }
    }
}
