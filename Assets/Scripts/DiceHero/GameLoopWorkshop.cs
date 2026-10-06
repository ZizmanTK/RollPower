using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// The Workshop (campaign plan, section 07), opened from the station map: put Pip's modules on the faces you want
    /// (including hidden ones found in caches), and spend chips on two small permanent boosts. Capped, so skill still decides.
    /// </summary>
    public partial class GameLoop
    {
        int wsFace = 1, wsRow;
        List<string> wsModules = new List<string>();

        static readonly (string id, string name, string desc, int cost, int max)[] Boosts =
        {
            ("hull", "PLATING", "+1 hull in every campaign stage", 100, 2),
            ("roll", "GYRO", "Roll recharges 10% faster", 120, 2),
        };

        int WsRows => wsModules.Count + Boosts.Length + 1; // modules, boosts, BACK

        void EnterWorkshop()
        {
            State = Screen2.Workshop;
            wsModules = Campaign.OwnedModules();
            wsFace = 1; wsRow = 0;
        }

        void WorkshopInput(float udt)
        {
            if (Controls.Back) { EnterMap(); Sound.Play(Sfx.UiConfirm, 0.6f, 0f); return; }
            var nav = Controls.Nav(udt);
            if (nav.x != 0) { wsFace = (wsFace - 1 + nav.x + 6) % 6 + 1; Sound.Play(Sfx.UiMove, 0.5f, 0f); }
            if (nav.y != 0) { wsRow = (wsRow - nav.y + WsRows) % WsRows; Sound.Play(Sfx.UiMove, 0.5f, 0f); }
            int key = Controls.NumberKey();
            if (key >= 1 && key <= 6) { wsFace = key; Sound.Play(Sfx.UiMove, 0.5f, 0f); }
            if (Controls.Confirm) WorkshopActivate(wsRow);
        }

        void WorkshopActivate(int row)
        {
            if (row < wsModules.Count)
            {
                Campaign.Arrange(wsFace, wsModules[row]);
                Sound.Play(Sfx.UiConfirm, 0.7f, 0f);
                Toast($"{WeaponDef.Find(wsModules[row]).name} ON FACE {wsFace}", 1.6f);
                return;
            }
            int b = row - wsModules.Count;
            if (b < Boosts.Length)
            {
                var bo = Boosts[b];
                int lvl = Campaign.Boost(bo.id);
                if (lvl >= bo.max) { Toast("ALREADY AT MAX", 1.4f); Sound.Play(Sfx.UiMove, 0.5f, 0f); return; }
                if (Loadout.Chips < bo.cost) { Toast($"NEED {bo.cost - Loadout.Chips} MORE CHIPS", 1.6f); Sound.Play(Sfx.UiMove, 0.5f, 0f); return; }
                Loadout.Chips -= bo.cost;
                Campaign.SetBoost(bo.id, lvl + 1);
                Toast($"{bo.name} {lvl + 1}/{bo.max}", 1.6f);
                Sound.Play(Sfx.Upgrade, 0.8f, 0f);
                return;
            }
            EnterMap();
        }

        void DrawWorkshop(float w)
        {
            loadoutMsgTime -= Time.unscaledDeltaTime;
            Plate(w, 40f, $"CHIPS  {Num(Loadout.Chips)}", Palette.Hex("#FFB020"), "WORKSHOP", 44);
            var e = Event.current;
            var faces = Campaign.Faces;
            // Left: the die net with Pip's modules.
            const float cell = 150f, gap = 10f;
            float netW = 3 * cell + 2 * gap;
            float nx = w * 0.5f - 60f - netW - 40f, ny = 190f;
            for (int f = 1; f <= 6; f++)
            {
                var gun = WeaponDef.Find(faces[f]);
                var c = NetCell[f];
                var r = new Rect(nx + c.x * (cell + gap), ny + c.y * (cell + gap), cell, cell);
                bool sel = f == wsFace, opposite = f == 7 - wsFace;
                UiKit.ChamferPanel(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.95f), sel ? UiKit.Cyan : opposite ? new Color(1f, 0.42f, 0.24f, 0.8f) : UiKit.Steel);
                UiKit.DieFace(new Rect(r.x + 10, r.y + 10, 30, 30), f, gun.color, UiKit.Ink);
                UiKit.DrawIcon(new Rect(r.center.x - 26, r.y + 40, 52, 52), GunIcon(gun), gun.color);
                UiKit.Line(gun.name, r.center.x, r.yMax - 40, UiKit.TextWidth(gun.name, 17) > cell - 14 ? 14 : 17, UiKit.Text, 0.5f, 2);
                if (opposite) UiKit.Line("OPPOSITE", r.center.x, r.yMax - 20, 12, new Color(1f, 0.55f, 0.35f), 0.5f, 2);
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { wsFace = f; e.Use(); }
            }
            var note = new GUIStyle(UiKit.TextStyle(18, 0)) { wordWrap = true };
            UiKit.Label(new Rect(nx, ny + 4 * (cell + gap) + 4, netW, 90),
                "A roll reaches the four neighbouring faces; the opposite face takes two rolls or a vault. Keep the guns you switch between often next to each other.", note, UiKit.Soft);

            // Right: modules, then boosts, then BACK.
            float lx = w * 0.5f, ly = 190f, lw = Mathf.Min(760f, w - lx - 40f), rowH = 60f;
            UiKit.Line($"FACE {wsFace}: PICK A MODULE", lx, ly - 34f, 18, UiKit.Mutedish, 0f, 2);
            int row = 0;
            for (int i = 0; i < wsModules.Count; i++, row++)
            {
                var gun = WeaponDef.Find(wsModules[i]);
                var r = new Rect(lx, ly + row * rowH, lw, rowH - 8f);
                int mi = row;
                Row(r, mi, () => WorkshopActivate(mi));
                UiKit.DrawIcon(new Rect(r.x + 18, r.y + 11, 30, 30), GunIcon(gun), gun.color);
                UiKit.Line(gun.name, r.x + 64, r.y + 8, 20, UiKit.Text, 0f, 2);
                UiKit.Line(Traits(gun), r.x + 64, r.y + 31, 13, UiKit.Mutedish, 0f, 2);
                int onFace = System.Array.IndexOf(faces, gun.id);
                UiKit.Line(onFace > 0 ? $"FACE {onFace}" : "SPARE", r.xMax - 20, r.y + 16, 17, onFace > 0 ? UiKit.Cyan : Palette.Hex("#FFB020"), 1f, 2);
            }
            float by = ly + row * rowH + 16f;
            UiKit.Line("BOOSTS (CHIPS)", lx, by - 6f, 16, UiKit.Mutedish, 0f, 2);
            for (int b = 0; b < Boosts.Length; b++, row++)
            {
                var bo = Boosts[b];
                var r = new Rect(lx, by + 18f + b * rowH, lw, rowH - 8f);
                int bi = row;
                Row(r, bi, () => WorkshopActivate(bi));
                int lvl = Campaign.Boost(bo.id);
                UiKit.Line(bo.name + $"  {lvl}/{bo.max}", r.x + 24, r.y + 8, 20, UiKit.Text, 0f, 2);
                UiKit.Line(bo.desc, r.x + 24, r.y + 31, 13, UiKit.Mutedish, 0f, 2);
                UiKit.Line(lvl >= bo.max ? "MAX" : $"{bo.cost} CHIPS", r.xMax - 20, r.y + 16, 17, lvl >= bo.max ? UiKit.Cyan : Loadout.Chips >= bo.cost ? Palette.Hex("#FFB020") : new Color(1f, 0.45f, 0.35f), 1f, 2);
            }
            var back = new Rect(lx, by + 18f + Boosts.Length * rowH + 8f, lw, 56f);
            int backRow = row;
            Row(back, backRow, () => WorkshopActivate(backRow));
            UiKit.DrawIcon(new Rect(back.x + 22, back.center.y - 12, 24, 24), "ui_back", UiKit.Text);
            UiKit.Line("BACK TO THE MAP", back.x + 58, back.center.y - 11, 22, UiKit.Text, 0f, 2);
            if (loadoutMsgTime > 0f && loadoutMsg != null) UiKit.Line(loadoutMsg, w * 0.5f, UiKit.H - 110f, 22, Palette.Hex("#FFB020"), 0.5f, 2, 2f);

            float hy = UiKit.H - 52f, kx = 40f;
            kx += UiKit.Keycap(kx, hy, "←") + 4f; kx += UiKit.Keycap(kx, hy, "→") + 4f; kx += UiKit.Keycap(kx, hy, "1-6") + 8f;
            UiKit.Line("face", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1); kx += 60f;
            kx += UiKit.Keycap(kx, hy, "↑") + 4f; kx += UiKit.Keycap(kx, hy, "↓") + 8f;
            UiKit.Line("row", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1); kx += 56f;
            kx += UiKit.Keycap(kx, hy, "ENTER") + 8f;
            UiKit.Line("mount / buy", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1); kx += 120f;
            kx += UiKit.Keycap(kx, hy, "ESC") + 8f;
            UiKit.Line("back", kx, hy + 7f, 18, UiKit.Mutedish, 0f, 1);
        }

        void Row(Rect r, int index, System.Action click)
        {
            var e = Event.current;
            bool sel = wsRow == index;
            if (r.Contains(e.mousePosition) && e.type == EventType.MouseMove) wsRow = index;
            UiKit.ChamferPanel(r, new Color(sel ? 0.06f : UiKit.Ink.r, sel ? 0.16f : UiKit.Ink.g, sel ? 0.25f : UiKit.Ink.b, 0.94f), sel ? UiKit.Cyan : new Color(UiKit.Steel.r, UiKit.Steel.g, UiKit.Steel.b, 0.6f));
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { wsRow = index; e.Use(); click(); }
        }
    }
}
