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

        void Kite(Vector3 p, float dt)
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
            if (away.magnitude > 2.5f) dice.Dash();
        }
    }
}
