using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Gun models for the navy theme (Art.Theme 3). Each gun is built from a specific reference
    /// (store screenshots on the canvas "Guns & obstacles" page):
    ///   base     SYNTHETIK turret: square white plate with corner bolts
    ///   1 rail   The Ascent mech cannon: long white barrel, dark breech, cyan screen on the side
    ///   2 twin   SYNTHETIK drones: white ball body with a band, a short barrel pod on each side, antenna
    ///   3 tri    SYNTHETIK ML 7K: white receiver with oval cut-outs, capped barrels (fanned to three)
    ///   4 plasma The Riftbreaker cannon turret: armoured drum, gold rings, glowing core in the muzzle
    ///   5 scatter The Ascent ABR Commander / SYNTHETIK drum launcher: drum magazine, wide short muzzle
    ///   6 missile SYNTHETIK pipe stacks: bundle of tubes with yellow bands in a white frame
    /// Only the emitter glows, in the gun's colour. Local +Z is forward.
    /// </summary>
    public static class GunsNeon
    {
        public static void Build(int number, Palette pal, Transform root, List<Transform> muzzles)
        {
            var def = WeaponDef.All[number];
            var white = pal.Get("NGPanel", Palette.Hex("#E4EAF0"), 0.6f, 0.05f);
            var dark = pal.Get("NGInset", Palette.Hex("#2A3442"), 0.5f, 0.2f);
            var steel = pal.Get("NGSteel", Palette.Hex("#6A7686"), 0.55f, 0.4f);
            var amber = pal.Get("NGAmber", Palette.Hex("#FFB020"), 0.45f, 0.1f);
            var orange = pal.Get("NGOrange", Palette.Hex("#FF7A2A"), 0.45f, 0f);
            var screen = pal.Glow("NGScreen", Palette.Hex("#29B6F6"), 1.3f);
            var emit = pal.Glow("NGEmit" + def.id, def.color, 1.6f);
            var fwd = Quaternion.Euler(90f, 0f, 0f);
            var side = Quaternion.Euler(0f, 0f, 90f);

            // SYNTHETIK turret base: square white plate, dark inset, four bolts.
            P(PrimitiveType.Cube, root, new Vector3(0f, -0.13f, 0f), new Vector3(0.44f, 0.05f, 0.44f), white);
            P(PrimitiveType.Cube, root, new Vector3(0f, -0.104f, 0f), new Vector3(0.3f, 0.006f, 0.3f), dark);
            foreach (float bx in new[] { -0.17f, 0.17f })
            foreach (float bz in new[] { -0.17f, 0.17f })
                P(PrimitiveType.Cylinder, root, new Vector3(bx, -0.1f, bz), new Vector3(0.045f, 0.012f, 0.045f), steel);
            P(PrimitiveType.Cylinder, root, new Vector3(0f, -0.07f, 0f), new Vector3(0.2f, 0.05f, 0.2f), dark);

            switch (def.model)
            {
                case 1: // The Ascent mech cannon
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.01f, -0.14f), new Vector3(0.26f, 0.18f, 0.32f), dark);
                    foreach (float x in new[] { -0.131f, 0.131f })
                        P(PrimitiveType.Cube, root, new Vector3(x, 0.02f, -0.14f), new Vector3(0.006f, 0.08f, 0.17f), screen);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.102f, -0.18f), new Vector3(0.16f, 0.006f, 0.12f), white);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.01f, 0.08f), new Vector3(0.15f, 0.08f, 0.15f), dark, fwd);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.01f, 0.44f), new Vector3(0.11f, 0.32f, 0.11f), white, fwd);
                    foreach (float z in new[] { 0.3f, 0.62f })
                        P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.01f, z), new Vector3(0.125f, 0.02f, 0.125f), dark, fwd);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.01f, 0.765f), new Vector3(0.08f, 0.006f, 0.08f), emit, fwd);
                    muzzles.Add(Muzzle(root, new Vector3(0f, 0.01f, 0.78f)));
                    break;

                case 2: // SYNTHETIK drone
                    P(PrimitiveType.Sphere, root, new Vector3(0f, 0.04f, -0.02f), Vector3.one * 0.3f, white);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.04f, -0.02f), new Vector3(0.305f, 0.03f, 0.305f), dark);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.12f, 0.08f), new Vector3(0.08f, 0.04f, 0.06f), dark);
                    P(PrimitiveType.Cylinder, root, new Vector3(0.05f, 0.25f, -0.08f), new Vector3(0.012f, 0.1f, 0.012f), steel, Quaternion.Euler(-20f, 0f, -15f));
                    foreach (float x in new[] { -0.19f, 0.19f })
                    {
                        P(PrimitiveType.Cube, root, new Vector3(x * 0.7f, 0.03f, 0f), new Vector3(0.1f, 0.05f, 0.06f), dark);
                        P(PrimitiveType.Cylinder, root, new Vector3(x, 0.03f, 0.12f), new Vector3(0.09f, 0.17f, 0.09f), steel, fwd);
                        P(PrimitiveType.Cylinder, root, new Vector3(x, 0.03f, 0.27f), new Vector3(0.1f, 0.03f, 0.1f), dark, fwd);
                        P(PrimitiveType.Cylinder, root, new Vector3(x, 0.03f, 0.302f), new Vector3(0.065f, 0.006f, 0.065f), emit, fwd);
                        muzzles.Add(Muzzle(root, new Vector3(x, 0.03f, 0.32f)));
                    }
                    break;

                case 3: // SYNTHETIK ML 7K, barrels fanned to three
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.01f, -0.08f), new Vector3(0.3f, 0.17f, 0.34f), white);
                    for (int i = 0; i < 3; i++)
                        P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.097f, -0.19f + i * 0.1f), new Vector3(0.18f, 0.006f, 0.06f), dark);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.01f, -0.255f), new Vector3(0.3f, 0.06f, 0.012f), orange);
                    foreach (float a in new[] { -16f, 0f, 16f })
                    {
                        var r = Quaternion.Euler(0f, a, 0f);
                        Vector3 d = r * Vector3.forward;
                        P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.01f, 0.09f) + d * 0.16f, new Vector3(0.07f, 0.14f, 0.07f), steel, r * fwd);
                        P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.01f, 0.09f) + d * 0.31f, new Vector3(0.085f, 0.025f, 0.085f), dark, r * fwd);
                        P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.01f, 0.09f) + d * 0.338f, new Vector3(0.055f, 0.006f, 0.055f), emit, r * fwd);
                        muzzles.Add(Muzzle(root, new Vector3(0f, 0.01f, 0.09f) + d * 0.35f));
                    }
                    break;

                case 4: // The Riftbreaker cannon turret
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.02f, -0.17f), new Vector3(0.22f, 0.14f, 0.12f), dark);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f, 0.02f), new Vector3(0.28f, 0.15f, 0.28f), white, fwd);
                    foreach (float z in new[] { -0.07f, 0.11f })
                        P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f, z), new Vector3(0.292f, 0.025f, 0.292f), amber, fwd);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f, 0.17f), new Vector3(0.24f, 0.01f, 0.24f), dark, fwd);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f, 0.175f), new Vector3(0.16f, 0.01f, 0.16f), emit, fwd);
                    muzzles.Add(Muzzle(root, new Vector3(0f, 0.03f, 0.21f)));
                    break;

                case 5: // Drum-fed scatter gun (The Ascent ABR Commander, SYNTHETIK drum launcher)
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, -0.06f), new Vector3(0.24f, 0.15f, 0.38f), white);
                    P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.12f, -0.06f), new Vector3(0.2f, 0.09f, 0.2f), orange, side);
                    foreach (float x in new[] { -0.092f, 0.092f })
                        P(PrimitiveType.Cylinder, root, new Vector3(x, 0.12f, -0.06f), new Vector3(0.15f, 0.006f, 0.15f), dark, side);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.2f), new Vector3(0.3f, 0.1f, 0.16f), dark);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.31f), new Vector3(0.44f, 0.11f, 0.08f), white);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.352f), new Vector3(0.4f, 0.07f, 0.006f), dark);
                    for (int i = -2; i <= 2; i++)
                        P(PrimitiveType.Cube, root, new Vector3(i * 0.075f, 0f, 0.357f), new Vector3(0.035f, 0.035f, 0.006f), emit);
                    muzzles.Add(Muzzle(root, new Vector3(0f, 0f, 0.38f)));
                    break;

                default: // SYNTHETIK pipe stack as a missile pod
                    foreach (float z in new[] { -0.17f, 0.17f })
                        P(PrimitiveType.Cube, root, new Vector3(0f, 0.06f, z), new Vector3(0.44f, 0.3f, 0.04f), white);
                    P(PrimitiveType.Cube, root, new Vector3(0f, 0.215f, 0f), new Vector3(0.1f, 0.02f, 0.38f), dark);
                    for (int r = 0; r < 2; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        var p = new Vector3((c - 1) * 0.125f, 0.12f - r * 0.125f, 0.02f);
                        P(PrimitiveType.Cylinder, root, p, new Vector3(0.11f, 0.2f, 0.11f), dark, fwd);
                        P(PrimitiveType.Cylinder, root, p + Vector3.forward * 0.08f, new Vector3(0.115f, 0.025f, 0.115f), amber, fwd);
                        P(PrimitiveType.Sphere, root, p + Vector3.forward * 0.21f, new Vector3(0.08f, 0.08f, 0.1f), emit);
                        muzzles.Add(Muzzle(root, p + Vector3.forward * 0.25f));
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
