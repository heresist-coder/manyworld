using UnityEngine;
using UnityEngine.InputSystem;

namespace Manyworld
{
    /// <summary>
    /// 어깨 너머 3인칭 카메라 (배틀그라운드 TPP 참고): 캐릭터 오른쪽 어깨 바로 뒤, 캐릭터는 화면 왼쪽 가운데를 크게 차지하고
    /// 조준점은 화면 중앙. 거리 2.9m, 어깨 0.55m, 높이 1.75m, 가로 시야각 80°(16:9에서 세로 51°). 우클릭 조준 시 어깨로 더 당겨진다.
    /// 예전 값(거리 3.6m, 어깨 0.95m, 세로 62°)은 캐릭터가 왼쪽 멀리 작게 서는 액션게임 구도였다.
    /// </summary>
    public class PlayerCamera : MonoBehaviour
    {
        public Transform target;
        public float sensitivity = 0.12f;
        public float yaw, pitch = 10f;
        public bool aiming;
        public bool vehicleMode;   // 차를 몰 때: 더 멀리, 어깨 오프셋 없이

        Camera cam;
        float currentDist = 2.9f, currentShoulder = 0.55f;
        /// <summary>QA: 값이 있으면 조준 상태를 덮어쓴다 (배치 모드엔 마우스가 없다).</summary>
        public static bool? QaAim;

        const float NormalDist = 2.9f, AimDist = 1.75f;
        const float NormalFov = 51f, AimFov = 42f;
        static readonly Vector3 PivotOffset = new Vector3(0, 1.75f, 0);
        const float Shoulder = 0.55f, AimShoulder = 0.62f; // 조준 땐 어깨를 더 빼서 머리가 시야를 가리지 않게

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        public void Kick(float amount) => pitch = Mathf.Clamp(pitch - amount, -40f, 70f);

        void LateUpdate()
        {
            if (target == null) return;
            bool aim = QaAim ?? aiming;
            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue() * sensitivity * (aim ? 0.6f : 1f);
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -40f, 70f);
            }
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var pivot = target.position + (vehicleMode ? new Vector3(0, 2.2f, 0) : PivotOffset);
            float wantDist = vehicleMode ? 7.5f : aim ? AimDist : NormalDist;
            currentShoulder = Mathf.Lerp(currentShoulder, vehicleMode ? 0f : aim ? AimShoulder : Shoulder, Time.deltaTime * 12f);
            var dir = rot * new Vector3(currentShoulder, 0, -wantDist);
            float len = dir.magnitude;

            // 벽에 파묻히지 않게
            if (Physics.SphereCast(pivot, 0.2f, dir.normalized, out var hit, len, ~(1 << 2), QueryTriggerInteraction.Ignore))
                len = Mathf.Max(0.3f, hit.distance - 0.05f);
            currentDist = Mathf.Lerp(currentDist, len, Time.deltaTime * 14f);
            if (len < currentDist) currentDist = len;

            transform.position = pivot + dir.normalized * currentDist;
            transform.rotation = rot;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, vehicleMode ? 66f : aim ? AimFov : NormalFov, Time.deltaTime * 12f);
        }

        /// <summary>화면 중앙에서 쏜 광선이 닿는 점.</summary>
        public Ray CenterRay => cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
    }
}
