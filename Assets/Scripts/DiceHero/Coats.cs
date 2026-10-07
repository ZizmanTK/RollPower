using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// The look of each defence that sits over a robot, so the player can tell what hurts it at a glance:
    /// vines wrapped round it (burn them), a crust of ice shards (melt it), an energy ring (overload it) and, on the
    /// High Roller, riveted steel slabs (pierce or blow them). Built around a centre at height y, radius r.
    /// </summary>
    public static class CoatModels
    {
        public static Color Tint(Defence d) => d == Defence.Vines ? Palette.Hex("#7CE05A") : d == Defence.Ice ? Palette.Hex("#BFEFFF")
            : d == Defence.Shield ? Palette.Hex("#49C8FF") : d == Defence.Steel ? Palette.Hex("#AEB8C4") : Color.white;

        public static Transform Build(Palette pal, Transform parent, Defence d, float r, float y, bool big = false)
        {
            if (!big)
            {
                // Blender coats are built at radius 1 from the floor up.
                var bm = BlenderModels.Spawn("Coats/" + d, parent, pal);
                if (bm != null) { bm.localPosition = new Vector3(0f, Mathf.Max(0f, y - 0.45f), 0f); bm.localScale = new Vector3(r, r * 0.95f, r); return bm; }
            }
            var root = new GameObject("Coat" + d).transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, y, 0f);
            var rng = new System.Random(d.GetHashCode() * 97 + (big ? 1 : 0));
            float R() => (float)rng.NextDouble();
            switch (d)
            {
                case Defence.Vines:
                {
                    var vine = pal.Get("CoatVine", Palette.Hex("#2E6B33"), 0.3f, 0f);
                    var leaf = pal.Get("CoatLeaf", Palette.Hex("#4FA34A"), 0.35f, 0f);
                    int n = big ? 10 : 5;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * 360f / n;
                        var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                        Prim.Make(PrimitiveType.Cylinder, "Vine", root, dir * r * 0.92f, new Vector3(0.07f, r * 0.75f, 0.07f) * (big ? 2.2f : 1f), vine, Quaternion.Euler(0f, a, 38f));
                        var lp = dir * r + Vector3.up * (R() - 0.3f) * r * 0.9f;
                        Prim.Make(PrimitiveType.Sphere, "Leaf", root, lp, new Vector3(0.22f, 0.05f, 0.14f) * (big ? 2.4f : 1f), leaf, Quaternion.Euler(R() * 40f - 20f, a + 90f, R() * 50f));
                    }
                    break;
                }
                case Defence.Ice:
                {
                    var ice = pal.Get("CoatIce", Palette.Hex("#DDF4FF"), 0.95f, 0.1f);
                    var rim = pal.Glow("CoatIceGlow", Palette.Hex("#9FE3FF"), 0.7f);
                    int n = big ? 16 : 8;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * 360f / n + R() * 20f;
                        float h = (R() - 0.4f) * r * (big ? 1.6f : 0.8f);
                        var p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * r + Vector3.up * h;
                        float s = (big ? 0.42f : 0.17f) + R() * (big ? 0.3f : 0.12f);
                        Prim.Make(PrimitiveType.Cube, "Shard", root, p, new Vector3(s * 0.7f, s * 1.5f, s * 0.7f), i % 4 == 0 ? rim : ice, Quaternion.Euler(R() * 50f - 25f, a, R() * 50f - 25f));
                    }
                    if (!big) Prim.Make(PrimitiveType.Sphere, "Cap", root, Vector3.up * r * 0.35f, new Vector3(r * 1.5f, r * 0.5f, r * 1.5f), ice);
                    break;
                }
                case Defence.Shield:
                {
                    var m = pal.Glow("CoatShield", Tint(d), 2.4f);
                    foreach (float ring in big ? new[] { 0f, 90f } : new[] { 0f })
                    {
                        var rt = new GameObject("Ring").transform;
                        rt.SetParent(root, false);
                        rt.localRotation = Quaternion.Euler(ring, 0f, 0f);
                        for (int k = 0; k < 14; k++)
                        {
                            float a = k * Mathf.PI * 2f / 14f;
                            Prim.Make(PrimitiveType.Cube, "Seg", rt, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), new Vector3(0.06f, big ? 0.3f : 0.5f, r * 0.42f), m, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
                        }
                    }
                    if (!big) Prim.Make(PrimitiveType.Cylinder, "Cap", root, Vector3.up * 0.34f, new Vector3(r * 1.6f, 0.01f, r * 1.6f), m);
                    break;
                }
                case Defence.Steel:
                {
                    var steel = pal.Get("CoatSteel", Palette.Hex("#8A939E"), 0.55f, 0.85f);
                    var rivet = pal.Get("CoatRivet", Palette.Hex("#D0D6DC"), 0.6f, 0.9f);
                    float half = r * 0.82f;
                    foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down })
                    {
                        var rot = Quaternion.LookRotation(n, Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up);
                        Prim.Make(PrimitiveType.Cube, "Slab", root, n * half, new Vector3(half * 1.5f, half * 1.5f, 0.08f), steel, rot);
                        foreach (float x in new[] { -0.6f, 0.6f }) foreach (float z in new[] { -0.6f, 0.6f })
                            Prim.Make(PrimitiveType.Sphere, "Rivet", root, n * (half + 0.05f) + rot * new Vector3(x, z, 0f) * half, Vector3.one * 0.11f, rivet);
                    }
                    break;
                }
            }
            return root;
        }
    }
}
