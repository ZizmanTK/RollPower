using UnityEngine;

namespace DiceHero
{
    /// <summary>Smoothly follows the dice from a fixed 3/4 top-down angle, looking north (+Z).</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 8.6f, -7f);
        public float smoothTime = 0.25f;

        Vector3 focus, velocity, shake;

        public void SnapToTarget()
        {
            if (target == null) return;
            focus = target.position;
            Apply();
        }

        public void Shake(float amount) { shake = Random.insideUnitSphere * amount; }

        void LateUpdate()
        {
            if (target == null) return;
            focus = Vector3.SmoothDamp(focus, target.position, ref velocity, smoothTime);
            shake = Vector3.Lerp(shake, Vector3.zero, 12f * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            transform.position = focus + offset + shake;
            transform.rotation = Quaternion.LookRotation(-offset + Vector3.up * 0.5f, Vector3.up);
        }
    }
}

