using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>Waves, bombs, the boss, enemy AI and fire, dice health, score and combo.</summary>
    public partial class Game
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
        public int Kills, BombsDisposed, Rolls, BestCombo = 1, BossesBeaten;
        public float TimeAlive;

        /// <summary>True between "wave cleared" and the next wave starting (the upgrade pick happens here).</summary>
        public bool Intermission { get; private set; }
        public event System.Action<int> WaveCleared;
        public event System.Action<int> Slammed;
        public Enemy Boss { get; private set; }
        /// <summary>The deck's foreman boss (deck 1: the Compactor), if one is on the field.</summary>
        public Enemy Foreman { get; private set; }
        public bool FirstBombSeen { get; private set; }

        /// <summary>Campaign stage being played: its scripted waves replace the endless wave director (null: gauntlet).</summary>
        public StageDef Stage;
        /// <summary>The last scripted wave of the stage is cleared.</summary>
        public event System.Action StageCleared;
        /// <summary>Damage taken (cause), and a hit the roll's shield absorbed.</summary>
        public event System.Action<string> Hurt;
        public event System.Action Dodged;
        public int HitsTaken { get; private set; }

        /// <summary>Set by tests/demo to script their own spawns instead of the wave director.</summary>
        public bool ManualSpawning;

        readonly DiceController dice;
        readonly Palette pal;
        readonly GameLoop loop;
        float invuln, waveDelay = 1.2f, deflectCooldown, bombTimer;
        int bombsLeft;
        readonly System.Random rng;

        class Bullet { public Transform t; public Vector3 pos, vel; public float life, age; public int dmg; public bool tumble; }
        readonly List<Bullet> bullets = new List<Bullet>();

        /// <summary>
        /// The closest thing about to hit the die within 'within' seconds: an enemy bullet on course, or a ground enemy
        /// (or the boss) closing in. Returns where it comes from, or null. Used by the autopilot to time dodge rolls.
        /// </summary>
        public Vector3? IncomingThreat(Vector3 p, float within = 0.35f)
        {
            Vector3? best = null; float bestT = within;
            foreach (var b in bullets)
            {
                Vector3 rel = p + Vector3.up * 0.5f - b.pos, v = b.vel;
                float t = Vector3.Dot(rel, v) / Mathf.Max(0.01f, v.sqrMagnitude);
                if (t < 0f || t > bestT) continue;
                if ((rel - v * t).magnitude < 0.75f) { bestT = t; best = b.pos; }
            }
            foreach (var e in Enemies)
            {
                if (!e.Alive || e.Flying) continue;
                Vector3 d = e.pos - p; d.y = 0f;
                // A charging Compactor covers ground fast: get out of its lane early.
                if (e.kind == EnemyKind.Compactor && (e.mode == 1 || e.mode == 2) && d.magnitude < 7f)
                {
                    Vector3 rel = p - e.pos; rel.y = 0f;
                    float along = Vector3.Dot(rel, e.chargeDir), across = (rel - e.chargeDir * along).magnitude;
                    if (along > 0f && across < e.radius + 0.7f) return e.pos;
                }
                float reach = e.radius + 0.5f + (e.IsBig ? 1.4f : 0.6f);
                if (d.magnitude < reach) return e.pos;
            }
            return best;
        }

        // Each wave leans on one "problem" enemy type so the right gun matters.
        static readonly EnemyKind[] FocusOrder = { EnemyKind.Crawler, EnemyKind.Drone, EnemyKind.Tank, EnemyKind.Mite, EnemyKind.Bomber };
        readonly Queue<EnemyKind> spawnQueue = new Queue<EnemyKind>();
        float spawnTimer;
        int coatTurn;
        static bool tripTold;
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
            dice.ForcedTrip += () =>
            {
                Fx.Text(dice.transform.position + Vector3.up * 1.6f, "TRIPPED!", Palette.Hex("#FFB020"), 0.9f, 1f);
                if (!tripTold) { tripTold = true; loop.ShowBanner(null, "LOW PIPES TRIP YOU: YOU TIP OVER, ONTO THE OPPOSITE FACE", 3f, Palette.Hex("#FFB020")); }
            };
        }

        // ---------------- Player actions ----------------

        void OnRolled(int steps)
        {
            Rolls++;
            Slam(steps);
            int every = RunStats.Current.RepairEvery;
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
            // A button roll is cheap, so its landing stomp is smaller; a vault keeps the big one.
            float radius = (steps == 2 ? 3f : DiceController.ButtonMode ? 1.7f : 2.2f) * s.slamRadiusMul;
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
                if (!e.IsBig) e.vel += d.normalized * 7f;
                if (e.Crushable) BlastEnemy(e, steps + s.slamDamageBonus, c, e.Low ? "CRUSHED" : steps == 2 ? "MEGA SLAM" : "SLAM");
            }
        }

        // ---------------- Spawning ----------------

        public Enemy Spawn(EnemyKind kind, Vector3 pos, int tier = 1)
        {
            var e = EnemyModels.Create(kind, pal, pos, tier);
            Enemies.Add(e);
            Targets.All.Add(e);
            Fx.Flash(pal, pos + Vector3.up * 0.5f, Palette.Hex("#FF2A3D"), kind == EnemyKind.Boss ? 3f : 1.2f, 0.25f);
            if (kind == EnemyKind.Boss) { Boss = e; e.boss.defence = BossDefence(e.boss.tier, Defence.Bare); ShowBossDefence(e); }
            if (e.IsForeman) Foreman = e;
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

            if (Stage != null) { StartStageWave(); return; }
            if (BossWave)
            {
                int tier = Wave / 5;
                var b = Spawn(EnemyKind.Boss, new Vector3(0f, 0f, 4f), tier);
                b.hp = b.maxHp;
                bombsLeft = 3 + tier * 2;
                bombTimer = 6f;
                loop.ShowBanner("BOSS: HIGH ROLLER", BossLine(b.boss.defence) + "  ·  " + Enemy.Hint(EnemyKind.Boss), 4f, UiKit.Red);
                Sound.Play(Sfx.BossRoar, 1f, 0f);
                Juice(0.5f, 0f);
                return;
            }

            int slot = Wave - 1 - (Wave - 1) / 5; // skip boss waves in the focus order
            Focus = slot < FocusOrder.Length ? FocusOrder[slot] : FocusOrder[rng.Next(1, FocusOrder.Length)];
            int unlocked = Mathf.Min(slot + 1, FocusOrder.Length);
            int count = 8 + Wave * 2; // early waves used to end in under 10 s
            var list = new List<EnemyKind>();
            for (int i = 0; i < count; i++)
            {
                bool focus = i < count * 0.6f;
                var k = focus ? Focus : (Wave < 5 ? EnemyKind.Crawler : FocusOrder[rng.Next(unlocked)]);
                if (k == EnemyKind.Bomber && list.FindAll(x => x == EnemyKind.Bomber).Count >= 2 + Wave / 6) k = EnemyKind.Crawler;
                list.Add(k);
                if (k == EnemyKind.Mite)
                {
                    // Mites come in pairs, and only so many: they must be crushed one roll at a time.
                    if (list.FindAll(x => x == EnemyKind.Mite).Count > 4 + Wave / 2) list[list.Count - 1] = EnemyKind.Crawler;
                    else list.Add(k);
                }
            }
            // Shuffle so the focus type trickles in with the rest.
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
            foreach (var k in list) spawnQueue.Enqueue(k);
            loop.ShowBanner($"WAVE {Wave}", $"{Enemy.Plural(Focus)} INCOMING", 3.2f, UiKit.Gold);
            Sound.Play(Sfx.WaveStart, 0.7f, 0f);
        }

        void StartStageWave()
        {
            var w = Stage.waves[Mathf.Min(Wave, Stage.waves.Length) - 1];
            bombsLeft = w.bombs;
            bombTimer = 4f;
            var list = new List<EnemyKind>();
            if (w.boss.HasValue)
            {
                var b = Spawn(w.boss.Value, new Vector3(0f, 0f, 5f), w.bossTier);
                b.hp = b.maxHp = b.maxHp * w.bossHealth * (Settings.Hard ? 1.3f : 1f);
                loop.ShowBanner((b.kind == EnemyKind.Boss ? "THE HOUSE: " : "FOREMAN: ") + Enemy.Plural(w.boss.Value),
                    b.kind == EnemyKind.Boss ? BossLine(b.boss.defence) : Enemy.Hint(w.boss.Value), 4.5f, UiKit.Red);
                Sound.Play(Sfx.BossRoar, 1f, 0f);
                Juice(0.5f, 0f);
                if (w.radio != null) loop.Radio(w.radio);
                return;
            }
            foreach (var (kind, count) in w.enemies) for (int i = 0; i < count; i++) list.Add(kind);
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
            foreach (var k in list) spawnQueue.Enqueue(k);
            Focus = w.enemies[0].kind;
            loop.ShowBanner($"WAVE {Wave} / {Stage.waves.Length}", w.title, 3f, UiKit.Gold);
            if (w.radio != null) loop.Radio(w.radio);
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
                        var coats = Stage != null && Wave >= 1 && Wave <= Stage.waves.Length ? Stage.waves[Wave - 1].coats : null;
                        if (coats != null && coats.Length > 0 && (e.kind == EnemyKind.Crawler || e.kind == EnemyKind.Bomber)) GiveCoat(e, coats[coatTurn++ % coats.Length]);
                        if (Stage == null) e.hp *= 1f + 0.12f * (Wave - 1); // stages set their own difficulty
                        else if (Settings.Hard) e.hp *= 1.35f;
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

            if (e.IsForeman)
            {
                Foreman = null;
                BossesBeaten++;
                Flash(0.6f);
                Juice(1f, 0.35f);
                loop.ShowBanner(Enemy.Plural(e.kind) + " DESTROYED", "IT DROPPED A MODULE", 3.5f, UiKit.Gold);
                Hp = MaxHp;
                Bombs.Clear();
                foreach (var o in Enemies) if (o != e && o.Alive) { o.hp = 0f; Fx.Explosion(pal, o.Position, Palette.Hex("#FF6A2A"), 0.8f); }
            }
            if (e.kind == EnemyKind.Boss)
            {
                Boss = null;
                BossesBeaten++;
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

        public void HurtPlayer(int amount, Vector3 from, string cause = "hit")
        {
            if (dice.Shielded && invuln <= 0f && !Lost && !Won && amount > 0) { Dodged?.Invoke(); return; }
            if (invuln > 0f || dice.Shielded || Lost || Won || amount <= 0) return;
            if (Invincible) { invuln = 1.2f; HurtFlash = 0.2f; Juice(0.3f, 0.03f); return; }
            Hp = Mathf.Max(0, Hp - amount);
            HitsTaken++;
            Hurt?.Invoke(cause);
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

            MitesOnPip = 0;
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var e = Enemies[i];
                if (!e.Alive)
                {
                    Targets.All.Remove(e);
                    Enemies.RemoveAt(i);
                    if (e.t != null) Fx.Kill(e.t.gameObject);
                    if (e.lane != null) Object.Destroy(e.lane.gameObject);
                    continue;
                }
                StepEnemy(e, dt, dp);
            }
            Separate();
            if (Hazards.Step(dt, dp)) HurtPlayer(1, dp, "vent");
            StepBullets(dt, dp);
            Bombs.Step(dt);

            if (ManualSpawning || Lost || Intermission) return;
            StepSpawning(dt);
            bool clear = Enemies.Count == 0 && spawnQueue.Count == 0;
            if (!clear) { waveDelay = 1f; return; }
            waveDelay -= dt;
            if (waveDelay > 0f) return;
            if (Wave == 0) { StartWave(); return; }
            if (Stage != null)
            {
                // Stages run their waves back to back; cards come between stages, not waves.
                if (Wave < Stage.waves.Length) { StartWave(); return; }
                CompleteStage();
                return;
            }

            // Wave cleared: small repair, then the loop shows the upgrade pick and calls StartNextWave().
            Intermission = true;
            if (Hp < MaxHp) { Hp++; Fx.Text(dice.transform.position + Vector3.up * 2f, "+1 INTEGRITY", UiKit.Mint, 1.2f, 1f); }
            Sound.Play(Sfx.WaveClear, 0.8f, 0f);
            WaveCleared?.Invoke(Wave);
        }

        /// <summary>Starts the next wave (after the upgrade pick).</summary>
        public void StartNextWave() => StartWave();

        /// <summary>Coats a robot in vines, ice or an energy shield (Bare: leaves it as it is). Only the coat's counter breaks it.</summary>
        public void GiveCoat(Enemy e, Defence coat, float hp = 0f)
        {
            if (coat == Defence.Bare) return;
            e.coat = coat;
            e.coatHp = e.coatMax = hp > 0f ? hp : coat == Defence.Ice ? 5f : 4f;
            if (e.coatFx != null) Object.Destroy(e.coatFx.gameObject);
            e.coatFx = CoatModels.Build(pal, e.t, coat, e.radius + 0.14f, 0.45f);
        }

        public void CoatBroken(Enemy e)
        {
            if (e.coatFx != null) Object.Destroy(e.coatFx.gameObject);
            e.coatFx = null;
            Color c = CoatModels.Tint(e.coat);
            string text = e.coat == Defence.Vines ? "VINES BURNT" : e.coat == Defence.Ice ? "ICE MELTED" : "SHIELD DOWN";
            Fx.Shockwave(pal, e.pos, c, 1.2f + (e.IsBig ? 1.5f : 0f));
            Fx.Debris(pal, e.Position, c, 6, 3.5f);
            Fx.Text(e.Position + Vector3.up * 0.9f, text, c, 0.8f, 0.8f);
            Sound.Play(Sfx.Clonk, 0.8f, 0f);
            if (e.kind == EnemyKind.Smelter) loop.ShowBanner(null, "CORE SHIELD DOWN · ANY GUN", 2f, UiKit.Gold);
        }

        // ---------------- High Roller defences ----------------

        static readonly Defence[] BossDefences = { Defence.Steel, Defence.Vines, Defence.Ice, Defence.Shield };

        /// <summary>A defence for the next phase that one of Pip's guns gets through (so the fight can always be won), not the current one.</summary>
        static Defence BossDefence(int seed, Defence not)
        {
            var options = new List<Defence>();
            foreach (var d in BossDefences) if (Enemy.CounterFace(d) > 0 && d != not) options.Add(d);
            if (options.Count == 0) return Enemy.CounterFace(not) > 0 && not != Defence.Bare ? not : Defence.Bare;
            return options[new System.Random(seed).Next(options.Count)];
        }

        static string BossLine(Defence d) => d == Defence.Bare ? "NO ARMOUR: ANY GUN" : Enemy.DefenceName(d) + ": " + Enemy.Beaters(d);

        /// <summary>Dresses the High Roller in this phase's defence (plates, vines, ice or a shield ring).</summary>
        void ShowBossDefence(Enemy e)
        {
            if (e.coatFx != null) Object.Destroy(e.coatFx.gameObject);
            e.coatFx = e.boss.defence == Defence.Bare ? null : CoatModels.Build(pal, e.boss.model.Body, e.boss.defence, BossState.Size * 0.62f, 0f, true);
        }

        /// <summary>Distance from p to the closest enemy bullet (infinity when there is none).</summary>
        public float NearestBullet(Vector3 p)
        {
            float best = float.PositiveInfinity;
            foreach (var b in bullets) { Vector3 d = b.pos - p; d.y = 0f; best = Mathf.Min(best, d.magnitude); }
            return best;
        }

        /// <summary>Campaign: the stage is won (its last wave, the tutorial's last step, or its boss).</summary>
        public void CompleteStage()
        {
            if (Won || Lost) return;
            Won = true;
            Intermission = true;
            Sound.Play(Sfx.WaveClear, 0.8f, 0f);
            StageCleared?.Invoke();
        }

        /// <summary>Demo capture: end the run now (to show the retry screen).</summary>
        public void DemoKill() { Invincible = false; Hp = 0; }

        /// <summary>Full repair (the Reinforced Shell upgrade).</summary>
        public void RepairFull() => Hp = MaxHp;

        void StepEnemy(Enemy e, float dt, Vector3 dp)
        {
            e.spawnT = Mathf.Min(1f, e.spawnT + dt * (e.kind == EnemyKind.Boss ? 1.2f : 3f));
            float pop = e.spawnT < 1f ? Mathf.Sin(e.spawnT * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(e.spawnT * Mathf.PI)) : 1f;
            e.t.localScale = Vector3.one * Mathf.Max(0.01f, pop);
            e.UpdateFlash(dt);
            if (Lost) return;
            // Burning and shocked robots: flames and arcs; a shocked one is stunned (no moving, no biting).
            if (StepStatus(e, dt)) { if (e.Alive) e.t.position = e.pos + Random.insideUnitSphere * 0.03f; return; }

            if (e.kind == EnemyKind.Boss) { StepBoss(e, dt, dp); return; }
            if (e.kind == EnemyKind.Compactor) { StepCompactor(e, dt, dp); return; }
            if (e.IsForeman) { StepForeman(e, dt, dp); return; }

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
            // Contact damage (ground units only), once they have finished appearing.
            if (!e.Flying && e.spawnT >= 1f && dist < e.radius + 0.5f)
            {
                if (e.Low)
                {
                    // A mite latches on and chews for a moment (it shakes): roll now and the landing crushes it.
                    // If it gets to bite, it bursts: a swarm you didn't crush costs a hit each, not your whole hull.
                    e.abilityTimer += dt;
                    MitesOnPip++;
                    // Clamped to Pip's side, jaws working: roll now and the landing crushes it.
                    Vector3 clamp = dist > 0.01f ? -dir : Vector3.back;
                    e.pos = dp + clamp * 0.62f;
                    e.t.position = e.pos + Random.insideUnitSphere * 0.04f;
                    e.t.rotation = Quaternion.LookRotation(dir);
                    if (e.abilityTimer >= MiteBite)
                    {
                        HurtPlayer(1, e.pos, "contact:" + e.kind);
                        e.hp = 0f; Fx.Sparks(pal, e.Position, Palette.Hex("#FF6A2A"), 4); Sound.Play(Sfx.Hit, 0.5f, 0.1f);
                    }
                }
                else
                {
                    HurtPlayer(1, e.pos, "contact:" + e.kind);
                    e.vel = -dir * 3f;
                }
            }
            else if (e.Low) e.abilityTimer = 0f;
        }

        /// <summary>Mites clamped on Pip this frame (the HUD warns: roll!).</summary>
        public int MitesOnPip { get; private set; }


        /// <summary>Seconds a mite chews on Pip before it bites.</summary>
        public const float MiteBite = 1.6f;

        // ---------------- Foreman: Compactor (deck 1) ----------------
        // Rolls toward Pip, winds up (its charge lane lights the floor), then charges in a straight line. Hitting the
        // arena edge or a vent box stuns it and opens its armour: only then do Pip's deck-1 guns hurt it. Below half
        // health it winds up faster, charges harder and calls in crawlers when it recovers.

        void StepCompactor(Enemy e, float dt, Vector3 dp)
        {
            if (e.spawnT < 1f) return;
            Vector3 to = dp - e.pos; to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : Vector3.forward;
            bool angry = e.hp < e.maxHp * 0.5f;
            e.modeT -= dt;
            float lim = World.HalfSize - e.radius;
            switch (e.mode)
            {
                case 0: // approach
                    e.vel = Vector3.Lerp(e.vel, dir * e.speed * (angry ? 1.4f : 1f), 1f - Mathf.Exp(-3f * dt));
                    if (e.modeT <= 0f)
                    {
                        e.mode = 1; e.modeT = angry ? 0.65f : 0.95f; e.chargeDir = dir;
                        e.lane.gameObject.SetActive(true);
                        Sound.Play(Sfx.BossRoar, 0.6f, 0.1f);
                    }
                    break;
                case 1: // wind-up: stand still, lane on the floor, shake
                    e.vel = Vector3.zero;
                    e.t.position = e.pos + Random.insideUnitSphere * 0.04f;
                    float laneLen = 14f;
                    e.lane.position = e.pos + e.chargeDir * (laneLen * 0.5f + e.radius) + Vector3.up * 0.02f;
                    e.lane.rotation = Quaternion.LookRotation(e.chargeDir);
                    e.lane.localScale = new Vector3(1.9f, 0.01f, laneLen) * (0.9f + 0.1f * Mathf.Sin(Time.time * 30f));
                    if (e.modeT <= 0f) { e.mode = 2; e.modeT = 3f; e.lane.gameObject.SetActive(false); Sound.Play(Sfx.Dash, 1f, 0f); }
                    break;
                case 2: // charge
                {
                    e.vel = e.chargeDir * (angry ? 14f : 12f);
                    Vector3 next = e.pos + e.vel * dt;
                    bool wall = Mathf.Abs(next.x) > lim || Mathf.Abs(next.z) > lim;
                    bool box = false;
                    foreach (var ob in World.Obstacles) if (ob != null && ob.kind == ObstacleKind.Barrier && ob.Overlaps(next, e.radius * 0.8f)) { box = true; break; }
                    if (wall || box || e.modeT <= 0f)
                    {
                        e.mode = 3; e.modeT = angry ? 2f : 2.4f; e.vel = Vector3.zero;
                        Juice(0.7f, 0.06f);
                        Fx.Shockwave(pal, e.pos, Palette.Hex("#F2B21E"), 2.6f);
                        Fx.Sparks(pal, e.pos + e.chargeDir * e.radius + Vector3.up * 0.6f, Palette.Hex("#FFC940"), 14);
                        Sound.Play(Sfx.MegaSlam, 1f, 0f);
                        Fx.Text(e.Position + Vector3.up * 1.6f, "STUNNED: FIRE!", Palette.Hex("#3BD16F"), 1.3f, 1.2f);
                        loop.ShowBanner(null, "ARMOUR OPEN: FIRE!", 1.6f, Palette.Hex("#3BD16F"));
                    }
                    break;
                }
                case 3: // stunned, armour open
                    e.vel = Vector3.zero;
                    if (Random.value < dt * 6f) Fx.Sparks(pal, e.Position + Vector3.up * 0.6f, Palette.Hex("#FF7A1A"), 3);
                    if (e.modeT <= 0f)
                    {
                        e.mode = 0; e.modeT = angry ? 2.4f : 3.2f;
                        if (angry) for (int i = 0; i < 3; i++) Spawn(EnemyKind.Crawler, SpawnPoint(5f));
                    }
                    break;
            }
            // Armour plates swing open while stunned.
            float open = e.mode == 3 ? 1f : 0f;
            for (int s = 0; s < e.plates.Length; s++)
            {
                var hinge = e.plates[s];
                float target = (s == 0 ? -1f : 1f) * 70f * open;
                hinge.localRotation = Quaternion.Slerp(hinge.localRotation, Quaternion.Euler(0f, 0f, target), 1f - Mathf.Exp(-10f * dt));
            }
            e.pos += e.vel * dt;
            e.pos.x = Mathf.Clamp(e.pos.x, -lim, lim);
            e.pos.z = Mathf.Clamp(e.pos.z, -lim, lim);
            if (e.mode != 1) e.t.position = e.pos;
            Vector3 face = e.mode >= 1 ? e.chargeDir : dir;
            e.t.rotation = Quaternion.Slerp(e.t.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-(e.mode == 0 ? 3f : 12f) * dt));
            if (e.mode != 3 && dist < e.radius + 0.55f)
            {
                HurtPlayer(1, e.pos, e.mode == 2 ? "compactor-charge" : "compactor");
                dice.Knock((e.mode == 2 ? e.chargeDir : dir) * 9f);
            }
        }

        // ---------------- Boss: High Roller ----------------
        // Three phases. In each phase the boss keeps one number on top (it hops and turns, never rolls), and only
        // the gun on that face can hurt it. When its health crosses a third it rerolls: 3 s, untouchable, the next
        // number shown from the start, so the player has time to line up the roll.

        public const float BossRerollTime = 3f;

        void StepBoss(Enemy e, float dt, Vector3 dp)
        {
            var b = e.boss;
            Vector3 to = dp - e.pos; to.y = 0f;
            float half = BossState.Size * 0.5f;
            if (e.spawnT < 1f) return;

            // Phase change when health crosses a third.
            int phaseByHp = e.hp > e.maxHp * 2f / 3f ? 0 : e.hp > e.maxHp / 3f ? 1 : 2;
            if (!b.Rerolling && phaseByHp > b.phase) StartReroll(e);

            if (b.Rerolling)
            {
                b.reroll -= dt;
                float u = Mathf.Clamp01(1f - b.reroll / BossRerollTime);
                float ease = u * u * (3f - 2f * u);
                // Spins in place: two full turns around a tilted axis while easing toward the new face.
                b.orientation = Quaternion.Slerp(b.startRot, b.targetRot, ease) * Quaternion.AngleAxis(720f * ease, b.axis);
                b.model.Body.localPosition = new Vector3(0f, half + Mathf.Sin(u * Mathf.PI) * 1.4f, 0f);
                b.model.Body.localRotation = b.orientation;
                if (b.reroll <= 0f)
                {
                    b.orientation = b.targetRot;
                    b.weak = b.next;
                    b.defence = b.nextDefence;
                    ShowBossDefence(e);
                    b.phase++;
                    b.rest = 0.8f;
                    b.model.Body.localRotation = b.orientation;
                    b.model.Body.localPosition = new Vector3(0f, half, 0f);
                    BossImpact(e, dp, 1.2f);
                    loop.ShowBanner(null, BossLine(b.defence), 2.2f, CoatModels.Tint(b.defence));
                }
                return;
            }

            float enrage = b.phase == 2 ? 0.75f : 1f;
            if (!b.Hopping)
            {
                b.rest -= dt;
                // Aimed triple burst while resting.
                e.fireTimer += dt;
                if (e.fireTimer > 2.3f * enrage)
                {
                    e.fireTimer = 0f;
                    Vector3 from = e.pos + Vector3.up * 1f;
                    for (int i = -1; i <= 1; i++)
                        FireBullet(from, Quaternion.Euler(0f, i * 14f, 0f) * (dp + Vector3.up * 0.5f - from).normalized * 5.5f, 1, 0.34f, "DieShot");
                    Sound.Play(Sfx.EnemyShot, 0.6f);
                }
                if (b.rest <= 0f) StartHop(e, to);
            }
            else
            {
                b.hopT += dt / 0.6f;
                float u = Mathf.Clamp01(b.hopT);
                float ease = u * u * (3f - 2f * u);
                e.pos = Vector3.Lerp(b.from, b.to, ease);
                // Turns a quarter around the vertical axis while airborne: the top face never changes.
                b.orientation = Quaternion.AngleAxis(b.yaw * ease, Vector3.up) * b.startRot;
                b.model.Body.localPosition = new Vector3(0f, half + Mathf.Sin(u * Mathf.PI) * 1.1f, 0f);
                b.model.Body.localRotation = b.orientation;
                if (u >= 1f)
                {
                    b.hopT = -1f;
                    b.orientation = Quaternion.LookRotation(SnapAxis(b.orientation * Vector3.forward), SnapAxis(b.orientation * Vector3.up));
                    b.model.Body.localPosition = new Vector3(0f, half, 0f);
                    b.hops++;
                    b.rest = Mathf.Max(1.4f, 2.4f - b.tier * 0.3f) * enrage;
                    BossImpact(e, dp, 1f);
                }
            }
            b.model.Body.localRotation = b.orientation;
            e.t.position = e.pos;

            // Touching it hurts.
            if (to.magnitude < half + 0.5f)
            {
                HurtPlayer(1, e.pos, "boss");
                dice.Knock(to.normalized * 6f);
            }
        }

        void StartHop(Enemy e, Vector3 toPlayer)
        {
            var b = e.boss;
            Vector3 dir = toPlayer.magnitude > 0.1f ? toPlayer.normalized : Vector3.forward;
            float dist = Mathf.Clamp(toPlayer.magnitude - 2.5f, 0.5f, 3f);
            float lim = World.HalfSize - BossState.Size * 0.5f - 0.2f;
            Vector3 dest = e.pos + dir * dist;
            dest.x = Mathf.Clamp(dest.x, -lim, lim);
            dest.z = Mathf.Clamp(dest.z, -lim, lim);
            b.from = e.pos;
            b.to = dest;
            b.startRot = b.orientation;
            b.yaw = (b.hops % 2 == 0 ? 90f : -90f);
            b.hopT = 0f;
        }

        void StartReroll(Enemy e)
        {
            var b = e.boss;
            var rng2 = new System.Random(b.tier * 31 + b.phase * 7 + Wave);
            int next;
            do next = 1 + rng2.Next(6); while (next == b.weak);
            b.next = next;
            b.nextDefence = BossDefence(b.tier * 31 + b.phase * 7 + Wave, b.defence);
            b.reroll = BossRerollTime;
            b.hopT = -1f;
            b.startRot = b.orientation;
            b.targetRot = BossState.RotationFor(next, rng2.Next(4));
            b.axis = new Vector3(1f, 0.6f, 0.4f).normalized;
            loop.ShowBanner("REROLL", "NEXT: " + BossLine(b.nextDefence), BossRerollTime, CoatModels.Tint(b.nextDefence));
            if (Stage != null && Hp < MaxHp) { Hp = Mathf.Min(MaxHp, Hp + 2); Fx.Text(dice.transform.position + Vector3.up * 2f, "+2 HULL", UiKit.Mint, 1.2f, 1f); loop.Radio("Patched you up. Two more. Watch what it's wearing now."); }
            Sound.Play(Sfx.BossRoar, 0.8f, 0f);
            Juice(0.4f, 0f);
        }

        /// <summary>Landing: shockwave, a ring of bullets, bombs nearby get shoved, mites now and then.</summary>
        void BossImpact(Enemy e, Vector3 dp, float strength)
        {
            var b = e.boss;
            Fx.Shockwave(pal, e.pos, UiKit.Red, 3f * strength);
            Fx.Debris(pal, e.pos + Vector3.up * 0.1f, Palette.Hex("#6A707C"), 10, 5f);
            Sound.Play(Sfx.MegaSlam, 1f, 0.02f);
            Juice(0.5f * strength, 0.03f);
            Vector3 to = dp - e.pos; to.y = 0f;
            if (to.magnitude < 2.6f) { HurtPlayer(1, e.pos, "boss-landing"); dice.Knock(to.normalized * 7f); }
            Bombs.Slam(e.pos, 2.6f, 1);

            int count = 6 + b.tier * 2 + b.phase * 2;
            float offset = b.hops * 11f;
            for (int i = 0; i < count; i++)
            {
                Vector3 d = Quaternion.Euler(0f, offset + i * 360f / count, 0f) * Vector3.forward;
                FireBullet(e.pos + Vector3.up * 0.5f + d * 1.2f, d * 4.2f, 1, 0.3f, "DieShot");
            }
            if (b.hops > 0 && b.hops % 4 == 0)
                for (int i = 0; i < b.tier; i++) Spawn(EnemyKind.Mite, e.pos + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 1.6f);
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
            FireBullet(from, (target - from).normalized * (e.kind == EnemyKind.Tank ? 6f : 7.5f), dmg, dmg > 1 ? 0.36f : 0.24f, e.kind == EnemyKind.Tank ? "EnemyShell" : "EnemyDart");
            Sound.Play(Sfx.EnemyShot, 0.35f);
        }

        /// <summary>
        /// An enemy shot. Each shooter throws its own (Blender) projectile, all in the House's danger red: drones
        /// fire darts, tanks shells, the Gardener spore pods, the Smelter globs of slag, the High Roller dice.
        /// </summary>
        void FireBullet(Vector3 from, Vector3 vel, int dmg, float size, string look = "EnemyDart")
        {
            var red = Palette.Hex("#FF2A3D");
            var b = new Bullet { pos = from, life = 4f, dmg = dmg, vel = vel, tumble = look == "DieShot" || look == "Spore" };
            var model = BlenderModels.Spawn("Ammo/" + look, null, pal, red, "enemy");
            if (model != null)
            {
                b.t = model;
                b.t.position = from;
                b.t.rotation = Quaternion.LookRotation(vel.sqrMagnitude > 0.01f ? vel : Vector3.forward);
                b.t.localScale = Vector3.one * Mathf.Clamp(size / 0.26f, 0.8f, 1.8f);
            }
            else b.t = Prim.Make(PrimitiveType.Sphere, "EnemyShot", null, from, Vector3.one * size, pal.Glow("EnemyShot", red, 2.4f, red)).transform;
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
                b.age += dt;
                b.pos += b.vel * dt;
                b.t.position = b.pos;
                if (b.vel.sqrMagnitude > 0.01f)
                    b.t.rotation = Quaternion.LookRotation(b.vel) * (b.tumble ? Quaternion.Euler(b.age * 420f, b.age * 300f, 0f) : Quaternion.identity);
                bool done = b.life <= 0f || b.pos.y < 0f || Mathf.Abs(b.pos.x) > World.HalfSize || Mathf.Abs(b.pos.z) > World.HalfSize;
                if (!done && (b.pos - center).sqrMagnitude < 0.6f * 0.6f)
                {
                    HurtPlayer(b.dmg, b.pos, "bullet");
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
                    float wa = a.IsBig ? 0f : b.IsBig ? 1f : 0.5f;
                    Vector3 push = d / m * (min - m);
                    a.pos -= push * wa; b.pos += push * (1f - wa);
                }
            }
        }

        /// <summary>What the current gun can't hurt right now (for the autopilot and HUD), or null.</summary>
        public string Immune(WeaponDef w)
        {
            foreach (var e in Enemies)
            {
                if (!e.Alive || e.Low || e.Untouchable || e.CanBeHitBy(w)) continue;
                return Enemy.DefenceName(e.CurrentDefence);
            }
            return null;
        }

        /// <summary>Removes every bullet (used when the run ends).</summary>
        public void ClearBullets()
        {
            foreach (var b in bullets) Fx.Kill(b.t.gameObject);
            bullets.Clear();
        }
    }
}
