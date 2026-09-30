using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    public enum EnemyKind { Crawler, Drone, Tank, Mite, Bomber, Boss }

    /// <summary>
    /// A hostile bot. Weaknesses:
    ///   Crawler – any gun.  Drone – flies: only anti-air guns (3 tri-shot, 6 missiles).
    ///   Tank – armoured: only armour-piercing guns (1 railgun, 4 plasma).  Mite – fast swarm, 1 HP.
    ///   Bomber – any gun, plants bombs.  Boss (High Roller) – a giant die: only the gun matching its top number hurts it.
    /// Bombs and landing slams hurt everything.
    /// </summary>
    public class Enemy : ITarget
    {
        public EnemyKind kind;
        public Transform t;
        public float hp, maxHp, speed, radius, fireTimer, hitFlash, spawnT, abilityTimer;
        public Vector3 pos, vel;
        public BossState boss;
        Renderer[] renderers;
        Material[] baseMats;
        Material flashMat;

        public Vector3 Position => pos + Vector3.up * (Flying ? 2.1f : kind == EnemyKind.Boss ? 1f : radius);
        public float Radius => radius;
        public bool Alive => hp > 0f && t != null;
        public bool Flying => kind == EnemyKind.Drone;
        public bool Armored => kind == EnemyKind.Tank;
        public int Weakness => boss != null ? DiceController.TopFor(boss.orientation) : 0;

        public static string Hint(EnemyKind k)
        {
            switch (k)
            {
                case EnemyKind.Drone: return "DRONES FLY: USE 3 TRI-SHOT OR 6 MISSILES";
                case EnemyKind.Tank: return "TANKS ARE ARMOURED: USE 1 RAILGUN OR 4 PLASMA";
                case EnemyKind.Mite: return "MITE SWARM: USE 5 SCATTER, 6 MISSILES OR 2 TWIN";
                case EnemyKind.Bomber: return "BOMBERS PLANT BOMBS: SHOVE THEM OFF THE EDGE";
                case EnemyKind.Boss: return "HIGH ROLLER: MATCH THE NUMBER ON ITS TOP FACE";
                default: return "CRAWLERS: ANY GUN WORKS";
            }
        }

        public static string Plural(EnemyKind k)
        {
            switch (k)
            {
                case EnemyKind.Drone: return "DRONES";
                case EnemyKind.Tank: return "TANKS";
                case EnemyKind.Mite: return "MITES";
                case EnemyKind.Bomber: return "BOMBERS";
                case EnemyKind.Boss: return "HIGH ROLLER";
                default: return "CRAWLERS";
            }
        }

        public bool CanBeHitBy(WeaponDef w)
        {
            switch (kind)
            {
                case EnemyKind.Drone: return w.number == 3 || w.number == 6;
                case EnemyKind.Tank: return w.armorPiercing;
                case EnemyKind.Boss: return w.number == Weakness;
                default: return true;
            }
        }

        public bool Hit(WeaponDef w, float damage, Vector3 from)
        {
            if (!Alive) return false;
            if (!CanBeHitBy(w))
            {
                if (Game.I != null) Game.I.Deflect(this, kind == EnemyKind.Boss ? $"NEED A {Weakness}" : Flying ? "OUT OF REACH" : "DEFLECTED");
                return false;
            }
            hp -= damage * RunStats.Current.damageMul;
            hitFlash = 0.1f;
            Sound.Play(Sfx.Hit, 0.5f, 0.15f);
            if (Game.I != null) Game.I.DamageNumber(this, damage * RunStats.Current.damageMul, w.color);
            Vector3 push = pos - from; push.y = 0f;
            if (kind != EnemyKind.Tank && kind != EnemyKind.Boss) vel += push.normalized * 4f;
            if (hp <= 0f && Game.I != null) Game.I.Killed(this);
            return true;
        }

        public void CacheRenderers(Material flash)
        {
            renderers = t.GetComponentsInChildren<Renderer>();
            baseMats = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseMats[i] = renderers[i].sharedMaterial;
            flashMat = flash;
        }

        bool flashing;

        public bool Reachable(WeaponDef w) => !Flying || CanBeHitBy(w);

        public void UpdateFlash(float dt)
        {
            hitFlash -= dt;
            bool want = hitFlash > 0f;
            if (want == flashing) return;
            flashing = want;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterial = want ? flashMat : baseMats[i];
        }
    }

    /// <summary>Tumble state for the High Roller boss (a 2-unit die that rolls toward the player).</summary>
    public class BossState
    {
        public DiceModel model;
        public Quaternion orientation = Quaternion.identity, startRot;
        public float rest = 2f, tumbleT = -1f;
        public Vector3 from, to, axis;
        public int tumbles, tier;
        public const float Size = 2f;
        public bool Tumbling => tumbleT >= 0f;
    }

    /// <summary>Builds enemy models from primitives.</summary>
    public static class EnemyModels
    {
        static readonly Color Red = Palette.Hex("#FF2A3D");

        public static Enemy Create(EnemyKind kind, Palette pal, Vector3 pos, int tier = 1)
        {
            var e = new Enemy { kind = kind, pos = pos };
            var root = new GameObject(kind.ToString()).transform;
            root.position = pos;
            e.t = root;
            var hull = pal.Get("EnemyHull", Palette.Hex("#2E323C"), 0.6f, 0.8f);
            var dark = pal.Get("EnemyDark", Palette.Hex("#15171B"), 0.5f, 0.6f);
            var eye = pal.Glow("EnemyEye", Red, 2.6f, Red);
            var accent = pal.Glow("EnemyAccent", Red, 1.6f, Palette.Hex("#5A0D16"));
            var armour = pal.Get("EnemyArmour", Palette.Hex("#C8913E"), 0.75f, 0.95f);

            switch (kind)
            {
                case EnemyKind.Crawler:
                    e.hp = 3f; e.speed = 2.5f; e.radius = 0.4f;
                    Prim.Make(PrimitiveType.Sphere, "Shell", root, new Vector3(0f, 0.38f, 0f), new Vector3(0.8f, 0.5f, 0.8f), hull);
                    Prim.Make(PrimitiveType.Cylinder, "Band", root, new Vector3(0f, 0.36f, 0f), new Vector3(0.82f, 0.03f, 0.82f), accent);
                    Prim.Make(PrimitiveType.Cube, "Visor", root, new Vector3(0f, 0.42f, 0.33f), new Vector3(0.4f, 0.08f, 0.12f), eye);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = 45f + i * 90f;
                        var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                        Prim.Make(PrimitiveType.Cube, "Leg", root, d * 0.42f + Vector3.up * 0.15f, new Vector3(0.1f, 0.3f, 0.1f), dark, Quaternion.Euler(0f, a, 25f));
                    }
                    break;
                case EnemyKind.Drone:
                    e.hp = 3f; e.speed = 3f; e.radius = 0.5f;
                    Prim.Make(PrimitiveType.Cylinder, "Disc", root, new Vector3(0f, 2.1f, 0f), new Vector3(0.9f, 0.08f, 0.9f), hull);
                    Prim.Make(PrimitiveType.Cylinder, "Rim", root, new Vector3(0f, 2.1f, 0f), new Vector3(0.94f, 0.03f, 0.94f), accent);
                    Prim.Make(PrimitiveType.Sphere, "Core", root, new Vector3(0f, 2.05f, 0f), new Vector3(0.45f, 0.35f, 0.45f), dark);
                    Prim.Make(PrimitiveType.Sphere, "Eye", root, new Vector3(0f, 1.95f, 0.12f), new Vector3(0.2f, 0.16f, 0.2f), eye);
                    foreach (var d in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                        Prim.Make(PrimitiveType.Cylinder, "Rotor", root, new Vector3(0f, 2.2f, 0f) + d * 0.55f, new Vector3(0.35f, 0.01f, 0.35f), eye);
                    // Ground marker so players can see where the drone is.
                    Prim.Make(PrimitiveType.Cylinder, "Marker", root, new Vector3(0f, 0.02f, 0f), new Vector3(0.8f, 0.005f, 0.8f), pal.Glow("DroneMarker", Red, 0.8f, Red));
                    break;
                case EnemyKind.Tank:
                    e.hp = 7f; e.speed = 1.3f; e.radius = 0.65f;
                    Prim.Make(PrimitiveType.Cube, "Treads", root, new Vector3(0f, 0.18f, 0f), new Vector3(1.1f, 0.36f, 1.2f), dark);
                    Prim.Make(PrimitiveType.Cube, "Hull", root, new Vector3(0f, 0.5f, 0f), new Vector3(0.95f, 0.36f, 1.0f), armour);
                    Prim.Make(PrimitiveType.Cube, "Plate", root, new Vector3(0f, 0.5f, 0.52f), new Vector3(1.0f, 0.4f, 0.08f), armour, Quaternion.Euler(-20f, 0f, 0f));
                    Prim.Make(PrimitiveType.Cylinder, "Turret", root, new Vector3(0f, 0.78f, 0f), new Vector3(0.5f, 0.1f, 0.5f), hull);
                    Prim.Make(PrimitiveType.Cylinder, "Barrel", root, new Vector3(0f, 0.8f, 0.45f), new Vector3(0.12f, 0.3f, 0.12f), dark, Quaternion.Euler(90f, 0f, 0f));
                    Prim.Make(PrimitiveType.Cube, "Sensor", root, new Vector3(0f, 0.62f, 0.5f), new Vector3(0.5f, 0.06f, 0.05f), eye);
                    break;
                case EnemyKind.Bomber:
                    e.hp = 4f; e.speed = 2f; e.radius = 0.5f; e.abilityTimer = 2.5f;
                    Prim.Make(PrimitiveType.Cube, "Base", root, new Vector3(0f, 0.2f, 0f), new Vector3(0.8f, 0.3f, 0.9f), dark);
                    Prim.Make(PrimitiveType.Cube, "Body", root, new Vector3(0f, 0.45f, 0.1f), new Vector3(0.7f, 0.3f, 0.6f), hull);
                    Prim.Make(PrimitiveType.Cube, "Visor", root, new Vector3(0f, 0.5f, 0.41f), new Vector3(0.5f, 0.08f, 0.04f), eye);
                    // Carries a spare bomb on its back.
                    Prim.Make(PrimitiveType.Cylinder, "Payload", root, new Vector3(0f, 0.72f, -0.22f), new Vector3(0.42f, 0.2f, 0.42f), pal.Get("BombBody", Palette.Hex("#4A505C"), 0.55f, 0.75f));
                    Prim.Make(PrimitiveType.Cylinder, "PayloadRing", root, new Vector3(0f, 0.72f, -0.22f), new Vector3(0.44f, 0.03f, 0.44f), pal.Glow("BombRed", Palette.Hex("#FF2A3D"), 4f));
                    break;
                case EnemyKind.Boss:
                {
                    e.radius = 1.1f; e.speed = 0f;
                    e.hp = 70f + 45f * (tier - 1);
                    e.boss = new BossState { tier = tier };
                    var look = new DiceModel.Look
                    {
                        body = pal.Get("BossBody", Palette.Hex("#1B1D26"), 0.8f, 0.9f),
                        plate = pal.Get("BossPlate", Palette.Hex("#262A36"), 0.6f, 0.8f),
                        pip = pal.Glow("BossPip", Red, 4.5f, Red),
                        seam = pal.Glow("BossSeam", Red, 2f),
                    };
                    var m = DiceModel.Build(pal, root, pos, look, BossState.Size);
                    // Start with a random face up.
                    var rng = new System.Random(tier * 17);
                    e.boss.orientation = Quaternion.Euler(90f * rng.Next(4), 90f * rng.Next(4), 90f * rng.Next(4));
                    m.Body.localRotation = e.boss.orientation;
                    e.boss.model = m;
                    break;
                }
                default: // Mite
                    e.hp = 1f; e.speed = 3.6f; e.radius = 0.24f;
                    Prim.Make(PrimitiveType.Sphere, "Body", root, new Vector3(0f, 0.2f, 0f), new Vector3(0.42f, 0.3f, 0.5f), hull);
                    Prim.Make(PrimitiveType.Sphere, "Eye", root, new Vector3(0f, 0.24f, 0.2f), new Vector3(0.16f, 0.1f, 0.12f), eye);
                    break;
            }
            e.maxHp = e.hp;
            e.CacheRenderers(pal.Glow("HitFlash", Color.white, 2f, Color.white));
            e.spawnT = 0f;
            root.localScale = Vector3.one * 0.01f;
            return e;
        }
    }
}
