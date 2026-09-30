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

    /// <summary>Short-lived glow effects (flashes, beams, explosions, smoke puffs).</summary>
    public static class Fx
    {
        class Item { public Transform t; public float life, max; public Vector3 scale; public int mode; }
        static readonly List<Item> items = new List<Item>();

        public static void Flash(Palette pal, Vector3 pos, Color c, float size, float life)
        {
            var go = Prim.Make(PrimitiveType.Sphere, "Flash", null, pos, Vector3.one * size, pal.Glow("Flash" + ColorUtility.ToHtmlStringRGB(c), c, 2f, c));
            items.Add(new Item { t = go.transform, life = life, max = life, scale = go.transform.localScale, mode = 0 });
        }

        public static void Explosion(Palette pal, Vector3 pos, Color c, float radius)
        {
            var go = Prim.Make(PrimitiveType.Sphere, "Boom", null, pos, Vector3.one * 0.2f, pal.Glow("Boom" + ColorUtility.ToHtmlStringRGB(c), c, 1.6f, c));
            items.Add(new Item { t = go.transform, life = 0.3f, max = 0.3f, scale = Vector3.one * radius * 1.1f, mode = 1 });
            for (int i = 0; i < 4; i++) Puff(pal, pos + Random.insideUnitSphere * radius * 0.4f, radius * 0.35f, 0.5f);
        }

        public static void Beam(Palette pal, Vector3 from, Vector3 dir, float length, Color c)
        {
            var go = Prim.Make(PrimitiveType.Cube, "Beam", null, from + dir * length * 0.5f, new Vector3(0.14f, 0.14f, length),
                pal.Glow("Beam" + ColorUtility.ToHtmlStringRGB(c), c, 2.2f, c), Quaternion.LookRotation(dir));
            items.Add(new Item { t = go.transform, life = 0.18f, max = 0.18f, scale = go.transform.localScale, mode = 2 });
        }

        /// <summary>Flat glowing ring that expands along the floor.</summary>
        public static void Shockwave(Palette pal, Vector3 pos, Color c, float radius)
        {
            var go = new GameObject("Shockwave");
            go.transform.position = pos + Vector3.up * 0.06f;
            var mat = pal.Glow("Wave" + ColorUtility.ToHtmlStringRGB(c), c, 1.8f, c);
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                var rot = Quaternion.Euler(0f, i * 360f / segments, 0f);
                Prim.Make(PrimitiveType.Cube, "Seg", go.transform, rot * Vector3.forward * 0.5f, new Vector3(0.14f, 0.04f, 0.025f), mat, rot);
            }
            go.transform.localScale = new Vector3(0.2f, 1f, 0.2f);
            items.Add(new Item { t = go.transform, life = 0.35f, max = 0.35f, scale = new Vector3(radius * 2f, 1f, radius * 2f), mode = 5 });
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                Puff(pal, pos + d * radius * 0.6f + Vector3.up * 0.2f, 0.35f, 0.5f);
            }
        }

        static Font font;

        /// <summary>Floating combat text that rises and shrinks.</summary>
        public static void Text(Vector3 pos, string text, Color c, float life)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Text");
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.color = c;
            tm.fontSize = 64;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            if (Camera.main != null) go.transform.rotation = Camera.main.transform.rotation;
            items.Add(new Item { t = go.transform, life = life, max = life, scale = Vector3.one, mode = 4 });
        }

        public static void Puff(Palette pal, Vector3 pos, float size, float life)
        {
            var go = Prim.Make(PrimitiveType.Sphere, "Smoke", null, pos, Vector3.one * size, pal.Get("Smoke", Palette.Hex("#1C2027"), 0.05f));
            items.Add(new Item { t = go.transform, life = life, max = life, scale = go.transform.localScale, mode = 3 });
        }

        public static void Step(float dt)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                it.life -= dt;
                float k = Mathf.Clamp01(it.life / it.max); // 1 → 0
                switch (it.mode)
                {
                    case 0: it.t.localScale = it.scale * k; break;
                    case 1: it.t.localScale = it.scale * Mathf.Sin((1f - k) * Mathf.PI * 0.5f + 0.3f) * (k > 0.3f ? 1f : k / 0.3f); break;
                    case 2: it.t.localScale = new Vector3(it.scale.x * k, it.scale.y * k, it.scale.z); break;
                    case 3: it.t.localScale = it.scale * (0.6f + (1f - k)) * k; it.t.position += Vector3.up * dt * 0.6f; break;
                    case 5:
                        float g = 1f - k * k;
                        it.t.localScale = new Vector3(Mathf.Max(0.2f, it.scale.x * g), k, Mathf.Max(0.2f, it.scale.z * g));
                        break;
                    case 4: it.t.localScale = it.scale * Mathf.Min(1f, k * 3f); it.t.position += Vector3.up * dt * 1.2f; break;
                }
                if (it.life <= 0f) { Kill(it.t.gameObject); items.RemoveAt(i); }
            }
        }

        public static void Kill(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }
    }
}

