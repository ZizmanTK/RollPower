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
        float t, nextShot = 6f, stateTime, limit = 240f;
        int shots;
        bool pausedOnce, godMode, howToDone;
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
            loop.Game.Invincible = godMode;
            loop.ExternalInput = true;
            bot = new Autopilot(loop.Dice, loop.Weapons, loop.Game);
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
            float dt = Time.unscaledDeltaTime;
            t += dt;
            if (loop.State != lastState) { lastState = loop.State; stateTime = 0f; }
            stateTime += dt;
            if (t > limit) { Debug.Log($"[RollPower] demo end: wave {loop.Game.Wave}, score {loop.Game.Score}"); Application.Quit(); return; }

            switch (loop.State)
            {
                case Screen2.Title:
                    if (Near(2.5f) && !howToDone) Shot("title");
                    if (stateTime > 3f && !howToDone) { howToDone = true; loop.DemoSetState(Screen2.HowTo); }
                    else if (stateTime > 3f) loop.DemoStart();
                    break;
                case Screen2.HowTo:
                    if (Near(1f)) Shot("howto");
                    if (stateTime > 1.5f) loop.DemoSetState(Screen2.Title);
                    break;
                case Screen2.Playing:
                    bot.Step(Mathf.Min(dt, 0.05f));
                    if (t > nextShot) { nextShot = t + 9f; Shot($"wave{loop.Game.Wave}"); }
                    if (!pausedOnce && loop.Game.Wave >= 2 && stateTime > 3f) { pausedOnce = true; loop.DemoSetState(Screen2.Paused); }
                    if (loop.Game.Boss != null && Near(6f)) Shot("boss");
                    break;
                case Screen2.Paused:
                    if (Near(0.6f)) Shot("pause");
                    if (stateTime > 1f) loop.DemoSetState(Screen2.Playing);
                    break;
                case Screen2.Upgrade:
                    if (Near(0.8f)) Shot($"upgrade{loop.Game.Wave}");
                    if (stateTime > 1.2f) loop.DemoPickUpgrade(0);
                    break;
                case Screen2.GameOver:
                    if (Near(2.2f)) Shot("gameover");
                    if (stateTime > 3f) { Debug.Log($"[RollPower] demo end: wave {loop.Game.Wave}, score {loop.Game.Score}"); Application.Quit(); }
                    break;
            }
        }

        bool Near(float at) => stateTime >= at && stateTime - Time.unscaledDeltaTime < at;
    }
}
