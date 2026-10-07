using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Procedural animation for the Blender robots, driven by how fast they actually move:
    ///   legged robots (crawler, mite) walk: diagonal legs swing and lift in turn, the body bobs;
    ///   wheels (bomber, Compactor) roll with the ground speed; the drone tilts into its flight;
    ///   the tank's turret sways and its hull rocks; the Driller's bit spins while it's up;
    ///   the Gardener's vines sway; the Smelter stomps.
    /// Pivots come from the models' "Pivot_*" empties (BlenderModels.Pivot), so they share the robot's axes.
    /// </summary>
    public class EnemyRig : MonoBehaviour
    {
        Transform body;                      // the model holder (bob, tilt)
        Vector3 bodyPos;
        readonly List<Transform> legs = new List<Transform>();
        readonly List<Vector3> legDir = new List<Vector3>();
        readonly List<Quaternion> legRest = new List<Quaternion>();
        readonly List<Transform> wheels = new List<Transform>();
        readonly List<Transform> sway = new List<Transform>();
        Transform turret, bit;
        EnemyKind kind;
        Vector3 last;
        float phase, speed, fwd, side, roll, legLift, legSwing, gaitRate, seed;

        /// <summary>Builds the rig from the pivots in a freshly spawned model.</summary>
        public static void Attach(Transform root, Transform holder, EnemyKind kind, Transform machine = null)
        {
            var rig = root.gameObject.AddComponent<EnemyRig>();
            rig.kind = kind; rig.body = holder; rig.bodyPos = holder.localPosition; rig.last = root.position; rig.seed = Random.value * 10f; rig.phase = Random.value * 6f;
            bool walker = kind == EnemyKind.Crawler || kind == EnemyKind.Mite || kind == EnemyKind.Smelter;
            rig.legSwing = kind == EnemyKind.Mite ? 28f : 22f;
            rig.legLift = kind == EnemyKind.Mite ? 12f : 18f;
            rig.gaitRate = kind == EnemyKind.Mite ? 9f : kind == EnemyKind.Smelter ? 3.2f : 5.5f;
            foreach (var name in Names(holder))
            {
                if (walker && name.StartsWith("Pivot_Leg"))
                {
                    var p = BlenderModels.Pivot(holder, name, root);
                    rig.legs.Add(p); rig.legRest.Add(p.localRotation);
                    Vector3 d = p.localPosition; d.y = 0f;
                    rig.legDir.Add(d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.right);
                }
                else if (name.StartsWith("Pivot_Wheel")) rig.wheels.Add(BlenderModels.Pivot(holder, name, root));
                else if (name.StartsWith("Pivot_Tendril")) rig.sway.Add(BlenderModels.Pivot(holder, name, root));
                else if (name == "Pivot_Turret") rig.turret = BlenderModels.Pivot(holder, name, root);
            }
            if (machine != null) rig.bit = BlenderModels.Pivot(machine, "Pivot_Bit", machine);
        }

        static List<string> Names(Transform t)
        {
            var list = new List<string>();
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name.StartsWith("Pivot_") && !list.Contains(c.name)) list.Add(c.name);
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        void LateUpdate() => Tick(Time.deltaTime);

        /// <summary>Legs walked so far (tests).</summary>
        public int LegCount => legs.Count;
        public Quaternion LegRotation(int i) => legs[i].localRotation;
        public Quaternion LegRest(int i) => legRest[i];

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            Vector3 v = (transform.position - last) / dt; last = transform.position;
            v.y = 0f;
            if (v.magnitude > 30f) v = Vector3.zero; // teleports (spawns, Gardener moving)
            Vector3 local = transform.InverseTransformDirection(v);
            speed = Mathf.Lerp(speed, v.magnitude, 1f - Mathf.Exp(-10f * dt));
            fwd = Mathf.Lerp(fwd, local.z, 1f - Mathf.Exp(-6f * dt));
            side = Mathf.Lerp(side, local.x, 1f - Mathf.Exp(-6f * dt));
            float amp = Mathf.Clamp01(speed / 1.4f);
            phase += dt * gaitRate * (0.25f + amp * 1.6f);

            // Walk cycle: legs in two alternating groups swing about the vertical and lift as they swing forward.
            for (int i = 0; i < legs.Count; i++)
            {
                float ph = phase + (i % 2 == 0 ? 0f : Mathf.PI);
                float swing = Mathf.Sin(ph) * legSwing * amp, lift = Mathf.Max(0f, Mathf.Cos(ph)) * legLift * amp;
                if (kind == EnemyKind.Smelter)
                {
                    legs[i].localPosition = new Vector3(legs[i].localPosition.x, 0.3f + lift * 0.006f, Mathf.Sin(ph) * 0.12f * amp);
                    continue;
                }
                Vector3 liftAxis = Vector3.Cross(legDir[i], Vector3.up);
                legs[i].localRotation = Quaternion.AngleAxis(swing, Vector3.up) * Quaternion.AngleAxis(-lift, liftAxis) * legRest[i];
            }
            if (body != null)
            {
                float bob = legs.Count > 0 ? Mathf.Abs(Mathf.Sin(phase * 2f)) * 0.025f * amp : 0f;
                if (kind == EnemyKind.Tank) bob = Mathf.Sin(phase * 3f) * 0.012f * amp;
                body.localPosition = bodyPos + Vector3.up * bob;
                if (kind == EnemyKind.Drone)
                    body.localRotation = Quaternion.Euler(Mathf.Clamp(fwd * 7f, -18f, 18f), 0f, Mathf.Clamp(-side * 7f, -18f, 18f));
                else if (kind == EnemyKind.Tank || kind == EnemyKind.Compactor)
                    body.localRotation = Quaternion.Euler(Mathf.Clamp(-fwd * 1.5f, -4f, 4f), 0f, 0f);
            }
            roll += fwd * dt;
            foreach (var w in wheels) w.localRotation = Quaternion.Euler(roll / 0.15f * Mathf.Rad2Deg, 0f, 0f); // rolls with the ground
            if (turret != null) turret.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.9f + seed) * 12f, 0f);
            if (bit != null && bit.gameObject.activeInHierarchy) bit.localRotation = Quaternion.Euler(0f, 0f, Time.time * 900f);
            for (int i = 0; i < sway.Count; i++)
                sway[i].localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + i * 1.7f) * 7f, Mathf.Sin(Time.time * 0.9f + i) * 10f, Mathf.Cos(Time.time * 1.1f + i * 2.3f) * 7f);
        }
    }
}
