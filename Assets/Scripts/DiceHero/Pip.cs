using UnityEngine;

namespace DiceHero
{
    public partial class DiceModel
    {
        /// <summary>Built as Pip-6 (the campaign character) rather than the plain die.</summary>
        public bool IsPip;
        /// <summary>Pip: where the active gun sits (on top of the eye-pod). Null for the plain die.</summary>
        public Transform GunMount;
        public Transform Head;
        public Transform Iris, Pupil, Glint, Happy;
        public Material DockMaterial;
        /// <summary>Pip: each face module's glow material (index = face number), dimmed during fights.</summary>
        public readonly Material[] FaceGlow = new Material[7];
        /// <summary>Pip: the module on each face (index = face number), rebuilt when a module is mounted.</summary>
        public readonly Transform[] FaceModule = new Transform[7];
        public Material IrisOn, IrisOff;
        public const float PipGunScale = 0.62f;
        public const float PipHeadHeight = 1.44f;
        public const float PipHeadScale = 1.25f;
    }

    /// <summary>
    /// Pip-6: a cube maintenance robot. Each face carries a socket with the module (gun) of that face, drawn in the
    /// gun's colour with its outline, and a row of pips for the face number. The eye-pod (modelled in Blender,
    /// Resources/Models/PipHead) floats above the top face, stays upright while the body rolls, and carries the
    /// active gun on its crown. Same hierarchy as the die: Root → Visual → Body (rolls) / WeaponMount (aims).
    /// </summary>
    public static class PipBuilder
    {
        public static DiceModel Build(Palette pal, Transform parent, Vector3 position)
        {
            var m = new DiceModel { IsPip = true };
            m.Root = new GameObject("Pip").transform;
            m.Root.SetParent(parent, false);
            m.Root.position = position;
            m.Visual = new GameObject("Visual").transform;
            m.Visual.SetParent(m.Root, false);

            var body = Prim.MeshObject("Body", m.Visual, MeshFactory.RoundedCube(0.1f, 3), pal.Get("PipBody", Palette.Hex("#13304A"), 0.72f, 0.5f));
            m.Body = body.transform;
            m.Body.localPosition = new Vector3(0f, 0.5f, 0f);

            var number = pal.Glow("PipNumber", Palette.Hex("#DCEBF5"), 1.2f);
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = DiceModel.FaceNormals[f];
                int face = DiceModel.FaceNumbers[f];
                // Face frame: local +Y out of the face, local +Z toward the face's "top" edge.
                var F = new GameObject("Face" + face).transform;
                F.SetParent(m.Body, false);
                F.localPosition = n * 0.5f;
                F.localRotation = Quaternion.LookRotation(Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up, n);
                for (int i = 0; i < face; i++)
                    Prim.Make(PrimitiveType.Cylinder, "Pip", F, new Vector3((i - (face - 1) * 0.5f) * 0.06f, 0.002f, -0.335f), new Vector3(0.044f, 0.006f, 0.044f), number);
                BuildModule(pal, m, F, face);
            }

            // Seams along the 12 edges: one material of Pip's own, recoloured to the top gun (PipFace).
            m.SeamMaterial = new Material(pal.Glow("PipSeam", Palette.Hex("#29B6F6"), 1.8f)) { name = "PipSeamLive" };
            const float e = 0.4707f;
            for (int axis = 0; axis < 3; axis++)
            for (int s1 = -1; s1 <= 1; s1 += 2)
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                Vector3 pos = axis == 0 ? new Vector3(0f, s1 * e, s2 * e) : axis == 1 ? new Vector3(s1 * e, 0f, s2 * e) : new Vector3(s1 * e, s2 * e, 0f);
                const float t = 0.03f;
                Vector3 sz = axis == 0 ? new Vector3(0.78f, t, t) : axis == 1 ? new Vector3(t, 0.78f, t) : new Vector3(t, t, 0.78f);
                Prim.Make(PrimitiveType.Cube, "Seam", m.Body, pos, sz, m.SeamMaterial);
            }

