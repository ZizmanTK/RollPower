using System;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Gliding dice. Steer with WASD / arrows; the dice keeps its momentum like on ice.
    /// Hitting a rock fast enough trips it into a 90° roll in the travel direction (a log: 180°),
    /// which changes the number on top.
    /// </summary>
    public class DiceController : MonoBehaviour
    {
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
        public float DashCooldownTotal => dashCooldown * RunStats.Current.dashCooldownMul;
        public int LastRollSteps => rollSteps;
        public event Action Dashed;
        float dashTime;

        /// <summary>Burst of speed in the steering direction (or current travel direction). Makes deliberate trips easy.</summary>
        public void Dash()
        {
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
            velocity += new Vector3(input.x, 0f, input.y) * acceleration * dt;
            velocity *= Mathf.Exp(-glideDrag * dt);
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
                if (tripCooldown <= 0f && velocity.magnitude >= tripSpeed)
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
            rollSteps = ob.RollSteps;
            rollAxis = Vector3.Cross(Vector3.up, dir);
            rollStartRot = orientation;
            rollFrom = transform.position;

            // Land just past the far side of the obstacle (at least one dice width per roll step).
            Vector3 op = ob.transform.position;
            float half = Mathf.Abs(dir.x) > 0f ? ob.halfExtents.x : ob.halfExtents.y;
            float along = Vector3.Dot(op - rollFrom, dir) + half + 0.6f;
            along = Mathf.Max(along, rollSteps * 1f);
            rollTo = rollFrom + dir * along;
            float limit = World.HalfSize - 0.5f;
            rollTo.x = Mathf.Clamp(rollTo.x, -limit, limit);
            rollTo.z = Mathf.Clamp(rollTo.z, -limit, limit);

            rollTime = rollSteps == 2 ? doubleRollDuration : rollDuration;
            rollHop = rollSteps == 2 ? doubleHopHeight : hopHeight;
            rollT = 0f;
            rolling = true;
            velocity = dir * velocity.magnitude * 0.55f;
            Tripped?.Invoke(ob);
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
