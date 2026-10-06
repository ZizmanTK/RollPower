using UnityEngine;

namespace DiceHero
{
    /// <summary>"Build your die": put a gun on each face, unlock new guns with chips, launch a run.</summary>
    public partial class GameLoop
    {
        /// <summary>Set before a reload to open the build screen instead of the title.</summary>
        public static bool OpenLoadout;

        int loadoutFace = 1;   // face being edited
        int loadoutRow;        // row in the gun list; the last row is LAUNCH
        int chipsEarned;
        string loadoutMsg;
        float loadoutMsgTime;

        int LoadoutRows => WeaponDef.Catalog.Length + 1;

        void EnterLoadout()
        {
            State = Screen2.Loadout;
            loadoutRow = System.Array.FindIndex(WeaponDef.Catalog, w => w.id == Loadout.Faces[loadoutFace]);
            if (cam != null) cam.Orbit = true;
        }

        void LoadoutInput(float udt)
        {
            if (Controls.Back) { State = Screen2.Title; Sound.Play(Sfx.UiConfirm, 0.6f, 0f); return; }
            var nav = Controls.Nav(udt);
            if (nav.x != 0) { loadoutFace = (loadoutFace - 1 + nav.x + 6) % 6 + 1; Sound.Play(Sfx.UiMove, 0.5f, 0f); }
            if (nav.y != 0) { loadoutRow = (loadoutRow - nav.y + LoadoutRows) % LoadoutRows; Sound.Play(Sfx.UiMove, 0.5f, 0f); }
            int key = Controls.NumberKey();
            if (key >= 1 && key <= 6) { loadoutFace = key; Sound.Play(Sfx.UiMove, 0.5f, 0f); }
            if (Controls.Confirm) ActivateLoadoutRow(loadoutRow);
        }

        void ActivateLoadoutRow(int row)
        {
            if (row >= WeaponDef.Catalog.Length) { Sound.Play(Sfx.UiConfirm, 0.7f, 0f); Reload(true); return; } // launch with the new die
            var w = WeaponDef.Catalog[row];
            if (!Loadout.Owns(w.id))
            {
                if (Loadout.Unlock(w.id)) { Toast($"{w.name} UNLOCKED", 2f); Sound.Play(Sfx.WaveClear, 0.7f, 0f); }
                else { Toast($"NEED {w.cost - Loadout.Chips} MORE CHIPS", 1.6f); Sound.Play(Sfx.UiMove, 0.5f, 0f); }
                return;
            }
            Loadout.Assign(loadoutFace, w.id);
            Sound.Play(Sfx.UiConfirm, 0.7f, 0f);
        }

        void Toast(string msg, float time) { loadoutMsg = msg; loadoutMsgTime = time; }

        static string GunIcon(WeaponDef w) => w.cost == 0 ? "ui_gun" + w.model : "ui_gun_" + w.id;

        static string Traits(WeaponDef w)
        {
            var t = new System.Collections.Generic.List<string>();
            if (w.antiAir) t.Add("HITS FLIERS");
            if (w.armorPiercing) t.Add("PIERCES ARMOUR");
            if (w.aoe > 0f) t.Add("AREA");
            if (w.homing) t.Add("HOMING");
            if (w.beam) t.Add("BEAM");
            return string.Join("  ·  ", t);
        }

        // Cube net (cross): face 1 in the middle, 2 above, 5 below it, 6 at the bottom, 3 left, 4 right.
        // Faces next to each other on the cube are next to each other here; opposite faces (sum 7) never touch.
        static readonly Vector2Int[] NetCell = { default, new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(2, 1), new Vector2Int(1, 2), new Vector2Int(1, 3) };

