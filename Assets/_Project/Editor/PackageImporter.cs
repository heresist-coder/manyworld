using System;
using UnityEditor;
using UnityEngine;

namespace Manyworld.EditorTools
{
    /// <summary>
    /// 배치 모드 패키지 가져오기. -importPackage는 비동기 임포트가 끝나기 전에 종료될 때가 있어서,
    /// 완료 콜백을 받은 뒤 종료한다.
    /// 사용: -executeMethod Manyworld.EditorTools.PackageImporter.Run -mwpackages "a.unitypackage;b.unitypackage"
    /// </summary>
    public static class PackageImporter
    {
        static string[] queue;
        static int index;

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-mwpackages") queue = args[i + 1].Split(';');
            if (queue == null || queue.Length == 0)
            {
                Debug.LogError("[Manyworld] -mwpackages 없음");
                EditorApplication.Exit(1);
                return;
            }
            AssetDatabase.importPackageCompleted += name => { Debug.Log($"[Manyworld] 가져옴: {name}"); Next(); };
            AssetDatabase.importPackageFailed += (name, err) => { Debug.LogError($"[Manyworld] 실패: {name} — {err}"); Next(); };
            AssetDatabase.importPackageCancelled += name => { Debug.LogError($"[Manyworld] 취소: {name}"); Next(); };
            index = -1;
            Next();
        }

        static void Next()
        {
            index++;
            if (index >= queue.Length)
            {
                AssetDatabase.Refresh();
                Debug.Log("[Manyworld] 패키지 가져오기 완료");
                EditorApplication.Exit(0);
                return;
            }
            Debug.Log($"[Manyworld] 가져오는 중: {queue[index]}");
            AssetDatabase.ImportPackage(queue[index], false);
        }
    }
}
