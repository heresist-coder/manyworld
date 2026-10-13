using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 사람 모델(Synty·Tripo, 휴머노이드) + Mixamo 소총 애니메이션. 아트가 없으면 그레이박스 도형을 그대로 쓴다.
    /// Animator 파라미터: MoveX, MoveZ (로컬 속도 m/s), Aim, Down.
    /// 총 들기 (gun-pose-unity, CUBE 125 3인칭 방식):
    /// 총은 오른손 손바닥에 고정한다. 손 안의 총 각도는 조준 클립 자세에서 한 번 잰다 (총열 = 몸 정면, 위 = 몸 위).
    /// 그 뒤로는 클립이 손을 움직이는 대로 총이 따라간다 (서 있을 땐 로우 레디, 걸을 땐 흔들림).
    /// LateUpdate에서 조준 중이면 가슴을 돌려 총열을 조준 방향에 맞추고, 왼손은 총 앞 손잡이(받침)로 IK.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        public Animator animator;
        public Transform rightHand, leftHand, rifle;
        Transform chest, armLU, armLL, armRU, model;
        Transform idxR, midR, idxL, midL;
        Transform[] triggerFinger;
        Vector3 supportLocal; // 총 기준 왼손 받침 자리 (+Z 총구, +Y 위)
        Vector3 lastPos;
        Vector2 smoothed;
        float aimW;

        static readonly int MoveX = Animator.StringToHash("MoveX");
        static readonly int MoveZ = Animator.StringToHash("MoveZ");
        static readonly int AimHash = Animator.StringToHash("Aim");
        static readonly int DownHash = Animator.StringToHash("Down");

        public bool Aim { get; set; }
        public bool Down { get; set; }
        /// <summary>QA: 값이 있으면 모든 인물의 조준 상태를 덮어쓴다.</summary>
        public static bool? QaAim;
        bool Aiming => QaAim ?? Aim;
        /// <summary>조준 방향 (월드). 0이면 몸 정면.</summary>
        public Vector3 AimDir { get; set; }

        /// <summary>QA: 마지막 프레임의 왼손 받침 오차(m)와 조준 중 총열-조준 방향 각도.</summary>
        public float SupportError { get; private set; }
        public float AimErrorDeg { get; private set; }

        /// <summary>visual 아래에 모델을 붙인다. 성공하면 기존 도형 렌더러를 숨긴다.</summary>
        public static CharacterRig Attach(Transform visual, GameObject modelPrefab, float height, GameObject riflePrefab = null)
        {
            var art = ArtCatalog.Instance;
            if (modelPrefab == null || art == null || art.humanoidController == null) return null;

            foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.enabled = false;

            var model = Instantiate(modelPrefab, visual);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            var b = ArtCatalog.RendererBounds(model);
            float s = height / Mathf.Max(0.1f, b.size.y);
            model.transform.localScale = Vector3.one * s;
            b = ArtCatalog.RendererBounds(model);
            model.transform.position += Vector3.up * (visual.position.y - b.min.y);
            foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = art.humanoidController;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var rig = visual.root.gameObject.AddComponent<CharacterRig>();
            rig.animator = anim;
            rig.model = model.transform;
            rig.lastPos = visual.root.position;
            if (anim.isHuman)
            {
                rig.rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                rig.leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                rig.chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
                rig.armLU = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rig.armLL = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rig.armRU = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                rig.idxR = anim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                rig.midR = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                rig.idxL = anim.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
                rig.midL = anim.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                rig.triggerFinger = new[]
                {
                    anim.GetBoneTransform(HumanBodyBones.RightIndexProximal),
                    anim.GetBoneTransform(HumanBodyBones.RightIndexIntermediate),
                    anim.GetBoneTransform(HumanBodyBones.RightIndexDistal),
                };
            }
            if (riflePrefab != null && rig.rightHand != null) rig.MountRifle(visual, riflePrefab);
            return rig;
        }

        /// <summary>
        /// 총 메시를 총 틀(+Z 총구, +Y 위, 원점 = 오른손 손바닥 자리) 아래에 놓고, 조준 클립 자세에서 손에 고정한다.
        /// 손잡이·받침 자리는 방아쇠 부품 위치에서 잡는다 (신티 총은 _Trigger 부품이 따로 있다).
        /// </summary>
        void MountRifle(Transform visual, GameObject riflePrefab)
        {
            // 메시 축 재기: 원점·정방향·크기 1로 꺼내 본다
            var gun = Instantiate(riflePrefab);
            gun.name = "RifleMesh";
            foreach (var c in gun.GetComponentsInChildren<Collider>()) c.enabled = false;
            gun.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            gun.transform.localScale = Vector3.one;
            var gb = ArtCatalog.RendererBounds(gun);
            Vector3 trig = gb.center + new Vector3(0, -gb.extents.y * 0.3f, -gb.extents.z * 0.2f);
            foreach (var t in gun.GetComponentsInChildren<Transform>())
                if (t.name.Contains("Trigger")) { var tr = t.GetComponent<Renderer>(); trig = tr != null ? tr.bounds.center : t.position; break; }
            // 가장 긴 축이 총열. 방아쇠는 개머리 쪽에 있으니 총구는 방아쇠에서 가운데로 가는 쪽
            bool alongX = gb.size.x >= gb.size.z;
            float sign = alongX ? Mathf.Sign(gb.center.x - trig.x) : Mathf.Sign(gb.center.z - trig.z);
            if (sign == 0f) sign = 1f;
            var toFrame = alongX ? Quaternion.Euler(0, sign > 0 ? -90f : 90f, 0) : Quaternion.Euler(0, sign > 0 ? 0f : 180f, 0);
            float len = Mathf.Max(gb.size.x, gb.size.z);
            // 신티 인물은 머리가 크고 팔이 짧다: 실제 길이(1m)면 왼손이 받침에 닿지 않고 총이 막대처럼 길어 보인다
            float scale = 0.86f / Mathf.Max(0.05f, len);

            // 총 틀 기준: 방아쇠 자리 → 손바닥(방아쇠울 뒤 손목 자리), 받침(방아쇠 앞 27cm, 총열 아래)
            Vector3 trigF = toFrame * (trig * scale);
            Vector3 gripF = trigF + new Vector3(0f, -0.035f, -0.075f);
            supportLocal = trigF + new Vector3(0f, -0.015f, 0.21f) - gripF;

            var frame = new GameObject("Rifle").transform;
            frame.SetParent(visual, false);
            gun.transform.SetParent(frame, false);
            gun.transform.localRotation = toFrame;
            gun.transform.localScale = Vector3.one * scale;
            gun.transform.localPosition = -gripF;
            rifle = frame;
            Calibrate();
        }

        /// <summary>조준 클립 한 프레임을 평가해 손 안의 총 각도를 정한다: 총열 = 몸 정면, 위 = 몸 위.</summary>
        void Calibrate()
        {
            if (rifle == null || rightHand == null) return;
            var culling = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // 화면 밖이어도 뼈를 움직여 잰다
            animator.SetBool(AimHash, true);
            animator.Play("AimMove", 0, 0f);
            animator.Update(0f);
            var want = Quaternion.LookRotation(transform.forward, transform.up);
            rifle.SetParent(rightHand, false);
            rifle.localScale = new Vector3(1f / rightHand.lossyScale.x, 1f / rightHand.lossyScale.y, 1f / rightHand.lossyScale.z);
            rifle.rotation = want;
            rifle.position = PalmR();
            animator.SetBool(AimHash, Aiming);
            animator.Play("Move", 0, 0f);
            animator.Update(0f);
            animator.cullingMode = culling;
        }

        Vector3 PalmR() => idxR != null && midR != null ? Vector3.Lerp(rightHand.position, (idxR.position + midR.position) * 0.5f, 0.6f) : rightHand.position;
        Vector3 PalmL() => idxL != null && midL != null ? Vector3.Lerp(leftHand.position, (idxL.position + midL.position) * 0.5f, 0.6f) : leftHand.position;

        void LateUpdate()
        {
            if (rifle == null || rightHand == null) return;
            if (Down)
            {
                rifle.gameObject.SetActive(false);
                return;
            }
            rifle.gameObject.SetActive(true);
            if (animator == null || !animator.isActiveAndEnabled) return;

            // 조준: 가슴을 돌려 총열을 조준 방향에 (총은 손에, 손은 팔에, 팔은 가슴에 달려 있으니 같이 돈다)
            aimW = Mathf.MoveTowards(aimW, Aiming ? 1f : 0f, Time.deltaTime * 6f);
            var want = !QaAim.HasValue && AimDir.sqrMagnitude > 0.01f ? AimDir.normalized : transform.forward;
            if (chest != null && aimW > 0.001f)
                for (int i = 0; i < 2; i++)
                    chest.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(rifle.forward, want), aimW) * chest.rotation;
            AimErrorDeg = Vector3.Angle(rifle.forward, want);

            // 서 있기·걷기(로우 레디): 클립은 총구를 40° 가까이 떨군다. 손목을 들어 18° 아래로 (방향은 클립 그대로)
            if (aimW < 0.999f)
            {
                var cur = rifle.forward;
                var flat = Vector3.ProjectOnPlane(cur, Vector3.up);
                if (flat.sqrMagnitude > 0.01f)
                {
                    var low = Quaternion.AngleAxis(18f, Vector3.Cross(Vector3.up, flat.normalized)) * flat.normalized;
                    rightHand.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, low), 1f - aimW) * rightHand.rotation;
                }
            }

            // 방아쇠에 검지를 건다 (Mixamo 클립은 검지를 곧게 편다)
            if (Aiming && triggerFinger != null && idxR != null && midR != null)
            {
                var axis = Vector3.Cross(rifle.forward, Vector3.up).sqrMagnitude > 0.01f ? -rifle.right : transform.right;
                float[] bend = { 20f, 30f, 15f };
                for (int i = 0; i < 3; i++) if (triggerFinger[i] != null) triggerFinger[i].rotation = Quaternion.AngleAxis(bend[i] * aimW, axis) * triggerFinger[i].rotation;
            }

            // 왼손: 받침 자리로. 손 회전은 클립 그대로 (Mixamo 소총 클립의 왼손은 이미 총열을 받치는 모양)
            if (leftHand != null && armLU != null && armLL != null)
            {
                var target = rifle.TransformPoint(supportLocal); // 총 틀의 월드 축척은 1
                for (int i = 0; i < 2; i++)
                {
                    var wrist = target - (PalmL() - leftHand.position);
                    // 팔꿈치: 몸 바깥(오른 어깨→왼 어깨)과 아래로. 클립이 몸을 비틀어 서므로 몸통 기준이 아니라 어깨 기준
                    var outward = armRU != null ? (armLU.position - armRU.position).normalized : -transform.right;
                    var pole = outward * 0.92f + Vector3.down * 0.6f;
                    TwoBonePole(armLU, armLL, leftHand, wrist, pole, model);
                }
                SupportError = Vector3.Distance(PalmL(), target);
            }
        }

        /// <summary>두 뼈 IK — 팔꿈치가 갈 방향(pole)을 받아 해석적으로 푼다. 축척 없는 모델 공간에서 (CUBE 125 ActorView.TwoBonePole).</summary>
        public static void TwoBonePole(Transform up, Transform low, Transform end, Vector3 target, Vector3 pole, Transform frame = null)
        {
            if (up == null || low == null || end == null) return;
            Vector3 a = frame != null ? frame.InverseTransformPoint(up.position) : up.position;
            Vector3 b = frame != null ? frame.InverseTransformPoint(low.position) : low.position;
            Vector3 c = frame != null ? frame.InverseTransformPoint(end.position) : end.position;
            if (frame != null) { target = frame.InverseTransformPoint(target); pole = frame.InverseTransformVector(pole); }
            float l1 = Vector3.Distance(a, b), l2 = Vector3.Distance(b, c);
            if (l1 < 1e-4f || l2 < 1e-4f) return;
            Vector3 toT = target - a;
            float d = toT.magnitude;
            if (d < 1e-4f) return;
            d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 1e-3f, (l1 + l2) * 0.999f);
            Vector3 n = toT.normalized;
            Vector3 tgt = a + n * d;
            float x = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
            Vector3 perp = Vector3.ProjectOnPlane(pole, n);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.ProjectOnPlane(Vector3.up, n);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.ProjectOnPlane(Vector3.right, n);
            Vector3 elbow = a + n * x + perp.normalized * h;
            Quaternion q = Quaternion.FromToRotation(b - a, elbow - a);
            if (frame != null) q = frame.rotation * q * Quaternion.Inverse(frame.rotation);
            up.rotation = q * up.rotation;
            Vector3 actualB = frame != null ? frame.InverseTransformPoint(low.position) : low.position;
            Vector3 actualC = frame != null ? frame.InverseTransformPoint(end.position) : end.position;
            q = Quaternion.FromToRotation(actualC - actualB, tgt - actualB);
            if (frame != null) q = frame.rotation * q * Quaternion.Inverse(frame.rotation);
            low.rotation = q * low.rotation;
        }

        void Update()
        {
            if (animator == null) return;
            var pos = transform.position;
            var v = Time.deltaTime > 0 ? (pos - lastPos) / Time.deltaTime : Vector3.zero;
            lastPos = pos;
            var local = transform.InverseTransformDirection(v);
            smoothed = Vector2.Lerp(smoothed, new Vector2(local.x, local.z), Time.deltaTime * 10f);
            animator.SetFloat(MoveX, smoothed.x);
            animator.SetFloat(MoveZ, smoothed.y);
            animator.SetBool(AimHash, Aiming);
            animator.SetBool(DownHash, Down);
        }
    }
}
