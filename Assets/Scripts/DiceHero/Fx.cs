using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>Short-lived effects (flashes, beams, explosions, smoke, debris, sparks, floating text). Primitives are pooled.</summary>
    public static class Fx
    {
        enum Mode { Shrink, Burst, Beam, Smoke, Text, Ring, Debris, Spark }

        class Item
        {
            public Transform t;
            public float life, max;
            public Vector3 scale, vel, spin;
            public Mode mode;
            public int pool = -1; // PrimitiveType index when pooled
        }

        static readonly List<Item> items = new List<Item>();
        static readonly Dictionary<int, Stack<GameObject>> pools = new Dictionary<int, Stack<GameObject>>();

        /// <summary>Global multiplier for particle counts (lowered on WebGL).</summary>
        public static float Density = 1f;

        static GameObject Get(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            if (pools.TryGetValue((int)type, out var stack))
                while (stack.Count > 0)
                {
                    var go = stack.Pop();
                    if (go == null) continue;
                    go.SetActive(true);
                    go.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
                    go.transform.localScale = scale;
                    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    return go;
                }
            return Prim.Make(type, name, null, pos, scale, mat, rot);
        }

        static void Add(GameObject go, PrimitiveType? type, Mode mode, float life, Vector3 scale, Vector3 vel = default, Vector3 spin = default)
        {
            items.Add(new Item { t = go.transform, life = life, max = life, scale = scale, mode = mode, vel = vel, spin = spin, pool = type.HasValue ? (int)type.Value : -1 });
        }

        static Material Glow(Palette pal, string prefix, Color c, float intensity) =>
            pal.Glow(prefix + ColorUtility.ToHtmlStringRGB(c), c, intensity, c);

        public static void Flash(Palette pal, Vector3 pos, Color c, float size, float life)
        {
            var go = Get(PrimitiveType.Sphere, "Flash", pos, Vector3.one * size, Glow(pal, "Flash", c, 2f));
            Add(go, PrimitiveType.Sphere, Mode.Shrink, life, go.transform.localScale);
        }

        public static void Explosion(Palette pal, Vector3 pos, Color c, float radius)
        {
            var go = Get(PrimitiveType.Sphere, "Boom", pos, Vector3.one * 0.2f, Glow(pal, "Boom", c, 1.6f));
            Add(go, PrimitiveType.Sphere, Mode.Burst, 0.3f, Vector3.one * radius * 1.1f);
            // Hot white core for the first few frames.
            var core = Get(PrimitiveType.Sphere, "BoomCore", pos, Vector3.one * radius * 0.6f, Glow(pal, "Core", Color.white, 3f));
            Add(core, PrimitiveType.Sphere, Mode.Shrink, 0.12f, core.transform.localScale);
            for (int i = 0; i < 4; i++) Puff(pal, pos + Random.insideUnitSphere * radius * 0.4f, radius * 0.35f, 0.5f);
        }

        public static void Beam(Palette pal, Vector3 from, Vector3 dir, float length, Color c)
        {
            var go = Get(PrimitiveType.Cube, "Beam", from + dir * length * 0.5f, new Vector3(0.14f, 0.14f, length), Glow(pal, "Beam", c, 2.2f), Quaternion.LookRotation(dir));
            Add(go, PrimitiveType.Cube, Mode.Beam, 0.18f, go.transform.localScale);
        }

        /// <summary>Flat glowing ring that expands along the floor.</summary>
        public static void Shockwave(Palette pal, Vector3 pos, Color c, float radius)
        {
            var go = new GameObject("Shockwave");
            go.transform.position = pos + Vector3.up * 0.06f;
            var mat = Glow(pal, "Wave", c, 1.8f);
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                var rot = Quaternion.Euler(0f, i * 360f / segments, 0f);
                Prim.Make(PrimitiveType.Cube, "Seg", go.transform, rot * Vector3.forward * 0.5f, new Vector3(0.14f, 0.04f, 0.025f), mat, rot);
            }
            go.transform.localScale = new Vector3(0.2f, 1f, 0.2f);
            Add(go, null, Mode.Ring, 0.35f, new Vector3(radius * 2f, 1f, radius * 2f));
            int puffs = Mathf.Max(3, Mathf.RoundToInt(8 * Density));
            for (int i = 0; i < puffs; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 360f / puffs, 0f) * Vector3.forward;
                Puff(pal, pos + d * radius * 0.6f + Vector3.up * 0.2f, 0.35f, 0.5f);
            }
        }

        /// <summary>Chunks of metal that fly out, bounce on the deck and fade.</summary>
        public static void Debris(Palette pal, Vector3 pos, Color c, int count, float force)
        {
            var mat = pal.Get("Debris" + ColorUtility.ToHtmlStringRGB(c), c, 0.5f, 0.7f);
            count = Mathf.Max(1, Mathf.RoundToInt(count * Density));
            for (int i = 0; i < count; i++)
            {
                float s = Random.Range(0.07f, 0.18f);
                var go = Get(PrimitiveType.Cube, "Debris", pos + Random.insideUnitSphere * 0.2f, Vector3.one * s, mat, Random.rotation);
                Vector3 v = Random.insideUnitSphere * force * 0.6f;
                v.y = Mathf.Abs(v.y) + force * Random.Range(0.4f, 0.9f);
                Add(go, PrimitiveType.Cube, Mode.Debris, Random.Range(0.8f, 1.5f), go.transform.localScale, v, Random.insideUnitSphere * 720f);
            }
        }

        /// <summary>Fast glowing streaks.</summary>
        public static void Sparks(Palette pal, Vector3 pos, Color c, int count)
        {
            var mat = Glow(pal, "Spark", c, 3.5f);
            count = Mathf.Max(1, Mathf.RoundToInt(count * Density));
            for (int i = 0; i < count; i++)
            {
                Vector3 v = Random.insideUnitSphere * 7f;
                v.y = Mathf.Abs(v.y) * 0.8f + 1.5f;
                var go = Get(PrimitiveType.Cube, "Spark", pos, new Vector3(0.04f, 0.04f, 0.28f), mat, Quaternion.LookRotation(v));
                Add(go, PrimitiveType.Cube, Mode.Spark, Random.Range(0.2f, 0.4f), go.transform.localScale, v);
            }
        }

        static Font font;
        static readonly Stack<TextMesh> textPool = new Stack<TextMesh>();

        /// <summary>Floating combat text that pops in, rises and shrinks.</summary>
        public static void Text(Vector3 pos, string text, Color c, float life, float size = 1f)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TextMesh tm = null;
            while (textPool.Count > 0 && tm == null) tm = textPool.Pop();
            if (tm == null)
            {
                var go = new GameObject("Text");
                tm = go.AddComponent<TextMesh>();
                tm.font = font;
                tm.fontSize = 64;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.fontStyle = FontStyle.Bold;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            tm.gameObject.SetActive(true);
            tm.transform.position = pos;
            tm.text = text;
            tm.color = c;
            tm.characterSize = 0.06f * size;
            if (Camera.main != null) tm.transform.rotation = Camera.main.transform.rotation;
            Add(tm.gameObject, null, Mode.Text, life, Vector3.one);
        }

        public static void Puff(Palette pal, Vector3 pos, float size, float life)
        {
            var go = Get(PrimitiveType.Sphere, "Smoke", pos, Vector3.one * size, pal.Get("Smoke", Palette.Hex("#3A3F4A"), 0.05f));
            Add(go, PrimitiveType.Sphere, Mode.Smoke, life, go.transform.localScale);
        }

        public static void Step(float dt)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                if (it.t == null) { items.RemoveAt(i); continue; }
                it.life -= dt;
                float k = Mathf.Clamp01(it.life / it.max); // 1 → 0
                switch (it.mode)
                {
                    case Mode.Shrink: it.t.localScale = it.scale * k; break;
                    case Mode.Burst: it.t.localScale = it.scale * Mathf.Sin((1f - k) * Mathf.PI * 0.5f + 0.3f) * (k > 0.3f ? 1f : k / 0.3f); break;
                    case Mode.Beam: it.t.localScale = new Vector3(it.scale.x * k, it.scale.y * k, it.scale.z); break;
                    case Mode.Smoke: it.t.localScale = it.scale * (0.6f + (1f - k)) * k; it.t.position += Vector3.up * dt * 0.6f; break;
                    case Mode.Ring:
                        float g = 1f - k * k;
                        it.t.localScale = new Vector3(Mathf.Max(0.2f, it.scale.x * g), k, Mathf.Max(0.2f, it.scale.z * g));
                        break;
                    case Mode.Text:
                    {
                        float age = it.max - it.life;
                        float pop = age < 0.12f ? Mathf.Lerp(1.6f, 1f, age / 0.12f) : 1f;
                        it.t.localScale = it.scale * pop * Mathf.Min(1f, k * 3f);
                        it.t.position += Vector3.up * dt * 1.2f;
                        break;
                    }
                    case Mode.Debris:
                    {
                        it.vel += Vector3.down * 20f * dt;
                        Vector3 p = it.t.position + it.vel * dt;
                        bool onDeck = Mathf.Abs(p.x) < World.HalfSize && Mathf.Abs(p.z) < World.HalfSize;
                        if (onDeck && p.y < it.scale.y * 0.5f && it.vel.y < 0f)
                        {
                            p.y = it.scale.y * 0.5f;
                            it.vel = new Vector3(it.vel.x * 0.6f, -it.vel.y * 0.35f, it.vel.z * 0.6f);
                            it.spin *= 0.5f;
                        }
                        it.t.position = p;
                        it.t.rotation = Quaternion.Euler(it.spin * dt) * it.t.rotation;
                        it.t.localScale = it.scale * Mathf.Min(1f, k * 4f);
                        break;
                    }
                    case Mode.Spark:
                        it.vel += Vector3.down * 9f * dt;
                        it.vel *= Mathf.Exp(-3f * dt);
                        it.t.position += it.vel * dt;
                        if (it.vel.sqrMagnitude > 0.01f) it.t.rotation = Quaternion.LookRotation(it.vel);
                        it.t.localScale = new Vector3(it.scale.x, it.scale.y, it.scale.z * k);
                        break;
                }
                if (it.life <= 0f) { Release(it); items.RemoveAt(i); }
            }
        }

        static void Release(Item it)
        {
            if (it.mode == Mode.Text && Application.isPlaying)
            {
                it.t.gameObject.SetActive(false);
                textPool.Push(it.t.GetComponent<TextMesh>());
                return;
            }
            if (it.pool < 0 || !Application.isPlaying) { Kill(it.t.gameObject); return; }
            it.t.gameObject.SetActive(false);
            if (!pools.TryGetValue(it.pool, out var stack)) pools[it.pool] = stack = new Stack<GameObject>();
            stack.Push(it.t.gameObject);
        }

        /// <summary>Drops every live effect and pooled object (call before reloading the scene).</summary>
        public static void Clear()
        {
            foreach (var it in items) if (it.t != null) Kill(it.t.gameObject);
            items.Clear();
            foreach (var s in pools.Values) foreach (var go in s) if (go != null) Kill(go);
            pools.Clear();
            foreach (var tm in textPool) if (tm != null) Kill(tm.gameObject);
            textPool.Clear();
        }

        public static void Kill(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }
    }
}
