using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Builds a die. Hierarchy:
    ///   Dice (root, sits on the ground)  →  Visual (squash on landing)  →  Body (rolls, carries the pips)
    ///                                                                  →  WeaponMount (gun turret above the top face)
    /// Local face numbers: +Y 1, -Y 6, +X 2, -X 5, +Z 3, -Z 4 (opposite faces add up to 7).
    /// </summary>
    public class DiceModel
    {
        public Transform Root, Visual, Body, WeaponMount;
        public Material PipMaterial, SeamMaterial;

        public static readonly Vector3[] FaceNormals =
            { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        public static readonly int[] FaceNumbers = { 1, 6, 2, 5, 3, 4 };

        const float PipOffset = 0.25f;

        /// <summary>Materials for one die style.</summary>
        public struct Look
        {
            public Material body, plate, pip, seam;
        }

        /// <summary>Roll Power's hero: a polished gold die with ink-black pips and warm glowing seams.</summary>
        public static Look Hero(Palette pal) => new Look
        {
            body = pal.Get("DiceGold", Palette.Hex("#E7A92E"), 0.82f, 0.95f),
            plate = pal.Get("DiceGoldPlate", Palette.Hex("#C98A1C"), 0.7f, 0.9f),
            pip = pal.Get("DicePip", Palette.Hex("#140D07"), 0.9f, 0f),
            seam = pal.Glow("DiceSeam", Palette.Hex("#FFD27A"), 1.6f),
        };

        public static DiceModel Build(Palette pal, Transform parent, Vector3 position) => Build(pal, parent, position, Hero(pal), 1f);

        public static DiceModel Build(Palette pal, Transform parent, Vector3 position, Look look, float size)
        {
            var m = new DiceModel { PipMaterial = look.pip, SeamMaterial = look.seam };

            m.Root = new GameObject("Dice").transform;
            m.Root.SetParent(parent, false);
            m.Root.position = position;

            m.Visual = new GameObject("Visual").transform;
            m.Visual.SetParent(m.Root, false);
            m.Visual.localScale = Vector3.one;

            var body = Prim.MeshObject("Body", m.Visual, MeshFactory.RoundedCube(0.1f, 3), look.body);
            m.Body = body.transform;
            m.Body.localPosition = new Vector3(0f, 0.5f * size, 0f);
            m.Body.localScale = Vector3.one * size;

            for (int f = 0; f < 6; f++)
            {
                Vector3 n = FaceNormals[f];
                // Slightly recessed plate on each face, with the pips on top of it.
                Prim.Make(PrimitiveType.Cube, "Plate", m.Body, n * 0.49f, Vector3.one * 0.84f - Abs(n) * 0.83f, look.plate);
                AddPips(m.Body, n, FaceNumbers[f], look.pip);
            }

            // Glowing seams along the 12 rounded edges.
            const float e = 0.4707f; // outermost point of the 0.1-radius rounded edge
            for (int axis = 0; axis < 3; axis++)
            for (int s1 = -1; s1 <= 1; s1 += 2)
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                Vector3 pos = axis == 0 ? new Vector3(0f, s1 * e, s2 * e) : axis == 1 ? new Vector3(s1 * e, 0f, s2 * e) : new Vector3(s1 * e, s2 * e, 0f);
                Vector3 sz = axis == 0 ? new Vector3(0.78f, 0.03f, 0.03f) : axis == 1 ? new Vector3(0.03f, 0.78f, 0.03f) : new Vector3(0.03f, 0.03f, 0.78f);
                Prim.Make(PrimitiveType.Cube, "Seam", m.Body, pos, sz, look.seam);
            }

            // Guns hover over the top face so the top number stays readable.
            m.WeaponMount = new GameObject("WeaponMount").transform;
            m.WeaponMount.SetParent(m.Visual, false);
            m.WeaponMount.localPosition = new Vector3(0f, 1.3f * size, 0f);
            return m;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static void AddPips(Transform body, Vector3 normal, int number, Material mat)
        {
            bool cap = Mathf.Abs(normal.y) > 0.5f;
            Vector3 u = cap ? Vector3.right : Vector3.Cross(Vector3.up, normal);
            Vector3 v = cap ? Vector3.forward : Vector3.up;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal);
            foreach (var p in PipLayout(number))
            {
                Vector3 pos = normal * 0.503f + u * (p.x * PipOffset) + v * (p.y * PipOffset);
                Prim.Make(PrimitiveType.Cylinder, "Pip" + number, body, pos, new Vector3(0.16f, 0.008f, 0.16f), mat, rot);
            }
        }

        static Vector2[] PipLayout(int n)
        {
            switch (n)
            {
                case 1: return new[] { Vector2.zero };
                case 2: return new[] { new Vector2(-1, -1), new Vector2(1, 1) };
                case 3: return new[] { new Vector2(-1, -1), Vector2.zero, new Vector2(1, 1) };
                case 4: return new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
                case 5: return new[] { new Vector2(-1, -1), new Vector2(1, -1), Vector2.zero, new Vector2(-1, 1), new Vector2(1, 1) };
                default: return new[] { new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 0), new Vector2(1, 1) };
            }
        }
    }
}
