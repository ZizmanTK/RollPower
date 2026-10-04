using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Obstacles, floor decor and backdrop for the navy theme (Art.Theme 3), each built from a referenced game
    /// (see the canvas "Guns & obstacles" page). Footprints match World.Build exactly.
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

        /// <summary>
        /// Roll-1 obstacle, from the SYNTHETIK vent boxes: dark block with ribbed sides, a round fan vent with
        /// radial fins on top, corner bolts, an orange warning label. Thin orange top edge so it reads on navy.
        /// </summary>
        public static void Barrier(Palette pal, Transform o)
        {
            Mats(pal);
            var box = pal.Get("NPBox", Palette.Hex("#141B24"), 0.45f, 0.3f);
            var rib = pal.Get("NPRib", Palette.Hex("#34404F"), 0.5f, 0.3f);
            var bolt = pal.Get("NPBolt", Palette.Hex("#9AA6B4"), 0.6f, 0.5f);
            P(PrimitiveType.Cube, o, new Vector3(0f, 0.005f, 0f), new Vector3(0.9f, 0.01f, 0.9f), pal.Glow("NPPad", Palette.Hex("#FF5A2A"), 0.22f, Palette.Hex("#2A1610")));
            P(PrimitiveType.Cube, o, new Vector3(0f, 0.17f, 0f), new Vector3(0.64f, 0.34f, 0.64f), box);
            for (int i = 0; i < 5; i++)
                P(PrimitiveType.Cube, o, new Vector3(0f, 0.05f + i * 0.055f, 0f), new Vector3(0.66f, 0.018f, 0.66f), rib);
            P(PrimitiveType.Cube, o, new Vector3(0f, 0.341f, 0f), new Vector3(0.66f, 0.012f, 0.66f), orange);
            P(PrimitiveType.Cube, o, new Vector3(0f, 0.345f, 0f), new Vector3(0.6f, 0.012f, 0.6f), box);
            P(PrimitiveType.Cylinder, o, new Vector3(0f, 0.352f, 0f), new Vector3(0.44f, 0.006f, 0.44f), inset);
            for (int i = 0; i < 10; i++)
                P(PrimitiveType.Cube, o, new Vector3(0f, 0.357f, 0f), new Vector3(0.4f, 0.006f, 0.025f), rib, Quaternion.Euler(0f, i * 18f, 0f));
            P(PrimitiveType.Cylinder, o, new Vector3(0f, 0.362f, 0f), new Vector3(0.1f, 0.01f, 0.1f), bolt);
            foreach (float bx in new[] { -0.26f, 0.26f })
            foreach (float bz in new[] { -0.26f, 0.26f })
                P(PrimitiveType.Cylinder, o, new Vector3(bx, 0.356f, bz), new Vector3(0.05f, 0.012f, 0.05f), bolt);
            P(PrimitiveType.Cube, o, new Vector3(-0.12f, 0.12f, -0.33f), new Vector3(0.1f, 0.07f, 0.01f), amber);
        }

        /// <summary>
        /// Roll-2 obstacle, from the SYNTHETIK pipe racks: three grey pipes stacked in a triangle with yellow bands,
        /// open dark pipe ends, dark steel rack frames, and two yellow diamond labels on each end frame.
        /// </summary>
        public static void Conduit(Palette pal, Transform o, bool alongX)
        {
            Mats(pal);
            var pipe = pal.Get("NPPipe", Palette.Hex("#5E6B7C"), 0.5f, 0.4f);
            var rack = pal.Get("NPRack", Palette.Hex("#1E2834"), 0.45f, 0.3f);
            var rackTop = pal.Get("NPRackTop", Palette.Hex("#D8DEE6"), 0.5f, 0.1f);
            var band = pal.Get("NPBand", Palette.Hex("#FFB020"), 0.45f, 0.1f);
            Quaternion rot = alongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);
            Vector3 axis = alongX ? Vector3.right : Vector3.forward, side = alongX ? Vector3.forward : Vector3.right;
            var tubes = new[] { side * -0.12f + Vector3.up * 0.12f, side * 0.12f + Vector3.up * 0.12f, Vector3.up * 0.33f };
            foreach (var c in tubes)
            {
                P(PrimitiveType.Cylinder, o, c, new Vector3(0.23f, 1.42f, 0.23f), pipe, rot);
                foreach (float s in new[] { -1f, 1f })
                    P(PrimitiveType.Cylinder, o, c + axis * (s * 1.425f), new Vector3(0.15f, 0.01f, 0.15f), inset, rot);
                for (int i = -2; i <= 2; i++)
                    P(PrimitiveType.Cylinder, o, c + axis * (i * 0.55f), new Vector3(0.24f, 0.05f, 0.24f), band, rot);
            }
            foreach (float a in new[] { -1.05f, 0f, 1.05f })
            {
                Vector3 p = axis * a;
                foreach (float s in new[] { -1f, 1f })
                    P(PrimitiveType.Cube, o, p + side * (s * 0.25f) + Vector3.up * 0.26f, axis * 0.07f + side * 0.04f + Vector3.up * 0.52f, rack);
                P(PrimitiveType.Cube, o, p + Vector3.up * 0.52f, axis * 0.08f + side * 0.56f + Vector3.up * 0.04f, rackTop);
            }
            foreach (float s in new[] { -1f, 1f })
            foreach (float q in new[] { -0.08f, 0.08f })
                P(PrimitiveType.Cube, o, axis * (s * 1.05f) + side * q + Vector3.up * 0.545f, new Vector3(0.08f, 0.008f, 0.08f), band, Quaternion.Euler(0f, 45f, 0f));
        }

        /// <summary>
        /// Floor and platform decor. Landing ring at the start (Everspace 2 hangar). Painted bay outlines with
        /// diagonal hatching and plus marks (SYNTHETIK floor). Floor grates and round hatch covers (The Ascent).
        /// Canister clusters with yellow bands on the corner outriggers (SYNTHETIK).
        /// </summary>
        public static void Decor(Palette pal, Transform root)
        {
            Mats(pal);
            float H = World.HalfSize;
            var paint = pal.Get("NPPaint", Palette.Hex("#3E5B7E"), 0.5f, 0f);
            var grate = pal.Get("NPGrate", Palette.Hex("#0E1622"), 0.4f, 0.4f);
            var slat = pal.Get("NPSlat", Palette.Hex("#3A4A5E"), 0.5f, 0.4f);
            var hatch = pal.Get("NPHatch", Palette.Hex("#2C4260"), 0.6f, 0.3f);
            var can = pal.Get("NPCan", Palette.Hex("#141B24"), 0.5f, 0.3f);

            // Landing ring around the start point.
            var start = World.PlayerStart;
            int seg = 40;
            for (int i = 0; i < seg; i++)
            {
                if (i % 10 == 9) continue;
                float a = i * 360f / seg;
                var p = start + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.7f;
                P(PrimitiveType.Cube, root, p + Vector3.up * 0.007f, new Vector3(2f * Mathf.PI * 1.7f / seg * 1.05f, 0.008f, 0.07f), trim, Quaternion.Euler(0f, a, 0f));
            }

            // Painted bays: outline, hatched corner, plus marks.
            foreach (var (cx, cz, w, d) in new[] { (-5.5f, 4.5f, 4.2f, 3.2f), (5.5f, -4.6f, 4.2f, 3.2f) })
            {
                var c = new Vector3(cx, 0.006f, cz);
                foreach (float s in new[] { -1f, 1f })
                {
                    P(PrimitiveType.Cube, root, c + new Vector3(0f, 0f, s * d * 0.5f), new Vector3(w, 0.006f, 0.06f), paint);
                    P(PrimitiveType.Cube, root, c + new Vector3(s * w * 0.5f, 0f, 0f), new Vector3(0.06f, 0.006f, d), paint);
                }
                for (int k = 0; k < 5; k++)
                    P(PrimitiveType.Cube, root, c + new Vector3(w * 0.5f - 0.35f - k * 0.22f, 0f, d * 0.5f - 0.35f), new Vector3(0.07f, 0.006f, 0.6f), paint, Quaternion.Euler(0f, 45f, 0f));
                foreach (var (px, pz) in new[] { (-0.6f, -0.4f), (0.7f, 0.3f) })
                {
                    P(PrimitiveType.Cube, root, c + new Vector3(px, 0f, pz), new Vector3(0.3f, 0.006f, 0.05f), paint);
                    P(PrimitiveType.Cube, root, c + new Vector3(px, 0f, pz), new Vector3(0.05f, 0.006f, 0.3f), paint);
                }
            }

            // Floor grates.
            foreach (var (gx, gz, alongX) in new[] { (-7.5f, -1.2f, false), (7.6f, 2.4f, false), (0.9f, 7.9f, true), (-2.8f, -8.1f, true) })
            {
                var c = new Vector3(gx, 0.007f, gz);
                P(PrimitiveType.Cube, root, c, alongX ? new Vector3(2.2f, 0.008f, 0.7f) : new Vector3(0.7f, 0.008f, 2.2f), grate);
                for (int k = -8; k <= 8; k++)
                    P(PrimitiveType.Cube, root, c + (alongX ? new Vector3(k * 0.12f, 0.004f, 0f) : new Vector3(0f, 0.004f, k * 0.12f)),
                        alongX ? new Vector3(0.04f, 0.008f, 0.62f) : new Vector3(0.62f, 0.008f, 0.04f), slat);
            }

            // Round hatch covers with bolts.
            foreach (var (hx, hz) in new[] { (-2.6f, 2.2f), (3.4f, 5.6f), (-6.8f, -6.6f), (6.9f, -0.6f) })
            {
                var c = new Vector3(hx, 0.006f, hz);
                P(PrimitiveType.Cylinder, root, c, new Vector3(0.9f, 0.006f, 0.9f), slat);
                P(PrimitiveType.Cylinder, root, c + Vector3.up * 0.002f, new Vector3(0.78f, 0.006f, 0.78f), hatch);
                for (int k = 0; k < 6; k++)
                    P(PrimitiveType.Cylinder, root, c + Quaternion.Euler(0f, k * 60f, 0f) * Vector3.forward * 0.32f + Vector3.up * 0.004f, new Vector3(0.05f, 0.006f, 0.05f), slat);
            }

            // Canister clusters on the corner outriggers, outside the play area.
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                var b = new Vector3(sx * (H + 1.1f), 0f, sz * (H + 1.1f));
                P(PrimitiveType.Cube, root, b + Vector3.down * 0.25f, new Vector3(1.6f, 0.5f, 1.6f), navy);
                foreach (var (ox, oz, h) in new[] { (-0.35f, -0.3f, 0.7f), (0.3f, -0.35f, 0.55f), (0f, 0.35f, 0.8f) })
                {
                    var p = b + new Vector3(ox, h * 0.5f, oz);
                    P(PrimitiveType.Cylinder, root, p, new Vector3(0.42f, h * 0.5f, 0.42f), can);
                    P(PrimitiveType.Cylinder, root, p + Vector3.up * (h * 0.18f), new Vector3(0.43f, 0.06f, 0.43f), amber);
                    P(PrimitiveType.Cylinder, root, p + Vector3.up * (h * 0.5f), new Vector3(0.3f, 0.01f, 0.3f), inset);
                }
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
