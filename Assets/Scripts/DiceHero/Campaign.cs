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
            DeckDef Locked(string id, string name, string look, string mechanic) => new DeckDef { id = id, name = name, look = look, mechanic = mechanic };
            return new[]
            {
                test,
                Locked("hydro", "HYDROPONICS", "Deep green, mist, grow-lamps", "Armour and the opposite face"),
                Locked("cryo", "CRYO MINES", "White ice and deep blue", "Ice floor, mite bombs, shoving"),
                Locked("foundry", "FOUNDRY", "Molten red, black steel", "Shield colours name the gun that breaks them"),
                Locked("core", "THE CORE", "Navy and cyan, the House", "Everything, then the High Roller"),
            };
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
                foreach (var d in Decks)
                {
                    int limit = AssumeClearedBefore != null && System.Array.IndexOf(d.stages, AssumeClearedBefore) >= 0 ? System.Array.IndexOf(d.stages, AssumeClearedBefore) : -1;
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

        public static bool SeenIntro { get => PlayerPrefs.GetInt("rp.c.intro", 0) == 1; set => PlayerPrefs.SetInt("rp.c.intro", value ? 1 : 0); }
        public static bool Cleared(StageDef s) => PlayerPrefs.GetInt("rp.c.clear." + s.id, 0) == 1;
        /// <summary>Stars earned (bit 0 cleared, bit 1 no damage, bit 2 under par time).</summary>
        public static int Stars(StageDef s) => PlayerPrefs.GetInt("rp.c.stars." + s.id, 0);
        public static bool Unlocked(StageDef s)
        {
            var d = DeckOf(s);
            int i = System.Array.IndexOf(d.stages, s);
            return d.playable && (i == 0 || Cleared(d.stages[i - 1]));
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
