using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Arena look for the art-direction prototypes (Art.Theme 1-3). Same board size, same barrier and conduit
    /// footprints and roll rules as World.Build; only the dressing changes.
    /// </summary>
    public static class WorldThemes
    {
        const int N = World.BoardSize;
        static float H => World.HalfSize;

        public static Transform Build(Palette pal)
        {
            World.Obstacles.Clear();
            var root = new GameObject("World").transform;
            int t = Art.Theme;

            var (deckA, deckB, gap) = t switch
            {
                1 => (pal.Get("T1DeckA", Palette.Hex("#5E6470"), 0.35f, 0.1f), pal.Get("T1DeckB", Palette.Hex("#6A707C"), 0.35f, 0.1f), Palette.Hex("#9FD8FF")),
                2 => (pal.Get("T2DeckA", Palette.Hex("#4A4F57"), 0.35f, 0.2f), pal.Get("T2DeckB", Palette.Hex("#545961"), 0.35f, 0.2f), Palette.Hex("#FF9A3C")),
                _ => (pal.Get("T3DeckA", Palette.Hex("#07080D"), 0.92f, 0.3f), pal.Get("T3DeckB", Palette.Hex("#0B0D14"), 0.92f, 0.3f), Palette.Hex("#2FE6FF")),
            };
            // One continuous deck surface: no tile pattern.
            Prim.Make(PrimitiveType.Cube, "Deck", root, new Vector3(0f, -0.15f, 0f), new Vector3(N, 0.3f, N), deckA);

            var hullDark = pal.Get("T" + t + "Hull", t == 1 ? Palette.Hex("#1E2330") : Palette.Hex("#111318"), 0.5f, 0.7f);
            var hullMid = pal.Get("T" + t + "HullMid", t == 1 ? Palette.Hex("#2C3242") : Palette.Hex("#1A1D23"), 0.5f, 0.7f);
            Prim.Make(PrimitiveType.Cube, "Hull", root, new Vector3(0f, -0.9f, 0f), new Vector3(N + 0.4f, 1.2f, N + 0.4f), hullDark);
            Prim.Make(PrimitiveType.Cube, "HullLower", root, new Vector3(0f, -1.9f, 0f), new Vector3(N - 3f, 1f, N - 3f), hullMid);
            Color edge = t == 1 ? Palette.Hex("#22D3FF") : t == 2 ? Palette.Hex("#FF8A1E") : Palette.Hex("#FF2BD6");
            var edgeGlow = pal.Glow("T" + t + "Edge", edge, 1.8f);
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f, hs = H + 0.21f;
                Prim.Make(PrimitiveType.Cube, "HullStrip", root, alongX ? new Vector3(0f, -0.75f, sign * hs) : new Vector3(sign * hs, -0.75f, 0f),
                    alongX ? new Vector3(N + 0.2f, 0.06f, 0.02f) : new Vector3(0.02f, 0.06f, N + 0.2f), edgeGlow);
            }

            if (t == 1) ArenaRings(pal, root);
            else if (t == 2) IndustrialMarkings(pal, root);
            else NeonGrid(pal, root);

            World.BuildFence(pal, root, hullMid, t == 1 ? Palette.Hex("#22D3FF") : t == 2 ? Palette.Hex("#FFB547") : Palette.Hex("#2FE6FF"));
            Obstacles(pal, root, t);
            Backdrop(pal, root, t);
            return root;
        }

        // ---------- Floors ----------

        /// <summary>Assault Android Cactus: concentric glowing rings and spokes painted on a clean tiled arena.</summary>
        static void ArenaRings(Palette pal, Transform root)
        {
            var red = pal.Glow("T1Ring", Palette.Hex("#FF2E55"), 2.4f);
            var cyan = pal.Glow("T1RingOuter", Palette.Hex("#22D3FF"), 1.6f);
            Ring(root, 3.1f, 0.09f, 48, red);
            Ring(root, 6.2f, 0.09f, 80, red);
            Ring(root, 9.2f, 0.07f, 110, cyan);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Prim.Make(PrimitiveType.Cube, "Spoke", root, dir * 4.65f + Vector3.up * 0.012f, new Vector3(0.08f, 0.01f, 3.1f), red, Quaternion.Euler(0f, a, 0f));
            }
            // Pale centre disc
            Prim.Make(PrimitiveType.Cylinder, "Centre", root, Vector3.up * 0.006f, new Vector3(6.1f, 0.004f, 6.1f), pal.Get("T1Centre", Palette.Hex("#787E8A"), 0.4f, 0.1f));
        }

        static void Ring(Transform root, float r, float w, int seg, Material mat)
        {
            float len = 2f * Mathf.PI * r / seg * 1.04f;
            for (int i = 0; i < seg; i++)
            {
                float a = i * 360f / seg;
                var p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * r;
                Prim.Make(PrimitiveType.Cube, "Ring", root, p + Vector3.up * 0.014f, new Vector3(len, 0.01f, w), mat, Quaternion.Euler(0f, a, 0f));
            }
        }

        /// <summary>The Ascent / DRG: Survivor: hazard-striped border, painted lanes, floor grates and chevrons.</summary>
        static void IndustrialMarkings(Palette pal, Transform root)
        {
            var yellow = pal.Get("T2Yellow", Palette.Hex("#E8B21E"), 0.35f, 0.1f);
            var black = pal.Get("T2Black", Palette.Hex("#15171B"), 0.35f, 0.1f);
            var paint = pal.Get("T2Paint", Palette.Hex("#C9CDD3"), 0.3f, 0f);
            var grate = pal.Get("T2Grate", Palette.Hex("#0C0D10"), 0.3f, 0.8f);
            var slat = pal.Get("T2Slat", Palette.Hex("#5A6069"), 0.45f, 0.3f);
            var hazardGlow = pal.Glow("T2Lamp", Palette.Hex("#FF9A3C"), 2.4f);

            // Hazard stripes around the edge, diagonal blocks.
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f;
                for (int i = 0; i < N * 2; i++)
                {
                    float a = -H + 0.25f + i * 0.5f;
                    Vector3 p = alongX ? new Vector3(a, 0.008f, sign * (H - 0.35f)) : new Vector3(sign * (H - 0.35f), 0.008f, a);
                    Prim.Make(PrimitiveType.Cube, "Hazard", root, p, new Vector3(0.5f, 0.012f, 0.5f), i % 2 == 0 ? yellow : black,
                        Quaternion.Euler(0f, alongX ? 0f : 90f, 0f));
                }
            }
            // Painted lane box and corner ticks.
            foreach (float s in new[] { -1f, 1f })
            {
                Prim.Make(PrimitiveType.Cube, "Lane", root, new Vector3(0f, 0.008f, s * 6.5f), new Vector3(13.1f, 0.01f, 0.1f), paint);
                Prim.Make(PrimitiveType.Cube, "Lane", root, new Vector3(s * 6.5f, 0.008f, 0f), new Vector3(0.1f, 0.01f, 13.1f), paint);
            }
            // Chevrons pointing to the centre.
            foreach (float a in new[] { 0f, 90f, 180f, 270f })
            {
                var rot = Quaternion.Euler(0f, a, 0f);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 c = rot * new Vector3(0f, 0.009f, 7.6f + k * 0.55f);
                    Prim.Make(PrimitiveType.Cube, "Chevron", root, c + rot * new Vector3(-0.22f, 0f, 0f), new Vector3(0.12f, 0.01f, 0.6f), yellow, rot * Quaternion.Euler(0f, 40f, 0f));
                    Prim.Make(PrimitiveType.Cube, "Chevron", root, c + rot * new Vector3(0.22f, 0f, 0f), new Vector3(0.12f, 0.01f, 0.6f), yellow, rot * Quaternion.Euler(0f, -40f, 0f));
                }
            }
            // Floor grates with glowing vents underneath.
            var rng = new System.Random(5);
            for (int i = 0; i < 16; i++)
            {
                int gx = rng.Next(-8, 8), gz = rng.Next(-8, 8);
                var p = new Vector3(gx + 0.5f, 0.007f, gz + 0.5f);
                if (new Vector2(p.x, p.z - World.PlayerStart.z).magnitude < 2f) continue;
                Prim.Make(PrimitiveType.Cube, "Grate", root, p, new Vector3(0.92f, 0.01f, 0.92f), grate);
                for (int k = -3; k <= 3; k++)
                    Prim.Make(PrimitiveType.Cube, "Slat", root, p + new Vector3(k * 0.12f, 0.006f, 0f), new Vector3(0.04f, 0.01f, 0.86f), slat);
                if (i % 3 == 0) Prim.Make(PrimitiveType.Cube, "Vent", root, p + Vector3.up * 0.004f, new Vector3(0.7f, 0.004f, 0.7f), hazardGlow);
            }
        }

        /// <summary>Nex Machina / Geometry Wars: glowing lines on every tile edge over black glass.</summary>
        /// <summary>Nex Machina / Geometry Wars: black glass with a few long neon frame lines (no per-tile grid).</summary>
        static void NeonGrid(Palette pal, Transform root)
        {
            var cyan = pal.Glow("T3Grid", Palette.Hex("#2FB8FF"), 1.4f);
            var violet = pal.Glow("T3GridMajor", Palette.Hex("#7A5CFF"), 1.8f);
            Frame(root, H - 0.7f, 0.06f, violet);
            Frame(root, H - 3.2f, 0.035f, cyan);
            // Diagonal light strips from the corners toward the middle.
            foreach (float a in new[] { 45f, 135f, 225f, 315f })
            {
                var rot = Quaternion.Euler(0f, a, 0f);
                Prim.Make(PrimitiveType.Cube, "Strip", root, rot * new Vector3(0f, 0.006f, (H - 0.7f) * 1.41f - 2.6f), new Vector3(0.035f, 0.008f, 3.4f), cyan, rot);
            }
        }

        static void Frame(Transform root, float half, float w, Material m)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                Prim.Make(PrimitiveType.Cube, "Frame", root, new Vector3(0f, 0.006f, s * half), new Vector3(half * 2f + w, 0.008f, w), m);
                Prim.Make(PrimitiveType.Cube, "Frame", root, new Vector3(s * half, 0.006f, 0f), new Vector3(w, 0.008f, half * 2f + w), m);
            }
        }

        // ---------- Obstacles (identical footprints to World.Build) ----------

        static void Obstacles(Palette pal, Transform root, int t)
        {
            Color red = World.BarrierRed, blue = World.ConduitBlue;
            Material block, top, band, pipe, ring, clamp;
            switch (t)
            {
                case 1:
                    block = pal.Get("T1Block", Palette.Hex("#E6EAF0"), 0.85f, 0f);
                    top = pal.Get("T1BlockTop", Palette.Hex("#F6F8FB"), 0.85f, 0f);
                    band = pal.Glow("T1Band", red, 2.6f);
                    pipe = pal.Get("T1Pipe", Palette.Hex("#E6EAF0"), 0.85f, 0f);
                    ring = pal.Glow("T1PipeRing", blue, 2.2f);
                    clamp = pal.Get("T1Clamp", Palette.Hex("#3A4150"), 0.6f, 0.5f);
                    break;
                case 2:
                    block = pal.Get("T2Block", Palette.Hex("#9A9DA3"), 0.2f, 0f);
                    top = pal.Get("T2BlockTop", Palette.Hex("#B0B3B9"), 0.2f, 0f);
                    band = pal.Glow("T2Band", red, 2f);
                    pipe = pal.Get("T2Pipe", Palette.Hex("#5A6069"), 0.45f, 0.3f);
                    ring = pal.Glow("T2PipeRing", blue, 1.8f);
                    clamp = pal.Get("T2Clamp", Palette.Hex("#E8B21E"), 0.35f, 0.2f);
                    break;
                default:
                    block = pal.Get("T3Block", Palette.Hex("#090A10"), 0.95f, 0.2f);
                    top = pal.Glow("T3BlockTop", red, 2.8f);
                    band = pal.Glow("T3Band", red, 3.4f);
                    pipe = pal.Get("T3Pipe", Palette.Hex("#090A10"), 0.95f, 0.2f);
                    ring = pal.Glow("T3PipeRing", blue, 3.4f);
                    clamp = pal.Get("T3Clamp", Palette.Hex("#151827"), 0.9f, 0.2f);
                    break;
            }

            foreach (var b in BarrierPositions())
            {
                var o = new GameObject("Barrier").transform;
                o.SetParent(root, false);
                o.localPosition = new Vector3(b.x, 0f, b.y);
                if (t == 2)
                {
                    // Jersey barrier: wider base, narrow top, red/white stripes.
                    Prim.Make(PrimitiveType.Cube, "Base", o, new Vector3(0f, 0.08f, 0f), new Vector3(0.72f, 0.16f, 0.72f), block);
                    Prim.Make(PrimitiveType.Cube, "Block", o, new Vector3(0f, 0.25f, 0f), new Vector3(0.54f, 0.2f, 0.54f), top);
                    Prim.Make(PrimitiveType.Cube, "Band", o, new Vector3(0f, 0.22f, 0f), new Vector3(0.56f, 0.06f, 0.56f), band);
                }
                else
                {
                    Prim.Make(PrimitiveType.Cube, "Block", o, new Vector3(0f, 0.17f, 0f), new Vector3(0.66f, 0.34f, 0.66f), block);
                    Prim.Make(PrimitiveType.Cube, "Top", o, new Vector3(0f, 0.345f, 0f), new Vector3(t == 3 ? 0.62f : 0.52f, 0.03f, t == 3 ? 0.62f : 0.52f), top);
                    Prim.Make(PrimitiveType.Cube, "Band", o, new Vector3(0f, t == 3 ? 0.02f : 0.21f, 0f), new Vector3(0.68f, t == 3 ? 0.03f : 0.07f, 0.68f), band);
                }
                Prim.Make(PrimitiveType.Cube, "Pad", o, new Vector3(0f, 0.005f, 0f), new Vector3(0.9f, 0.01f, 0.9f), pal.Glow("T" + t + "Pad", red, 0.25f, Palette.Hex("#3A1418")));
                var ob = o.gameObject.AddComponent<Obstacle>();
                ob.kind = ObstacleKind.Barrier;
                ob.halfExtents = new Vector2(0.33f, 0.33f);
                World.Obstacles.Add(ob);
            }

            foreach (var c in World.ConduitLayout)
            {
                var o = new GameObject("Conduit").transform;
                o.SetParent(root, false);
                o.localPosition = new Vector3(c.pos.x, 0f, c.pos.y);
                Quaternion rot = c.alongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);
                Vector3 axis = c.alongX ? Vector3.right : Vector3.forward;
                Vector3 center = new Vector3(0f, 0.24f, 0f);
                Prim.Make(PrimitiveType.Cylinder, "Pipe", o, center, new Vector3(t == 2 ? 0.52f : 0.46f, 1.5f, t == 2 ? 0.52f : 0.46f), pipe, rot);
                for (int i = -2; i <= 2; i++)
                    Prim.Make(PrimitiveType.Cylinder, "Ring", o, center + axis * (i * 0.65f), new Vector3(0.5f, 0.04f, 0.5f) * (t == 2 ? 1.12f : 1f), ring, rot);
                foreach (float s in new[] { -1f, 1f })
                    Prim.Make(PrimitiveType.Cube, "Clamp", o, new Vector3(0f, 0.12f, 0f) + axis * (s * 1.42f),
                        c.alongX ? new Vector3(0.18f, 0.24f, 0.62f) : new Vector3(0.62f, 0.24f, 0.18f), clamp);
                var ob = o.gameObject.AddComponent<Obstacle>();
                ob.kind = ObstacleKind.Conduit;
                ob.halfExtents = c.alongX ? new Vector2(1.5f, 0.24f) : new Vector2(0.24f, 1.5f);
                World.Obstacles.Add(ob);
            }
        }

        /// <summary>Same barrier layout as World.Build (same seed, same draws).</summary>
        static List<Vector2> BarrierPositions()
        {
            var rng = new System.Random(12);
            for (int i = 0; i < 14 * 3; i++) rng.NextDouble(); // World.BuildCraters draws first
            var list = new List<Vector2>();
            var start = new Vector2(World.PlayerStart.x, World.PlayerStart.z);
            for (int gx = -2; gx <= 2; gx++)
            for (int gz = -2; gz <= 2; gz++)
            {
                var b = new Vector2(gx * 3.6f + ((float)rng.NextDouble() - 0.5f) * 1.2f, gz * 3.6f + ((float)rng.NextDouble() - 0.5f) * 1.2f);
                if ((b - start).magnitude < 2.5f) continue;
                bool near = false;
                foreach (var c in World.ConduitLayout)
                {
                    Vector2 d = b - c.pos;
                    float along = c.alongX ? Mathf.Abs(d.x) : Mathf.Abs(d.y), across = c.alongX ? Mathf.Abs(d.y) : Mathf.Abs(d.x);
                    if (along < 2.4f && across < 1.4f) near = true;
                }
                if (!near) list.Add(b);
            }
            return list;
        }

        // ---------- Backdrops ----------

        static void Backdrop(Palette pal, Transform root, int t)
        {
            var space = new GameObject("Space").transform;
            space.SetParent(root, false);
            var rng = new System.Random(31);
            var cube = World.GetCubeMesh();

            // Sparse stars (one combined mesh).
            var combine = new List<CombineInstance>();
            for (int i = 0; i < (t == 2 ? 250 : 600); i++)
            {
                Vector3 d = new Vector3((float)rng.NextDouble() * 2f - 1f, -(float)rng.NextDouble() * 0.9f - 0.05f, (float)rng.NextDouble() * 2f - 1f).normalized;
                float s = 0.08f + (float)rng.NextDouble() * 0.16f;
                combine.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(d * (45f + (float)rng.NextDouble() * 50f) + Vector3.forward * 12f, Quaternion.identity, Vector3.one * s) });
            }
            var stars = new Mesh { name = "Stars", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            stars.CombineMeshes(combine.ToArray(), true, true);
            Prim.MeshObject("Stars", space, stars, pal.Glow("T" + t + "Star", t == 3 ? Palette.Hex("#C9B8FF") : Palette.Hex("#DDE8FF"), 2f, Color.white));

            if (t == 1)
            {
                // Stadium: stacked cargo containers and girders below the arena, in team colours.
                Color[] cols = { Palette.Hex("#2E5BFF"), Palette.Hex("#FF2E55"), Palette.Hex("#E9EDF3"), Palette.Hex("#1E2330") };
                for (int i = 0; i < 60; i++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f, r = H + 3f + (float)rng.NextDouble() * 14f;
                    var p = new Vector3(Mathf.Cos(ang) * r, -3.5f - (float)rng.NextDouble() * 9f, Mathf.Sin(ang) * r + 3f);
                    var m = pal.Get("T1Cargo" + (i % 4), cols[i % 4], 0.5f, 0.3f);
                    Prim.Make(PrimitiveType.Cube, "Cargo", space, p, new Vector3(2.4f, 1.1f, 1.1f), m, Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f));
                }
                World.AddPointLight(space, new Vector3(-12f, 6f, 12f), Palette.Hex("#22D3FF"), 24f, 2.2f);
                World.AddPointLight(space, new Vector3(12f, 6f, 12f), Palette.Hex("#FF2E55"), 24f, 1.6f);
                World.AddPointLight(space, new Vector3(0f, 7f, -12f), Palette.Hex("#FFFFFF"), 20f, 1.4f);
            }
            else if (t == 2)
            {
                // Mining rig below: dark towers with lit windows, warm sodium lamps.
                var tower = pal.Get("T2Tower", Palette.Hex("#15171C"), 0.4f, 0.7f);
                var window = pal.Glow("T2Window", Palette.Hex("#FFB547"), 2.2f);
                var teal = pal.Glow("T2WindowTeal", Palette.Hex("#2FD6C8"), 2f);
                for (int i = 0; i < 34; i++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f, r = H + 4f + (float)rng.NextDouble() * 16f;
                    float h = 4f + (float)rng.NextDouble() * 10f;
                    var p = new Vector3(Mathf.Cos(ang) * r, -4f - h * 0.5f, Mathf.Sin(ang) * r + 4f);
                    float w = 1.2f + (float)rng.NextDouble() * 1.8f;
                    Prim.Make(PrimitiveType.Cube, "Tower", space, p, new Vector3(w, h, w), tower);
                    for (int k = 0; k < 4; k++)
                        Prim.Make(PrimitiveType.Cube, "Window", space, p + new Vector3(0f, h * 0.5f - 0.6f - k * 1.3f, -w * 0.51f), new Vector3(w * 0.7f, 0.12f, 0.02f), k % 3 == 0 ? teal : window);
                }
                World.AddPointLight(space, new Vector3(-11f, 5f, 11f), Palette.Hex("#FF9A3C"), 22f, 2.4f);
                World.AddPointLight(space, new Vector3(11f, 5f, 11f), Palette.Hex("#FFB547"), 22f, 1.8f);
                World.AddPointLight(space, new Vector3(0f, 6f, -13f), Palette.Hex("#2FD6C8"), 20f, 1.4f);
            }
            else
            {
                // Floating voxel blocks with neon edges.
                var dark = pal.Get("T3Voxel", Palette.Hex("#0A0B12"), 0.9f, 0.2f);
                Material[] edges = { pal.Glow("T3VoxEdgeA", Palette.Hex("#FF2BD6"), 2.4f), pal.Glow("T3VoxEdgeB", Palette.Hex("#2FE6FF"), 2.4f) };
                for (int i = 0; i < 40; i++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f, r = H + 3f + (float)rng.NextDouble() * 15f;
                    var p = new Vector3(Mathf.Cos(ang) * r, -2f - (float)rng.NextDouble() * 10f, Mathf.Sin(ang) * r + 4f);
                    float s = 0.6f + (float)rng.NextDouble() * 1.8f;
                    var o = new GameObject("Voxel").transform;
                    o.SetParent(space, false);
                    o.localPosition = p;
                    o.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f);
                    Prim.Make(PrimitiveType.Cube, "Block", o, Vector3.zero, Vector3.one * s, dark);
                    var e = edges[i % 2];
                    foreach (float y in new[] { -0.5f, 0.5f })
                    foreach (float q in new[] { -0.5f, 0.5f })
                    {
                        Prim.Make(PrimitiveType.Cube, "EdgeX", o, new Vector3(0f, y, q) * s, new Vector3(s, 0.04f, 0.04f), e);
                        Prim.Make(PrimitiveType.Cube, "EdgeZ", o, new Vector3(q, y, 0f) * s, new Vector3(0.04f, 0.04f, s), e);
                    }
                }
                World.AddPointLight(space, new Vector3(-12f, 5f, 12f), Palette.Hex("#FF2BD6"), 22f, 2.4f);
                World.AddPointLight(space, new Vector3(12f, 5f, 12f), Palette.Hex("#2FE6FF"), 22f, 2.4f);
                World.AddPointLight(space, new Vector3(0f, 6f, -13f), Palette.Hex("#7A5CFF"), 20f, 1.6f);
            }
        }
    }
}