            // Mag-lev dock ring between the body and the eye-pod, in the top gun's colour.
            m.DockMaterial = new Material(pal.Glow("PipDock", Palette.Hex("#29B6F6"), 2f)) { name = "PipDockLive" };
            Prim.Make(PrimitiveType.Cylinder, "Dock", m.Visual, new Vector3(0f, 1.03f, 0f), new Vector3(0.3f, 0.006f, 0.3f), m.DockMaterial);

            m.WeaponMount = new GameObject("WeaponMount").transform; // aims; carries the eye-pod and the gun
            m.WeaponMount.SetParent(m.Visual, false);
            m.WeaponMount.localPosition = new Vector3(0f, DiceModel.PipHeadHeight, 0f);
            m.Head = BuildHead(pal, m);

            m.GunMount = new GameObject("GunMount").transform;
            m.GunMount.SetParent(m.WeaponMount, false);
            m.GunMount.localPosition = new Vector3(0f, 0.33f, -0.02f);
            m.GunMount.localScale = Vector3.one * DiceModel.PipGunScale;
            return m;
        }

        /// <summary>The socket and module on one face, in that face's gun colour (grey with a cross when empty).</summary>
        static void BuildModule(Palette pal, DiceModel m, Transform F, int face)
        {
            var gun = WeaponDef.All[face];
            // Pip's own copy, so dimming it in fights leaves the floor markers and menus alone.
            var glow = new Material(pal.Glow("PipGun" + gun.id, gun.color, gun.IsEmpty ? 0.5f : 1.7f)) { name = "PipGunLive" + face };
            m.FaceGlow[face] = glow;
            var mod = new GameObject("Module").transform;
            mod.SetParent(F, false);
            m.FaceModule[face] = mod;
            Prim.Make(PrimitiveType.Cube, "Socket", mod, new Vector3(0f, 0.008f, 0.035f), new Vector3(0.56f, 0.03f, 0.56f),
                pal.Get("PipSocket" + gun.id, gun.color * 0.2f + Color.black * 0.8f, 0.6f, 0.3f));
            Ring(mod, new Vector3(0f, 0.02f, 0.035f), 0.3f, 0.028f, glow);
            var g = new GameObject("Glyph").transform;
            g.SetParent(mod, false);
            g.localPosition = new Vector3(0f, 0.03f, 0.035f);
            g.localScale = Vector3.one * 0.5f;
            Glyphs.Build(gun.model, g, glow);
        }

        /// <summary>A module was mounted on this face during play: rebuild its socket.</summary>
        public static void RebuildFace(Palette pal, DiceModel m, int face)
        {
            if (m.FaceModule[face] == null) return;
            var F = m.FaceModule[face].parent;
            Object.Destroy(m.FaceModule[face].gameObject);
            BuildModule(pal, m, F, face);
        }

        static void Ring(Transform parent, Vector3 c, float outer, float width, Material mat)
        {
            float mid = outer - width * 0.5f;
            Prim.Make(PrimitiveType.Cube, "Rim", parent, c + new Vector3(0f, 0f, mid), new Vector3(outer * 2f, 0.03f, width), mat);
            Prim.Make(PrimitiveType.Cube, "Rim", parent, c + new Vector3(0f, 0f, -mid), new Vector3(outer * 2f, 0.03f, width), mat);
            Prim.Make(PrimitiveType.Cube, "Rim", parent, c + new Vector3(mid, 0f, 0f), new Vector3(width, 0.03f, outer * 2f - width * 2f), mat);
            Prim.Make(PrimitiveType.Cube, "Rim", parent, c + new Vector3(-mid, 0f, 0f), new Vector3(width, 0.03f, outer * 2f - width * 2f), mat);
        }

        static Transform BuildHead(Palette pal, DiceModel m)
        {
            var prefab = Resources.Load<GameObject>("Models/PipHead");
            var holder = new GameObject("EyePod").transform;
            holder.SetParent(m.WeaponMount, false);
            holder.localScale = Vector3.one * DiceModel.PipHeadScale;
            if (prefab == null) { Debug.LogWarning("[RollPower] Models/PipHead missing"); return holder; }
            var head = Object.Instantiate(prefab, holder, false).transform;
            head.name = "PipHead";
            // Turn and scale the pod using the axis markers exported with it (1 m ahead and 1 m up in Blender).
            Transform fwd = Find(head, "AxisFwd"), up = Find(head, "AxisUp");
            if (fwd != null && up != null)
            {
                Vector3 f = holder.InverseTransformPoint(fwd.position) - holder.InverseTransformPoint(head.position);
                Vector3 u = holder.InverseTransformPoint(up.position) - holder.InverseTransformPoint(head.position);
                head.localRotation = Quaternion.Inverse(Quaternion.LookRotation(f, u)) * head.localRotation;
                head.localScale *= 1f / Mathf.Max(0.0001f, f.magnitude);
            }
            var shell = pal.Get("PodShell", Palette.Hex("#C3D2DD"), 0.75f, 0.35f);
            var band = pal.Get("PodBand", Palette.Hex("#1A2C3C"), 0.65f, 0.6f);
            var ink = pal.Get("PodInk", Palette.Hex("#08131F"), 0.4f, 0.2f);
            var iris = pal.Glow("PodIris", Palette.Hex("#29B6F6"), 2.6f);
            m.IrisOn = iris;
            m.IrisOff = pal.Glow("PodIrisOff", Palette.Hex("#33485A"), 0.4f);
            var glint = pal.Glow("PodGlint", Color.white, 2.4f);
            foreach (var r in head.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                r.sharedMaterial = n.StartsWith("Shell") ? shell
                    : n.StartsWith("Band") || n.StartsWith("Cheek") || n.StartsWith("EyeRim") ? band
                    : n.StartsWith("EyeSocket") || n.StartsWith("Pupil") ? ink
                    : n.StartsWith("Iris") || n.StartsWith("Happy") ? iris
                    : n.StartsWith("Glint") ? glint : band;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (n.StartsWith("Iris")) m.Iris = r.transform;
                else if (n.StartsWith("Pupil")) m.Pupil = r.transform;
                else if (n.StartsWith("Glint")) m.Glint = r.transform;
                else if (n.StartsWith("Happy")) m.Happy = r.transform;
            }
            if (m.Happy != null) m.Happy.gameObject.SetActive(false);
            return holder;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name.StartsWith(name)) return t;
            foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
            return null;
        }
    }

    /// <summary>
    /// Pip's live look: the seams and dock ring take the top gun's colour (white while shielded), and the eye blinks,
    /// glances toward the advised roll, squints when hit and smiles between waves.
    /// In fights the side modules dim: rolling toward a side brings up the face opposite it, so a bright module next to
    /// a floor marker would point the wrong way. The floor markers carry that information; menus show the modules bright.
    /// </summary>
    public class PipFace
    {
        readonly DiceModel m;
        readonly Vector3 irisPos, pupilPos, glintPos, irisScale, pupilScale;
        Color seam;
        float t, nextBlink = 2.5f, blink, happy, faceLight = 1f;

        public PipFace(DiceModel model)
        {
            m = model;
            if (m.Iris != null) { irisPos = m.Iris.localPosition; irisScale = m.Iris.localScale; }
            if (m.Pupil != null) { pupilPos = m.Pupil.localPosition; pupilScale = m.Pupil.localScale; }
            if (m.Glint != null) glintPos = m.Glint.localPosition;
            seam = Palette.Hex("#29B6F6");
        }

        public void Step(float dt, DiceController dice, Game game, RollPlan plan)
        {
            t += dt;
            Color target = WeaponDef.All[dice.TopNumber].color;
            seam = Color.Lerp(seam, target, 1f - Mathf.Exp(-10f * dt));
            Color s = dice.Shielded ? Color.white : seam;
            m.SeamMaterial.SetColor("_EmissionColor", s * 1.8f * Palette.GlowScale);
            m.SeamMaterial.SetColor("_BaseColor", s * 0.3f);
            m.DockMaterial.SetColor("_EmissionColor", seam * 0.9f * Palette.GlowScale);
            faceLight = Mathf.MoveTowards(faceLight, game == null ? 1f : 0.22f, dt * 3f);
            for (int f = 1; f <= 6; f++)
                if (m.FaceGlow[f] != null) m.FaceGlow[f].SetColor("_EmissionColor", WeaponDef.All[f].color * (WeaponDef.All[f].IsEmpty ? 0.5f : 1.7f) * faceLight * Palette.GlowScale);
            // The gun aims exactly (WeaponMount); the eye-pod turns only part of the way when the aim points away
            // from the camera, so the player keeps seeing Pip's eye.
            Vector3 aim = m.WeaponMount.forward; aim.y = 0f;
            if (aim.sqrMagnitude > 0.001f)
            {
                Vector3 face = aim.normalized + Vector3.back * Mathf.Max(0f, aim.normalized.z) * 1.1f;
                if (face.sqrMagnitude < 0.01f) face = Vector3.back;
                m.Head.rotation = Quaternion.Slerp(m.Head.rotation, Quaternion.LookRotation(face.normalized, Vector3.up), 1f - Mathf.Exp(-12f * dt));
            }
            if (m.Iris == null) return;

            // Expressions.
            bool hurt = game != null && game.HurtFlash > 0f;
            happy = game != null && game.Intermission && !game.Lost ? happy + dt : 0f;
            nextBlink -= dt;
            if (nextBlink <= 0f) { blink = 0.13f; nextBlink = Random.Range(2.5f, 5f); }
            blink -= dt;
            float open = hurt ? 0.25f : blink > 0f ? 0.12f : 1f;
            bool smile = happy > 0.3f && !hurt;
            // Offline: no module under the eye-pod, so the lens goes dim.
            var irisR = m.Iris.GetComponent<Renderer>();
            var lens = WeaponDef.All[dice.TopNumber].IsEmpty ? m.IrisOff : m.IrisOn;
            if (irisR.sharedMaterial != lens) irisR.sharedMaterial = lens;
            m.Happy.gameObject.SetActive(smile);
            m.Iris.gameObject.SetActive(!smile);
            m.Pupil.gameObject.SetActive(!smile);
            m.Glint.gameObject.SetActive(!smile && open > 0.5f);

            // Glance: shift the iris toward the advised roll direction, as seen from the pod.
            Vector3 look = Vector3.zero;
            if (plan != null)
            {
                var head = m.Head;
                float x = Vector3.Dot(plan.dir, head.right), z = Vector3.Dot(plan.dir, head.forward);
                look = head.right * x * 0.045f + head.up * (z < -0.5f ? -0.03f : 0.01f);
            }
            Squash(m.Iris, irisScale, open);
            Squash(m.Pupil, pupilScale, open);
            Offset(m.Iris, irisPos, look);
            Offset(m.Pupil, pupilPos, look * 1.2f);
            Offset(m.Glint, glintPos, look);
        }

        /// <summary>Scales a part along whichever of its local axes points up in the pod (the blink axis).</summary>
        void Squash(Transform part, Vector3 baseScale, float k)
        {
            Vector3 up = m.Head.up;
            float ax = Mathf.Abs(Vector3.Dot(part.TransformDirection(Vector3.right), up));
            float ay = Mathf.Abs(Vector3.Dot(part.TransformDirection(Vector3.up), up));
            float az = Mathf.Abs(Vector3.Dot(part.TransformDirection(Vector3.forward), up));
            Vector3 sc = baseScale;
            if (ax >= ay && ax >= az) sc.x *= k; else if (ay >= az) sc.y *= k; else sc.z *= k;
            part.localScale = sc;
        }

        static void Offset(Transform part, Vector3 basePos, Vector3 worldOffset)
        {
            part.localPosition = basePos + (part.parent != null ? part.parent.InverseTransformVector(worldOffset) : worldOffset);
        }
    }
}
