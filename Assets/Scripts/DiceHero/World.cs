using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    public enum ObstacleKind { Barrier, Conduit }

    /// <summary>Something the dice trips over. Barriers roll it one face, conduits (pipes) roll it two.</summary>
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

    /// <summary>Builds the arena: neon-grid deck, armoured walls, obstacles and the tech skyline around it.</summary>
    public static class World
    {
        public const int BoardSize = 20;
        public static float HalfSize => BoardSize * 0.5f;

        public static readonly List<Obstacle> Obstacles = new List<Obstacle>();

        public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -6f);
        static Vector2 PlayerStart2D => new Vector2(PlayerStart.x, PlayerStart.z);

        static readonly (Vector2 pos, bool alongX)[] ConduitLayout =
        {
            (new Vector2(5.4f, 1.8f), false),
            (new Vector2(-5.4f, -3.6f), true),
            (new Vector2(-1.8f, 7f), true),
        };

        static readonly Color Cyan = Palette.Hex("#2EDBFF");
        static readonly Color Hazard = Palette.Hex("#FF7A1A");
        static readonly Color Magenta = Palette.Hex("#FF2E88");

        public static Transform Build(Palette pal)
        {
            Obstacles.Clear();
            var root = new GameObject("World").transform;
            var rng = new System.Random(12);

            var metalDark = pal.Get("MetalDark", Palette.Hex("#1B2029"), 0.55f, 0.7f);
            var metalMid = pal.Get("MetalMid", Palette.Hex("#2C333F"), 0.6f, 0.8f);
            var metalLight = pal.Get("MetalLight", Palette.Hex("#48515F"), 0.65f, 0.85f);

            // Deck plates; the gaps show a glowing slab underneath, which draws the neon grid.
            var board = Prim.MeshObject("Deck", root, MeshFactory.CheckerBoard(BoardSize, BoardSize, 1f, 0.06f, 0.3f),
                pal.Get("DeckA", Palette.Hex("#2A323F"), 0.78f, 0.55f),
                pal.Get("DeckB", Palette.Hex("#333C4A"), 0.78f, 0.55f));
            board.transform.localPosition = Vector3.zero;
            Prim.Make(PrimitiveType.Cube, "GridGlow", root, new Vector3(0f, -0.17f, 0f), new Vector3(BoardSize, 0.3f, BoardSize),
                pal.Glow("GridGlow", Cyan, 0.9f));

            // Outer ground far below the deck level
            Prim.Make(PrimitiveType.Cube, "Ground", root, new Vector3(0f, -0.6f, 0f), new Vector3(140f, 0.2f, 140f),
                pal.Get("Ground", Palette.Hex("#0A0D12"), 0.4f, 0.3f));

            // Armoured border walls with a light strip
            var stripMat = pal.Glow("WallStrip", Cyan, 2.5f);
            float hs = HalfSize + 0.3f;
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f;
                Vector3 pos = alongX ? new Vector3(0f, 0.2f, sign * hs) : new Vector3(sign * hs, 0.2f, 0f);
                Vector3 size = alongX ? new Vector3(BoardSize + 1.2f, 0.9f, 0.6f) : new Vector3(0.6f, 0.9f, BoardSize + 1.2f);
                Prim.Make(PrimitiveType.Cube, "Wall", root, pos, size, metalMid);
                Vector3 inward = alongX ? new Vector3(0f, 0f, -sign) : new Vector3(-sign, 0f, 0f);
                Vector3 stripSize = alongX ? new Vector3(BoardSize + 0.6f, 0.06f, 0.04f) : new Vector3(0.04f, 0.06f, BoardSize + 0.6f);
                Prim.Make(PrimitiveType.Cube, "Strip", root, pos + inward * 0.31f + Vector3.up * 0.2f, stripSize, stripMat);
            }

            // Barriers (roll 1): low hazard-striped blocks.
            var hazardMat = pal.Glow("Hazard", Hazard, 2.2f);
            // Loose grid of barriers so there's always one to slam into nearby.
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
                Prim.Make(PrimitiveType.Cube, "Block", o, new Vector3(0f, 0.16f, 0f), new Vector3(0.66f, 0.32f, 0.66f), metalLight);
                Prim.Make(PrimitiveType.Cube, "Top", o, new Vector3(0f, 0.33f, 0f), new Vector3(0.5f, 0.04f, 0.5f), metalDark);
                Prim.Make(PrimitiveType.Cube, "StripeA", o, new Vector3(0f, 0.2f, 0f), new Vector3(0.68f, 0.05f, 0.68f), hazardMat);
                Prim.Make(PrimitiveType.Cube, "StripeB", o, new Vector3(0f, 0.09f, 0f), new Vector3(0.68f, 0.05f, 0.68f), hazardMat);
                var ob = o.gameObject.AddComponent<Obstacle>();
                ob.kind = ObstacleKind.Barrier;
                ob.halfExtents = new Vector2(0.33f, 0.33f);
                Obstacles.Add(ob);
            }

            // Conduits (roll 2): armoured pipes with glowing rings.
            var ringMat = pal.Glow("ConduitRing", Magenta, 2.5f);
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
                    Prim.Make(PrimitiveType.Cylinder, "Ring", o, center + axis * (i * 0.65f), new Vector3(0.5f, 0.035f, 0.5f), ringMat, rot);
                foreach (float s in new[] { -1f, 1f })
                    Prim.Make(PrimitiveType.Cube, "Clamp", o, new Vector3(0f, 0.12f, 0f) + axis * (s * 1.42f),
                        c.alongX ? new Vector3(0.18f, 0.24f, 0.62f) : new Vector3(0.62f, 0.24f, 0.18f), metalLight);
                var ob = o.gameObject.AddComponent<Obstacle>();
                ob.kind = ObstacleKind.Conduit;
                ob.halfExtents = c.alongX ? new Vector2(1.5f, 0.24f) : new Vector2(0.24f, 1.5f);
                Obstacles.Add(ob);
            }

            BuildSkyline(pal, root, rng, metalDark, metalMid);
            return root;
        }

        static void BuildSkyline(Palette pal, Transform root, System.Random rng, Material dark, Material mid)
        {
            var lights = new[] { pal.Glow("PylonCyan", Cyan, 3f), pal.Glow("PylonMagenta", Magenta, 3f), pal.Glow("PylonHazard", Hazard, 2.5f) };
            var skyline = new GameObject("Skyline").transform;
            skyline.SetParent(root, false);

            // Tech pylons on the far and side edges (the near edge stays clear for the camera).
            for (int i = 0; i < 40; i++)
            {
                int side = rng.Next(3);
                float along = (float)rng.NextDouble() * 40f - 20f;
                float away = HalfSize + 2.5f + (float)rng.NextDouble() * 14f;
                float x = side == 0 ? along : (side == 1 ? away : -away);
                float z = side == 0 ? away : along;
                float h = 1.5f + (float)rng.NextDouble() * (3f + away - HalfSize);
                float w = 0.8f + (float)rng.NextDouble() * 1.6f;
                var p = new GameObject("Pylon").transform;
                p.SetParent(skyline, false);
                p.localPosition = new Vector3(x, -0.5f, z);
                Prim.Make(PrimitiveType.Cube, "Tower", p, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, w), rng.Next(2) == 0 ? dark : mid);
                var lm = lights[rng.Next(lights.Length)];
                Prim.Make(PrimitiveType.Cube, "LightStrip", p, new Vector3(0f, h * 0.5f, -w * 0.5f - 0.01f), new Vector3(0.08f, h * 0.8f, 0.02f), lm);
                Prim.Make(PrimitiveType.Cube, "Beacon", p, new Vector3(0f, h + 0.05f, 0f), new Vector3(w * 0.6f, 0.1f, w * 0.6f), lm);
            }

            // Coloured rim lights for mood (no shadows).
            AddPointLight(skyline, new Vector3(-12f, 4f, 12f), Cyan, 18f, 2.2f);
            AddPointLight(skyline, new Vector3(12f, 4f, 12f), Magenta, 18f, 2f);
            AddPointLight(skyline, new Vector3(0f, 5f, -14f), Cyan, 16f, 1.2f);
        }

        static void AddPointLight(Transform parent, Vector3 pos, Color color, float range, float intensity)
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
