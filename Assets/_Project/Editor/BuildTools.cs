using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Manyworld.EditorTools
{
    public static class BuildTools
    {
        [MenuItem("Manyworld/Build Windows")]
        public static void BuildWindows()
        {
            ProjectSetup.IncludeTerrainEngineShaders();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                // 숲 현장의 구운 간접광 씬 (있으면)
                scenes = System.IO.File.Exists("Assets/_Project/Scenes/ForestLighting.unity")
                    ? new[] { "Assets/_Project/Scenes/Main.unity", "Assets/_Project/Scenes/ForestLighting.unity" }
                    : new[] { "Assets/_Project/Scenes/Main.unity" },
                locationPathName = "Builds/Windows/Manyworld.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[Manyworld] Build {report.summary.result}: {report.summary.totalSize / (1024 * 1024)}MB, errors {report.summary.totalErrors}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
