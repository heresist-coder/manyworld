using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Manyworld.EditorTools
{
    /// <summary>
    /// 외부 아트 연결. Synty(Town/City), Tripo 동료 모델, Mixamo 소총 애니메이션, 스프라이트 이펙트.
    /// 외부 에셋은 .gitignore로 레포에서 빠져 있다. 이 스크립트는 로컬에 있는 것만 연결해 ArtCatalog를 채운다.
    /// 배치: -executeMethod Manyworld.EditorTools.ArtSetup.RunAll
    /// </summary>
    public static class ArtSetup
    {
        const string ThirdParty = "Assets/ThirdParty";
        const string CharactersDir = ThirdParty + "/Characters";
        const string MixamoDir = ThirdParty + "/Mixamo";
        const string GeneratedDir = ThirdParty + "/Generated";
        const string CatalogPath = "Assets/_Project/Resources/ArtCatalog.asset";

        static readonly string[] LoopClips = { "idle", "idle aiming", "walk forward", "walk backward", "walk left", "walk right", "run forward", "run backward", "run left", "run right", "sprint forward" };

        [MenuItem("Manyworld/Art/Setup All")]
        public static void RunAll()
        {
            ProjectSetup.CreateBaseMaterials();
            EnsureFogFeature();
            ConvertFxMaterials();
            ConfigureImports();
            BuildAnimator();
            BuildCatalog();
            AssetDatabase.SaveAssets();
            Debug.Log("[Manyworld] Art setup done");
        }

        [MenuItem("Manyworld/Art/1 Configure Imports")]
        public static void ConfigureImports()
        {
            foreach (var path in Fbx(CharactersDir))
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.SaveAndReimport();
                // Tripo FBX는 텍스처(JPG)를 안에 묻어 둔다. 꺼내야 머티리얼에 붙는다
                string texDir = CharactersDir + "/Textures";
                if (!AssetDatabase.IsValidFolder(texDir)) AssetDatabase.CreateFolder(CharactersDir, "Textures");
                if (mi.ExtractTextures(texDir))
                {
                    AssetDatabase.Refresh();
                    mi.SaveAndReimport();
                }
            }

            // ithappy 차: 머티리얼(Color.mat)에 팔레트 텍스처가 비어 있다. Textures_4.png를 넣는다
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ithappy/City_Characters/Textures/Textures_4.png");
            if (palette != null)
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/ithappy" }))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == null)
                    {
                        m.SetTexture("_BaseMap", palette);
                        m.SetColor("_BaseColor", Color.white);
                        EditorUtility.SetDirty(m);
                    }
                }
            // ithappy 차: 외부 텍스처(Textures_4.png)를 이름으로 찾는다
            foreach (var path in Fbx("Assets/ithappy"))
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.materialSearch = ModelImporterMaterialSearch.Everywhere;
                mi.SaveAndReimport();
            }

            foreach (var path in Fbx(MixamoDir))
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importAnimation = true;
                mi.SaveAndReimport();

                string name = Path.GetFileNameWithoutExtension(path);
                var clips = mi.defaultClipAnimations;
                foreach (var c in clips)
                {
                    c.name = name;
                    c.loopTime = LoopClips.Contains(name);
                    // 위치는 코드가 움직인다. 애니메이션은 제자리에서
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = true;
                    c.keepOriginalOrientation = true;
                    c.keepOriginalPositionY = true;
                    c.keepOriginalPositionXZ = true;
                }
                mi.clipAnimations = clips;
                mi.SaveAndReimport();
            }
        }

        static IEnumerable<string> Fbx(string dir) =>
            AssetDatabase.IsValidFolder(dir)
                ? AssetDatabase.FindAssets("t:Model", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                : Enumerable.Empty<string>();

        static AnimationClip Clip(string name)
        {
            var path = $"{MixamoDir}/{name}.fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        [MenuItem("Manyworld/Art/2 Build Animator")]
        public static void BuildAnimator()
        {
            if (Clip("idle") == null)
            {
                Debug.LogWarning("[Manyworld] Mixamo 클립 없음 — 애니메이터 생략");
                return;
            }
            if (!AssetDatabase.IsValidFolder(GeneratedDir)) AssetDatabase.CreateFolder(ThirdParty, "Generated");
            string path = GeneratedDir + "/Humanoid.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Aim", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Down", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;

            AnimatorState Locomotion(string stateName, string idle)
            {
                var state = ctrl.CreateBlendTreeInController(stateName, out var tree, 0);
                tree.blendType = BlendTreeType.FreeformCartesian2D;
                tree.blendParameter = "MoveX";
                tree.blendParameterY = "MoveZ";
                void Add(string clip, float x, float z)
                {
                    var c = Clip(clip);
                    if (c != null) tree.AddChild(c, new Vector2(x, z));
                }
                Add(idle, 0, 0);
                Add("walk forward", 0, 1.6f);
                Add("walk backward", 0, -1.6f);
                Add("walk left", -1.6f, 0);
                Add("walk right", 1.6f, 0);
                Add("run forward", 0, 4f);
                Add("run backward", 0, -4f);
                Add("run left", -4f, 0);
                Add("run right", 4f, 0);
                Add("sprint forward", 0, 6.5f);
                return state;
            }

            var move = Locomotion("Move", "idle");
            var aim = Locomotion("AimMove", "idle aiming");
            var down = sm.AddState("Down");
            down.motion = Clip("death from the front");
            sm.defaultState = move;

            var t1 = move.AddTransition(aim);
            t1.AddCondition(AnimatorConditionMode.If, 0, "Aim");
            t1.hasExitTime = false; t1.duration = 0.12f;
            var t2 = aim.AddTransition(move);
            t2.AddCondition(AnimatorConditionMode.IfNot, 0, "Aim");
            t2.hasExitTime = false; t2.duration = 0.2f;
            var t3 = sm.AddAnyStateTransition(down);
            t3.AddCondition(AnimatorConditionMode.If, 0, "Down");
            t3.hasExitTime = false; t3.duration = 0.15f; t3.canTransitionToSelf = false;
            var t4 = down.AddTransition(move);
            t4.AddCondition(AnimatorConditionMode.IfNot, 0, "Down");
            t4.hasExitTime = false; t4.duration = 0.3f;
            EditorUtility.SetDirty(ctrl);
        }

        static GameObject Prefab(string fileName)
        {
            foreach (var guid in AssetDatabase.FindAssets(fileName + " t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == fileName) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }

        static GameObject[] Prefabs(params string[] names) => names.Select(Prefab).Where(g => g != null).ToArray();

        /// <summary>터레인 나무가 차를 막도록 줄기에 캡슐 콜라이더를 단 변형 프리팹을 만든다.</summary>
        static GameObject[] WithTrunkColliders(GameObject[] trees)
        {
            const string dir = GeneratedDir + "/Trees";
            if (!AssetDatabase.IsValidFolder(GeneratedDir)) AssetDatabase.CreateFolder(ThirdParty, "Generated");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(GeneratedDir, "Trees");
            var result = new List<GameObject>();
            foreach (var t in trees)
            {
                string path = $"{dir}/{t.name}_Col.prefab";
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(t);
                foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                var cap = inst.AddComponent<CapsuleCollider>();
                cap.radius = 0.35f;
                cap.height = 6f;
                cap.center = new Vector3(0, 3f, 0);
                var saved = PrefabUtility.SaveAsPrefabAsset(inst, path);
                Object.DestroyImmediate(inst);
                if (saved != null) result.Add(saved);
            }
            return result.ToArray();
        }

        static GameObject FirstPrefabIn(string dir)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            var guid = AssetDatabase.FindAssets("t:Prefab", new[] { dir }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        [MenuItem("Manyworld/Art/3 Build Catalog")]
        public static void BuildCatalog()
        {
            var cat = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
            if (cat == null)
            {
                cat = ScriptableObject.CreateInstance<ArtCatalog>();
                AssetDatabase.CreateAsset(cat, CatalogPath);
            }

            cat.houses = Prefabs(Enumerable.Range(1, 11).Select(i => $"SM_Bld_House_Preset_{i:00}").ToArray());
            cat.shops = Prefabs("SM_Bld_Shop_01", "SM_Bld_Shop_02", "SM_Bld_Shop_03", "SM_Bld_Shop_04", "SM_Bld_Shop_05", "SM_Bld_Shop_06",
                "SM_Bld_OfficeOld_Small_01", "SM_Bld_OfficeOld_Small_02");
            cat.parkedCars = Prefabs("SM_Veh_Car_Medium_01", "SM_Veh_Car_Sedan_01", "SM_Veh_Car_Van_01", "SM_Veh_Car_Taxi_01", "SM_Veh_Pickup_01");
            cat.streetProps = Prefabs("SM_Prop_Hydrant_01", "SM_Prop_Trashbin_01", "SM_Prop_Trashbin_02", "SM_Prop_TrashBag_01", "SM_Prop_TrashBag_02",
                "SM_Prop_ParkBench_01", "SM_Prop_PowerBox_01", "SM_Prop_RubbishBin_01", "SM_Prop_Mailbox_01");
            cat.tico = Prefab("SM_Veh_Car_Small_01");

            cat.driverModel = AssetDatabase.LoadAssetAtPath<GameObject>(CharactersDir + "/01-dohyun-rigged.fbx");
            cat.companionModel = AssetDatabase.LoadAssetAtPath<GameObject>(CharactersDir + "/02-luna-rigged.fbx");
            cat.humanoidController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GeneratedDir + "/Humanoid.controller");
            cat.rifle = Prefab("SM_Wep_American_Rifle_01");
            cat.companionRifle = Prefab("SM_Wep_HuntingRifle_Clean_01");

            // 티코 = ithappy Car_06 (차체 + 바퀴 4개)
            cat.ticoBody = Prefab("Car_06");
            cat.ticoWheels = new[] { Prefab("Car_06_Wheel_Front_Left"), Prefab("Car_06_Wheel_Front_Right"), Prefab("Car_06_Wheel_Rear_Left"), Prefab("Car_06_Wheel_Rear_Right") };
            if (cat.ticoWheels.Any(w => w == null)) cat.ticoWheels = new GameObject[0];

            // 큰 월드 자연물
            cat.trees = WithTrunkColliders(Prefabs("SM_Tree_01", "SM_Tree_02", "SM_Tree_03", "SM_Tree_04", "SM_Tree_Round_01", "SM_Tree_Round_02", "SM_Tree_Round_03",
                "SM_Tree_Pine_01", "SM_Tree_Pine_02", "SM_Tree_Pine_Large_01", "SM_Tree_Birch_01", "SM_Tree_Birch_02", "SM_Tree_Willow_Medium_01",
                "SM_Env_Tree_Meadow_01", "SM_Env_Tree_Meadow_02"));
            cat.deadTrees = WithTrunkColliders(Prefabs("SM_Tree_Dead_01", "SM_Tree_Dead_02", "SM_Tree_Pine_Dead_01", "SM_Tree_Birch_Dead_01"));
            cat.bushes = Prefabs("SM_Plant_Bush_01", "SM_Plant_Bush_02", "SM_Plant_Bush_03");
            cat.rocks = Prefabs("SM_Rock_01", "SM_Rock_02", "SM_Rock_03", "SM_Rock_04", "SM_Rock_Boulder_01", "SM_Rock_Cluster_Large_01", "SM_Rock_Cluster_Large_02",
                "SM_Env_Rock_Pile_01", "SM_Env_Rock_Pile_02");
            cat.grass = Prefabs("SM_Plant_Grass_01", "SM_Plant_Grass_02", "SM_Env_Grass_Tall_Clump_01", "SM_Env_Wildflowers_Patch_01");
            cat.farmBuildings = Prefabs("SM_Bld_Farmhouse_01", "SM_Bld_Farmhouse_02", "SM_Bld_Barn_01", "SM_Bld_Barn_02", "SM_Bld_Silo_01",
                "SM_Bld_Shelter_01", "SM_Bld_Outhouse_01", "SM_Bld_Stone_Cabin_01", "SM_Bld_WaterTower_01");
            cat.fences = Prefabs("SM_Prop_Fence_Wood_01", "SM_Prop_Fence_Wire_01", "SM_Prop_StoneWall_01");

            const string fx = "Assets/118 sprite effects bundle";
            cat.muzzleFlash = FirstPrefabIn(fx + "/MuzzleFlashes/_prefabs");
            cat.bloodSplash = FirstPrefabIn(fx + "/10 Blood splash sprite effects/_prefabs");
            cat.smoke = FirstPrefabIn(fx + "/Smoke sprite effects/_prefabs");

            EditorUtility.SetDirty(cat);
            Debug.Log($"[Manyworld] ArtCatalog: houses {cat.houses.Length}, shops {cat.shops.Length}, cars {cat.parkedCars.Length}, props {cat.streetProps.Length}, " +
                      $"tico {(cat.tico ? "o" : "x")}/{(cat.ticoBody ? "ithappy" : "-")} wheels {cat.ticoWheels.Length}, trees {cat.trees.Length}+{cat.deadTrees.Length}, rocks {cat.rocks.Length}, grass {cat.grass.Length}, farm {cat.farmBuildings.Length}, driver {(cat.driverModel ? "o" : "x")}, companion {(cat.companionModel ? "o" : "x")}, " +
                      $"anim {(cat.humanoidController ? "o" : "x")}, rifle {(cat.rifle ? cat.rifle.name : "x")}/{(cat.companionRifle ? cat.companionRifle.name : "x")}, fx {(cat.muzzleFlash ? "o" : "x")}{(cat.bloodSplash ? "o" : "x")}{(cat.smoke ? "o" : "x")}");
        }

        /// <summary>
        /// Volumetric Fog 2 렌더 피처를 URP 렌더러(PC·Mobile)에 한 번만 붙인다 (everret WorldFogPass와 같은 방식).
        /// 패키지에 asmdef가 없어 리플렉션으로 타입을 찾는다. 패키지가 없으면 건너뛴다.
        /// </summary>
        [MenuItem("Manyworld/Art/Ensure Volumetric Fog Feature")]
        public static void EnsureFogFeature()
        {
            System.Type type = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                if ((type = asm.GetType("VolumetricFogAndMist2.VolumetricFogRenderFeature")) != null) break;
            if (type == null)
            {
                Debug.Log("[Manyworld] Volumetric Fog 2 없음 — 렌더 피처 생략");
                return;
            }
            foreach (var path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
            {
                var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(path);
                if (data == null) continue;
                if (data.rendererFeatures.Any(f => f != null && f.GetType() == type)) continue;
                var feature = (UnityEngine.Rendering.Universal.ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = "VolumetricFog";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                Debug.Log($"[Manyworld] Volumetric Fog 렌더 피처 추가: {path}");
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>이펙트 번들의 구형 모바일 파티클 셰이더를 URP Particles/Unlit으로 바꾼다 (안 바꾸면 분홍색).</summary>
        [MenuItem("Manyworld/Art/Convert FX Materials")]
        public static void ConvertFxMaterials()
        {
            var urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urp == null) return;
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/118 sprite effects bundle" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || m.shader == null || !m.shader.name.StartsWith("Mobile/Particles")) continue;
                bool additive = m.shader.name.Contains("Additive");
                var tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                m.shader = urp;
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", additive ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
                m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(m);
                n++;
            }
            Debug.Log($"[Manyworld] 이펙트 머티리얼 URP 변환 {n}개");
        }

        /// <summary>셰이더 현황 (URP 변환이 필요한지 판단용).</summary>
        [MenuItem("Manyworld/Art/Report Shaders")]
        public static void ReportShaders()
        {
            foreach (var root in new[] { "Assets/Synty", "Assets/118 sprite effects bundle", ThirdParty })
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                var counts = new Dictionary<string, int>();
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    var s = m != null && m.shader != null ? m.shader.name : "(null)";
                    counts[s] = counts.TryGetValue(s, out var n) ? n + 1 : 1;
                }
                foreach (var kv in counts.OrderByDescending(k => k.Value))
                    Debug.Log($"[Manyworld][Shader] {root}: {kv.Value} × {kv.Key}");
            }
        }
    }
}
