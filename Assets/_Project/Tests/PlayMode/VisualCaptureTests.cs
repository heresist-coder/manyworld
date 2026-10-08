using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Manyworld.Tests
{
    /// <summary>카메라 렌더를 PNG로 남긴다. 환경변수 MANYWORLD_CAPTURE_DIR가 있을 때만 돈다.</summary>
    public class VisualCaptureTests
    {
        [UnityTest, Timeout(900000)]
        public IEnumerator CaptureWorlds()
        {
            string dir = System.Environment.GetEnvironmentVariable("MANYWORLD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("캡처 경로 없음"); yield break; }
            Directory.CreateDirectory(dir);
            SaveData.PathOverride = Path.Combine(Application.temporaryCachePath, "manyworld_capture_save.json");
            yield return null;
            var gm = GameManager.Instance;
            gm.ResetSave();
            if (gm.Mode != GameMode.Hub) gm.EnterHub();
            yield return new WaitForSeconds(0.5f);
            Capture(Path.Combine(dir, "hub.png"));

            gm.BeginDrive(CallContract.Create(47, new System.Random(1)));
            yield return new WaitForSeconds(1.5f);
            Capture(Path.Combine(dir, "drive_start.png"));
            gm.CancelDrive();
            yield return null;

            foreach (int n in new[] { 3, 47, 200, 999 })
            {
                gm.StartCall(CallContract.Create(n, new System.Random(n)));
                yield return new WaitForSeconds(1.5f);
                var mc = MissionController.Instance;
                // 몬스터 하나를 앞에 세워 둔다
                var m = mc.Monsters[mc.Monsters.Count - 1];
                m.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(mc.Player.transform.position + Quaternion.Euler(0, 45, 0) * Vector3.forward * 8f);
                yield return new WaitForSeconds(0.5f);
                var spec = mc.Spec;
                Capture(Path.Combine(dir, $"world_{n}_{spec.rules}_{spec.terrain}.png".Replace(", ", "+")));
                // 하늘에서 본 큰 월드
                var pc = Camera.main.GetComponent<PlayerCamera>();
                var keep = pc.target;
                pc.target = null;
                Camera.main.transform.SetPositionAndRotation(new Vector3(300, 230, 120), Quaternion.Euler(38, 35, 0));
                Capture(Path.Combine(dir, $"world_{n}_aerial.png"));
                Camera.main.transform.SetPositionAndRotation(mc.Car.transform.position + new Vector3(-6, 3, -6), Quaternion.LookRotation(mc.Car.transform.position - (mc.Car.transform.position + new Vector3(-6, 3, -6))));
                Capture(Path.Combine(dir, $"world_{n}_tico.png"));
                // 게이트 정면, 마을(장터거리) 근경
                var gate = mc.Layout.gatePos + new Vector3(-8, 0, -6);
                var gcam = gate + Quaternion.Euler(0, 40, 0) * new Vector3(0, 3.5f, -16f);
                Camera.main.transform.SetPositionAndRotation(gcam, Quaternion.LookRotation(gate + Vector3.up * 3.5f - gcam));
                Capture(Path.Combine(dir, $"world_{n}_gate.png"));
                var village = mc.Layout.midpointPos;
                var vcam = village + new Vector3(-30, 9, -34);
                Camera.main.transform.SetPositionAndRotation(vcam, Quaternion.LookRotation(village + Vector3.up * 2f - vcam));
                Capture(Path.Combine(dir, $"world_{n}_village.png"));
                pc.target = keep;
                Object.Destroy(mc.gameObject);
                yield return null;
                gm.EnterHub();
                yield return null;
            }
            gm.ResetSave();
            SaveData.PathOverride = null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator DiagnoseTerrain()
        {
            string dir = System.Environment.GetEnvironmentVariable("MANYWORLD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("캡처 경로 없음"); yield break; }
            Directory.CreateDirectory(dir);
            yield return null;
            var gm = GameManager.Instance;
            gm.StartCall(CallContract.Create(47, new System.Random(47)));
            yield return new WaitForSeconds(1f);
            var mc = MissionController.Instance;
            var t = mc.Layout.terrain;
            var mat = t.materialTemplate;
            Debug.Log($"[Diag] terrain enabled={t.enabled} drawHeightmap={t.drawHeightmap} instanced={t.drawInstanced} size={t.terrainData.size} " +
                      $"mat={(mat ? mat.shader.name : "null")} supported={(mat ? mat.shader.isSupported : false)} layers={t.terrainData.terrainLayers.Length} " +
                      $"h(center)={t.SampleHeight(new Vector3(512, 0, 512))} h(gate)={t.SampleHeight(new Vector3(150, 0, 150))} pos={t.transform.position} layer={t.gameObject.layer} " +
                      $"camCulling={Camera.main.cullingMask} far={Camera.main.farClipPlane}");
            var pc0 = Camera.main.GetComponent<PlayerCamera>();
            pc0.target = null;
            Camera.main.transform.SetPositionAndRotation(mc.Car.transform.position + new Vector3(-6, 3, -6), Quaternion.Euler(15, 45, 0));
            Capture(Path.Combine(dir, "diag_low_withfog.png"));
            foreach (var b in Object.FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
                if (b.GetType().Namespace == "VolumetricFogAndMist2") b.enabled = false;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.name == "Water") r.enabled = false;
            yield return null;
            Capture(Path.Combine(dir, "diag_low_nofog.png"));
            RenderSettings.fog = false;
            var pc = Camera.main.GetComponent<PlayerCamera>();
            pc.target = null;
            Camera.main.transform.SetPositionAndRotation(new Vector3(300, 230, 120), Quaternion.Euler(38, 35, 0));
            Capture(Path.Combine(dir, "diag_aerial_nowater.png"));
            t.drawInstanced = false;
            yield return null;
            Capture(Path.Combine(dir, "diag_aerial_noinstancing.png"));
            Object.Destroy(mc.gameObject);
            yield return null;
            gm.EnterHub();
        }

        static void Capture(string path)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(rt);
            Debug.Log($"[Capture] {path}");
        }
    }
}
