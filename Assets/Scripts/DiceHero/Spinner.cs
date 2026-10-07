using UnityEngine;

namespace DiceHero
{
    /// <summary>Turns a part about an axis of another transform (drone rotors about the drone's up).</summary>
    public class Spinner : MonoBehaviour
    {
        Transform axis; float speed;
        public void Set(Transform a, float s) { axis = a; speed = s; }
        void Update() { if (axis != null) transform.Rotate(axis.up, speed * Time.deltaTime, Space.World); }
    }
}
