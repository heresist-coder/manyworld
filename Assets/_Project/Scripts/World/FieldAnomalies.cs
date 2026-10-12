using UnityEngine;

namespace Manyworld
{
    /// <summary>떠 있는 집: 아주 천천히 오르내리고 조금 돈다.</summary>
    public class FloatingBob : MonoBehaviour
    {
        public float amplitude = 0.6f;
        Vector3 basePos;
        Quaternion baseRot;
        float seed;

        void Start()
        {
            basePos = transform.position;
            baseRot = transform.rotation;
            seed = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time * 0.25f + seed;
            transform.position = basePos + Vector3.up * Mathf.Sin(t) * amplitude;
            transform.rotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.7f) * 1.2f, Mathf.Sin(t * 0.3f) * 3f, 0);
        }
    }

    /// <summary>보이지 않는 몸이 오가며 그림자만 길 위를 건넌다.</summary>
    public class DriftingShadow : MonoBehaviour
    {
        public Vector3 from, to;
        public float period = 40f;

        void Update()
        {
            float t = Mathf.PingPong(Time.time / period * 2f, 1f);
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
        }
    }
}
