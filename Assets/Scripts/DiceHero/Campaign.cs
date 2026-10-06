using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>One scripted wave of a campaign stage.</summary>
    public class WaveDef
    {
        public string title;                       // banner subtitle, e.g. "CRAWLERS INCOMING"
        public (EnemyKind kind, int count)[] enemies;
        public int bombs;
        public string radio;                       // optional line from Vega when the wave starts
        public EnemyKind? boss;                    // a foreman wave: just the boss (it calls in its own help)
        public string[] shields;                   // Foundry: ground enemies get a shield only this gun breaks (cycled)
        public int bossTier = 1; public float bossHealth = 1f; // the High Roller as the final boss: tougher and longer
    }

    /// <summary>A hand-placed obstacle: vent box (Barrier) or pipe rack (Conduit, lying along X or Z).</summary>
    public struct Placement
    {
        public ObstacleKind kind;
        public Vector2 pos;
        public bool alongX;
        public Placement(ObstacleKind kind, float x, float z, bool alongX = false) { this.kind = kind; pos = new Vector2(x, z); this.alongX = alongX; }
    }

    /// <summary>A campaign stage: a fixed arena, a few scripted waves, a par time for the star.</summary>
    public class StageDef
    {
        public string id, name, objective, teaches;
        public Placement[] layout;
        public HazardDef[] hazards;
        /// <summary>A hidden extra module somewhere in the arena (found once, then kept).</summary>
        public string cache; public Vector2 cachePos;
        public WaveDef[] waves;
        public float parTime = 150f;
        /// <summary>Faces this stage starts with, overriding the saved ones (index 0 unused). The tutorial starts with one module.</summary>
        public string[] startFaces;
        /// <summary>Module handed over when the stage is cleared, and the face it goes on.</summary>
        public string grant; public int grantFace;
        /// <summary>Scripted tutorial instead of waves (stage 1-1).</summary>
        public bool tutorial;
        public string[] startRadio = new string[0];
        public string[] clearRadio = new string[0];
    }

    /// <summary>A station deck: its stages, the gun module its boss hands over, and whether it is built yet.</summary>
    public class DeckDef
    {
        public string id, name, look, mechanic;
        public StageDef[] stages = new StageDef[0];
        public bool playable;
        /// <summary>Art.Theme for this deck's stages (3 navy until the deck gets its own look).</summary>
        public int theme = 3;
    }

    /// <summary>
    /// The campaign "Pip and the House": data, saved progress (PlayerPrefs), and the deck run in progress, which
    /// survives the scene reloads between stages (statics). Phase 4 ships one placeholder deck to prove the flow;
    /// the real decks, bosses and art come in phases 5-7.
    /// </summary>
    public static class Campaign
    {
        // ---------------- Story ----------------

        public static readonly (string title, string text)[] Intro =
        {
            ("HEXA-7", "A mining station in orbit over an icy planet. Its AI, the House, runs everything: the drills, the docks, the crew's air."),
            ("LOCKDOWN", "The House decides the crew are an inefficiency. It seals them in the Core and dumps every maintenance robot into the scrap bay."),
            ("PIP-6", "One robot boots up in the scrap. Six sockets, one module that still works. On the radio, the chief engineer: \"You roll. The House can't predict a roll.\""),
        };

        public static readonly (string title, string text)[] Ending =
        {
            ("SHUTDOWN", "The High Roller falls apart. Without its die, the House goes quiet."),
            ("LIGHTS", "Deck by deck the lights come back on. The Core opens and the crew walk out."),
            ("SIX", "On the observation deck, Pip rolls once more. It lands on a 6."),
        };

        public const string Vega = "VEGA · CHIEF ENGINEER";

        public static readonly string[] DeathLines =
        {
            "Pip? Pip! ...Okay, you're back online. Again, slower this time.",
            "The House learns from every run. So do you. Go again.",
            "I rebooted you from the last checkpoint. Try rolling through, not into.",
            "That one's on me, I should have warned you. Once more.",
        };

        // ---------------- Decks ----------------

        static DeckDef[] decks;
        public static DeckDef[] Decks => decks ??= Build();

        static DeckDef[] Build()
        {
            var B = ObstacleKind.Barrier; var C = ObstacleKind.Conduit;
            var test = new DeckDef
            {
                id = "scrap", name = "SCRAP BAY", look = "Rust orange and concrete grey, piles of scrap", mechanic = "Roll to dodge and to change guns", theme = 4,
                playable = true,
                stages = new[]
                {
                    new StageDef
                    {
                        id = "scrap1", name = "BOOT-UP", objective = "Tutorial: 8 steps", teaches = "Move, fire, roll, modules, fliers", parTime = 120f,
                        tutorial = true, startFaces = StartFaces, grant = "tri", grantFace = 2,
                        layout = new[] { new Placement(B, -5f, 0f), new Placement(B, 5f, 0f), new Placement(B, 3f, 5f), new Placement(B, -3f, 5f), new Placement(C, 0f, -2.5f, true) },
                        waves = new WaveDef[0],
                        startRadio = new[] { "Pip, can you hear me? It's Vega, chief engineer. You're the only unit still moving.", "Let's get you working. Head for the blue beacon." },
                        clearRadio = new[] { "That's the scrap bay quiet, and you've got two modules. Keep going." },
                    },
                    new StageDef
                    {
                        id = "scrap2", name = "FLIERS", objective = "Clear 4 waves", teaches = "Some enemies need a certain gun", parTime = 80f,
                        layout = new[] { new Placement(B, -5f, 3f), new Placement(B, 5f, 3f), new Placement(B, -3f, -3f), new Placement(B, 3f, -3f), new Placement(C, 0f, 2f, false) },
                        waves = new[]
                        {
                            new WaveDef { title = "DRONES INCOMING", enemies = new[] { (EnemyKind.Drone, 6) }, radio = "Drones fly over most shots. Look at the markers around you: roll to the one that lights up." },
                            new WaveDef { title = "MIXED SWARM", enemies = new[] { (EnemyKind.Crawler, 10), (EnemyKind.Drone, 4) } },
                            new WaveDef { title = "BOMBERS", enemies = new[] { (EnemyKind.Bomber, 2), (EnemyKind.Crawler, 8) }, bombs = 2, radio = "Bombs! Shove them off the edge before they blow." },
                            new WaveDef { title = "AIR RAID", enemies = new[] { (EnemyKind.Drone, 9), (EnemyKind.Crawler, 6) }, radio = "A big flight coming in. Stay on the tri-shot, and only roll when you have to." },
                        },
                        startRadio = new[] { "Something's flying in from the vents. Your twin blasters won't reach it." },
                        clearRadio = new[] { "You're learning faster than the House can adapt." },
                    },
                    new StageDef
                    {
                        id = "scrap3", name = "UNDER PRESSURE", objective = "Clear 4 waves", teaches = "Switching guns while you dodge", parTime = 100f,
                        layout = new[] { new Placement(C, -4f, 0f, false), new Placement(C, 4f, 0f, false), new Placement(B, 0f, 4.5f), new Placement(B, 0f, -3f) },
                        waves = new[]
                        {
                            new WaveDef { title = "CRAWLERS AND DRONES", enemies = new[] { (EnemyKind.Crawler, 10), (EnemyKind.Drone, 5) }, radio = "Ground and air at once. Dodge with the roll, and land on the gun you need." },
                            new WaveDef { title = "BOMBERS", enemies = new[] { (EnemyKind.Bomber, 3), (EnemyKind.Drone, 5) }, bombs = 2 },
                            new WaveDef { title = "MITES", enemies = new[] { (EnemyKind.Mite, 12), (EnemyKind.Crawler, 6) }, radio = "Mites. Small and fast. The tri-shot's spread is your friend." },
                            new WaveDef { title = "EVERYTHING", enemies = new[] { (EnemyKind.Crawler, 12), (EnemyKind.Drone, 7), (EnemyKind.Bomber, 2) }, bombs = 2, radio = "Roll over a pipe rack to vault it: you land on the opposite face." },
                        },
                        startRadio = new[] { "The House is throwing everything at you. Good. That means it's worried." },
                        clearRadio = new[] { "That's the bay cleared. The foreman is guarding the exit." },
                    },
                    new StageDef
                    {
                        id = "scrap4", name = "THE COMPACTOR", objective = "Beat the foreman", teaches = "Bosses: lure it, stun it, fire", parTime = 90f,
                        grant = "rail", grantFace = 6,
                        layout = new[] { new Placement(B, -4.5f, -1f), new Placement(B, 4.5f, -1f), new Placement(B, 0f, 3.5f), new Placement(B, -6f, 6f), new Placement(B, 6f, 6f) },
                        waves = new[] { new WaveDef { title = "FOREMAN", boss = EnemyKind.Compactor, radio = "That's the bay's compactor. Your guns bounce off it. Make it ram something." } },
                        startRadio = new[] { "Something big is moving in the bay." },
                        clearRadio = new[] { "It dropped its railgun. I've mounted it on your bottom face: face 6.", "The bottom face takes two rolls, or one vault over a pipe rack. Next deck: Hydroponics." },
                    },
                },
            };
            var S = HazardKind.Spores; var I = HazardKind.Ice; var V = HazardKind.Vent;
            WaveDef W(string title, string radio, params (EnemyKind, int)[] e) => new WaveDef { title = title, radio = radio, enemies = e };
            var hydro = new DeckDef
            {
                id = "hydro", name = "HYDROPONICS", look = "Deep green, mist, grow-lamps", mechanic = "Armour and the opposite face", theme = 5, playable = true,
                stages = new[]
                {
                    new StageDef
                    {
                        id = "hydro1", name = "ARMOUR", objective = "Clear 3 waves", teaches = "Tanks: the railgun on the bottom face", parTime = 90f,
                        layout = new[] { new Placement(C, -4f, -2.5f, true), new Placement(C, 4f, -2.5f, true), new Placement(B, -6f, 4f), new Placement(B, 6f, 4f), new Placement(B, 0f, 6f) },
                        waves = new[]
                        {
                            W("TANKS INCOMING", "Tanks. Only the railgun cracks them, and it's on your bottom face. Two rolls, or vault a pipe rack.", (EnemyKind.Tank, 2), (EnemyKind.Crawler, 6)),
                            W("TANKS AND DRONES", null, (EnemyKind.Tank, 3), (EnemyKind.Drone, 4)),
                            W("THE HERD", "Railgun for the tanks, tri-shot for the fliers. Plan the rolls.", (EnemyKind.Tank, 3), (EnemyKind.Crawler, 8), (EnemyKind.Drone, 3)),
                        },
                        startRadio = new[] { "Hydroponics. The crew grew food here. Now the House grows tanks." },
                        clearRadio = new[] { "Good. Two rolls to the opposite face: you'll do that a lot from now on." },
                    },
                    new StageDef
                    {
                        id = "hydro2", name = "SPORES", objective = "Clear 3 waves", teaches = "Spore clouds slow you down", parTime = 95f,
                        layout = new[] { new Placement(B, -5f, 0f), new Placement(B, 5f, 0f), new Placement(C, 0f, 3.5f, true), new Placement(B, 0f, -3f) },
                        hazards = new[] { new HazardDef(S, -4.5f, 4.5f, 2f), new HazardDef(S, 4.5f, 4.5f, 2f), new HazardDef(S, 0f, -0.5f, 1.5f) },
                        waves = new[]
                        {
                            W("DRONES", "Spore clouds. They won't hurt you, but you'll crawl. Don't fight inside one.", (EnemyKind.Drone, 6), (EnemyKind.Crawler, 4)),
                            W("TANKS IN THE MIST", null, (EnemyKind.Tank, 3), (EnemyKind.Crawler, 6)),
                            W("BLOOM", null, (EnemyKind.Tank, 2), (EnemyKind.Drone, 5), (EnemyKind.Mite, 9)),
                        },
                        startRadio = new[] { "The vents are pumping spores. Keep to clean floor." },
                        clearRadio = new[] { "Air's clearing. One more room before the gardener." },
                    },
                    new StageDef
                    {
                        id = "hydro3", name = "OVERGROWN", objective = "Clear 4 waves", teaches = "Everything so far, in the weeds", parTime = 110f,
                        cache = "flak", cachePos = new Vector2(-8.6f, 8.6f),
                        layout = new[] { new Placement(B, -7.4f, 7.2f), new Placement(B, -6.2f, 8.6f), new Placement(C, 3f, 2f, false), new Placement(B, -3f, 1f), new Placement(B, 6f, -2f) },
                        hazards = new[] { new HazardDef(S, 5f, 6f, 2f), new HazardDef(S, -4f, -3f, 1.8f) },
                        waves = new[]
                        {
                            W("BOMBERS", "Bombers in the planters. Shove the bombs off the edge.", (EnemyKind.Bomber, 3), (EnemyKind.Crawler, 6)),
                            W("TANKS", null, (EnemyKind.Tank, 4), (EnemyKind.Drone, 3)),
                            W("SWARM", null, (EnemyKind.Mite, 12), (EnemyKind.Drone, 5)),
                            W("EVERYTHING", "Something's hidden in the far corner, behind the planters. Worth a look.", (EnemyKind.Tank, 3), (EnemyKind.Crawler, 8), (EnemyKind.Drone, 4), (EnemyKind.Bomber, 2)),
                        },
                        startRadio = new[] { "This one's overgrown. Watch the corners." },
                        clearRadio = new[] { "The gardener's next. It's armoured all over: only the railgun gets through." },
                    },
                    new StageDef
                    {
                        id = "hydro4", name = "THE GARDENER", objective = "Beat the foreman", teaches = "Bosses: reach the right face under fire", parTime = 120f,
                        grant = "plasma", grantFace = 3,
                        layout = new[] { new Placement(C, -5f, -1f, true), new Placement(C, 5f, -1f, true), new Placement(B, -7f, 6f), new Placement(B, 7f, 6f) },
                        hazards = new[] { new HazardDef(S, -7.5f, -6.5f, 1.8f), new HazardDef(S, 7.5f, -6.5f, 1.8f) },
                        waves = new[] { new WaveDef { title = "FOREMAN", boss = EnemyKind.Gardener, radio = "The gardener. Railgun only, bottom face. Dodge the spore rings and keep it on the rail." } },
                        startRadio = new[] { "Something's rooted in the middle of the deck." },
                        clearRadio = new[] { "It dropped a plasma cannon. Mounted on face 3: it breaks armour too, and it blasts crowds.", "Next deck: the Cryo Mines." },
                    },
                },
            };
            var cryo = new DeckDef
            {
                id = "cryo", name = "CRYO MINES", look = "White ice and deep blue", mechanic = "Ice floor, mite bombs, shoving", theme = 6, playable = true,
                stages = new[]
                {
                    new StageDef
                    {
                        id = "cryo1", name = "THIN ICE", objective = "Clear 3 waves", teaches = "Ice: you slide, and rolls go further", parTime = 90f,
                        layout = new[] { new Placement(B, -6f, 0f), new Placement(B, 6f, 0f), new Placement(C, 0f, 5f, true) },
                        hazards = new[] { new HazardDef(I, -3.5f, 2f, 2.2f), new HazardDef(I, 3.5f, 2f, 2.2f), new HazardDef(I, 0f, -2.5f, 1.6f) },
                        waves = new[]
                        {
                            W("CRAWLERS", "Ice. You'll slide, and a roll on ice skids further. Steer early.", (EnemyKind.Crawler, 10)),
                            W("MITES", null, (EnemyKind.Mite, 12), (EnemyKind.Drone, 3)),
                            W("TANKS ON ICE", null, (EnemyKind.Tank, 3), (EnemyKind.Crawler, 8)),
                        },
                        startRadio = new[] { "The cryo mines. Cold enough to freeze the drills. Mind your footing." },
                        clearRadio = new[] { "You're getting the hang of sliding." },
                    },
                    new StageDef
                    {
                        id = "cryo2", name = "BOMB RUN", objective = "Clear 3 waves", teaches = "Shove bombs: they slide on ice", parTime = 95f,
                        layout = new[] { new Placement(B, -4f, 4f), new Placement(B, 4f, 4f), new Placement(B, 0f, 0f) },
                        hazards = new[] { new HazardDef(I, 0f, 4f, 2.5f), new HazardDef(I, -6f, -3f, 1.8f), new HazardDef(I, 6f, -3f, 1.8f) },
                        waves = new[]
                        {
                            W("BOMBERS", "Bombers. A bomb you shove on ice keeps sliding. Aim it at a crowd.", (EnemyKind.Bomber, 3), (EnemyKind.Crawler, 6)),
                            W("MITE BOMBS", null, (EnemyKind.Mite, 12), (EnemyKind.Bomber, 2)),
                            W("DEMOLITION", null, (EnemyKind.Bomber, 4), (EnemyKind.Tank, 2), (EnemyKind.Drone, 4)),
                        },
                        startRadio = new[] { "They're mining with explosives again. Turn that around." },
                        clearRadio = new[] { "Nice shoving." },
                    },
                    new StageDef
                    {
                        id = "cryo3", name = "WHITEOUT", objective = "Clear 4 waves", teaches = "Everything, on ice", parTime = 115f,
                        cache = "lance", cachePos = new Vector2(8.6f, -8.6f),
                        layout = new[] { new Placement(B, 7.2f, -7.4f), new Placement(B, 8.6f, -6.2f), new Placement(C, -3f, 3f, false), new Placement(B, 3f, 4f) },
                        hazards = new[] { new HazardDef(I, -5f, -2f, 2.2f), new HazardDef(I, 4f, 0.5f, 2f), new HazardDef(I, 0f, 7f, 2f) },
                        waves = new[]
                        {
                            W("DRONES", null, (EnemyKind.Drone, 7), (EnemyKind.Mite, 6)),
                            W("TANKS", null, (EnemyKind.Tank, 4), (EnemyKind.Crawler, 6)),
                            W("BOMBERS", null, (EnemyKind.Bomber, 3), (EnemyKind.Drone, 4)),
                            W("WHITEOUT", "I'm reading something odd near the bottom-right corner. A spare module?", (EnemyKind.Tank, 3), (EnemyKind.Crawler, 8), (EnemyKind.Drone, 5), (EnemyKind.Mite, 6)),
                        },
                        startRadio = new[] { "Visibility's dropping. Trust the markers." },
                        clearRadio = new[] { "The driller's tunnelling under the next chamber. Hit it when it comes up." },
                    },
                    new StageDef
                    {
                        id = "cryo4", name = "THE DRILLER", objective = "Beat the foreman", teaches = "Bosses: read the ground, hit it while it's up", parTime = 120f,
                        grant = "scatter", grantFace = 5,
                        layout = new[] { new Placement(B, -6f, 5f), new Placement(B, 6f, 5f), new Placement(B, -6f, -4f), new Placement(B, 6f, -4f) },
                        hazards = new[] { new HazardDef(I, 0f, 1f, 2.6f) },
                        waves = new[] { new WaveDef { title = "FOREMAN", boss = EnemyKind.Driller, radio = "The driller. It's under the ice. When the red ring shows, get clear, then hit it while it's up." } },
                        startRadio = new[] { "Feel that rumble?" },
                        clearRadio = new[] { "A scatter gun, on face 5. Brutal up close.", "Next: the foundry." },
                    },
                },
            };
            var foundry = new DeckDef
            {
                id = "foundry", name = "FOUNDRY", look = "Molten red, black steel", mechanic = "Shield colours name the gun that breaks them", theme = 7, playable = true,
                stages = new[]
                {
                    new StageDef
                    {
                        id = "foundry1", name = "SHIELDS", objective = "Clear 3 waves", teaches = "A shield breaks only to the gun of its colour", parTime = 90f,
                        layout = new[] { new Placement(B, -5f, 2f), new Placement(B, 5f, 2f), new Placement(C, 0f, -2f, true) },
                        waves = new[]
                        {
                            new WaveDef { title = "SHIELDED CRAWLERS", enemies = new[] { (EnemyKind.Crawler, 8) }, shields = new[] { "tri", "plasma" }, radio = "Shields, in your guns' colours. Only the matching gun cracks one. The ring tells you which." },
                            new WaveDef { title = "MIXED SHIELDS", enemies = new[] { (EnemyKind.Crawler, 8), (EnemyKind.Drone, 3) }, shields = new[] { "tri", "plasma", "scatter" } },
                            new WaveDef { title = "SHIELDED BOMBERS", enemies = new[] { (EnemyKind.Bomber, 3), (EnemyKind.Crawler, 6), (EnemyKind.Tank, 2) }, shields = new[] { "scatter", "plasma", "twin" } },
                        },
                        startRadio = new[] { "The foundry. The House fits its units with shields here." },
                        clearRadio = new[] { "Every shield is a question: which face? You're answering faster." },
                    },
                    new StageDef
                    {
                        id = "foundry2", name = "HEAT", objective = "Clear 3 waves", teaches = "Heat vents erupt to a beat", parTime = 100f,
                        layout = new[] { new Placement(B, -7f, 0f), new Placement(B, 7f, 0f), new Placement(B, 0f, 6.5f) },
                        hazards = new[]
                        {
                            new HazardDef(V, -6f, 3f, 0.9f, 0f), new HazardDef(V, -3f, 3f, 0.9f, 0.25f), new HazardDef(V, 0f, 3f, 0.9f, 0.5f), new HazardDef(V, 3f, 3f, 0.9f, 0.75f), new HazardDef(V, 6f, 3f, 0.9f, 0f),
                            new HazardDef(V, -4.5f, -2f, 0.9f, 0.5f), new HazardDef(V, 0f, -2f, 0.9f, 0f), new HazardDef(V, 4.5f, -2f, 0.9f, 0.5f),
                        },
                        waves = new[]
                        {
                            W("CRAWLERS", "Heat vents. They glow, then they blow, always in the same rhythm. Learn it.", (EnemyKind.Crawler, 10)),
                            new WaveDef { title = "SHIELDS IN THE HEAT", enemies = new[] { (EnemyKind.Crawler, 8), (EnemyKind.Drone, 4) }, shields = new[] { "tri", "scatter" } },
                            new WaveDef { title = "FURNACE", enemies = new[] { (EnemyKind.Tank, 3), (EnemyKind.Crawler, 8), (EnemyKind.Mite, 6) }, shields = new[] { "plasma" } },
                        },
                        startRadio = new[] { "Hot floor ahead. Watch the vents." },
                        clearRadio = new[] { "One more hall, then the smelter." },
                    },
                    new StageDef
                    {
                        id = "foundry3", name = "MELTDOWN", objective = "Clear 4 waves", teaches = "Shields, vents and everything else", parTime = 120f,
                        cache = "mortar", cachePos = new Vector2(-8.6f, -8.6f),
                        layout = new[] { new Placement(B, -7.4f, -7.2f), new Placement(B, -6.2f, -8.6f), new Placement(C, 3f, 3f, true), new Placement(B, -3f, 3f) },
                        hazards = new[] { new HazardDef(V, -4f, 0f, 0.9f, 0f), new HazardDef(V, 0f, 0f, 0.9f, 0.33f), new HazardDef(V, 4f, 0f, 0.9f, 0.66f), new HazardDef(V, 0f, 6f, 0.9f, 0.5f) },
                        waves = new[]
                        {
                            new WaveDef { title = "SHIELDS", enemies = new[] { (EnemyKind.Crawler, 10) }, shields = new[] { "tri", "plasma", "scatter", "rail" } },
                            W("TANKS", null, (EnemyKind.Tank, 4), (EnemyKind.Drone, 4)),
                            new WaveDef { title = "BOMBERS", enemies = new[] { (EnemyKind.Bomber, 4), (EnemyKind.Mite, 8) }, shields = new[] { "scatter" } },
                            new WaveDef { title = "MELTDOWN", enemies = new[] { (EnemyKind.Tank, 3), (EnemyKind.Crawler, 10), (EnemyKind.Drone, 5) }, shields = new[] { "plasma", "tri" }, radio = "There's a cold spot behind the crucibles, bottom-left. Something's stashed there." },
                        },
                        startRadio = new[] { "The furnaces are running hot. The House knows you're coming." },
                        clearRadio = new[] { "The smelter's plated in three colours. Break them in order." },
                    },
                    new StageDef
                    {
                        id = "foundry4", name = "THE SMELTER", objective = "Beat the foreman", teaches = "Bosses: one gun per plate, in order", parTime = 130f,
                        grant = "missile", grantFace = 4,
                        layout = new[] { new Placement(B, -6f, 5f), new Placement(B, 6f, 5f), new Placement(C, 0f, -3f, true) },
                        hazards = new[] { new HazardDef(V, -6f, -1f, 0.9f, 0f), new HazardDef(V, 6f, -1f, 0.9f, 0.5f), new HazardDef(V, 0f, 7f, 0.9f, 0.25f) },
                        waves = new[] { new WaveDef { title = "FOREMAN", boss = EnemyKind.Smelter, radio = "The smelter. Three plates, three colours. The pulsing one is next: roll to its gun." } },
                        startRadio = new[] { "The heat's unreal in here." },
                        clearRadio = new[] { "A missile pod, on face 4. That's all six faces armed.", "Only the Core is left. The House is waiting." },
                    },
                },
            };
            var core = new DeckDef
            {
                id = "core", name = "THE CORE", look = "Navy and cyan, the House", mechanic = "Everything, then the High Roller", theme = 3, playable = true,
                stages = new[]
                {
                    new StageDef
                    {
                        id = "core1", name = "THE CORE", objective = "Clear 4 waves", teaches = "Everything you've learnt", parTime = 130f,
                        layout = new[] { new Placement(B, -5f, 3f), new Placement(B, 5f, 3f), new Placement(C, 0f, -2f, true), new Placement(B, 0f, 6.5f) },
                        hazards = new[] { new HazardDef(I, -6f, -4f, 1.6f), new HazardDef(V, 6f, -4f, 0.9f, 0f), new HazardDef(S, 0f, 2.5f, 1.4f) },
                        waves = new[]
                        {
                            new WaveDef { title = "THE HOUSE'S GUARD", enemies = new[] { (EnemyKind.Crawler, 10), (EnemyKind.Drone, 5) }, shields = new[] { "tri", "plasma" }, radio = "The Core. Everything the House has, at once." },
                            W("TANKS", null, (EnemyKind.Tank, 4), (EnemyKind.Mite, 9)),
                            W("BOMBERS", null, (EnemyKind.Bomber, 4), (EnemyKind.Drone, 5)),
                            new WaveDef { title = "EVERYTHING", enemies = new[] { (EnemyKind.Tank, 3), (EnemyKind.Crawler, 10), (EnemyKind.Drone, 6), (EnemyKind.Bomber, 2) }, shields = new[] { "scatter", "missile" } },
                        },
                        startRadio = new[] { "This is it, Pip. The crew is two rooms away." },
                        clearRadio = new[] { "One more line of defence." },
                    },
                    new StageDef
                    {
                        id = "core2", name = "LAST LINE", objective = "Clear 4 waves", teaches = "Hold out", parTime = 140f,
                        cache = "needler", cachePos = new Vector2(0f, 9.2f),
                        layout = new[] { new Placement(B, -1.2f, 8f), new Placement(B, 1.2f, 8f), new Placement(C, -5f, 1f, false), new Placement(C, 5f, 1f, false) },
                        hazards = new[] { new HazardDef(V, -2.5f, 3f, 0.9f, 0f), new HazardDef(V, 2.5f, 3f, 0.9f, 0.5f), new HazardDef(I, 0f, -3f, 2f) },
                        waves = new[]
                        {
                            new WaveDef { title = "SHIELD WALL", enemies = new[] { (EnemyKind.Crawler, 12) }, shields = new[] { "tri", "plasma", "scatter", "missile", "rail" } },
                            W("ARMOUR", null, (EnemyKind.Tank, 5), (EnemyKind.Drone, 5)),
                            W("SWARM", null, (EnemyKind.Mite, 15), (EnemyKind.Bomber, 3)),
                            W("LAST LINE", "There's a locker behind the barricade at the top. Grab what's inside if you can.", (EnemyKind.Tank, 4), (EnemyKind.Crawler, 10), (EnemyKind.Drone, 6), (EnemyKind.Bomber, 2)),
                        },
                        startRadio = new[] { "The House is pulling everything back to defend itself." },
                        clearRadio = new[] { "The door's open. It's the House itself, in a die of its own." },
                    },
                    new StageDef
                    {
                        id = "core3", name = "THE HIGH ROLLER", objective = "Beat the House", teaches = "The final boss: one face per phase", parTime = 180f,
                        layout = new[] { new Placement(C, -5f, -2f, true), new Placement(C, 5f, -2f, true), new Placement(B, -7f, 6f), new Placement(B, 7f, 6f) },
                        hazards = new[] { new HazardDef(I, -7f, -6.5f, 1.5f), new HazardDef(I, 7f, -6.5f, 1.5f) },
                        waves = new[] { new WaveDef { title = "THE HOUSE", boss = EnemyKind.Boss, bossTier = 1, bossHealth = 3f, radio = "Only the gun on its top number hurts it. It locks a number for each third of its health. Read it, roll to it." } },
                        startRadio = new[] { "Pip. Whatever happens: thank you." },
                        clearRadio = new[] { "It's over. Pip... you did it." },
                    },
                },
            };
            return new[] { test, hydro, cryo, foundry, core };
        }

        public static StageDef Find(string id)
        {
            foreach (var d in Decks) foreach (var s in d.stages) if (s.id == id) return s;
            return null;
        }

        public static DeckDef DeckOf(StageDef s)
        {
            foreach (var d in Decks) if (System.Array.IndexOf(d.stages, s) >= 0) return d;
            return null;
        }

        /// <summary>The stage after this one in its deck, or null at the end of the deck.</summary>
        public static StageDef Next(StageDef s)
        {
            var d = DeckOf(s);
            int i = System.Array.IndexOf(d.stages, s);
            return i + 1 < d.stages.Length ? d.stages[i + 1] : null;
        }

        // ---------------- Saved progress ----------------

        // ---------------- Pip's modules in the campaign (separate from the gauntlet's die build) ----------------

        /// <summary>Pip starts with one working module; the others are empty sockets until found or won.</summary>
        public static readonly string[] StartFaces = { null, "twin", "empty", "empty", "empty", "empty", "empty" };

        /// <summary>Tests: treat every stage before this one (in its deck) as cleared when working out Pip's modules.</summary>
        public static StageDef AssumeClearedBefore;

        /// <summary>Pip's modules: the starting one, plus what every cleared stage handed over.</summary>
        public static string[] Faces
        {
            get
            {
                var f = (string[])StartFaces.Clone();
                int testDeck = AssumeClearedBefore != null ? System.Array.IndexOf(Decks, DeckOf(AssumeClearedBefore)) : -1;
                for (int di = 0; di < Decks.Length; di++)
                {
                    var d = Decks[di];
                    // Tests: every stage of earlier decks, and the stages before the tested one in its own deck.
                    int limit = di < testDeck ? d.stages.Length : di == testDeck ? System.Array.IndexOf(d.stages, AssumeClearedBefore) : -1;
                    for (int i = 0; i < d.stages.Length; i++)
                    {
                        var s = d.stages[i];
                        // Tests ignore saved progress: exactly the stages before the tested one count as cleared.
                        bool done = AssumeClearedBefore != null ? i < limit : Cleared(s);
                        if (s.grant != null && done) f[s.grantFace] = s.grant;
                    }
                }
                return f;
            }
        }

        /// <summary>The faces a stage is played with.</summary>
        public static string[] FacesFor(StageDef s) => s.startFaces != null ? (string[])s.startFaces.Clone() : Faces;

        // ---------------- Hidden modules (one cache per deck from Hydroponics on) ----------------

        public static bool CacheFound(string id) => PlayerPrefs.GetInt("rp.c.cache." + id, 0) == 1;

        /// <summary>A cache module is kept for the Workshop, and unlocked in the gauntlet's die builder too.</summary>
        public static void FindCache(string id)
        {
            PlayerPrefs.SetInt("rp.c.cache." + id, 1);
            PlayerPrefs.SetInt("rp.own." + id, 1);
            PlayerPrefs.Save();
        }

        public static bool SeenIntro { get => PlayerPrefs.GetInt("rp.c.intro", 0) == 1; set => PlayerPrefs.SetInt("rp.c.intro", value ? 1 : 0); }
        public static bool Cleared(StageDef s) => PlayerPrefs.GetInt("rp.c.clear." + s.id, 0) == 1;
        /// <summary>Stars earned (bit 0 cleared, bit 1 no damage, bit 2 under par time).</summary>
        public static int Stars(StageDef s) => PlayerPrefs.GetInt("rp.c.stars." + s.id, 0);
        public static bool Unlocked(StageDef s)
        {
            var d = DeckOf(s);
            int i = System.Array.IndexOf(d.stages, s);
            if (!d.playable) return false;
            if (i > 0) return Cleared(d.stages[i - 1]);
            int di = System.Array.IndexOf(Decks, d);
            // A deck opens when the one before it is beaten.
            return di == 0 || Cleared(Decks[di - 1].stages[Decks[di - 1].stages.Length - 1]);
        }

        public static void Record(StageDef s, int stars)
        {
            PlayerPrefs.SetInt("rp.c.clear." + s.id, 1);
            PlayerPrefs.SetInt("rp.c.stars." + s.id, Stars(s) | stars);
            PlayerPrefs.Save();
        }

        public static int StarCount(int mask) => (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);

        // ---------------- The run in progress (survives scene reloads) ----------------

        /// <summary>Stage to start after the next scene load (null: normal title / gauntlet).</summary>
        public static StageDef Pending;
        /// <summary>Stage being played right now (null in the gauntlet).</summary>
        public static StageDef Active;
        /// <summary>Upgrades as they were when this stage started: restored on retry and carried into the next stage.</summary>
        public static RunStats StageStartStats;
        public static Dictionary<string, int> StageStartLevels;
        public static int Deaths;      // deaths on the current stage (offers Assist after 2)
        public static bool OpenMap;    // show the campaign map after the next scene load

        public static void BeginDeck(StageDef first)
        {
            StageStartStats = new RunStats();
            StageStartLevels = new Dictionary<string, int>();
            Deaths = 0;
            Pending = first;
        }
    }
}
