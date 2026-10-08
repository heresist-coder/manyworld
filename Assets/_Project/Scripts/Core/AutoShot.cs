using System.Collections;
using System.IO;
using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 개발용: 빌드를 "-mwshots 경로"로 실행하면 주요 화면을 돌며 스크린샷을 찍고 종료한다.
    /// 인자가 없으면 아무것도 하지 않는다.
    /// </summary>
    public class AutoShot : MonoBehaviour
    {
        string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-mwshots")
                {
                    var go = new GameObject("AutoShot");
                    go.AddComponent<AutoShot>().dir = args[i + 1];
                }
        }

        IEnumerator Start()
        {
            Application.runInBackground = true; // 창 포커스를 잃어도 계속 찍는다
            Directory.CreateDirectory(dir);
            SaveData.PathOverride = Path.Combine(dir, "shot_save.json");
            yield return null;
            var gm = GameManager.Instance;
            gm.ResetSave();
            var hub = gm.GetComponent<HubScreen>();

            // 보여 줄 데이터: 기절 사건 하나(테이프 포함), 도감 한 칸
            var kc = new KnockoutCase { id = 1, worldNumber = 47, day = 0, causeId = "behind", hasTape = true };
            kc.traces.Add(DeathCauses.Get("behind").trace);
            kc.traces.Add("쓰러진 곳: 문방구 자리 앞. 진입 2분 14초 뒤.");
            kc.tape.Add("[02:01] (탕)");
            kc.tape.Add("[02:05] (끼이익─ 하는 소리)");
            kc.tape.Add("[02:09] 은주: \"엄호할게요. 보이면 쏩니다.\"");
            kc.tape.Add("[02:14] " + DeathCauses.Get("behind").tapeLine);
            kc.tape.Add("[02:16] 은주: \"기사님? …기사님!\"");
            kc.tape.Add("(치직─ 녹음 끝)");
            kc.choices.AddRange(new[] { "pack", "behind", "gunfire" });
            gm.Save.cases.Add(kc);
            gm.Save.UnlockCodex("brute", 12);
            gm.EnterHub();

            yield return Shot("1_hub_calls");
            hub.Tab = 1;
            yield return Shot("2_hub_cases");
            hub.PlayTape(kc);
            yield return new WaitForSeconds(5f);
            yield return Shot("3_cassette");
            hub.StopTape();
            hub.Tab = 2;
            yield return Shot("4_codex");
            hub.Tab = 3;
            yield return Shot("4b_info");
            hub.Tab = 0;

            gm.BeginDaily();
            yield return new WaitForSeconds(3f);
            yield return Shot("5a_daily_job");
            var daily = Object.FindFirstObjectByType<DriveSession>();
            daily.ForceOffer();
            yield return new WaitForSeconds(1f);
            yield return Shot("5b_radio_call");
            gm.CancelDrive();
            yield return null;

            gm.BeginDrive(CallContract.Create(47, new System.Random(1)));
            yield return new WaitForSeconds(2.5f);
            yield return Shot("5_drive");

            gm.StartCall(CallContract.Create(47, new System.Random(3)));
            yield return new WaitForSeconds(3f);
            yield return Shot("6a_mission_day");
            var m47 = MissionController.Instance;
            m47.Player.cam.aiming = true;
            yield return new WaitForSeconds(0.5f);
            m47.Player.cam.yaw += 140f;
            yield return Shot("6b_mission_companion");
            m47.Player.cam.aiming = false;
            var pcc = m47.Player.GetComponent<CharacterController>();
            pcc.enabled = false;
            m47.Player.transform.position = m47.Car.transform.position + m47.Car.transform.right * 2f;
            pcc.enabled = true;
            m47.EnterCar();
            m47.Car.externalInput = new Vector2(0.15f, 1f);
            yield return new WaitForSeconds(3f);
            yield return Shot("6c_driving");
            m47.Car.externalInput = Vector2.zero;
            Object.Destroy(m47.gameObject);
            yield return null;
            gm.EnterHub();
            yield return null;

            gm.StartCall(CallContract.Create(200, new System.Random(2)));
            yield return new WaitForSeconds(2.5f);
            yield return Shot("6_mission_gas_night");

            var mc = MissionController.Instance;
            mc.Player.health.TakeDamage(new DamageInfo { amount = 999, source = DamageSource.Monster });
            yield return new WaitForSeconds(1f);
            yield return Shot("7_downed");
            mc.Companion.health.TakeDamage(new DamageInfo { amount = 999, source = DamageSource.Monster });
            yield return new WaitForSeconds(4.5f);
            yield return Shot("8_bureau");

            gm.ResetSave();
            Application.Quit();
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForSeconds(0.6f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return new WaitForSeconds(0.3f);
        }
    }
}
