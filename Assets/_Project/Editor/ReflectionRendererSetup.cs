using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Manyworld.EditorTools
{
    /// <summary>
    /// 호수 평면 반사용 가벼운 렌더러 (렌더러 기능 없음: 화면 공간 GI·볼류메트릭 연무·SSAO·회화풍 필터를 반사에 돌리지 않는다).
    /// PC·Mobile 파이프라인 에셋의 렌더러 목록 끝에 붙인다. PlanarReflection이 이름("Reflection_Renderer")으로 찾는다.
    /// 반사 카메라가 기본 렌더러를 쓰면 t05에서 23ms가 더 들었다.
    /// </summary>
    public static class ReflectionRendererSetup
    {
        const string Path = "Assets/Settings/Reflection_Renderer.asset";

        [MenuItem("Manyworld/Render/Ensure Reflection Renderer")]
        public static void Ensure()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                data.name = "Reflection_Renderer";
                AssetDatabase.CreateAsset(data, Path);
            }
            data.renderingMode = RenderingMode.Forward;
            data.rendererFeatures.Clear();
            EditorUtility.SetDirty(data);

            foreach (var assetPath in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
                if (rp == null) continue;
                var so = new SerializedObject(rp);
                var list = so.FindProperty("m_RendererDataList");
                bool has = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) has = true;
                if (!has)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(rp);
                Debug.Log($"[Manyworld] 반사 렌더러 {(has ? "있음" : "추가")}: {assetPath}");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
