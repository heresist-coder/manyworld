using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Manyworld
{
    /// <summary>
    /// 숲 현장의 풀. 터레인 디테일 대신 직접 GPU 인스턴싱으로 그린다 (신티 풀 메시를 무광 URP Lit로).
    /// 32m 칸으로 나눠 카메라 거리·시야로 거르고, 모든 카메라 렌더 직전에 그린다 (캡처 카메라 포함).
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        class Batch
        {
            public Mesh mesh;
            public Material material;
            public Bounds bounds;
            public Matrix4x4[] matrices;
        }

        public float drawDistance = 120f;
        public int InstanceCount { get; private set; }
        readonly List<Batch> batches = new List<Batch>();
        readonly Plane[] planes = new Plane[6];

        /// <summary>한 종류의 풀을 칸별로 묶어 넣는다.</summary>
        public void Add(Mesh mesh, Material material, List<Matrix4x4> instances, float chunk = 32f)
        {
            if (mesh == null || material == null || instances.Count == 0) return;
            var groups = new Dictionary<Vector2Int, List<Matrix4x4>>();
            foreach (var m in instances)
            {
                var p = m.GetColumn(3);
                var key = new Vector2Int(Mathf.FloorToInt(p.x / chunk), Mathf.FloorToInt(p.z / chunk));
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<Matrix4x4>();
                list.Add(m);
            }
            foreach (var g in groups.Values)
            {
                for (int start = 0; start < g.Count; start += 1000)
                {
                    int n = Mathf.Min(1000, g.Count - start);
                    var arr = g.GetRange(start, n).ToArray();
                    var b = new Bounds(arr[0].GetColumn(3), Vector3.one);
                    foreach (var m in arr) b.Encapsulate(m.GetColumn(3));
                    b.Expand(new Vector3(2f, 3f, 2f));
                    batches.Add(new Batch { mesh = mesh, material = material, bounds = b, matrices = arr });
                }
            }
            InstanceCount += instances.Count;
        }

        // ---------------- 뾰족한 로우폴리 풀 덤불 (목표 샷의 풀) ----------------

        static Mesh[] clumpMeshes;
        static Material[] clumpMaterials;

        /// <summary>
        /// 잎 여러 장이 바깥으로 휘어 기운 덤불. 잎마다 아래 사각형 + 위 삼각형 두 마디로 휜다.
        /// uv.y는 높이 비율(풀 셰이더가 뿌리→끝 색과 바람에 쓴다). 법선은 위쪽으로 섞어 앞뒤 밝기가 비슷하게.
        /// </summary>
        static Mesh BladeClump(int seed, int blades, float height)
        {
            var rng = new System.Random(seed);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < blades; i++)
            {
                float ang = (float)(i / (double)blades * Mathf.PI * 2.0 + rng.NextDouble() * 0.9);
                var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                var side = new Vector3(-dir.z, 0, dir.x);
                float r0 = 0.02f + (float)rng.NextDouble() * 0.07f;
                float w = 0.045f + (float)rng.NextDouble() * 0.045f;
                float hgt = height * (0.5f + (float)rng.NextDouble() * 0.65f);
                float lean = hgt * (0.2f + (float)rng.NextDouble() * 0.5f);
                var b = dir * r0;
                // 아래 마디는 거의 곧게, 끝으로 갈수록 바깥으로 휜다
                var midC = b + dir * lean * 0.25f + Vector3.up * hgt * 0.55f;
                var tip = b + dir * lean + Vector3.up * hgt + side * (float)(rng.NextDouble() - 0.5) * 0.06f;
                var p0 = b - side * w; var p1 = b + side * w;
                var p2 = midC - side * w * 0.72f; var p3 = midC + side * w * 0.72f;
                var n = Vector3.Cross(p1 - p0, tip - p0).normalized;
                n = Vector3.Lerp(n, Vector3.up, 0.6f).normalized;
                int k = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3); verts.Add(tip);
                for (int q = 0; q < 5; q++) norms.Add(n);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 0.55f)); uvs.Add(new Vector2(1, 0.55f)); uvs.Add(new Vector2(0.5f, 1f));
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                tris.Add(k + 1); tris.Add(k + 2); tris.Add(k + 3);
                tris.Add(k + 2); tris.Add(k + 4); tris.Add(k + 3);
            }
            var mesh = new Mesh { name = "GrassClump" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>덤불 메시 3종과 머티리얼 3종 (볕 받은 연두, 올리브, 마른 기운이 많은 것).</summary>
        public static void Clumps(out Mesh[] meshes, out Material[] materials)
        {
            if (clumpMeshes == null)
            {
                clumpMeshes = new[] { BladeClump(11, 14, 0.6f), BladeClump(23, 18, 0.8f), BladeClump(37, 10, 0.5f) };
                var grass = Resources.Load<Material>("Materials/Grass");
                var tips = new[] { new Color(0.5f, 0.58f, 0.23f), new Color(0.43f, 0.51f, 0.2f), new Color(0.55f, 0.56f, 0.27f) };
                var dry = new[] { 0.45f, 0.35f, 0.75f };
                clumpMaterials = new Material[tips.Length];
                var template = Resources.Load<Material>("Materials/GrayboxOpaque");
                for (int i = 0; i < tips.Length; i++)
                {
                    Material m;
                    if (grass != null)
                    {
                        m = new Material(grass);
                        m.SetColor("_TipColor", tips[i]);
                        m.SetFloat("_DryAmount", dry[i]);
                    }
                    else
                    {
                        m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        m.color = tips[i] * 0.85f;
                        m.SetFloat("_Smoothness", 0f);
                        m.SetFloat("_Cull", 0f); // 양면
                    }
                    m.name = "GrassClump_" + i;
                    m.enableInstancing = true;
                    clumpMaterials[i] = m;
                }
            }
            meshes = clumpMeshes;
            materials = clumpMaterials;
        }

        /// <summary>
        /// 심을 풀 종류 (메시, 머티리얼, 비율, 크기). Stylized Grass Shader 패키지가 카탈로그에 있으면 그 풀(지형 색 섞기·바람·역광),
        /// 없으면 직접 만든 덤불 3종 × 색 3종.
        /// </summary>
        public static List<(Mesh mesh, Material mat, float weight, float scale)> Kinds()
        {
            var kinds = new List<(Mesh, Material, float, float)>();
            var art = ArtCatalog.Instance;
            if (art != null && art.sgsMeshes != null && art.sgsMeshes.Length > 0)
            {
                for (int i = 0; i < art.sgsMeshes.Length; i++)
                    if (art.sgsMeshes[i] != null && art.sgsMaterials[i] != null)
                        kinds.Add((art.sgsMeshes[i], art.sgsMaterials[i], art.sgsWeights[i], 1f));
                if (kinds.Count > 0) return kinds;
            }
            Clumps(out var meshes, out var mats);
            for (int i = 0; i < meshes.Length; i++)
            for (int c = 0; c < mats.Length; c++)
                kinds.Add((meshes[i], mats[c], c == 2 ? 0.6f : 1f, 1f));
            return kinds;
        }

        static List<(Mesh mesh, Material mat, float weight, float scale)> objectKinds;

        /// <summary>낱개 덤불 오브젝트 (길가·방 입구처럼 몇 개만 놓을 때).</summary>
        public static GameObject ClumpObject(Transform parent, Vector3 pos, float scale, System.Random rng)
        {
            objectKinds ??= Kinds();
            // 낱개는 키 큰 풀 위주 (꽃·토끼풀 제외: 비율이 높은 앞쪽 종류만)
            var kind = objectKinds[rng.Next(Mathf.Min(objectKinds.Count, 4))];
            var go = new GameObject("GrassClump", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0));
            go.transform.localScale = Vector3.one * scale * kind.scale;
            go.GetComponent<MeshFilter>().sharedMesh = kind.mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = kind.mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += Draw;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

        void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            var p = cam.transform.position;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            float d2 = drawDistance * drawDistance;
            foreach (var b in batches)
            {
                if (b.bounds.SqrDistance(p) > d2) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, b.bounds)) continue;
                var rp = new RenderParams(b.material)
                {
                    camera = cam,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = true,
                    worldBounds = b.bounds,
                    layer = gameObject.layer,
                };
                Graphics.RenderMeshInstanced(rp, b.mesh, 0, b.matrices);
            }
        }
    }
}
