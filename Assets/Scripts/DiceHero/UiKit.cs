using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Roll Power's IMGUI look: a chunky 5x7 pixel font for headlines, rounded glass panels and
    /// a menu widget that works with mouse, keyboard and gamepad. Everything is laid out on a
    /// 1080-pixel-tall virtual canvas and scaled to the real screen.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Gold = Palette.Hex("#FFC940");
        public static readonly Color GoldDeep = Palette.Hex("#E08A1E");
        public static readonly Color Ink = Palette.Hex("#0A0E19");
        public static readonly Color Text = Palette.Hex("#E8EEF8");
        public static readonly Color Muted = Palette.Hex("#8FA0BA");
        public static readonly Color Red = Palette.Hex("#FF3B4E");
        public static readonly Color Blue = Palette.Hex("#4D8BFF");
        public static readonly Color Mint = Palette.Hex("#3DFFB0");

        public static Texture2D White { get; private set; }
        static Texture2D rounded, roundedBorder, glow;
        static GUIStyle panelStyle, borderStyle;
        public static GUIStyle Body, BodyBold, Small, Tiny, BodyCenter, SmallCenter;

        /// <summary>Virtual canvas width (height is always 1080).</summary>
        public static float W { get; private set; }
        public const float H = 1080f;
        static float scale;

        public static void Begin()
        {
            Ensure();
            scale = Screen.height / H;
            W = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.color = Color.white;
        }

        public static void End()
        {
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        /// <summary>Mouse position on the virtual canvas.</summary>
        public static Vector2 Mouse => Event.current != null ? Event.current.mousePosition : Vector2.zero;

        static void Ensure()
        {
            if (White != null) return;
            White = Solid(Color.white);
            rounded = RoundRect(48, 14, 0f, 0f);
            roundedBorder = RoundRect(48, 14, 2.2f, 1f);
            glow = RadialGlow(64);
            panelStyle = new GUIStyle { normal = { background = rounded }, border = new RectOffset(16, 16, 16, 16) };
            borderStyle = new GUIStyle { normal = { background = roundedBorder }, border = new RectOffset(16, 16, 16, 16) };
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Body = new GUIStyle { font = font, fontSize = 26, normal = { textColor = Color.white }, wordWrap = true, richText = true };
            BodyBold = new GUIStyle(Body) { fontStyle = FontStyle.Bold };
            Small = new GUIStyle(Body) { fontSize = 21 };
            Tiny = new GUIStyle(Body) { fontSize = 17 };
            BodyCenter = new GUIStyle(BodyBold) { alignment = TextAnchor.MiddleCenter };
            SmallCenter = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter };
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        /// <summary>Anti-aliased rounded square; with border > 0 only the outline ring is filled.</summary>
        static Texture2D RoundRect(int size, float radius, float border, float alpha)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            float h = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - h) - (h - radius), py = Mathf.Abs(y + 0.5f - h) - (h - radius);
                float d = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius; // <0 inside
                float a = Mathf.Clamp01(0.5f - d);
                if (border > 0f) a *= Mathf.Clamp01(d + border + 0.5f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * (border > 0f ? alpha : 1f)));
            }
            t.Apply();
            return t;
        }

        static Texture2D RadialGlow(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ primitives

        public static void Rect(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = Color.white;
        }

        public static void Panel(Rect r, float alpha = 0.84f, Color? border = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, alpha);
            panelStyle.Draw(r, false, false, false, false);
            GUI.color = border ?? new Color(1f, 1f, 1f, 0.09f);
            borderStyle.Draw(r, false, false, false, false);
            GUI.color = Color.white;
        }

        public static void Glow(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, glow);
            GUI.color = Color.white;
        }

        public static void Bar(Rect r, float fill, Color c, Color? back = null)
        {
            Rect(r, back ?? new Color(1f, 1f, 1f, 0.1f));
            if (fill > 0f) Rect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), c);
        }

        public static void Label(Rect r, string text, GUIStyle style, Color c)
        {
            GUI.color = c;
            GUI.Label(r, text, style);
            GUI.color = Color.white;
        }

        /// <summary>Label with a soft drop shadow for text drawn straight over the 3D scene.</summary>
        public static void ShadowLabel(Rect r, string text, GUIStyle style, Color c)
        {
            Label(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), text, style, new Color(0f, 0f, 0f, c.a * 0.7f));
            Label(r, text, style, c);
        }

        // ------------------------------------------------------------------ pixel font

        const int GW = 5, GH = 7;
        static Dictionary<char, string[]> glyphs;

        /// <summary>Width of a pixel-font string at the given pixel size.</summary>
        public static float PixelWidth(string s, float px) => s.Length == 0 ? 0f : (s.Length * (GW + 1) - 1) * px;

        /// <summary>Draws blocky pixel text. 'align' 0 = left, 0.5 = centre, 1 = right of x.</summary>
        public static void Pixel(string s, float x, float y, float px, Color c, float align = 0f, float shadow = 0.18f, Color? shadowColor = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            EnsureGlyphs();
            s = s.ToUpperInvariant();
            x -= PixelWidth(s, px) * align;
            if (shadow > 0f) DrawPixels(s, x + px * shadow * 2.2f, y + px * shadow * 3f, px, shadowColor ?? new Color(0f, 0f, 0f, 0.55f * c.a));
            DrawPixels(s, x, y, px, c);
        }

        static void DrawPixels(string s, float x, float y, float px, Color c)
        {
            GUI.color = c;
            float cx = x;
            foreach (char ch in s)
            {
                if (glyphs.TryGetValue(ch, out var rows))
                    for (int gy = 0; gy < GH; gy++)
                    {
                        string row = rows[gy];
                        int run = -1;
                        for (int gx = 0; gx <= GW; gx++)
                        {
                            bool on = gx < GW && row[gx] == '#';
                            if (on && run < 0) run = gx;
                            if (!on && run >= 0)
                            {
                                // One rect per horizontal run keeps draw calls low.
                                GUI.DrawTexture(new Rect(cx + run * px, y + gy * px, (gx - run) * px + 0.5f, px + 0.5f), White);
                                run = -1;
                            }
                        }
                    }
                cx += (GW + 1) * px;
            }
            GUI.color = Color.white;
        }

        static void EnsureGlyphs()
        {
            if (glyphs != null) return;
            glyphs = new Dictionary<char, string[]>();
            void G(char c, params string[] rows) => glyphs[c] = rows;
            G('A', ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
            G('B', "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.");
            G('C', ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###.");
            G('D', "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.");
            G('E', "#####", "#....", "#....", "####.", "#....", "#....", "#####");
            G('F', "#####", "#....", "#....", "####.", "#....", "#....", "#....");
            G('G', ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####");
            G('H', "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
            G('I', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####");
            G('J', "..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##..");
            G('K', "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#");
            G('L', "#....", "#....", "#....", "#....", "#....", "#....", "#####");
            G('M', "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#");
            G('N', "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#");
            G('O', ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
            G('P', "####.", "#...#", "#...#", "####.", "#....", "#....", "#....");
            G('Q', ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#");
            G('R', "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#");
            G('S', ".####", "#....", "#....", ".###.", "....#", "....#", "####.");
            G('T', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
            G('U', "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
            G('V', "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..");
            G('W', "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#");
            G('X', "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#");
            G('Y', "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..");
            G('Z', "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####");
            G('0', ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###.");
            G('1', "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###.");
            G('2', ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####");
            G('3', "####.", "....#", "....#", ".###.", "....#", "....#", "####.");
            G('4', "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#.");
            G('5', "#####", "#....", "####.", "....#", "....#", "#...#", ".###.");
            G('6', ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###.");
            G('7', "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#...");
            G('8', ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###.");
            G('9', ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###.");
            G(' ', ".....", ".....", ".....", ".....", ".....", ".....", ".....");
            G('-', ".....", ".....", ".....", ".###.", ".....", ".....", ".....");
            G('+', ".....", "..#..", "..#..", "#####", "..#..", "..#..", ".....");
            G('!', "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#..");
            G('?', ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#..");
            G('.', ".....", ".....", ".....", ".....", ".....", ".....", "..#..");
            G(',', ".....", ".....", ".....", ".....", ".....", "..#..", ".#...");
            G(':', ".....", "..#..", ".....", ".....", ".....", "..#..", ".....");
            G('/', "....#", "....#", "...#.", "..#..", ".#...", "#....", "#....");
            G('%', "##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##");
            G('\'', "..#..", "..#..", ".....", ".....", ".....", ".....", ".....");
            G('×', ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", ".....");
            G('*', ".....", ".#.#.", "#####", "#####", ".###.", "..#..", "....."); // heart
            G('<',"...#.", "..#..", ".#...", "#....", ".#...", "..#..", "...#.");
            G('>', ".#...", "..#..", "...#.", "....#", "...#.", "..#..", ".#...");
        }

        // ------------------------------------------------------------------ dice pip icon

        /// <summary>Draws a small die face showing 'n' (used for weapon icons and the logo).</summary>
        public static void DieFace(Rect r, int n, Color body, Color pip)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = body;
            panelStyle.Draw(r, false, false, false, false);
            float s = r.width * 0.19f, o = r.width * 0.26f;
            Vector2 c = r.center;
            foreach (var p in PipLayout(n))
            {
                GUI.color = pip;
                GUI.DrawTexture(new Rect(c.x + p.x * o - s * 0.5f, c.y - p.y * o - s * 0.5f, s, s), rounded);
            }
            GUI.color = Color.white;
        }

        static Vector2[] PipLayout(int n)
        {
            switch (n)
            {
                case 1: return new[] { Vector2.zero };
                case 2: return new[] { new Vector2(-1, 1), new Vector2(1, -1) };
                case 3: return new[] { new Vector2(-1, 1), Vector2.zero, new Vector2(1, -1) };
                case 4: return new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
                case 5: return new[] { new Vector2(-1, -1), new Vector2(1, -1), Vector2.zero, new Vector2(-1, 1), new Vector2(1, 1) };
                default: return new[] { new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 0), new Vector2(1, 1) };
            }
        }
    }

    /// <summary>
    /// A vertical list of menu entries. Mouse hover selects, click activates; arrows / stick move,
    /// Enter / A activates, Left/Right adjust sliders. Returns the activated index (or -1).
    /// </summary>
    public class Menu
    {
        public int Selected;
        public class Item
        {
            public string label;
            public Func<float> getValue;   // slider items (0..1) have a getter and setter
            public Action<float> setValue;
            public Func<string> valueText; // toggle / choice items show a value on the right
            public Action<int> cycle;      // left/right on a choice item
            public bool Slider => getValue != null;
        }

        public readonly List<Item> Items = new List<Item>();
        float pulse;
        Vector2 lastMouse;

        public Menu Add(string label) { Items.Add(new Item { label = label }); return this; }
        public Menu AddSlider(string label, Func<float> get, Action<float> set) { Items.Add(new Item { label = label, getValue = get, setValue = set }); return this; }
        public Menu AddChoice(string label, Func<string> text, Action<int> cycle) { Items.Add(new Item { label = label, valueText = text, cycle = cycle }); return this; }

        /// <summary>Handles non-GUI input (keyboard / gamepad). Call from Update.</summary>
        public int UpdateInput(float unscaledDt)
        {
            pulse += unscaledDt;
            var nav = Controls.Nav(unscaledDt);
            if (nav.y != 0)
            {
                Selected = (Selected - nav.y + Items.Count) % Items.Count;
                Sound.Play(Sfx.UiMove, 0.5f, 0f);
            }
            var it = Items[Selected];
            if (nav.x != 0)
            {
                if (it.Slider) { it.setValue(Mathf.Clamp01(Mathf.Round((it.getValue() + nav.x * 0.1f) * 10f) / 10f)); Sound.Play(Sfx.UiMove, 0.5f, 0f); }
                else if (it.cycle != null) { it.cycle(nav.x); Sound.Play(Sfx.UiMove, 0.5f, 0f); }
            }
            if (Controls.Confirm && !it.Slider)
            {
                if (it.cycle != null) { it.cycle(1); Sound.Play(Sfx.UiMove, 0.5f, 0f); return -1; }
                Sound.Play(Sfx.UiConfirm, 0.7f, 0f);
                return Selected;
            }
            return -1;
        }

        /// <summary>Draws the menu centred at x, starting at y. Returns the clicked index (or -1).</summary>
        public int Draw(float cx, float y, float width = 560f, float rowH = 74f)
        {
            int clicked = -1;
            var e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Repaint) lastMouse = e.mousePosition;
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                var r = new Rect(cx - width * 0.5f, y + i * rowH, width, rowH - 12f);
                bool hover = r.Contains(e.mousePosition);
                if (hover && mouseMoved && Selected != i) { Selected = i; Sound.Play(Sfx.UiMove, 0.4f, 0f); }
                bool sel = Selected == i;

                if (sel)
                {
                    float p = 0.5f + 0.5f * Mathf.Sin(pulse * 5f);
                    UiKit.Glow(new Rect(r.x - 40f, r.y - 30f, r.width + 80f, r.height + 60f), new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.10f + 0.06f * p));
                    UiKit.Panel(r, 0.9f, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.85f));
                    UiKit.DieFace(new Rect(r.x + 16f, r.y + r.height * 0.5f - 17f, 34f, 34f), (i % 6) + 1, UiKit.Gold, UiKit.Ink);
                }
                else UiKit.Panel(r, 0.6f);

                Color tc = sel ? UiKit.Gold : UiKit.Text;
                float px = 5f;
                if (it.Slider || it.valueText != null)
                {
                    UiKit.Pixel(it.label, r.x + 68f, r.center.y - px * 3.5f, px, tc);
                    float bx = r.xMax - 210f;
                    if (it.Slider)
                    {
                        var bar = new Rect(bx, r.center.y - 7f, 180f, 14f);
                        UiKit.Bar(bar, it.getValue(), sel ? UiKit.Gold : UiKit.Muted);
                        UiKit.Pixel(Mathf.RoundToInt(it.getValue() * 100f) + "%", bx - 16f, r.center.y - 3f * 3.5f, 3f, UiKit.Muted, 1f);
                        if (e.type == EventType.MouseDown && e.button == 0 && new Rect(bar.x - 6f, r.y, bar.width + 12f, r.height).Contains(e.mousePosition))
                        {
                            it.setValue(Mathf.Clamp01(Mathf.Round((e.mousePosition.x - bar.x) / bar.width * 10f) / 10f));
                            Sound.Play(Sfx.UiMove, 0.5f, 0f);
                            e.Use();
                        }
                    }
                    else
                    {
                        UiKit.Pixel("< " + it.valueText() + " >", r.xMax - 24f, r.center.y - px * 3.5f, px, sel ? UiKit.Text : UiKit.Muted, 1f);
                        if (e.type == EventType.MouseDown && e.button == 0 && hover)
                        {
                            it.cycle(e.mousePosition.x < r.center.x + 120f ? -1 : 1);
                            Sound.Play(Sfx.UiMove, 0.5f, 0f);
                            e.Use();
                        }
                    }
                }
                else
                {
                    UiKit.Pixel(it.label, r.center.x, r.center.y - px * 3.5f, px, tc, 0.5f);
                    if (e.type == EventType.MouseDown && e.button == 0 && hover)
                    {
                        Selected = i;
                        clicked = i;
                        Sound.Play(Sfx.UiConfirm, 0.7f, 0f);
                        e.Use();
                    }
                }
            }
            return clicked;
        }
    }
}
