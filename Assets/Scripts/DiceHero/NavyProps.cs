using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Obstacles, floor decor and backdrop for the navy theme (Art.Theme 3), using the Everspace 2 kit:
    /// white armour panels, dark insets, orange trim, cyan lights. The trip-over obstacles carry glowing pips
    /// that say how far they roll the die: barriers show one pip (roll 1), conduits show two (roll 2).
    /// Footprints match World.Build exactly.
    /// </summary>
    public static class NavyProps
    {
        static Material panel, inset, navy, trim, cyan, orange, amber;

        static void Mats(Palette pal)
        {
            panel = pal.Get("NPPanel", Palette.Hex("#E4EAF0"), 0.6f, 0.05f);
            inset = pal.Get("NPInset", Palette.Hex("#2A3442"), 0.5f, 0.2f);
            navy = pal.Get("NPNavy", Palette.Hex("#13263A"), 0.6f, 0.15f);
            trim = pal.Get("NPTrim", Palette.Hex("#FF7A2A"), 0.45f, 0f);
            cyan = pal.Glow("NPCyan", Palette.Hex("#29B6F6"), 1.4f);
            orange = pal.Glow("NPOrange", Palette.Hex("#FF5A2A"), 1.5f);
            amber = pal.Glow("NPAmber", Palette.Hex("#FFB020"), 1.6f);
        }

        static void P(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
            => Prim.Make(t, t.ToString(), parent, pos, scale, m, rot);

        /// <summary>Roll-1 obstacle: white armoured block, orange trim, one glowing pip on top.</summary>
        public static void Barrier(Palette pal, Transform o)
        {
            Mats(pal);
            P(PrimitiveType.Cube, o, new Vector3(0f, 0.005f, 0f), new Vector3(0.92f, 0.01f, 0.92f), pal.Glow("NPPad", Palette.Hex("#FF5A2A"), 0.22f, Palette.Hex("#2A1610")));
            P(PrimitiveType.Cube, o, new Vector3(0f, 0.04f, 0f), new Vector3(0.7f, 0.08f, 0.7f), inset);
            P(PrimitiveType.Cube, o, new Vector3(0f, 0.2f, 0f), new Vector3(0.62f, 0.26f, 0.62f), panel);
            foreach (float a in new[] { 0f, 90f, 180f, 270f })
            {
                var r = Quaternion.Euler(0f, a, 0f);
                P(PrimitiveType.Cube, o, r * new Vector3(0f, 0.18f, 0.311f), new Vector3(0.36f, 0.1f, 0.01f), inset, r);
                P(PrimitiveType.Cube, o, r * new Vector3(0.2f, 0.18f, 0.312f), new Vector3(0.04f, 0.1f, 0.01f), trim, r);
            }
            P(PrimitiveType.Cube, o, new Vector3(0f, 0.32f, 0f), new Vector3(0.64f, 0.03f, 0.64f), trim);
            P(PrimitiveType.Cube, o, new Vector3(0f, 0.338f, 0f), new Vector3(0.44f, 0.01f, 0.44f), navy);
            P(PrimitiveType.Cylinder, o, new Vector3(0f, 0.344f, 0f), new Vector3(0.13f, 0.006f, 0.13f), orange);
        }

        /// <summary>Roll-2 obstacle: dark armoured pipe with white collars, cyan rings, end housings with two pips.</summary>
        public static void Conduit(Palette pal, Transform o, bool alongX)
        {
            Mats(pal);
            Quaternion rot = alongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);
            Vector3 axis = alongX ? Vector3.right : Vector3.forward, side = alongX ? Vector3.forward : Vector3.right;
            Vector3 c = new Vector3(0f, 0.24f, 0f);
            P(PrimitiveType.Cylinder, o, c, new Vector3(0.42f, 1.36f, 0.42f), inset, rot);
            for (int i = -2; i <= 2; i++)
            {
                P(PrimitiveType.Cylinder, o, c + axis * (i * 0.6f), new Vector3(0.5f, 0.06f, 0.5f), panel, rot);
                P(PrimitiveType.Cylinder, o, c + axis * (i * 0.6f + 0.045f), new Vector3(0.505f, 0.012f, 0.505f), trim, rot);
            }
            for (int i = -2; i < 2; i++)
                P(PrimitiveType.Cylinder, o, c + axis * ((i + 0.5f) * 0.6f), new Vector3(0.45f, 0.018f, 0.45f), cyan, rot);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 h = axis * (s * 1.4f);
                P(PrimitiveType.Cube, o, h + Vector3.up * 0.29f, axis * 0.22f + side * 0.6f + Vector3.up * 0.58f, panel);
                P(PrimitiveType.Cube, o, h + Vector3.up * 0.26f + axis * (s * 0.112f), axis * 0.01f + side * 0.44f + Vector3.up * 0.3f, inset);
                P(PrimitiveType.Cube, o, h + Vector3.up * 0.585f, axis * 0.2f + side * 0.58f + Vector3.up * 0.012f, navy);
                foreach (float q in new[] { -0.12f, 0.12f })
                    P(PrimitiveType.Cylinder, o, h + side * q + Vector3.up * 0.593f, new Vector3(0.12f, 0.005f, 0.12f), cyan);
            }
        }

        /// <summary>Floor and platform decor: landing ring at the start, corner pylons, hull lights.</summary>
        public static void Decor(Palette pal, Transform root)
        {
            Mats(pal);
            float H = World.HalfSize;
            // Landing pad ring around the start point (Everspace hangar pad).
            var start = World.PlayerStart;
            int seg = 40;
            for (int i = 0; i < seg; i++)
            {
                if (i % 10 == 9) continue; // gaps like a hangar pad marking
                float a = i * 360f / seg;
                var p = start + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.7f;
                P(PrimitiveType.Cube, root, p + Vector3.up * 0.007f, new Vector3(2f * Mathf.PI * 1.7f / seg * 1.05f, 0.008f, 0.07f), trim, Quaternion.Euler(0f, a, 0f));
            }
            foreach (float a in new[] { 0f, 90f, 180f, 270f })
                P(PrimitiveType.Cube, root, start + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 2.05f + Vector3.up * 0.007f, new Vector3(0.06f, 0.008f, 0.4f), trim, Quaternion.Euler(0f, a, 0f));

            // Corner pylons on small outrigger platforms, outside the play area.
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                var b = new Vector3(sx * (H + 1.1f), 0f, sz * (H + 1.1f));
                P(PrimitiveType.Cube, root, b + Vector3.down * 0.25f, new Vector3(1.6f, 0.5f, 1.6f), navy);
                P(PrimitiveType.Cube, root, b + new Vector3(-sx * 0.6f, -0.2f, 0f), new Vector3(1.0f, 0.2f, 0.4f), inset);
                P(PrimitiveType.Cube, root, b + Vector3.up * 1.2f, new Vector3(0.42f, 2.4f, 0.42f), panel);
                P(PrimitiveType.Cube, root, b + new Vector3(0f, 1.2f, -sz * 0.212f), new Vector3(0.12f, 2f, 0.01f), cyan);
                P(PrimitiveType.Cube, root, b + Vector3.up * 1.9f, new Vector3(0.46f, 0.06f, 0.46f), trim);
                P(PrimitiveType.Cube, root, b + Vector3.up * 2.5f, new Vector3(0.3f, 0.2f, 0.3f), inset);
                P(PrimitiveType.Sphere, root, b + Vector3.up * 2.7f, Vector3.one * 0.18f, amber);
            }
        }

        /// <summary>Everspace 2 backdrop: a bright icy planet, a disc space station with antennas, drifting rocks.</summary>
        public static void Backdrop(Palette pal, Transform space, System.Random rng)
        {
            Mats(pal);
            var planetPos = new Vector3(38f, -34f, 46f);
            P(PrimitiveType.Sphere, space, planetPos, Vector3.one * 52f, pal.Glow("NPPlanet", Palette.Hex("#BFE6FF"), 2.2f, Palette.Hex("#DDF1FF")));
            P(PrimitiveType.Sphere, space, planetPos, Vector3.one * 54.5f, pal.Glow("NPHalo", Palette.Hex("#29B6F6"), 0.18f, Palette.Hex("#0B2236")));

            var st = new GameObject("Station").transform;
            st.SetParent(space, false);
            st.localPosition = new Vector3(-30f, -12f, 34f);
            st.localRotation = Quaternion.Euler(8f, 20f, -6f);
            P(PrimitiveType.Cylinder, st, Vector3.zero, new Vector3(12f, 0.6f, 12f), pal.Get("NPStation", Palette.Hex("#B8C4D0"), 0.5f, 0.1f));
            P(PrimitiveType.Cylinder, st, Vector3.up * 0.65f, new Vector3(8f, 0.4f, 8f), inset);
            P(PrimitiveType.Cylinder, st, Vector3.up * 0.1f, new Vector3(12.2f, 0.15f, 12.2f), cyan);
            P(PrimitiveType.Cube, st, Vector3.up * 2f, new Vector3(2f, 3f, 2f), panel);
            for (int i = 0; i < 4; i++)
            {
                var r = Quaternion.Euler(0f, i * 90f + 30f, 0f);
                P(PrimitiveType.Cube, st, r * new Vector3(0f, 2.5f, 5f), new Vector3(0.3f, 5f, 0.3f), inset);
                P(PrimitiveType.Sphere, st, r * new Vector3(0f, 5.1f, 5f), Vector3.one * 0.35f, amber);
            }

            var rock = pal.Get("NPRock", Palette.Hex("#3A4A5E"), 0.15f, 0.05f);
            for (int i = 0; i < 34; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f, r = World.HalfSize + 4f + (float)rng.NextDouble() * 15f;
                var p = new Vector3(Mathf.Cos(ang) * r, -3f - (float)rng.NextDouble() * 10f, Mathf.Sin(ang) * r + 4f);
                float s = 0.5f + (float)rng.NextDouble() * 2f;
                P(i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube, space, p, new Vector3(s, s * 0.75f, s * 0.9f), rock,
                    Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f));
            }
            World.AddPointLight(space, new Vector3(-12f, 5f, 12f), Palette.Hex("#29B6F6"), 24f, 2.6f);
            World.AddPointLight(space, new Vector3(12f, 5f, 12f), Palette.Hex("#FFB020"), 24f, 1.8f);
            World.AddPointLight(space, new Vector3(0f, 6f, -13f), Palette.Hex("#E6F2FA"), 22f, 1.8f);
        }
    }
}
