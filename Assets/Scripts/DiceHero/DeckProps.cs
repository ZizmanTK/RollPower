using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// The looks of decks 2-4 (Art.Theme 5-7), from the campaign plan's references. Same footprints and roll rules as
    /// every theme; only the dressing changes.
    ///   5 Hydroponics (Portal 2's overgrowth, The Riftbreaker's jungle): dark green deck, moss, planters, grow lamps.
    ///   6 Cryo Mines (Deep Rock Galactic: Survivor, Mindustry's snow): pale ice-blue rock, frost, glowing ore veins, crystals.
    ///   7 Foundry (The Riftbreaker's lava fields, RUINER): black steel, molten channels, slag, red furnace light.
    /// </summary>
    public static class DeckProps
    {
        static float H => World.HalfSize;

        public static Color Floor(int t) => t == 5 ? Palette.Hex("#3E5446") : t == 6 ? Palette.Hex("#9FB7C4") : Palette.Hex("#2E2A28");
        public static Color Accent(int t) => t == 5 ? Palette.Hex("#8EE06A") : t == 6 ? Palette.Hex("#7FD8FF") : Palette.Hex("#FF6A1A");

        public static void Light(int t, Light sun)
        {
            switch (t)
            {
                case 5: sun.color = Palette.Hex("#E8FFE0"); sun.intensity = 1.5f; RenderSettings.ambientSkyColor = new Color(0.26f, 0.36f, 0.28f); RenderSettings.ambientEquatorColor = new Color(0.14f, 0.2f, 0.15f); break;
                case 6: sun.color = Palette.Hex("#E6F4FF"); sun.intensity = 1.45f; RenderSettings.ambientSkyColor = new Color(0.36f, 0.42f, 0.5f); RenderSettings.ambientEquatorColor = new Color(0.22f, 0.27f, 0.33f); break;
                default: sun.color = Palette.Hex("#FFC9A0"); sun.intensity = 1.35f; RenderSettings.ambientSkyColor = new Color(0.32f, 0.2f, 0.16f); RenderSettings.ambientEquatorColor = new Color(0.18f, 0.1f, 0.08f); break;
            }
        }

        public static void Decor(Palette pal, Transform root, int t)
        {
            var rng = new System.Random(50 + t);
            var accent = pal.Glow("D" + t + "Accent", Accent(t), t == 7 ? 2.6f : 1.4f);
            var patchA = pal.Get("D" + t + "PatchA", t == 5 ? Palette.Hex("#4E6B3E") : t == 6 ? Palette.Hex("#D7E8F0") : Palette.Hex("#1E1B1A"), 0.3f, 0f);
            var patchB = pal.Get("D" + t + "PatchB", t == 5 ? Palette.Hex("#33452F") : t == 6 ? Palette.Hex("#86A2B2") : Palette.Hex("#4A2A1C"), 0.3f, 0f);
            // Moss / frost / slag patches.
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3(Rand(rng, H - 1.2f), 0.004f, Rand(rng, H - 1.2f));
                float r = 0.3f + (float)rng.NextDouble() * 0.6f;
                Prim.Make(PrimitiveType.Cylinder, "Patch", root, p, new Vector3(r * 2f, 0.003f, r * (1.3f + (float)rng.NextDouble())), i % 2 == 0 ? patchA : patchB, Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f));
            }
            // Edge treatment: moss strip with grow lamps / frost rim / a molten channel.
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f;
                Vector3 c = alongX ? new Vector3(0f, 0.008f, sign * (H - 0.3f)) : new Vector3(sign * (H - 0.3f), 0.008f, 0f);
                Vector3 len = alongX ? new Vector3(World.BoardSize - 0.4f, 0.01f, 0.32f) : new Vector3(0.32f, 0.01f, World.BoardSize - 0.4f);
                Prim.Make(PrimitiveType.Cube, "Edge", root, c, len, t == 7 ? accent : patchA);
                if (t != 7) Prim.Make(PrimitiveType.Cube, "EdgeLine", root, c + (alongX ? new Vector3(0f, 0.004f, -sign * 0.2f) : new Vector3(-sign * 0.2f, 0.004f, 0f)), alongX ? new Vector3(World.BoardSize - 0.4f, 0.01f, 0.04f) : new Vector3(0.04f, 0.01f, World.BoardSize - 0.4f), accent);
            }
            // Lines across the deck: irrigation channels / ore veins / cracks of glowing metal.
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(Rand(rng, H - 2f), 0.007f, Rand(rng, H - 2f));
                float a = (float)rng.NextDouble() * 180f, l = 1.2f + (float)rng.NextDouble() * 2.4f;
                Prim.Make(PrimitiveType.Cube, "Vein", root, p, new Vector3(0.05f, 0.008f, l), accent, Quaternion.Euler(0f, a, 0f));
                Prim.Make(PrimitiveType.Cube, "Vein", root, p + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, l * 0.5f), new Vector3(0.04f, 0.008f, l * 0.5f), accent, Quaternion.Euler(0f, a + 35f, 0f));
            }
            // Clusters in the corners, outside the play space: plants / crystals / slag heaps.
            foreach (var c in new[] { new Vector3(-H + 0.5f, 0f, H - 0.5f), new Vector3(H - 0.5f, 0f, H - 0.5f), new Vector3(-H + 0.5f, 0f, -H + 0.5f), new Vector3(H - 0.5f, 0f, -H + 0.5f) })
                Cluster(pal, root, c, rng, t, 1f);
        }

        static void Cluster(Palette pal, Transform root, Vector3 at, System.Random rng, int t, float size)
        {
            if (t == 5)
            {
                var leaf = pal.Get("D5Leaf", Palette.Hex("#4FA34A"), 0.35f, 0f);
                var leafDark = pal.Get("D5LeafDark", Palette.Hex("#2E6B33"), 0.3f, 0f);
                for (int i = 0; i < 7; i++)
                    Prim.Make(PrimitiveType.Sphere, "Bush", root, at + new Vector3(Rand(rng, size * 0.6f), size * 0.3f + (float)rng.NextDouble() * 0.3f, Rand(rng, size * 0.6f)), Vector3.one * size * (0.4f + (float)rng.NextDouble() * 0.4f), i % 2 == 0 ? leaf : leafDark);
            }
            else if (t == 6)
            {
                var ice = pal.Get("D6Crystal", Palette.Hex("#D8F2FF"), 0.95f, 0.1f);
                var glow = pal.Glow("D6CrystalGlow", Palette.Hex("#7FD8FF"), 1.8f);
                for (int i = 0; i < 6; i++)
                {
                    float h = size * (0.6f + (float)rng.NextDouble() * 1.1f);
                    Prim.Make(PrimitiveType.Cube, "Crystal", root, at + new Vector3(Rand(rng, size * 0.5f), h * 0.5f, Rand(rng, size * 0.5f)), new Vector3(0.22f, h, 0.22f) * size, i % 3 == 0 ? glow : ice,
                        Quaternion.Euler(Rand(rng, 25f), (float)rng.NextDouble() * 360f, Rand(rng, 25f)));
                }
            }
            else
            {
                var slag = pal.Get("D7Slag", Palette.Hex("#1E1B1A"), 0.3f, 0.6f);
                var hot = pal.Glow("D7Hot", Palette.Hex("#FF6A1A"), 2.2f);
                for (int i = 0; i < 7; i++)
                    Prim.Make(PrimitiveType.Cube, "Slag", root, at + new Vector3(Rand(rng, size * 0.6f), size * 0.2f, Rand(rng, size * 0.6f)), new Vector3(0.5f, 0.3f, 0.4f) * size * (0.6f + (float)rng.NextDouble()), i % 4 == 0 ? hot : slag,
                        Quaternion.Euler(Rand(rng, 15f), (float)rng.NextDouble() * 360f, Rand(rng, 15f)));
            }
        }

        /// <summary>Vent box footprint: planter / ore block in ice / crucible.</summary>
        public static void Barrier(Palette pal, Transform o, int t)
        {
            if (t == 5)
            {
                Prim.Make(PrimitiveType.Cube, "Planter", o, new Vector3(0f, 0.17f, 0f), new Vector3(0.66f, 0.34f, 0.66f), pal.Get("D5Planter", Palette.Hex("#3A4B52"), 0.5f, 0.5f));
                Prim.Make(PrimitiveType.Cube, "Band", o, new Vector3(0f, 0.3f, 0f), new Vector3(0.68f, 0.05f, 0.68f), pal.Glow("D5Band", Accent(5), 1.4f));
                for (int i = 0; i < 4; i++) Prim.Make(PrimitiveType.Sphere, "Leaf", o, new Vector3((i % 2 - 0.5f) * 0.3f, 0.45f, (i / 2 - 0.5f) * 0.3f), Vector3.one * 0.32f, pal.Get("D5Leaf", Palette.Hex("#4FA34A"), 0.35f, 0f));
            }
            else if (t == 6)
            {
                Prim.Make(PrimitiveType.Cube, "Ore", o, new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.3f, 0.5f), pal.Get("D6Ore", Palette.Hex("#3A4F63"), 0.4f, 0.5f));
                Prim.Make(PrimitiveType.Cube, "Ice", o, new Vector3(0f, 0.2f, 0f), new Vector3(0.66f, 0.4f, 0.66f), pal.Get("D6IceBlock", Palette.Hex("#CFEAF5"), 0.95f, 0.1f), Quaternion.Euler(0f, 6f, 0f));
                Prim.Make(PrimitiveType.Cube, "Vein", o, new Vector3(0f, 0.41f, 0f), new Vector3(0.4f, 0.02f, 0.06f), pal.Glow("D6Vein", Accent(6), 2f), Quaternion.Euler(0f, 30f, 0f));
            }
            else
            {
                Prim.Make(PrimitiveType.Cube, "Crucible", o, new Vector3(0f, 0.18f, 0f), new Vector3(0.66f, 0.36f, 0.66f), pal.Get("D7Iron", Palette.Hex("#2A2624"), 0.4f, 0.8f));
                Prim.Make(PrimitiveType.Cube, "Melt", o, new Vector3(0f, 0.365f, 0f), new Vector3(0.52f, 0.02f, 0.52f), pal.Glow("D7Melt", Accent(7), 3f));
                foreach (float x in new[] { -0.34f, 0.34f }) Prim.Make(PrimitiveType.Cube, "Lug", o, new Vector3(x, 0.26f, 0f), new Vector3(0.06f, 0.1f, 0.3f), pal.Get("D7Lug", Palette.Hex("#5A4A40"), 0.4f, 0.7f));
            }
        }

        /// <summary>Pipe rack footprint: irrigation main / insulated pipe / hot pipe.</summary>
        public static void Conduit(Palette pal, Transform o, bool alongX, int t)
        {
            Quaternion rot = alongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            var pipe = pal.Get("D" + t + "Pipe", t == 5 ? Palette.Hex("#D9E3E6") : t == 6 ? Palette.Hex("#EEF4F7") : Palette.Hex("#4A2418"), 0.6f, 0.4f);
            var band = pal.Glow("D" + t + "PipeBand", Accent(t), t == 7 ? 2.6f : 1.5f);
            var clamp = pal.Get("D" + t + "Clamp", Palette.Hex("#2A3138"), 0.5f, 0.6f);
            Vector3 c = new Vector3(0f, 0.24f, 0f);
            Prim.Make(PrimitiveType.Cylinder, "Pipe", o, c, new Vector3(0.46f, 1.5f, 0.46f), pipe, rot);
            for (int i = -2; i <= 2; i += 2) Prim.Make(PrimitiveType.Cylinder, "Band", o, c + axis * (i * 0.55f), new Vector3(0.49f, 0.035f, 0.49f), band, rot);
            foreach (float s in new[] { -1f, 1f })
                Prim.Make(PrimitiveType.Cube, "Clamp", o, new Vector3(0f, 0.13f, 0f) + axis * (s * 1.3f), alongX ? new Vector3(0.14f, 0.26f, 0.6f) : new Vector3(0.6f, 0.26f, 0.14f), clamp);
        }

        /// <summary>Indoors: a hall with ribbed walls and lamps in the deck's colour, plus one feature at the back wall.</summary>
        public static void Backdrop(Palette pal, Transform space, System.Random rng, int t)
        {
            var wall = pal.Get("D" + t + "Wall", t == 5 ? Palette.Hex("#1F2A24") : t == 6 ? Palette.Hex("#2A3642") : Palette.Hex("#1A1413"), 0.3f, 0.5f);
            var rib = pal.Get("D" + t + "Rib", t == 5 ? Palette.Hex("#2E3B33") : t == 6 ? Palette.Hex("#3E4E5E") : Palette.Hex("#2A201D"), 0.35f, 0.6f);
            var lamp = pal.Glow("D" + t + "Lamp", t == 5 ? Palette.Hex("#E6FFD6") : t == 6 ? Palette.Hex("#BDEBFF") : Palette.Hex("#FF6A1A"), 2.6f);
            float d = H + 2.6f, h = 7f;
            Prim.Make(PrimitiveType.Cube, "Apron", space, new Vector3(0f, -0.16f, 0f), new Vector3(d * 2f, 0.3f, d * 2f), pal.Get("D" + t + "Apron", Floor(t) * 0.7f + Color.black * 0.3f, 0.25f, 0.1f));
            for (int side = 0; side < 3; side++)
            {
                Vector3 n = side == 0 ? Vector3.forward : side == 1 ? Vector3.right : Vector3.left;
                Vector3 along = Vector3.Cross(Vector3.up, n);
                Prim.Make(PrimitiveType.Cube, "Wall", space, n * d + Vector3.up * (h * 0.5f - 0.3f), side == 0 ? new Vector3(d * 2f, h, 0.4f) : new Vector3(0.4f, h, d * 2f), wall);
                for (int i = -5; i <= 5; i++)
                {
                    Prim.Make(PrimitiveType.Cube, "Rib", space, n * (d - 0.25f) + along * (i * 2.3f) + Vector3.up * (h * 0.5f - 0.3f), new Vector3(0.3f, h, 0.3f), rib);
                    if (i % 2 == 0) Prim.Make(PrimitiveType.Cube, "Lamp", space, n * (d - 0.42f) + along * (i * 2.3f + 1.15f) + Vector3.up * 3.2f, side == 0 ? new Vector3(0.7f, 0.18f, 0.06f) : new Vector3(0.06f, 0.18f, 0.7f), lamp);
                }
            }
            // Back-wall feature: glass grow tanks / an ore face of crystals / two furnace mouths.
            for (int i = -2; i <= 2; i++)
            {
                Vector3 p = new Vector3(i * 4.2f, 0f, d - 1.3f);
                if (t == 5)
                {
                    Prim.Make(PrimitiveType.Cube, "Tank", space, p + Vector3.up * 1.4f, new Vector3(2.4f, 2.8f, 1.2f), pal.Glow("D5Tank", Palette.Hex("#6FD3B0"), 0.6f, Palette.Hex("#1E4A3C")));
                    Cluster(pal, space, p + Vector3.up * 0.2f, rng, 5, 0.9f);
                }
                else if (t == 6) Cluster(pal, space, p, rng, 6, 1.6f);
                else if (i % 2 == 0)
                {
                    Prim.Make(PrimitiveType.Cube, "Furnace", space, p + Vector3.up * 1.6f, new Vector3(3f, 3.2f, 1.4f), wall);
                    Prim.Make(PrimitiveType.Cube, "FurnaceMouth", space, p + new Vector3(0f, 1.2f, -0.72f), new Vector3(1.8f, 1.1f, 0.05f), pal.Glow("D7Mouth", Accent(7), 3.4f));
                }
            }
            Color lc = t == 5 ? Palette.Hex("#B6FF9A") : t == 6 ? Palette.Hex("#9FDCFF") : Palette.Hex("#FF6A1A");
            World.AddPointLight(space, new Vector3(-10f, 5f, 10f), lc, 22f, 2.2f);
            World.AddPointLight(space, new Vector3(10f, 5f, 10f), lc, 22f, 1.8f);
            World.AddPointLight(space, new Vector3(0f, 6f, -12f), Color.white, 20f, 1.1f);
        }

        static float Rand(System.Random rng, float half) => ((float)rng.NextDouble() * 2f - 1f) * half;
    }
}
