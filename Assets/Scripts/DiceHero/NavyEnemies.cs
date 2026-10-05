using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Enemy models for the navy theme (Art.Theme 3). Shared colour language from the Nex Machina robots:
    /// red shells, silver domes, gunmetal joints, glowing red eyes. Each shape comes from a reference:
    ///   Crawler  Assault Android Cactus spider robot: low armoured body, four jointed legs, stripes at the knees
    ///   Drone    Assault Android Cactus flying pods: egg body, big round lens, side thrusters
    ///   Mite     Nex Machina small walkers: red cup base, silver dome, red eye
    ///   Bomber   Nex Machina big red domes (petal armour, glowing core) carrying an
    ///            Assault Android Cactus spiked mine
    ///   Tank     The Ascent heavy mech: round armoured torso, shoulder pads, arm cannon, stubby legs;
    ///            rust-orange armour marks it as armoured
    /// Sizes match the gameplay radii in EnemyModels. Local +Z is forward.
    /// </summary>
    public static class NavyEnemies
    {
        static Material shell, silver, metal, eye, glow, rust;

        public static void Build(EnemyKind kind, Palette pal, Transform root)
        {
            shell = pal.Get("NEShell", Palette.Hex("#D8263A"), 0.4f, 0.05f);
            silver = pal.Get("NESilver", Palette.Hex("#C9D1DB"), 0.45f, 0.15f);
            metal = pal.Get("NEMetal", Palette.Hex("#2A3039"), 0.55f, 0.5f);
            eye = pal.Glow("NEEye", Palette.Hex("#FF2A3D"), 2.6f, Palette.Hex("#FF2A3D"));
            glow = pal.Glow("NEGlow", Palette.Hex("#FF3B4E"), 1.3f, Palette.Hex("#5A0D16"));
            rust = pal.Get("NERust", Palette.Hex("#C7642E"), 0.35f, 0.15f);
            switch (kind)
            {
                case EnemyKind.Crawler: Crawler(root); break;
                case EnemyKind.Drone: Drone(pal, root); break;
                case EnemyKind.Tank: Tank(root); break;
                case EnemyKind.Bomber: Bomber(pal, root); break;
                default: Mite(root); break;
            }
        }

        static void P(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
            => Prim.Make(t, t.ToString(), parent, pos, scale, m, rot);

        /// <summary>Box limb from a to b.</summary>
        static void Seg(Transform parent, Vector3 a, Vector3 b, float thick, Material m)
            => Prim.Make(PrimitiveType.Cube, "Limb", parent, (a + b) * 0.5f, new Vector3(thick, thick, (b - a).magnitude), m, Quaternion.LookRotation(b - a));

        static void Crawler(Transform r)
        {
            P(PrimitiveType.Cube, r, new Vector3(0f, 0.3f, 0f), new Vector3(0.4f, 0.12f, 0.5f), metal);
            P(PrimitiveType.Cube, r, new Vector3(0f, 0.39f, -0.04f), new Vector3(0.46f, 0.08f, 0.42f), shell);
            P(PrimitiveType.Cube, r, new Vector3(0f, 0.44f, -0.06f), new Vector3(0.3f, 0.04f, 0.26f), silver);
            P(PrimitiveType.Cube, r, new Vector3(0f, 0.33f, 0.26f), new Vector3(0.3f, 0.12f, 0.08f), shell);
            P(PrimitiveType.Cube, r, new Vector3(0f, 0.34f, 0.305f), new Vector3(0.2f, 0.04f, 0.02f), eye);
            for (int i = 0; i < 4; i++)
            {
                float a = 45f + i * 90f;
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Vector3 hip = d * 0.2f + Vector3.up * 0.32f, knee = d * 0.46f + Vector3.up * 0.52f, foot = d * 0.62f;
                Seg(r, hip, knee, 0.12f, shell);
                Seg(r, knee, foot, 0.09f, metal);
                P(PrimitiveType.Sphere, r, knee, Vector3.one * 0.12f, silver);
                P(PrimitiveType.Cube, r, Vector3.Lerp(knee, foot, 0.25f), new Vector3(0.1f, 0.03f, 0.1f), glow, Quaternion.LookRotation(foot - knee));
                P(PrimitiveType.Cube, r, foot + Vector3.up * 0.02f, new Vector3(0.1f, 0.04f, 0.12f), metal, Quaternion.Euler(0f, a, 0f));
            }
        }

        static void Drone(Palette pal, Transform r)
        {
            var c = new Vector3(0f, 2.1f, 0f);
            P(PrimitiveType.Sphere, r, c, new Vector3(0.5f, 0.42f, 0.56f), silver);
            P(PrimitiveType.Cube, r, c + new Vector3(0f, 0.2f, -0.04f), new Vector3(0.16f, 0.06f, 0.44f), shell);
            P(PrimitiveType.Cylinder, r, c + new Vector3(0f, -0.2f, 0f), new Vector3(0.36f, 0.04f, 0.36f), shell);
            P(PrimitiveType.Cylinder, r, c + new Vector3(0f, 0.02f, 0.3f), new Vector3(0.32f, 0.08f, 0.32f), metal, Quaternion.Euler(90f, 0f, 0f));
            P(PrimitiveType.Cylinder, r, c + new Vector3(0f, 0.02f, 0.385f), new Vector3(0.22f, 0.01f, 0.22f), eye, Quaternion.Euler(90f, 0f, 0f));
            foreach (float s in new[] { -1f, 1f })
            {
                P(PrimitiveType.Cube, r, c + new Vector3(s * 0.27f, -0.02f, -0.05f), new Vector3(0.14f, 0.06f, 0.14f), metal);
                P(PrimitiveType.Cylinder, r, c + new Vector3(s * 0.36f, -0.02f, -0.06f), new Vector3(0.16f, 0.15f, 0.16f), shell, Quaternion.Euler(90f, 0f, 0f));
                P(PrimitiveType.Cylinder, r, c + new Vector3(s * 0.36f, -0.02f, -0.215f), new Vector3(0.12f, 0.01f, 0.12f), eye, Quaternion.Euler(90f, 0f, 0f));
            }
            // Ground marker so players can see where the drone is (gameplay, kept from 2.0).
            P(PrimitiveType.Cylinder, r, new Vector3(0f, 0.02f, 0f), new Vector3(0.8f, 0.005f, 0.8f), pal.Glow("DroneMarker", Palette.Hex("#FF2A3D"), 0.8f, Palette.Hex("#FF2A3D")));
        }

        static void Mite(Transform r)
        {
            P(PrimitiveType.Cylinder, r, new Vector3(0f, 0.11f, 0f), new Vector3(0.4f, 0.07f, 0.4f), shell);
            P(PrimitiveType.Cylinder, r, new Vector3(0f, 0.15f, 0f), new Vector3(0.42f, 0.015f, 0.42f), glow);
            P(PrimitiveType.Sphere, r, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.26f, 0.3f), silver);
            P(PrimitiveType.Sphere, r, new Vector3(0f, 0.26f, 0.13f), Vector3.one * 0.11f, eye);
            for (int i = 0; i < 3; i++)
            {
                var d = Quaternion.Euler(0f, 60f + i * 120f, 0f) * Vector3.forward;
                Seg(r, d * 0.17f + Vector3.up * 0.1f, d * 0.34f, 0.05f, metal);
            }
        }

        static void Bomber(Palette pal, Transform r)
        {
            P(PrimitiveType.Cylinder, r, new Vector3(0f, 0.12f, 0f), new Vector3(0.86f, 0.1f, 0.86f), metal);
            P(PrimitiveType.Sphere, r, new Vector3(0f, 0.26f, 0f), new Vector3(0.32f, 0.34f, 0.32f), metal);
            for (int i = 0; i < 6; i++)
            {
                var g = Quaternion.Euler(0f, 30f + i * 60f, 0f);
                P(PrimitiveType.Cube, r, g * new Vector3(0f, 0.28f, 0.27f), new Vector3(0.05f, 0.26f, 0.05f), glow, g * Quaternion.Euler(20f, 0f, 0f));
            }
            for (int i = 0; i < 6; i++)
            {
                var rot = Quaternion.Euler(0f, i * 60f, 0f);
                P(PrimitiveType.Cube, r, rot * new Vector3(0f, 0.28f, 0.3f), new Vector3(0.22f, 0.32f, 0.08f), shell, rot * Quaternion.Euler(20f, 0f, 0f));
            }
            P(PrimitiveType.Cube, r, new Vector3(0f, 0.2f, 0.355f), new Vector3(0.14f, 0.05f, 0.03f), eye);
            // Carried spiked mine.
            var m = new Vector3(0f, 0.72f, -0.08f);
            P(PrimitiveType.Sphere, r, m, Vector3.one * 0.3f, pal.Get("BombBody", Palette.Hex("#4A505C"), 0.55f, 0.75f));
            P(PrimitiveType.Cylinder, r, m, new Vector3(0.31f, 0.02f, 0.31f), pal.Glow("BombRed", Palette.Hex("#FF2A3D"), 4f));
            foreach (var d in new[] { Vector3.up, Vector3.left, Vector3.right, Vector3.forward, Vector3.back, (Vector3.up + Vector3.left).normalized, (Vector3.up + Vector3.right).normalized })
                Seg(r, m + d * 0.12f, m + d * 0.24f, 0.035f, silver);
        }

        static void Tank(Transform r)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                Seg(r, new Vector3(s * 0.26f, 0.42f, 0f), new Vector3(s * 0.32f, 0.12f, 0.05f), 0.18f, metal);
                P(PrimitiveType.Cube, r, new Vector3(s * 0.32f, 0.06f, 0.06f), new Vector3(0.26f, 0.12f, 0.38f), metal);
                P(PrimitiveType.Cube, r, new Vector3(s * 0.32f, 0.13f, 0.18f), new Vector3(0.22f, 0.04f, 0.1f), rust);
            }
            P(PrimitiveType.Cube, r, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.14f, 0.34f), metal);
            P(PrimitiveType.Sphere, r, new Vector3(0f, 0.76f, -0.04f), new Vector3(0.7f, 0.54f, 0.6f), rust);
            P(PrimitiveType.Cube, r, new Vector3(0f, 0.86f, 0.26f), new Vector3(0.34f, 0.1f, 0.12f), metal);
            P(PrimitiveType.Cube, r, new Vector3(0f, 0.86f, 0.325f), new Vector3(0.26f, 0.04f, 0.02f), eye);
            P(PrimitiveType.Cube, r, new Vector3(0f, 0.66f, 0.28f), new Vector3(0.5f, 0.22f, 0.07f), silver, Quaternion.Euler(-15f, 0f, 0f));
            foreach (float s in new[] { -1f, 1f })
                P(PrimitiveType.Sphere, r, new Vector3(s * 0.4f, 0.98f, -0.04f), new Vector3(0.28f, 0.12f, 0.34f), silver);
            // Arm cannon on the right, short claw arm on the left.
            Seg(r, new Vector3(0.5f, 0.86f, 0f), new Vector3(0.56f, 0.66f, 0.1f), 0.14f, metal);
            P(PrimitiveType.Cylinder, r, new Vector3(0.56f, 0.64f, 0.36f), new Vector3(0.2f, 0.32f, 0.2f), metal, Quaternion.Euler(90f, 0f, 0f));
            P(PrimitiveType.Cylinder, r, new Vector3(0.56f, 0.64f, 0.5f), new Vector3(0.23f, 0.05f, 0.23f), rust, Quaternion.Euler(90f, 0f, 0f));
            P(PrimitiveType.Cylinder, r, new Vector3(0.56f, 0.64f, 0.685f), new Vector3(0.12f, 0.01f, 0.12f), eye, Quaternion.Euler(90f, 0f, 0f));
            Seg(r, new Vector3(-0.5f, 0.86f, 0f), new Vector3(-0.55f, 0.6f, 0.2f), 0.12f, metal);
            P(PrimitiveType.Cube, r, new Vector3(-0.55f, 0.58f, 0.27f), new Vector3(0.16f, 0.12f, 0.14f), silver);
        }
    }
}
