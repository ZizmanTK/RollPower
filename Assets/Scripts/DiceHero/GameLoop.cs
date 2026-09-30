using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Drives the whole simulation from one place (dice, guns, projectiles, effects) and draws the HUD.
    /// Controls: WASD / arrows glide, mouse aims, left click or Space fires.
    /// </summary>
    static class GuiStyleExt
    {
        public static GUIStyle Centered(this GUIStyle s) => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter };
    }

    public class GameLoop : MonoBehaviour
    {
        public DiceController Dice { get; private set; }
        public WeaponSystem Weapons { get; private set; }
        public Palette Palette { get; private set; }

        string banner;
        float bannerTime;

        public void Init(DiceController dice, Palette pal)
        {
            Dice = dice;
            Palette = pal;
            Weapons = new WeaponSystem(dice, pal);
            Game = new Game(dice, pal, this);
            Weapons.Fired += d => Sound.Play(Sound.GunSound(d.number), d.number == 2 ? 0.45f : 0.7f);
            dice.Dashed += () => Sound.Play(Sfx.Dash, 0.7f);
            dice.Tripped += ob => Sound.Play(Sfx.Roll, 0.8f);
            dice.TopChanged += (o, n) => ShowBanner($"ROLLED {n}  —  {WeaponDef.All[n].name}");
        }

        public Game Game { get; private set; }

        public void ShowBanner(string text, float seconds = 1.8f) { banner = text; bannerTime = seconds; }

        void Update()
        {
            if (Dice == null) return;
            if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                Dice.Dash();
            Step(Time.deltaTime, true);
        }

        /// <summary>One simulation tick. Guns aim and fire on their own; 'allowFire' lets tests hold fire.</summary>
        public void Step(float dt, bool allowFire)
        {
            bool over = Game.Lost || Game.Won;
            if (over) Dice.InputOverride = Vector2.zero;
            Dice.Step(dt);
            Weapons.Step(dt, allowFire && !over);
            Projectiles.Step(Palette, dt);
            Game.Step(dt);
            Fx.Step(dt);
            bannerTime -= dt;
        }

        static void Restart()
        {
            Projectiles.Clear();
            Targets.All.Clear();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }

        // ---------------- HUD ----------------

        GUIStyle big, mid, small, center;
        Texture2D panel, bar;

        void EnsureStyles()
        {
            if (big != null) return;
            panel = MakeTex(new Color(0.02f, 0.05f, 0.09f, 0.78f));
            bar = MakeTex(Color.white);
            big = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold };
            mid = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            center = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void OnGUI()
        {
            if (Dice == null) return;
            EnsureStyles();
            var def = Weapons.Current;
            float s = Screen.height / 1080f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;

            // Current gun
            GUI.DrawTexture(new Rect(24, 24, 430, 150), panel);
            GUI.color = def.color;
            GUI.Label(new Rect(40, 30, 90, 90), def.number.ToString(), big);
            GUI.Label(new Rect(120, 44, 330, 36), def.name, mid);
            GUI.color = new Color(0.8f, 0.9f, 1f);
            GUI.Label(new Rect(120, 80, 330, 26), def.role, small);
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(40, 130, 398, 8), bar);
            GUI.color = def.color;
            GUI.DrawTexture(new Rect(40, 130, 398 * (1f - Weapons.CooldownFraction), 8), bar);

            // Roll preview: what each trip direction would give.
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(24, 186, 430, 190), panel);
            GUI.color = new Color(0.6f, 0.75f, 0.9f);
            GUI.Label(new Rect(40, 192, 400, 24), "TRIP PREVIEW  (barrier = 1 roll, conduit = 2)", small);
            (string label, Vector3 dir)[] dirs = { ("NORTH ↑", Vector3.forward), ("EAST →", Vector3.right), ("SOUTH ↓", Vector3.back), ("WEST ←", Vector3.left) };
            for (int i = 0; i < dirs.Length; i++)
            {
                int n = Dice.PreviewTop(dirs[i].dir);
                GUI.color = new Color(0.75f, 0.85f, 1f);
                GUI.Label(new Rect(40, 222 + i * 30, 110, 28), dirs[i].label, small);
                GUI.color = WeaponDef.All[n].color;
                GUI.Label(new Rect(150, 219 + i * 30, 300, 28), $"{n}  {WeaponDef.All[n].name}", mid);
            }
            int two = Dice.PreviewTop(Vector3.forward, 2);
            GUI.color = new Color(0.75f, 0.85f, 1f);
            GUI.Label(new Rect(40, 342, 120, 28), "CONDUIT", small);
            GUI.color = WeaponDef.All[two].color;
            GUI.Label(new Rect(150, 339, 300, 28), $"{two}  {WeaponDef.All[two].name}", mid);

            // Health, wave, score (top right)
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(w - 454, 24, 430, 110), panel);
            GUI.color = new Color(0.75f, 0.85f, 1f);
            GUI.Label(new Rect(w - 438, 32, 200, 26), "INTEGRITY", small);
            for (int i = 0; i < Game.MaxHp; i++)
            {
                GUI.color = i < Game.Hp ? new Color(0.25f, 0.95f, 1f) : new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(new Rect(w - 438 + i * 44, 60, 36, 18), bar);
            }
            GUI.color = new Color(0.75f, 0.85f, 1f);
            GUI.Label(new Rect(w - 438, 92, 420, 30), $"WAVE {Mathf.Max(1, Game.Wave)}     ENEMIES {Game.Enemies.Count}     SCORE {Game.Score}", small);

            // Warning when the current gun can't hurt something on the field
            string immune = Game.Immune(def);
            if (immune != null)
            {
                GUI.color = new Color(1f, 0.82f, 0.25f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f));
                GUI.Label(new Rect(0, 220, w, 40), $"{def.name} CAN'T HURT {immune}  —  TRIP TO REROLL", mid.Centered());
            }

            // Damage vignette
            if (Game.HurtFlash > 0f)
            {
                GUI.color = new Color(1f, 0.1f, 0.15f, Game.HurtFlash * 0.8f);
                GUI.DrawTexture(new Rect(0, 0, w, 1080), bar);
            }

            // Banner
            if (bannerTime > 0f)
            {
                GUI.color = new Color(def.color.r, def.color.g, def.color.b, Mathf.Clamp01(bannerTime * 2f));
                GUI.Label(new Rect(0, 150, w, 60), banner, center);
            }

            // Dash + overcharge (bottom centre)
            float bx = w * 0.5f - 200f;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(bx, 1080 - 118, 400, 62), panel);
            float dash = 1f - Mathf.Clamp01(Dice.DashCooldownLeft / Dice.dashCooldown);
            GUI.color = new Color(0.75f, 0.85f, 1f);
            GUI.Label(new Rect(bx + 14, 1080 - 114, 120, 24), dash >= 1f ? "DASH READY" : "DASH", small);
            GUI.color = dash >= 1f ? new Color(0.3f, 1f, 0.8f) : new Color(1f, 1f, 1f, 0.3f);
            GUI.DrawTexture(new Rect(bx + 140, 1080 - 106, 246 * dash, 8), bar);
            if (Weapons.Overcharge > 0f)
            {
                GUI.color = def.color;
                GUI.Label(new Rect(bx + 14, 1080 - 88, 130, 26), "OVERCHARGE", small);
                GUI.DrawTexture(new Rect(bx + 140, 1080 - 80, 246 * Weapons.Overcharge / WeaponSystem.OverchargeTime, 8), bar);
            }

            GUI.color = new Color(0.7f, 0.8f, 0.95f, 0.8f);
            GUI.Label(new Rect(24, 1080 - 40, 1100, 26), "WASD glide   •   SPACE / SHIFT dash   •   Slam into barriers to roll a new gun   •   Guns aim and fire on their own", small);
            GUI.color = Color.white;
            GUI.matrix = m;
        }
    }
}
