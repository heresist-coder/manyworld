using UnityEngine;
using UnityEngine.InputSystem;

namespace Manyworld
{
    /// <summary>어깨 너머 3인칭 카메라. 우클릭 조준 시 당겨진다.</summary>
    public class PlayerCamera : MonoBehaviour
    {
        public Transform target;
        public float sensitivity = 0.12f;
        public float yaw, pitch = 10f;
        public bool aiming;
        public bool vehicleMode;   // 차를 몰 때: 더 멀리, 어깨 오프셋 없이

        Camera cam;
        float currentDist = 3.6f;

        const float NormalDist = 3.6f, AimDist = 1.7f;
        const float NormalFov = 62f, AimFov = 42f;
        static readonly Vector3 PivotOffset = new Vector3(0, 1.65f, 0);
        const float Shoulder = 0.95f;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        public void Kick(float amount) => pitch = Mathf.Clamp(pitch - amount, -40f, 70f);

        void LateUpdate()
        {
            if (target == null) return;
            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue() * sensitivity * (aiming ? 0.6f : 1f);
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -40f, 70f);
            }
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var pivot = target.position + (vehicleMode ? new Vector3(0, 2.2f, 0) : PivotOffset);
            float wantDist = vehicleMode ? 7.5f : aiming ? AimDist : NormalDist;
            var dir = rot * new Vector3(vehicleMode ? 0f : Shoulder, 0, -wantDist);
            float len = dir.magnitude;

            // 벽에 파묻히지 않게
            if (Physics.SphereCast(pivot, 0.2f, dir.normalized, out var hit, len, ~(1 << 2), QueryTriggerInteraction.Ignore))
                len = Mathf.Max(0.3f, hit.distance - 0.05f);
            currentDist = Mathf.Lerp(currentDist, len, Time.deltaTime * 14f);
            if (len < currentDist) currentDist = len;

            transform.position = pivot + dir.normalized * currentDist;
            transform.rotation = rot;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, vehicleMode ? 66f : aiming ? AimFov : NormalFov, Time.deltaTime * 12f);
        }

        /// <summary>화면 중앙에서 쏜 광선이 닿는 점.</summary>
        public Ray CenterRay => cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
    }
}
