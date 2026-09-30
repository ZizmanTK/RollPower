using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>Anything the guns can hit (enemies implement this).</summary>
    public interface ITarget
    {
        Vector3 Position { get; }
        float Radius { get; }
        bool Alive { get; }
        bool Flying { get; }
        /// <summary>False when this weapon physically can't reach the target (e.g. bullets passing under a drone).</summary>
        bool Reachable(WeaponDef weapon);
        /// <summary>Returns true if the hit did damage (false = deflected).</summary>
        bool Hit(WeaponDef weapon, float damage, Vector3 from);
    }

    public static class Targets
    {
        public static readonly List<ITarget> All = new List<ITarget>();

        public static ITarget Nearest(Vector3 from, float maxDist, System.Predicate<ITarget> filter = null)
        {
            ITarget best = null;
            float bestD = maxDist * maxDist;
            foreach (var t in All)
            {
                if (!t.Alive || (filter != null && !filter(t))) continue;
                float d = (t.Position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }
    }

    /// <summary>Simulates every bullet, orb and missile. Stepped by the GameLoop.</summary>
    public static class Projectiles
    {
        class Shot
        {
            public Transform t;
            public WeaponDef def;
            public Vector3 pos, vel;
            public float life, delay, age;
            public ITarget target;
        }

        static readonly List<Shot> shots = new List<Shot>();

        public static void Spawn(Palette pal, WeaponDef def, Vector3 pos, Vector3 dir, float delay)
        {
            var mat = pal.Glow("Shot" + def.number, def.color, 1.6f, def.color);
            var go = Prim.Make(PrimitiveType.Sphere, "Shot", null, pos, Vector3.one * def.radius * 2f, mat);
            if (!def.homing && def.aoe <= 0f) go.transform.localScale = new Vector3(def.radius * 1.6f, def.radius * 1.6f, def.radius * 5f);
            var s = new Shot { t = go.transform, def = def, pos = pos, life = def.range / def.speed + 0.6f, delay = delay };
            s.vel = def.homing ? (dir + Vector3.up * 0.9f).normalized * def.speed * 0.6f : dir * def.speed;
            if (def.homing) s.target = Targets.Nearest(pos + dir * 6f, def.range, t => t.Reachable(def));
            go.SetActive(delay <= 0f);
            go.transform.rotation = Quaternion.LookRotation(s.vel);
            shots.Add(s);
        }

        /// <summary>Instant piercing beam: damages everything along the line.</summary>
        public static void Beam(Palette pal, WeaponDef def, Vector3 from, Vector3 dir)
        {
            float length = def.range;
            foreach (var t in Targets.All.ToArray())
            {
                if (!t.Alive || !t.Reachable(def)) continue;
                Vector3 to = t.Position - from;
                to.y = 0f;
                float along = Vector3.Dot(to, dir);
                if (along < 0f || along > length) continue;
                if ((to - dir * along).magnitude < t.Radius + 0.35f)
                {
                    t.Hit(def, def.damage, from);
                    Fx.Flash(pal, t.Position, def.color, 0.6f, 0.15f);
                }
            }
            // Clip the visual at the arena wall.
            float wall = World.HalfSize;
            for (float d = 0f; d < length; d += 0.25f)
            {
                Vector3 p = from + dir * d;
                if (Mathf.Abs(p.x) > wall || Mathf.Abs(p.z) > wall) { length = d; break; }
            }
            Fx.Beam(pal, from, dir, length, def.color);
            Fx.Flash(pal, from + dir * length, def.color, 0.5f, 0.12f);
        }

        public static void Step(Palette pal, float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                if (s.delay > 0f)
                {
                    s.delay -= dt;
                    if (s.delay <= 0f) s.t.gameObject.SetActive(true);
                    continue;
                }
                s.age += dt;
                s.life -= dt;

                if (s.def.homing)
                {
                    if (s.target == null || !s.target.Alive) s.target = Targets.Nearest(s.pos, s.def.range, t => t.Reachable(s.def));
                    if (s.target != null && s.age > 0.18f)
                    {
                        Vector3 want = (s.target.Position - s.pos).normalized * s.def.speed;
                        s.vel = Vector3.Lerp(s.vel, want, 1f - Mathf.Exp(-6f * dt));
                    }
                    else s.vel += Vector3.down * 3f * dt;
                    if (Random.value < 0.6f) Fx.Puff(pal, s.pos, 0.12f, 0.35f);
                }

                s.pos += s.vel * dt;
                s.t.position = s.pos;
                if (s.vel.sqrMagnitude > 0.01f) s.t.rotation = Quaternion.LookRotation(s.vel);

                bool done = s.life <= 0f;
                ITarget hit = null;
                foreach (var t in Targets.All)
                {
                    if (!t.Alive || !t.Reachable(s.def)) continue;
                    if (FlatDistSq(t.Position, s.pos) < (t.Radius + s.def.radius) * (t.Radius + s.def.radius)) { hit = t; break; }
                }
                if (hit != null) { hit.Hit(s.def, s.def.damage, s.pos - s.vel.normalized); done = true; }
                if (Mathf.Abs(s.pos.x) > World.HalfSize + 0.2f || Mathf.Abs(s.pos.z) > World.HalfSize + 0.2f || s.pos.y < 0.05f) done = true;

                if (done)
                {
                    if (s.def.aoe > 0f) Explode(pal, s.def, s.pos, hit);
                    else Fx.Flash(pal, s.pos, s.def.color, 0.3f, 0.1f);
                    Fx.Kill(s.t.gameObject);
                    shots.RemoveAt(i);
                }
            }
        }

        static void Explode(Palette pal, WeaponDef def, Vector3 pos, ITarget alreadyHit)
        {
            Fx.Explosion(pal, pos, def.color, def.aoe);
            foreach (var t in Targets.All.ToArray())
            {
                if (!t.Alive || t == alreadyHit || !t.Reachable(def)) continue;
                if (FlatDistSq(t.Position, pos) < def.aoe * def.aoe) t.Hit(def, def.damage * 0.75f, pos);
            }
        }

        static float FlatDistSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        public static void Clear()
        {
            foreach (var s in shots) Fx.Kill(s.t.gameObject);
            shots.Clear();
        }
    }
}
