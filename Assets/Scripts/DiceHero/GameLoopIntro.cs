using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// First contact. The first time a kind of enemy (or a coat) shows up, the game freezes on a card: its picture,
    /// what protects it and why, and which of Pip's guns gets through (face number and icon). After "got it", the
    /// fight runs at half speed with a ring on that enemy until the gun on top can hurt it (for mites: until one is
    /// crushed), so the first kill is made the right way. Seen once per save (rp.seen.*); off in batch runs.
    /// </summary>
    public partial class GameLoop
    {
        struct IntroDef { public string title, why; public Defence d; }

        static readonly Dictionary<string, IntroDef> Intros = new Dictionary<string, IntroDef>
        {
            ["Crawler"] = new IntroDef { title = "CRAWLER", d = Defence.Bare, why = "Bare circuits: nothing protects it. Any gun works, and landing a roll on it crushes it." },
            ["Drone"] = new IntroDef { title = "DRONE · FLYING", d = Defence.Flying, why = "It hovers above your barrels, so flat shots pass under it. Seekers climb to it, and a shock arc jumps up to it." },
            ["Tank"] = new IntroDef { title = "TANK · STEEL PLATE", d = Defence.Steel, why = "Bolts, fire and missiles bounce off steel. A piercing slug punches through it; an explosion cracks it." },
            ["Mite"] = new IntroDef { title = "MITE · TOO LOW", d = Defence.Low, why = "It hugs the floor under your barrels: no gun can hit it. Roll onto it, Pip weighs a tonne. It chews for a moment before it bites." },
            ["Bomber"] = new IntroDef { title = "BOMBER", d = Defence.Bare, why = "Bare, so any gun hurts it, but it plants bombs. Shove the bombs off the edge before they blow." },
            ["Vines"] = new IntroDef { title = "OVERGROWN · VINES", d = Defence.Vines, why = "Shots pass straight through the leaves. Fire burns the vines off; then the robot inside is bare." },
            ["Ice"] = new IntroDef { title = "FROZEN · ICE SHELL", d = Defence.Ice, why = "Shots skid off the ice. Fire melts it; then the robot inside is bare." },
            ["Shield"] = new IntroDef { title = "SHIELDED · ENERGY FIELD", d = Defence.Shield, why = "The field soaks every shot. A shock overloads it; then the robot inside is bare." },
        };

        string introKey;
        Enemy introEnemy;
        int introPhase;            // 0 none, 1 card (frozen), 2 coaching (half speed)
        float introT;
        Transform introRing;
        int introMitesAtStart;
        readonly HashSet<Enemy> introChecked = new HashSet<Enemy>();

        static bool IntrosOn => Application.isPlaying && !Application.isBatchMode;
        // Scripted captures keep their own list, so they never mark intros as seen on the player's machine.
        static readonly HashSet<string> demoSeen = new HashSet<string>();
        static bool Seen(string key) => DemoDirector.Requested ? demoSeen.Contains(key) : PlayerPrefs.GetInt("rp.seen." + key, 0) == 1;
        static void MarkSeen(string key) { if (DemoDirector.Requested) demoSeen.Add(key); else { PlayerPrefs.SetInt("rp.seen." + key, 1); PlayerPrefs.Save(); } }

        /// <summary>Time multiplier while an intro runs (0 on the card, 0.45 while coaching).</summary>
        float IntroScale => introPhase == 1 ? 0f : introPhase == 2 ? 0.45f : 1f;
        bool IntroBlocking => introPhase == 1;

        static string IntroKey(Enemy e)
        {
            if (e.Coated && (e.coat == Defence.Vines || e.coat == Defence.Ice || e.coat == Defence.Shield)) return e.coat.ToString();
            if (e.defenceOverride.HasValue || e.IsBig) return null;
            switch (e.kind)
            {
                case EnemyKind.Crawler: return "Crawler";
                case EnemyKind.Drone: return "Drone";
                case EnemyKind.Tank: return "Tank";
                case EnemyKind.Mite: return "Mite";
                case EnemyKind.Bomber: return "Bomber";
                default: return null;
            }
        }

        /// <summary>Called every playing frame with real time: starts, advances and ends intros.</summary>
        void StepIntro(float udt)
        {
            if (!IntrosOn || Game == null) return;
            introT += udt;
            if (introPhase == 0)
            {
                if (Game.Lost || Game.Won) return;
                foreach (var e in Game.Enemies)
                {
                    if (!e.Alive || e.spawnT < 1f || introChecked.Contains(e)) continue;
                    introChecked.Add(e);
                    var key = IntroKey(e);
                    if (key == null || Seen(key)) continue;
                    MarkSeen(key);
                    introKey = key; introEnemy = e; introPhase = 1; introT = 0f;
                    introMitesAtStart = Game.Kills;
                    Sound.Play(Sfx.UiConfirm, 0.8f, 0f);
                    Telemetry.Log("intro", "enemy", key, "stage", Campaign.Active?.id);
                    break;
                }
                return;
            }
            if (introPhase == 1)
            {
                bool auto = DemoDirector.Requested && introT > 2.6f;
                if ((introT > 0.35f && Controls.Confirm) || auto) StartCoaching();
                return;
            }
            // Coaching: until the gun on top gets through (or the mite is crushed), at most 8 s.
            var d = Intros[introKey].d;
            bool alive = introEnemy != null && introEnemy.Alive;
            bool solved = d == Defence.Low ? Game.Kills > introMitesAtStart || !alive
                : d == Defence.Bare || !alive || Enemy.Counters(Weapons.Current.type, introEnemy.CurrentDefence) && !Weapons.Current.IsEmpty;
            if (alive && introRing != null) introRing.position = introEnemy.pos + Vector3.up * 0.03f;
            if (solved || introT > 8f || Game.Lost || Game.Won) EndIntro(solved && d != Defence.Low && d != Defence.Bare);
        }

        void StartCoaching()
        {
            introPhase = 2; introT = 0f;
            var d = Intros[introKey].d;
            Color c = d == Defence.Bare || d == Defence.Low ? UiKit.Mint : CoatModels.Tint(d);
            if (introEnemy != null) introRing = Tutorial.MarkerRing(Palette, "IntroRing", introEnemy.pos, c);
            int f = Enemy.CounterFace(d);
            string what = d == Defence.Low ? "ROLL ONTO IT: SPACE THE WAY YOU MOVE" : d == Defence.Bare ? "ANY GUN · OR LAND A ROLL ON IT"
                : f > 0 ? $"ROLL TO THE {WeaponDef.All[f].name} MARKER (FACE {f})" : Enemy.Beaters(d);
            ShowBanner(null, what, 4f, c);
        }

        void EndIntro(bool rightGun)
        {
            if (rightGun) ShowBanner(null, "THAT'S IT: " + Weapons.Current.name + " GETS THROUGH", 2f, UiKit.Mint);
            introPhase = 0; introKey = null; introEnemy = null;
            if (introRing != null) Object.Destroy(introRing.gameObject);
            introRing = null;
        }

        /// <summary>The first-contact card (frozen game).</summary>
        void DrawIntro(float w)
        {
            if (introPhase != 1 || introKey == null) return;
            var def = Intros[introKey];
            UiKit.Rect(new Rect(0, 0, w, UiKit.H), new Color(0.01f, 0.03f, 0.06f, 0.55f));
            var r = new Rect(w * 0.5f - 560f, 250f, 1120f, 420f);
            Color edge = def.d == Defence.Bare || def.d == Defence.Low ? UiKit.Mint : CoatModels.Tint(def.d);
            UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.97f), edge);
            var pic = Resources.Load<Texture2D>("Intro/" + introKey);
            if (pic != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(r.x + 24f, r.y + 40f, 340f, 340f), pic, ScaleMode.ScaleToFit, true);
            float x = r.x + 390f, cw = r.width - 420f;
            UiKit.Line("NEW ENEMY", x, r.y + 34f, 18, edge, 0f, 2);
            UiKit.Line(def.title, x, r.y + 58f, 44, UiKit.Text, 0f, 2);
            var body = new GUIStyle(UiKit.TextStyle(24, 0)) { wordWrap = true };
            UiKit.Label(new Rect(x, r.y + 120f, cw, 100f), def.why, body, UiKit.Soft);
            float y = r.y + 236f;
            UiKit.Line(def.d == Defence.Low ? "WHAT KILLS IT" : "GETS THROUGH", x, y, 16, UiKit.Mutedish, 0f, 2);
            y += 26f;
            if (def.d == Defence.Low)
            {
                float kx = x + UiKit.Keycap(x, y + 6f, "SPACE") + 12f;
                UiKit.Line("a roll that lands on it", kx, y + 12f, 22, UiKit.Mint, 0f, 1);
            }
            else
            {
                float gx = x; int shown = 0;
                for (int n = 1; n <= 6 && shown < 3; n++)
                {
                    var g = WeaponDef.All[n];
                    if (g.IsEmpty || !Enemy.Counters(g.type, def.d)) continue;
                    UiKit.DieFace(new Rect(gx, y, 44, 44), n, g.color, UiKit.Ink);
                    UiKit.DrawIcon(new Rect(gx + 52f, y + 6f, 32f, 32f), GunIcon(g), g.color);
                    UiKit.Line(g.name, gx + 92f, y + 10f, 20, g.color, 0f, 2);
                    gx += 110f + g.name.Length * 11f; shown++;
                }
                if (shown == 0) UiKit.Line(Enemy.Beaters(def.d), x, y + 10f, 20, UiKit.Red, 0f, 2);
            }
            // The gun on top right now, and whether it works.
            var cur = Weapons.Current;
            bool ok = !cur.IsEmpty && (def.d == Defence.Bare || Enemy.Counters(cur.type, def.d));
            string now = def.d == Defence.Low ? "NO GUN REACHES IT" : ok ? $"YOUR {cur.name} WORKS" : $"YOUR {cur.name} WON'T";
            UiKit.Line(now, x, y + 64f, 20, ok ? UiKit.Mint : UiKit.Red, 0f, 2);
            float hy = r.yMax - 46f, kx2 = r.xMax - 230f;
            kx2 += UiKit.Keycap(kx2, hy, "ENTER") + 8f;
            UiKit.Line("got it", kx2, hy + 7f, 19, UiKit.Mutedish, 0f, 1);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && introT > 0.35f) { Event.current.Use(); StartCoaching(); }
        }
    }
}
