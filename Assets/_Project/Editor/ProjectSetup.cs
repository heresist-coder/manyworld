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

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/SampleScene.unity") != null)
                AssetDatabase.DeleteAsset("Assets/Scenes");

            CreateBaseMaterials();
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

            // 지형: 머티리얼을 비워 두면 안 그려지는 경우가 있어 URP Terrain/Lit을 명시한다 (빌드에서도 빠지지 않게 에셋으로)
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader != null) Save(new Material(terrainShader), dir + "/Terrain.mat");
            Debug.Log("[Manyworld] Base materials created");
        }

        static void Save(Material m, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
        }
    }
}
