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
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Main.unity" },
                locationPathName = "Builds/Windows/Manyworld.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[Manyworld] Build {report.summary.result}: {report.summary.totalSize / (1024 * 1024)}MB, errors {report.summary.totalErrors}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
