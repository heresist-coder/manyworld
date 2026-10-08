using UnityEngine;

namespace Manyworld
{
    /// <summary>게이트 막이 숨 쉬듯 일렁인다.</summary>
    public class GateShimmer : MonoBehaviour
    {
        public Material mat;
        public Light glow;
        public Color baseColor;

        void Update()
        {
            float t = Time.time;
            float pulse = 0.45f + Mathf.Sin(t * 1.7f) * 0.15f + Mathf.Sin(t * 5.3f) * 0.05f;
            if (mat != null) mat.SetColor("_EmissionColor", baseColor * pulse); // 은은하게. 블룸에 날아가지 않게
            if (glow != null) glow.intensity = 2.4f + pulse;
            transform.localRotation = Quaternion.Euler(90, t * 6f, 0);
        }
    }
}
