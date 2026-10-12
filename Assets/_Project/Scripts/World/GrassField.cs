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

        /// <summary>삼각형 잎 여러 장이 바깥으로 기운 덤불. 법선은 위쪽으로 섞어 앞뒤 밝기가 비슷하게.</summary>
        static Mesh BladeClump(int seed, int blades, float height)
        {
            var rng = new System.Random(seed);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < blades; i++)
            {
                float ang = (float)(i / (double)blades * Mathf.PI * 2.0 + rng.NextDouble() * 0.8);
                var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                var side = new Vector3(-dir.z, 0, dir.x);
                float r0 = 0.03f + (float)rng.NextDouble() * 0.05f;
                float w = 0.05f + (float)rng.NextDouble() * 0.05f;
                float hgt = height * (0.55f + (float)rng.NextDouble() * 0.6f);
                float lean = hgt * (0.25f + (float)rng.NextDouble() * 0.45f);
                var b = dir * r0;
                var a0 = b - side * w;
                var a1 = b + side * w;
                var tip = dir * (r0 + lean) + Vector3.up * hgt + side * (float)(rng.NextDouble() - 0.5) * 0.06f;
                var n = Vector3.Cross(a1 - a0, tip - a0).normalized;
                n = Vector3.Lerp(n, Vector3.up, 0.65f).normalized;
                int k = verts.Count;
                verts.Add(a0); verts.Add(a1); verts.Add(tip);
                norms.Add(n); norms.Add(n); norms.Add(n);
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
            }
            var mesh = new Mesh { name = "GrassClump" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>덤불 메시 3종과 색 3종 (햇빛 받은 연두, 올리브, 그늘진 초록).</summary>
        public static void Clumps(out Mesh[] meshes, out Material[] materials)
        {
            if (clumpMeshes == null)
            {
                clumpMeshes = new[] { BladeClump(11, 9, 0.6f), BladeClump(23, 12, 0.8f), BladeClump(37, 7, 0.5f) };
                var colors = new[] { new Color(0.46f, 0.53f, 0.22f), new Color(0.4f, 0.46f, 0.2f), new Color(0.3f, 0.39f, 0.18f) };
                clumpMaterials = new Material[colors.Length];
                var template = Resources.Load<Material>("Materials/GrayboxOpaque");
                for (int i = 0; i < colors.Length; i++)
                {
                    var m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    m.name = "GrassClump_" + i;
                    m.color = colors[i];
                    m.SetFloat("_Smoothness", 0f);
                    m.SetFloat("_Cull", 0f); // 양면
                    m.enableInstancing = true;
                    clumpMaterials[i] = m;
                }
            }
            meshes = clumpMeshes;
            materials = clumpMaterials;
        }

        /// <summary>낱개 덤불 오브젝트 (길가·방 입구처럼 몇 개만 놓을 때).</summary>
        public static GameObject ClumpObject(Transform parent, Vector3 pos, float scale, System.Random rng)
        {
            Clumps(out var meshes, out var mats);
            var go = new GameObject("GrassClump", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0));
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<MeshFilter>().sharedMesh = meshes[rng.Next(meshes.Length)];
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mats[rng.Next(mats.Length)];
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
