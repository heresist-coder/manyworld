using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Manyworld
{
    /// <summary>
    /// 지형 나무를 GPU 인스턴싱으로 그린다 (그림자 포함). 지형은 나무를 한 그루씩 따로 그려서
    /// 숲 현장 1만 8천 그루가 프레임의 절반을 먹었다 (빌드 측정: 01 흙길 31.8ms → 나무 끔 14.8ms).
    /// 나무 자리·충돌은 그대로 지형(TerrainData·TerrainCollider)이 갖고, 그리기만 여기서 한다.
    /// 64m 칸으로 묶어 거리·시야로 거른다. 가까운 칸은 시야 밖이어도 그린다 (등 뒤 나무의 그림자가 화면에 드리운다).
    /// </summary>
    public class TreeField : MonoBehaviour
    {
        class Batch
        {
            public Mesh mesh;
            public int submesh;
            public Material material;
            public Bounds bounds;
            public Matrix4x4[] matrices;
            public float minDistance, maxDistance = float.MaxValue; // 먼 LOD는 거리 구간에서만
        }

        public float drawDistance = 1500f;
        public float shadowKeepDistance = 160f;
        public float shadowCastDistance = 180f; // 이보다 먼 나무는 그림자를 그리지 않는다 (그림자 4단 패스가 나무 비용의 큰 몫)
        public int InstanceCount { get; private set; }
        readonly List<Batch> batches = new List<Batch>();
        readonly Plane[] planes = new Plane[6];

        /// <summary>지형의 나무를 옮겨 담고 지형은 나무를 그리지 않게 한다.</summary>
        public static TreeField FromTerrain(Terrain terrain, float chunk = 64f)
        {
            var td = terrain.terrainData;
            var protos = td.treePrototypes;
            // 원형마다: (메시, 머티리얼들, 뿌리 기준 행렬)
            var parts = new List<(Mesh mesh, Material[] mats, Matrix4x4 local)>[protos.Length];
            for (int i = 0; i < protos.Length; i++)
            {
                parts[i] = new List<(Mesh, Material[], Matrix4x4)>();
                var prefab = protos[i].prefab;
                if (prefab == null) continue;
                var rootInv = prefab.transform.worldToLocalMatrix;
                var lod = prefab.GetComponent<LODGroup>();
                var renderers = lod != null && lod.GetLODs().Length > 0 ? lod.GetLODs()[0].renderers : prefab.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    var mf = r != null ? r.GetComponent<MeshFilter>() : null;
                    if (mf == null || mf.sharedMesh == null || !(r is MeshRenderer)) continue;
                    parts[i].Add((mf.sharedMesh, r.sharedMaterials, rootInv * r.transform.localToWorldMatrix));
                }
            }
            // 먼 거리 LOD (TreeLods: 신티 LOD1·2 메시, LOD0과 같은 머티리얼)
            var lodMeshes = new Mesh[protos.Length][];
            var lodFrom = new float[protos.Length][];
            for (int i = 0; i < protos.Length; i++)
            {
                var tl = protos[i].prefab != null ? protos[i].prefab.GetComponent<TreeLods>() : null;
                if (tl == null || tl.lods == null || tl.lods.Length == 0 || parts[i].Count != 1) continue;
                lodMeshes[i] = tl.lods;
                lodFrom[i] = tl.fromDistance;
            }
            // 칸별 행렬 모으기 (LOD 단계마다 따로)
            var groups = new Dictionary<(int proto, int part, int sub, Vector2Int cell, int lod), List<Matrix4x4>>();
            var origin = terrain.transform.position;
            foreach (var ti in td.treeInstances)
            {
                var list = parts[ti.prototypeIndex];
                if (list.Count == 0) continue;
                var wp = Vector3.Scale(ti.position, td.size) + origin;
                var trs = Matrix4x4.TRS(wp, Quaternion.Euler(0f, ti.rotation * Mathf.Rad2Deg, 0f), new Vector3(ti.widthScale, ti.heightScale, ti.widthScale));
                var cell = new Vector2Int(Mathf.FloorToInt(wp.x / chunk), Mathf.FloorToInt(wp.z / chunk));
                int levels = lodMeshes[ti.prototypeIndex] != null ? lodMeshes[ti.prototypeIndex].Length + 1 : 1;
                for (int p = 0; p < list.Count; p++)
                {
                    var m = trs * list[p].local;
                    for (int lod = 0; lod < levels; lod++)
                    {
                        var mesh = lod == 0 ? list[p].mesh : lodMeshes[ti.prototypeIndex][lod - 1];
                        for (int s = 0; s < mesh.subMeshCount && s < list[p].mats.Length; s++)
                        {
                            var key = (ti.prototypeIndex, p, s, cell, lod);
                            if (!groups.TryGetValue(key, out var g)) groups[key] = g = new List<Matrix4x4>();
                            g.Add(m);
                        }
                    }
                }
            }
            var go = new GameObject("TreeField");
            go.transform.SetParent(terrain.transform.parent, false);
            var field = go.AddComponent<TreeField>();
            foreach (var kv in groups)
            {
                var part = parts[kv.Key.proto][kv.Key.part];
                var mat = part.mats[kv.Key.sub];
                if (mat == null) continue;
                if (!mat.enableInstancing) mat = InstancedCopy(mat); // 카탈로그가 켜 두지만, 빠진 게 있어도 나무가 사라지지 않게
                int lodLevel = kv.Key.lod;
                var drawMesh = lodLevel == 0 ? part.mesh : lodMeshes[kv.Key.proto][lodLevel - 1];
                var from = lodFrom[kv.Key.proto];
                float minD = lodLevel == 0 ? 0f : from[lodLevel - 1];
                float maxD = from == null ? float.MaxValue : lodLevel < from.Length ? from[lodLevel] : float.MaxValue;
                var meshBounds = drawMesh.bounds;
                for (int start = 0; start < kv.Value.Count; start += 1000)
                {
                    int n = Mathf.Min(1000, kv.Value.Count - start);
                    var arr = kv.Value.GetRange(start, n).ToArray();
                    var b = new Bounds(arr[0].MultiplyPoint3x4(meshBounds.center), Vector3.zero);
                    foreach (var m in arr)
                    {
                        b.Encapsulate(m.MultiplyPoint3x4(meshBounds.min));
                        b.Encapsulate(m.MultiplyPoint3x4(meshBounds.max));
                    }
                    field.batches.Add(new Batch { mesh = drawMesh, submesh = kv.Key.sub, material = mat, bounds = b, matrices = arr, minDistance = minD, maxDistance = maxD });
                }
            }
            field.InstanceCount = td.treeInstanceCount;
            terrain.drawTreesAndFoliage = false; // 충돌은 TerrainCollider가 계속 맡는다
            Debug.Log($"[Manyworld] 나무 인스턴싱: {field.InstanceCount}그루, 묶음 {field.batches.Count}, 먼 LOD 원형 {System.Linq.Enumerable.Count(lodMeshes, l => l != null)}/{protos.Length}");
            return field;
        }

        static readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        static Material InstancedCopy(Material m)
        {
            if (!copies.TryGetValue(m, out var c) || c == null) copies[m] = c = new Material(m) { enableInstancing = true, name = m.name + "_Inst" };
            return c;
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += Draw;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

        void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            var p = cam.transform.position;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            float d2 = drawDistance * drawDistance, k2 = shadowKeepDistance * shadowKeepDistance, c2 = shadowCastDistance * shadowCastDistance;
            // 호수 반사 카메라: 시야 밖은 그리지 않고 그림자도 없다 (가까운 칸을 다 그리면 반사가 본 화면만큼 비쌌다)
            if (cam == PlanarReflection.ReflectionCamera) { k2 = 0f; c2 = 0f; d2 = Mathf.Min(d2, 900f * 900f); }
            foreach (var b in batches)
            {
                float sq = b.bounds.SqrDistance(p);
                if (sq > d2 || sq < b.minDistance * b.minDistance || sq >= b.maxDistance * b.maxDistance) continue;
                if (sq > k2 && !GeometryUtility.TestPlanesAABB(planes, b.bounds)) continue;
                var rp = new RenderParams(b.material)
                {
                    camera = cam,
                    shadowCastingMode = sq < c2 ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    worldBounds = b.bounds,
                    layer = gameObject.layer,
                };
                Graphics.RenderMeshInstanced(rp, b.mesh, b.submesh, b.matrices);
            }
        }
    }
}
