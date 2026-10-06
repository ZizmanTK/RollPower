using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Simple AI player used for play-testing and the demo video.
    /// When the current gun can't hurt something on the field, it picks the obstacle + approach direction
    /// whose roll gives a gun that can, lines up, and dashes into it. Otherwise it kites enemies.
    /// </summary>
    public class Autopilot
    {
        readonly DiceController dice;
        readonly WeaponSystem weapons;
        readonly Game game;

        enum Mode { Kite, Approach, Charge }
        Mode mode;
        Obstacle goalOb;
        Vector3 goalDir, approachPoint;
        float modeTime, sinceRoll, wanderAngle;

        public string Status => mode == Mode.Kite ? "kite" : $"{mode} {goalOb?.kind} dir {goalDir}";

        public Autopilot(DiceController dice, WeaponSystem weapons, Game game)
        {
            this.dice = dice;
            this.weapons = weapons;
            this.game = game;
            dice.TopChanged += (o, n) => { mode = Mode.Kite; sinceRoll = 0f; };
        }

        public void Step(float dt)
        {
            modeTime += dt;
            sinceRoll += dt;
            Vector3 p = dice.transform.position;
            if (dice.IsRolling) return;
            if (DiceController.ButtonMode) { StepButton(p, dt); return; }

            bool needRoll = game.Immune(weapons.Current) != null;
            if (mode == Mode.Kite && modeTime > 0.3f) PlanShove(p);
            if (mode == Mode.Kite && needRoll && modeTime > 0.3f) PlanRoll(p);
            if (goalBomb != null)
            {
                // Track the bomb while lining up; give up if it's about to blow or already gone.
                if (!game.Bombs.All.Contains(goalBomb) || !goalBomb.Armed || goalBomb.fuse < 1.6f) { goalBomb = null; SetMode(Mode.Kite); }
                else if (mode == Mode.Approach) approachPoint = goalBomb.pos - goalDir * 1.5f;
            }

            switch (mode)
            {
                case Mode.Approach:
                {
                    Vector3 to = approachPoint - p; to.y = 0f;
                    if (to.magnitude < 0.45f || modeTime > 4f) { SetMode(to.magnitude < 0.8f ? Mode.Charge : Mode.Kite); break; }
                    // Brake as we arrive so the charge starts from a clean line.
                    Vector3 wantVel = to.normalized * Mathf.Min(6f, to.magnitude * 3f);
                    Steer(wantVel - dice.Velocity);
                    break;
                }
                case Mode.Charge:
                    dice.InputOverride = new Vector2(goalDir.x, goalDir.z);
                    if (modeTime > 0.12f) dice.Dash();
                    if (modeTime > 1.4f) { SetMode(Mode.Kite); goalBomb = null; }
                    break;
                default:
                    Kite(p, dt);
                    break;
            }
        }

        void SetMode(Mode m) { mode = m; modeTime = 0f; }

        /// <summary>Seconds the bot takes to notice its gun is wrong before rolling (a deliberate, human-ish delay).</summary>
        public float ReactionTime = 0.45f;
        /// <summary>Chance per wrong-gun spell that the bot doesn't act on the advice at all (a player who missed it).</summary>
        public float IgnoreAdvice;
        bool ignoring;
        float wrongFor;

        /// <summary>
        /// Button mode: kite and shove bombs as usual, roll toward the advised marker once the gun has been wrong for
        /// ReactionTime, and roll away from crowding, preferring a direction whose gun is no worse than the current one.
        /// </summary>
        /// <summary>Tutorial hooks: a place to walk to, and a roll the tutorial asks for.</summary>
        public System.Func<Vector3?> Goal;
        public System.Func<RollPlan> ExtraAdvice;

        void StepButton(Vector3 p, float dt)
        {
            // Step off an erupting (or about to erupt) heat vent.
            if (Hazards.Danger(p) && dice.DashCooldownLeft <= 0f) { dice.TryRoll(); return; }
            var extra = ExtraAdvice?.Invoke();
            if (extra != null && dice.DashCooldownLeft <= 0f) { dice.InputOverride = new Vector2(extra.dir.x, extra.dir.z); dice.TryRoll(); return; }
            var goal = Goal?.Invoke();
            if (goal.HasValue && game.EnemiesLeft == 0)
            {
                Vector3 to = goal.Value - p; to.y = 0f;
                Steer(to * 3f - dice.Velocity);
                // Blocked (a pipe rack in the way): roll toward the goal, which vaults it.
                if (dice.Velocity.magnitude < 0.6f && to.magnitude > 1.5f && dice.DashCooldownLeft <= 0f && modeTime > 0.5f) { dice.TryRoll(); modeTime = 0f; }
                return;
            }
            // Dodge first: roll across whatever is about to hit, preferring a direction whose gun is no worse.
            var threat = game.IncomingThreat(p);
            if (threat.HasValue && dice.DashCooldownLeft <= 0f)
            {
                Vector3 from = p - threat.Value; from.y = 0f;
                int cur = RollAdvisor.Value(weapons.Current, game);
                Vector3 bestDir = Vector3.zero; float bestS = float.MinValue;
                foreach (var d in RollAdvisor.Directions)
                {
                    float s = -Mathf.Abs(Vector3.Dot(d, from.normalized)) * 1.5f + Vector3.Dot(d, from.normalized)
                              + (RollAdvisor.Value(WeaponDef.All[dice.PreviewTop(d)], game) >= cur ? 1.5f : 0f) - (Blocked(p + d * 1.6f) ? 5f : 0f);
                    if (s > bestS) { bestS = s; bestDir = d; }
                }
                dice.InputOverride = new Vector2(bestDir.x, bestDir.z);
                if (dice.TryRoll()) return;
            }
            bool wrong = RollAdvisor.Needed(dice, game);
            if (wrong && wrongFor == 0f) ignoring = Random.value < IgnoreAdvice;
            wrongFor = wrong ? wrongFor + dt : 0f;
            if (wrong && !ignoring && wrongFor > ReactionTime && dice.DashCooldownLeft <= 0f)
            {
                var plan = RollAdvisor.BestButton(dice, game);
                if (plan != null) { dice.InputOverride = new Vector2(plan.dir.x, plan.dir.z); dice.TryRoll(); return; }
            }
            if (mode == Mode.Kite && modeTime > 0.3f) PlanShove(p);
            if (goalBomb != null && (!game.Bombs.All.Contains(goalBomb) || !goalBomb.Armed || goalBomb.fuse < 1.6f)) { goalBomb = null; SetMode(Mode.Kite); }
            switch (mode)
            {
                case Mode.Approach:
                {
                    if (goalBomb != null) approachPoint = goalBomb.pos - goalDir * 1.5f;
                    Vector3 to = approachPoint - p; to.y = 0f;
                    if (to.magnitude < 0.45f || modeTime > 4f) { SetMode(to.magnitude < 0.8f ? Mode.Charge : Mode.Kite); break; }
                    Steer(to.normalized * Mathf.Min(6f, to.magnitude * 3f) - dice.Velocity);
                    break;
                }
                case Mode.Charge: // push the bomb by gliding into it; a roll would pass over it
                    dice.InputOverride = new Vector2(goalDir.x, goalDir.z);
                    if (modeTime > 1.2f) { SetMode(Mode.Kite); goalBomb = null; }
                    break;
                default:
                {
                    Vector3 away = Kite(p, dt, false);
                    if (away.magnitude > 2.5f && dice.DashCooldownLeft <= 0f)
                    {
                        int cur = RollAdvisor.Value(weapons.Current, game);
                        Vector3 bestDir = Vector3.zero; float best = float.MinValue;
                        foreach (var d in RollAdvisor.Directions)
                        {
                            float s = Vector3.Dot(d, away.normalized) * 3f + (RollAdvisor.Value(WeaponDef.All[dice.PreviewTop(d)], game) >= cur ? 2f : 0f);
                            if (s > best) { best = s; bestDir = d; }
                        }
                        dice.InputOverride = new Vector2(bestDir.x, bestDir.z);
                        dice.TryRoll();
                    }
                    break;
                }
            }
        }

        Bomb goalBomb;

        /// <summary>Lines up behind a nearby bomb and dashes it toward the closest edge.</summary>
        void PlanShove(Vector3 p)
        {
            Bomb best = null;
            float bestD = 7f;
            foreach (var b in game.Bombs.All)
            {
                if (!b.Armed || b.fuse < 2.4f) continue;
                float d = (b.pos - p).magnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            if (best == null) return;
            Vector3 bp = best.pos;
            float h = World.HalfSize;
            (float dist, Vector3 dir)[] edges = { (h - bp.x, Vector3.right), (h + bp.x, Vector3.left), (h - bp.z, Vector3.forward), (h + bp.z, Vector3.back) };
            System.Array.Sort(edges, (a, b) => a.dist.CompareTo(b.dist));
            foreach (var e in edges)
            {
                Vector3 ap = bp - e.dir * 1.5f;
                float lim = h - 0.8f;
                if (Mathf.Abs(ap.x) > lim || Mathf.Abs(ap.z) > lim || Blocked(ap)) continue;
                goalBomb = best;
                goalDir = e.dir;
                approachPoint = ap;
                SetMode(Mode.Approach);
                return;
            }
        }

        void Steer(Vector3 v)
        {
            v.y = 0f;
            dice.InputOverride = v.sqrMagnitude > 0.01f ? new Vector2(v.x, v.z).normalized : Vector2.zero;
        }

        /// <summary>Follows the same advice the on-screen roll guide shows.</summary>
        void PlanRoll(Vector3 p)
        {
            var plan = RollAdvisor.Best(dice, game);
            if (plan == null) return;
            goalOb = plan.obstacle;
            goalDir = plan.dir;
            approachPoint = plan.approach;
            SetMode(Mode.Approach);
        }

        static bool Blocked(Vector3 pos)
        {
            foreach (var ob in World.Obstacles) if (ob.Overlaps(pos, 0.7f)) return true;
            return false;
        }

        Vector3 Kite(Vector3 p, float dt, bool dash = true)
        {
            // Keep a comfortable distance from ground enemies, drift around the arena otherwise.
            Vector3 away = Vector3.zero;
            foreach (var e in game.Enemies)
            {
                if (!e.Alive || e.Flying) continue;
                Vector3 d = p - e.pos; d.y = 0f;
                float m = d.magnitude;
                if (m < 4f && m > 0.01f) away += d / m * (4f - m);
            }
            // Run from bombs that are about to blow.
            foreach (var b in game.Bombs.All)
            {
                if (b.falling || b.fuse > 2.4f) continue;
                Vector3 d = p - b.pos; d.y = 0f;
                float m = d.magnitude;
                if (m < Bombs.BlastRadius + 1.5f && m > 0.01f) away += d / m * (Bombs.BlastRadius + 1.5f - m) * 2f;
            }
            wanderAngle += dt * 0.6f;
            Vector3 orbit = new Vector3(Mathf.Cos(wanderAngle), 0f, Mathf.Sin(wanderAngle)) * 4f - p;
            Vector3 want = away * 2f + orbit * 0.35f;
            Steer(want * 3f - dice.Velocity);
            if (dash && away.magnitude > 2.5f) dice.Dash();
            return away;
        }
    }
}
