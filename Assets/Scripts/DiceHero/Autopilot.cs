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
            if (mode == Mode.Kite && needRoll && modeTime > 0.3f) PlanRoll(p);

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
                    if (modeTime > 1.4f) SetMode(Mode.Kite);
                    break;
                default:
                    Kite(p, dt);
                    break;
            }
        }

        void SetMode(Mode m) { mode = m; modeTime = 0f; }

        void Steer(Vector3 v)
        {
            v.y = 0f;
            dice.InputOverride = v.sqrMagnitude > 0.01f ? new Vector2(v.x, v.z).normalized : Vector2.zero;
        }

        void PlanRoll(Vector3 p)
        {
            float best = float.MaxValue;
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            foreach (var ob in World.Obstacles)
            {
                foreach (var d in dirs)
                {
                    int top = dice.PreviewTop(d, ob.RollSteps);
                    int gain = Hurtable(WeaponDef.All[top]) - Hurtable(weapons.Current);
                    if (gain <= 0) continue;
                    float half = Mathf.Abs(d.x) > 0f ? ob.halfExtents.x : ob.halfExtents.y;
                    // Conduits only make sense to cross, not to hit end-on.
                    float across = Mathf.Abs(d.x) > 0f ? ob.halfExtents.y : ob.halfExtents.x;
                    if (across > 1f) continue;
                    Vector3 ap = ob.transform.position - d * (half + 1.9f);
                    float lim = World.HalfSize - 0.8f;
                    if (Mathf.Abs(ap.x) > lim || Mathf.Abs(ap.z) > lim) continue;
                    if (Blocked(ap)) continue;
                    float cost = (ap - p).magnitude - gain * 2f;
                    if (cost < best) { best = cost; goalOb = ob; goalDir = d; approachPoint = ap; }
                }
            }
            if (best < float.MaxValue) SetMode(Mode.Approach);
        }

        static bool Blocked(Vector3 pos)
        {
            foreach (var ob in World.Obstacles) if (ob.Overlaps(pos, 0.7f)) return true;
            return false;
        }

        int Hurtable(WeaponDef w)
        {
            int n = 0;
            foreach (var e in game.Enemies)
                if (e.Alive && Enemy.CanHit(w, e.kind)) n += e.kind == EnemyKind.Mite ? 1 : 2;
            return n;
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
            wanderAngle += dt * 0.6f;
            Vector3 orbit = new Vector3(Mathf.Cos(wanderAngle), 0f, Mathf.Sin(wanderAngle)) * 4f - p;
            Vector3 want = away * 2f + orbit * 0.35f;
            Steer(want * 3f - dice.Velocity);
            if (away.magnitude > 2.5f) dice.Dash();
        }
    }
}
