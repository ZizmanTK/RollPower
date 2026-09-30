using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Smoothly follows the dice from a fixed 3/4 top-down angle, looking north (+Z).
    /// Screen shake is trauma-based (shake = trauma², smooth Perlin noise) and scaled by the player's setting.
    /// On the title screen it slowly orbits the arena instead.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 10.4f, -8.4f);
        public float smoothTime = 0.25f;

        /// <summary>When true the camera orbits the arena centre (title / game over backdrop).</summary>
        public bool Orbit;
        float orbitAngle, orbitBlend;

        Vector3 focus, velocity;
        float trauma, fovKick, seed;
        Camera cam;
        float baseFov;

        void Awake()
        {
            cam = GetComponent<Camera>();
            baseFov = cam != null ? cam.fieldOfView : 40f;
            seed = Random.value * 100f;
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            focus = target.position;
            Apply(Vector3.zero, 0f);
        }

        /// <summary>Adds trauma (0..1). Kept for callers that used the old API.</summary>
        public void Shake(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        /// <summary>Brief zoom punch (mega slams, boss hits).</summary>
        public void Kick(float amount) => fovKick = Mathf.Max(fovKick, amount);

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            orbitBlend = Mathf.MoveTowards(orbitBlend, Orbit ? 1f : 0f, dt * 0.8f);
            orbitAngle += dt * 6f;

            Vector3 want = Vector3.Lerp(target.position, Vector3.zero, orbitBlend);
            focus = Vector3.SmoothDamp(focus, want, ref velocity, smoothTime, Mathf.Infinity, dt);

            trauma = Mathf.Max(0f, trauma - dt * 1.6f);
            fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 12f);
            float s = trauma * trauma * Settings.ShakeAmount;
            float t = Time.unscaledTime * 22f;
            Vector3 shake = new Vector3(Mathf.PerlinNoise(seed, t) - 0.5f, Mathf.PerlinNoise(seed + 10f, t) - 0.5f, Mathf.PerlinNoise(seed + 20f, t) - 0.5f) * 1.6f * s;
            float roll = (Mathf.PerlinNoise(seed + 30f, t) - 0.5f) * 6f * s;
            Apply(shake, roll);
            if (cam != null) cam.fieldOfView = baseFov - fovKick * 4f + orbitBlend * 6f;
        }

        void Apply(Vector3 shake, float roll)
        {
            Vector3 off = offset;
            if (orbitBlend > 0f)
            {
                // Wider, lower orbit around the arena for the menus.
                Vector3 orbit = Quaternion.Euler(0f, orbitAngle, 0f) * new Vector3(0f, 9.5f, -15f);
                float e = orbitBlend * orbitBlend * (3f - 2f * orbitBlend);
                off = Vector3.Lerp(offset, orbit, e);
            }
            transform.position = focus + off + shake;
            transform.rotation = Quaternion.LookRotation(-off + Vector3.up * 0.5f, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
