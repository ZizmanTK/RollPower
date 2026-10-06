using UnityEngine;

namespace DiceHero
{
    /// <summary>A suggested roll: slam 'obstacle' while travelling in 'dir' to get 'top' (and its gun).</summary>
    public class RollPlan
    {
        public Obstacle obstacle;
        public Vector3 dir;        // cardinal travel direction at the moment of impact
        public Vector3 approach;   // where to line up before the slam
        public int top;            // number that will land on top
        public WeaponDef Gun => WeaponDef.All[top];
        public bool Conduit => obstacle.kind == ObstacleKind.Conduit;
    }

    /// <summary>
    /// Works out which roll gives a gun that can hurt what's on the field, and which nearby barrier to
    /// slam to get it. Shared by the on-screen roll guide and the autopilot.
    /// </summary>
    public static class RollAdvisor
    {
        public static readonly Vector3[] Directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        /// <summary>How useful a gun is right now: enemies it can hurt (the boss counts a lot, mites a little).</summary>
        public static int Value(WeaponDef w, Game game)
        {
            int n = 0;
            foreach (var e in game.Enemies)
                if (e.Alive && e.CanBeHitBy(w)) n += e.kind == EnemyKind.Mite ? 1 : e.kind == EnemyKind.Boss ? 6 : 2;
            return n;
        }

        /// <summary>
        /// Best roll from the dice's current orientation, or null when no roll improves on the current gun.
        /// Prefers the biggest gain, then the closest obstacle to line up on.
        /// </summary>
        public static RollPlan Best(DiceController dice, Game game)
        {
            var current = WeaponDef.All[dice.TopNumber];
            int baseValue = Value(current, game);
            Vector3 p = dice.transform.position;
            RollPlan best = null;
            float bestCost = float.MaxValue;
            foreach (var ob in World.Obstacles)
            {
                if (ob == null) continue;
                foreach (var d in Directions)
                {
                    // Conduits only make sense to cross, not to hit end-on.
                    float across = Mathf.Abs(d.x) > 0f ? ob.halfExtents.y : ob.halfExtents.x;
                    if (across > 1f) continue;
                    int top = dice.PreviewTop(d, ob.RollSteps);
                    int gain = Value(WeaponDef.All[top], game) - baseValue;
                    if (gain <= 0) continue;
                    float half = Mathf.Abs(d.x) > 0f ? ob.halfExtents.x : ob.halfExtents.y;
                    Vector3 ap = ob.transform.position - d * (half + 1.9f);
                    float lim = World.HalfSize - 0.8f;
                    if (Mathf.Abs(ap.x) > lim || Mathf.Abs(ap.z) > lim || Blocked(ap)) continue;
                    float cost = (ap - p).magnitude - gain * 2f;
                    if (cost < bestCost) { bestCost = cost; best = new RollPlan { obstacle = ob, dir = d, approach = ap, top = top }; }
                }
            }
            return best;
        }

        /// <summary>
        /// True when the current gun is clearly the wrong one: some gun would hurt at least twice as much of
        /// what is on the field. A single stray drone among crawlers does not trigger a hint.
        /// </summary>
        public static bool Needed(DiceController dice, Game game)
        {
            int cur = Value(WeaponDef.All[dice.TopNumber], game), best = 0;
            for (int n = 1; n <= 6; n++) best = Mathf.Max(best, Value(WeaponDef.All[n], game));
            return best > 0 && cur * 2 <= best;
        }

        /// <summary>Distance to the line-up point, minus a bonus for how much the roll helps (lower is better).</summary>
        public static float Cost(RollPlan plan, DiceController dice, Game game)
        {
            int gain = Value(plan.Gun, game) - Value(WeaponDef.All[dice.TopNumber], game);
            return (plan.approach - dice.transform.position).magnitude - gain * 2f;
        }

        /// <summary>The plan still lands the promised number and that gun still helps.</summary>
        public static bool StillValid(RollPlan plan, DiceController dice, Game game)
            => plan.obstacle != null && dice.PreviewTop(plan.dir, plan.obstacle.RollSteps) == plan.top
               && Value(plan.Gun, game) > Value(WeaponDef.All[dice.TopNumber], game);

        static bool Blocked(Vector3 pos)
        {
            foreach (var ob in World.Obstacles) if (ob != null && ob.Overlaps(pos, 0.7f)) return true;
            return false;
        }

        /// <summary>Screen-space arrow for a travel direction (the camera always looks north).</summary>
        public static string Arrow(Vector3 d) => d.z > 0.5f ? "↑" : d.z < -0.5f ? "↓" : d.x > 0f ? "→" : "←";
    }

