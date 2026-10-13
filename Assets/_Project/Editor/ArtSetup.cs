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
                // 인물 텍스처는 2048까지 (Tripo detailed는 4K로 나온다. 3인칭 근접에서도 2048이면 충분)
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
                {
                    var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                    if (ti != null && ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; ti.SaveAndReimport(); }
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

        /// <summary>
        /// 2026-10-13 Tripo 인물(웹에서 생성·리깅, Mixamo 뼈대): 휴머노이드로 임포트하고, 기본색 텍스처만 쓰는 무광 URP Lit로 바꿔 끼운다.
        /// zip의 노멀·금속도·거칠기 맵은 쓰지 않는다 (평면 색 세계, 숲에서 피부·옷이 번들거리지 않게. docs/art/2026-10-13-characters).
        /// </summary>
        static void SetupTripoCharacters()
        {
            const string dir = CharactersDir + "/Tripo20261013";
            if (!AssetDatabase.IsValidFolder(dir)) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var path in Fbx(dir))
            {
                string key = Path.GetFileNameWithoutExtension(path).Replace("-rigged", "");
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                bool dirty = false;
                if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
                if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
                var texPath = $"{dir}/Textures/{key}_basecolor.png";
                if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; ti.SaveAndReimport(); }
                var matPath = $"{dir}/Tripo_{key}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, matPath); }
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.1f);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_SpecularHighlights", 0f);
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                foreach (var src in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
                    dirty = true;
                }
                if (dirty) mi.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                int tris = model != null ? model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r => r.sharedMesh != null ? r.sharedMesh.triangles.Length / 3 : 0) : 0;
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                Debug.Log($"[Manyworld] Tripo 인물 {key}: 삼각형 {tris}, 휴머노이드 {(avatar != null && avatar.isHuman ? "o" : "x")}");
            }
        }

        /// <summary>Tripo20261013 폴더의 리깅된 FBX (tools/tripo/generate_characters.py가 내려받은 것). 휴머노이드로 임포트됐을 때만.</summary>
        static GameObject TripoCharacter(string key)
        {
            const string dir = CharactersDir + "/Tripo20261013";
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            foreach (var path in Fbx(dir))
            {
                if (!Path.GetFileName(path).StartsWith(key + "-rigged")) continue;
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null || mi.animationType != ModelImporterAnimationType.Human) continue;
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
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
        static GameObject[] WithTrunkColliders(GameObject[] trees, string suffix = "_Col", float leafShade = 1f)
        {
            const string dir = GeneratedDir + "/Trees";
            if (!AssetDatabase.IsValidFolder(GeneratedDir)) AssetDatabase.CreateFolder(ThirdParty, "Generated");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(GeneratedDir, "Trees");
            var result = new List<GameObject>();
            foreach (var t in trees)
            {
                string path = $"{dir}/{t.name}{suffix}.prefab";
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(t);
                foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                // 신티 LOD 셰이더(SyntyStudios_LOD)가 이 URP에서 하얗게 나온다. LOD0만 남긴다
                var lodGroup = inst.GetComponent<LODGroup>();
                if (lodGroup != null)
                {
                    var lods = lodGroup.GetLODs();
                    if (lods.Length > 1)
                    {
                        var keep = new HashSet<Renderer>(lods[0].renderers.Where(r => r != null));
                        for (int i = 1; i < lods.Length; i++)
                            foreach (var r in lods[i].renderers)
                                if (r != null && !keep.Contains(r)) Object.DestroyImmediate(r.gameObject);
                        lodGroup.SetLODs(new[] { new LOD(0.004f, keep.ToArray()) });
                        lodGroup.RecalculateBounds();
                    }
                }
                // 신티 나무 셰이더는 광택이 고정이라 먼 잎이 하늘을 반사해 하얗게 뜬다. 광택 없는 URP Lit로 바꾼다
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = Instanced(MatteFoliage(mats[i], leafShade));
                    r.sharedMaterials = mats;
                }
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

        /// <summary>나무는 TreeField가 GPU 인스턴싱으로 그린다. 인스턴싱이 꺼진 원본 머티리얼은 켠 사본을 만든다.</summary>
        static Material Instanced(Material m)
        {
            if (m == null || m.enableInstancing) return m;
            string path = AssetDatabase.GetAssetPath(m);
            if (path.StartsWith(GeneratedDir)) { m.enableInstancing = true; EditorUtility.SetDirty(m); return m; }
            const string dir = GeneratedDir + "/Trees/Materials";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(GeneratedDir + "/Trees", "Materials");
            string copyPath = $"{dir}/{m.name}_Inst.mat";
            var copy = AssetDatabase.LoadAssetAtPath<Material>(copyPath);
            if (copy == null) { copy = new Material(m); AssetDatabase.CreateAsset(copy, copyPath); }
            copy.enableInstancing = true;
            EditorUtility.SetDirty(copy);
            return copy;
        }

        /// <summary>신티 나무 머티리얼 → 광택·반사 없는 URP Lit (잎은 알파 클립 + 양면). 같은 원본은 한 번만 만든다.</summary>
        static Material MatteFoliage(Material src, float leafShade = 1f)
        {
            if (src == null) return null;
            // 신티 기본 나무 셰이더(_Main_Texture)만 바꾼다. 잎·줄기 텍스처가 따로인 바이옴 셰이더는 그대로
            bool trees = src.HasProperty("_Main_Texture") && !src.HasProperty("_Leaf_Texture");
            bool grass = src.HasProperty("_Leaf_Texture") && !src.HasProperty("_Trunk_Texture"); // 바이옴 풀: 잎 텍스처 하나
            if (!trees && !grass) return src;
            const string dir = GeneratedDir + "/Trees/Materials";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(GeneratedDir + "/Trees", "Materials");
            string path = leafShade < 0.99f ? $"{dir}/{src.name}_Matte_Dark.mat" : $"{dir}/{src.name}_Matte.mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = m == null;
            if (created) m = new Material(lit);
            m.shader = lit;
            Texture tex = src.HasProperty("_Main_Texture") ? src.GetTexture("_Main_Texture") : src.HasProperty("_Leaf_Texture") ? src.GetTexture("_Leaf_Texture") : null;
            if (tex == null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
            if (tex == null && src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
            var tint = src.HasProperty("_Color_Tint") ? src.GetColor("_Color_Tint") : Color.white;
            // 줄기 텍스처가 밝아서 그림자 거리 밖에선 줄기만 하얗게 줄지어 보인다. 어둡게
            if (src.name.Contains("Trunk")) tint *= new Color(0.5f, 0.44f, 0.4f);
            else tint *= new Color(1.2f, 1.26f, 1.02f) * leafShade; // 잎: 볕 받는 면이 초록으로 읽히게 (먼 숲은 leafShade로 어둡게)
            if (src.name.Contains("Trunk")) tint *= leafShade;
            else if (leafShade < 0.99f) tint *= new Color(0.9f, 1.06f, 0.78f); // 먼 숲은 회색이 아니라 짙은 초록으로
            tint.a = 1f;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.enableInstancing = true; // 터레인 디테일(풀)은 인스턴싱 머티리얼이어야 그려진다
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            bool clip = (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f) || src.name.Contains("Leaves") || src.name.Contains("Leaf") || grass;
            if (clip && tex is Texture2D leafTex) m.SetTexture("_BaseMap", FilledLeafTexture(leafTex));
            if (clip)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cutoff", src.HasProperty("_Mask_Clip_Value") ? src.GetFloat("_Mask_Clip_Value") : 0.5f);
                m.SetFloat("_Cull", 0f); // 잎은 양면
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            if (created) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 목표 샷 01의 차는 깨끗한 노란 해치백이다. 신티 텍스처의 노란 칸에 박힌 진흙 얼룩을 노랑으로 덮은
        /// 텍스처·머티리얼·프리팹 사본을 만든다 (아래 끝의 흙 띠는 조금 남긴다).
        /// </summary>
        static GameObject CleanCar(GameObject src)
        {
            const string dir = GeneratedDir + "/Vehicles";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(GeneratedDir, "Vehicles");
            const string texSrc = "Assets/Synty/PolygonMilitary/Textures/Vehicles/Land_Vehicles_03.png";
            const string matSrc = "Assets/Synty/PolygonMilitary/Materials/Vehicles/Land_Vehicles_03.mat";
            var texPath = dir + "/Land_Vehicles_03_Clean.png";
            var matPath = dir + "/Land_Vehicles_03_Clean.mat";
            var prefabPath = dir + "/" + src.name + "_Clean.prefab";
            var srcMat = AssetDatabase.LoadAssetAtPath<Material>(matSrc);
            if (srcMat == null || !File.Exists(texSrc)) return null;
            if (!File.Exists(texPath))
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(File.ReadAllBytes(texSrc));
                int w = t.width, hgt = t.height;
                var px = t.GetPixels32();
                var yellow = new Color32(213, 165, 72, 255);
                // PNG 위에서 1365~1600행, 가로 908~ (2048 기준) = 노란 차체 칸. Texture2D는 아래가 0행
                float k = w / 2048f;
                for (int y = Mathf.RoundToInt(1365 * k); y <= Mathf.RoundToInt(1600 * k); y++)
                for (int x = Mathf.RoundToInt(908 * k); x < w; x++)
                    px[(hgt - 1 - y) * w + x] = yellow;
                t.SetPixels32(px);
                File.WriteAllBytes(texPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(texPath);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(srcMat);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            foreach (var prop in new[] { "_Albedo_Map", "_BaseMap", "_MainTex" })
                if (mat.HasProperty(prop)) mat.SetTexture(prop, tex);
            EditorUtility.SetDirty(mat);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) if (mats[i] == srcMat) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            var saved = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
            Object.DestroyImmediate(inst);
            return saved;
        }

        /// <summary>
        /// 잎 텍스처의 투명한 칸은 밝은 회색이라 먼 밉맵에서 잎이 하얗게 번진다 (먼 숲이 눈 덮인 것처럼 보임).
        /// 투명한 칸을 잎의 평균색으로 채운 사본을 만든다.
        /// </summary>
        static Texture2D FilledLeafTexture(Texture2D src)
        {
            var srcPath = AssetDatabase.GetAssetPath(src);
            if (string.IsNullOrEmpty(srcPath) || !srcPath.EndsWith(".png")) return src;
            const string dir = GeneratedDir + "/Trees/Textures";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(GeneratedDir + "/Trees", "Textures");
            var path = $"{dir}/{Path.GetFileNameWithoutExtension(srcPath)}_Fill.png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(File.ReadAllBytes(srcPath));
                var px = t.GetPixels32();
                long r = 0, g = 0, b = 0, n = 0;
                foreach (var c in px) if (c.a > 127) { r += c.r; g += c.g; b += c.b; n++; }
                if (n == 0) return src;
                var fill = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                for (int i = 0; i < px.Length; i++) if (px[i].a <= 127) px[i] = new Color32(fill.r, fill.g, fill.b, px[i].a);
                t.SetPixels32(px);
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                {
                    ti.alphaIsTransparency = true;
                    ti.mipMapsPreserveCoverage = true;
                    ti.alphaTestReferenceValue = 0.5f;
                    ti.SaveAndReimport();
                }
            }
            var filled = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return filled != null ? filled : src;
        }

        /// <summary>풀·덤불: 광택 없는 URP Lit 사본 프리팹 (터레인 디테일 인스턴싱이 되도록, 콜라이더 없음).</summary>
        static GameObject[] MatteCopies(GameObject[] prefabs)
        {
            const string dir = GeneratedDir + "/Plants";
            if (!AssetDatabase.IsValidFolder(GeneratedDir)) AssetDatabase.CreateFolder(ThirdParty, "Generated");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(GeneratedDir, "Plants");
            var result = new List<GameObject>();
            foreach (var t in prefabs)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(t);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                var lod = inst.GetComponent<LODGroup>();
                if (lod != null)
                {
                    var lods = lod.GetLODs();
                    if (lods.Length > 1)
                    {
                        var keep = new HashSet<Renderer>(lods[0].renderers.Where(r => r != null));
                        for (int i = 1; i < lods.Length; i++)
                            foreach (var r in lods[i].renderers)
                                if (r != null && !keep.Contains(r)) Object.DestroyImmediate(r.gameObject);
                    }
                    Object.DestroyImmediate(lod);
                }
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = MatteFoliage(mats[i]);
                    r.sharedMaterials = mats;
                }
                // 터레인 디테일은 프리팹 루트의 메시만 그린다. LOD 자식에 있으면 루트로 올린다
                if (inst.GetComponent<MeshFilter>() == null)
                {
                    var child = inst.GetComponentInChildren<MeshFilter>();
                    var childR = child != null ? child.GetComponent<MeshRenderer>() : null;
                    if (child != null && childR != null)
                    {
                        var mesh = child.sharedMesh;
                        var mats = childR.sharedMaterials;
                        var local = Matrix4x4.TRS(child.transform.localPosition, child.transform.localRotation, child.transform.localScale);
                        if (local != Matrix4x4.identity)
                        {
                            // 자식 변환을 메시에 굽는다
                            var baked = Object.Instantiate(mesh);
                            var v = baked.vertices; var n = baked.normals;
                            for (int k = 0; k < v.Length; k++) v[k] = local.MultiplyPoint3x4(v[k]);
                            for (int k = 0; k < n.Length; k++) n[k] = local.MultiplyVector(n[k]).normalized;
                            baked.vertices = v; baked.normals = n; baked.RecalculateBounds();
                            AssetDatabase.CreateAsset(baked, $"{dir}/{t.name}_Mesh.asset");
                            mesh = baked;
                        }
                        foreach (Transform c in inst.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
                        inst.AddComponent<MeshFilter>().sharedMesh = mesh;
                        inst.AddComponent<MeshRenderer>().sharedMaterials = mats;
                    }
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(inst, $"{dir}/{t.name}_Matte.prefab");
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

        /// <summary>
        /// 잎 텍스처: 알파 테스트 잎은 먼 밉맵에서 알파가 평균돼 사라지고 줄기만 하얗게 남는다.
        /// 밉맵 커버리지를 보존해 멀리서도 잎이 보이게 한다.
        /// </summary>
        [MenuItem("Manyworld/Art/Fix Foliage Mipmaps")]
        public static void FixFoliageMips()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Synty")) return;
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Synty" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = Path.GetFileNameWithoutExtension(path);
                if (file.EndsWith("_Mask") || file.EndsWith("_Emissive")) continue;
                if (!(file.Contains("Leaves") || file.Contains("Leaf") || file.Contains("Grass") || file.Contains("Plant") || file.Contains("Bush") || file.Contains("Foliage"))) continue;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti) || (ti.mipMapsPreserveCoverage && ti.alphaIsTransparency)) continue;
                ti.mipMapsPreserveCoverage = true;
                ti.alphaIsTransparency = true; // 투명 부분의 흰색이 먼 밉맵에 번지지 않게
                ti.alphaTestReferenceValue = 0.5f;
                ti.SaveAndReimport();
                n++;
            }
            Debug.Log($"[Manyworld] 잎 텍스처 밉맵 커버리지 보존 {n}개");
        }

        [MenuItem("Manyworld/Art/3 Build Catalog")]
        public static void BuildCatalog()
        {
            SetupTripoCharacters();
            FixFoliageMips();
            EnsureSsaoFeature();
            EnsureRadiantFeature();
            EnsureFogBorder();
            EnsurePainterlyFeature();
            // 지형: 숲 현장 질감은 법선 맵을 쓴다 (_NORMALMAP 변형이 빌드에 들어가게 머티리얼에 켜 둔다)
            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Resources/Materials/Terrain.mat");
            if (terrainMat != null && !terrainMat.IsKeywordEnabled("_NORMALMAP")) { terrainMat.EnableKeyword("_NORMALMAP"); EditorUtility.SetDirty(terrainMat); }
            // 그레이박스 베이스 머티리얼에 인스턴싱 변형을 넣어 둔다 (빌드에서 풀 인스턴싱이 빠지지 않게)
            var baseOpaque = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Resources/Materials/GrayboxOpaque.mat");
            if (baseOpaque != null && !baseOpaque.enableInstancing) { baseOpaque.enableInstancing = true; EditorUtility.SetDirty(baseOpaque); }
            // 숲 현장의 직접 만든 셰이더 머티리얼 (셰이더가 빌드에 들어가도록 Resources에 둔다)
            foreach (var (shaderName, matName) in new[] { ("Manyworld/CloudDome", "CloudDome"), ("Manyworld/Grass", "Grass"), ("Manyworld/Backdrop", "Backdrop") })
            {
                var path = $"Assets/_Project/Resources/Materials/{matName}.mat";
                var sh = Shader.Find(shaderName);
                if (sh != null && AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                    AssetDatabase.CreateAsset(new Material(sh) { name = matName, enableInstancing = true }, path);
            }
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

            // 인물: 2026-10-13 Tripo 인물(목표 샷 02·03의 두 사람, docs/art/2026-10-13-characters)이 있으면 그것,
            // 없으면 신티 휴머노이드 (City 재킷 남자, Town 딸), 그것도 없으면 예전 Tripo 동료
            cat.driverModel = TripoCharacter("01-driver") ?? Prefab("Character_Male_Jacket") ?? AssetDatabase.LoadAssetAtPath<GameObject>(CharactersDir + "/01-dohyun-rigged.fbx");
            cat.companionModel = TripoCharacter("02-eunju") ?? Prefab("SM_Chr_Daughter_01") ?? AssetDatabase.LoadAssetAtPath<GameObject>(CharactersDir + "/02-luna-rigged.fbx");
            cat.humanoidController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GeneratedDir + "/Humanoid.controller");
            cat.rifle = Prefab("SM_Wep_American_Rifle_01");
            cat.companionRifle = Prefab("SM_Wep_HuntingRifle_Clean_01");

            // 티코 = 신티 Military 노란 소형 해치백 (바퀴가 자식으로 붙어 있다). 없으면 ithappy Car_06
            var smallCar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonMilitary/Prefabs/Vehicles/SM_Veh_Car_Small_01.prefab");
            if (smallCar != null)
            {
                cat.ticoBody = CleanCar(smallCar) ?? smallCar;
                cat.ticoWheels = new GameObject[0];
            }
            else
            {
                cat.ticoBody = Prefab("Car_06");
                cat.ticoWheels = new[] { Prefab("Car_06_Wheel_Front_Left"), Prefab("Car_06_Wheel_Front_Right"), Prefab("Car_06_Wheel_Rear_Left"), Prefab("Car_06_Wheel_Rear_Right") };
                if (cat.ticoWheels.Any(w => w == null)) cat.ticoWheels = new GameObject[0];
            }
            cat.clouds = Prefabs("SM_Gen_Env_Cloud_01", "SM_Gen_Env_Cloud_02", "SM_Gen_Env_Cloud_03");
            cat.fieldProps = MatteCopies(Prefabs("SM_Env_Grass_Tall_Clump_01", "SM_Env_Grass_Tall_Clump_02"));
            cat.fieldGrass = MatteCopies(Prefabs("SM_Env_Grass_Tall_Clump_01", "SM_Env_Grass_Tall_Clump_02", "SM_Env_Wildflowers_Patch_01"));
            StylizedGrass(cat);
            cat.fieldBushes = MatteCopies(Prefabs("SM_Plant_Bush_01", "SM_Plant_Bush_02", "SM_Plant_Bush_03"));

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
            cat.hamletProps = Prefabs("SM_Prop_Well_01", "SM_Prop_StoneWall_01");
            cat.skyline = Prefabs(Enumerable.Range(1, 11).Select(i => $"SM_Gen_Bld_Background_{i:00}").ToArray());

            // 숲 현장
            cat.pines = WithTrunkColliders(Prefabs("SM_Tree_Pine_01", "SM_Tree_Pine_02", "SM_Tree_Pine_Large_01"));
            cat.pinesDark = WithTrunkColliders(Prefabs("SM_Tree_Pine_01", "SM_Tree_Pine_02", "SM_Tree_Pine_Large_01"), "_Dark", 0.55f);
            cat.meadowTrees = WithTrunkColliders(Prefabs("SM_Tree_Round_01", "SM_Tree_Round_02", "SM_Env_Tree_Meadow_01", "SM_Env_Tree_Meadow_02"));
            cat.plushies = Prefabs("SM_Prop_Toy_Bear_01", "SM_Prop_Plushie_04", "SM_Prop_Plushie_03", "SM_Prop_Plushie_07");
            cat.siteChair = Prefab("SM_Prop_DiningChair_01");
            cat.mountains = Prefabs("SM_Gen_Env_Background_Mountain_01", "SM_Gen_Env_Background_Mountain_02", "SM_Gen_Env_Background_Mountain_03");
            // 숲 바닥 잔것 (ScatterClutter)
            cat.clutterSmall = Prefabs("SM_Gen_Env_Twig_01", "SM_Gen_Env_Twig_02", "SM_Gen_Env_Twig_03", "SM_Gen_Env_Twig_04");
            cat.clutterLeaves = Prefabs("SM_Gen_Env_Leaves_01", "SM_Gen_Env_Leaves_02", "SM_Gen_Env_Leaves_03"); // 낙엽 무더기(Pile)는 볕에서 모래 둔덕처럼 보여 뺐다
            cat.clutterFerns = Prefabs("SM_Gen_Env_Fern_01", "SM_Gen_Env_Fern_02", "SM_Gen_Env_Fern_03");
            cat.clutterPebbles = Prefabs("SM_Gen_Env_Rock_Pebbles_01", "SM_Gen_Env_Rock_Pebbles_02", "SM_Gen_Env_Rock_Pebbles_03", "SM_Gen_Env_Rock_Pebbles_04", "SM_Gen_Env_Rock_Pebbles_05");
            cat.clutterMushrooms = Prefabs("SM_Gen_Env_Mushroom_01", "SM_Gen_Env_Mushroom_02", "SM_Gen_Env_Mushroom_03");
            cat.clutterLogs = Prefabs("SM_Gen_Env_Log_01", "SM_Gen_Env_Log_02", "SM_Gen_Env_Stump_01", "SM_Gen_Env_Stump_02", "SM_Gen_Env_Stump_03");

            const string fx = "Assets/118 sprite effects bundle";
            cat.muzzleFlash = FirstPrefabIn(fx + "/MuzzleFlashes/_prefabs");
            cat.bloodSplash = FirstPrefabIn(fx + "/10 Blood splash sprite effects/_prefabs");
            cat.smoke = FirstPrefabIn(fx + "/Smoke sprite effects/_prefabs");

            EditorUtility.SetDirty(cat);
            Debug.Log($"[Manyworld] ArtCatalog: houses {cat.houses.Length}, shops {cat.shops.Length}, cars {cat.parkedCars.Length}, props {cat.streetProps.Length}, " +
                      $"tico {(cat.tico ? "o" : "x")}/{(cat.ticoBody ? "ithappy" : "-")} wheels {cat.ticoWheels.Length}, trees {cat.trees.Length}+{cat.deadTrees.Length}, rocks {cat.rocks.Length}, grass {cat.grass.Length}, farm {cat.farmBuildings.Length}, driver {(cat.driverModel ? "o" : "x")}, companion {(cat.companionModel ? "o" : "x")}, " +
                      $"anim {(cat.humanoidController ? "o" : "x")}, rifle {(cat.rifle ? cat.rifle.name : "x")}/{(cat.companionRifle ? cat.companionRifle.name : "x")}, fx {(cat.muzzleFlash ? "o" : "x")}{(cat.bloodSplash ? "o" : "x")}{(cat.smoke ? "o" : "x")}, " +
                      $"site: pines {cat.pines.Length}, meadow {cat.meadowTrees.Length}, plush {cat.plushies.Length}, chair {(cat.siteChair ? "o" : "x")}, mountains {cat.mountains.Length}, driver {(cat.driverModel ? cat.driverModel.name : "x")}, companion {(cat.companionModel ? cat.companionModel.name : "x")}");
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

        /// <summary>
        /// Volumetric Fog 2의 상자 경계 흐림(FOG_BORDER)을 켠다. 에셋 설정 파일(CommonsURP.hlsl)의 컴파일 옵션이라 기본은 꺼져 있다.
        /// 숲 현장 연무는 숲 위에만 깔리는 상자라 경계가 없으면 초원에서 세로 벽처럼 보인다. 에셋은 깃에 없으므로 여기서 매번 맞춘다.
        /// </summary>
        [MenuItem("Manyworld/Art/Ensure Fog Border")]
        public static void EnsureFogBorder()
        {
            const string path = "Assets/VolumetricFog2/Resources/Shaders/CommonsURP.hlsl";
            if (!File.Exists(path)) return;
            var text = File.ReadAllText(path);
            if (!text.Contains("//#define FOG_BORDER")) return;
            File.WriteAllText(path, text.Replace("//#define FOG_BORDER", "#define FOG_BORDER"));
            AssetDatabase.ImportAsset(path);
            Debug.Log("[Manyworld] Volumetric Fog 경계 흐림 켬");
        }

        /// <summary>
        /// Radiant GI(화면 공간 간접광) 렌더 피처를 PC 렌더러에 한 번만 붙인다 (D27). 유료 에셋이라 리플렉션으로 찾고, 없으면 건너뛴다.
        /// 패키지 없는 체크아웃에선 이 항목이 빈 스크립트 참조로 남지만 URP는 빈 피처를 건너뛴다 (Volumetric Fog 2와 같음).
        /// </summary>
        [MenuItem("Manyworld/Art/Ensure Radiant GI Feature")]
        public static void EnsureRadiantFeature()
        {
            System.Type type = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                if ((type = asm.GetType("RadiantGI.Universal.RadiantRenderFeature")) != null) break;
            var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
            if (type == null || data == null)
            {
                Debug.Log("[Manyworld] Radiant GI 없음 — 렌더 피처 생략");
                return;
            }
            var feature = data.rendererFeatures.FirstOrDefault(f => f != null && f.GetType() == type);
            if (feature == null)
            {
                feature = (UnityEngine.Rendering.Universal.ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = "RadiantGI";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Manyworld] Radiant GI 렌더 피처 추가");
            }
            // 렌더러가 Forward+라 Forward 경로
            var path = type.GetField("renderingPath");
            if (path != null) path.SetValue(feature, System.Enum.ToObject(path.FieldType, 0));
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        /// <summary>SSAO(화면 공간 주변광 차폐): 바위·나무 밑동·인물 발밑을 땅에 붙인다. PC 렌더러에 한 번만.</summary>
        [MenuItem("Manyworld/Art/Ensure SSAO Feature")]
        public static void EnsureSsaoFeature()
        {
            var baseType = typeof(UnityEngine.Rendering.Universal.ScriptableRendererFeature);
            var type = baseType.Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
            if (type == null || data == null) { Debug.LogWarning("[Manyworld] SSAO 피처를 못 찾음"); return; }
            var feature = data.rendererFeatures.FirstOrDefault(f => f != null && f.GetType() == type);
            if (feature == null)
            {
                feature = (UnityEngine.Rendering.Universal.ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = "SSAO";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var fs = new SerializedObject(feature);
            void SetF(string n, float v) { var p = fs.FindProperty("m_Settings." + n); if (p != null) p.floatValue = v; }
            SetF("Intensity", 0.7f);
            SetF("Radius", 0.45f);
            SetF("DirectLightingStrength", 0.25f);
            SetF("Falloff", 120f);
            fs.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[Manyworld] SSAO 피처 준비");
        }

        /// <summary>
        /// Stylized Grass Shader 패키지(있으면)의 풀 LOD0 메시와 머티리얼 사본을 카탈로그에 넣는다.
        /// 머티리얼은 ThirdParty/Generated에 복사해 인스턴싱을 켜고 지형 색(컬러맵) 섞기를 쓴다.
        /// </summary>
        static void StylizedGrass(ArtCatalog cat)
        {
            const string pkg = "Packages/xyz.staggart-creations.stylized-grass/Prefabs/";
            // (프리팹, 비율, 잎끝 색). 패키지 기본 라임색은 볕에서 형광처럼 떠서 목표 샷 01의 노란빛 올리브로
            var picks = new[]
            {
                // 목표 샷 01 초원 중간 (134,119,71): 노란 연두보다 황토 쪽 (it52 샘플 155,152,77)
                ("GrassPatch", 1f, new Color(0.62f, 0.58f, 0.31f)),
                ("GrassThick", 0.8f, new Color(0.53f, 0.53f, 0.27f)),
                ("GrassTall", 0.35f, new Color(0.7f, 0.6f, 0.35f)),
                ("GrassThin", 0.5f, new Color(0.58f, 0.55f, 0.29f)),
                ("Flowers/Groundcover_Clovers", 0.25f, new Color(0.42f, 0.52f, 0.24f)),
                ("Flowers/Flower_Chamomile", 0.02f, Color.clear),
            };
            var meshes = new List<Mesh>();
            var mats = new List<Material>();
            var weights = new List<float>();
            const string dir = GeneratedDir + "/StylizedGrass";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(GeneratedDir, "StylizedGrass");
            foreach (var (name, weight, tip) in picks)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pkg + name + ".prefab");
                if (prefab == null) continue;
                var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                var mf = filters.FirstOrDefault(f => f.name.EndsWith("LOD0")) ?? filters.FirstOrDefault();
                var mr = mf != null ? mf.GetComponent<MeshRenderer>() : null;
                if (mf == null || mf.sharedMesh == null || mr == null || mr.sharedMaterial == null) continue;
                var copyPath = $"{dir}/{mr.sharedMaterial.name}_Field.mat";
                var copy = AssetDatabase.LoadAssetAtPath<Material>(copyPath);
                if (copy == null)
                {
                    copy = new Material(mr.sharedMaterial);
                    AssetDatabase.CreateAsset(copy, copyPath);
                }
                copy.enableInstancing = true;
                if (copy.HasProperty("_ColorMapStrength")) copy.SetFloat("_ColorMapStrength", 1f);
                if (copy.HasProperty("_ColorMapHeight")) copy.SetFloat("_ColorMapHeight", 0.2f); // 땅 색이 잎 중간까지 올라온다
                if (tip.a > 0f && copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", tip);
                if (copy.HasProperty("_Translucency")) copy.SetFloat("_Translucency", 0.45f);
                EditorUtility.SetDirty(copy);
                meshes.Add(mf.sharedMesh);
                mats.Add(copy);
                weights.Add(weight);
            }
            cat.sgsMeshes = meshes.ToArray();
            cat.sgsMaterials = mats.ToArray();
            cat.sgsWeights = weights.ToArray();
            Debug.Log($"[Manyworld] Stylized Grass {meshes.Count}종: {string.Join(", ", meshes.Select(m => m.name))}");
        }

        /// <summary>회화풍 후처리 (쿠와하라) 전체 화면 패스를 PC 렌더러에 붙인다. 켜고 끄는 건 전역 _ManyworldPaint.</summary>
        public static void EnsurePainterlyFeature()
        {
            const string matPath = "Assets/_Project/Resources/Materials/Painterly.mat";
            var shader = Shader.Find("Manyworld/Painterly");
            var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
            if (shader == null || data == null) { Debug.LogWarning("[Manyworld] 회화풍 후처리 준비 실패"); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "Painterly" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var feature = data.rendererFeatures.OfType<UnityEngine.Rendering.Universal.FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == "Painterly");
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.FullScreenPassRendererFeature>();
                feature.name = "Painterly";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            mat.SetFloat("_Radius", 3f);
            mat.SetFloat("_Sharpness", 4f);
            mat.SetFloat("_Amount", 1f);
            EditorUtility.SetDirty(mat);
            feature.passMaterial = mat;
            feature.injectionPoint = UnityEngine.Rendering.Universal.FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[Manyworld] 회화풍 후처리 준비");
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
