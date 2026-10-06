using System;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// The player's die. Steer with WASD / arrows.
    /// Button mode (default): the roll button tips the die one face in the held direction; that roll is both the
    /// dodge (invulnerable for its first part, vulnerable on landing) and the weapon change. Rolling into a pipe
    /// rack vaults it (two tips, the opposite face); rolling into a vent box is cancelled. Moving never rolls.
    /// Bump mode (2.0): the die glides like on ice and hitting an obstacle fast enough trips it into a roll.
    /// </summary>
    public class DiceController : MonoBehaviour
    {
        /// <summary>Roll on a button (true) or by bumping into obstacles (false, the 2.0 behaviour).</summary>
        public static bool ButtonMode = true;

        [Header("Roll button")]
        public float buttonRollTime = 0.32f;
        public float vaultRollTime = 0.5f;
        public float rollDistance = 1.6f;
        public float rollInvulnerable = 0.22f; // seconds from the start of a single roll
        public float rollCooldown = 0.45f;     // after landing
        Vector3 facing = Vector3.forward;
        [Header("Glide")]
        public float acceleration = 16f;
        public float maxSpeed = 7f;
        public float glideDrag = 1.1f;
        public float wallBounce = 0.5f;

        [Header("Trip & roll")]
        public float tripSpeed = 2.5f;
        public float rollDuration = 0.42f;
        public float doubleRollDuration = 0.62f;
        public float hopHeight = 0.55f;
        public float doubleHopHeight = 1.3f;

        /// <summary>When set, used instead of keyboard input (demo autopilot).</summary>
        public Vector2? InputOverride;

        public DiceModel Model { get; private set; }
        public Vector3 Velocity => velocity;
        public bool IsRolling => rolling;
        /// <summary>
        /// Can not be hurt. Button mode: only the first part of a roll (longer for a vault), so landing is a risk
        /// and the roll can't be chained into permanent safety. Bump mode: while rolling, early dash, just after landing.
        /// </summary>
        public bool Shielded => ButtonMode
            ? rolling && rollT * rollTime < (rollSteps == 2 ? rollInvulnerable * 1.6f : rollInvulnerable)
            : rolling || dashTime > 0.1f || landGrace > 0f;
        float landGrace;
        public int TopNumber { get; private set; } = 1;
        public Quaternion Orientation => orientation;

        /// <summary>Raised when a roll lands: (old top, new top).</summary>
        public event Action<int, int> TopChanged;
        public event Action<Obstacle> Tripped;

        Vector3 velocity;
        Quaternion orientation = Quaternion.identity;
        bool rolling;
        float rollT, rollTime, rollHop, tripCooldown;
        int rollSteps;
        Vector3 rollAxis, rollFrom, rollTo;
        Quaternion rollStartRot;
        float squash, squashVel, mountScale = 1f;

        const float BevelRadius = 0.1f;

        public void Init(DiceModel model)
        {
            Model = model;
            TopNumber = TopFor(orientation);
        }

        [Header("Dash")]
        public float dashSpeed = 10f;
        public float dashCooldown = 0.9f;
        public float DashCooldownLeft { get; private set; }
        public float DashCooldownTotal => (ButtonMode ? rollCooldown : dashCooldown) * RunStats.Current.dashCooldownMul;
        public int LastRollSteps => rollSteps;
        public event Action Dashed;
        /// <summary>Button mode: a roll was refused because a vent box is right in the way.</summary>
        public event Action RollBlocked;
        float dashTime;

        /// <summary>
        /// The roll/dash button. Button mode: tips the die toward the held direction (see <see cref="TryRoll"/>).
        /// Bump mode: burst of speed in the steering direction (or current travel direction).
        /// </summary>
        public void Dash()
        {
            if (ButtonMode) { TryRoll(); return; }
            if (rolling || DashCooldownLeft > 0f) return;
            Vector2 input = ReadInput();
            Vector3 dir = input.sqrMagnitude > 0.01f ? new Vector3(input.x, 0f, input.y).normalized
                        : velocity.sqrMagnitude > 0.1f ? velocity.normalized : Vector3.forward;
            velocity = dir * dashSpeed;
            dashTime = 0.35f;
            DashCooldownLeft = DashCooldownTotal;
            squashVel += 3f;
            Dashed?.Invoke();
        }

        /// <summary>Direction the next roll would take: the held direction, else the travel direction, else the last one.</summary>
        public Vector3 RollDirection()
        {
            Vector2 input = ReadInput();
            if (input.sqrMagnitude > 0.09f) return CardinalOf(new Vector3(input.x, 0f, input.y));
            if (velocity.sqrMagnitude > 0.25f) return CardinalOf(velocity);
            return facing;
        }

        /// <summary>
        /// Button mode roll: one tip toward <see cref="RollDirection"/>, moving one die-and-a-half.
        /// A pipe rack in the way is vaulted (two tips, landing on the far side); a vent box right in front
        /// cancels the roll, and one a little further shortens it.
        /// </summary>
        public bool TryRoll()
        {
            if (rolling || DashCooldownLeft > 0f) return false;
            Vector3 dir = RollDirection();
            Vector3 from = transform.position;
            float distance = rollDistance;
            Obstacle vault = null;
            for (float d = 0.2f; d <= rollDistance + 0.01f; d += 0.1f)
            {
                Obstacle hit = null;
                foreach (var ob in World.Obstacles)
                    if (ob != null && ob.Overlaps(from + dir * d, 0.45f)) { hit = ob; break; }
                if (hit == null) continue;
                float across = Mathf.Abs(dir.x) > 0f ? hit.halfExtents.x : hit.halfExtents.y;
                if (hit.kind == ObstacleKind.Conduit && across <= 1f) { vault = hit; break; }
                if (d < 0.75f) { Bump(); DashCooldownLeft = 0.15f; RollBlocked?.Invoke(); return false; }
                distance = d - 0.3f;
                break;
            }
            facing = dir;
            if (vault != null) { velocity = dir * rollDistance; StartRoll(vault); return true; }
            Vector3 to = from + dir * distance;
            float limit = World.HalfSize - 0.5f;
            to.x = Mathf.Clamp(to.x, -limit, limit);
            to.z = Mathf.Clamp(to.z, -limit, limit);
            BeginRoll(dir, 1, to, buttonRollTime, hopHeight * 0.6f);
            Tripped?.Invoke(null);
            return true;
        }

        public void Knock(Vector3 impulse)
        {
            if (rolling) return;
            velocity = Vector3.ClampMagnitude(velocity + impulse, maxSpeed);
            tripCooldown = Mathf.Max(tripCooldown, 0.6f); // getting shoved never rerolls the gun by accident
        }

        /// <summary>Soft push (e.g. recoil from shoving a bomb) that doesn't block the next trip.</summary>
        public void Nudge(Vector3 impulse)
        {
            if (rolling) return;
            velocity = Vector3.ClampMagnitude(velocity + impulse, Mathf.Max(maxSpeed, velocity.magnitude));
        }

        /// <summary>Test helper: moves the dice and stops it dead.</summary>
        public void Teleport(Vector3 pos)
        {
            transform.position = pos;
            velocity = Vector3.zero;
            DashCooldownLeft = 0f;
            tripCooldown = 0f;
        }

        /// <summary>Debug/test helper: instantly turns the dice so face 'number' is on top.</summary>
        public void ForceTop(int number)
        {
            int i = System.Array.IndexOf(DiceModel.FaceNumbers, number);
            orientation = Quaternion.FromToRotation(DiceModel.FaceNormals[i], Vector3.up);
            int old = TopNumber;
            TopNumber = TopFor(orientation);
            Model.Body.localRotation = orientation;
            TopChanged?.Invoke(old, TopNumber);
        }

        /// <summary>Advances the simulation by dt. Called by the GameLoop (and by tests).</summary>
        public void Step(float dt)
        {
            tripCooldown -= dt;
            DashCooldownLeft -= dt;
            dashTime -= dt;
            landGrace -= dt;
            if (rolling) UpdateRoll(dt);
            else UpdateGlide(dt);
            UpdateJuice(dt);
        }

        Vector2 ReadInput()
        {
            if (InputOverride.HasValue) return Vector2.ClampMagnitude(InputOverride.Value, 1f);
            return Controls.Move;
        }

        void UpdateGlide(float dt)
        {
            Vector2 input = ReadInput();
            // Button mode steers tightly (ice is saved for an environment that wants it); bump mode glides.
            float accel = ButtonMode ? 38f : acceleration, drag = ButtonMode ? 6f : glideDrag;
            velocity += new Vector3(input.x, 0f, input.y) * accel * dt;
            velocity *= Mathf.Exp(-drag * dt);
            velocity = Vector3.ClampMagnitude(velocity, dashTime > 0f ? dashSpeed : maxSpeed);

            Vector3 pos = transform.position + velocity * dt;

            // Arena walls
            float limit = World.HalfSize - 0.5f;
            if (Mathf.Abs(pos.x) > limit) { pos.x = Mathf.Sign(pos.x) * limit; velocity.x = -velocity.x * wallBounce; Bump(); }
            if (Mathf.Abs(pos.z) > limit) { pos.z = Mathf.Sign(pos.z) * limit; velocity.z = -velocity.z * wallBounce; Bump(); }

            // Obstacles: trip when fast, otherwise just get blocked.
            foreach (var ob in World.Obstacles)
            {
                if (ob == null || !ob.Overlaps(pos, 0.5f)) continue;
                if (!ButtonMode && tripCooldown <= 0f && velocity.magnitude >= tripSpeed)
                {
                    transform.position = pos;
                    StartRoll(ob);
                    return;
                }
                pos = PushOut(pos, ob);
            }
            transform.position = pos;
        }

        Vector3 PushOut(Vector3 pos, Obstacle ob)
        {
            Vector3 d = pos - ob.transform.position;
            float px = ob.halfExtents.x + 0.5f - Mathf.Abs(d.x);
            float pz = ob.halfExtents.y + 0.5f - Mathf.Abs(d.z);
            if (px < pz) { pos.x += Mathf.Sign(d.x) * px; velocity.x = 0f; }
            else { pos.z += Mathf.Sign(d.z) * pz; velocity.z = 0f; }
            return pos;
        }

        static Vector3 CardinalOf(Vector3 v)
        {
            return Mathf.Abs(v.x) >= Mathf.Abs(v.z) ? new Vector3(Mathf.Sign(v.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        void StartRoll(Obstacle ob)
        {
            Vector3 dir = CardinalOf(velocity);
            int steps = ob.RollSteps;
            Vector3 from = transform.position;

            // Land just past the far side of the obstacle (at least one dice width per roll step).
            Vector3 op = ob.transform.position;
            float half = Mathf.Abs(dir.x) > 0f ? ob.halfExtents.x : ob.halfExtents.y;
            float along = Vector3.Dot(op - from, dir) + half + 0.6f;
            along = Mathf.Max(along, steps * 1f);
            Vector3 to = from + dir * along;
            float limit = World.HalfSize - 0.5f;
            to.x = Mathf.Clamp(to.x, -limit, limit);
            to.z = Mathf.Clamp(to.z, -limit, limit);

            float time = steps == 2 ? (ButtonMode ? vaultRollTime : doubleRollDuration) : rollDuration;
            BeginRoll(dir, steps, to, time, steps == 2 ? doubleHopHeight : hopHeight);
            Tripped?.Invoke(ob);
        }

        void BeginRoll(Vector3 dir, int steps, Vector3 to, float time, float hop)
        {
            rollSteps = steps;
            rollAxis = Vector3.Cross(Vector3.up, dir);
            rollStartRot = orientation;
            rollFrom = transform.position;
            rollTo = to;
            rollTime = time;
            rollHop = hop;
            rollT = 0f;
            rolling = true;
            velocity = dir * velocity.magnitude * 0.55f;
        }

        void UpdateRoll(float dt)
        {
            rollT += dt / rollTime;
            float u = Mathf.Clamp01(rollT);
            float spin = 1f - Mathf.Pow(1f - u, 2.2f);          // fast kick-off, settles into the landing
            float angle = 90f * rollSteps * spin;
            orientation = Quaternion.AngleAxis(angle, rollAxis) * rollStartRot;

            transform.position = Vector3.Lerp(rollFrom, rollTo, Mathf.SmoothStep(0f, 1f, u));

            // Lift the body so the rotated cube never dips into the ground, plus a hop arc.
            float a = (angle % 90f) * Mathf.Deg2Rad;
            float lowest = (0.5f - BevelRadius) * (Mathf.Abs(Mathf.Cos(a)) + Mathf.Abs(Mathf.Sin(a))) + BevelRadius;
            float hop = rollHop * 4f * u * (1f - u);
            Model.Body.localPosition = new Vector3(0f, Mathf.Max(0.5f + hop, lowest), 0f);

            if (u >= 1f) Land();
        }

        void Land()
        {
            rolling = false;
            tripCooldown = 0.25f;
            landGrace = ButtonMode ? 0f : 0.4f;
            if (ButtonMode)
            {
                DashCooldownLeft = DashCooldownTotal;
                velocity = Vector3.ClampMagnitude(velocity, 2.5f); // keep a little of the roll's momentum
            }
            orientation = SnapToAxes(orientation);
            Model.Body.localPosition = new Vector3(0f, 0.5f, 0f);
            squashVel -= 7f;
            int old = TopNumber;
            TopNumber = TopFor(orientation);
            TopChanged?.Invoke(old, TopNumber);
        }

        void Bump() { squashVel -= 2.5f; }

        void UpdateJuice(float dt)
        {
            // Spring-damped squash (negative = flatter).
            squashVel += (-squash * 180f - squashVel * 14f) * dt;
            squash += squashVel * dt;
            float sy = 1f + squash, sxz = 1f - squash * 0.5f;
            Model.Visual.localScale = new Vector3(sxz, sy, sxz);
            Model.Body.localRotation = orientation;

            // The gun turret retracts while tumbling and deploys again after landing.
            mountScale = Mathf.MoveTowards(mountScale, rolling ? 0f : 1f, dt * (rolling ? 14f : 5f));
            float pop = mountScale < 1f ? 1f + Mathf.Sin(mountScale * Mathf.PI) * 0.2f : 1f;
            Model.WeaponMount.localScale = Vector3.one * Mathf.Max(0.0001f, mountScale * pop);
        }

        // ----- Face maths -----

        public static int TopFor(Quaternion rot)
        {
            int best = 0;
            float bestY = float.MinValue;
            for (int i = 0; i < 6; i++)
            {
                float y = (rot * DiceModel.FaceNormals[i]).y;
                if (y > bestY) { bestY = y; best = i; }
            }
            return DiceModel.FaceNumbers[best];
        }

        /// <summary>Number that would end up on top after rolling 'steps' times toward 'dir' (a cardinal direction).</summary>
        public int PreviewTop(Vector3 dir, int steps = 1)
        {
            var axis = Vector3.Cross(Vector3.up, dir);
            return TopFor(Quaternion.AngleAxis(90f * steps, axis) * orientation);
        }

        static Quaternion SnapToAxes(Quaternion q)
        {
            Vector3 f = Snap(q * Vector3.forward), up = Snap(q * Vector3.up);
            return Quaternion.LookRotation(f, up);
        }

        static Vector3 Snap(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            if (ay >= az) return new Vector3(0f, Mathf.Sign(v.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(v.z));
        }
    }
}
