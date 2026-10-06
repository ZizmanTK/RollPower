using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// The player's die build, kept between runs: which gun sits on each face, which guns are unlocked, and the
    /// chips earned by playing (spent to unlock guns). Opposite faces add up to 7, so a single roll can never
    /// go straight to the opposite face; only a pipe rack (double roll) can.
    /// </summary>
    public static class Loadout
    {
        public static readonly string[] Default = { null, "rail", "twin", "tri", "plasma", "scatter", "missile" };

        static string[] faces;
        /// <summary>Gun id on each face (index 0 unused). Loaded on first use, not in a static constructor (PlayerPrefs).</summary>
        public static string[] Faces => faces ??= Load();

        static string[] Load()
        {
            var f = (string[])Default.Clone();
            string saved = PlayerPrefs.GetString("rp.loadout", "");
            var parts = saved.Split(',');
            if (parts.Length == 6)
                for (int i = 0; i < 6; i++)
                    if (Owns(parts[i])) f[i + 1] = parts[i];
            // A broken save could repeat a gun: fall back to the default die.
            var seen = new HashSet<string>();
            for (int i = 1; i <= 6; i++) if (!seen.Add(f[i])) return (string[])Default.Clone();
            return f;
        }

        /// <summary>Tests: use these faces without checking ownership or saving.</summary>
        public static void OverrideForTest(string[] f) => faces = f;

        public static void Save()
        {
            PlayerPrefs.SetString("rp.loadout", string.Join(",", Faces, 1, 6));
            PlayerPrefs.Save();
        }

        public static int Chips { get => PlayerPrefs.GetInt("rp.chips", 0); set => PlayerPrefs.SetInt("rp.chips", Mathf.Max(0, value)); }

        public static bool Owns(string id)
        {
            var w = WeaponDef.Find(id);
            return w.id == id && (w.cost == 0 || PlayerPrefs.GetInt("rp.own." + id, 0) == 1);
        }

        /// <summary>Spends chips on a gun. Returns false if it is already owned or too expensive.</summary>
        public static bool Unlock(string id)
        {
            var w = WeaponDef.Find(id);
            if (Owns(id) || Chips < w.cost) return false;
            Chips -= w.cost;
            PlayerPrefs.SetInt("rp.own." + id, 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Puts a gun on a face. If it already sits on another face, the two swap.</summary>
        public static void Assign(int face, string id)
        {
            if (!Owns(id)) return;
            int other = System.Array.IndexOf(Faces, id);
            if (other > 0) Faces[other] = Faces[face];
            Faces[face] = id;
            Save();
        }

        /// <summary>What the die is missing to face every enemy (drones fly, tanks are armoured), or null if it is complete.</summary>
        public static string Missing()
        {
            bool air = false, armour = false;
            for (int f = 1; f <= 6; f++) { var w = WeaponDef.Find(Faces[f]); air |= w.antiAir; armour |= w.armorPiercing; }
            if (!air && !armour) return "A GUN THAT HITS FLIERS AND ONE THAT PIERCES ARMOUR";
            return !air ? "A GUN THAT HITS FLIERS (FOR DRONES)" : !armour ? "A GUN THAT PIERCES ARMOUR (FOR TANKS)" : null;
        }

        /// <summary>Chips for a finished run: 10 per wave reached, 40 per boss beaten, 1 per 5 kills.</summary>
        public static int ChipsFor(int wave, int bosses, int kills) => wave * 10 + bosses * 40 + kills / 5;
    }
}
