using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>Modifiers for the current run. Reset on every new game; changed by upgrades.</summary>
    public class RunStats
    {
        public static RunStats Current = new RunStats();

        public float damageMul = 1f;
        public float fireRateMul = 1f;
        public float slamRadiusMul = 1f;
        public int slamDamageBonus;
        public float dashCooldownMul = 1f;
        public float overchargeTime = 3f;
        public int maxHpBonus;
        public float kickMul = 1f;
        public int blastResist;
        public float chainChance;
        public int repairEvery;        // 0 = off; otherwise every Nth roll repairs 1 integrity
        public int extraShots;
        public float fuseBonus;
        // Face upgrades: per-face multipliers (index = face number).
        public readonly float[] faceDamage = { 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        public readonly float[] faceRate = { 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        public float FaceDamage(int face) => face >= 1 && face <= 6 ? faceDamage[face] : 1f;
        public float FaceRate(int face) => face >= 1 && face <= 6 ? faceRate[face] : 1f;
    }

    public class UpgradeDef
    {
        public string id, name, desc;
        public int maxLevel;
        public Color color;
        public Action<RunStats, int> apply; // (stats, new level)

        /// <summary>Face number for a face upgrade ("face3" → 3), else 0.</summary>
        public int Face => id.StartsWith("face") ? id[4] - '0' : 0;

        /// <summary>Everything the deck can deal: the general upgrades plus one tune-up per face of this die.</summary>
        static UpgradeDef[] all;
        public static UpgradeDef[] All => all ??= Combine(BuildFaces()); // lazy: General is declared below

        public static void RebuildFaceCards() => all = Combine(BuildFaces());

        static UpgradeDef[] Combine(UpgradeDef[] faces)
        {
            var all = new UpgradeDef[General.Length + faces.Length];
            General.CopyTo(all, 0);
            faces.CopyTo(all, General.Length);
            return all;
        }

        static UpgradeDef[] BuildFaces()
        {
            var list = new UpgradeDef[6];
            for (int f = 1; f <= 6; f++)
            {
                int face = f;
                var w = WeaponDef.All[f];
                list[f - 1] = new UpgradeDef
                {
                    id = "face" + f, name = w.name + " TUNE-UP", maxLevel = 3, color = w.color,
                    desc = $"Face {f} only: +30% damage and +15% fire rate for the {Title(w.name)}",
                    apply = (s, l) => { s.faceDamage[face] += 0.3f; s.faceRate[face] += 0.15f; },
                };
            }
            return list;
        }

        static string Title(string caps) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(caps.ToLowerInvariant());

        static readonly UpgradeDef[] General =
        {
            new UpgradeDef { id = "dmg", name = "HOLLOW POINTS", desc = "+25% weapon damage", maxLevel = 4, color = Palette.Hex("#FF5C5C"),
                apply = (s, l) => s.damageMul += 0.25f },
            new UpgradeDef { id = "rate", name = "HAIR TRIGGER", desc = "+20% fire rate", maxLevel = 4, color = Palette.Hex("#FFD24A"),
                apply = (s, l) => s.fireRateMul += 0.2f },
            new UpgradeDef { id = "slam", name = "HEAVY LANDING", desc = "Roll landings hit 25% wider and +1 harder", maxLevel = 3, color = Palette.Hex("#FF8A2A"),
                apply = (s, l) => { s.slamRadiusMul += 0.25f; s.slamDamageBonus++; } },
            new UpgradeDef { id = "dash", name = "AFTERBURNER", desc = "Dash recharges 25% faster", maxLevel = 3, color = Palette.Hex("#3DFFB0"),
                apply = (s, l) => s.dashCooldownMul *= 0.75f },
            new UpgradeDef { id = "over", name = "OVERCLOCK", desc = "Double fire rate lasts +2s after each roll", maxLevel = 3, color = Palette.Hex("#35E6FF"),
                apply = (s, l) => s.overchargeTime += 2f },
            new UpgradeDef { id = "hp", name = "REINFORCED SHELL", desc = "+1 max integrity and a full repair", maxLevel = 3, color = Palette.Hex("#8AF0FF"),
                apply = (s, l) => s.maxHpBonus++ },
            new UpgradeDef { id = "loaded", name = "LOADED DIE", desc = "Every 3rd roll repairs 1 integrity (then every 2nd)", maxLevel = 2, color = Palette.Hex("#FFC940"),
                apply = (s, l) => s.repairEvery = l == 1 ? 3 : 2 },
            new UpgradeDef { id = "resist", name = "BLAST PLATING", desc = "Bomb blasts deal 1 less damage to you", maxLevel = 2, color = Palette.Hex("#B8C4D6"),
                apply = (s, l) => s.blastResist++ },
            new UpgradeDef { id = "kick", name = "POWER SHOVE", desc = "Shove bombs 35% harder", maxLevel = 2, color = Palette.Hex("#4D8BFF"),
                apply = (s, l) => s.kickMul += 0.35f },
            new UpgradeDef { id = "chain", name = "CHAIN REACTION", desc = "Kills have a 25% chance to explode", maxLevel = 3, color = Palette.Hex("#FF3FA4"),
                apply = (s, l) => s.chainChance += 0.25f },
            new UpgradeDef { id = "barrel", name = "EXTRA BARREL", desc = "Multi-shot guns fire +1 projectile", maxLevel = 2, color = Palette.Hex("#5CFF8A"),
                apply = (s, l) => s.extraShots++ },
            new UpgradeDef { id = "fuse", name = "SLOW BURN", desc = "Bomb fuses last 2s longer", maxLevel = 2, color = Palette.Hex("#FF7A1A"),
                apply = (s, l) => s.fuseBonus += 2f },
        };
    }

    /// <summary>Tracks owned upgrade levels and deals random cards.</summary>
    public class UpgradeDeck
    {
        readonly Dictionary<string, int> levels = new Dictionary<string, int>();
        readonly System.Random rng;

        public UpgradeDeck(int seed) { rng = new System.Random(seed); }

        public int Level(UpgradeDef u) => levels.TryGetValue(u.id, out int l) ? l : 0;

        public List<UpgradeDef> Deal(int count)
        {
            var pool = new List<UpgradeDef>();
            foreach (var u in UpgradeDef.All) if (Level(u) < u.maxLevel) pool.Add(u);
            var hand = new List<UpgradeDef>();
            while (hand.Count < count && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                hand.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return hand;
        }

        public void Take(UpgradeDef u, RunStats stats)
        {
            int l = Level(u) + 1;
            levels[u.id] = l;
            u.apply(stats, l);
        }

        /// <summary>Owned upgrades with their level, for the pause screen.</summary>
        public IEnumerable<(UpgradeDef def, int level)> Owned()
        {
            foreach (var u in UpgradeDef.All)
                if (Level(u) > 0) yield return (u, Level(u));
        }
    }
}
