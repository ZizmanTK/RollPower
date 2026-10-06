using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Button-mode readout on the floor around the die: one marker per roll direction showing the gun that
    /// roll would put on top (its colour, plus an outline of the gun so colour is never the only cue).
    /// The marker the roll guide recommends glows brighter and pulses. Hidden while rolling.
    /// The "ahead" marker sits further out because the die itself hides the floor behind it from the camera.
    /// </summary>
    public class RollCompass
    {
        readonly Palette pal;
        readonly Transform root;
        readonly Slot[] slots = new Slot[4];
        float t;

        class Slot
        {
            public Transform t;
            public Renderer[] ring;
            public Transform[] glyphs = new Transform[7]; // index = face number
            public Renderer[][] glyphParts = new Renderer[7][];
            public int shown = -1;
            public bool hot;
        }

        public RollCompass(Palette pal)
        {
            this.pal = pal;
            root = new GameObject("RollCompass").transform;
            for (int i = 0; i < 4; i++)
            {
                var s = new Slot { t = new GameObject("Slot" + i).transform };
                s.t.SetParent(root, false);
                s.t.localRotation = Quaternion.LookRotation(RollAdvisor.Directions[i]);
                s.ring = new Renderer[12];
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI * 2f / 12f;
                    var seg = Prim.Make(PrimitiveType.Cube, "Ring", s.t, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.46f,
                        new Vector3(0.05f, 0.015f, 0.13f), null, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
                    s.ring[k] = seg.GetComponent<Renderer>();
                }
                for (int f = 1; f <= 6; f++)
                {
                    var g = new GameObject("Glyph" + f).transform;
                    g.SetParent(s.t, false);
                    Glyph(WeaponDef.All[f].model, g, Mat(f, false));
                    g.gameObject.SetActive(false);
                    s.glyphs[f] = g;
                    s.glyphParts[f] = g.GetComponentsInChildren<Renderer>(true);
                }
                slots[i] = s;
            }
            root.gameObject.SetActive(false);
        }

        Material Mat(int face, bool hot) => hot
            ? pal.Glow("CompassHot" + face, WeaponDef.All[face].color, 4.2f)
            : pal.Glow("Compass" + face, WeaponDef.All[face].color, 1.5f, WeaponDef.All[face].color * 0.15f);

        public void Step(float dt, DiceController dice, RollPlan plan, bool active)
        {
            t += dt;
            bool show = active && DiceController.ButtonMode && !dice.IsRolling;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;
            root.position = dice.transform.position;
            for (int i = 0; i < 4; i++)
            {
                var s = slots[i];
                Vector3 d = RollAdvisor.Directions[i];
                int top = dice.PreviewTop(d);
                bool hot = plan != null && plan.dir == d;
                if (top != s.shown || hot != s.hot)
                {
                    if (s.shown > 0) s.glyphs[s.shown].gameObject.SetActive(false);
                    s.glyphs[top].gameObject.SetActive(true);
                    var m = Mat(top, hot);
                    foreach (var r in s.glyphParts[top]) r.sharedMaterial = m;
                    foreach (var r in s.ring) r.sharedMaterial = m;
                    s.shown = top; s.hot = hot;
                }
                float dist = d.z > 0.5f ? 1.9f : 1.35f;
                float pulse = hot ? 1f + 0.12f * Mathf.Sin(t * 8f) : 1f;
                s.t.localPosition = d * (dist + (hot ? 0.08f * Mathf.Sin(t * 8f) : 0f)) + Vector3.up * 0.02f;
                s.t.localScale = Vector3.one * pulse;
            }
        }

        /// <summary>Flat outline of a gun family (same shapes as the Pip concept), lying on the floor, "up" = away from the die.</summary>
        static void Glyph(int model, Transform g, Material m)
        {
            const float h = 0.02f;
            void Bar(float x, float z, float w, float l, float yaw = 0f) =>
                Prim.Make(PrimitiveType.Cube, "G", g, new Vector3(x, 0f, z), new Vector3(w, h, l), m, Quaternion.Euler(0f, yaw, 0f));
            void Dot(float x, float z, float r) =>
                Prim.Make(PrimitiveType.Cylinder, "G", g, new Vector3(x, 0f, z), new Vector3(r * 2f, h * 0.5f, r * 2f), m);
            switch (model)
            {
                case 1: Bar(0f, 0.04f, 0.1f, 0.62f); Bar(0f, -0.22f, 0.2f, 0.16f); break;                  // rail
                case 2: Bar(-0.1f, 0f, 0.09f, 0.52f); Bar(0.1f, 0f, 0.09f, 0.52f); break;                  // twin
                case 3: foreach (float a in new[] { -24f, 0f, 24f }) Bar(Mathf.Sin(a * Mathf.Deg2Rad) * 0.1f, 0f, 0.08f, 0.48f, a); break; // tri
                case 4:                                                                                     // plasma
                    for (int i = 0; i < 10; i++) { float a = i * 36f * Mathf.Deg2Rad; Bar(Mathf.Cos(a) * 0.2f, Mathf.Sin(a) * 0.2f, 0.07f, 0.12f, -a * Mathf.Rad2Deg); }
                    Dot(0f, 0f, 0.08f); break;
                case 5:                                                                                     // scatter
                    for (int i = 0; i < 5; i++) { float a = (-56f + i * 28f) * Mathf.Deg2Rad; Dot(Mathf.Sin(a) * 0.3f, Mathf.Cos(a) * 0.3f - 0.16f, 0.05f); }
                    break;
                default:                                                                                    // missile pod
                    for (int ix = 0; ix < 3; ix++) for (int iz = 0; iz < 2; iz++) Dot((ix - 1) * 0.15f, (iz - 0.5f) * 0.16f, 0.06f);
                    break;
            }
        }
    }
}
