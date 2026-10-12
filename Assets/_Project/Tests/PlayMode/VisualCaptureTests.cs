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
            // 자연 월드 (숲 현장) 먼저
            MissionHUD.ShowArrival = false;
            gm.StartField();
            yield return new WaitForSeconds(2f);
            yield return CaptureField(dir);
            gm.EnterHub();
            yield return new WaitForSeconds(0.5f);
            Capture(Path.Combine(dir, "hub.png"));

            // 날씨·시간대: 종류마다 그날을 찾아 우리 동네를 찍는다
            MissionHUD.ShowArrival = false;
            foreach (var kind in new[] { WeatherKind.Overcast, WeatherKind.Rain, WeatherKind.MorningFog, WeatherKind.Snow })
            {
                for (int d = 1; d < 400; d++)
                {
                    var date = new System.DateTime(1987, 4, 1).AddDays(d);
                    if (WeatherState.ForHome(d, date.Month).kind != kind) continue;
                    gm.Save.day = d;
                    gm.EnterHub();
                    yield return new WaitForSeconds(1f);
                    Capture(Path.Combine(dir, $"home_{kind}_{gm.HomeWeather.time}.png"));
                    break;
                }
            }
            gm.Save.day = 0;
            gm.EnterHub();
            yield return null;

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

        /// <summary>숲 현장: 목표 샷 01(흙길)·03(빈 방) 구도와 하늘, 닫힌 방.</summary>
        static IEnumerator CaptureField(string dir)
        {
            var mc = MissionController.Instance;
            var cam = Camera.main;
            var pc = cam.GetComponent<PlayerCamera>();
            var terrain = mc.Layout.terrain;
            Vector3 Ground(Vector3 p, float up) { p.y = terrain.SampleHeight(p) + up; return p; }
            void Shot(string name, Vector3 pos, Vector3 look)
            {
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                Capture(Path.Combine(dir, name));
            }

            Capture(Path.Combine(dir, "field_start.png"));
            var keep = pc.target;
            pc.target = null;
            var car = mc.Car.transform;
            Shot("field_road.png", Ground(car.position - car.forward * 8f + car.right * 1.5f, 2.3f), car.position + car.forward * 40f + Vector3.up * 3f);
            Shot("field_aerial.png", new Vector3(170, 250, 120), new Vector3(430, 40, 470));
            var room = mc.Room.transform;
            float half = ForestSiteBuilder.RoomHalf;
            Shot("field_trailhead.png", Ground(mc.Layout.midpointPos - room.forward * 8f, 2f), room.position + Vector3.up * 4f);
            Shot("field_room.png", Ground(room.position - room.forward * (half + 6f), 1.7f), room.position + Vector3.up * 1.2f);
            // 망원: 먼 산비탈 숲 (하얀 점 진단)
            float fov = cam.fieldOfView;
            cam.fieldOfView = 6f;
            var north = new Vector3(ForestSiteBuilder.Size * 0.45f, 0, ForestSiteBuilder.Size - 90f);
            Shot("field_tele.png", Ground(mc.Layout.ticoPos, 3f), Ground(north, 0f));
            cam.fieldOfView = fov;
            Shot("field_room_inside.png", Ground(room.position - room.forward * 2f + room.right * 3f, 1.6f), room.position + room.forward * half + Vector3.up * 3f);

            // 기사와 은주가 방 앞에 선 모습 (3인칭)
            pc.target = keep;
            var stand = Ground(room.position - room.forward * (half + 5f), 0.1f);
            mc.WarpParty(stand, room.eulerAngles.y, Ground(stand + room.right * 1.5f + room.forward * 1.5f, 0.1f));
            yield return new WaitForSeconds(1.2f);
            Capture(Path.Combine(dir, "field_party_room.png"));

            pc.target = null;
            mc.Room.ForceClosed();
            yield return null;
            Shot("field_room_closed.png", Ground(room.position + room.forward * 3f, 1.7f), mc.Room.Entrance + Vector3.up * 2.5f);
            pc.target = keep;
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
