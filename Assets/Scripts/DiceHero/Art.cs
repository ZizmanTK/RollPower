using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Art-direction prototypes. Theme 0 is the shipped 2.0 look; 1-3 are candidate directions picked with
    /// "-theme N" on the command line. Gameplay (footprints, roll rules, guns per face) is identical in all.
    ///   1 Show arena  (Assault Android Cactus): clean grey arena, glowing ring lines, white toy-like die.
    ///   2 Industrial  (The Ascent, Deep Rock Galactic: Survivor, SYNTHETIK): gunmetal deck, hazard paint, armoured gold die.
    ///   3 Neon void   (Nex Machina, Geometry Wars 3): black glass deck, neon grid, black die with pips lit in each gun's colour.
    /// </summary>
    public static class Art
    {
        public static int Theme = ReadTheme();
        /// <summary>The look outside the campaign (title, gauntlet): -theme N, else 3. Campaign decks bring their own.</summary>
        public static readonly int BaseTheme = Theme;

        static int ReadTheme()
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-theme");
            return i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out int t) ? t : 3; // 3 (navy) is the default look; -theme 0 restores 2.0
        }

        public static Color Space => Theme switch
        {
            1 => Palette.Hex("#0A0E1C"),
            2 => Palette.Hex("#07080B"),
            3 => Palette.Hex("#06101A"),
            4 => Palette.Hex("#0E0C0A"),
            _ => World.SpaceColor,
        };

        /// <summary>Ambient light and sun colour per theme.</summary>
        public static void Light(Light sun)
        {
            switch (Theme)
            {
                case 1:
                    sun.color = Palette.Hex("#FFF4E6"); sun.intensity = 1.25f;
                    RenderSettings.ambientSkyColor = new Color(0.42f, 0.46f, 0.6f);
                    RenderSettings.ambientEquatorColor = new Color(0.26f, 0.28f, 0.36f);
                    break;
                case 2:
                    sun.color = Palette.Hex("#FFD9A8"); sun.intensity = 1.5f;
                    RenderSettings.ambientSkyColor = new Color(0.22f, 0.26f, 0.3f);
                    RenderSettings.ambientEquatorColor = new Color(0.14f, 0.14f, 0.15f);
                    break;
                case 3:
                    sun.color = Palette.Hex("#DCEBFF"); sun.intensity = 1.3f;
                    RenderSettings.ambientSkyColor = new Color(0.3f, 0.4f, 0.55f);
                    RenderSettings.ambientEquatorColor = new Color(0.16f, 0.22f, 0.3f);
                    break;
                case 4: // Scrap Bay: warm sodium light indoors
                    sun.color = Palette.Hex("#FFEBD6"); sun.intensity = 1.6f;
                    RenderSettings.ambientSkyColor = new Color(0.34f, 0.32f, 0.3f);
                    RenderSettings.ambientEquatorColor = new Color(0.2f, 0.17f, 0.14f);
                    break;
            }
        }

        public static DiceModel.Look Dice(Palette pal) => Theme switch
        {
            1 => new DiceModel.Look
            {
                body = pal.Get("A1DiceBody", Palette.Hex("#EEF1F6"), 0.88f, 0f),
                plate = pal.Get("A1DicePlate", Palette.Hex("#D5DAE3"), 0.8f, 0f),
                pip = pal.Glow("A1DicePip", Palette.Hex("#22D3FF"), 3.2f),
                seam = pal.Glow("A1DiceSeam", Palette.Hex("#22D3FF"), 1.4f),
            },
            2 => new DiceModel.Look
            {
                body = pal.Get("A2DiceGold", Palette.Hex("#F2B23A"), 0.6f, 0.3f),
                plate = pal.Get("A2DicePlate", Palette.Hex("#E0A02C"), 0.5f, 0.3f),
                pip = pal.Get("A2DicePip", Palette.Hex("#0E0B08"), 0.4f, 0f),
                seam = pal.Get("A2DiceFrame", Palette.Hex("#3A3E45"), 0.45f, 0.3f),
            },
            >= 3 => new DiceModel.Look
            {
                body = pal.Get("A3DiceBody", Palette.Hex("#0B1A2A"), 0.85f, 0.1f),
                plate = pal.Get("A3DicePlate", Palette.Hex("#10243A"), 0.85f, 0.1f),
                pip = pal.Glow("A3DicePip", Color.white, 3f),
                seam = pal.Glow("A3DiceSeam", Palette.Hex("#29B6F6"), 2.2f),
            },
            _ => DiceModel.Hero(pal),
        };

        /// <summary>Theme 3: the player is Pip-6 (eye-pod, face modules) rather than the plain die.</summary>
        public static bool Pip => Theme >= 3;
        /// <summary>Theme 3: each face's pips glow in the colour of the gun that face fires.</summary>
        public static bool PipsInGunColour => Theme >= 3;
        /// <summary>Theme 2: thick dark armour bars on the edges instead of thin glowing seams.</summary>
        public static float SeamThickness => Theme == 2 ? 0.085f : 0.03f;

        /// <summary>Gun body materials: (dark, body, glow intensity, scale).</summary>
        public static (Material dark, Material body, float glow, float scale) Gun(Palette pal) => Theme switch
        {
            1 => (pal.Get("A1GunDark", Palette.Hex("#2A2F3A"), 0.7f, 0.3f), pal.Get("A1GunBody", Palette.Hex("#F2F4F8"), 0.85f, 0f), 3f, 1f),
            2 => (pal.Get("A2GunDark", Palette.Hex("#2A2E35"), 0.5f, 0.3f), pal.Get("A2GunBody", Palette.Hex("#E8B21E"), 0.5f, 0.25f), 2.2f, 1.15f),
            >= 3 => (pal.Get("A3GunDark", Palette.Hex("#0B1A2A"), 0.8f, 0.1f), pal.Get("A3GunBody", Palette.Hex("#DDE8F0"), 0.7f, 0.1f), 3f, 1f),
            _ => (pal.Get("GunDark", Palette.Hex("#1A1D23"), 0.6f, 0.8f), pal.Get("GunSteel", Palette.Hex("#A7B1BE"), 0.7f, 0.5f), 3f, 1f),
        };
    }
}
