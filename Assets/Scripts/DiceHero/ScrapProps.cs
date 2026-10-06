using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Deck 1, Scrap Bay (Art.Theme 4), from SYNTHETIK's scrapyard levels and Portal 2's wrecked facility: warm concrete,
    /// oil stains and debris, hazard paint, sodium lamps, and a hall with ribbed walls and a conveyor instead of space.
    /// Same footprints and roll rules as every theme: a vent box becomes a block of compacted scrap, a pipe rack a rusty pipe.
    /// </summary>
    public static class ScrapProps
    {
        static float H => World.HalfSize;

        public static readonly Color Concrete = Palette.Hex("#8C8A84"), Rust = Palette.Hex("#8A4A26"), Hazard = Palette.Hex("#F2B21E"),
            Sodium = Palette.Hex("#FF9A3C"), Oil = Palette.Hex("#6A6760");

        public static void Decor(Palette pal, Transform root)
        {
            var rng = new System.Random(41);
            var oil = pal.Get("S4Oil", Oil, 0.75f, 0.1f);
            var paint = pal.Get("S4Paint", Hazard, 0.35f, 0.05f);
            var black = pal.Get("S4Black", Palette.Hex("#1B1A18"), 0.35f, 0.05f);
            var crack = pal.Get("S4Crack", Palette.Hex("#6E695F"), 0.2f, 0f);
            // Oil stains and cracked patches.
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3(Rand(rng, H - 1.5f), 0.004f, Rand(rng, H - 1.5f));
                float r = 0.2f + (float)rng.NextDouble() * 0.35f;
                Prim.Make(PrimitiveType.Cylinder, "Oil", root, p, new Vector3(r * 2f, 0.003f, r * (1.2f + (float)rng.NextDouble())), i % 3 == 0 ? crack : oil,
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f));
            }
            // Hazard stripes along the edge.
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f;
                for (int i = 0; i < World.BoardSize * 2; i++)
                {
                    float a = -H + 0.25f + i * 0.5f;
                    Vector3 p = alongX ? new Vector3(a, 0.008f, sign * (H - 0.3f)) : new Vector3(sign * (H - 0.3f), 0.008f, a);
                    Prim.Make(PrimitiveType.Cube, "Hazard", root, p, new Vector3(0.5f, 0.01f, 0.38f), i % 2 == 0 ? paint : black, Quaternion.Euler(0f, alongX ? 0f : 90f, 0f));
                }
            }
            // Faded loading-bay box and a drain grate.
            foreach (float s in new[] { -1f, 1f })
            {
                Prim.Make(PrimitiveType.Cube, "Bay", root, new Vector3(0f, 0.007f, s * 7.6f), new Vector3(9.2f, 0.008f, 0.09f), paint);
                Prim.Make(PrimitiveType.Cube, "Bay", root, new Vector3(s * 4.6f, 0.007f, 0f), new Vector3(0.09f, 0.008f, 15.2f), paint);
            }
            var grate = pal.Get("S4Grate", Palette.Hex("#141311"), 0.3f, 0.7f);
            var slat = pal.Get("S4Slat", Palette.Hex("#6E6A63"), 0.4f, 0.5f);
            foreach (var g in new[] { new Vector3(-7.5f, 0.006f, -3f), new Vector3(7.2f, 0.006f, 2.5f), new Vector3(1.5f, 0.006f, 8.2f) })
            {
                Prim.Make(PrimitiveType.Cube, "Grate", root, g, new Vector3(1.4f, 0.008f, 0.9f), grate);
                for (int k = -4; k <= 4; k++) Prim.Make(PrimitiveType.Cube, "Slat", root, g + new Vector3(k * 0.15f, 0.005f, 0f), new Vector3(0.04f, 0.008f, 0.82f), slat);
            }
            // Debris scattered over the floor (low: never in the way).
            var bits = new[] { pal.Get("S4Bit0", Rust, 0.3f, 0.4f), pal.Get("S4Bit1", Palette.Hex("#6D7378"), 0.4f, 0.6f), pal.Get("S4Bit2", Palette.Hex("#3F6B70"), 0.35f, 0.3f) };
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector3(Rand(rng, H - 0.8f), 0.02f, Rand(rng, H - 0.8f));
                if (new Vector2(p.x, p.z - World.PlayerStart.z).magnitude < 1.5f) continue;
                float s = 0.05f + (float)rng.NextDouble() * 0.12f;
                Prim.Make(PrimitiveType.Cube, "Debris", root, p, new Vector3(s * 1.6f, s * 0.5f, s), bits[i % 3], Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f));
            }
            // Scrap heaps in the corners, outside the play space.
            foreach (var c in new[] { new Vector3(-H + 0.4f, 0f, H - 0.4f), new Vector3(H - 0.4f, 0f, H - 0.4f), new Vector3(-H + 0.4f, 0f, -H + 0.4f), new Vector3(H - 0.4f, 0f, -H + 0.4f) })
                Heap(pal, root, c, rng, 0.9f);
        }

        /// <summary>Vent box footprint: a block of compacted scrap held by yellow straps.</summary>
        public static void Barrier(Palette pal, Transform o)
        {
            var layers = new[] { pal.Get("S4Slab0", Rust, 0.3f, 0.4f), pal.Get("S4Slab1", Palette.Hex("#6D7378"), 0.4f, 0.6f), pal.Get("S4Slab2", Palette.Hex("#3F6B70"), 0.35f, 0.3f), pal.Get("S4Slab3", Palette.Hex("#A0602E"), 0.3f, 0.4f) };
            var strap = pal.Get("S4Strap", Hazard, 0.45f, 0.2f);
            for (int i = 0; i < 4; i++)
                Prim.Make(PrimitiveType.Cube, "Slab", o, new Vector3(((i * 37) % 5 - 2) * 0.012f, 0.05f + i * 0.085f, ((i * 53) % 5 - 2) * 0.012f),
                    new Vector3(0.66f - (i % 2) * 0.03f, 0.085f, 0.64f + (i % 2) * 0.02f), layers[i], Quaternion.Euler(0f, (i % 2 == 0 ? 2f : -3f), 0f));
            foreach (float x in new[] { -0.18f, 0.18f })
                Prim.Make(PrimitiveType.Cube, "Strap", o, new Vector3(x, 0.19f, 0f), new Vector3(0.05f, 0.4f, 0.69f), strap);
            Prim.Make(PrimitiveType.Cube, "Top", o, new Vector3(0f, 0.385f, 0f), new Vector3(0.6f, 0.02f, 0.6f), pal.Get("S4Top", Palette.Hex("#4A4640"), 0.3f, 0.6f));
            Prim.Make(PrimitiveType.Cube, "Lamp", o, new Vector3(0f, 0.4f, 0f), new Vector3(0.12f, 0.03f, 0.12f), pal.Glow("S4Warn", Sodium, 2.4f));
        }

        /// <summary>Pipe rack footprint: a rusty pipe on yellow-black clamps.</summary>
        public static void Conduit(Palette pal, Transform o, bool alongX)
        {
            Quaternion rot = alongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            var pipe = pal.Get("S4Pipe", Palette.Hex("#7A4426"), 0.45f, 0.55f);
            var band = pal.Glow("S4PipeBand", Sodium, 1.2f);
            var clamp = pal.Get("S4Clamp", Hazard, 0.45f, 0.2f);
            var dark = pal.Get("S4ClampDark", Palette.Hex("#1B1A18"), 0.4f, 0.3f);
            Vector3 c = new Vector3(0f, 0.24f, 0f);
            Prim.Make(PrimitiveType.Cylinder, "Pipe", o, c, new Vector3(0.46f, 1.5f, 0.46f), pipe, rot);
            for (int i = -2; i <= 2; i += 2)
                Prim.Make(PrimitiveType.Cylinder, "Band", o, c + axis * (i * 0.55f), new Vector3(0.49f, 0.035f, 0.49f), band, rot);
            foreach (float s in new[] { -1f, -0.33f, 0.33f, 1f })
            {
                Vector3 p = new Vector3(0f, 0.13f, 0f) + axis * (s * 1.3f);
                Prim.Make(PrimitiveType.Cube, "Clamp", o, p, alongX ? new Vector3(0.14f, 0.26f, 0.6f) : new Vector3(0.6f, 0.26f, 0.14f), Mathf.Abs(s) > 0.5f ? clamp : dark);
            }
        }

        /// <summary>Indoors: ribbed hall walls with sodium lamps, a conveyor feeding scrap in at the back, a crane beam.</summary>
        public static void Backdrop(Palette pal, Transform space, System.Random rng)
        {
            var wall = pal.Get("S4Wall", Palette.Hex("#2A2723"), 0.3f, 0.5f);
            var rib = pal.Get("S4Rib", Palette.Hex("#3A3631"), 0.35f, 0.6f);
            var lamp = pal.Glow("S4Lamp", Sodium, 3f);
            var stripe = pal.Get("S4WallStripe", Hazard, 0.4f, 0.1f);
            float d = H + 2.6f, h = 7f;
            // Floor apron between the fence and the walls.
            Prim.Make(PrimitiveType.Cube, "Apron", space, new Vector3(0f, -0.16f, 0f), new Vector3(d * 2f, 0.3f, d * 2f), pal.Get("S4Apron", Palette.Hex("#3A3732"), 0.25f, 0.1f));
            for (int side = 0; side < 3; side++) // north, east, west (south is behind the camera)
            {
                Vector3 n = side == 0 ? Vector3.forward : side == 1 ? Vector3.right : Vector3.left;
                Vector3 along = Vector3.Cross(Vector3.up, n);
                Vector3 c = n * d + Vector3.up * (h * 0.5f - 0.3f);
                Prim.Make(PrimitiveType.Cube, "Wall", space, c, side == 0 ? new Vector3(d * 2f, h, 0.4f) : new Vector3(0.4f, h, d * 2f), wall);
                Prim.Make(PrimitiveType.Cube, "WallStripe", space, n * (d - 0.22f) + Vector3.up * 0.5f, side == 0 ? new Vector3(d * 2f, 0.35f, 0.04f) : new Vector3(0.04f, 0.35f, d * 2f), stripe);
                for (int i = -5; i <= 5; i++)
                {
                    Vector3 p = n * (d - 0.25f) + along * (i * 2.3f) + Vector3.up * (h * 0.5f - 0.3f);
                    Prim.Make(PrimitiveType.Cube, "Rib", space, p, side == 0 ? new Vector3(0.3f, h, 0.3f) : new Vector3(0.3f, h, 0.3f), rib);
                    if (i % 2 == 0) Prim.Make(PrimitiveType.Cube, "Lamp", space, n * (d - 0.42f) + along * (i * 2.3f + 1.15f) + Vector3.up * 3.2f, side == 0 ? new Vector3(0.7f, 0.18f, 0.06f) : new Vector3(0.06f, 0.18f, 0.7f), lamp);
                }
            }
            // Conveyor at the back wall, with scrap on the belt.
            var belt = pal.Get("S4Belt", Palette.Hex("#1B1A18"), 0.3f, 0.4f);
            Prim.Make(PrimitiveType.Cube, "Conveyor", space, new Vector3(0f, 1.1f, d - 1.3f), new Vector3(d * 1.6f, 0.25f, 1.6f), belt);
            Prim.Make(PrimitiveType.Cube, "ConveyorFrame", space, new Vector3(0f, 0.55f, d - 1.3f), new Vector3(d * 1.6f, 0.9f, 1.3f), rib);
            for (int i = 0; i < 9; i++) Heap(pal, space, new Vector3(-d * 0.7f + i * d * 0.175f, 1.25f, d - 1.3f), rng, 0.45f);
            // Crane gantry overhead (high: above the camera's view of the arena).
            Prim.Make(PrimitiveType.Cube, "Gantry", space, new Vector3(0f, h - 0.6f, d - 3.5f), new Vector3(d * 2f, 0.5f, 0.6f), stripe);
            World.AddPointLight(space, new Vector3(-10f, 5f, 10f), Sodium, 22f, 2.6f);
            World.AddPointLight(space, new Vector3(10f, 5f, 10f), Sodium, 22f, 2.2f);
            World.AddPointLight(space, new Vector3(0f, 6f, -12f), Palette.Hex("#FFE2B8"), 20f, 1.2f);
        }

        static void Heap(Palette pal, Transform root, Vector3 at, System.Random rng, float size)
        {
            var mats = new[] { pal.Get("S4Bit0", Rust, 0.3f, 0.4f), pal.Get("S4Bit1", Palette.Hex("#6D7378"), 0.4f, 0.6f), pal.Get("S4Bit2", Palette.Hex("#3F6B70"), 0.35f, 0.3f), pal.Get("S4Strap", Hazard, 0.45f, 0.2f) };
            for (int i = 0; i < 9; i++)
            {
                float s = size * (0.25f + (float)rng.NextDouble() * 0.45f);
                var p = at + new Vector3(Rand(rng, size * 0.7f), s * 0.4f + (i > 5 ? size * 0.3f : 0f), Rand(rng, size * 0.7f));
                Prim.Make(PrimitiveType.Cube, "Scrap", root, p, new Vector3(s * 1.4f, s * 0.6f, s), mats[i % mats.Length],
                    Quaternion.Euler((float)rng.NextDouble() * 30f - 15f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 30f - 15f));
            }
        }

        static float Rand(System.Random rng, float half) => ((float)rng.NextDouble() * 2f - 1f) * half;
    }
}
