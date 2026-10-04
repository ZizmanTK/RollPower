using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Gun models for the navy theme (Art.Theme 3). Design language from Everspace 2 ships and SYNTHETIK guns:
    /// white panels, dark insets, orange trim, small cyan lights, and only the emitter glowing in the gun's colour
    /// (so bloom does not smear the silhouette). Each gun has a distinct top-down shape. Local +Z is forward.
    /// </summary>
    public static class GunsNeon
    {
        public static void Build(int number, Palette pal, Transform root, List<Transform> muzzles)
        {
            var def = WeaponDef.All[number];
            var panel = pal.Get("NGPanel", Palette.Hex("#E4EAF0"), 0.6f, 0.05f);
            var inset = pal.Get("NGInset", Palette.Hex("#2A3442"), 0.5f, 0.2f);
            var navy = pal.Get("NGNavy", Palette.Hex("#13263A"), 0.6f, 0.15f);
            var trim = pal.Get("NGTrim", Palette.Hex("#FF7A2A"), 0.45f, 0f);
            var cyan = pal.Glow("NGCyan", Palette.Hex("#29B6F6"), 1.2f);
            var emit = pal.Glow("NGEmit" + number, def.color, 1.6f);
            var fwd = Quaternion.Euler(90f, 0f, 0f);

            // Turret base: navy disc, white yoke, thin cyan rim.
            P(PrimitiveType.Cylinder, root, new Vector3(0f, -0.13f, 0f), new Vector3(0.44f, 0.04f, 0.44f), navy);
            P(PrimitiveType.Cylinder, root, new Vector3(0f, -0.13f, 0f), new Vector3(0.46f, 0.012f, 0.46f), cyan);
            P(PrimitiveType.Cube, root, new Vector3(0f, -0.07f, -0.02f), new Vector3(0.22f, 0.08f, 0.22f), inset);

            switch (number)
            {
                case 1: // Railgun: long open rails, energy line between them, capacitor at the back.
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, -0.12f), new Vector3(0.26f, 0.15f, 0.36f), panel);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.076f, -0.12f), new Vector3(0.14f, 0.01f, 0.26f), inset);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.02f, -0.34f), new Vector3(0.3f, 0.11f, 0.1f), navy);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.078f, -0.34f), new Vector3(0.22f, 0.01f, 0.05f), trim);
                    foreach (float x in new[] { -0.075f, 0.075f })
                    {
                        P(PrimitiveType.Cube, root, new Vector3(x, 0f, 0.4f), new Vector3(0.045f, 0.08f, 0.86f), panel);
                        P(PrimitiveType.Cube, root, new Vector3(x, 0.041f, 0.4f), new Vector3(0.02f, 0.005f, 0.7f), trim);
                    }
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.42f), new Vector3(0.022f, 0.025f, 0.78f), emit);
                    foreach (float z in new[] { 0.14f, 0.38f, 0.62f })
                        P(PrimitiveType.Cube, root, new Vector3(0f, 0f, z), new Vector3(0.22f, 0.11f, 0.035f), inset);
                    muzzles.Add(Muzzle(root, new Vector3(0f, 0f, 0.86f)));
                    break;

                case 2: // Twin blasters: boxy receiver, two shrouded barrels with vents.
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, -0.06f), new Vector3(0.4f, 0.15f, 0.3f), panel);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.077f, -0.08f), new Vector3(0.18f, 0.01f, 0.18f), inset);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.04f, -0.215f), new Vector3(0.4f, 0.05f, 0.02f), trim);
                    foreach (float x in new[] { -0.12f, 0.12f })
                    {
                        P(PrimitiveType.Cube, root, new Vector3(x, 0f, 0.2f), new Vector3(0.14f, 0.12f, 0.22f), inset);
                        for (int i = 0; i < 3; i++)
                            P(PrimitiveType.Cube, root, new Vector3(x, 0.062f, 0.13f + i * 0.06f), new Vector3(0.1f, 0.012f, 0.02f), panel);
                        P(PrimitiveType.Cylinder, root, new Vector3(x, 0f, 0.4f), new Vector3(0.075f, 0.1f, 0.075f), navy, fwd);
                        P(PrimitiveType.Cylinder, root, new Vector3(x, 0f, 0.5f), new Vector3(0.06f, 0.006f, 0.06f), emit, fwd);
                        muzzles.Add(Muzzle(root, new Vector3(x, 0f, 0.52f)));
                    }
                    break;

                case 3: // Tri-shot: wedge body that fans out into three barrels.
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, -0.08f), new Vector3(0.28f, 0.15f, 0.26f), panel);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.077f, -0.1f), new Vector3(0.12f, 0.01f, 0.16f), inset);
                    foreach (float a in new[] { -16f, 0f, 16f })
                    {
                        var rot = Quaternion.Euler(0f, a, 0f);
                        Vector3 d = rot * Vector3.forward;
                        P(PrimitiveType.Cube, root, d * 0.14f, new Vector3(0.11f, 0.11f, 0.14f), panel, rot);
                        P(PrimitiveType.Cube, root, d * 0.14f + Vector3.up * 0.056f, new Vector3(0.03f, 0.006f, 0.12f), trim, rot);
                        P(PrimitiveType.Cylinder, root, d * 0.33f, new Vector3(0.065f, 0.12f, 0.065f), navy, rot * fwd);
                        P(PrimitiveType.Cylinder, root, d * 0.455f, new Vector3(0.055f, 0.006f, 0.055f), emit, rot * fwd);
                        muzzles.Add(Muzzle(root, d * 0.47f));
                    }
                    break;

                case 4: // Plasma cannon: caged glowing core feeding a wide, short barrel.
                    P(PrimitiveType.Sphere, root, new Vector3(0f, 0.03f, -0.14f), Vector3.one * 0.2f, emit);
                    for (int i = 0; i < 4; i++)
                    {
                        var rot = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                        P(PrimitiveType.Cube, root, new Vector3(0f, 0.03f, -0.14f) + rot * Vector3.forward * 0.14f, new Vector3(0.07f, 0.24f, 0.04f), panel, rot);
                    }
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.15f, -0.14f), new Vector3(0.3f, 0.02f, 0.3f), inset);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f, 0.14f), new Vector3(0.26f, 0.12f, 0.26f), panel, fwd);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f, 0.14f), new Vector3(0.27f, 0.02f, 0.27f), trim, fwd);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f, 0.3f), new Vector3(0.32f, 0.04f, 0.32f), navy, fwd);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f, 0.345f), new Vector3(0.16f, 0.006f, 0.16f), emit, fwd);
                    muzzles.Add(Muzzle(root, new Vector3(0f, 0.03f, 0.37f)));
                    break;

                case 5: // Scatter gun: short receiver, wide flat bell with five pellet ports.
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, -0.1f), new Vector3(0.28f, 0.16f, 0.3f), panel);
                    P(PrimitiveType.Cube, root, new Vector3(-0.06f, 0.082f, -0.1f), new Vector3(0.04f, 0.006f, 0.24f), trim);
                    P(PrimitiveType.Cube, root, new Vector3(0.06f, 0.082f, -0.1f), new Vector3(0.04f, 0.006f, 0.24f), trim);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.15f), new Vector3(0.36f, 0.1f, 0.2f), inset);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.31f), new Vector3(0.5f, 0.12f, 0.12f), panel);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.372f), new Vector3(0.46f, 0.08f, 0.006f), navy);
                    for (int i = -2; i <= 2; i++)
                        P(PrimitiveType.Cube, root, new Vector3(i * 0.085f, 0f, 0.377f), new Vector3(0.04f, 0.04f, 0.006f), emit);
                    muzzles.Add(Muzzle(root, new Vector3(0f, 0f, 0.4f)));
                    break;

                default: // Missile pod: armoured box, 2 x 3 missile noses, side fins, cyan hatch line.
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.05f, 0f), new Vector3(0.44f, 0.28f, 0.42f), panel);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.192f, 0f), new Vector3(0.3f, 0.006f, 0.02f), cyan);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.192f, -0.12f), new Vector3(0.36f, 0.006f, 0.06f), trim);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.05f, 0.212f), new Vector3(0.4f, 0.24f, 0.01f), inset);
                    foreach (float x in new[] { -0.24f, 0.24f })
                        P(PrimitiveType.Cube, root, new Vector3(x, 0.05f, -0.06f), new Vector3(0.04f, 0.2f, 0.28f), navy);
                    for (int r = 0; r < 2; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        var p = new Vector3((c - 1) * 0.12f, 0.12f - r * 0.13f, 0.225f);
                        P(PrimitiveType.Cylinder, root, p, new Vector3(0.08f, 0.02f, 0.08f), panel, fwd);
                        P(PrimitiveType.Sphere, root, p + Vector3.forward * 0.025f, Vector3.one * 0.06f, emit);
                        muzzles.Add(Muzzle(root, p + Vector3.forward * 0.06f));
                    }
                    break;
            }
        }

        static void P(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
            => Prim.Make(t, t.ToString(), parent, pos, scale, m, rot);

        static Transform Muzzle(Transform parent, Vector3 localPos)
        {
            var t = new GameObject("Muzzle").transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }
    }
}
