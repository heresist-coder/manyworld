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
            // 비교용: MANYWORLD_CAPTURE_FX에 nogi / nofog가 있으면 간접광·연무를 끄고 찍는다
            string fx = System.Environment.GetEnvironmentVariable("MANYWORLD_CAPTURE_FX") ?? "";
            ScreenSpaceGI.Enabled = !fx.Contains("nogi");
            var mist = MissionController.FieldMist;
            if (fx.Contains("nofog")) MissionController.FieldMist.density = 0f;
            gm.StartField();
            yield return new WaitForSeconds(2f);
            var mc = MissionController.Instance;
            var cam = Camera.main;
            var pc = cam.GetComponent<PlayerCamera>();
            var hud = mc.GetComponent<MissionHUD>();
            var terrain = mc.Layout.terrain;
            Vector3 Ground(Vector3 p, float up) { p.y = terrain.SampleHeight(p) + up; return p; }
            // 간접광은 시간 누적이라 카메라를 옮긴 뒤 몇십 프레임 그려서 가라앉힌 다음 찍는다
            IEnumerator Shot(string name, Vector3 pos, Vector3 look)
            {
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                yield return Settle(cam, 24);
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
            // 목표 샷 01 구도: 오른쪽 뒤 높은 곳에서 길을 따라. 차는 왼쪽, 바위 무더기는 오른쪽 앞
            // (길이 차 앞 40m에서 오른쪽으로 휘므로 시선도 길을 따라 오른쪽으로: 차가 화면 왼쪽 아래에 온다)
            // (11m 뒤 6.5m 높이는 차가 너무 작고 멀었다)
            yield return Shot("t01_road.png", Ground(car.position - car.forward * 7.5f + car.right * 4f, 4.4f), car.position + car.forward * 50f + car.right * 4f + Vector3.up * 2.5f);
            yield return Shot("t01_chase.png", Ground(car.position - car.forward * 7f + car.right * 1.4f, 2.1f), car.position + car.forward * 50f - car.right * 8f + Vector3.up * 6f);
            yield return Shot("t01_wide.png", Ground(car.position - car.forward * 12f - car.right * 3f, 3.5f), car.position + car.forward * 120f + Vector3.up * 20f);
            yield return Shot("aerial.png", new Vector3(170, 250, 120), new Vector3(430, 40, 470));
            var fence = GameObject.Find("Fence");
            if (fence != null) yield return Shot("fence.png", fence.transform.position + new Vector3(6f, 1.2f, -4f), fence.transform.position);
            yield return Shot("car.png", car.position + car.right * 5f + Vector3.up * 1.2f, car.position + Vector3.up * 0.6f);
            yield return Shot("grass.png", Ground(car.position - car.right * 9f, 0.9f), Ground(car.position - car.right * 30f + car.forward * 10f, 0f));
            foreach (var probeName in new[] { "RoadRock", "SM_Plant_Bush" })
            {
                Transform probe = null;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.StartsWith(probeName) && t.GetComponentInChildren<Renderer>() != null) { probe = t; break; }
                if (probe == null) { Debug.Log($"[Diag] {probeName} 없음"); continue; }
                foreach (var r in probe.GetComponentsInChildren<Renderer>())
                    Debug.Log($"[Diag] {probeName} {r.name} enabled={r.enabled} mats={string.Join(",", System.Array.ConvertAll(r.sharedMaterials, m => m == null ? "null" : m.name + "/" + m.shader.name + "/" + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-")))}");
                yield return Shot($"diag_{probeName}.png", probe.position + new Vector3(3f, 1.5f, -3f), probe.position + Vector3.up * 0.5f);
            }
            // 진단: 먼 숲 망원 (잎 밉맵이 하얗게 번지지 않는지)
            float fov0 = cam.fieldOfView;
            cam.fieldOfView = 7f;
            yield return Shot("diag_far.png", Ground(car.position - car.forward * 7f, 2.1f), car.position + car.forward * 430f - car.right * 110f + Vector3.up * 25f);
            cam.fieldOfView = fov0;
            var leanRoom = mc.Room.transform;
            var mid = Vector3.Lerp(mc.Layout.midpointPos, leanRoom.position, 0.45f);
            yield return Shot("lean.png", Ground(mid - leanRoom.right * 30f, 2f), mid + Vector3.up * 6f);

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
            // 목표 샷 03: 입구 밖 조금 높은 곳에서. 안쪽 벽 소나무 꼭대기와 그 위 하늘·산이 보인다
            // (4.5m 뒤 2.3m 높이는 양옆 벽 수관이 화면 위를 덮어 하늘이 없었다)
            yield return Shot("t03_room.png", Ground(stand - room.forward * 7.5f - room.right * 0.6f, 3f), room.position + Vector3.up * 4.2f);
            // t02: 숲길 위에서 방 쪽으로 (길은 가운데서 살짝 꺾인다)
            var th = ForestSiteBuilder.Trailhead;
            var bend = Vector2.Lerp(th, ForestSiteBuilder.RoomEntrance, 0.5f) + new Vector2(-6f, 2f);
            // 목표 샷 02 구도: 터널 어귀(주차 공터 끝)에서 조금 높게, 기운 숲 속으로
            var onTrail = Vector2.Lerp(th, bend, 0.2f);
            var ent = ForestSiteBuilder.RoomEntrance;
            // 두 사람이 카메라 앞 숲길을 걸어 들어간다 (목표 샷 02)
            var walk = Vector2.Lerp(th, bend, 0.46f);
            var trailDir = (bend - th).normalized;
            float trailYaw = Mathf.Atan2(trailDir.x, trailDir.y) * Mathf.Rad2Deg;
            var trailRight = new Vector2(trailDir.y, -trailDir.x);
            mc.WarpParty(Ground(new Vector3(walk.x, 0, walk.y) - new Vector3(trailRight.x, 0, trailRight.y) * 0.7f, 0.1f), trailYaw,
                Ground(new Vector3(walk.x, 0, walk.y) + new Vector3(trailRight.x + trailDir.x * 0.8f, 0, trailRight.y + trailDir.y * 0.8f) * 1f, 0.1f));
            yield return new WaitForSeconds(0.5f);
            // 목표 샷 02: 두 사람 바로 뒤 4~5m 높이에서 숲길을 내려다본다 (15m 뒤 3.2m는 사람이 작고 가까운 수관이 화면 위를 덮었다)
            var t02Cam = walk - trailDir * 8.5f - trailRight * 0.4f;
            var t02Look = walk + trailDir * 22f;
            yield return Shot("t02_path.png", Ground(new Vector3(t02Cam.x, 0, t02Cam.y), 3.9f), Ground(new Vector3(t02Look.x, 0, t02Look.y), 0.6f));

            // 목표 샷 04 구도: 길가 평평한 어깨에 차를 세우고 두 사람이 내려 비탈 숲의 철제 나무를 본다. 카메라는 그 뒤 길 위
            var iron = ForestSiteBuilder.IronTree;
            var spot = ForestSiteBuilder.IronRoadSpot;
            var roadDir = ForestSiteBuilder.IronRoadDir;
            var toIron = (iron - spot).normalized;
            {
                var carAt = spot - roadDir * 2.5f + toIron * 0.8f;
                var cr = Quaternion.LookRotation(new Vector3(roadDir.x, 0, roadDir.y));
                var crb = mc.Car.GetComponent<Rigidbody>();
                if (crb != null) crb.rotation = cr;
                mc.Car.transform.rotation = cr;
                mc.Car.Teleport(Ground(new Vector3(carAt.x, 0, carAt.y), 0.4f));
                var partyAt = spot + toIron * 2.6f + roadDir * 1.4f;
                mc.WarpParty(Ground(new Vector3(partyAt.x, 0, partyAt.y), 0.1f), Mathf.Atan2(toIron.x, toIron.y) * Mathf.Rad2Deg,
                    Ground(new Vector3(partyAt.x + roadDir.x * 1.1f, 0, partyAt.y + roadDir.y * 1.1f), 0.1f));
                yield return new WaitForSeconds(0.8f);
            }
            var ironBase = Ground(new Vector3(iron.x, 0, iron.y), 0f);
            var camXZ = spot - toIron * 9f - roadDir * 5f;
            var camIron = new Vector3(camXZ.x, Ground(new Vector3(spot.x, 0, spot.y), 0f).y + 3.2f, camXZ.y);
            yield return Shot("t04_iron.png", camIron, Vector3.Lerp(Ground(new Vector3(spot.x, 0, spot.y), 1f), ironBase + Vector3.up * 13f, 0.7f));

            // 목표 샷 05 구도: 남동쪽 물가 언덕에서 호수 너머 북서쪽 산. 물속 집이 앞쪽에 보인다
            var lv = ForestSiteBuilder.LakeView;
            var lc = ForestSiteBuilder.LakeCenter;
            // (물가 언덕 위 높이 4m에선 수면 반사가 물속 집을 가렸다 → 목표처럼 높이 내려다본다)
            var across = lc + (lc - lv) * 0.25f;
            // 진단: 물속 집을 위에서 (위치·깊이 확인)
            var sunk = GameObject.Find("SubmergedHouse");
            if (sunk != null)
            {
                var hb = ArtCatalog.RendererBounds(sunk);
                Debug.Log($"[Diag] 물속 집 중심 {hb.center} 꼭대기 {hb.max.y:F1} 수면 {ForestSiteBuilder.LakeWaterY:F1}");
                yield return Shot("diag_house.png", hb.center + new Vector3(0, ForestSiteBuilder.LakeWaterY - hb.center.y + 18f, -14f), hb.center);
            }
            // 전망대 평평한 마당에 차를 세우고 두 사람이 물가 쪽에 서서 호수를 본다 (목표 샷 05)
            var toLake = (lc - lv).normalized;
            var side = new Vector2(toLake.y, -toLake.x);
            var lakeCar = lv + toLake * 1.5f - side * 3.6f;
            var carRot = Quaternion.LookRotation(new Vector3(side.x, 0, side.y));
            var rb = mc.Car.GetComponent<Rigidbody>();
            if (rb != null) rb.rotation = carRot;
            mc.Car.transform.rotation = carRot;
            mc.Car.Teleport(Ground(new Vector3(lakeCar.x, 0, lakeCar.y), 0.4f));
            // 목표 샷 05는 조용히 호수를 보는 장면: 근처 몬스터를 잠깐 치운다 (두 사람이 총을 겨누고 서 있었다)
            var hiddenMonsters = new System.Collections.Generic.List<GameObject>();
            foreach (var mo in mc.Monsters)
                if (mo != null && mo.gameObject.activeSelf && Vector2.Distance(new Vector2(mo.transform.position.x, mo.transform.position.z), lv) < 80f)
                {
                    mo.gameObject.SetActive(false);
                    hiddenMonsters.Add(mo.gameObject);
                }
            var standLake = lv + toLake * 4.5f - side * 1.2f; // 목표 샷 05: 차 옆 왼쪽 물가에 서서 호수를 본다
            mc.WarpParty(Ground(new Vector3(standLake.x, 0, standLake.y), 0.1f), Mathf.Atan2(toLake.x, toLake.y) * Mathf.Rad2Deg,
                Ground(new Vector3(standLake.x - side.x * 1.2f, 0, standLake.y - side.y * 1.2f), 0.1f));
            yield return new WaitForSeconds(0.8f);
            // 목표 샷 05 구도: 물가 오른쪽 뒤 약간 높은 곳. 차·두 사람은 왼쪽 앞, 물속 집은 아래 가운데, 위는 노을 하늘과 산
            // (마당 뒤 8.5m에서 내려다보면 화면 아래 3분의 1이 밝은 흙 마당이었고 하늘이 안 보였다)
            var lakeCamXZ = lv - toLake * 6.5f + side * 6.5f; // (물가 바로 위 1.5m 뒤는 차가 화면 밖, 두 사람이 왼쪽 아래 귀퉁이에 잘렸다)
            var lakeCam = new Vector3(lakeCamXZ.x, Ground(new Vector3(lv.x, 0, lv.y), 0f).y + 5.5f, lakeCamXZ.y);
            var lakeLook = lc + (lv - lc) * 0.3f - side * 11f;
            float lakeDist = Vector2.Distance(lakeCamXZ, lakeLook);
            yield return Shot("t05_lake.png", lakeCam, new Vector3(lakeLook.x, lakeCam.y - lakeDist * Mathf.Tan(7f * Mathf.Deg2Rad), lakeLook.y));
            foreach (var mo in hiddenMonsters) mo.SetActive(true);
            hud.enabled = true;
            pc.target = keep;
            gm.EnterHub();
            ScreenSpaceGI.Enabled = true;
            MissionController.FieldMist = mist;
            yield return null;
        }

        /// <summary>
        /// 3인칭 총 들기 (gun-pose-unity): 기사·은주를 숲길 입구에 세우고 서 있기·조준 자세를 앞·옆·뒤에서 가까이 찍는다.
        /// [GUN-POSE] 로그: 총열과 몸 정면의 각도, 왼손 받침 오차.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator CaptureGunPose()
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
            var th = ForestSiteBuilder.Trailhead;
            var fwd2 = ForestSiteBuilder.RoomForward;
            var fwd = new Vector3(fwd2.x, 0, fwd2.y);
            var right = Vector3.Cross(Vector3.up, fwd);
            var stand = Ground(new Vector3(th.x, 0, th.y) - fwd * 6f, 0.1f);
            float yaw = Quaternion.LookRotation(fwd).eulerAngles.y;
            mc.WarpParty(stand, yaw, Ground(stand + right * 4f, 0.1f));
            mc.Companion.order = CompanionOrder.Hold;
            yield return new WaitForSeconds(1f);
            var keep = pc.target;
            pc.target = null;
            hud.enabled = false;
            var people = new (string name, Transform t)[] { ("driver", mc.Player.transform), ("companion", mc.Companion.transform) };
            foreach (var state in new[] { "idle", "aim" })
            {
                CharacterRig.QaAim = state == "aim";
                foreach (var (name, t) in people) t.rotation = Quaternion.LookRotation(fwd);
                yield return new WaitForSeconds(0.8f);
                foreach (var (name, t) in people)
                {
                    var rig = t.GetComponent<CharacterRig>();
                    if (rig != null && rig.rifle != null)
                    {
                        var se = $"{rig.GetType().GetProperty("SupportError")?.GetValue(rig)} reach={rig.GetType().GetProperty("SupportReach")?.GetValue(rig)}";
                        Debug.Log($"[GUN-POSE] {name} {state} barrelVsBody={Vector3.Angle(rig.rifle.forward, t.forward):F1} barrelPitch={Vector3.SignedAngle(Vector3.ProjectOnPlane(rig.rifle.forward, t.right), t.forward, t.right):F1} support={se}");
                    }
                    var p = t.position;
                    var tf = t.forward; var tr = t.right;
                    foreach (var (view, pos, look) in new[]
                    {
                        ("front", p + tf * 2.1f + tr * 0.9f + Vector3.up * 1.45f, p + Vector3.up * 1.2f),
                        ("side", p + tr * 2.3f + tf * 0.2f + Vector3.up * 1.35f, p + Vector3.up * 1.2f + tf * 0.2f),
                        ("back", p - tf * 2f + tr * 0.9f + Vector3.up * 1.7f, p + Vector3.up * 1.2f + tf * 1.5f),
                    })
                    {
                        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                        yield return Settle(cam, 8);
                        Capture(Path.Combine(dir, $"gun_{name}_{state}_{view}.png"));
                    }
                }
            }
            // 실제 플레이 카메라(배틀그라운드식 어깨 너머 TPS)로 서 있기·조준
            pc.target = keep;
            pc.yaw = Quaternion.LookRotation(fwd).eulerAngles.y;
            pc.pitch = 4f;
            foreach (var state in new[] { "idle", "aim" })
            {
                CharacterRig.QaAim = state == "aim";
                PlayerCamera.QaAim = state == "aim";
                mc.Player.transform.rotation = Quaternion.LookRotation(fwd);
                for (float tt = 0; tt < 1f; tt += Time.unscaledDeltaTime) yield return null; // 카메라가 따라와 가라앉게
                yield return Settle(cam, 8);
                Capture(Path.Combine(dir, $"tps_{state}.png"));
            }
            PlayerCamera.QaAim = null;

            // 재장전 중간 (상체 레이어 재장전 클립 + 왼손 받침 풀림): 기사는 실제 무기로, 은주는 진행도를 직접
            CharacterRig.QaAim = false;
            var weapon = mc.Player.GetComponent<PlayerWeapon>();
            var compRig = mc.Companion.GetComponent<CharacterRig>();
            if (weapon != null)
            {
                weapon.StartReload();
                for (float tt = 0; tt < weapon.reloadTime * 0.45f; tt += Time.deltaTime)
                {
                    if (compRig != null) compRig.ReloadT = Mathf.Clamp01(tt / weapon.reloadTime);
                    if (mc.Player.rig != null) mc.Player.rig.ReloadT = weapon.ReloadProgress; // 배치 실행엔 키보드가 없어 PlayerController가 안 넘길 수 있다
                    yield return null;
                }
                Capture(Path.Combine(dir, "tps_reload.png"));
                pc.target = null;
                foreach (var (name, t) in people)
                {
                    var p = t.position; var tr = t.right; var tf = t.forward;
                    var pos = p + tr * 2.3f + tf * 0.4f + Vector3.up * 1.35f;
                    cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(p + Vector3.up * 1.2f + tf * 0.2f - pos));
                    if (mc.Player.rig != null) mc.Player.rig.ReloadT = weapon.ReloadProgress;
                    yield return Settle(cam, 2);
                    Capture(Path.Combine(dir, $"gun_{name}_reload_side.png"));
                }
                if (compRig != null) compRig.ReloadT = -1f;
                if (mc.Player.rig != null) mc.Player.rig.ReloadT = -1f;
                for (float tt = 0; tt < weapon.reloadTime; tt += Time.deltaTime) yield return null;
            }
            pc.target = null;

            // 걷기: 둘을 앞으로 1.4m/s로 1.2초 옮기며 옆에서 (이동 부품은 잠시 끈다)
            CharacterRig.QaAim = false;
            var cc = mc.Player.GetComponent<CharacterController>();
            var agent = mc.Companion.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (cc != null) cc.enabled = false;
            if (agent != null) agent.enabled = false;
            for (float tt = 0; tt < 1.2f; tt += Time.deltaTime)
            {
                foreach (var (name, t) in people)
                {
                    t.rotation = Quaternion.LookRotation(fwd);
                    t.position = Ground(t.position + fwd * 1.4f * Time.deltaTime, 0.05f);
                }
                yield return null;
            }
            foreach (var (name, t) in people)
            {
                // 뿌리가 아니라 보이는 모델 위치를 겨눈다 (기사 컷이 화면 가장자리로 밀렸다)
                var rigW = t.GetComponent<CharacterRig>();
                for (int f = 0; f < 3; f++)
                {
                    t.position = Ground(t.position + fwd * 0.03f, 0.05f); // 배치 모드는 프레임이 길어 고정 보폭
                    yield return null;
                }
                // 프레임을 넘기는 동안 카메라 스크립트가 카메라를 옮기므로 찍기 직전에 놓는다
                // 실제로 보이는 몸(스킨 메시 범위)을 겨눈다. 뿌리·애니메이터 위치를 겨누면 기사 컷이 비었다
                var p = t.position;
                if (rigW != null && rigW.animator != null)
                {
                    var smr = rigW.animator.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null) { p = smr.bounds.center; p.y = smr.bounds.min.y; }
                }
                Debug.Log($"[GUN-POSE] walk {name} root={t.position} body={p}");
                var pos = p + t.right * 2.4f + t.forward * 0.6f + Vector3.up * 1.3f;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(p + Vector3.up * 1.1f - pos));
                Capture(Path.Combine(dir, $"gun_{name}_walk_side.png"));
            }
            if (cc != null) cc.enabled = true;
            if (agent != null) agent.enabled = true;
            CharacterRig.QaAim = null;
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

        static IEnumerator Settle(Camera cam, int frames)
        {
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            for (int i = 0; i < frames; i++)
            {
                cam.Render();
                yield return null;
            }
            cam.targetTexture = null;
            Object.Destroy(rt);
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
