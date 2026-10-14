using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Manyworld
{
    /// <summary>
    /// 호수 평면 반사 (목표 샷 05: 물가 숲과 노을 산이 거울처럼 비친다).
    /// 수면 아래로 뒤집은 카메라가 장면을 1/3 해상도 텍스처에 그리고, 물 셰이더가 화면 좌표를 위아래로 뒤집어 읽는다.
    /// 화면 공간 반사는 화면에 없는 것(카메라 뒤·화면 위로 벗어난 나무 꼭대기)을 비추지 못해 얼룩덜룩했다.
    /// 카메라가 호수 둘레(activeRadius) 안이고 수면 위일 때만 그린다 (장면을 한 번 더 그리는 비용).
    /// </summary>
    public class PlanarReflection : MonoBehaviour
    {
        public float waterY;
        public Vector2 center;
        public float activeRadius = 150f; // (420m는 흙길·숲길·방까지 덮어 모든 장면이 두 번 그려졌다: 01 22→39ms)
        public float lakeRadius = 90f;
        public float clipOffset = 0.15f;

        Camera refl;
        /// <summary>반사 카메라 (풀은 반사에 그리지 않는다: 수면에 비친 풀은 보이지 않고 5만 포기가 비싸다).</summary>
        public static Camera ReflectionCamera { get; private set; }
        RenderTexture rt;
        readonly Plane[] planes = new Plane[6];
        static readonly int TexId = Shader.PropertyToID("_PlanarReflectionTex");
        static readonly int OnId = Shader.PropertyToID("_PlanarReflectionOn");

        // 미리 한 번 그린다: 처음 그릴 때 셰이더·파이프라인 준비로 수 초 멈췄다 (빌드 측정 t05 첫 프레임 4초).
        // 현장 생성 직후(로딩 중)에 호수를 내려다보는 자리에서 한 번 그려 둔다
        System.Collections.IEnumerator Start()
        {
            yield return null;
            var cam = Camera.main;
            if (cam == null) yield break;
            Ensure(cam);
            refl.transform.SetPositionAndRotation(new Vector3(center.x, waterY - 30f, center.y - 120f), Quaternion.LookRotation(new Vector3(0, 0.3f, 1f)));
            refl.fieldOfView = cam.fieldOfView;
            refl.aspect = cam.aspect;
            refl.Render();
            refl.enabled = false;
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(OnId, 0f);
            if (refl != null) refl.enabled = false;
        }

        void OnDestroy()
        {
            Shader.SetGlobalFloat(OnId, 0f);
            if (rt != null) rt.Release();
            if (refl != null) Destroy(refl.gameObject);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var p = cam.transform.position;
            bool on = Vector2.Distance(new Vector2(p.x, p.z), center) < activeRadius && p.y > waterY + 0.2f;
            // 호수가 화면에 있을 때만
            if (on)
            {
                GeometryUtility.CalculateFrustumPlanes(cam, planes);
                on = GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(center.x, waterY, center.y), new Vector3(lakeRadius * 2f, 2f, lakeRadius * 2f)));
            }
            if (!on)
            {
                if (refl != null) refl.enabled = false;
                Shader.SetGlobalFloat(OnId, 0f);
                return;
            }
            Ensure(cam);

            // 거울 위치·방향: 위치는 수면 아래로, 앞은 위아래를 뒤집고, 위쪽은 수평 성분을 뒤집어 오른손 좌표를 유지한다
            // (오른쪽 벡터가 그대로라 이미지는 반사를 위아래로 뒤집은 것 → 셰이더가 1-v로 읽는다)
            var f = cam.transform.forward;
            var u = cam.transform.up;
            refl.transform.SetPositionAndRotation(new Vector3(p.x, 2f * waterY - p.y, p.z),
                Quaternion.LookRotation(new Vector3(f.x, -f.y, f.z), new Vector3(-u.x, u.y, -u.z)));
            refl.fieldOfView = cam.fieldOfView;
            refl.aspect = cam.aspect;
            refl.nearClipPlane = cam.nearClipPlane;
            refl.farClipPlane = Mathf.Min(cam.farClipPlane, 1800f);
            refl.ResetProjectionMatrix();
            // 수면 아래(호수 바닥·물속 집)는 비추지 않는다: 근평면을 수면에 맞춘 사선 투영
            var w2c = refl.worldToCameraMatrix;
            var cp = w2c.MultiplyPoint(new Vector3(p.x, waterY + clipOffset, p.z));
            var cn = w2c.MultiplyVector(Vector3.up).normalized;
            refl.projectionMatrix = refl.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));
            refl.enabled = true;
            Shader.SetGlobalTexture(TexId, rt);
            Shader.SetGlobalFloat(OnId, 1f);
        }

        void Ensure(Camera cam)
        {
            int w = Mathf.Max(256, cam.pixelWidth / 3), h = Mathf.Max(144, cam.pixelHeight / 3); // 1/3 해상도 (물결이 흔들어 거칠어도 티가 안 난다)
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf) { name = "PlanarReflection" };
                rt.Create();
                if (refl != null) refl.targetTexture = rt;
            }
            if (refl != null) return;
            var go = new GameObject("LakeReflectionCamera");
            go.transform.SetParent(transform, false);
            refl = go.AddComponent<Camera>();
            ReflectionCamera = refl;
            refl.targetTexture = rt;
            refl.depth = cam.depth - 1f; // 본 카메라보다 먼저
            refl.clearFlags = CameraClearFlags.Skybox;
            refl.cullingMask = cam.cullingMask & ~(1 << 5); // UI 빼고
            refl.allowMSAA = false;
            refl.useOcclusionCulling = false;
            var data = refl.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.antialiasing = AntialiasingMode.None;
            // 렌더러 기능이 없는 반사 전용 렌더러 (ReflectionRendererSetup). 없으면 기본 렌더러
            if (UniversalRenderPipeline.asset != null)
            {
                var list = UniversalRenderPipeline.asset.rendererDataList;
                for (int i = 0; i < list.Length; i++)
                    if (list[i] != null && list[i].name == "Reflection_Renderer") { data.SetRenderer(i); break; }
            }
            data.volumeLayerMask = 0; // 볼륨 효과(화면 공간 GI·색보정) 없이: 반사는 반 해상도라 GI가 비용만 컸다
        }
    }
}
