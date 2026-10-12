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

        /// <summary>숲 현장만: 목표 샷과 같은 구도 (t01 흙길, t03 빈 방, t02 숲길, 하늘).</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator CaptureFieldTargets()
        {
            string dir = System.Environment.GetEnvironmentVariable("MANYWORLD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("캡처 경로 없음"); yield break; }
            Directory.CreateDirectory(dir);
            var gm = GameManager.Instance;
            MissionHUD.ShowArrival = false;
            gm.StartField();
            yield return new WaitForSeconds(2f);
            var mc = MissionController.Instance;
            var cam = Camera.main;
            var pc = cam.GetComponent<PlayerCamera>();
            var hud = mc.GetComponent<MissionHUD>();
            var terrain = mc.Layout.terrain;
            Vector3 Ground(Vector3 p, float up) { p.y = terrain.SampleHeight(p) + up; return p; }
            void Shot(string name, Vector3 pos, Vector3 look)
            {
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                Capture(Path.Combine(dir, name));
            }
            var keep = pc.target;

            // t01: 흙길 위 노란 차 뒤에서 길을 따라 (두 사람은 차 안)
            var car = mc.Car.transform;
            mc.WarpParty(car.position - car.right * 2.2f + Vector3.up * 0.3f, car.eulerAngles.y, car.position + car.right * 2.2f);
            yield return null;
            mc.EnterCar();
            yield return new WaitForSeconds(0.5f);
            pc.target = null;
            // 그림자가 길 위에 오는 순간
            var shadow = GameObject.Find("CrossingShadow");
            if (shadow != null)
            {
                var ds = shadow.GetComponent<DriftingShadow>();
                ds.enabled = false;
                shadow.transform.position = Vector3.Lerp(ds.from, ds.to, 0.45f);
            }
            Shot("t01_road.png", Ground(car.position - car.forward * 7f + car.right * 1.4f, 2.1f), car.position + car.forward * 50f - car.right * 8f + Vector3.up * 6f);
            Shot("t01_wide.png", Ground(car.position - car.forward * 12f - car.right * 3f, 3.5f), car.position + car.forward * 120f + Vector3.up * 20f);
            Shot("aerial.png", new Vector3(170, 250, 120), new Vector3(430, 40, 470));
            var fence = GameObject.Find("Fence");
            if (fence != null) Shot("fence.png", fence.transform.position + new Vector3(6f, 1.2f, -4f), fence.transform.position);
            Shot("car.png", car.position + car.right * 5f + Vector3.up * 1.2f, car.position + Vector3.up * 0.6f);
            Shot("grass.png", Ground(car.position - car.right * 9f, 0.9f), Ground(car.position - car.right * 30f + car.forward * 10f, 0f));
            foreach (var probeName in new[] { "RoadRock", "SM_Plant_Bush" })
            {
                Transform probe = null;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.StartsWith(probeName) && t.GetComponentInChildren<Renderer>() != null) { probe = t; break; }
                if (probe == null) { Debug.Log($"[Diag] {probeName} 없음"); continue; }
                foreach (var r in probe.GetComponentsInChildren<Renderer>())
                    Debug.Log($"[Diag] {probeName} {r.name} enabled={r.enabled} mats={string.Join(",", System.Array.ConvertAll(r.sharedMaterials, m => m == null ? "null" : m.name + "/" + m.shader.name + "/" + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-")))}");
                Shot($"diag_{probeName}.png", probe.position + new Vector3(3f, 1.5f, -3f), probe.position + Vector3.up * 0.5f);
            }
            // 진단: 먼 숲 망원 (잎 밉맵이 하얗게 번지지 않는지)
            float fov0 = cam.fieldOfView;
            cam.fieldOfView = 7f;
            Shot("diag_far.png", Ground(car.position - car.forward * 7f, 2.1f), car.position + car.forward * 430f - car.right * 110f + Vector3.up * 25f);
            cam.fieldOfView = fov0;
            var leanRoom = mc.Room.transform;
            var mid = Vector3.Lerp(mc.Layout.midpointPos, leanRoom.position, 0.45f);
            Shot("lean.png", Ground(mid - leanRoom.right * 30f, 2f), mid + Vector3.up * 6f);

            // t03: 두 사람이 방 입구에 서서 안을 본다
            pc.target = keep;
            mc.ExitCar();
            var room = mc.Room.transform;
            float half = ForestSiteBuilder.RoomHalf;
            var stand = Ground(room.position - room.forward * (half + 2.5f) - room.right * 1.8f, 0.1f);
            mc.WarpParty(stand, room.eulerAngles.y, Ground(stand + room.right * 2.6f + room.forward * 1.5f, 0.1f));
            mc.Companion.order = CompanionOrder.Hold;
            yield return new WaitForSeconds(1f);
            pc.target = null;
            hud.enabled = false;
            Shot("t03_room.png", Ground(stand - room.forward * 4.5f + room.right * 1.6f, 2.3f), room.position + Vector3.up * 1.4f);
            // t02: 숲길 위에서 방 쪽으로 (길은 가운데서 살짝 꺾인다)
            var th = ForestSiteBuilder.Trailhead;
            var bend = Vector2.Lerp(th, ForestSiteBuilder.RoomEntrance, 0.5f) + new Vector2(-6f, 2f);
            var onTrail = Vector2.Lerp(th, bend, 0.45f);
            var ent = ForestSiteBuilder.RoomEntrance;
            Shot("t02_path.png", Ground(new Vector3(onTrail.x, 0, onTrail.y), 2.4f), Ground(new Vector3(ent.x, 0, ent.y), 1.5f));
            hud.enabled = true;
            pc.target = keep;
            gm.EnterHub();
            yield return null;
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
