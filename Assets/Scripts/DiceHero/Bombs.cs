using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Roll Power's signature hazard: barrel bombs with a visible fuse (green → yellow → red).
    /// Shove them with the dice (or blast them with a landing slam) until they slide off the edge
    /// of the platform. Explosions hurt everything nearby, enemies included, and set off other bombs.
    /// </summary>
    public class Bomb
    {
        public Transform t;
        public Vector3 pos, vel;
        public float fuse, maxFuse, drop, fallT, spin;
        public bool falling;
        public bool Armed => drop <= 0f && !falling;
        public const float Radius = 0.42f;
        public Renderer[] rings;
        public Transform marker, spark;
        public int ringState = -1;
        public float beep;
    }

    public class Bombs
    {
        public readonly List<Bomb> All = new List<Bomb>();
        readonly DiceController dice;
        readonly Palette pal;
        readonly Game game;
        readonly Material[] ringMats;
        readonly Material body, cap, markerMat;
        public const float BlastRadius = 2.4f;
        public const float DropHeight = 9f;

        public Bombs(DiceController dice, Palette pal, Game game)
        {
            this.dice = dice;
            this.pal = pal;
            this.game = game;
            ringMats = new[]
            {
                pal.Glow("BombGreen", Palette.Hex("#3DFF7A"), 3f),
                pal.Glow("BombYellow", Palette.Hex("#FFD23D"), 3.2f),
                pal.Glow("BombRed", Palette.Hex("#FF2A3D"), 4f),
                pal.Get("BombRingOff", Palette.Hex("#2A1216"), 0.4f),
            };
            body = pal.Get("BombBody", Palette.Hex("#4A505C"), 0.55f, 0.75f);
            cap = pal.Get("BombCap", Palette.Hex("#22262E"), 0.5f, 0.6f);
            markerMat = pal.Glow("BombMarker", Palette.Hex("#FF2A3D"), 1.2f, Palette.Hex("#FF2A3D"));
        }

        public Bomb Spawn(Vector3 ground, float dropFrom = DropHeight)
        {
            var b = new Bomb { pos = ground, drop = dropFrom > 0.01f ? 0.7f : 0f };
            b.maxFuse = b.fuse = Mathf.Max(6f, 9.5f - game.Wave * 0.15f) + RunStats.Current.fuseBonus;
            var root = new GameObject("Bomb").transform;
            b.t = root;
            // Upright barrel, like the jam original: steel drum with two light bands and a fuse cap.
            Prim.Make(PrimitiveType.Cylinder, "Drum", root, new Vector3(0f, 0.42f, 0f), new Vector3(0.78f, 0.4f, 0.78f), body);
            var rA = Prim.Make(PrimitiveType.Cylinder, "RingA", root, new Vector3(0f, 0.24f, 0f), new Vector3(0.81f, 0.05f, 0.81f), ringMats[0]);
            var rB = Prim.Make(PrimitiveType.Cylinder, "RingB", root, new Vector3(0f, 0.6f, 0f), new Vector3(0.81f, 0.05f, 0.81f), ringMats[0]);
            Prim.Make(PrimitiveType.Cylinder, "Cap", root, new Vector3(0f, 0.84f, 0f), new Vector3(0.5f, 0.04f, 0.5f), cap);
            var sp = Prim.Make(PrimitiveType.Sphere, "Fuse", root, new Vector3(0f, 0.92f, 0f), Vector3.one * 0.16f, ringMats[0]);
            b.rings = new[] { rA.GetComponent<Renderer>(), rB.GetComponent<Renderer>(), sp.GetComponent<Renderer>() };
            b.spark = sp.transform;
            if (b.drop > 0f)
            {
                b.marker = Prim.Make(PrimitiveType.Cylinder, "DropMarker", null, ground + Vector3.up * 0.03f, new Vector3(0.2f, 0.004f, 0.2f), markerMat).transform;
                root.position = ground + Vector3.up * dropFrom;
            }
            else root.position = ground;
            All.Add(b);
            return b;
        }

        public void Step(float dt)
        {
            var s = RunStats.Current;
            Vector3 dp = dice.transform.position;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var b = All[i];
                if (b.drop > 0f) { StepDrop(b, dt); continue; }
                if (b.falling) { if (StepFall(b, dt)) { Remove(i); } continue; }

                b.fuse -= dt;
                UpdateLights(b, dt);
                if (b.fuse <= 0f) { Explode(b); Remove(i); continue; }

                // Glide with a little friction so a good shove carries it a long way.
                b.vel *= Mathf.Exp(-0.85f * dt);
                b.pos += b.vel * dt;

                // Obstacles bounce it.
                foreach (var ob in World.Obstacles)
                {
                    if (!ob.Overlaps(b.pos, Bomb.Radius)) continue;
                    Vector3 d = b.pos - ob.transform.position;
                    float px = ob.halfExtents.x + Bomb.Radius - Mathf.Abs(d.x);
                    float pz = ob.halfExtents.y + Bomb.Radius - Mathf.Abs(d.z);
                    if (px < pz) { b.pos.x += Mathf.Sign(d.x) * px; b.vel.x = Mathf.Abs(b.vel.x) * Mathf.Sign(d.x) * 0.7f; }
                    else { b.pos.z += Mathf.Sign(d.z) * pz; b.vel.z = Mathf.Abs(b.vel.z) * Mathf.Sign(d.z) * 0.7f; }
                    if (b.vel.sqrMagnitude > 4f) Sound.Play(Sfx.Clonk, 0.35f);
                }

                // Moving bombs bowl enemies over.
                if (b.vel.sqrMagnitude > 9f)
                    foreach (var e in game.Enemies)
                    {
                        if (!e.Alive || e.Flying) continue;
                        Vector3 d = e.pos - b.pos; d.y = 0f;
                        if (d.magnitude > e.radius + Bomb.Radius) continue;
                        e.vel += d.normalized * b.vel.magnitude * 0.9f;
                        game.BlastEnemy(e, 1f, b.pos, "BOWLED");
                        b.vel *= 0.8f;
                    }

                // Off the edge: no fence for bombs.
                if (Mathf.Abs(b.pos.x) > World.HalfSize + 0.05f || Mathf.Abs(b.pos.z) > World.HalfSize + 0.05f)
                {
                    b.falling = true;
                    Sound.Play(Sfx.Whistle, 0.6f, 0f);
                    game.BombDisposed(b.pos);
                }

                b.spin += b.vel.magnitude * dt * 90f;
                Vector3 tilt = Vector3.Cross(Vector3.up, b.vel).normalized * Mathf.Min(12f, b.vel.magnitude * 2f);
                float swell = b.fuse < 1f ? 1f + (1f - b.fuse) * 0.18f + Mathf.Sin(b.fuse * 60f) * 0.03f : 1f;
                b.t.localScale = new Vector3(swell, 1f + (swell - 1f) * 0.5f, swell);
                b.t.SetPositionAndRotation(b.pos, Quaternion.Euler(tilt) * Quaternion.Euler(0f, b.spin, 0f));
            }
            Collide(dp);
        }

        void StepDrop(Bomb b, float dt)
        {
            b.drop -= dt;
            float u = 1f - Mathf.Clamp01(b.drop / 0.7f);
            b.t.position = b.pos + Vector3.up * DropHeight * (1f - u * u);
            if (b.marker != null) b.marker.localScale = new Vector3(0.2f + u * 0.8f, 0.004f, 0.2f + u * 0.8f);
            UpdateLights(b, dt);
            if (b.drop <= 0f)
            {
                b.t.position = b.pos;
                if (b.marker != null) Fx.Kill(b.marker.gameObject);
                Fx.Shockwave(pal, b.pos, Palette.Hex("#FF7A1A"), 1.1f);
                Fx.Debris(pal, b.pos + Vector3.up * 0.1f, Palette.Hex("#8C96A3"), 6, 3f);
                Sound.Play(Sfx.Thud, 0.8f);
                game.Juice(0.12f, 0f);
                // Landing on the dice hurts.
                Vector3 d = dice.transform.position - b.pos; d.y = 0f;
                if (d.magnitude < 0.9f) { game.HurtPlayer(1, b.pos); dice.Knock(d.normalized * 5f); }
            }
        }

        bool StepFall(Bomb b, float dt)
        {
            b.fallT += dt;
            b.vel += Vector3.down * 18f * dt;
            b.pos += b.vel * dt;
            b.spin += dt * 400f;
            b.t.SetPositionAndRotation(b.pos, Quaternion.Euler(b.fallT * 260f, b.spin, b.fallT * 130f));
            if (b.fallT < 1.1f) return false;
            // Far below: a harmless distant flash.
            Fx.Explosion(pal, b.pos, Palette.Hex("#FF7A1A"), 2.5f);
            Sound.Play(Sfx.Explosion, 0.35f);
            return true;
        }

        void UpdateLights(Bomb b, float dt)
        {
            float k = b.fuse / b.maxFuse;
            int state = k > 0.6f ? 0 : k > 0.3f ? 1 : 2;
            if (state == 2)
            {
                // Blink faster and faster as it runs out.
                float rate = Mathf.Lerp(14f, 4f, Mathf.Clamp01(b.fuse / (b.maxFuse * 0.3f)));
                if (Mathf.Repeat(b.fuse * rate, 1f) > 0.5f) state = 3;
            }
            if (state != b.ringState)
            {
                b.ringState = state;
                foreach (var r in b.rings) r.sharedMaterial = ringMats[state];
            }
            if (b.drop > 0f) return;
            b.beep -= dt;
            if (b.beep <= 0f)
            {
                b.beep = Mathf.Lerp(0.14f, 1.1f, k);
                Sound.Play(Sfx.Beep, k < 0.3f ? 0.35f : 0.18f, 0f);
            }
            b.spark.localScale = Vector3.one * (0.13f + Random.value * 0.07f);
        }

        void Collide(Vector3 dp)
        {
            var s = RunStats.Current;
            for (int i = 0; i < All.Count; i++)
            {
                var b = All[i];
                if (!b.Armed) continue;

                // Dice shove (while gliding; landing slams are handled by Slam()).
                if (!dice.IsRolling)
                {
                    Vector3 d = b.pos - dp; d.y = 0f;
                    float min = 0.5f + Bomb.Radius;
                    float m = d.magnitude;
                    if (m < min && m > 0.0001f)
                    {
                        Vector3 n = d / m;
                        b.pos += n * (min - m);
                        float closing = Vector3.Dot(dice.Velocity - b.vel, n);
                        if (closing > 0f)
                        {
                            b.vel += n * (closing * 1.3f * s.kickMul + 0.6f);
                            dice.Nudge(-n * closing * 0.3f);
                            if (closing > 2.5f)
                            {
                                Sound.Play(Sfx.Clonk, Mathf.Clamp01(closing / 8f) + 0.2f);
                                Fx.Sparks(pal, b.pos + Vector3.up * 0.4f - n * 0.4f, Palette.Hex("#FFC940"), 6);
                                game.Juice(0.06f + closing * 0.01f, closing > 6f ? 0.03f : 0f);
                            }
                        }
                    }
                }

                // Bomb vs bomb
                for (int j = i + 1; j < All.Count; j++)
                {
                    var o = All[j];
                    if (!o.Armed) continue;
                    Vector3 d = o.pos - b.pos; d.y = 0f;
                    float m = d.magnitude;
                    if (m >= Bomb.Radius * 2f || m < 0.0001f) continue;
                    Vector3 n = d / m;
                    Vector3 push = n * (Bomb.Radius * 2f - m) * 0.5f;
                    b.pos -= push; o.pos += push;
                    float closing = Vector3.Dot(b.vel - o.vel, n);
                    if (closing > 0f) { b.vel -= n * closing * 0.9f; o.vel += n * closing * 0.9f; if (closing > 2f) Sound.Play(Sfx.Clonk, 0.4f); }
                }
            }
        }

        /// <summary>Landing shockwave launches nearby bombs outward.</summary>
        public void Slam(Vector3 center, float radius, int steps)
        {
            float power = (steps == 2 ? 12f : 8.5f) * RunStats.Current.kickMul;
            foreach (var b in All)
            {
                if (!b.Armed) continue;
                Vector3 d = b.pos - center; d.y = 0f;
                float m = d.magnitude;
                if (m > radius + 0.6f) continue;
                Vector3 n = m > 0.01f ? d / m : dice.Velocity.normalized;
                b.vel += n * power * Mathf.Lerp(1f, 0.6f, m / (radius + 0.6f));
            }
        }

        void Explode(Bomb b)
        {
            Vector3 c = b.pos;
            Fx.Explosion(pal, c + Vector3.up * 0.4f, Palette.Hex("#FF7A1A"), BlastRadius * 0.9f);
            Fx.Shockwave(pal, c, Palette.Hex("#FF3B1A"), BlastRadius);
            Fx.Debris(pal, c + Vector3.up * 0.3f, Palette.Hex("#4A505C"), 12, 6f);
            Fx.Sparks(pal, c + Vector3.up * 0.5f, Palette.Hex("#FFC940"), 14);
            Sound.Play(Sfx.BigExplosion, 1f, 0.05f);
            game.Juice(0.6f, 0.07f);
            game.Flash(0.25f);

            // Enemies (armour and flight don't help against a bomb).
            foreach (var e in game.Enemies.ToArray())
            {
                if (!e.Alive) continue;
                Vector3 d = e.pos - c; d.y = 0f;
                if (d.magnitude > BlastRadius + e.radius) continue;
                e.vel += d.normalized * 8f;
                game.BlastEnemy(e, e.kind == EnemyKind.Boss ? 6f : 10f, c, "BOOM");
            }
            // The dice (a roll in progress dodges it).
            Vector3 pd = dice.transform.position - c; pd.y = 0f;
            if (pd.magnitude < BlastRadius + 0.3f)
            {
                int dmg = Mathf.Max(0, 2 - RunStats.Current.blastResist);
                if (dmg > 0) game.HurtPlayer(dmg, c);
                dice.Knock(pd.normalized * 8f);
            }
            // Chain reaction with other bombs.
            foreach (var o in All)
            {
                if (o == b || !o.Armed) continue;
                Vector3 d = o.pos - c; d.y = 0f;
                if (d.magnitude < BlastRadius) { o.fuse = Mathf.Min(o.fuse, 0.18f); o.vel += d.normalized * 6f; }
            }
        }

        void Remove(int i)
        {
            var b = All[i];
            if (b.marker != null) Fx.Kill(b.marker.gameObject);
            if (b.t != null) Fx.Kill(b.t.gameObject);
            All.RemoveAt(i);
        }

        public void Clear()
        {
            for (int i = All.Count - 1; i >= 0; i--) Remove(i);
        }

        /// <summary>Most urgent armed bomb (lowest fuse), for HUD warnings and the autopilot.</summary>
        public Bomb MostUrgent()
        {
            Bomb best = null;
            foreach (var b in All) if (b.Armed && (best == null || b.fuse < best.fuse)) best = b;
            return best;
        }
    }
}
