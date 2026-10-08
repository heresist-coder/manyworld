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
        public Transform rightHand;
        Vector3 lastPos;
        Vector2 smoothed;

        static readonly int MoveX = Animator.StringToHash("MoveX");
        static readonly int MoveZ = Animator.StringToHash("MoveZ");
        static readonly int AimHash = Animator.StringToHash("Aim");
        static readonly int DownHash = Animator.StringToHash("Down");

        public bool Aim { get; set; }
        public bool Down { get; set; }

        /// <summary>visual 아래에 모델을 붙인다. 성공하면 기존 도형 렌더러를 숨긴다.</summary>
        public static CharacterRig Attach(Transform visual, GameObject modelPrefab, float height, Color? tint = null)
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
            if (tint.HasValue)
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.materials) m.color *= tint.Value;

            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = art.humanoidController;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var rig = visual.root.gameObject.AddComponent<CharacterRig>();
            rig.animator = anim;
            rig.lastPos = visual.root.position;
            rig.rightHand = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;

            // 소총을 오른손에
            if (art.rifle != null && rig.rightHand != null)
            {
                var gun = Instantiate(art.rifle, rig.rightHand);
                gun.name = "Rifle";
                var gb = ArtCatalog.RendererBounds(gun);
                float gs = 0.95f / Mathf.Max(0.05f, Mathf.Max(gb.size.x, gb.size.y, gb.size.z));
                gun.transform.localScale = Vector3.one * gs / Mathf.Max(0.0001f, rig.rightHand.lossyScale.x);
                gun.transform.localPosition = new Vector3(0, 0.05f, 0.05f) / Mathf.Max(0.0001f, rig.rightHand.lossyScale.x);
                gun.transform.localRotation = Quaternion.Euler(0, 90, 90);
                foreach (var c in gun.GetComponentsInChildren<Collider>()) c.enabled = false;
            }
            return rig;
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
