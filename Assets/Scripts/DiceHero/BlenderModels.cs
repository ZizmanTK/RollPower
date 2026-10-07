using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Models made in Blender (Tools/Blender/roster.py, exported to Resources/Models): enemies, coats, guns and
    /// obstacles. Every part is named "Key_Part": the key picks the game material, so colours stay defined here.
    /// Each FBX carries AxisFwd/AxisUp markers (1 m ahead and up in Blender) used to turn and scale it, whatever the
    /// FBX axis conversion did. When a model is missing, callers fall back to their primitive builds.
    /// </summary>
    public static class BlenderModels
    {
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        /// <summary>Instantiates Models/path under parent (normalised: +Z forward, +Y up, metres), or returns null.</summary>
        public static Transform Spawn(string path, Transform parent, Palette pal, Color? emit = null, string emitId = "")
        {
            if (!prefabs.TryGetValue(path, out var prefab)) { prefab = Resources.Load<GameObject>("Models/" + path); prefabs[path] = prefab; }
            if (prefab == null) return null;
            var holder = new GameObject(System.IO.Path.GetFileName(path)).transform;
            holder.SetParent(parent, false);
            var m = Object.Instantiate(prefab, holder, false).transform;
            Transform fwd = Find(m, "AxisFwd"), up = Find(m, "AxisUp");
            if (fwd != null && up != null)
            {
                Vector3 f = holder.InverseTransformPoint(fwd.position) - holder.InverseTransformPoint(m.position);
                Vector3 u = holder.InverseTransformPoint(up.position) - holder.InverseTransformPoint(m.position);
                m.localRotation = Quaternion.Inverse(Quaternion.LookRotation(f, u)) * m.localRotation;
                m.localScale *= 1f / Mathf.Max(0.0001f, f.magnitude);
            }
            foreach (var r in m.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                int cut = n.IndexOf('_');
                r.sharedMaterial = Mat(pal, cut > 0 ? n.Substring(0, cut) : n, emit, emitId);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return holder;
        }

        /// <summary>
        /// A moving piece (an empty named name and its parts): rebuilt as a clean pivot under 'under', at the same
        /// place but with under's axes, so game code can turn, hide or scale it like the primitive builds' pivots.
        /// </summary>
        public static Transform Pivot(Transform holder, string name, Transform under)
        {
            var src = Find(holder, name);
            if (src == null) return new GameObject(name).transform;
            var p = new GameObject(name).transform;
            p.SetParent(under, false);
            p.position = src.position;
            p.rotation = under.rotation;
            var kids = new List<Transform>();
            foreach (Transform c in src) kids.Add(c);
            foreach (var c in kids) c.SetParent(p, true);
            return p;
        }

        /// <summary>The empties named "Muzzle_n", in order.</summary>
        public static void Muzzles(Transform root, List<Transform> into)
        {
            var found = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Muzzle_")) found.Add(t);
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            into.AddRange(found);
        }

        /// <summary>Spins every part whose name starts with prefix about the holder's up axis (drone rotors).</summary>
        public static void Spin(Transform holder, string prefix, float degPerSec)
        {
            foreach (var t in holder.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(prefix)) t.gameObject.AddComponent<Spinner>().Set(holder, degPerSec);
        }

        static Material Mat(Palette pal, string key, Color? emit, string emitId)
        {
            switch (key)
            {
                case "Shell": return pal.Get("BMShell", Palette.Hex("#D8263A"), 0.4f, 0.05f);
                case "Silver": return pal.Get("BMSilver", Palette.Hex("#C9D1DB"), 0.45f, 0.15f);
                case "Metal": return pal.Get("BMMetal", Palette.Hex("#2A3039"), 0.55f, 0.5f);
                case "Eye": return pal.Glow("BMEye", Palette.Hex("#FF2A3D"), 2.6f, Palette.Hex("#FF2A3D"));
                case "Glow": return pal.Glow("BMGlow", Palette.Hex("#FF3B4E"), 1.3f, Palette.Hex("#5A0D16"));
                case "Steel": return pal.Get("BMSteel", Palette.Hex("#8A939E"), 0.55f, 0.85f);
                case "Rivet": return pal.Get("BMRivet", Palette.Hex("#D0D6DC"), 0.6f, 0.9f);
                case "Tread": return pal.Get("BMTread", Palette.Hex("#15171B"), 0.5f, 0.6f);
                case "Hazard": return pal.Get("BMHazard", Palette.Hex("#F2B21E"), 0.5f, 0.2f);
                case "Pcb": return pal.Get("BMPcb", Palette.Hex("#1F5E3B"), 0.5f, 0f);
                case "Trace": return pal.Glow("BMTrace", Palette.Hex("#E8B84A"), 0.9f);
                case "Chip": return pal.Get("BMChip", Palette.Hex("#101318"), 0.6f, 0.2f);
                case "Bomb": return pal.Get("BombBody", Palette.Hex("#4A505C"), 0.55f, 0.75f);
                case "BombGlow": return pal.Glow("BombRed", Palette.Hex("#FF2A3D"), 4f);
                case "Vine": return pal.Get("CoatVine", Palette.Hex("#2E6B33"), 0.3f, 0f);
                case "Leaf": return pal.Get("CoatLeaf", Palette.Hex("#4FA34A"), 0.35f, 0f);
                case "Ice": return pal.Get("CoatIce", Palette.Hex("#DDF4FF"), 0.95f, 0.1f);
                case "IceGlow": return pal.Glow("CoatIceGlow", Palette.Hex("#9FE3FF"), 0.7f);
                case "Shield": return pal.Glow("CoatShield", Palette.Hex("#49C8FF"), 2.4f);
                case "Panel": return pal.Get("NGPanel", Palette.Hex("#E4EAF0"), 0.6f, 0.05f);
                case "Inset": return pal.Get("NGInset", Palette.Hex("#2A3442"), 0.5f, 0.2f);
                case "Gun": return pal.Get("NGSteel", Palette.Hex("#6A7686"), 0.55f, 0.4f);
                case "Fuel": return pal.Get("BMFuel", Palette.Hex("#FF7A2A"), 0.45f, 0.1f);
                case "Copper": return pal.Get("BMCopper", Palette.Hex("#C7642E"), 0.7f, 0.9f);
                case "Dish": return pal.Get("BMDish", Palette.Hex("#C9D1DB"), 0.8f, 0.8f);
                case "Slug": return pal.Glow("BMSlug", WeaponDef.PierceColor, 1.2f);
                case "Flame": return pal.Glow("BMFlame", Palette.Hex("#FFB04A"), 3f);
                case "Emit": return pal.Glow("BMEmit" + emitId, emit ?? Color.white, 1.6f);
                // Obstacles
                case "Rust": return pal.Get("BMRust", Palette.Hex("#8A4A28"), 0.35f, 0.5f);
                case "Teal": return pal.Get("BMTeal", Palette.Hex("#3F6B70"), 0.35f, 0.3f);
                case "Planter": return pal.Get("BMPlanter", Palette.Hex("#3A4B52"), 0.5f, 0.4f);
                case "Glass": return pal.Get("BMGlass", Palette.Hex("#A9D8CE"), 0.95f, 0.1f);
                case "Water": return pal.Glow("BMWater", Palette.Hex("#3FBF9A"), 0.7f, Palette.Hex("#1E4A3C"));
                case "PipeWhite": return pal.Get("BMPipeWhite", Palette.Hex("#D9E3E6"), 0.6f, 0.3f);
                case "Ore": return pal.Get("BMOre", Palette.Hex("#3A4F63"), 0.4f, 0.5f);
                case "Ingot": return pal.Get("BMIngot", Palette.Hex("#7A828C"), 0.6f, 0.9f);
                case "Hot": return pal.Glow("BMHot", Palette.Hex("#FF6A1A"), 1.8f);
                case "Iron": return pal.Get("BMIron", Palette.Hex("#2A2624"), 0.4f, 0.8f);
                case "Slag": return pal.Glow("BMSlag", Palette.Hex("#E8481A"), 1.3f, Palette.Hex("#5A1A08"));
                case "Rack": return pal.Get("BMRack", Palette.Hex("#141B24"), 0.45f, 0.3f);
                case "Led": return pal.Glow("BMLed", Palette.Hex("#29E0FF"), 2.4f);
                case "Tray": return pal.Get("BMTray", Palette.Hex("#3A4554"), 0.5f, 0.6f);
                case "CableA": return pal.Get("BMCableA", Palette.Hex("#C8323C"), 0.4f, 0f);
                case "CableB": return pal.Get("BMCableB", Palette.Hex("#2F5FBF"), 0.4f, 0f);
                case "CableY": return pal.Get("BMCableY", Palette.Hex("#E0B030"), 0.4f, 0f);
                default: return pal.Get("BMMetal", Palette.Hex("#2A3039"), 0.55f, 0.5f);
            }
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name.StartsWith(name)) return t;
            foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
            return null;
        }
    }
}
