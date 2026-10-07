using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>Short-lived effects (flashes, beams, explosions, smoke, debris, sparks, floating text). Primitives are pooled.</summary>
    public static class Fx
    {
        enum Mode { Shrink, Burst, Beam, Smoke, Text, Ring, Debris, Spark, Fire, BigSmoke, Scorch }

        class Item
        {
            public Transform t;
            public float life, max;
            public Vector3 scale, vel, spin;
            public Mode mode;
            public int pool = -1; // PrimitiveType index when pooled
            public string model;  // Blender effect model pool key (fireballs, smoke, flames)
        }

        static readonly Dictionary<string, Stack<GameObject>> modelPools = new Dictionary<string, Stack<GameObject>>();

        /// <summary>A pooled Blender effect model (Resources/Models/Fx), tinted c; null when the model is missing.</summary>
        static GameObject GetModel(Palette pal, string name, Color c, Vector3 pos, Quaternion rot, Vector3 scale, out string key)
        {
            key = name + ColorUtility.ToHtmlStringRGB(c);
            GameObject go = null;
            if (modelPools.TryGetValue(key, out var stack))
                while (stack.Count > 0 && go == null) { go = stack.Pop(); if (go != null) go.SetActive(true); }
            if (go == null)
            {
                var t = BlenderModels.Spawn("Fx/" + name, null, pal, c, "fx" + ColorUtility.ToHtmlStringRGB(c));
                if (t == null) { key = null; return null; }
                go = t.gameObject;
            }
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            return go;
        }

        static void AddModel(GameObject go, string key, Mode mode, float life, Vector3 scale, Vector3 vel = default, Vector3 spin = default)
        {
            items.Add(new Item { t = go.transform, life = life, max = life, scale = scale, mode = mode, vel = vel, spin = spin, model = key });
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

        static readonly Color FireOrange = Palette.Hex("#FF7A1A"), FireYellow = Palette.Hex("#FFD04A"), FireRed = Palette.Hex("#D8301A");

        /// <summary>
        /// A real blast, not a ball: several lumpy fireballs (Blender) in the weapon's colour and fire colours, each
        /// swelling and churning at its own pace, then rolling smoke, flying embers and a scorch mark on the deck.
        /// </summary>
        public static void Explosion(Palette pal, Vector3 pos, Color c, float radius)
        {
            var core = Get(PrimitiveType.Sphere, "BoomCore", pos, Vector3.one * radius * 0.35f, Glow(pal, "Core", Color.white, 3f));
            Add(core, PrimitiveType.Sphere, Mode.Shrink, 0.07f, core.transform.localScale);
            int n = Mathf.Max(2, Mathf.RoundToInt((radius < 1.1f ? 3 : radius < 2f ? 5 : 7) * Mathf.Max(0.6f, Density)));
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                Color col = i == 0 ? c : i % 3 == 1 ? FireOrange : i % 3 == 2 ? FireYellow : FireRed;
                Vector3 off = Random.insideUnitSphere * radius * 0.45f; off.y = Mathf.Abs(off.y) * 0.6f;
                float s = radius * Random.Range(0.5f, 0.85f);
                var go = GetModel(pal, Random.value < 0.5f ? "FireballA" : "FireballB", col, pos + off, Random.rotation, Vector3.one * s * 0.2f, out var key);
                if (go == null) break;
                any = true;
                AddModel(go, key, Mode.Fire, Random.Range(0.3f, 0.5f) * (0.8f + radius * 0.15f), Vector3.one * s, Vector3.up * Random.Range(0.4f, 1.2f), Random.insideUnitSphere * 220f);
            }
            if (!any)
            {
                var go = Get(PrimitiveType.Sphere, "Boom", pos, Vector3.one * 0.2f, Glow(pal, "Boom", c, 1.6f));
                Add(go, PrimitiveType.Sphere, Mode.Burst, 0.3f, Vector3.one * radius * 1.1f);
            }
            for (int i = 0; i < n - 1; i++)
            {
                var go = GetModel(pal, "Smoke", Color.white, pos + Random.insideUnitSphere * radius * 0.4f + Vector3.up * 0.2f, Random.rotation, Vector3.one * radius * 0.2f, out var key);
                if (go == null) break;
                AddModel(go, key, Mode.BigSmoke, Random.Range(0.8f, 1.2f), Vector3.one * radius * Random.Range(0.35f, 0.55f), Vector3.up * Random.Range(0.7f, 1.3f) + Random.insideUnitSphere * 0.4f, Random.insideUnitSphere * 60f);
            }
            Spray(pal, pos, Vector3.up, FireYellow, Mathf.RoundToInt(4 + radius * 3f), 6f + radius * 2f);
            if (radius >= 0.9f && pos.y < 1.6f)
            {
                var scorch = Get(PrimitiveType.Cylinder, "Scorch", new Vector3(pos.x, 0.014f, pos.z), new Vector3(radius * 0.8f, 0.002f, radius * 0.8f), pal.Get("Scorch", Palette.Hex("#2B231E"), 0.1f));
                Add(scorch, PrimitiveType.Cylinder, Mode.Scorch, 2.2f, scorch.transform.localScale);
            }
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
                Puff(pal, pos + d * radius * 0.6f + Vector3.up * 0.1f, 0.18f, 0.4f); // dust kicked up
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

        /// <summary>A tongue of flame (Blender) that licks upward and dies (burning robots, the flamer's jet).</summary>
        public static void Flame(Palette pal, Vector3 pos, Color c, float size, float life)
        {
            var go = GetModel(pal, "FlameLick", c, pos, Quaternion.Euler(Random.Range(-15f, 15f), Random.value * 360f, Random.Range(-15f, 15f)), new Vector3(size, size * 1.4f, size), out var key);
            if (go != null) { AddModel(go, key, Mode.Smoke, life * Random.Range(0.7f, 1.1f), go.transform.localScale); return; }
            go = Get(PrimitiveType.Sphere, "Flame", pos, new Vector3(size, size * 1.5f, size), Glow(pal, "Flame", c, 2.6f));
            Add(go, PrimitiveType.Sphere, Mode.Smoke, life * Random.Range(0.7f, 1.1f), go.transform.localScale);
        }

        /// <summary>A jagged lightning arc from a to b (shock hits, the arc lance).</summary>
        public static void Arc(Palette pal, Vector3 a, Vector3 b, Color c, float life, float thick = 0.045f)
        {
            var mat = Glow(pal, "Arc", c, 3.2f);
            int n = Mathf.Clamp(Mathf.RoundToInt((b - a).magnitude / 0.35f), 3, 18);
            Vector3 d = b - a, side = Vector3.Cross(d.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            Vector3 prev = a;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 p = i == n ? b : a + d * t + (side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-0.6f, 0.6f)) * Mathf.Min(0.3f, d.magnitude * 0.08f);
                Vector3 seg = p - prev;
                if (seg.sqrMagnitude > 0.0001f)
                {
                    var go = Get(PrimitiveType.Cube, "Arc", (p + prev) * 0.5f, new Vector3(thick, thick, seg.magnitude), mat, Quaternion.LookRotation(seg));
                    Add(go, PrimitiveType.Cube, Mode.Beam, life, go.transform.localScale);
                }
                prev = p;
            }
        }

        /// <summary>A few sparks thrown one way (ricochets, a slug punching out of the far side).</summary>
        public static void Spray(Palette pal, Vector3 pos, Vector3 dir, Color c, int count, float speed)
        {
            var mat = Glow(pal, "Spark", c, 3.5f);
            count = Mathf.Max(1, Mathf.RoundToInt(count * Density));
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                Vector3 v = (dir + Random.insideUnitSphere * 0.35f).normalized * speed * Random.Range(0.6f, 1.1f);
                var go = Get(PrimitiveType.Cube, "Spark", pos, new Vector3(0.04f, 0.04f, 0.3f), mat, Quaternion.LookRotation(v));
                Add(go, PrimitiveType.Cube, Mode.Spark, Random.Range(0.15f, 0.3f), go.transform.localScale, v);
            }
        }

        /// <summary>A small lumpy smoke cloud (Blender) that rises and fades.</summary>
        public static void Puff(Palette pal, Vector3 pos, float size, float life)
        {
            var go = GetModel(pal, "Smoke", Color.white, pos, Random.rotation, Vector3.one * size, out var key);
            if (go != null) { AddModel(go, key, Mode.Smoke, life, go.transform.localScale, default, Random.insideUnitSphere * 90f); return; }
            go = Get(PrimitiveType.Sphere, "Smoke", pos, Vector3.one * size, pal.Get("Smoke", Palette.Hex("#3A3F4A"), 0.05f));
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
                    case Mode.Smoke:
                        it.t.localScale = it.scale * (0.6f + (1f - k)) * k; it.t.position += Vector3.up * dt * 0.6f;
                        if (it.spin != Vector3.zero) it.t.rotation = Quaternion.Euler(it.spin * dt) * it.t.rotation;
                        break;
                    case Mode.Fire:
                    {
                        // Swells fast, churns, then collapses.
                        float age = 1f - k;
                        float grow = Mathf.Sin(Mathf.Clamp01(age / 0.45f) * Mathf.PI * 0.5f);
                        float fade = age > 0.6f ? 1f - (age - 0.6f) / 0.4f : 1f;
                        it.t.localScale = it.scale * Mathf.Max(0.05f, (0.2f + 0.8f * grow) * fade);
                        it.t.position += it.vel * dt;
                        it.t.rotation = Quaternion.Euler(it.spin * dt) * it.t.rotation;
                        break;
                    }
                    case Mode.BigSmoke:
                    {
                        float age = 1f - k;
                        it.t.localScale = it.scale * (0.4f + 0.9f * Mathf.Sqrt(age)) * Mathf.Min(1f, k * 3f);
                        it.t.position += it.vel * dt;
                        it.vel *= Mathf.Exp(-0.8f * dt);
                        it.t.rotation = Quaternion.Euler(it.spin * dt) * it.t.rotation;
                        break;
                    }
                    case Mode.Scorch: it.t.localScale = new Vector3(it.scale.x * Mathf.Min(1f, k * 3f), it.scale.y, it.scale.z * Mathf.Min(1f, k * 3f)); break;
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
            if (it.model != null && Application.isPlaying)
            {
                it.t.gameObject.SetActive(false);
                if (!modelPools.TryGetValue(it.model, out var ms)) modelPools[it.model] = ms = new Stack<GameObject>();
                ms.Push(it.t.gameObject);
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
            foreach (var s in modelPools.Values) foreach (var go in s) if (go != null) Kill(go);
            modelPools.Clear();
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
