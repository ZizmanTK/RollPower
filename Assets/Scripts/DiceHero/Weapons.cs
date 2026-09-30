using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>Stats for the gun tied to each top number.</summary>
    public class WeaponDef
    {
        public int number;
        public string name;
        public string role;
        public Color color;
        public float cooldown;
        public int shots;            // projectiles per trigger pull
        public float spread;         // total fan angle in degrees
        public float speed;
        public float damage;
        public float range;
        public float radius = 0.12f; // projectile size
        public float aoe;            // explosion radius (0 = none)
        public bool armorPiercing;
        public bool homing;
        public bool beam;            // instant hitscan beam (railgun)

        public static readonly WeaponDef[] All =
        {
            null,
            new WeaponDef { number = 1, name = "RAILGUN", role = "Piercing beam, pierces armour", color = Palette.Hex("#35E6FF"),
                cooldown = 0.9f, shots = 1, speed = 0f, damage = 3f, range = 18f, beam = true, armorPiercing = true },
            new WeaponDef { number = 2, name = "TWIN BLASTERS", role = "Fast twin bolts", color = Palette.Hex("#5CFF8A"),
                cooldown = 0.22f, shots = 2, spread = 0f, speed = 22f, damage = 1f, range = 16f },
            new WeaponDef { number = 3, name = "TRI-SHOT", role = "3-way spread, hits fliers", color = Palette.Hex("#FFE14D"),
                cooldown = 0.42f, shots = 3, spread = 24f, speed = 20f, damage = 1f, range = 16f },
            new WeaponDef { number = 4, name = "PLASMA CANNON", role = "Explosive orb, breaks armour", color = Palette.Hex("#FF3FA4"),
                cooldown = 1.0f, shots = 1, speed = 12f, damage = 4f, range = 14f, radius = 0.3f, aoe = 1.6f, armorPiercing = true },
            new WeaponDef { number = 5, name = "SCATTER GUN", role = "5 pellets, close range", color = Palette.Hex("#FF8A2A"),
                cooldown = 0.65f, shots = 5, spread = 50f, speed = 19f, damage = 1f, range = 7f, radius = 0.1f },
            new WeaponDef { number = 6, name = "MISSILE POD", role = "6 homing missiles, anti-swarm", color = Palette.Hex("#FF4B3A"),
                cooldown = 2.0f, shots = 6, spread = 70f, speed = 12f, damage = 1f, range = 20f, radius = 0.14f, aoe = 1.0f, homing = true },
        };
    }

    /// <summary>Builds the six gun models from primitives. Each returns its muzzle points (local +Z is forward).</summary>
    public static class GunModels
    {
        public static Transform Build(int number, Palette pal, Transform parent, List<Transform> muzzles)
        {
            var def = WeaponDef.All[number];
            var root = new GameObject("Gun" + number + "_" + def.name).transform;
            root.SetParent(parent, false);
            var dark = pal.Get("GunDark", Palette.Hex("#1A1D23"), 0.6f, 0.8f);
            var steel = pal.Get("GunSteel", Palette.Hex("#8C96A3"), 0.75f, 0.95f);
            var glow = pal.Glow("GunGlow" + number, def.color, 3f);

            // Common turret base
            Prim.Make(PrimitiveType.Cylinder, "Base", root, new Vector3(0f, -0.12f, 0f), new Vector3(0.42f, 0.06f, 0.42f), dark);
            Prim.Make(PrimitiveType.Cylinder, "BaseRing", root, new Vector3(0f, -0.12f, 0f), new Vector3(0.46f, 0.02f, 0.46f), glow);
            var fwd = Quaternion.Euler(90f, 0f, 0f); // cylinder axis → +Z

            switch (number)
            {
                case 1: // Railgun: long rail with glowing coils
                    Prim.Make(PrimitiveType.Cube, "Body", root, new Vector3(0f, 0f, -0.05f), new Vector3(0.22f, 0.18f, 0.5f), steel);
                    Prim.Make(PrimitiveType.Cube, "RailL", root, new Vector3(-0.06f, 0f, 0.45f), new Vector3(0.04f, 0.1f, 0.9f), dark);
                    Prim.Make(PrimitiveType.Cube, "RailR", root, new Vector3(0.06f, 0f, 0.45f), new Vector3(0.04f, 0.1f, 0.9f), dark);
                    for (int i = 0; i < 4; i++)
                        Prim.Make(PrimitiveType.Cylinder, "Coil", root, new Vector3(0f, 0f, 0.2f + i * 0.2f), new Vector3(0.2f, 0.02f, 0.2f), glow, fwd);
                    muzzles.Add(Muzzle(root, new Vector3(0f, 0f, 0.95f)));
                    break;
                case 2: // Twin blasters
                    Prim.Make(PrimitiveType.Cube, "Body", root, new Vector3(0f, 0f, 0f), new Vector3(0.4f, 0.16f, 0.4f), steel);
                    foreach (float x in new[] { -0.13f, 0.13f })
                    {
                        Prim.Make(PrimitiveType.Cylinder, "Barrel", root, new Vector3(x, 0f, 0.35f), new Vector3(0.09f, 0.25f, 0.09f), dark, fwd);
                        Prim.Make(PrimitiveType.Cylinder, "Tip", root, new Vector3(x, 0f, 0.6f), new Vector3(0.1f, 0.02f, 0.1f), glow, fwd);
                        muzzles.Add(Muzzle(root, new Vector3(x, 0f, 0.62f)));
                    }
                    break;
                case 3: // Tri-shot: three fanned barrels
                    Prim.Make(PrimitiveType.Cube, "Body", root, new Vector3(0f, 0f, 0f), new Vector3(0.36f, 0.18f, 0.36f), steel);
                    foreach (float a in new[] { -12f, 0f, 12f })
                    {
                        var rot = Quaternion.Euler(0f, a, 0f);
                        Vector3 dir = rot * Vector3.forward;
                        Prim.Make(PrimitiveType.Cylinder, "Barrel", root, dir * 0.38f, new Vector3(0.08f, 0.22f, 0.08f), dark, rot * fwd);
                        Prim.Make(PrimitiveType.Cylinder, "Tip", root, dir * 0.6f, new Vector3(0.09f, 0.02f, 0.09f), glow, rot * fwd);
                        muzzles.Add(Muzzle(root, dir * 0.62f));
                    }
                    break;
                case 4: // Plasma cannon: fat barrel with a glowing chamber
                    Prim.Make(PrimitiveType.Sphere, "Chamber", root, new Vector3(0f, 0.02f, -0.05f), new Vector3(0.42f, 0.36f, 0.42f), steel);
                    Prim.Make(PrimitiveType.Sphere, "Core", root, new Vector3(0f, 0.2f, -0.05f), new Vector3(0.2f, 0.08f, 0.2f), glow);
                    Prim.Make(PrimitiveType.Cylinder, "Barrel", root, new Vector3(0f, 0.02f, 0.35f), new Vector3(0.26f, 0.25f, 0.26f), dark, fwd);
                    Prim.Make(PrimitiveType.Cylinder, "Muzzle", root, new Vector3(0f, 0.02f, 0.6f), new Vector3(0.3f, 0.03f, 0.3f), glow, fwd);
                    muzzles.Add(Muzzle(root, new Vector3(0f, 0.02f, 0.65f)));
                    break;
                case 5: // Scatter gun: wide flat barrel block
                    Prim.Make(PrimitiveType.Cube, "Body", root, new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.2f, 0.36f), steel);
                    Prim.Make(PrimitiveType.Cube, "Barrel", root, new Vector3(0f, 0f, 0.32f), new Vector3(0.46f, 0.14f, 0.34f), dark);
                    Prim.Make(PrimitiveType.Cube, "Vent", root, new Vector3(0f, 0f, 0.5f), new Vector3(0.44f, 0.05f, 0.02f), glow);
                    muzzles.Add(Muzzle(root, new Vector3(0f, 0f, 0.52f)));
                    break;
                default: // Missile pod: 2 x 3 launch tubes
                    Prim.Make(PrimitiveType.Cube, "Pod", root, new Vector3(0f, 0.05f, 0.05f), new Vector3(0.5f, 0.34f, 0.5f), steel);
                    for (int r = 0; r < 2; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        var p = new Vector3((c - 1) * 0.15f, 0.12f - r * 0.15f, 0.31f);
                        Prim.Make(PrimitiveType.Cylinder, "Tube", root, p, new Vector3(0.1f, 0.02f, 0.1f), glow, fwd);
                        muzzles.Add(Muzzle(root, p + Vector3.forward * 0.05f));
                    }
                    break;
            }
            return root;
        }

        static Transform Muzzle(Transform parent, Vector3 localPos)
        {
            var t = new GameObject("Muzzle").transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }
    }

    /// <summary>Handles the active gun: switching on roll, aiming, recoil and firing.</summary>
    public class WeaponSystem
    {
        readonly DiceController dice;
        readonly Palette pal;
        readonly Transform[] guns = new Transform[7];
        readonly List<Transform>[] muzzles = new List<Transform>[7];
        float cooldown, recoil;

        /// <summary>World-space aim direction on the ground plane.</summary>
        public Vector3 AimDir { get; set; } = Vector3.forward;
        public WeaponDef Current => WeaponDef.All[dice.TopNumber];
        public float CooldownFraction => Current == null ? 0f : Mathf.Clamp01(cooldown / Current.cooldown);

        public WeaponSystem(DiceController dice, Palette pal)
        {
            this.dice = dice;
            this.pal = pal;
            for (int n = 1; n <= 6; n++)
            {
                muzzles[n] = new List<Transform>();
                guns[n] = GunModels.Build(n, pal, dice.Model.WeaponMount, muzzles[n]);
                guns[n].localScale = Vector3.one * 1.4f;
            }
            ShowGun(dice.TopNumber);
            dice.TopChanged += (oldTop, newTop) => { ShowGun(newTop); cooldown = 0.15f; Overcharge = OverchargeTime; };
        }

        /// <summary>Seconds of double fire rate left (granted by every fresh roll).</summary>
        public float Overcharge { get; private set; }
        public const float OverchargeTime = 3f;
        public Enemy Target { get; private set; }
        public event System.Action<WeaponDef> Fired;

        void ShowGun(int number)
        {
            for (int n = 1; n <= 6; n++) guns[n].gameObject.SetActive(n == number);
        }

        /// <summary>Auto-targeting: nearest enemy this gun can hurt; otherwise the nearest armoured one (so you see it deflect).</summary>
        Enemy PickTarget(WeaponDef def)
        {
            Vector3 p = dice.transform.position;
            Enemy best = null, fallback = null;
            float bestD = def.range * def.range, fallbackD = bestD;
            foreach (var t in Targets.All)
            {
                if (!(t is Enemy e) || !e.Alive || e.spawnT < 1f) continue;
                Vector3 d = e.pos - p; d.y = 0f;
                float sq = d.sqrMagnitude;
                if (Enemy.CanHit(def, e.kind)) { if (sq < bestD) { bestD = sq; best = e; } }
                else if (!e.Flying && sq < fallbackD) { fallbackD = sq; fallback = e; }
            }
            return best ?? fallback;
        }

        public void Step(float dt, bool allowFire)
        {
            var def = Current;
            cooldown -= dt;
            Overcharge -= dt;
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 2.5f);

            Target = PickTarget(def);
            if (Target != null)
            {
                // Lead the target a little so projectiles connect with moving enemies.
                Vector3 to = Target.pos - dice.transform.position;
                float travel = def.beam || def.homing ? 0f : to.magnitude / def.speed;
                Vector3 aim = Target.pos + Target.vel * travel - dice.transform.position;
                aim.y = 0f;
                if (aim.sqrMagnitude > 0.01f) AimDir = aim.normalized;
            }

            var mount = dice.Model.WeaponMount;
            if (AimDir.sqrMagnitude > 0.001f)
                mount.rotation = Quaternion.Slerp(mount.rotation, Quaternion.LookRotation(AimDir, Vector3.up), 1f - Mathf.Exp(-20f * dt));
            guns[dice.TopNumber].localPosition = new Vector3(0f, 0f, -recoil * 0.25f);

            bool aimed = Vector3.Angle(mount.forward, AimDir) < 25f;
            if (allowFire && Target != null && aimed && cooldown <= 0f && !dice.IsRolling) Fire();
        }

        void Fire()
        {
            var def = Current;
            cooldown = def.cooldown * (Overcharge > 0f ? 0.5f : 1f);
            Fired?.Invoke(def);
            recoil = def.number == 4 || def.number == 1 ? 1f : 0.5f;
            var ms = muzzles[def.number];
            Vector3 fwd = dice.Model.WeaponMount.forward;

            for (int i = 0; i < def.shots; i++)
            {
                var muzzle = ms[i % ms.Count];
                float t = def.shots == 1 ? 0f : i / (float)(def.shots - 1) - 0.5f;
                Vector3 dir = Quaternion.Euler(0f, t * def.spread, 0f) * fwd;
                if (def.beam) Projectiles.Beam(pal, def, muzzle.position, dir);
                else Projectiles.Spawn(pal, def, muzzle.position, dir, i * (def.homing ? 0.06f : 0f));
            }
            foreach (var m in ms) Fx.Flash(pal, m.position, def.color, def.number == 4 ? 0.5f : 0.3f, 0.08f);
        }
    }
}
