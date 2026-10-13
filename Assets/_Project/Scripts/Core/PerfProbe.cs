using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 성능 측정 모드 (빌드 실행 인자 -mw-perf). 숲 현장의 목표 샷 세 구도(01 흙길, 02 숲길, 03 방)에서
    /// 모두 켬 → 간접광 끔 → 연무 끔 → 회화풍 필터 끔 → 풀 끔 순서로 각 4초씩 프레임 시간을 재고
    /// 실행 파일 옆 mw-perf.txt에 평균·1% 최악 프레임(ms)을 적은 뒤 종료한다.
    /// </summary>
    public class PerfProbe : MonoBehaviour
    {
        public static bool Requested => System.Environment.GetCommandLineArgs().Contains("-mw-perf");

        // 안전장치: 무엇에 막히든 4분 남짓 뒤에는 지금까지의 기록을 남기고 끝낸다
        readonly StringBuilder report = new StringBuilder();
        void Update()
        {
            if (Time.realtimeSinceStartup > 260f) { Write(report.AppendLine("시간 초과로 중단")); Application.Quit(); enabled = false; }
            var mc = MissionController.Instance;
            if (mc != null && mc.Paused) mc.SetPaused(false);
        }

        IEnumerator Start()
        {
            Debug.Log("[Perf] 측정 시작");
            Application.runInBackground = true; // 창이 포커스를 잃어도 멈추지 않게 (첫 측정이 여기서 멈췄다)
            report.AppendLine($"Manyworld perf {System.DateTime.Now:yyyy-MM-dd HH:mm} | {SystemInfo.graphicsDeviceName} | {Screen.width}x{Screen.height} | vSync {QualitySettings.vSyncCount}");
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            MissionHUD.ShowArrival = false;
            yield return new WaitForSecondsRealtime(4f); // 생성·셰이더 준비 (도착 화면이 시간을 멈추므로 실제 시간으로)
            Time.timeScale = 1f;
            var mc = MissionController.Instance;
            var cam = Camera.main;
            var pc = cam != null ? cam.GetComponent<PlayerCamera>() : null;
            if (mc == null || cam == null || mc.Layout == null) { Write(report.AppendLine("숲 현장 없음")); Application.Quit(); yield break; }
            if (pc != null) pc.target = null;
            var terrain = mc.Layout.terrain;
            Vector3 Ground(Vector3 p, float up) { p.y = terrain.SampleHeight(p) + up; return p; }
            var car = mc.Car.transform;
            var room = mc.Room.transform;
            var th = ForestSiteBuilder.Trailhead;
            var onTrail = Vector2.Lerp(th, Vector2.Lerp(th, ForestSiteBuilder.RoomEntrance, 0.5f) + new Vector2(-6f, 2f), 0.45f);
            var ent = ForestSiteBuilder.RoomEntrance;
            var shots = new (string name, Vector3 pos, Vector3 look)[]
            {
                ("t01_road", Ground(car.position - car.forward * 6.5f + car.right * 3.8f, 3.3f), car.position + car.forward * 40f - car.right * 6f + Vector3.up),
                ("t02_path", Ground(new Vector3(onTrail.x, 0, onTrail.y), 2.4f), Ground(new Vector3(ent.x, 0, ent.y), 1.5f)),
                ("t03_room", Ground(room.position - room.forward * (ForestSiteBuilder.RoomHalf + 7f) + room.right * 1.6f, 2.3f), room.position + Vector3.up * 1.4f),
            };
            var fog = mc.transform.Find("VolumetricFog");
            var grass = FindObjectsByType<GrassField>(FindObjectsSortMode.None);
            var trees = FindObjectsByType<TreeField>(FindObjectsSortMode.None);
            var gm = GameManager.Instance;
            float billboard0 = terrain.treeBillboardDistance;
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            float shadow0 = urp != null ? urp.shadowDistance : 0f;
            void SetShadow(float d) { if (urp != null) urp.shadowDistance = d; }
            var steps = new (string name, System.Action off, System.Action on)[]
            {
                ("모두 켬", () => { }, () => { }),
                ("간접광 끔", () => { if (ScreenSpaceGI.Enabled) ScreenSpaceGI.Toggle(); }, () => { if (!ScreenSpaceGI.Enabled) ScreenSpaceGI.Toggle(); }),
                ("연무 끔", () => { if (fog != null) fog.gameObject.SetActive(false); }, () => { if (fog != null) fog.gameObject.SetActive(true); }),
                ("필터 끔", () => { GameManager.PaintFilter = false; gm.ApplyPaint(); }, () => { GameManager.PaintFilter = true; gm.ApplyPaint(); }),
                ("풀 끔", () => { foreach (var g in grass) g.enabled = false; }, () => { foreach (var g in grass) g.enabled = true; }),
                ("먼 나무 빌보드 300m", () => terrain.treeBillboardDistance = 300f, () => terrain.treeBillboardDistance = billboard0),
                ("그림자 거리 200m", () => SetShadow(200f), () => SetShadow(shadow0)),
                ("나무 끔", () => { foreach (var t in trees) t.enabled = false; }, () => { foreach (var t in trees) t.enabled = true; }),
            };
            foreach (var shot in shots)
            {
                cam.transform.SetPositionAndRotation(shot.pos, Quaternion.LookRotation(shot.look - shot.pos));
                foreach (var step in steps)
                {
                    step.off();
                    yield return new WaitForSecondsRealtime(1f);
                    var times = new List<float>();
                    float end = Time.realtimeSinceStartup + 4f;
                    while (Time.realtimeSinceStartup < end)
                    {
                        if (pc != null) pc.target = null;
                        cam.transform.SetPositionAndRotation(shot.pos, Quaternion.LookRotation(shot.look - shot.pos));
                        yield return null;
                        times.Add(Time.unscaledDeltaTime * 1000f);
                    }
                    if (step.name.Contains("빌보드") || step.name == "모두 켬")
                    {
                        // 빌보드가 예전처럼 하얗게 뜨는지 눈으로 보려고 같은 구도를 남긴다
                        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", $"mw-perf-{shot.name}-{(step.name == "모두 켬" ? "base" : "billboard")}.png"));
                        yield return null;
                        yield return null;
                    }
                    step.on();
                    times.Sort();
                    float avg = times.Average();
                    float worst = times.Skip((int)(times.Count * 0.99f)).DefaultIfEmpty(times.Last()).Average();
                    Debug.Log($"[Perf] {shot.name} {step.name} {avg:F2}ms");
                    report.AppendLine($"{shot.name}\t{step.name}\tavg {avg:F2}ms ({1000f / avg:F0} fps)\t1% {worst:F2}ms\tframes {times.Count}");
                }
            }
            Write(report);
            Application.Quit();
        }

        static void Write(StringBuilder sb)
        {
            var path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "mw-perf.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[Perf]\n" + sb);
        }
    }
}
