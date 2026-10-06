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
        public string[] startRadio = new string[0];
        public string[] clearRadio = new string[0];
    }

    /// <summary>A station deck: its stages, the gun module its boss hands over, and whether it is built yet.</summary>
    public class DeckDef
    {
        public string id, name, look, mechanic;
        public StageDef[] stages = new StageDef[0];
        public bool playable;
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
                id = "scrap", name = "SCRAP BAY", look = "Placeholder art (navy deck) until phase 5", mechanic = "Roll to dodge and to change guns",
                playable = true,
                stages = new[]
                {
                    new StageDef
                    {
                        id = "scrap1", name = "BOOT-UP", objective = "Clear 2 waves", teaches = "Moving, automatic fire, the roll", parTime = 30f,
                        layout = new[] { new Placement(B, -4f, 0f), new Placement(B, 4f, 0f), new Placement(B, 0f, 4f), new Placement(C, 0f, -1.5f, true) },
                        waves = new[]
                        {
                            new WaveDef { title = "CRAWLERS INCOMING", enemies = new[] { (EnemyKind.Crawler, 6) }, radio = "Crawlers. Your guns aim on their own, just keep moving." },
                            new WaveDef { title = "MORE CRAWLERS", enemies = new[] { (EnemyKind.Crawler, 9) }, radio = "Roll through them. You can't be hurt at the start of a roll." },
                        },
                        startRadio = new[] { "Pip, can you hear me? It's Vega, chief engineer. You're the only unit still moving." },
                        clearRadio = new[] { "Nice. That's the scrap bay quiet. Keep going." },
                    },
                    new StageDef
                    {
                        id = "scrap2", name = "FLIERS", objective = "Clear 3 waves", teaches = "Some enemies need a certain gun", parTime = 45f,
                        layout = new[] { new Placement(B, -5f, 3f), new Placement(B, 5f, 3f), new Placement(B, -3f, -3f), new Placement(B, 3f, -3f), new Placement(C, 0f, 2f, false) },
                        waves = new[]
                        {
                            new WaveDef { title = "DRONES INCOMING", enemies = new[] { (EnemyKind.Drone, 5) }, radio = "Drones fly over most shots. Look at the markers around you: roll to the one that lights up." },
                            new WaveDef { title = "MIXED SWARM", enemies = new[] { (EnemyKind.Crawler, 6), (EnemyKind.Drone, 4) } },
                            new WaveDef { title = "BOMBERS", enemies = new[] { (EnemyKind.Bomber, 2), (EnemyKind.Crawler, 6) }, bombs = 2, radio = "Bombs! Shove them off the edge before they blow." },
                        },
                        startRadio = new[] { "Something's flying in from the vents. Your twin blasters won't reach it." },
                        clearRadio = new[] { "You're learning faster than the House can adapt." },
                    },
                    new StageDef
                    {
                        id = "scrap3", name = "UNDER PRESSURE", objective = "Clear 3 waves", teaches = "Switching guns while you dodge", parTime = 60f,
                        layout = new[] { new Placement(C, -4f, 0f, false), new Placement(C, 4f, 0f, false), new Placement(B, 0f, 4.5f), new Placement(B, 0f, -3f) },
                        waves = new[]
                        {
                            new WaveDef { title = "CRAWLERS AND DRONES", enemies = new[] { (EnemyKind.Crawler, 8), (EnemyKind.Drone, 4) }, radio = "Ground and air at once. Dodge with the roll, and land on the gun you need." },
                            new WaveDef { title = "BOMBERS", enemies = new[] { (EnemyKind.Bomber, 3), (EnemyKind.Drone, 4) }, bombs = 2 },
                            new WaveDef { title = "EVERYTHING", enemies = new[] { (EnemyKind.Crawler, 10), (EnemyKind.Drone, 6), (EnemyKind.Bomber, 2) }, bombs = 2, radio = "Roll over a pipe rack to vault it: you land on the opposite face." },
                        },
                        startRadio = new[] { "The House is throwing everything at you. Good. That means it's worried." },
                        clearRadio = new[] { "That's the deck. The foreman's next, but it's not built yet. Phase 5." },
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
