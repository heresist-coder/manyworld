using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 사람 모델(Tripo, Mixamo 뼈대) + 소총 애니메이션. 아트가 없으면 그레이박스 도형을 그대로 쓴다.
    /// Animator 파라미터: MoveX, MoveZ (로컬 속도 m/s), Aim, Down.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        public Animator animator;
        public Transform rightHand, leftHand, rifle;
        Quaternion rifleBase = Quaternion.identity;
        Vector3 lastPos;
        Vector2 smoothed;

        static readonly int MoveX = Animator.StringToHash("MoveX");
        static readonly int MoveZ = Animator.StringToHash("MoveZ");
        static readonly int AimHash = Animator.StringToHash("Aim");
        static readonly int DownHash = Animator.StringToHash("Down");

        public bool Aim { get; set; }
        public bool Down { get; set; }

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
            rig.lastPos = visual.root.position;
            rig.rightHand = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;

            // 소총: 오른손에 쥐고 총구는 왼손 쪽을 향하게 매 프레임 맞춘다 (LateUpdate)
            rig.leftHand = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            if (riflePrefab != null && rig.rightHand != null)
            {
                var gun = Instantiate(riflePrefab, visual); // visual 아래: 차에 타면 같이 숨는다
                gun.name = "Rifle";
                foreach (var c in gun.GetComponentsInChildren<Collider>()) c.enabled = false;
                gun.transform.rotation = Quaternion.identity;
                gun.transform.localScale = Vector3.one;
                var gb = ArtCatalog.RendererBounds(gun);
                float len = Mathf.Max(gb.size.x, gb.size.y, gb.size.z);
                gun.transform.localScale = Vector3.one * (1.05f / Mathf.Max(0.05f, len));
                rig.rifle = gun.transform;
                // 메시의 가장 긴 축을 총열 방향(+Z)으로
                rig.rifleBase = gb.size.x >= gb.size.z && gb.size.x >= gb.size.y ? Quaternion.Euler(0, -90, 0) : Quaternion.identity;
            }
            return rig;
        }

        void LateUpdate()
        {
            if (rifle == null || rightHand == null) return;
            if (Down)
            {
                rifle.gameObject.SetActive(false);
                return;
            }
            rifle.gameObject.SetActive(true);
            var aimDir = leftHand != null ? (leftHand.position - rightHand.position) : transform.forward;
            if (aimDir.sqrMagnitude < 0.01f) aimDir = transform.forward;
            // 손 사이가 짧으면 몸 앞쪽으로 보정
            aimDir = Vector3.Lerp(aimDir.normalized, transform.forward, 0.35f).normalized;
            rifle.rotation = Quaternion.LookRotation(aimDir, transform.up) * rifleBase;
            rifle.position = rightHand.position + aimDir * 0.18f;
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
            animator.SetBool(AimHash, Aim);
            animator.SetBool(DownHash, Down);
        }
    }
}
