using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    public enum EnemyKind { Crawler, Drone, Tank, Mite, Bomber, Boss, Compactor, Gardener, Driller, Smelter }

    /// <summary>
    /// What protects an enemy, shown on its model. The rule: you can tell what hurts an enemy by looking at it.
    ///   Bare     nothing: any gun, and a landing roll crushes it
    ///   Low      floor-hugger under the barrels: only a landing roll (Pip weighs a tonne) or a bomb
    ///   Flying   flat shots pass under it: seekers climb to it, shock arcs jump to it
    ///   Steel    plate: a piercing slug or an explosion cracks it; bolts, fire and seekers bounce off
    ///   Vines    shots pass through the leaves: fire burns them
    ///   Ice      shots skid off: fire melts it
    ///   Shield   an energy field soaks every shot: a shock overloads it
    ///   Burrowed underground: only an explosion's shockwave reaches it
    /// Vines, ice and shields are coats over a bare robot: break the coat and it's bare.
    /// </summary>
    public enum Defence { Bare, Low, Flying, Steel, Vines, Ice, Shield, Burrowed }

    /// <summary>
    /// A hostile bot. Crawler and bomber are bare (stages can coat them in vines, ice or a shield), drones fly, tanks
    /// are steel, mites are low. Bombs hurt everything; a landing roll crushes bare and low ones.
    /// </summary>
    public class Enemy : ITarget
    {
        public EnemyKind kind;
        public Transform t;
        public float hp, maxHp, speed, radius, fireTimer, hitFlash, spawnT, abilityTimer;
        public Vector3 pos, vel;
        public BossState boss;
        // Compactor (deck 1 foreman): 0 approach, 1 wind-up, 2 charge, 3 stunned (armour open).
        public int mode;
        public float modeT;
        public Vector3 chargeDir;
        public Transform[] plates;
        public Transform lane;
        public bool Stunned => kind == EnemyKind.Compactor && mode == 3;
        // A coat (vines, ice or an energy shield) over the robot: only its counter breaks it, then the robot is bare.
        public Defence coat;
        public float coatHp, coatMax;
        public Transform coatFx;
        public bool Coated => coatHp > 0f;
        /// <summary>Overrides the kind's defence (the tutorial's rusty turret has lost its plating).</summary>
        public Defence? defenceOverride;
        /// <summary>Bosses don't get shoved by bullets, minions or slams.</summary>
        public bool IsBig => kind >= EnemyKind.Boss;
        /// <summary>A deck foreman (every boss except the High Roller).</summary>
        public bool IsForeman => kind >= EnemyKind.Compactor;
        // Smelter: what's left of the current steel plate; after the three plates its core is shielded.
        public float plateHp;
        public const float SmelterPlateHp = 70f, SmelterShieldHp = 45f;
        /// <summary>Health left as a fraction, counting the Smelter's plates and core shield.</summary>
        public float BossFraction => kind == EnemyKind.Smelter
            ? (Mathf.Max(0, 2 - mode) * SmelterPlateHp + (mode < 3 ? plateHp + SmelterShieldHp : coatHp) + hp) / (3 * SmelterPlateHp + SmelterShieldHp + maxHp)
            : hp / Mathf.Max(1f, maxHp);

        /// <summary>What protects it right now.</summary>
        public Defence CurrentDefence
        {
            get
            {
                if (Coated) return coat;
                if (defenceOverride.HasValue) return defenceOverride.Value;
                switch (kind)
                {
                    case EnemyKind.Drone: return Defence.Flying;
                    case EnemyKind.Tank: return Defence.Steel;
                    case EnemyKind.Mite: return Defence.Low;
                    case EnemyKind.Compactor: return Stunned ? Defence.Bare : Defence.Steel;
                    case EnemyKind.Gardener: return Defence.Vines;
                    case EnemyKind.Driller: return mode == 2 ? Defence.Ice : Defence.Burrowed;
                    case EnemyKind.Smelter: return mode < 3 ? Defence.Steel : Defence.Bare;
                    case EnemyKind.Boss: return boss.defence;
                    default: return Defence.Bare;
                }
            }
        }

        /// <summary>Nothing hurts it right now (the Gardener moving underground, the High Roller rerolling).</summary>
        public bool Untouchable => (kind == EnemyKind.Gardener && mode != 0) || (kind == EnemyKind.Boss && boss.Rerolling);

        /// <summary>The rule table: which damage type gets through which defence.</summary>
        public static bool Counters(DamageType t, Defence d)
        {
            switch (d)
            {
                case Defence.Bare: return true;
                case Defence.Flying: return t == DamageType.Seeker || t == DamageType.Shock;
                case Defence.Steel: return t == DamageType.Pierce || t == DamageType.Blast;
                case Defence.Vines: case Defence.Ice: return t == DamageType.Fire;
                case Defence.Shield: return t == DamageType.Shock;
                case Defence.Burrowed: return t == DamageType.Blast;
                default: return false; // Low: only a landing roll
            }
        }

        public static string DefenceName(Defence d) => d == Defence.Low ? "TOO LOW" : d == Defence.Flying ? "FLYING" : d == Defence.Steel ? "STEEL PLATE"
            : d == Defence.Vines ? "VINES" : d == Defence.Ice ? "ICE SHELL" : d == Defence.Shield ? "ENERGY SHIELD" : d == Defence.Burrowed ? "UNDERGROUND" : "BARE";

        /// <summary>What beats a defence, naming the guns on Pip's faces ("RAILGUN OR PLASMA CANNON"), or the types when none is mounted.</summary>
        public static string Beaters(Defence d)
        {
            if (d == Defence.Low) return "ROLL ONTO IT";
            var list = new List<string>();
            for (int n = 1; n <= 6; n++) { var w = WeaponDef.All[n]; if (!w.IsEmpty && Counters(w.type, d) && !list.Contains(w.name)) list.Add(w.name); }
            if (list.Count > 0) return string.Join(" OR ", list);
            var types = new List<string>();
            foreach (DamageType t in System.Enum.GetValues(typeof(DamageType))) if (Counters(t, d)) types.Add(WeaponDef.TypeName(t));
            return "NEEDS " + string.Join(" OR ", types);
        }

        /// <summary>The first face whose gun gets through this defence (0: none mounted).</summary>
        public static int CounterFace(Defence d)
        {
            for (int n = 1; n <= 6; n++) if (!WeaponDef.All[n].IsEmpty && Counters(WeaponDef.All[n].type, d)) return n;
            return 0;
        }

        /// <summary>What to do about this foreman right now (HUD line and deflect text).</summary>
        public string ForemanState()
        {
            switch (kind)
            {
                case EnemyKind.Compactor: return Stunned ? "STUNNED: PLATING BUCKLED, FIRE!" : mode == 1 || mode == 2 ? "CHARGING: GET OUT OF THE LANE" : "STEEL: MAKE IT RAM A WALL OR A VENT BOX";
                case EnemyKind.Gardener: return mode == 0 ? "VINES: " + Beaters(Defence.Vines) : "MOVING: WATCH WHERE IT COMES UP";
                case EnemyKind.Driller: return mode == 2 ? "SURFACED, ICED OVER: " + Beaters(Defence.Ice) : mode == 1 ? "SURFACING: GET CLEAR!" : "UNDERGROUND: " + Beaters(Defence.Burrowed);
                case EnemyKind.Smelter: return mode < 3 ? $"STEEL PLATE {mode + 1}/3: " + Beaters(Defence.Steel) : Coated ? "CORE SHIELD: " + Beaters(Defence.Shield) : "CORE EXPOSED: ANY GUN";
                default: return "";
            }
        }
        Renderer[] renderers;
        Material[] baseMats;
        Material flashMat;

        public Vector3 Position => pos + Vector3.up * (Flying ? 2.1f : kind == EnemyKind.Boss ? 1f : radius);
        public float Radius => radius;
        public bool Alive => hp > 0f && t != null;
        public bool Flying => kind == EnemyKind.Drone;
        public bool Low => kind == EnemyKind.Mite;
        /// <summary>The defence to line up a gun for (the High Roller: including the one it is rerolling to).</summary>
        public Defence PlannedDefence => boss == null ? CurrentDefence : boss.Rerolling ? boss.nextDefence : boss.defence;

        public static string Hint(EnemyKind k)
        {
            switch (k)
            {
                case EnemyKind.Drone: return "DRONES FLY OVER FLAT SHOTS: " + Beaters(Defence.Flying);
                case EnemyKind.Tank: return "TANKS ARE STEEL: " + Beaters(Defence.Steel);
                case EnemyKind.Mite: return "MITES ARE TOO LOW TO SHOOT: ROLL ONTO THEM";
                case EnemyKind.Bomber: return "BOMBERS PLANT BOMBS: SHOVE THEM OFF THE EDGE";
                case EnemyKind.Boss: return "EACH PHASE IT WEARS A DIFFERENT DEFENCE · 3 PHASES";
                case EnemyKind.Compactor: return "STEEL: MAKE IT RAM A WALL OR A VENT BOX, THEN FIRE";
                case EnemyKind.Gardener: return "A PLANT: SHOTS PASS THROUGH ITS LEAVES. " + Beaters(Defence.Vines);
                case EnemyKind.Driller: return "UNDERGROUND, ONLY EXPLOSIVES REACH IT. WHEN IT'S UP, ITS DRILL IS ICED: USE FIRE";
                case EnemyKind.Smelter: return "THREE STEEL PLATES, THEN A SHIELDED CORE";
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
                case EnemyKind.Compactor: return "COMPACTOR";
                case EnemyKind.Gardener: return "GARDENER";
                case EnemyKind.Driller: return "DRILLER";
                case EnemyKind.Smelter: return "SMELTER";
                default: return "CRAWLERS";
            }
        }

        public bool CanBeHitBy(WeaponDef w) => !w.IsEmpty && !Untouchable && Counters(w.type, CurrentDefence);

        /// <summary>A landing roll crushes bare and low robots; steel, coats and bosses just get shoved.</summary>
        public bool Crushable => !IsBig && !Untouchable && (CurrentDefence == Defence.Bare || CurrentDefence == Defence.Low);

        /// <summary>Deflect text: what protects it and what gets through ("STEEL PLATE · RAILGUN").</summary>
        public string DeflectText()
        {
            if (Untouchable) return kind == EnemyKind.Boss ? "REROLLING" : "OUT OF REACH";
            var d = CurrentDefence;
            return DefenceName(d) + " · " + Beaters(d);
        }

        public bool Hit(WeaponDef w, float damage, Vector3 from)
        {
            if (!Alive) return false;
            if (!CanBeHitBy(w))
            {
                if (Game.I != null) Game.I.Deflect(this, DeflectText());
                return false;
            }
            if (kind == EnemyKind.Smelter && mode < 3)
            {
                float pd = damage * RunStats.Current.damageMul * RunStats.Current.FaceDamage(w.number);
                plateHp -= pd;
                hitFlash = 0.1f;
                Sound.Play(Sfx.Hit, 0.5f, 0.15f);
                if (Game.I != null) { Game.I.DamageNumber(this, pd, w.color); if (plateHp <= 0f) Game.I.SmelterPlateBroken(this); }
                return true;
            }
            if (Coated)
            {
                coatHp -= damage * RunStats.Current.damageMul * RunStats.Current.FaceDamage(w.number);
                hitFlash = 0.1f;
                Sound.Play(Sfx.Hit, 0.5f, 0.15f);
                if (coatHp <= 0f && Game.I != null) Game.I.CoatBroken(this);
                return true;
            }
            hp -= damage * RunStats.Current.damageMul * RunStats.Current.FaceDamage(w.number);
            hitFlash = 0.1f;
            Sound.Play(Sfx.Hit, 0.5f, 0.15f);
            if (Game.I != null) Game.I.DamageNumber(this, damage * RunStats.Current.damageMul * RunStats.Current.FaceDamage(w.number), w.color);
            Vector3 push = pos - from; push.y = 0f;
            if (kind != EnemyKind.Tank && !IsBig) vel += push.normalized * 4f;
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

        /// <summary>Shots pass under fliers and over low robots they can't hurt.</summary>
        public bool Reachable(WeaponDef w) => !(Flying || Low) || CanBeHitBy(w);

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

    /// <summary>High Roller state: three phases, each locked on one weak number; hops between rests, rerolls between phases.</summary>
    public class BossState
    {
        public DiceModel model;
        public Quaternion orientation = Quaternion.identity, startRot, targetRot;
        public float rest = 2f, hopT = -1f, reroll, yaw;
        public Vector3 from, to, axis;
        public int hops, tier, phase, weak, next;    // weak/next: the number on top (looks only)
        public Defence defence, nextDefence;           // what protects it this phase, and the next phase
        public const float Size = 2f;
        public bool Hopping => hopT >= 0f;
        public bool Rerolling => reroll > 0f;

        /// <summary>Orientation with face number on top, turned by quarter turns around the vertical.</summary>
        public static Quaternion RotationFor(int number, int quarterTurns)
        {
            int i = System.Array.IndexOf(DiceModel.FaceNumbers, number);
            return Quaternion.AngleAxis(90f * quarterTurns, Vector3.up) * Quaternion.FromToRotation(DiceModel.FaceNormals[i], Vector3.up);
        }
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

            // Navy theme: same stats, model rebuilt from references (NavyEnemies); the 2.0 parts go to a discarded parent.
            bool navy = Art.Theme >= 3 && kind < EnemyKind.Boss;
            Transform mroot = navy ? new GameObject("Discard").transform : root;
            switch (kind)
            {
                case EnemyKind.Crawler:
                    e.hp = 3f; e.speed = 2.5f; e.radius = 0.4f;
                    Prim.Make(PrimitiveType.Sphere, "Shell", mroot, new Vector3(0f, 0.38f, 0f), new Vector3(0.8f, 0.5f, 0.8f), hull);
                    Prim.Make(PrimitiveType.Cylinder, "Band", mroot, new Vector3(0f, 0.36f, 0f), new Vector3(0.82f, 0.03f, 0.82f), accent);
                    Prim.Make(PrimitiveType.Cube, "Visor", mroot, new Vector3(0f, 0.42f, 0.33f), new Vector3(0.4f, 0.08f, 0.12f), eye);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = 45f + i * 90f;
                        var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                        Prim.Make(PrimitiveType.Cube, "Leg", mroot, d * 0.42f + Vector3.up * 0.15f, new Vector3(0.1f, 0.3f, 0.1f), dark, Quaternion.Euler(0f, a, 25f));
                    }
                    break;
                case EnemyKind.Drone:
                    e.hp = 3f; e.speed = 3f; e.radius = 0.5f;
                    Prim.Make(PrimitiveType.Cylinder, "Disc", mroot, new Vector3(0f, 2.1f, 0f), new Vector3(0.9f, 0.08f, 0.9f), hull);
                    Prim.Make(PrimitiveType.Cylinder, "Rim", mroot, new Vector3(0f, 2.1f, 0f), new Vector3(0.94f, 0.03f, 0.94f), accent);
                    Prim.Make(PrimitiveType.Sphere, "Core", mroot, new Vector3(0f, 2.05f, 0f), new Vector3(0.45f, 0.35f, 0.45f), dark);
                    Prim.Make(PrimitiveType.Sphere, "Eye", mroot, new Vector3(0f, 1.95f, 0.12f), new Vector3(0.2f, 0.16f, 0.2f), eye);
                    foreach (var d in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                        Prim.Make(PrimitiveType.Cylinder, "Rotor", mroot, new Vector3(0f, 2.2f, 0f) + d * 0.55f, new Vector3(0.35f, 0.01f, 0.35f), eye);
                    // Ground marker so players can see where the drone is.
                    Prim.Make(PrimitiveType.Cylinder, "Marker", mroot, new Vector3(0f, 0.02f, 0f), new Vector3(0.8f, 0.005f, 0.8f), pal.Glow("DroneMarker", Red, 0.8f, Red));
                    break;
                case EnemyKind.Tank:
                    e.hp = 7f; e.speed = 1.3f; e.radius = 0.65f;
                    Prim.Make(PrimitiveType.Cube, "Treads", mroot, new Vector3(0f, 0.18f, 0f), new Vector3(1.1f, 0.36f, 1.2f), dark);
                    Prim.Make(PrimitiveType.Cube, "Hull", mroot, new Vector3(0f, 0.5f, 0f), new Vector3(0.95f, 0.36f, 1.0f), armour);
                    Prim.Make(PrimitiveType.Cube, "Plate", mroot, new Vector3(0f, 0.5f, 0.52f), new Vector3(1.0f, 0.4f, 0.08f), armour, Quaternion.Euler(-20f, 0f, 0f));
                    Prim.Make(PrimitiveType.Cylinder, "Turret", mroot, new Vector3(0f, 0.78f, 0f), new Vector3(0.5f, 0.1f, 0.5f), hull);
                    Prim.Make(PrimitiveType.Cylinder, "Barrel", mroot, new Vector3(0f, 0.8f, 0.45f), new Vector3(0.12f, 0.3f, 0.12f), dark, Quaternion.Euler(90f, 0f, 0f));
                    Prim.Make(PrimitiveType.Cube, "Sensor", mroot, new Vector3(0f, 0.62f, 0.5f), new Vector3(0.5f, 0.06f, 0.05f), eye);
                    break;
                case EnemyKind.Bomber:
                    e.hp = 4f; e.speed = 2f; e.radius = 0.5f; e.abilityTimer = 2.5f;
                    Prim.Make(PrimitiveType.Cube, "Base", mroot, new Vector3(0f, 0.2f, 0f), new Vector3(0.8f, 0.3f, 0.9f), dark);
                    Prim.Make(PrimitiveType.Cube, "Body", mroot, new Vector3(0f, 0.45f, 0.1f), new Vector3(0.7f, 0.3f, 0.6f), hull);
                    Prim.Make(PrimitiveType.Cube, "Visor", mroot, new Vector3(0f, 0.5f, 0.41f), new Vector3(0.5f, 0.08f, 0.04f), eye);
                    // Carries a spare bomb on its back.
                    Prim.Make(PrimitiveType.Cylinder, "Payload", mroot, new Vector3(0f, 0.72f, -0.22f), new Vector3(0.42f, 0.2f, 0.42f), pal.Get("BombBody", Palette.Hex("#4A505C"), 0.55f, 0.75f));
                    Prim.Make(PrimitiveType.Cylinder, "PayloadRing", mroot, new Vector3(0f, 0.72f, -0.22f), new Vector3(0.44f, 0.03f, 0.44f), pal.Glow("BombRed", Palette.Hex("#FF2A3D"), 4f));
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
                    e.boss.weak = 1 + rng.Next(6);
                    e.boss.orientation = BossState.RotationFor(e.boss.weak, rng.Next(4));
                    m.Body.localRotation = e.boss.orientation;
                    e.boss.model = m;
                    break;
                }
                case EnemyKind.Compactor:
                {
                    // Deck 1 foreman: a scrap compactor on treads. Armoured plates on top open when it's stunned.
                    e.hp = 140f; e.speed = 1.4f; e.radius = 1.15f; e.modeT = 2.5f;
                    var cbm = BlenderModels.Spawn("Enemies/Compactor", root, pal);
                    if (cbm != null) e.plates = new[] { BlenderModels.Pivot(cbm, "Pivot_PlateL", root), BlenderModels.Pivot(cbm, "Pivot_PlateR", root) };
                    else
                    {
                    var steel = pal.Get("CompSteel", Palette.Hex("#4A505C"), 0.55f, 0.75f);
                    var yellow = pal.Get("CompYellow", Palette.Hex("#F2B21E"), 0.5f, 0.2f);
                    var core = pal.Glow("CompCore", Palette.Hex("#FF7A1A"), 1.1f);
                    foreach (float x in new[] { -0.85f, 0.85f })
                    {
                        Prim.Make(PrimitiveType.Cube, "Tread", root, new Vector3(x, 0.26f, 0f), new Vector3(0.42f, 0.52f, 2.0f), dark);
                        for (int i = 0; i < 6; i++) Prim.Make(PrimitiveType.Cube, "Lug", root, new Vector3(x, 0.53f, -0.85f + i * 0.34f), new Vector3(0.44f, 0.04f, 0.12f), steel);
                    }
                    Prim.Make(PrimitiveType.Cube, "Hull", root, new Vector3(0f, 0.72f, -0.1f), new Vector3(1.32f, 0.78f, 1.6f), steel);
                    Prim.Make(PrimitiveType.Sphere, "Core", root, new Vector3(0f, 1.0f, -0.1f), new Vector3(0.62f, 0.26f, 0.85f), core);
                    Prim.Make(PrimitiveType.Cube, "Crusher", root, new Vector3(0f, 0.62f, 0.95f), new Vector3(1.9f, 1.0f, 0.26f), dark);
                    for (int i = 0; i < 5; i++)
                        Prim.Make(PrimitiveType.Cube, "Stripe", root, new Vector3(-0.72f + i * 0.36f, 0.62f, 1.09f), new Vector3(0.14f, 0.95f, 0.02f), yellow, Quaternion.Euler(0f, 0f, 30f));
                    Prim.Make(PrimitiveType.Cube, "Eye", root, new Vector3(0f, 1.2f, 0.72f), new Vector3(0.9f, 0.08f, 0.06f), eye);
                    foreach (float x in new[] { -0.35f, 0.35f })
                        Prim.Make(PrimitiveType.Cylinder, "Stack", root, new Vector3(x, 1.25f, -0.75f), new Vector3(0.18f, 0.35f, 0.18f), dark);
                    e.plates = new Transform[2];
                    for (int s = 0; s < 2; s++)
                    {
                        float side = s == 0 ? -1f : 1f;
                        var hinge = new GameObject("PlateHinge").transform;
                        hinge.SetParent(root, false);
                        hinge.localPosition = new Vector3(side * 0.68f, 1.12f, -0.1f);
                        Prim.Make(PrimitiveType.Cube, "Plate", hinge, new Vector3(-side * 0.34f, 0.04f, 0f), new Vector3(0.7f, 0.1f, 1.55f), yellow);
                        e.plates[s] = hinge;
                    }
                    }
                    // Charge lane: shown on the floor during the wind-up so the charge is never a surprise.
                    e.lane = Prim.Make(PrimitiveType.Cube, "ChargeLane", null, pos, new Vector3(1.9f, 0.01f, 1f), pal.Glow("CompLane", Red, 1.2f, Red * 0.4f)).transform;
                    e.lane.gameObject.SetActive(false);
                    break;
                }
                case EnemyKind.Gardener:
                case EnemyKind.Driller:
                case EnemyKind.Smelter:
                    BossModels.Build(e, pal, root);
                    break;
                default: // Mite
                    e.hp = 1f; e.speed = 3.0f; e.radius = 0.24f;
                    Prim.Make(PrimitiveType.Sphere, "Body", mroot, new Vector3(0f, 0.2f, 0f), new Vector3(0.42f, 0.3f, 0.5f), hull);
                    Prim.Make(PrimitiveType.Sphere, "Eye", mroot, new Vector3(0f, 0.24f, 0.2f), new Vector3(0.16f, 0.1f, 0.12f), eye);
                    break;
            }
            if (navy)
            {
                Object.DestroyImmediate(mroot.gameObject);
                NavyEnemies.Build(kind, pal, root);
            }
            e.maxHp = e.hp;
            e.CacheRenderers(pal.Glow("HitFlash", Color.white, 2f, Color.white));
            e.spawnT = 0f;
            root.localScale = Vector3.one * 0.01f;
            return e;
        }
    }
}
