using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Roll Power's IMGUI look: the Rajdhani typeface (OFL), SVG icons, rounded glass panels and
    /// a menu widget that works with mouse, keyboard and gamepad. Everything is laid out on a
    /// 1080-pixel-tall virtual canvas and scaled to the real screen.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Gold = Palette.Hex("#FFC940");
        public static readonly Color GoldDeep = Palette.Hex("#E08A1E");
        public static readonly Color Ink = Palette.Hex("#0B1A2A");
        public static readonly Color Text = Palette.Hex("#E8EEF8");
        public static readonly Color Muted = Palette.Hex("#8FA0BA");
        public static readonly Color Red = Palette.Hex("#FF3B4E");
        public static readonly Color Blue = Palette.Hex("#4D8BFF");
        public static readonly Color Mint = Palette.Hex("#3DFFB0");
        // Style 7 (upgrade screen) palette, shared by every menu.
        public static readonly Color Cyan = Palette.Hex("#29B6F6"), Steel = Palette.Hex("#2F8FC0"), Soft = Palette.Hex("#B9D2E2"), Mutedish = Palette.Hex("#8FB4CC");

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
            EnsureFonts();
            Body = new GUIStyle { font = fontMedium, fontSize = 26, normal = { textColor = Color.white }, wordWrap = true, richText = true };
            BodyBold = new GUIStyle(Body) { font = fontBold };
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

        // ------------------------------------------------------------------ text (Rajdhani)

        static Font fontMedium, fontSemi, fontBold;
        static readonly Dictionary<long, GUIStyle> textStyles = new Dictionary<long, GUIStyle>();

        static void EnsureFonts()
        {
            if (fontBold != null) return;
            fontMedium = Resources.Load<Font>("Fonts/Rajdhani-Medium");
            fontSemi = Resources.Load<Font>("Fonts/Rajdhani-SemiBold");
            fontBold = Resources.Load<Font>("Fonts/Rajdhani-Bold");
            var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fontMedium == null) fontMedium = legacy;
            if (fontSemi == null) fontSemi = fontMedium;
            if (fontBold == null) fontBold = fontSemi;
        }

        /// <summary>weight: 0 medium, 1 semibold, 2 bold.</summary>
        public static GUIStyle TextStyle(int size, int weight = 2)
        {
            EnsureFonts();
            long key = size * 4L + weight;
            if (textStyles.TryGetValue(key, out var st)) return st;
            st = new GUIStyle
            {
                font = weight == 2 ? fontBold : weight == 1 ? fontSemi : fontMedium,
                fontSize = size, normal = { textColor = Color.white }, clipping = TextClipping.Overflow, wordWrap = false, richText = false,
            };
            textStyles[key] = st;
            return st;
        }

        public static float TextWidth(string s, int size, int weight = 2) => s.Length == 0 ? 0f : TextStyle(size, weight).CalcSize(new GUIContent(s)).x;

        /// <summary>Single-line text whose cap height starts at y. align: 0 left, 0.5 centre, 1 right.</summary>
        public static void Line(string s, float x, float y, int size, Color c, float align = 0f, int weight = 2, float shadow = 0f)
        {
            var st = TextStyle(size, weight);
            float wdt = st.CalcSize(new GUIContent(s)).x;
            var r = new Rect(x - wdt * align, y - size * 0.24f, wdt + 4f, size * 1.4f);
            if (shadow > 0f) Label(new Rect(r.x + shadow, r.y + shadow * 1.4f, r.width, r.height), s, st, new Color(0f, 0f, 0f, 0.6f * c.a));
            Label(r, s, st, c);
        }

        // Legacy pixel-font API, now drawn with Rajdhani: px is the old pixel size (cap height = 7 * px).
        static int SizeFor(float px) => Mathf.Max(10, Mathf.RoundToInt(px * 10.4f));
        static int WeightFor(float px) => px >= 4f ? 2 : 1;

        /// <summary>Width of a headline string at the given (legacy) pixel size.</summary>
        public static float PixelWidth(string s, float px) => TextWidth(s, SizeFor(px), WeightFor(px));

        public static void Pixel(string s, float x, float y, float px, Color c, float align = 0f, float shadow = 0.18f, Color? shadowColor = null)
        {
            if (string.IsNullOrEmpty(s)) return;
            int size = SizeFor(px);
            var st = TextStyle(size, WeightFor(px));
            float wdt = st.CalcSize(new GUIContent(s)).x;
            var r = new Rect(x - wdt * align, y - size * 0.24f, wdt + 4f, size * 1.4f);
            if (shadow > 0f) Label(new Rect(r.x + px * shadow * 2.2f, r.y + px * shadow * 3f, r.width, r.height), s, st, shadowColor ?? new Color(0f, 0f, 0f, 0.55f * c.a));
            Label(r, s, st, c);
        }

        // ------------------------------------------------------------------ icons, diamonds, keycaps, chamfered panels

        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        static Texture2D chamfer, chamferBorder;
        static GUIStyle chamferStyle, chamferBorderStyle;

        /// <summary>An SVG icon from Resources/Icons (imported as a texture).</summary>
        public static Texture2D Icon(string name)
        {
            if (icons.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("Icons/" + name);
            icons[name] = t;
            return t;
        }

        /// <summary>Draws an icon tinted with c, optionally rotated (degrees, clockwise on screen).</summary>
        public static void DrawIcon(Rect r, string name, Color c, float angle = 0f)
        {
            var t = Icon(name);
            if (t == null || Event.current.type != EventType.Repaint) return;
            var m = GUI.matrix;
            if (angle != 0f) Rotate(angle, r.center);
            GUI.color = c;
            GUI.DrawTexture(r, t, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;
            GUI.matrix = m;
        }

        /// <summary>Rotates following GUI drawing around a point on the virtual canvas (GUIUtility.RotateAroundPivot expects screen pixels).</summary>
        static void Rotate(float angle, Vector2 pivot)
        {
            var m = GUI.matrix;
            Vector3 p = m.MultiplyPoint3x4(pivot);
            GUI.matrix = Matrix4x4.Translate(p) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, angle)) * Matrix4x4.Translate(-p) * m;
        }

        /// <summary>A filled diamond (square rotated 45°) whose corners touch a box of the given size.</summary>
        public static void Diamond(Vector2 c, float size, Color col)
        {
            if (Event.current.type != EventType.Repaint) return;
            var m = GUI.matrix;
            Rotate(45f, c);
            float s = size * 0.7071f;
            GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - s * 0.5f, c.y - s * 0.5f, s, s), White);
            GUI.color = Color.white;
            GUI.matrix = m;
        }

        /// <summary>Everspace-style emblem: gold diamond with a dark inner diamond.</summary>
        public static void Emblem(Vector2 c, float size)
        {
            Diamond(c, size + 4f, new Color(0.16f, 0.11f, 0.03f, 1f));
            Diamond(c, size, Palette.Hex("#E8B04A"));
            Diamond(c, size * 0.74f, Palette.Hex("#1A1A1E"));
        }

        /// <summary>A light keycap with a label (keyboard prompt).</summary>
        public static float Keycap(float x, float y, string label, float h = 30f)
        {
            int size = Mathf.RoundToInt(h * 0.6f);
            float wdt = Mathf.Max(h, TextWidth(label, size) + 12f);
            Rect(new Rect(x, y, wdt, h), Palette.Hex("#E6EBF0"));
            Line(label, x + wdt * 0.5f, y + h * 0.22f, size, Palette.Hex("#101418"), 0.5f);
            return wdt;
        }

        /// <summary>Riftbreaker-style panel: navy fill, cut top-left and bottom-right corners, thin border.</summary>
        public static void ChamferPanel(Rect r, Color fill, Color border)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (chamfer == null)
            {
                chamfer = ChamferTex(64, 18, 0f);
                chamferBorder = ChamferTex(64, 18, 2.2f);
                chamferStyle = new GUIStyle { normal = { background = chamfer }, border = new RectOffset(22, 22, 22, 22) };
                chamferBorderStyle = new GUIStyle { normal = { background = chamferBorder }, border = new RectOffset(22, 22, 22, 22) };
            }
            GUI.color = fill;
            chamferStyle.Draw(r, false, false, false, false);
            GUI.color = border;
            chamferBorderStyle.Draw(r, false, false, false, false);
            GUI.color = Color.white;
        }

        static Texture2D ChamferTex(int size, float cut, float border)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            const float k = 0.70710678f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = size - (y + 0.5f); // fy: distance from the top edge
                float d = Mathf.Min(Mathf.Min(fx, size - fx), Mathf.Min(fy, size - fy));
                d = Mathf.Min(d, (fx + fy - cut) * k);                       // top-left cut
                d = Mathf.Min(d, ((size - fx) + (size - fy) - cut) * k);     // bottom-right cut
                float a = Mathf.Clamp01(d + 0.5f);
                if (border > 0f) a *= Mathf.Clamp01(border - d + 0.5f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            return t;
        }

        /// <summary>Horizontal strip that fades out to the right (Everspace objective highlight).</summary>
        public static void FadeStrip(Rect r, Color c, int steps = 12)
        {
            float sw = r.width / steps;
            for (int i = 0; i < steps; i++)
                Rect(new Rect(r.x + i * sw, r.y, sw + 0.5f, r.height), new Color(c.r, c.g, c.b, c.a * (1f - i / (float)steps)));
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
            public string label, icon;
            public Func<float> getValue;   // slider items (0..1) have a getter and setter
            public Action<float> setValue;
            public Func<string> valueText; // toggle / choice items show a value on the right
            public Action<int> cycle;      // left/right on a choice item
            public bool Slider => getValue != null;
        }

        public readonly List<Item> Items = new List<Item>();
        float pulse;
        Vector2 lastMouse;
        int lastDrawFrame = -10;

        public Menu Add(string label, string icon = null) { Items.Add(new Item { label = label, icon = icon }); return this; }
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

        /// <summary>Draws the menu centred at x, starting at y (style 7 rows). Returns the clicked index (or -1).</summary>
        public int Draw(float cx, float y, float width = 560f, float rowH = 74f)
        {
            int clicked = -1;
            var e = Event.current;
            // When a menu (re)appears, remember where the cursor is so a resting mouse doesn't steal the selection.
            if (Time.frameCount - lastDrawFrame > 2) lastMouse = e.mousePosition;
            lastDrawFrame = Time.frameCount;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 4f;
            if (mouseMoved) lastMouse = e.mousePosition;
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
                    UiKit.Glow(new Rect(r.x - 40f, r.y - 30f, r.width + 80f, r.height + 60f), new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, 0.10f + 0.06f * p));
                    UiKit.ChamferPanel(r, new Color(0.06f, 0.16f, 0.25f, 0.97f), UiKit.Cyan);
                    UiKit.Rect(new Rect(r.x + 10f, r.y + 12f, 4f, r.height - 24f), UiKit.Cyan);
                }
                else UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.86f), new Color(UiKit.Steel.r, UiKit.Steel.g, UiKit.Steel.b, 0.7f));

                Color tc = sel ? UiKit.Text : UiKit.Soft;
                int fs = Mathf.RoundToInt(Mathf.Min(30f, r.height * 0.5f));
                float tx = r.x + 30f;
                if (it.icon != null)
                {
                    UiKit.DrawIcon(new Rect(tx, r.center.y - 14f, 28f, 28f), "ui_" + it.icon, sel ? UiKit.Cyan : UiKit.Mutedish);
                    tx += 46f;
                }
                if (it.Slider || it.valueText != null)
                {
                    UiKit.Line(it.label, tx, r.center.y - fs * 0.36f, fs, tc, 0f, 2);
                    float bx = r.xMax - 250f;
                    if (it.Slider)
                    {
                        var bar = new Rect(bx, r.center.y - 7f, 200f, 14f);
                        int filled = Mathf.RoundToInt(it.getValue() * 10f);
                        for (int s = 0; s < 10; s++)
                            UiKit.Rect(new Rect(bar.x + s * 20f, bar.y, 16f, bar.height), s < filled ? (sel ? UiKit.Cyan : UiKit.Steel) : new Color(0f, 0f, 0f, 0.45f));
                        UiKit.Line(Mathf.RoundToInt(it.getValue() * 100f) + "%", r.xMax - 24f, r.center.y - 7f, 19, sel ? UiKit.Text : UiKit.Mutedish, 1f, 2);
                        if (e.type == EventType.MouseDown && e.button == 0 && new Rect(bar.x - 6f, r.y, bar.width + 12f, r.height).Contains(e.mousePosition))
                        {
                            it.setValue(Mathf.Clamp01(Mathf.Round((e.mousePosition.x - bar.x) / bar.width * 10f) / 10f));
                            Sound.Play(Sfx.UiMove, 0.5f, 0f);
                            e.Use();
                        }
                    }
                    else
                    {
                        string v = it.valueText();
                        UiKit.Line(v, r.xMax - 70f, r.center.y - 10f, 26, sel ? UiKit.Cyan : UiKit.Soft, 0.5f, 2);
                        UiKit.DrawIcon(new Rect(r.xMax - 150f, r.center.y - 10f, 20f, 20f), "ui_arrow", sel ? UiKit.Text : UiKit.Mutedish, 180f);
                        UiKit.DrawIcon(new Rect(r.xMax - 30f, r.center.y - 10f, 20f, 20f), "ui_arrow", sel ? UiKit.Text : UiKit.Mutedish);
                        if (e.type == EventType.MouseDown && e.button == 0 && hover)
                        {
                            it.cycle(e.mousePosition.x < r.xMax - 70f ? -1 : 1);
                            Sound.Play(Sfx.UiMove, 0.5f, 0f);
                            e.Use();
                        }
                    }
                }
                else
                {
                    UiKit.Line(it.label, tx, r.center.y - fs * 0.36f, fs, tc, 0f, 2);
                    if (sel) UiKit.Keycap(r.xMax - 26f - (UiKit.TextWidth("ENTER", 16) + 12f), r.center.y - 13f, "ENTER", 26f);
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
