using UnityEngine;
using UnityEngine.SceneManagement;

namespace Manyworld
{
    /// <summary>
    /// 숲 현장의 구운 간접광. 에디터에서 같은 시드로 만든 숲에 APV(적응형 프로브 볼륨)를 구워
    /// 빛 데이터만 남긴 씬(ForestLighting)을 현장에 있는 동안 덧붙여 연다 (Manyworld > Field > Bake Forest Lighting).
    /// 숲은 시드 1로 항상 같게 생성되므로 구운 빛이 그대로 맞는다.
    /// </summary>
    public static class ForestLighting
    {
        public const string SceneName = "ForestLighting";

        public static bool Loaded => SceneManager.GetSceneByName(SceneName).isLoaded;

        public static void Load()
        {
            if (Loaded) return;
            if (!Application.CanStreamedLevelBeLoaded(SceneName))
            {
                Debug.Log("[Manyworld] 구운 숲 조명 씬이 없다 (Manyworld > Field > Bake Forest Lighting)");
                return;
            }
            SceneManager.LoadScene(SceneName, LoadSceneMode.Additive);
        }

        public static void Unload()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }
    }
}
