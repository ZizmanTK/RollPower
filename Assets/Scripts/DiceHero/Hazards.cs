using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    public enum HazardKind { Spores, Ice, Vent }

    /// <summary>A floor hazard placed by a campaign stage.</summary>
    public struct HazardDef
    {
        public HazardKind kind;
        public Vector2 pos;
        public float size;    // radius (spores) or half side (ice, vents)
        public float offset;  // vents: where in the eruption cycle this one starts (0-1), so rows erupt in a rhythm
        public HazardDef(HazardKind kind, float x, float z, float size, float offset = 0f) { this.kind = kind; pos = new Vector2(x, z); this.size = size; this.offset = offset; }
    }

    /// <summary>
    /// Deck hazards. Spore clouds (Hydroponics) slow Pip but never hurt. Ice (Cryo Mines) makes Pip slide and rolls
    /// travel further. Heat vents (Foundry) glow for a second, then erupt for a second: standing on one then costs 1 hull.
    /// Every vent follows the same 4 s cycle, offset per vent, so they erupt to a beat you can learn.
    /// </summary>
    public static class Hazards
    {
        public const float VentCycle = 4f, VentWarn = 1f, VentBurst = 1f;

        class Live { public HazardDef def; public Transform t; public Renderer[] glow; public float phase; }
        static readonly List<Live> all = new List<Live>();
        static Material ventIdle, ventWarn, ventHot;
        static float clock;

        public static bool Any => all.Count > 0;

        public static void Build(Palette pal, Transform root, HazardDef[] defs)
        {
            all.Clear();
            clock = 0f;
            if (defs == null) return;
            ventIdle = pal.Glow("HzVentIdle", Palette.Hex("#5A2410"), 0.6f);
            ventWarn = pal.Glow("HzVentWarn", Palette.Hex("#FF7A1A"), 2.2f);
            ventHot = pal.Glow("HzVentHot", Palette.Hex("#FFD05A"), 5f);
            foreach (var d in defs)
            {
                var t = new GameObject("Hazard" + d.kind).transform;
                t.SetParent(root, false);
                t.localPosition = new Vector3(d.pos.x, 0f, d.pos.y);
                var l = new Live { def = d, t = t };
                switch (d.kind)
                {
                    case HazardKind.Spores:
                    {
                        var m = pal.Glow("HzSpore", Palette.Hex("#8EE06A"), 0.9f, Palette.Hex("#2C4A20"));
                        Prim.Make(PrimitiveType.Cylinder, "Cloud", t, new Vector3(0f, 0.012f, 0f), new Vector3(d.size * 2f, 0.004f, d.size * 2f), pal.Get("HzSporeFloor", Palette.Hex("#3E5A2C"), 0.2f, 0f));
                        var rng = new System.Random((int)(d.pos.x * 31 + d.pos.y * 17));
                        for (int i = 0; i < 14; i++)
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = (float)rng.NextDouble() * d.size * 0.9f;
                            Prim.Make(PrimitiveType.Sphere, "Spore", t, new Vector3(Mathf.Cos(a) * r, 0.3f + (float)rng.NextDouble() * 0.9f, Mathf.Sin(a) * r), Vector3.one * (0.08f + (float)rng.NextDouble() * 0.1f), m);
                        }
                        break;
                    }
                    case HazardKind.Ice:
                        Prim.Make(PrimitiveType.Cube, "Ice", t, new Vector3(0f, 0.008f, 0f), new Vector3(d.size * 2f, 0.012f, d.size * 2f), pal.Get("HzIce", Palette.Hex("#BFE6F5"), 0.95f, 0.1f));
                        Prim.Make(PrimitiveType.Cube, "IceEdge", t, new Vector3(0f, 0.006f, 0f), new Vector3(d.size * 2f + 0.12f, 0.01f, d.size * 2f + 0.12f), pal.Glow("HzIceEdge", Palette.Hex("#7FD8FF"), 1.2f));
                        break;
                    case HazardKind.Vent:
                    {
                        Prim.Make(PrimitiveType.Cube, "VentFrame", t, new Vector3(0f, 0.01f, 0f), new Vector3(d.size * 2f + 0.14f, 0.02f, d.size * 2f + 0.14f), pal.Get("HzVentFrame", Palette.Hex("#1B1714"), 0.4f, 0.7f));
                        var parts = new List<Renderer>();
                        for (int k = -2; k <= 2; k++)
                            parts.Add(Prim.Make(PrimitiveType.Cube, "VentSlot", t, new Vector3(k * d.size * 0.38f, 0.022f, 0f), new Vector3(d.size * 0.24f, 0.01f, d.size * 1.8f), ventIdle).GetComponent<Renderer>());
                        var col = Prim.Make(PrimitiveType.Cylinder, "Plume", t, new Vector3(0f, 1f, 0f), new Vector3(d.size * 1.6f, 1f, d.size * 1.6f), ventHot);
                        col.SetActive(false);
                        parts.Add(col.GetComponent<Renderer>());
                        l.glow = parts.ToArray();
                        break;
                    }
                }
                all.Add(l);
            }
        }

        static bool Inside(Live l, Vector3 p)
        {
            Vector2 d = new Vector2(p.x, p.z) - l.def.pos;
            return l.def.kind == HazardKind.Spores ? d.magnitude < l.def.size : Mathf.Abs(d.x) < l.def.size && Mathf.Abs(d.y) < l.def.size;
        }

        /// <summary>Speed multiplier for Pip at p (spores slow).</summary>
        public static float SpeedMul(Vector3 p)
        {
            foreach (var l in all) if (l.def.kind == HazardKind.Spores && Inside(l, p)) return 0.55f;
            return 1f;
        }

        public static bool OnIce(Vector3 p)
        {
            foreach (var l in all) if (l.def.kind == HazardKind.Ice && Inside(l, p)) return true;
            return false;
        }

        /// <summary>0 idle, 1 warning, 2 erupting.</summary>
        static int VentState(Live l)
        {
            float t = Mathf.Repeat(clock / VentCycle + l.def.offset, 1f) * VentCycle;
            return t < VentCycle - VentWarn - VentBurst ? 0 : t < VentCycle - VentBurst ? 1 : 2;
        }

        /// <summary>Animates vents and returns true when an erupting vent is under p.</summary>
        public static bool Step(float dt, Vector3 p)
        {
            clock += dt;
            bool burn = false;
            foreach (var l in all)
            {
                if (l.def.kind == HazardKind.Spores)
                {
                    l.t.localRotation = Quaternion.Euler(0f, clock * 12f, 0f);
                    continue;
                }
                if (l.def.kind != HazardKind.Vent) continue;
                int s = VentState(l);
                var m = s == 0 ? ventIdle : s == 1 ? ventWarn : ventHot;
                for (int i = 0; i < l.glow.Length - 1; i++) if (l.glow[i].sharedMaterial != m) l.glow[i].sharedMaterial = m;
                var plume = l.glow[l.glow.Length - 1].gameObject;
                if (plume.activeSelf != (s == 2)) plume.SetActive(s == 2);
                if (s == 2) plume.transform.localScale = new Vector3(l.def.size * 1.6f, 1f + 0.15f * Mathf.Sin(clock * 30f), l.def.size * 1.6f);
                if (s == 2 && Inside(l, p)) burn = true;
            }
            return burn;
        }

        /// <summary>Erupting vents near p (for the autopilot): any vent that is warning or erupting within reach.</summary>
        public static bool Danger(Vector3 p)
        {
            foreach (var l in all) if (l.def.kind == HazardKind.Vent && VentState(l) > 0 && Inside(l, p)) return true;
            return false;
        }
    }
}
