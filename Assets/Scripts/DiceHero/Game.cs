using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>Waves, enemy AI, enemy fire, dice health and floating combat text.</summary>
    public class Game
    {
        public static Game I { get; private set; }

        public readonly List<Enemy> Enemies = new List<Enemy>();
        public int Wave { get; private set; }
        public int Hp { get; private set; } = MaxHp;
        public const int MaxHp = 6;
        public bool Won { get; private set; }
        public bool Lost => Hp <= 0;
        public float HurtFlash { get; private set; }
        public int Score { get; private set; }

        /// <summary>Set by the demo to script its own spawns instead of the wave list.</summary>
        public bool ManualSpawning;

        readonly DiceController dice;
        readonly Palette pal;
        readonly GameLoop loop;
        readonly CameraFollow cam;
        float invuln, waveDelay = 1.5f, deflectCooldown;

        class Bullet { public Transform t; public Vector3 pos, vel; public float life; public int dmg; }
        readonly List<Bullet> bullets = new List<Bullet>();

        // Endless waves: each wave leans on one "problem" enemy type so the right gun matters.
        static readonly EnemyKind[] FocusOrder = { EnemyKind.Crawler, EnemyKind.Drone, EnemyKind.Tank, EnemyKind.Mite };
        readonly Queue<EnemyKind> spawnQueue = new Queue<EnemyKind>();
        float spawnTimer;
        public EnemyKind Focus { get; private set; }
        public Game(DiceController dice, Palette pal, GameLoop loop)
        {
            I = this;
            this.dice = dice;
            this.pal = pal;
            this.loop = loop;
            cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            Targets.All.Clear();
            dice.TopChanged += (o, n) => Slam(dice.LastRollSteps);
        }

        public event System.Action<int> Slammed;

        /// <summary>Landing shockwave: damages and shoves every ground enemy nearby (armour doesn't help).</summary>
        void Slam(int steps)
        {
            float radius = steps == 2 ? 3f : 2.2f;
            Vector3 c = dice.transform.position;
            Fx.Shockwave(pal, c, WeaponDef.All[dice.TopNumber].color, radius);
            if (cam != null) cam.Shake(steps == 2 ? 0.45f : 0.25f);
            Slammed?.Invoke(steps);
            Sound.Play(steps == 2 ? Sfx.MegaSlam : Sfx.Slam, 1f, 0.03f);
            foreach (var e in Enemies.ToArray())
            {
                if (!e.Alive || e.Flying) continue;
                Vector3 d = e.pos - c; d.y = 0f;
                if (d.magnitude > radius + e.radius) continue;
                e.hp -= steps;
                e.hitFlash = 0.12f;
                e.vel += d.normalized * 7f;
                Fx.Text(e.Position + Vector3.up * 0.7f, steps == 2 ? "MEGA SLAM" : "SLAM", Palette.Hex("#FFFFFF"), 0.7f);
                if (e.hp <= 0f) Killed(e);
            }
        }

        // ---------------- Spawning ----------------

        public Enemy Spawn(EnemyKind kind, Vector3 pos)
        {
            var e = EnemyModels.Create(kind, pal, pos);
            Enemies.Add(e);
            Targets.All.Add(e);
            Fx.Flash(pal, pos + Vector3.up * 0.5f, Palette.Hex("#FF2A3D"), 1.2f, 0.25f);
            return e;
        }

        Vector3 SpawnPoint(System.Random rng)
        {
            float lim = World.HalfSize - 1.2f;
            for (int tries = 0; tries < 40; tries++)
            {
                var p = new Vector3((float)rng.NextDouble() * 2f * lim - lim, 0f, (float)rng.NextDouble() * 2f * lim - lim);
                if ((p - dice.transform.position).magnitude < 7f) continue;
                bool blocked = false;
                foreach (var ob in World.Obstacles) if (ob.Overlaps(p, 0.9f)) { blocked = true; break; }
                if (!blocked) return p;
            }
            return new Vector3(0f, 0f, lim);
        }

        readonly System.Random rng = new System.Random(3);

        void StartWave()
        {
            Wave++;
            Focus = Wave <= FocusOrder.Length ? FocusOrder[Wave - 1] : FocusOrder[rng.Next(1, FocusOrder.Length)];
            int unlocked = Mathf.Min(Wave, FocusOrder.Length);
            int count = 4 + Wave * 2;
            var list = new List<EnemyKind>();
            for (int i = 0; i < count; i++)
            {
                bool focus = i < count * 0.6f;
                var k = focus ? Focus : (Wave < 5 ? EnemyKind.Crawler : FocusOrder[rng.Next(unlocked)]);
                list.Add(k);
                if (k == EnemyKind.Mite) { list.Add(k); list.Add(k); } // mites come in threes
            }
            // Shuffle so the focus type trickles in with the rest.
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
            spawnQueue.Clear();
            foreach (var k in list) spawnQueue.Enqueue(k);
            spawnTimer = 0f;
            loop.ShowBanner($"WAVE {Wave}  —  {Enemy.Hint(Focus)}", 3.5f);
            Sound.Play(Sfx.WaveStart, 0.7f, 0f);
        }

        void StepSpawning(float dt)
        {
            if (spawnQueue.Count == 0) return;
            spawnTimer -= dt;
            if (spawnTimer > 0f) return;
            spawnTimer = Mathf.Max(0.9f, 2.2f - Wave * 0.12f);
            int group = Mathf.Min(spawnQueue.Count, 2 + Wave / 3);
            Vector3 anchor = SpawnPoint(rng);
            for (int i = 0; i < group; i++)
            {
                var e = Spawn(spawnQueue.Dequeue(), anchor + new Vector3(i % 2, 0f, i / 2) * 1.1f);
                e.hp *= 1f + 0.12f * (Wave - 1);
            }
        }
        // ---------------- Events from enemies ----------------

        public void Killed(Enemy e)
        {
            Score += e.kind == EnemyKind.Tank ? 50 : e.kind == EnemyKind.Mite ? 5 : 20;
            Fx.Explosion(pal, e.Position, Palette.Hex("#FF6A2A"), e.kind == EnemyKind.Tank ? 1.6f : 0.9f);
            Sound.Play(e.kind == EnemyKind.Tank ? Sfx.BigExplosion : Sfx.Explosion, e.kind == EnemyKind.Mite ? 0.5f : 0.9f);
            if (cam != null) cam.Shake(e.kind == EnemyKind.Tank ? 0.35f : 0.12f);
        }

        public void Deflect(Enemy e, string text)
        {
            if (deflectCooldown > 0f) return;
            deflectCooldown = 0.35f;
            Fx.Text(e.Position + Vector3.up * 0.8f, text, Palette.Hex("#FFD24A"), 0.9f);
            Fx.Flash(pal, e.Position, Palette.Hex("#FFD24A"), 0.35f, 0.08f);
            Sound.Play(Sfx.Deflect, 0.6f);
        }

        void Damage(int amount, Vector3 from)
        {
            if (invuln > 0f || dice.IsRolling || Lost || Won) return;
            Hp = Mathf.Max(0, Hp - amount);
            invuln = 1.2f;
            HurtFlash = 0.35f;
            Sound.Play(Sfx.Hurt);
            Fx.Flash(pal, dice.transform.position + Vector3.up * 0.5f, Palette.Hex("#FF2A3D"), 1.1f, 0.15f);
            if (cam != null) cam.Shake(0.3f);
            Vector3 push = dice.transform.position - from; push.y = 0f;
            dice.Knock(push.normalized * 3.5f);
            if (Lost) loop.ShowBanner($"DICE DESTROYED ON WAVE {Wave}  —  SCORE {Score}  —  press R to restart", 999f);
        }

        // ---------------- Update ----------------

        public void Step(float dt)
        {
            invuln -= dt;
            HurtFlash -= dt;
            deflectCooldown -= dt;
            Vector3 dp = dice.transform.position;

            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var e = Enemies[i];
                if (!e.Alive)
                {
                    Targets.All.Remove(e);
                    Enemies.RemoveAt(i);
                    if (e.t != null) Fx.Kill(e.t.gameObject);
                    continue;
                }
                StepEnemy(e, dt, dp);
            }
            Separate();
            StepBullets(dt, dp);

            if (!ManualSpawning && !Lost)
            {
                StepSpawning(dt);
                if (Enemies.Count == 0 && spawnQueue.Count == 0)
                {
                    waveDelay -= dt;
                    if (waveDelay <= 0f)
                    {
                        if (Wave > 0 && Hp < MaxHp) { Hp++; Fx.Text(dice.transform.position + Vector3.up * 2f, "+1 INTEGRITY", Palette.Hex("#3DFFD0"), 1.2f); }
                        StartWave();
                        waveDelay = 2.5f;
                    }
                }
            }
        }

        void StepEnemy(Enemy e, float dt, Vector3 dp)
        {
            e.spawnT = Mathf.Min(1f, e.spawnT + dt * 3f);
            e.t.localScale = Vector3.one * Mathf.Max(0.01f, e.spawnT);
            e.UpdateFlash(dt);
            if (Lost) return;

            Vector3 to = dp - e.pos; to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : Vector3.forward;
            Vector3 want;

            switch (e.kind)
            {
                case EnemyKind.Drone:
                    // Hover at a distance, circling, and shoot.
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    want = (dist > 4.5f ? dir : dist < 3f ? -dir : Vector3.zero) * e.speed + side * 1.4f;
                    Fire(e, dt, Mathf.Max(1.6f, 2.6f - Wave * 0.1f), 1);
                    break;
                case EnemyKind.Tank:
                    want = dist > 4f ? dir * e.speed : Vector3.zero;
                    Fire(e, dt, 3.0f, 1);
                    break;
                case EnemyKind.Mite:
                    want = (dir + Vector3.Cross(Vector3.up, dir) * Mathf.Sin(Time.time * 7f + e.GetHashCode()) * 0.6f).normalized * e.speed;
                    break;
                default:
                    want = dir * e.speed;
                    break;
            }

            e.vel = Vector3.Lerp(e.vel, want, 1f - Mathf.Exp(-4f * dt));
            e.pos += e.vel * dt;
            float lim = World.HalfSize - 0.6f;
            e.pos.x = Mathf.Clamp(e.pos.x, -lim, lim);
            e.pos.z = Mathf.Clamp(e.pos.z, -lim, lim);
            e.t.position = e.pos + (e.kind == EnemyKind.Drone ? Vector3.up * Mathf.Sin(Time.time * 3f + e.pos.x) * 0.12f : Vector3.zero);
            if (to.sqrMagnitude > 0.01f)
                e.t.rotation = Quaternion.Slerp(e.t.rotation, Quaternion.LookRotation(e.kind == EnemyKind.Drone ? dir : (e.vel.sqrMagnitude > 0.1f ? e.vel : dir)), 1f - Mathf.Exp(-8f * dt));

            // Contact damage (ground units only)
            if (!e.Flying && dist < e.radius + 0.5f)
            {
                Damage(1, e.pos);
                e.vel = -dir * 3f;
            }
        }

        void Fire(Enemy e, float dt, float interval, int dmg)
        {
            e.fireTimer += dt;
            if (e.fireTimer < interval || e.spawnT < 1f) return;
            e.fireTimer = Random.value * 0.5f;
            Vector3 from = e.Position + Vector3.up * (e.Flying ? -0.2f : 0.3f);
            Vector3 target = dice.transform.position + Vector3.up * 0.5f + dice.Velocity * 0.3f;
            var b = new Bullet { pos = from, life = 4f, dmg = dmg };
            b.vel = (target - from).normalized * (e.kind == EnemyKind.Tank ? 6f : 7.5f);
            var red = Palette.Hex("#FF2A3D");
            b.t = Prim.Make(PrimitiveType.Sphere, "EnemyShot", null, from, Vector3.one * (dmg > 1 ? 0.36f : 0.24f), pal.Glow("EnemyShot", red, 2f, red)).transform;
            bullets.Add(b);
            Fx.Flash(pal, from, red, 0.35f, 0.08f);
            Sound.Play(Sfx.EnemyShot, 0.35f);
        }

        void StepBullets(float dt, Vector3 dp)
        {
            Vector3 center = dp + Vector3.up * 0.5f;
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                b.life -= dt;
                b.pos += b.vel * dt;
                b.t.position = b.pos;
                bool done = b.life <= 0f || b.pos.y < 0f || Mathf.Abs(b.pos.x) > World.HalfSize || Mathf.Abs(b.pos.z) > World.HalfSize;
                if (!done && (b.pos - center).sqrMagnitude < 0.6f * 0.6f)
                {
                    Damage(b.dmg, b.pos);
                    done = true;
                }
                if (done)
                {
                    Fx.Flash(pal, b.pos, Palette.Hex("#FF2A3D"), 0.3f, 0.08f);
                    Fx.Kill(b.t.gameObject);
                    bullets.RemoveAt(i);
                }
            }
        }

        void Separate()
        {
            for (int i = 0; i < Enemies.Count; i++)
            for (int j = i + 1; j < Enemies.Count; j++)
            {
                var a = Enemies[i]; var b = Enemies[j];
                if (a.Flying != b.Flying) continue;
                Vector3 d = b.pos - a.pos; d.y = 0f;
                float min = a.radius + b.radius;
                float m = d.magnitude;
                if (m < min && m > 0.0001f)
                {
                    Vector3 push = d / m * (min - m) * 0.5f;
                    a.pos -= push; b.pos += push;
                }
            }
        }

        /// <summary>Which enemy types alive right now the current gun cannot hurt.</summary>
        public string Immune(WeaponDef w)
        {
            bool drone = false, tank = false;
            foreach (var e in Enemies)
            {
                if (!e.Alive || Enemy.CanHit(w, e.kind)) continue;
                if (e.kind == EnemyKind.Drone) drone = true;
                if (e.kind == EnemyKind.Tank) tank = true;
            }
            if (drone && tank) return "DRONES & TANKS";
            return drone ? "DRONES" : tank ? "TANKS" : null;
        }
    }
}
