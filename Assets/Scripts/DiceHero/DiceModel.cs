using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Builds the dice unit. Hierarchy:
    ///   Dice (root, sits on the ground)  →  Visual (squash on landing)  →  Body (rolls, carries the glowing pips)
    ///                                                                  →  WeaponMount (gun turret above the top face)
    /// Local face numbers: +Y 1, -Y 6, +X 2, -X 5, +Z 3, -Z 4 (opposite faces add up to 7).
    /// </summary>
    public class DiceModel
    {
        public Transform Root, Visual, Body, WeaponMount;

        public static readonly Vector3[] FaceNormals =
            { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        public static readonly int[] FaceNumbers = { 1, 6, 2, 5, 3, 4 };

        const float PipOffset = 0.25f;

        public static DiceModel Build(Palette pal, Transform parent, Vector3 position)
        {
            var m = new DiceModel();
            var bodyMat = pal.Get("DiceGunmetal", Palette.Hex("#6A7482"), 0.7f, 0.9f);
            var pipMat = pal.Glow("DicePip", Palette.Hex("#35E6FF"), 3f);
            var plateMat = pal.Get("DicePlate", Palette.Hex("#1C2027"), 0.5f, 0.6f);

            m.Root = new GameObject("Dice").transform;
            m.Root.SetParent(parent, false);
            m.Root.position = position;

            m.Visual = new GameObject("Visual").transform;
            m.Visual.SetParent(m.Root, false);

            var body = Prim.MeshObject("Body", m.Visual, MeshFactory.RoundedCube(0.1f, 3), bodyMat);
            m.Body = body.transform;
            m.Body.localPosition = new Vector3(0f, 0.5f, 0f);

            for (int f = 0; f < 6; f++)
            {
                Vector3 n = FaceNormals[f];
                // Recessed armour plate on each face, with the glowing pips on top of it.
                Prim.Make(PrimitiveType.Cube, "Plate", m.Body, n * 0.49f,
                    Vector3.one * 0.84f - Abs(n) * 0.83f, plateMat);
                AddPips(m.Body, n, FaceNumbers[f], pipMat);
            }

            // Glowing seams along the 12 rounded edges give it a tech-cube look.
            var seamMat = pal.Glow("DiceSeam", Palette.Hex("#35E6FF"), 1.4f);
            const float e = 0.4707f; // outermost point of the 0.1-radius rounded edge
            for (int axis = 0; axis < 3; axis++)
            for (int s1 = -1; s1 <= 1; s1 += 2)
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                Vector3 pos = axis == 0 ? new Vector3(0f, s1 * e, s2 * e) : axis == 1 ? new Vector3(s1 * e, 0f, s2 * e) : new Vector3(s1 * e, s2 * e, 0f);
                Vector3 size = axis == 0 ? new Vector3(0.78f, 0.035f, 0.035f) : axis == 1 ? new Vector3(0.035f, 0.78f, 0.035f) : new Vector3(0.035f, 0.035f, 0.78f);
                Prim.Make(PrimitiveType.Cube, "Seam", m.Body, pos, size, seamMat);
            }

            // Guns hover over the top face so the top number stays readable.
            m.WeaponMount = new GameObject("WeaponMount").transform;
            m.WeaponMount.SetParent(m.Visual, false);
            m.WeaponMount.localPosition = new Vector3(0f, 1.3f, 0f);
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
                Vector3 pos = normal * 0.505f + u * (p.x * PipOffset) + v * (p.y * PipOffset);
                Prim.Make(PrimitiveType.Cylinder, "Pip" + number, body, pos, new Vector3(0.15f, 0.01f, 0.15f), mat, rot);
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

