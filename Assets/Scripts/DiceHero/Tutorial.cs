using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Stage 1-1: the eight tutorial steps of the campaign plan (section 06). Each step teaches one thing and ends only
    /// when the player has done it; after 20 s stuck, a stronger hint and a line from Vega. The waves are scripted
    /// here instead of by the wave director (Game.ManualSpawning).
    ///   1 move to the beacon · 2 automatic fire · 3 roll to dodge a turret's shots · 4 the roll changed the top face
    ///   (empty socket: roll back) · 5 pick up the missile pod · 6 drones need it · 7 mixed wave, with mites to crush ·
    ///   8 the card (stage clear). The turret is a rusty one with no plating left, so the blasters can hurt it.
    /// </summary>
    public class Tutorial
    {
        public const int Steps = 8;
        const float StuckAfter = 20f;

        readonly GameLoop loop;
        readonly Game game;
        readonly DiceController dice;
        readonly Palette pal;
        Transform beacon, pickup;
        Enemy sentry;
        bool dodged, stuck;
        float t, total, rollNearShot;

        public int Step { get; private set; }            // 0-based; Step 7 = waiting for the stage-clear card
        public string Instruction { get; private set; }
        public string StuckHint { get; private set; }
        public bool IsStuck => stuck;
        /// <summary>A roll to recommend that the normal advice wouldn't give (step 4: back to the only working gun).</summary>
        public RollPlan PlanOverride { get; private set; }
        /// <summary>Where this step wants Pip to go (beacon, pickup), for the autopilot.</summary>
        public Vector3? Goal => Step == 0 ? BeaconPos : Step == 4 ? PickupPos : (Vector3?)null;

        static readonly Vector3 BeaconPos = new Vector3(0f, 0f, 0f), PickupPos = new Vector3(-3.5f, 0f, 2.5f);

        public Tutorial(GameLoop loop, Game game, DiceController dice, Palette pal)
        {
            this.loop = loop; this.game = game; this.dice = dice; this.pal = pal;
            game.ManualSpawning = true;
            game.Dodged += () => dodged = true;
            // A roll with a shot close by that doesn't end in a hit also counts as a dodge.
            dice.Tripped += _ => { if (Step == 2 && game.NearestBullet(dice.transform.position) < 2.6f) rollNearShot = 0.9f; };
            game.Hurt += _ => rollNearShot = 0f;
            Begin(0);
        }

        void Begin(int s)
        {
            if (s > 0) Telemetry.Log("tutorial_step", "step", Step + 1, "time", t, "stuck", stuck);
            Step = s; t = 0f; stuck = false; PlanOverride = null;
            if (s > 2 && sentry != null) { sentry.hp = 0f; sentry = null; } // the turret leaves once the dodge is learnt (step 3 can be skipped)
            switch (s)
            {
                case 0:
                    Instruction = "MOVE TO THE BEACON";
                    StuckHint = "WASD, the arrow keys or the left stick move Pip.";
                    beacon = Marker("Beacon", BeaconPos, Palette.Hex("#29B6F6"));
                    break;
                case 1:
                    Object.Destroy(beacon.gameObject);
                    Instruction = "PIP FIRES THE GUN ON ITS TOP FACE BY ITSELF. GET CLOSE";
                    StuckHint = "Pip aims and fires on its own. Just get within range.";
                    foreach (var x in new[] { -4f, 0f, 4f }) Slow(game.Spawn(EnemyKind.Crawler, new Vector3(x, 0f, 6f)), 0.45f);
                    loop.RadioNow("Crawlers. You don't aim: Pip fires whatever module is on its top face. Just get close.");
                    break;
                case 2:
                    Instruction = "PRESS SPACE TO ROLL THROUGH THE TURRET'S SHOTS";
                    StuckHint = "SPACE (A on a gamepad) rolls Pip one face the way you're moving. Press it just before a red shot arrives: Pip can't be hurt at the start of a roll.";
                    sentry = game.Spawn(EnemyKind.Tank, new Vector3(6.5f, 0f, 3f));
                    sentry.speed = 0f;
                    sentry.defenceOverride = Defence.Bare;
                    sentry.hp = sentry.maxHp = 30f;
                    dodged = false;
                    loop.RadioNow("A rusty old turret. Press SPACE as a shot arrives and roll right through it.");
                    break;
                case 3:
                    Instruction = "THE FACE ON TOP IS YOUR GUN. THIS ONE IS EMPTY: ROLL TO THE GREEN MARKER";
                    StuckHint = "Each floor marker shows the gun that roll puts on top. Move toward the green one and press SPACE.";
                    loop.RadioNow("See? The roll turned an empty face up, so Pip can't shoot. Roll to the green marker to get the blasters back on top.");
                    break;
                case 4:
                    Instruction = "A WORKING MODULE. PICK IT UP";
                    StuckHint = "Move onto the yellow module on the floor.";
                    pickup = Pickup(PickupPos);
                    break;
                case 5:
                    Object.Destroy(pickup.gameObject);
                    loop.MountModule(2, "missile");
                    Instruction = "DRONES. ROLL TO THE RED MARKER: MISSILES ON TOP";
                    StuckHint = "Drones fly over flat shots. Move toward the red marker and press SPACE.";
                    loop.RadioNow("A missile pod, on face 2. Missiles climb to fliers. Here they come.");
                    foreach (var x in new[] { -5f, 0f, 5f }) game.Spawn(EnemyKind.Drone, new Vector3(x, 0f, 7.5f));
                    break;
                case 6:
                    Instruction = "CLEAR THEM ALL";
                    StuckHint = null;
                    loop.RadioNow("Ground and air together. And mites: too low to shoot, so roll onto them.");
                    for (int i = 0; i < 3; i++) game.Spawn(EnemyKind.Mite, new Vector3(-3f + i * 3f, 0f, -6.5f));
                    for (int i = 0; i < 6; i++) game.Spawn(EnemyKind.Crawler, new Vector3(-7f + i * 2.8f, 0f, 7.5f));
                    foreach (var x in new[] { -6f, 6f, 0f }) game.Spawn(EnemyKind.Drone, new Vector3(x, 0f, 8.5f));
                    break;
                case 7:
                    Instruction = null; StuckHint = null;
                    game.CompleteStage(); // the stage-clear screen deals the first card (step 8)
                    break;
            }
        }

        public void Update(float dt)
        {
            t += dt; total += dt;
            if (game.Lost || game.Won) return;
            if (!stuck && StuckHint != null && t > StuckAfter)
            {
                stuck = true;
                loop.RadioNow(Step == 0 ? "Pip? Move. Anywhere. The beacon's the blue ring." : StuckHint);
            }
            Vector3 p = dice.transform.position;
            bool clear = game.EnemiesLeft == 0;
            switch (Step)
            {
                case 0: if (Flat(p - BeaconPos) < 1.3f) Begin(1); break;
                case 1: if (clear) Begin(2); break;
                case 2:
                    if (rollNearShot > 0f) { rollNearShot -= dt; if (rollNearShot <= 0f) dodged = true; }
                    // Destroying the turret first also ends the step (it is bare: the blasters hurt it).
                    if (dodged || sentry == null || !sentry.Alive) { sentry = null; Begin(WeaponDef.All[dice.TopNumber].IsEmpty ? 3 : 4); } break;
                case 3:
                    if (!WeaponDef.All[dice.TopNumber].IsEmpty) { Begin(4); break; }
                    PlanOverride = RollAdvisor.ToFace(dice, System.Array.FindIndex(WeaponDef.All, w => w != null && w.id == "twin"));
                    break;
                case 4: if (Flat(p - PickupPos) < 1.1f) Begin(5); break;
                case 5: if (clear) Begin(6); break;
                case 6: if (clear) Begin(7); break;
            }
            if (beacon != null) beacon.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(total * 5f));
            if (pickup != null) { pickup.Rotate(0f, 90f * dt, 0f); pickup.localPosition = PickupPos + Vector3.up * (0.35f + 0.08f * Mathf.Sin(total * 3f)); }
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
        static void Slow(Enemy e, float k) => e.speed *= k;

        Transform Marker(string name, Vector3 at, Color c) => MarkerRing(pal, name, at, c);

        public static Transform MarkerRing(Palette pal, string name, Vector3 at, Color c)
        {
            var root = new GameObject(name).transform;
            root.position = at;
            var m = pal.Glow("Tut" + name, c, 2.2f);
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f;
                Prim.Make(PrimitiveType.Cube, "Ring", root, new Vector3(Mathf.Cos(a), 0.02f, Mathf.Sin(a)) * 0.9f, new Vector3(0.08f, 0.02f, 0.22f), m, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
            }
            Prim.Make(PrimitiveType.Cylinder, "Column", root, new Vector3(0f, 1.2f, 0f), new Vector3(0.08f, 1.2f, 0.08f), m);
            return root;
        }

        Transform Pickup(Vector3 at) => ModulePickup(pal, at, "missile");

        /// <summary>A module lying on the floor: its socket plate, its outline in light and a ring (tutorial pickup, caches).</summary>
        public static Transform ModulePickup(Palette pal, Vector3 at, string gunId)
        {
            var root = new GameObject("ModulePickup").transform;
            root.position = at;
            var tri = WeaponDef.Find(gunId);
            var glow = pal.Glow("TutPickup" + gunId, tri.color, 2.6f);
            Prim.Make(PrimitiveType.Cube, "Plate", root, Vector3.zero, new Vector3(0.6f, 0.06f, 0.6f), pal.Get("TutPickupPlate" + gunId, tri.color * 0.2f + Color.black * 0.8f, 0.6f, 0.3f));
            var g = new GameObject("Glyph").transform;
            g.SetParent(root, false);
            g.localPosition = new Vector3(0f, 0.05f, 0f);
            g.localScale = Vector3.one * 0.8f;
            Glyphs.Build(tri.model, g, glow);
            MarkerRing(pal, "PickupRing" + gunId, at, tri.color).SetParent(root, true);
            return root;
        }
    }
}
