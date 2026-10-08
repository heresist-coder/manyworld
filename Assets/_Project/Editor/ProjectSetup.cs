using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Manyworld.EditorTools
{
    /// <summary>Main 씬을 만든다. 배치 모드에서도 돈다: -executeMethod Manyworld.EditorTools.ProjectSetup.CreateMainScene</summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        [MenuItem("Manyworld/Create Main Scene")]
        public static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.AddComponent<PlayerCamera>();
            cam.transform.position = new Vector3(0, 38, -70);

            new GameObject("GameManager").AddComponent<GameManager>();
            CreateBaseMaterials();
            CreateTerrainShaderStub();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/SampleScene.unity") != null)
                AssetDatabase.DeleteAsset("Assets/Scenes");

            AssetDatabase.SaveAssets();
            Debug.Log("[Manyworld] Main scene created");
        }

        /// <summary>
        /// 빌드에서 URP Lit 셰이더와 반투명·발광 변형이 빠지지 않도록 베이스 머티리얼을 에셋으로 둔다.
        /// Graybox가 런타임에 이걸 복제해 색만 바꾼다.
        /// </summary>
        [MenuItem("Manyworld/Create Base Materials")]
        public static void CreateBaseMaterials()
        {
            const string dir = "Assets/_Project/Resources/Materials";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources")) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Materials");
            var lit = Shader.Find("Universal Render Pipeline/Lit");

            var opaque = new Material(lit) { color = Color.gray };
            opaque.SetFloat("_Smoothness", 0.15f);
            Save(opaque, dir + "/GrayboxOpaque.mat");

            var emissive = new Material(lit) { color = Color.white };
            emissive.SetFloat("_Smoothness", 0.15f);
            emissive.EnableKeyword("_EMISSION");
            emissive.SetColor("_EmissionColor", Color.white);
            emissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Save(emissive, dir + "/GrayboxEmissive.mat");

            var transparent = new Material(lit) { color = new Color(1, 1, 1, 0.5f) };
            transparent.SetFloat("_Smoothness", 0.15f);
            transparent.SetFloat("_Surface", 1f);
            transparent.SetFloat("_Blend", 0f);
            transparent.SetOverrideTag("RenderType", "Transparent");
            transparent.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            transparent.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            transparent.SetInt("_ZWrite", 0);
            transparent.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            transparent.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            Save(transparent, dir + "/GrayboxTransparent.mat");

            // 하늘: 런타임에 색만 바꾼다
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null) Save(new Material(skyShader), dir + "/Sky.mat");

            // 지형: 머티리얼을 비워 두면 안 그려지는 경우가 있어 URP Terrain/Lit을 명시한다 (빌드에서도 빠지지 않게 에셋으로)
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader != null) Save(new Material(terrainShader), dir + "/Terrain.mat");
            CreateParticleMaterial();
            Debug.Log("[Manyworld] Base materials created");
        }

        /// <summary>비·눈 입자: URP Particles/Unlit 반투명. 텍스처는 런타임에 만든다.</summary>
        [MenuItem("Manyworld/Create Particle Material")]
        public static void CreateParticleMaterial()
        {
            const string path = "Assets/_Project/Resources/Materials/Particle.mat";
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) return;
            var m = new Material(sh) { color = Color.white };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { existing.CopyPropertiesFromMaterial(m); existing.shader = sh; EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 큰 월드 지형은 런타임에만 만들어서, 씬에 지형이 없으면 빌드가 지형 엔진·URP Terrain 셰이더를 빼 버린다
        /// ("Unable to find shaders used for the terrain engine"). 보이지 않는 작은 지형을 씬에 둬서 포함시킨다.
        /// 런타임 지형과 같은 구성: Terrain.mat, 레이어 5개(애드 패스), 나무 원형, 인스턴싱.
        /// </summary>
        public static void CreateTerrainShaderStub()
        {
            const string dir = "Assets/_Project/Scenes/TerrainStub";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Project/Scenes", "TerrainStub");

            var layers = new TerrainLayer[5];
            for (int i = 0; i < 5; i++)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, true);
                var px = new Color[16];
                for (int k = 0; k < 16; k++) px[k] = new Color(0.4f, 0.5f, 0.3f, 0.06f);
                tex.SetPixels(px);
                tex.Apply();
                string texPath = $"{dir}/StubLayer{i}.asset";
                AssetDatabase.DeleteAsset(texPath);
                AssetDatabase.CreateAsset(tex, texPath);
                var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(6, 6) };
                string layerPath = $"{dir}/StubLayer{i}.terrainlayer";
                AssetDatabase.DeleteAsset(layerPath);
                AssetDatabase.CreateAsset(layer, layerPath);
                layers[i] = layer;
            }

            // 나무 원형: 루트에 메시 하나 (터레인 나무 규칙)
            var treeGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(treeGo.GetComponent<Collider>());
            treeGo.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Resources/Materials/GrayboxOpaque.mat");
            string treePath = $"{dir}/StubTree.prefab";
            var treePrefab = PrefabUtility.SaveAsPrefabAsset(treeGo, treePath);
            Object.DestroyImmediate(treeGo);

            var td = new TerrainData { heightmapResolution = 33, size = new Vector3(8, 2, 8) };
            td.terrainLayers = layers;
            td.alphamapResolution = 16;
            td.treePrototypes = new[] { new TreePrototype { prefab = treePrefab } };
            td.SetTreeInstances(new[] { new TreeInstance { position = new Vector3(0.5f, 0, 0.5f), prototypeIndex = 0, widthScale = 1, heightScale = 1, color = Color.white, lightmapColor = Color.white } }, false);
            string tdPath = $"{dir}/StubTerrain.asset";
            AssetDatabase.DeleteAsset(tdPath);
            AssetDatabase.CreateAsset(td, tdPath);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "TerrainShaderStub (빌드용, 화면 밖)";
            go.transform.position = new Vector3(0, -800, 0);
            var t = go.GetComponent<Terrain>();
            t.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Resources/Materials/Terrain.mat");
            t.drawInstanced = true;
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 지형 엔진이 쓰는 숨김 셰이더(나무 빌보드, 디테일, 스플랫맵 보조 패스)를 "항상 포함"에 넣는다.
        /// 런타임에만 지형을 만들면 빌드가 이걸 빼서 지형이 안 그려진다.
        /// URP Terrain/Lit 본체는 Resources/Materials/Terrain.mat이 끌고 들어간다.
        /// </summary>
        [MenuItem("Manyworld/Include Terrain Engine Shaders")]
        public static void IncludeTerrainEngineShaders()
        {
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            var have = new System.Collections.Generic.HashSet<Object>();
            for (int i = 0; i < arr.arraySize; i++) have.Add(arr.GetArrayElementAtIndex(i).objectReferenceValue);
            int added = 0;
            foreach (var info in ShaderUtil.GetAllShaderInfo())
            {
                string n = info.name;
                bool want = n.Contains("TerrainEngine") || n.StartsWith("Nature/Terrain") ||
                            (n.StartsWith("Hidden/Universal Render Pipeline/Terrain") && !n.Contains("Brush"));
                if (!want) continue;
                var sh = Shader.Find(n);
                if (sh == null || have.Contains(sh)) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                have.Add(sh);
                added++;
                Debug.Log($"[Manyworld] 항상 포함 셰이더 추가: {n}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Manyworld] 지형 엔진 셰이더 {added}개 추가");
        }

        static void Save(Material m, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
        }
    }
}