    /// <summary>
    /// In-world roll guide: when the current gun can't hurt what's on the field, a glowing arrow in the
    /// new gun's colour sits behind the suggested barrier, pointing the way to slam it, and the barrier
    /// gets a pulsing pad. Hidden the rest of the time.
    /// </summary>
    public class RollGuide
    {
        readonly Palette pal;
        readonly Transform root, arrow, pad;
        readonly Renderer[] arrowParts;
        readonly Renderer padRenderer;
        float t;
        int shownTop = -1;

        public RollPlan Plan { get; private set; }

        public RollGuide(Palette pal)
        {
            this.pal = pal;
            root = new GameObject("RollGuide").transform;
            arrow = new GameObject("Arrow").transform;
            arrow.SetParent(root, false);
            var m = pal.Glow("GuideDefault", Color.white, 2f);
            // Chevron-style arrow lying on the deck, pointing along local +Z.
            var shaft = Prim.Make(PrimitiveType.Cube, "Shaft", arrow, new Vector3(0f, 0.03f, -0.35f), new Vector3(0.22f, 0.02f, 0.9f), m);
            var headL = Prim.Make(PrimitiveType.Cube, "HeadL", arrow, new Vector3(-0.17f, 0.03f, 0.18f), new Vector3(0.2f, 0.02f, 0.62f), m, Quaternion.Euler(0f, 40f, 0f));
            var headR = Prim.Make(PrimitiveType.Cube, "HeadR", arrow, new Vector3(0.17f, 0.03f, 0.18f), new Vector3(0.2f, 0.02f, 0.62f), m, Quaternion.Euler(0f, -40f, 0f));
            arrowParts = new[] { shaft.GetComponent<Renderer>(), headL.GetComponent<Renderer>(), headR.GetComponent<Renderer>() };
            pad = Prim.Make(PrimitiveType.Cube, "Pad", root, Vector3.zero, Vector3.one, m).transform;
            padRenderer = pad.GetComponent<Renderer>();
            root.gameObject.SetActive(false);
        }

        float replan;

        /// <summary>
        /// Shows advice only when the current gun is clearly wrong, and commits to one target: the plan only
        /// changes when it stops working, or when a much better one turns up (checked every 1.5 s).
        /// </summary>
        public void Step(float dt, DiceController dice, Game game, bool active)
        {
            t += dt;
            if (dice.IsRolling) { root.gameObject.SetActive(false); return; } // hidden mid-roll; re-checked on landing
            if (!active || game.Lost || !RollAdvisor.Needed(dice, game)) { Plan = null; root.gameObject.SetActive(false); return; }
            if (Plan != null && !RollAdvisor.StillValid(Plan, dice, game)) Plan = null;
            replan -= dt;
            if (Plan == null || replan <= 0f)
            {
                replan = 1.5f;
                var fresh = RollAdvisor.Best(dice, game);
                if (Plan == null || (fresh != null && RollAdvisor.Cost(fresh, dice, game) < RollAdvisor.Cost(Plan, dice, game) - 4f)) Plan = fresh;
            }
            root.gameObject.SetActive(Plan != null);
            if (Plan == null) return;

            if (Plan.top != shownTop)
            {
                shownTop = Plan.top;
                var mat = pal.Glow("Guide" + Plan.top, Plan.Gun.color, 2.6f);
                foreach (var r in arrowParts) r.sharedMaterial = mat;
                padRenderer.sharedMaterial = pal.Glow("GuidePad" + Plan.top, Plan.Gun.color, 0.9f, Plan.Gun.color * 0.3f);
            }

            var ob = Plan.obstacle;
            Vector3 c = ob.transform.position;
            float half = Mathf.Abs(Plan.dir.x) > 0f ? ob.halfExtents.x : ob.halfExtents.y;
            // The arrow slides toward the barrier, like a "hit it this way" gesture.
            float slide = Mathf.Repeat(t * 1.4f, 1f);
            arrow.SetPositionAndRotation(c - Plan.dir * (half + 1.9f - slide * 0.5f), Quaternion.LookRotation(Plan.dir));
            arrow.localScale = Vector3.one * (1.7f + 0.12f * Mathf.Sin(t * 8f));
            float pulse = 1f + 0.12f * Mathf.Sin(t * 6f);
            pad.position = c + Vector3.up * 0.012f;
            pad.localScale = new Vector3(ob.halfExtents.x * 2f + 0.5f, 0.01f, ob.halfExtents.y * 2f + 0.5f) * pulse;
        }
    }
}
