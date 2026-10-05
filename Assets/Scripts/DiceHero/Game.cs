using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>Waves, bombs, the boss, enemy AI and fire, dice health, score and combo.</summary>
    public class Game
    {
        public static Game I { get; private set; }

        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly Bombs Bombs;
        public int Wave { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp => 6 + RunStats.Current.maxHpBonus;
        public bool Won { get; private set; }
        public bool Lost => Hp <= 0;
        public float HurtFlash { get; private set; }
        public int Score { get; private set; }

        // Combo: kills and disposals inside the window raise the multiplier (max x5).
        public int Combo { get; private set; } = 1;
        public int Chain { get; private set; }
        public float ComboTimer { get; private set; }
        public const float ComboWindow = 3f;

        // Run stats for the game-over screen.
        public int Kills, BombsDisposed, Rolls, BestCombo = 1;
        public float TimeAlive;

        /// <summary>True between "wave cleared" and the next wave starting (the upgrade pick happens here).</summary>
        public bool Intermission { get; private set; }
        public event System.Action<int> WaveCleared;
        public event System.Action<int> Slammed;
        public Enemy Boss { get; private set; }
        public bool FirstBombSeen { get; private set; }

        /// <summary>Set by tests/demo to script their own spawns instead of the wave director.</summary>
        public bool ManualSpawning;

        readonly DiceController dice;
        readonly Palette pal;
        readonly GameLoop loop;
        float invuln, waveDelay = 1.2f, deflectCooldown, bombTimer;
        int bombsLeft;
        readonly System.Random rng;

        class Bullet { public Transform t; public Vector3 pos, vel; public float life; public int dmg; }
        readonly List<Bullet> bullets = new List<Bullet>();

        // Each wave leans on one "problem" enemy type so the right gun matters.
        static readonly EnemyKind[] FocusOrder = { EnemyKind.Crawler, EnemyKind.Drone, EnemyKind.Tank, EnemyKind.Mite, EnemyKind.Bomber };
        readonly Queue<EnemyKind> spawnQueue = new Queue<EnemyKind>();
        float spawnTimer;
        public EnemyKind Focus { get; private set; }
        public bool BossWave => Wave > 0 && Wave % 5 == 0;
        /// <summary>Enemies on the field plus those still queued for this wave.</summary>
        public int EnemiesLeft => Enemies.Count + spawnQueue.Count;

        public Game(DiceController dice, Palette pal, GameLoop loop, int seed = 3)
        {
            I = this;
            this.dice = dice;
            this.pal = pal;
            this.loop = loop;
            rng = new System.Random(seed);
            Hp = MaxHp;
            Targets.All.Clear();
            Bombs = new Bombs(dice, pal, this);
            dice.TopChanged += (o, n) => OnRolled(dice.LastRollSteps);
        }

        // ---------------- Player actions ----------------

        void OnRolled(int steps)
        {
            Rolls++;
            Slam(steps);
            int every = RunStats.Current.repairEvery;
            if (every > 0 && Rolls % every == 0 && Hp < MaxHp)
            {
                Hp++;
                Fx.Text(dice.transform.position + Vector3.up * 2.2f, "+1 REPAIR", UiKit.Mint, 1f, 0.9f);
                Sound.Play(Sfx.Repair, 0.6f, 0f);
            }
        }

        /// <summary>Landing shockwave: damages and shoves every ground enemy nearby, and launches bombs.</summary>
        void Slam(int steps)
        {
            var s = RunStats.Current;
            float radius = (steps == 2 ? 3f : 2.2f) * s.slamRadiusMul;
            Vector3 c = dice.transform.position;
            Fx.Shockwave(pal, c, WeaponDef.All[dice.TopNumber].color, radius);
            Fx.Debris(pal, c + Vector3.up * 0.1f, Palette.Hex("#6A707C"), steps == 2 ? 10 : 6, steps == 2 ? 5f : 3.5f);
            Juice(steps == 2 ? 0.55f : 0.32f, steps == 2 ? 0.06f : 0.025f);
            Slammed?.Invoke(steps);
            Sound.Play(steps == 2 ? Sfx.MegaSlam : Sfx.Slam, 1f, 0.03f);
            Bombs.Slam(c, radius, steps);
            foreach (var e in Enemies.ToArray())
            {
                if (!e.Alive || e.Flying) continue;
                Vector3 d = e.pos - c; d.y = 0f;
                if (d.magnitude > radius + e.radius) continue;
                if (e.kind != EnemyKind.Boss) e.vel += d.normalized * 7f;
                BlastEnemy(e, steps + s.slamDamageBonus, c, steps == 2 ? "MEGA SLAM" : "SLAM");
            }
        }

        // ---------------- Spawning ----------------

        public Enemy Spawn(EnemyKind kind, Vector3 pos, int tier = 1)
        {
            var e = EnemyModels.Create(kind, pal, pos, tier);
            Enemies.Add(e);
            Targets.All.Add(e);
            Fx.Flash(pal, pos + Vector3.up * 0.5f, Palette.Hex("#FF2A3D"), kind == EnemyKind.Boss ? 3f : 1.2f, 0.25f);
            if (kind == EnemyKind.Boss) Boss = e;
            return e;
        }

        Vector3 SpawnPoint(float minDist = 7f, float clearance = 0.9f, float edge = 1.2f)
        {
            float lim = World.HalfSize - edge;
            for (int tries = 0; tries < 60; tries++)
            {
                var p = new Vector3((float)rng.NextDouble() * 2f * lim - lim, 0f, (float)rng.NextDouble() * 2f * lim - lim);
                if ((p - dice.transform.position).magnitude < minDist) continue;
                bool blocked = false;
                foreach (var ob in World.Obstacles) if (ob.Overlaps(p, clearance)) { blocked = true; break; }
                foreach (var b in Bombs.All) if ((b.pos - p).magnitude < 1.2f) { blocked = true; break; }
                if (!blocked) return p;
            }
            return new Vector3(0f, 0f, lim);
        }

        void StartWave()
        {
            Wave++;
            Intermission = false;
            spawnQueue.Clear();
            spawnTimer = 0.4f;
            bombsLeft = Wave < 2 ? 0 : 1 + (Wave - 1) / 2;
            bombTimer = Wave == 2 ? 3f : 5f;

            if (BossWave)
            {
                int tier = Wave / 5;
                var b = Spawn(EnemyKind.Boss, new Vector3(0f, 0f, 4f), tier);
                b.hp = b.maxHp;
                bombsLeft = 3 + tier * 2;
                bombTimer = 6f;
                loop.ShowBanner("BOSS: HIGH ROLLER", Enemy.Hint(EnemyKind.Boss), 4f, UiKit.Red);
                Sound.Play(Sfx.BossRoar, 1f, 0f);
                Juice(0.5f, 0f);
                return;
            }

            int slot = Wave - 1 - (Wave - 1) / 5; // skip boss waves in the focus order
            Focus = slot < FocusOrder.Length ? FocusOrder[slot] : FocusOrder[rng.Next(1, FocusOrder.Length)];
            int unlocked = Mathf.Min(slot + 1, FocusOrder.Length);
            int count = 4 + Wave * 2;
            var list = new List<EnemyKind>();
            for (int i = 0; i < count; i++)
            {
                bool focus = i < count * 0.6f;
                var k = focus ? Focus : (Wave < 5 ? EnemyKind.Crawler : FocusOrder[rng.Next(unlocked)]);
                if (k == EnemyKind.Bomber && list.FindAll(x => x == EnemyKind.Bomber).Count >= 2 + Wave / 6) k = EnemyKind.Crawler;
                list.Add(k);
                if (k == EnemyKind.Mite) { list.Add(k); list.Add(k); } // mites come in threes
            }
            // Shuffle so the focus type trickles in with the rest.
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
            foreach (var k in list) spawnQueue.Enqueue(k);
            loop.ShowBanner($"WAVE {Wave}", $"{Enemy.Plural(Focus)} INCOMING", 3.2f, UiKit.Gold);
            Sound.Play(Sfx.WaveStart, 0.7f, 0f);
        }

        void StepSpawning(float dt)
        {
            if (spawnQueue.Count > 0)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f)
                {
                    spawnTimer = Mathf.Max(0.9f, 2.2f - Wave * 0.12f);
                    int group = Mathf.Min(spawnQueue.Count, 2 + Wave / 3);
                    Vector3 anchor = SpawnPoint();
                    for (int i = 0; i < group; i++)
                    {
                        var e = Spawn(spawnQueue.Dequeue(), anchor + new Vector3(i % 2, 0f, i / 2) * 1.1f);
                        e.hp *= 1f + 0.12f * (Wave - 1);
                        e.maxHp = e.hp;
                    }
                }
            }
            if (bombsLeft > 0 && Enemies.Count > 0)
            {
                bombTimer -= dt;
                if (bombTimer <= 0f)
                {
                    bombsLeft--;
                    bombTimer = Mathf.Max(4f, 10f - Wave * 0.35f);
                    DropBomb(SpawnPoint(3.5f, 0.8f, 2.2f));
                }
            }
        }

        public void DropBomb(Vector3 at, float height = Bombs.DropHeight)
        {
            Bombs.Spawn(at, height);
            if (!FirstBombSeen)
            {
                FirstBombSeen = true;
                loop.ShowBanner("BOMB INCOMING", "SHOVE IT OFF THE EDGE BEFORE THE FUSE RUNS OUT", 3.5f, UiKit.Red);
            }
        }

        // ---------------- Events ----------------

        public void Killed(Enemy e, bool byBomb = false)
        {
            Kills++;
            int basePts = e.kind == EnemyKind.Boss ? 2000 : e.kind == EnemyKind.Tank ? 50 : e.kind == EnemyKind.Bomber ? 30 : e.kind == EnemyKind.Mite ? 5 : 20;
            if (byBomb) basePts *= 2;
            AddScore(basePts, e.Position + Vector3.up * 0.4f, byBomb);
            float size = e.kind == EnemyKind.Boss ? 4f : e.kind == EnemyKind.Tank ? 1.6f : 0.9f;
            Fx.Explosion(pal, e.Position, Palette.Hex("#FF6A2A"), size);
            Fx.Debris(pal, e.Position, Palette.Hex("#2E323C"), e.kind == EnemyKind.Mite ? 3 : e.kind == EnemyKind.Boss ? 30 : 8, e.kind == EnemyKind.Boss ? 9f : 4.5f);
            Fx.Sparks(pal, e.Position, Palette.Hex("#FF6A2A"), e.kind == EnemyKind.Mite ? 3 : 8);
            Sound.Play(e.kind == EnemyKind.Tank || e.kind == EnemyKind.Boss ? Sfx.BigExplosion : Sfx.Explosion, e.kind == EnemyKind.Mite ? 0.5f : 0.9f);
            Juice(e.kind == EnemyKind.Boss ? 1f : e.kind == EnemyKind.Tank ? 0.35f : 0.12f, e.kind == EnemyKind.Boss ? 0.35f : e.kind == EnemyKind.Tank ? 0.05f : 0.015f);

            if (e.kind == EnemyKind.Boss)
            {
                Boss = null;
                Flash(0.6f);
                loop.ShowBanner("HIGH ROLLER DESTROYED", $"+{2000 * Combo} POINTS", 3.5f, UiKit.Gold);
                Hp = MaxHp;
                // Clear the stage: remaining bombs fizzle, minions pop.
                Bombs.Clear();
                foreach (var o in Enemies) if (o != e && o.Alive) { o.hp = 0f; Fx.Explosion(pal, o.Position, Palette.Hex("#FF6A2A"), 0.8f); }
            }
            else if (RunStats.Current.chainChance > 0f && rng.NextDouble() < RunStats.Current.chainChance)
            {
                // Chain reaction: a small blast that can take neighbours with it.
                Vector3 c = e.pos;
                Fx.Shockwave(pal, c, Palette.Hex("#FF3FA4"), 1.4f);
                foreach (var o in Enemies.ToArray())
                {
                    if (o == e || !o.Alive || o.kind == EnemyKind.Boss) continue;
                    Vector3 d = o.pos - c; d.y = 0f;
                    if (d.magnitude < 1.4f + o.radius) BlastEnemy(o, 2f, c, "CHAIN");
                }
            }
        }

        /// <summary>Damage that ignores weaknesses (bombs, slams, chain reactions).</summary>
        public void BlastEnemy(Enemy e, float dmg, Vector3 from, string label)
        {
            if (!e.Alive) return;
            e.hp -= dmg;
            e.hitFlash = 0.12f;
            Fx.Text(e.Position + Vector3.up * 0.8f, label, Color.white, 0.7f, 0.8f);
            if (e.hp <= 0f) Killed(e, label == "BOOM" || label == "BOWLED");
        }

        public void BombDisposed(Vector3 at)
        {
            BombsDisposed++;
            Vector3 p = new Vector3(Mathf.Clamp(at.x, -World.HalfSize + 1f, World.HalfSize - 1f), 1.2f, Mathf.Clamp(at.z, -World.HalfSize + 1f, World.HalfSize - 1f));
            AddScore(100, p, false, "DISPOSED");
            Sound.Play(Sfx.Disposed, 0.8f, 0f);
        }

        void AddScore(int pts, Vector3 at, bool big, string label = null)
        {
            // Every 3 chained kills/disposals inside the window raise the multiplier by one.
            Chain = ComboTimer > 0f ? Chain + 1 : 1;
            Combo = Mathf.Min(5, 1 + (Chain - 1) / 3);
            int gained = pts * Combo;
            Score += gained;
            ComboTimer = ComboWindow;
            BestCombo = Mathf.Max(BestCombo, Combo);
            string txt = label != null ? $"+{gained} {label}" : $"+{gained}";
            Fx.Text(at, txt, big ? UiKit.Gold : new Color(1f, 0.95f, 0.8f), big || label != null ? 1.1f : 0.8f, big || label != null ? 1.2f : 0.75f);
        }

        public void DamageNumber(Enemy e, float dmg, Color c)
        {
            Fx.Text(e.Position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f, Mathf.CeilToInt(dmg).ToString(), c, 0.55f, 0.6f);
        }

        public void Deflect(Enemy e, string text)
        {
            if (deflectCooldown > 0f) return;
            deflectCooldown = 0.35f;
            Fx.Text(e.Position + Vector3.up * 0.8f, text, Palette.Hex("#FFD24A"), 0.9f, 0.8f);
            Fx.Flash(pal, e.Position, Palette.Hex("#FFD24A"), 0.35f, 0.08f);
            Sound.Play(Sfx.Deflect, 0.6f);
        }

        public void Juice(float shake, float hitStop) => loop.Juice(shake, hitStop);
        public void Flash(float amount) => loop.FlashScreen(amount);

        public void HurtPlayer(int amount, Vector3 from)
        {
            if (invuln > 0f || dice.IsRolling || Lost || Won || amount <= 0) return;
            if (Invincible) { invuln = 1.2f; HurtFlash = 0.2f; Juice(0.3f, 0.03f); return; }
            Hp = Mathf.Max(0, Hp - amount);
            invuln = 1.2f;
            HurtFlash = 0.45f;
            Combo = 1;
            Chain = 0;
            ComboTimer = 0f;
            Sound.Play(Sfx.Hurt);
            Fx.Flash(pal, dice.transform.position + Vector3.up * 0.5f, Palette.Hex("#FF2A3D"), 1.1f, 0.15f);
            Juice(0.45f, 0.08f);
            Vector3 push = dice.transform.position - from; push.y = 0f;
            dice.Knock(push.normalized * 3.5f);
            if (Lost)
            {
                Fx.Explosion(pal, dice.transform.position + Vector3.up * 0.5f, UiKit.Gold, 2.5f);
                Fx.Debris(pal, dice.transform.position + Vector3.up * 0.5f, UiKit.Gold, 16, 7f);
                Sound.Play(Sfx.BigExplosion, 1f, 0f);
                Juice(1f, 0.25f);
            }
        }

        public bool Invulnerable => invuln > 0f;
        /// <summary>Demo capture "god mode": hits still flash but never cost integrity.</summary>
        public bool Invincible;

        // ---------------- Update ----------------

        public void Step(float dt)
        {
            invuln -= dt;
            HurtFlash -= dt;
            deflectCooldown -= dt;
            if (!Lost) TimeAlive += dt;
            ComboTimer -= dt;
            if (ComboTimer <= 0f) { Combo = 1; Chain = 0; }
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
            Bombs.Step(dt);

            if (ManualSpawning || Lost || Intermission) return;
            StepSpawning(dt);
            bool clear = Enemies.Count == 0 && spawnQueue.Count == 0;
            if (!clear) { waveDelay = 1f; return; }
            waveDelay -= dt;
            if (waveDelay > 0f) return;
            if (Wave == 0) { StartWave(); return; }

            // Wave cleared: small repair, then the loop shows the upgrade pick and calls StartNextWave().
            Intermission = true;
            if (Hp < MaxHp) { Hp++; Fx.Text(dice.transform.position + Vector3.up * 2f, "+1 INTEGRITY", UiKit.Mint, 1.2f, 1f); }
            Sound.Play(Sfx.WaveClear, 0.8f, 0f);
            WaveCleared?.Invoke(Wave);
        }

        /// <summary>Starts the next wave (after the upgrade pick).</summary>
        public void StartNextWave() => StartWave();

        /// <summary>Full repair (the Reinforced Shell upgrade).</summary>
        public void RepairFull() => Hp = MaxHp;

        void StepEnemy(Enemy e, float dt, Vector3 dp)
        {
            e.spawnT = Mathf.Min(1f, e.spawnT + dt * (e.kind == EnemyKind.Boss ? 1.2f : 3f));
            float pop = e.spawnT < 1f ? Mathf.Sin(e.spawnT * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(e.spawnT * Mathf.PI)) : 1f;
            e.t.localScale = Vector3.one * Mathf.Max(0.01f, pop);
            e.UpdateFlash(dt);
            if (Lost) return;

            if (e.kind == EnemyKind.Boss) { StepBoss(e, dt, dp); return; }

            Vector3 to = dp - e.pos; to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 want;

            switch (e.kind)
            {
                case EnemyKind.Drone:
                    // Hover at a distance, circling, and shoot.
                    want = (dist > 4.5f ? dir : dist < 3f ? -dir : Vector3.zero) * e.speed + side * 1.4f;
                    Fire(e, dt, Mathf.Max(1.6f, 2.6f - Wave * 0.1f), 1);
                    break;
                case EnemyKind.Tank:
                    want = dist > 4f ? dir * e.speed : Vector3.zero;
                    Fire(e, dt, 3.0f, 1);
                    break;
                case EnemyKind.Mite:
                    want = (dir + side * Mathf.Sin(Time.time * 7f + e.GetHashCode()) * 0.6f).normalized * e.speed;
                    break;
                case EnemyKind.Bomber:
                    // Keep mid range, strafe, and plant bombs.
                    want = (dist > 6.5f ? dir : dist < 4.5f ? -dir : Vector3.zero) * e.speed + side * 1.2f;
                    e.abilityTimer -= dt;
                    if (e.abilityTimer <= 0f && e.spawnT >= 1f && Bombs.All.Count < 6)
                    {
                        e.abilityTimer = Mathf.Max(5f, 8f - Wave * 0.15f);
                        Vector3 at = e.pos - e.t.forward * 0.95f;
                        float lim = World.HalfSize - 1f;
                        at.x = Mathf.Clamp(at.x, -lim, lim); at.z = Mathf.Clamp(at.z, -lim, lim); at.y = 0f;
                        DropBomb(at, 0f);
                        Sound.Play(Sfx.Thud, 0.6f);
                    }
                    break;
                default:
                    want = dir * e.speed;
                    break;
            }

            e.vel = Vector3.Lerp(e.vel, want, 1f - Mathf.Exp(-4f * dt));
            e.pos += e.vel * dt;
            float lim2 = World.HalfSize - 0.6f;
            e.pos.x = Mathf.Clamp(e.pos.x, -lim2, lim2);
            e.pos.z = Mathf.Clamp(e.pos.z, -lim2, lim2);
            e.t.position = e.pos + (e.kind == EnemyKind.Drone ? Vector3.up * Mathf.Sin(Time.time * 3f + e.pos.x) * 0.12f : Vector3.zero);
            if (to.sqrMagnitude > 0.01f)
                e.t.rotation = Quaternion.Slerp(e.t.rotation, Quaternion.LookRotation(e.kind == EnemyKind.Drone ? dir : (e.vel.sqrMagnitude > 0.1f ? e.vel : dir)), 1f - Mathf.Exp(-8f * dt));

            // Contact damage (ground units only)
            if (!e.Flying && dist < e.radius + 0.5f)
            {
                HurtPlayer(1, e.pos);
                e.vel = -dir * 3f;
            }
        }

        // ---------------- Boss: High Roller ----------------

        void StepBoss(Enemy e, float dt, Vector3 dp)
        {
            var b = e.boss;
            float enrage = e.hp < e.maxHp * 0.4f ? 0.7f : 1f;
            Vector3 to = dp - e.pos; to.y = 0f;

            if (!b.Tumbling)
            {
                if (e.spawnT < 1f) return;
                b.rest -= dt;
                // Aimed triple burst while resting.
                e.fireTimer += dt;
                if (e.fireTimer > 1.5f * enrage)
                {
                    e.fireTimer = 0f;
                    Vector3 from = e.pos + Vector3.up * 1f;
                    for (int i = -1; i <= 1; i++)
                        FireBullet(from, Quaternion.Euler(0f, i * 14f, 0f) * (dp + Vector3.up * 0.5f - from).normalized * 7f, 1, 0.3f);
                    Sound.Play(Sfx.EnemyShot, 0.6f);
                }
                if (b.rest <= 0f) StartTumble(e, to);
            }
            else
            {
                b.tumbleT += dt / 0.55f;
                float u = Mathf.Clamp01(b.tumbleT);
                float ease = u * u * (3f - 2f * u);
                float angle = 90f * ease;
                b.orientation = Quaternion.AngleAxis(angle, b.axis) * b.startRot;
                e.pos = Vector3.Lerp(b.from, b.to, ease);
                float a = angle * Mathf.Deg2Rad;
                float half = BossState.Size * 0.5f;
                float lift = half * (Mathf.Abs(Mathf.Cos(a)) + Mathf.Abs(Mathf.Sin(a))) - half;
                b.model.Body.localPosition = new Vector3(0f, half + lift * 0.9f, 0f);
                if (u >= 1f) LandBoss(e, dp);
            }
            b.model.Body.localRotation = b.orientation;
            e.t.position = e.pos;

            // Touching it hurts.
            if (to.magnitude < BossState.Size * 0.5f + 0.5f)
            {
                HurtPlayer(1, e.pos);
                dice.Knock(to.normalized * 6f);
            }
        }

        void StartTumble(Enemy e, Vector3 toPlayer)
        {
            var b = e.boss;
            Vector3 primary = Mathf.Abs(toPlayer.x) > Mathf.Abs(toPlayer.z) ? new Vector3(Mathf.Sign(toPlayer.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toPlayer.z));
            Vector3 secondary = Mathf.Abs(toPlayer.x) > Mathf.Abs(toPlayer.z) ? new Vector3(0f, 0f, Mathf.Sign(toPlayer.z == 0f ? 1f : toPlayer.z)) : new Vector3(Mathf.Sign(toPlayer.x == 0f ? 1f : toPlayer.x), 0f, 0f);
            Vector3[] options = { primary, secondary, -secondary, -primary };
            float lim = World.HalfSize - BossState.Size * 0.5f - 0.2f;
            foreach (var d in options)
            {
                Vector3 dest = e.pos + d * BossState.Size;
                if (Mathf.Abs(dest.x) > lim || Mathf.Abs(dest.z) > lim) continue;
                bool blocked = false;
                foreach (var ob in World.Obstacles) if (ob.Overlaps(dest, BossState.Size * 0.5f)) { blocked = true; break; }
                if (blocked) continue;
                b.from = e.pos;
                b.to = dest;
                b.axis = Vector3.Cross(Vector3.up, d);
                b.startRot = b.orientation;
                b.tumbleT = 0f;
                return;
            }
            b.rest = 1f; // boxed in; try again shortly
        }

        void LandBoss(Enemy e, Vector3 dp)
        {
            var b = e.boss;
            b.tumbleT = -1f;
            b.orientation = Quaternion.LookRotation(SnapAxis(b.orientation * Vector3.forward), SnapAxis(b.orientation * Vector3.up));
            b.model.Body.localPosition = new Vector3(0f, BossState.Size * 0.5f, 0f);
            b.tumbles++;
            float enrage = e.hp < e.maxHp * 0.4f ? 0.7f : 1f;
            // Rolls twice in a row, then rests: the rest is the window to match its number.
            b.rest = b.tumbles % 2 == 1 ? 0.15f : Mathf.Max(3.2f, 4.6f - b.tier * 0.4f) * enrage;

            Fx.Shockwave(pal, e.pos, UiKit.Red, 3f);
            Fx.Debris(pal, e.pos + Vector3.up * 0.1f, Palette.Hex("#6A707C"), 10, 5f);
            Sound.Play(Sfx.MegaSlam, 1f, 0.02f);
            Juice(0.5f, 0.03f);
            Vector3 to = dp - e.pos; to.y = 0f;
            if (to.magnitude < 2.6f) { HurtPlayer(1, e.pos); dice.Knock(to.normalized * 7f); }
            Bombs.Slam(e.pos, 2.6f, 1);

            // Ring of bullets.
            int count = 8 + b.tier * 2 + (enrage < 1f ? 4 : 0);
            float offset = b.tumbles * 11f;
            for (int i = 0; i < count; i++)
            {
                Vector3 d = Quaternion.Euler(0f, offset + i * 360f / count, 0f) * Vector3.forward;
                FireBullet(e.pos + Vector3.up * 0.5f + d * 1.2f, d * 5.5f, 1, 0.28f);
            }
            if (b.tumbles % 3 == 0)
                for (int i = 0; i < 1 + b.tier; i++) Spawn(EnemyKind.Mite, e.pos + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 1.6f);
        }

        static Vector3 SnapAxis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            if (ay >= az) return new Vector3(0f, Mathf.Sign(v.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        // ---------------- Enemy fire ----------------

        void Fire(Enemy e, float dt, float interval, int dmg)
        {
            e.fireTimer += dt;
            if (e.fireTimer < interval || e.spawnT < 1f) return;
            e.fireTimer = Random.value * 0.5f;
            Vector3 from = e.Position + Vector3.up * (e.Flying ? -0.2f : 0.3f);
            Vector3 target = dice.transform.position + Vector3.up * 0.5f + dice.Velocity * 0.3f;
            FireBullet(from, (target - from).normalized * (e.kind == EnemyKind.Tank ? 6f : 7.5f), dmg, dmg > 1 ? 0.36f : 0.24f);
            Sound.Play(Sfx.EnemyShot, 0.35f);
        }

        void FireBullet(Vector3 from, Vector3 vel, int dmg, float size)
        {
            var red = Palette.Hex("#FF2A3D");
            var b = new Bullet { pos = from, life = 4f, dmg = dmg, vel = vel };
            b.t = Prim.Make(PrimitiveType.Sphere, "EnemyShot", null, from, Vector3.one * size, pal.Glow("EnemyShot", red, 2.4f, red)).transform;
            bullets.Add(b);
            Fx.Flash(pal, from, red, 0.35f, 0.08f);
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
                    HurtPlayer(b.dmg, b.pos);
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
                    // The boss doesn't get shoved around by its minions.
                    float wa = a.kind == EnemyKind.Boss ? 0f : b.kind == EnemyKind.Boss ? 1f : 0.5f;
                    Vector3 push = d / m * (min - m);
                    a.pos -= push * wa; b.pos += push * (1f - wa);
                }
            }
        }

        /// <summary>What the current gun can't hurt right now (for the HUD warning), or null.</summary>
        public string Immune(WeaponDef w)
        {
            bool drone = false, tank = false, boss = false;
            foreach (var e in Enemies)
            {
                if (!e.Alive || e.CanBeHitBy(w)) continue;
                if (e.kind == EnemyKind.Drone) drone = true;
                if (e.kind == EnemyKind.Tank) tank = true;
                if (e.kind == EnemyKind.Boss) boss = true;
            }
            if (boss) return $"THE HIGH ROLLER (NEEDS A {Boss?.Weakness})";
            if (drone && tank) return "DRONES & TANKS";
            return drone ? "DRONES" : tank ? "TANKS" : null;
        }

        /// <summary>Removes every bullet (used when the run ends).</summary>
        public void ClearBullets()
        {
            foreach (var b in bullets) Fx.Kill(b.t.gameObject);
            bullets.Clear();
        }
    }
}