        void DrawLoadout(float w)
        {
            loadoutMsgTime -= Time.unscaledDeltaTime;
            Plate(w, 40f, $"CHIPS  {Num(Loadout.Chips)}", Palette.Hex("#FFB020"), "BUILD YOUR DIE", 44);
            var e = Event.current;

            // Left: the die net.
            const float cell = 168f, gap = 10f;
            float netW = 3 * cell + 2 * gap;
            float nx = w * 0.5f - 60f - netW - 40f, ny = 190f;
            for (int f = 1; f <= 6; f++)
            {
                var gun = WeaponDef.Find(Loadout.Faces[f]);
                var c = NetCell[f];
                var r = new Rect(nx + c.x * (cell + gap), ny + c.y * (cell + gap), cell, cell);
                bool sel = f == loadoutFace;
                bool opposite = f == 7 - loadoutFace;
                UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.95f), sel ? UiKit.Cyan : opposite ? new Color(1f, 0.42f, 0.24f, 0.8f) : UiKit.Steel);
                if (sel) UiKit.Glow(new Rect(r.x - 30, r.y - 30, r.width + 60, r.height + 60), new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, 0.14f));
                UiKit.DieFace(new Rect(r.x + 12, r.y + 12, 34, 34), f, gun.color, UiKit.Ink);
                UiKit.DrawIcon(new Rect(r.center.x - 30, r.y + 46, 60, 60), GunIcon(gun), gun.color);
                int ns = UiKit.TextWidth(gun.name, 18) > cell - 16 ? 15 : 18;
                UiKit.Line(gun.name, r.center.x, r.yMax - 46, ns, UiKit.Text, 0.5f, 2);
                if (opposite) UiKit.Line("OPPOSITE", r.center.x, r.yMax - 24, 13, new Color(1f, 0.55f, 0.35f), 0.5f, 2);
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { loadoutFace = f; Sound.Play(Sfx.UiMove, 0.5f, 0f); e.Use(); }
            }
            var note = new GUIStyle(UiKit.TextStyle(19, 0)) { wordWrap = true };
            UiKit.Label(new Rect(nx, ny + 4 * (cell + gap) + 6, netW, 90),
                "Opposite faces add up to 7. One roll moves the die to a neighbouring face; only a pipe rack (double roll) reaches the opposite face. Put guns you switch between often next to each other.",
                note, UiKit.Soft);

            // Right: the gun collection.
            float lx = w * 0.5f, ly = 190f, lw = Mathf.Min(760f, w - lx - 40f), rowH = 66f;
            UiKit.Line($"FACE {loadoutFace}: PICK A GUN", lx, ly - 34f, 18, UiKit.Mutedish, 0f, 2);
            for (int i = 0; i < WeaponDef.Catalog.Length; i++)
            {
                var gun = WeaponDef.Catalog[i];
                var r = new Rect(lx, ly + i * rowH, lw, rowH - 8f);
                bool owned = Loadout.Owns(gun.id);
                int onFace = System.Array.IndexOf(Loadout.Faces, gun.id);
                bool sel = loadoutRow == i;
                if (r.Contains(e.mousePosition) && e.type == EventType.MouseMove && loadoutRow != i) { loadoutRow = i; }
                UiKit.ChamferPanel(r, new Color(sel ? 0.06f : UiKit.Ink.r, sel ? 0.16f : UiKit.Ink.g, sel ? 0.25f : UiKit.Ink.b, 0.94f), sel ? UiKit.Cyan : new Color(UiKit.Steel.r, UiKit.Steel.g, UiKit.Steel.b, 0.6f));
                float a = owned ? 1f : 0.45f;
                UiKit.Rect(new Rect(r.x + 14, r.y + 8, 42, 42), new Color(gun.color.r * 0.3f, gun.color.g * 0.3f, gun.color.b * 0.3f, a));
                UiKit.DrawIcon(new Rect(r.x + 19, r.y + 13, 32, 32), GunIcon(gun), new Color(gun.color.r, gun.color.g, gun.color.b, a));
                UiKit.Line(gun.name, r.x + 70, r.y + 9, 21, new Color(1f, 1f, 1f, a), 0f, 2);
                UiKit.Line(Traits(gun), r.x + 70, r.y + 34, 14, new Color(UiKit.Mutedish.r, UiKit.Mutedish.g, UiKit.Mutedish.b, a), 0f, 2);
                string status = onFace > 0 ? $"FACE {onFace}" : owned ? (sel ? "ENTER: PUT ON FACE " + loadoutFace : "") : $"{gun.cost} CHIPS";
                Color sc = onFace > 0 ? UiKit.Cyan : owned ? UiKit.Mutedish : Loadout.Chips >= gun.cost ? Palette.Hex("#FFB020") : new Color(1f, 0.45f, 0.35f);
                UiKit.Line(status, r.xMax - 20, r.y + 19, 17, sc, 1f, 2);
                if (!owned) UiKit.Line(Loadout.Chips >= gun.cost ? "UNLOCK" : "LOCKED", r.xMax - 20, r.y + 38, 13, sc, 1f, 2);
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { loadoutRow = i; ActivateLoadoutRow(i); e.Use(); }
            }
            // Launch row.
            var lr = new Rect(lx, ly + WeaponDef.Catalog.Length * rowH + 8f, lw, 70f);
            bool lsel = loadoutRow == WeaponDef.Catalog.Length;
            UiKit.ChamferPanel(lr, lsel ? UiKit.Cyan : new Color(0.06f, 0.16f, 0.25f, 0.95f), UiKit.Cyan);
            UiKit.DrawIcon(new Rect(lr.x + 24, lr.center.y - 14, 28, 28), "ui_play", lsel ? UiKit.Ink : UiKit.Cyan);
            UiKit.Line("LAUNCH RUN", lr.x + 66, lr.center.y - 12, 28, lsel ? UiKit.Ink : UiKit.Text, 0f, 2);
            if (lr.Contains(e.mousePosition) && e.type == EventType.MouseMove) loadoutRow = WeaponDef.Catalog.Length;
            if (e.type == EventType.MouseDown && e.button == 0 && lr.Contains(e.mousePosition)) { e.Use(); ActivateLoadoutRow(WeaponDef.Catalog.Length); return; }

            if (loadoutMsgTime > 0f && loadoutMsg != null)
                UiKit.Line(loadoutMsg, w * 0.5f, lr.yMax + 14f, 22, Palette.Hex("#FFB020"), 0.5f, 2, 2f);

            float hy = UiKit.H - 52f, kx = 40f;
            kx += UiKit.Keycap(kx, hy, "←") + 4f; kx += UiKit.Keycap(kx, hy, "→") + 4f; kx += UiKit.Keycap(kx, hy, "1-6") + 8f;
            UiKit.Line("pick face", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1); kx += 100f;
            kx += UiKit.Keycap(kx, hy, "↑") + 4f; kx += UiKit.Keycap(kx, hy, "↓") + 8f;
            UiKit.Line("pick gun", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1); kx += 96f;
            kx += UiKit.Keycap(kx, hy, "ENTER") + 8f;
            UiKit.Line("assign / unlock / launch", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1); kx += 210f;
            kx += UiKit.Keycap(kx, hy, "ESC") + 8f;
            UiKit.Line("back", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1);
        }
    }
}
