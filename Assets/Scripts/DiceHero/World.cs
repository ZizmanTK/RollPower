using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    public enum ObstacleKind { Barrier, Conduit }

    /// <summary>Something the dice trips over. Barriers (red) roll it one face, conduits (blue pipes) roll it two.</summary>
    public class Obstacle : MonoBehaviour
    {
        public ObstacleKind kind;
        /// <summary>Half size of the footprint on the ground (x, z).</summary>
        public Vector2 halfExtents;
        public int RollSteps => kind == ObstacleKind.Conduit ? 2 : 1;

        public bool Overlaps(Vector3 center, float radius)
        {
            Vector3 p = transform.position;
            float dx = Mathf.Max(Mathf.Abs(center.x - p.x) - halfExtents.x, 0f);
            float dz = Mathf.Max(Mathf.Abs(center.z - p.z) - halfExtents.y, 0f);
            return dx * dx + dz * dz < radius * radius;
        }
    }

    /// <summary>
    /// Builds the arena: a floating lunar platform in space. Grey deck plates with craters, red barriers
    /// and blue conduits (the colours of the jam original), a force-field fence that stops the dice but
    /// not bombs, and a starfield with a planet far below.
    /// </summary>
    public static class World
    {
        public const int BoardSize = 20;
        public static float HalfSize => BoardSize * 0.5f;

        public static readonly List<Obstacle> Obstacles = new List<Obstacle>();

        /// <summary>Hand-placed obstacles for a campaign stage (null: the gauntlet's default layout). Navy theme only.</summary>
        public static Placement[] Layout;

        public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -6f);
        static Vector2 PlayerStart2D => new Vector2(PlayerStart.x, PlayerStart.z);

        internal static readonly (Vector2 pos, bool alongX)[] ConduitLayout =
        {
            (new Vector2(5.4f, 1.8f), false),
            (new Vector2(-5.4f, -3.6f), true),
            (new Vector2(-1.8f, 7f), true),
        };

        public static readonly Color BarrierRed = Palette.Hex("#FF3B4E");
        public static readonly Color ConduitBlue = Palette.Hex("#3D7BFF");
        public static readonly Color FenceBlue = Palette.Hex("#6FA8FF");
        public static readonly Color SpaceColor = Palette.Hex("#04060D");

        public static Transform Build(Palette pal)
        {
            if (Art.Theme != 0) return WorldThemes.Build(pal);
            Obstacles.Clear();
            var root = new GameObject("World").transform;
            var rng = new System.Random(12);

            var metalDark = pal.Get("MetalDark", Palette.Hex("#1B1F27"), 0.55f, 0.7f);
            var metalMid = pal.Get("MetalMid", Palette.Hex("#2E343F"), 0.6f, 0.8f);
            var metalLight = pal.Get("MetalLight", Palette.Hex("#59616E"), 0.65f, 0.85f);

            // Lunar deck: two tones of grey regolith plates; the gaps show a faint glowing slab underneath.
            var board = Prim.MeshObject("Deck", root, MeshFactory.CheckerBoard(BoardSize, BoardSize, 1f, 0.05f, 0.3f),
                pal.Get("DeckA", Palette.Hex("#6C727D"), 0.18f, 0.05f),
                pal.Get("DeckB", Palette.Hex("#767C87"), 0.18f, 0.05f));
            board.transform.localPosition = Vector3.zero;
            Prim.Make(PrimitiveType.Cube, "SeamGlow", root, new Vector3(0f, -0.17f, 0f), new Vector3(BoardSize, 0.3f, BoardSize),
                pal.Glow("SeamGlow", Palette.Hex("#7FB2FF"), 0.3f));
            BuildCraters(pal, root, rng);

            // Platform body under the deck, so it reads as a floating slab.
            Prim.Make(PrimitiveType.Cube, "Hull", root, new Vector3(0f, -0.9f, 0f), new Vector3(BoardSize + 0.4f, 1.2f, BoardSize + 0.4f), metalDark);
            Prim.Make(PrimitiveType.Cube, "HullLower", root, new Vector3(0f, -1.9f, 0f), new Vector3(BoardSize - 3f, 1f, BoardSize - 3f), metalMid);
            var underGlow = pal.Glow("UnderGlow", UiKit.Gold, 1.6f);
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f;
                float hs = HalfSize + 0.21f;
                Vector3 pos = alongX ? new Vector3(0f, -0.75f, sign * hs) : new Vector3(sign * hs, -0.75f, 0f);
                Vector3 size = alongX ? new Vector3(BoardSize + 0.2f, 0.05f, 0.02f) : new Vector3(0.02f, 0.05f, BoardSize + 0.2f);
                Prim.Make(PrimitiveType.Cube, "HullStrip", root, pos, size, underGlow);
            }

            BuildFence(pal, root, metalMid);

            // Barriers (roll 1): low blocks with red light bands.
            var barrierMat = pal.Glow("BarrierRed", BarrierRed, 2.4f);
            var barriers = new List<Vector2>();
            for (int gx = -2; gx <= 2; gx++)
            for (int gz = -2; gz <= 2; gz++)
            {
                var b = new Vector2(gx * 3.6f + ((float)rng.NextDouble() - 0.5f) * 1.2f, gz * 3.6f + ((float)rng.NextDouble() - 0.5f) * 1.2f);
                if ((b - PlayerStart2D).magnitude < 2.5f) continue;
                bool nearConduit = false;
                foreach (var c in ConduitLayout)
                {
                    Vector2 d = b - c.pos;
                    float along = c.alongX ? Mathf.Abs(d.x) : Mathf.Abs(d.y);
                    float across = c.alongX ? Mathf.Abs(d.y) : Mathf.Abs(d.x);
                    if (along < 2.4f && across < 1.4f) nearConduit = true;
                }
                if (!nearConduit) barriers.Add(b);
            }
            foreach (var b in barriers)
            {
                var o = new GameObject("Barrier").transform;
                o.SetParent(root, false);
                o.localPosition = new Vector3(b.x, 0f, b.y);
                Prim.Make(PrimitiveType.Cube, "Block", o, new Vector3(0f, 0.17f, 0f), new Vector3(0.66f, 0.34f, 0.66f), metalMid);
                Prim.Make(PrimitiveType.Cube, "Top", o, new Vector3(0f, 0.345f, 0f), new Vector3(0.52f, 0.03f, 0.52f), metalLight);
                Prim.Make(PrimitiveType.Cube, "Band", o, new Vector3(0f, 0.21f, 0f), new Vector3(0.68f, 0.07f, 0.68f), barrierMat);
                Prim.Make(PrimitiveType.Cube, "Pad", o, new Vector3(0f, 0.005f, 0f), new Vector3(0.9f, 0.01f, 0.9f), pal.Glow("BarrierPad", BarrierRed, 0.25f, Palette.Hex("#3A1418")));
                var ob = o.gameObject.AddComponent<Obstacle>();
                ob.kind = ObstacleKind.Barrier;
                ob.halfExtents = new Vector2(0.33f, 0.33f);
                Obstacles.Add(ob);
            }

            // Conduits (roll 2): armoured pipes with blue rings.
            var ringMat = pal.Glow("ConduitRing", ConduitBlue, 1.7f);
            foreach (var c in ConduitLayout)
            {
                var o = new GameObject("Conduit").transform;
                o.SetParent(root, false);
                o.localPosition = new Vector3(c.pos.x, 0f, c.pos.y);
                Quaternion rot = c.alongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);
                Vector3 axis = c.alongX ? Vector3.right : Vector3.forward;
                Vector3 center = new Vector3(0f, 0.24f, 0f);
                Prim.Make(PrimitiveType.Cylinder, "Pipe", o, center, new Vector3(0.46f, 1.5f, 0.46f), metalMid, rot);
                for (int i = -2; i <= 2; i++)
                    Prim.Make(PrimitiveType.Cylinder, "Ring", o, center + axis * (i * 0.65f), new Vector3(0.5f, 0.04f, 0.5f), ringMat, rot);
                foreach (float s in new[] { -1f, 1f })
                    Prim.Make(PrimitiveType.Cube, "Clamp", o, new Vector3(0f, 0.12f, 0f) + axis * (s * 1.42f),
                        c.alongX ? new Vector3(0.18f, 0.24f, 0.62f) : new Vector3(0.62f, 0.24f, 0.18f), metalLight);
                var ob = o.gameObject.AddComponent<Obstacle>();
                ob.kind = ObstacleKind.Conduit;
                ob.halfExtents = c.alongX ? new Vector2(1.5f, 0.24f) : new Vector2(0.24f, 1.5f);
                Obstacles.Add(ob);
            }

            BuildSpace(pal, root, rng, metalDark, metalMid);
            return root;
        }

        static void BuildCraters(Palette pal, Transform root, System.Random rng)
        {
            // Subtle: a slightly darker floor and slightly lighter rim than the deck plates.
            var floor = pal.Get("CraterFloor", Palette.Hex("#646A75"), 0.12f, 0.05f);
            var rim = pal.Get("CraterRim", Palette.Hex("#7D838E"), 0.15f, 0.05f);
            for (int i = 0; i < 14; i++)
            {
                float r = 0.25f + (float)rng.NextDouble() * (i < 2 ? 0.9f : 0.4f);
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * (BoardSize - 2f), 0f, ((float)rng.NextDouble() - 0.5f) * (BoardSize - 2f));
                Prim.Make(PrimitiveType.Cylinder, "CraterRim", root, p + Vector3.up * 0.004f, new Vector3(r * 2.2f, 0.004f, r * 2.2f), rim);
                Prim.Make(PrimitiveType.Cylinder, "Crater", root, p + Vector3.up * 0.009f, new Vector3(r * 1.8f, 0.004f, r * 1.8f), floor);
            }
        }

        /// <summary>Posts and a laser line around the edge: it stops the dice and enemies, bombs smash through.</summary>
        internal static void BuildFence(Palette pal, Transform root, Material postMat, Color? color = null)
        {
            Color fc = color ?? FenceBlue;
            var laser = pal.Glow("FenceLaser" + Art.Theme, fc, 1.3f);
            var cap = pal.Glow("FenceCap" + Art.Theme, fc, 3f);
            float hs = HalfSize + 0.08f;
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f;
                Vector3 c = alongX ? new Vector3(0f, 0f, sign * hs) : new Vector3(sign * hs, 0f, 0f);
                Vector3 len = alongX ? new Vector3(BoardSize, 0.025f, 0.025f) : new Vector3(0.025f, 0.025f, BoardSize);
                Prim.Make(PrimitiveType.Cube, "Laser", root, c + Vector3.up * 0.28f, len, laser);
                Prim.Make(PrimitiveType.Cube, "LaserLow", root, c + Vector3.up * 0.12f, len, laser);
                for (int i = 0; i <= BoardSize; i += 2)
                {
                    float a = i - HalfSize;
                    Vector3 p = alongX ? new Vector3(a, 0f, c.z) : new Vector3(c.x, 0f, a);
                    Prim.Make(PrimitiveType.Cube, "Post", root, p + Vector3.up * 0.2f, new Vector3(0.09f, 0.4f, 0.09f), postMat);
                    Prim.Make(PrimitiveType.Cube, "PostCap", root, p + Vector3.up * 0.42f, new Vector3(0.1f, 0.04f, 0.1f), cap);
                }
            }
        }

        static void BuildSpace(Palette pal, Transform root, System.Random rng, Material dark, Material mid)
        {
            var space = new GameObject("Space").transform;
            space.SetParent(root, false);

            // Starfield: one combined mesh of tiny cubes scattered below and around the platform.
            var cube = GetCubeMesh();
            var combine = new List<CombineInstance>();
            for (int i = 0; i < 700; i++)
            {
                Vector3 d = new Vector3((float)rng.NextDouble() * 2f - 1f, -(float)rng.NextDouble() * 0.9f - 0.05f, (float)rng.NextDouble() * 2f - 1f).normalized;
                float dist = 45f + (float)rng.NextDouble() * 50f;
                float s = 0.08f + (float)rng.NextDouble() * (i % 17 == 0 ? 0.35f : 0.14f);
                combine.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(d * dist + Vector3.forward * 12f, Quaternion.identity, Vector3.one * s) });
            }
            var stars = new Mesh { name = "Stars", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            stars.CombineMeshes(combine.ToArray(), true, true);
            Prim.MeshObject("Stars", space, stars, pal.Glow("Star", Palette.Hex("#DDE8FF"), 2.2f, Color.white));

            // A big planet far below, lit by the sun.
            Prim.Make(PrimitiveType.Sphere, "Planet", space, new Vector3(-34f, -42f, 34f), Vector3.one * 44f, pal.Get("Planet", Palette.Hex("#2C4C8A"), 0.35f, 0f));
            Prim.Make(PrimitiveType.Sphere, "PlanetHalo", space, new Vector3(-34f, -42f, 34f), Vector3.one * 46f, pal.Glow("PlanetHalo", Palette.Hex("#3D7BFF"), 0.25f, Palette.Hex("#0A1428")));

            // Floating rocks around the platform.
            var rock = pal.Get("Rock", Palette.Hex("#3E434C"), 0.1f, 0.05f);
            for (int i = 0; i < 26; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = HalfSize + 4f + (float)rng.NextDouble() * 12f;
                var p = new Vector3(Mathf.Cos(ang) * r, -3f - (float)rng.NextDouble() * 10f, Mathf.Sin(ang) * r + 4f);
                float s = 0.4f + (float)rng.NextDouble() * 1.6f;
                Prim.Make(PrimitiveType.Cube, "Rock", space, p, new Vector3(s, s * 0.7f, s * 0.9f), rock,
                    Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f));
            }

            // Coloured rim lights for mood (no shadows): warm gold and cool blue.
            AddPointLight(space, new Vector3(-12f, 5f, 12f), ConduitBlue, 22f, 2.4f);
            AddPointLight(space, new Vector3(12f, 5f, 12f), Palette.Hex("#FFB347"), 22f, 1.8f);
            AddPointLight(space, new Vector3(0f, 6f, -14f), Palette.Hex("#8FB8FF"), 18f, 1.2f);
        }

        internal static Mesh GetCubeMesh() => Prim.MeshFor(PrimitiveType.Cube);

        internal static void AddPointLight(Transform parent, Vector3 pos, Color color, float range, float intensity)
        {
            var l = new GameObject("RimLight").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = pos;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }
    }
}
