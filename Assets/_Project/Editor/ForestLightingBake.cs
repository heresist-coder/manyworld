using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Manyworld.EditorTools
{
    /// <summary>
    /// 숲 현장의 간접광 굽기 (D25).
    /// 런타임과 같은 시드로 숲을 에디터에서 만들고, 해·하늘·지형·나무·바위가 서로 튕기는 빛을
    /// APV(적응형 프로브 볼륨)에 굽는다. 다 구우면 지형과 나무는 지우고 빛 데이터만 남긴 씬을 저장한다.
    /// 게임은 숲을 그대로 코드로 만들고 이 씬을 덧붙여 열어 구운 빛을 쓴다 (ForestLighting).
    /// 숲 생성 코드나 배치를 바꾸면 다시 구워야 한다.
    /// </summary>
    public static class ForestLightingBake
    {
        const string ScenePath = "Assets/_Project/Scenes/ForestLighting.unity";
        const string SettingsPath = "Assets/_Project/Scenes/ForestLighting.lighting";

        [MenuItem("Manyworld/Field/Bake Forest Lighting")]
        public static void Bake()
        {
            EnableProbeVolumes();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 해와 하늘: 런타임 숲 현장과 같은 팔레트
            var sunGo = new GameObject("BakeSun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.lightmapBakeType = LightmapBakeType.Mixed; // 직사광은 런타임 해가, 간접광만 굽는다
            var camGo = new GameObject("BakeCamera");
            var cam = camGo.AddComponent<Camera>();
            WorldLook.Apply(WorldLook.Field, sun, sunGo.transform, cam);

            // 숲: 런타임과 같은 시드
            var layout = ForestSiteBuilder.Build(1, out _);
            var root = layout.root.gameObject;

            // 원경·하늘·풀은 빛을 튕기지 않는다 (먼 산 띠·바깥 땅은 볼륨 밖이라 굽는 시간만 늘린다)
            foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t == null) continue;
                if (t.name == "FarMountain" || t.name == "OuterGround" || t.name == "CloudDome" || t.name == "Cloud")
                    Object.DestroyImmediate(t.gameObject);
            }
            int contributors = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.StartsWith("GrassClump") || !r.enabled) continue;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI);
                r.receiveGI = ReceiveGI.LightProbes; // 라이트맵 없이 프로브로만
                contributors++;
            }
            var terrain = layout.terrain;
            // 지형 나무는 굽기에서 하늘을 가리지 않는다 — 숲 바닥이 하늘 아래 맨땅처럼 밝게 구워졌다 (목표 샷 02·03보다 1.5~2배).
            // 숲길·방 주변 나무만 굽는 동안 실제 물체로 꺼내 하늘과 해를 가리게 한다 (굽고 나면 지운다)
            contributors += TreesAsObjects(terrain, root.transform, 320f);
            GameObjectUtility.SetStaticEditorFlags(terrain.gameObject, StaticEditorFlags.ContributeGI);
            var so = new SerializedObject(terrain);
            var scale = so.FindProperty("m_ScaleInLightmap");
            if (scale != null) { scale.floatValue = 0.02f; so.ApplyModifiedPropertiesWithoutUndo(); }

            // 프로브 볼륨: 분지 안쪽 (가장자리 산 꼭대기까지)
            var pvGo = new GameObject("ForestProbeVolume");
            var pv = pvGo.AddComponent<ProbeVolume>();
            pv.mode = ProbeVolume.Mode.Local;
            pvGo.transform.position = new Vector3(ForestSiteBuilder.Size / 2, 105f, ForestSiteBuilder.Size / 2);
            pv.size = new Vector3(ForestSiteBuilder.Size - 40f, 230f, ForestSiteBuilder.Size - 40f);
            pv.overrideRendererFilters = true;
            pv.minRendererVolumeSize = 0.5f;

            // 굽기 세트 (씬 하나)
            EditorSceneManager.SaveScene(scene, ScenePath);
            var set = ScriptableObject.CreateInstance<ProbeVolumeBakingSet>();
            typeof(ProbeVolumeBakingSet).GetMethod("SetDefaults", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(set, null);
            set.name = "ForestLighting Baking Set";
            set.minDistanceBetweenProbes = 3f;   // 3m 간격 (데이터 크기·굽는 시간)
            set.simplificationLevels = 3;
            set.skyOcclusion = false;            // 하늘 가림 굽기는 D3D12 레이트레이싱에서 에디터가 죽는다 (6000.3.22f1)
            var setDir = "Assets/_Project/Scenes/ForestLighting";
            if (!AssetDatabase.IsValidFolder(setDir)) AssetDatabase.CreateFolder("Assets/_Project/Scenes", "ForestLighting");
            var setPath = setDir + "/ForestLighting Baking Set.asset";
            AssetDatabase.DeleteAsset(setPath);
            AssetDatabase.CreateAsset(set, setPath);
            set.TryAddScene(AssetDatabase.AssetPathToGUID(ScenePath));
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();

            // 굽기 설정: 간접광만, 라이트맵은 지형 하나만 아주 낮게
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "ForestLighting" };
                AssetDatabase.CreateAsset(ls, SettingsPath);
            }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.lightmapResolution = 1f;
            ls.lightmapMaxSize = 1024;
            ls.directSampleCount = 16;
            ls.indirectSampleCount = 128;
            ls.environmentSampleCount = 128;
            ls.maxBounces = 3;
            ls.ao = false;
            ls.indirectScale = 1.5f; // 튕긴 빛을 조금 세게 (목표 샷의 따뜻한 그늘)
            ls.albedoBoost = 1.25f;
            ls.autoGenerate = false;
            EditorUtility.SetDirty(ls);
            Lightmapping.lightingSettings = ls;

            Debug.Log($"[Manyworld] 숲 조명 굽기 시작: 빛을 튕기는 물체 {contributors}개 + 지형");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool ok = Lightmapping.Bake();
            Debug.Log($"[Manyworld] 숲 조명 굽기 {(ok ? "완료" : "실패")}: {watch.Elapsed.TotalSeconds:F0}초");
            if (!ok)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            // 빛 데이터만 남긴다: 숲·해·카메라·후처리는 런타임이 만든다
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(sunGo);
            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<ProbeVolume>() == null && go.GetComponent<ProbeVolumePerSceneData>() == null)
                    Object.DestroyImmediate(go);
            RenderSettings.skybox = null;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Manyworld] 숲 조명 씬 저장: {ScenePath}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// 숲길·방 반경 안의 지형 나무를 굽기용 대역(나무마다 8각 원뿔 하나, 하나의 메시로 합침)으로 세운다.
        /// 원래 나무 메시 1만 그루는 GPU 레이 트레이서(RadeonRays)가 메시를 올리다 죽었다 (6000.3.22f1, 접근 위반).
        /// 하늘·해를 가리는 덩어리만 있으면 되므로 짙은 잎 색 원뿔이면 충분하다.
        /// </summary>
        static int TreesAsObjects(Terrain terrain, Transform parent, float radius)
        {
            var td = terrain.terrainData;
            var center = (ForestSiteBuilder.Trailhead + ForestSiteBuilder.RoomCenter) * 0.5f;
            // 원형마다 크기 (원점·크기 1로 세워 잰다)
            var sizes = new Vector2[td.treePrototypes.Length]; // x 잎 반지름, y 높이
            for (int i = 0; i < sizes.Length; i++)
            {
                var prefab = td.treePrototypes[i].prefab;
                if (prefab == null) continue;
                var probe = Object.Instantiate(prefab);
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var b = ArtCatalog.RendererBounds(probe);
                sizes[i] = new Vector2(Mathf.Max(b.extents.x, b.extents.z), b.max.y);
                Object.DestroyImmediate(probe);
            }
            const int sides = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int n = 0;
            foreach (var ti in td.treeInstances)
            {
                var wp = Vector3.Scale(ti.position, td.size) + terrain.transform.position;
                if (Vector2.Distance(new Vector2(wp.x, wp.z), center) > radius) continue;
                var sz = sizes[ti.prototypeIndex];
                if (sz.y <= 0f) continue;
                float h = sz.y * ti.heightScale, r = sz.x * ti.widthScale * 0.85f;
                float baseY = wp.y + h * 0.28f; // 잎은 줄기 위 1/4쯤부터
                int apex = verts.Count;
                verts.Add(wp + Vector3.up * h);
                int ring = verts.Count;
                for (int k = 0; k < sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides + ti.rotation;
                    verts.Add(new Vector3(wp.x + Mathf.Cos(a) * r, baseY, wp.z + Mathf.Sin(a) * r));
                }
                int bottom = verts.Count;
                verts.Add(new Vector3(wp.x, baseY, wp.z));
                for (int k = 0; k < sides; k++)
                {
                    int a0 = ring + k, a1 = ring + (k + 1) % sides;
                    tris.Add(apex); tris.Add(a1); tris.Add(a0);
                    tris.Add(bottom); tris.Add(a0); tris.Add(a1);
                }
                n++;
            }
            var mesh = new Mesh { name = "BakeCanopy", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[verts.Count])); // 굽기는 UV가 있어야 받는다
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("BakeCanopy");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Graybox.Mat(new Color(0.15f, 0.19f, 0.11f)); // 짙은 솔잎
            mr.receiveGI = ReceiveGI.LightProbes;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
            Debug.Log($"[Manyworld] 굽기용 나무 대역 {n}그루 (반경 {radius}m, 삼각형 {tris.Count / 3})");
            return 1;
        }

        /// <summary>URP 에셋의 라이트 프로브 방식을 APV로 (L1).</summary>
        static void EnableProbeVolumes()
        {
            foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                var p = so.FindProperty("m_LightProbeSystem");
                if (p != null && p.intValue != 1)
                {
                    p.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
