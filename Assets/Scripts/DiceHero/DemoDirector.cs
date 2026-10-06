using System.IO;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Player-side capture mode for trailers, itch.io screenshots and smoke tests. Only active with
    /// command-line flags:  -capture &lt;folder&gt;  (screenshots of every screen)  -autoplay  (bot plays)
    /// -godmode (can't die)  -captureTime &lt;seconds&gt;  (quit after, default 240).
    /// The autopilot plays the real game; the director walks through title, how-to, pause, upgrades
    /// and game over, saving a PNG of each.
    /// </summary>
    public class DemoDirector : MonoBehaviour
    {
        GameLoop loop;
        Autopilot bot;
        string dir;
        float t, nextShot = 6f, nextGuide, stateTime, lastDt, limit = 240f;
        static int shots;               // keeps counting across the scene reloads of a campaign walk-through
        static int campaignStep;        // -campaign: 0 story+map, 1 first stage, 2 second stage (dies), 3 map again
        bool pausedOnce, godMode, howToDone;
        int guideShots;
        Screen2 lastState;

        public static bool Requested => Arg("-capture") != null || Flag("-autoplay");

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        static bool Flag(string name) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), name) >= 0;

        public void Begin(GameLoop loop)
        {
            this.loop = loop;
            dir = Arg("-capture");
            if (dir != null) Directory.CreateDirectory(dir);
            if (float.TryParse(Arg("-captureTime"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lim)) limit = lim;
            godMode = Flag("-godmode");
            if (Flag("-mute")) AudioListener.volume = 0f;
            loop.Game.Invincible = godMode;
            loop.ExternalInput = true;
            bot = new Autopilot(loop.Dice, loop.Weapons, loop.Game) { Goal = () => loop.TutorialGoal, ExtraAdvice = () => loop.TutorialAdvice };
            Debug.Log($"[RollPower] demo director: capture={dir ?? "off"} god={godMode} limit={limit}s");
        }

        void Shot(string name)
        {
            if (dir == null) return;
            string path = Path.Combine(dir, $"{shots++:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[RollPower] captured " + path);
        }

        void Update()
        {
            if (loop == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f); // the first frames can take seconds while loading
            t += dt;
            if (loop.State != lastState) { lastState = loop.State; stateTime = 0f; }
            stateTime += dt;
            lastDt = dt;
            if (Flag("-campaign")) { CampaignWalk(dt); return; }
            if (t > limit) { Debug.Log($"[RollPower] demo end: wave {loop.Game.Wave}, score {loop.Game.Score}"); Application.Quit(); return; }

            switch (loop.State)
            {
                case Screen2.Title:
                    if (!howToDone) loop.CoverMode = stateTime < 2.2f;
                    if (Near(1.8f) && !howToDone) Shot("cover");
                    if (Near(2.8f) && !howToDone) Shot("title");
                    if (stateTime > 3.6f && !howToDone) { howToDone = true; loop.DemoSetState(Screen2.HowTo); }
                    else if (howToDone && stateTime > 3f) loop.DemoStart();
                    break;
                case Screen2.HowTo:
                    if (Near(0.8f)) Shot("howto");
                    if (stateTime > 2f) loop.DemoSetState(Screen2.Loadout);
                    break;
                case Screen2.Loadout:
                    if (Near(0.8f)) Shot("loadout");
                    if (stateTime > 2f) loop.DemoSetState(Screen2.Settings);
                    break;
                case Screen2.Settings:
                    if (Near(0.8f)) Shot("settings");
                    if (stateTime > 2f) loop.DemoSetState(Screen2.Title);
                    break;
                case Screen2.Playing:
                    if (t > limit - 7f) { loop.DemoEndRun(); break; } // always capture the game-over screen
                    bot.Step(Mathf.Min(dt, 0.05f));
                    if (t > nextShot) { nextShot = t + 9f; Shot($"wave{loop.Game.Wave}"); }
                    if (!pausedOnce && loop.Game.Wave >= 2 && stateTime > 3f) { pausedOnce = true; loop.DemoSetState(Screen2.Paused); }
                    if (loop.Game.Boss != null && Near(6f)) Shot("boss");
                    if (loop.Guide.Plan != null && guideShots < 3 && t > nextGuide) { guideShots++; nextGuide = t + 12f; Shot("guide"); }
                    break;
                case Screen2.Paused:
                    if (Near(0.5f)) Shot("pause");
                    if (stateTime > 1.5f) loop.DemoSetState(Screen2.Playing);
                    break;
                case Screen2.Upgrade:
                    if (Near(0.7f)) Shot($"upgrade{loop.Game.Wave}");
                    if (stateTime > 1.6f) loop.DemoPickUpgrade(0);
                    break;
                case Screen2.GameOver:
                    if (Near(2.2f)) Shot("gameover");
                    if (stateTime > 3f) { Debug.Log($"[RollPower] demo end: wave {loop.Game.Wave}, score {loop.Game.Score}"); Application.Quit(); }
                    break;
            }
        }

        bool Near(float at) => stateTime >= at && stateTime - lastDt < at;

        /// <summary>-campaign: story slides, station map, a stage played by the bot to its clear screen and card,
        /// the next stage until Pip goes offline (retry screen), then the map again.</summary>
        void CampaignWalk(float dt)
        {
            if (Time.realtimeSinceStartup > limit) { Application.Quit(); return; }
            switch (loop.State)
            {
                case Screen2.Title:
                    if (campaignStep == 0 && Near(1.5f)) { Campaign.SeenIntro = false; loop.DemoOpenCampaign(); }
                    break;
                case Screen2.Story:
                    if (Near(1f)) Shot("story");
                    if (stateTime > 1.8f) { loop.DemoAdvanceStory(); stateTime = 0f; } // same state, next slide
                    break;
                case Screen2.Campaign:
                    if (Near(1.2f)) Shot("map");
                    if (campaignStep == 0 && Near(2.2f)) { campaignStep = 1; loop.DemoLaunchFirstStage(); }
                    if (campaignStep == 3 && Near(2.4f)) { Debug.Log("[RollPower] campaign walk-through done"); Application.Quit(); }
                    break;
                case Screen2.Playing:
                    bot.Step(Mathf.Min(dt, 0.05f));
                    if (Near(3f)) Shot("stage" + campaignStep);
                    if (loop.Guide.Plan != null && guideShots < 2 && t > nextGuide) { guideShots++; nextGuide = t + 10f; Shot("stage_guide"); }
                    if (campaignStep == 2 && stateTime > 9f) loop.Game.DemoKill();
                    break;
                case Screen2.StageClear:
                    if (Near(2f)) Shot("stageclear");
                    if (Near(3f)) { campaignStep = 2; loop.DemoStageMenu(0); }
                    break;
                case Screen2.Upgrade:
                    if (Near(0.8f)) Shot("stagecard");
                    if (Near(1.6f)) loop.DemoPickUpgrade(0);
                    break;
                case Screen2.StageFailed:
                    if (Near(2.2f)) Shot("stagefailed");
                    if (Near(3f)) { campaignStep = 3; loop.DemoStageMenu(1); }
                    break;
            }
        }
    }
}
