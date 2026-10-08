using UnityEngine;
using UnityEngine.InputSystem;

namespace Manyworld
{
    /// <summary>
    /// 출동: 기사식당 앞에서 티코를 몰고 게이트 공터까지 (brainstorm-01 2장 "[출동] 차로 현장까지 운전").
    /// 라디오는 보도 통제된 9시 뉴스, 은주는 조수석에서 워크맨.
    /// 게이트 앞에서 곡을 끝까지 듣게 해 주면 은주가 집중 상태로 들어간다.
    /// </summary>
    public class DriveSession : MonoBehaviour
    {
        enum Phase { Driving, Arrived, Waiting, Entering }

        public CallContract Contract { get; private set; }
        public TicoCar Car { get; private set; }
        public float DistanceToGate => Vector3.Distance(Flat(Car.transform.position), Flat(gatePos));

        Phase phase;
        Vector3 gatePos;
        Transform cam;
        Vector3 camVel;
        float phaseTime, radioTimer;
        int radioIndex;
        bool focused;
        public string RadioLine { get; private set; }
        public float Fade { get; private set; }

        static readonly string[] Radio =
        {
            "♪ 라디오: \"…어제 ○○동 일대에서 발생한 지반 침하는 노후 가스관 누출에 의한 것으로…\"",
            "♪ 라디오: \"…올림픽을 앞두고 정부는 도시 환경 정비 사업을 앞당기기로…\"",
            "♪ 라디오: \"…지반 연구 시설 주변은 통행을 삼가 주시기 바랍니다…\"",
            "♪ 라디오: \"오늘 날씨입니다. 오후 한때 서쪽 관문 쪽에 짙은 안개…\"",
            "은주: (이어폰 한쪽을 낀 채 창밖을 본다)",
            "♪ 라디오: \"…공사 측은 이번 사고와 관련해 '확인된 피해는 없다'고 밝혔습니다…\"",
        };

        public static DriveSession Begin(CallContract contract, MissionLayout home, Transform camera)
        {
            var go = new GameObject("Drive");
            var d = go.AddComponent<DriveSession>();
            d.Contract = contract;
            d.cam = camera;
            d.gatePos = home.gatePos;

            // 기사식당 앞 도로에서 출발
            Vector3 start = home.landmarks.TryGetValue("기사식당", out var diner) ? TownBuilder.NearestRoadPoint(diner) : new Vector3(30, 0, 30);
            // 도로를 따라 게이트가 있는 서쪽을 본다
            var heading = new Vector3(home.gatePos.x < start.x ? -1f : 1f, 0, 0);
            d.Car = TicoCar.Create(go.transform, start, Quaternion.LookRotation(heading), true);
            d.cam.position = start - heading * 8f + Vector3.up * 4f;

            Sfx.Play("car_door", 0.8f);
            Sfx.Play("radio_call", 0.6f);
            d.RadioLine = $"무전: \"{contract.spec.Title} 콜 확인. 서남쪽 게이트 공터로 가세요.\"";
            d.radioTimer = 5f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return d;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        void Update()
        {
            var kb = Keyboard.current;
            phaseTime += Time.deltaTime;

            radioTimer -= Time.deltaTime;
            if (radioTimer <= 0f && phase == Phase.Driving)
            {
                radioTimer = 6f;
                RadioLine = Radio[radioIndex++ % Radio.Length];
            }

            if (kb != null && kb.escapeKey.wasPressedThisFrame && phase == Phase.Driving)
            {
                GameManager.Instance.CancelDrive();
                return;
            }

            switch (phase)
            {
                case Phase.Driving:
                    // Tab: 운전 건너뛰기 (테스트용)
                    if (kb != null && kb.tabKey.wasPressedThisFrame)
                    {
                        Car.transform.position = gatePos + new Vector3(8, 0.1f, 6);
                        Car.Stop();
                    }
                    if (DistanceToGate < 9f) SetPhase(Phase.Arrived);
                    break;

                case Phase.Arrived:
                    Car.Stop();
                    Car.controllable = false;
                    if (kb == null) break;
                    if (kb.eKey.wasPressedThisFrame) Enter(false);
                    else if (kb.qKey.wasPressedThisFrame)
                    {
                        SetPhase(Phase.Waiting);
                        RadioLine = "은주: \"잠깐만요. 이 곡만 다 듣고 내릴게요.\"";
                    }
                    break;

                case Phase.Waiting:
                    if (phaseTime > 7f) Enter(true);
                    break;

                case Phase.Entering:
                    Fade = Mathf.Clamp01(phaseTime / 1.2f);
                    if (phaseTime > 1.3f) GameManager.Instance.StartCall(Contract, focused);
                    break;
            }
        }

        public void Enter(bool withFocus)
        {
            focused = withFocus;
            if (withFocus) RadioLine = "은주: (이어폰을 빼며) \"…됐어요. 가요, 기사님.\"";
            SetPhase(Phase.Entering);
            Sfx.Play("car_door", 0.8f);
            Sfx.Play("gate_whoosh", 0.9f);
        }

        void SetPhase(Phase p)
        {
            phase = p;
            phaseTime = 0f;
        }

        void LateUpdate()
        {
            if (Car == null || cam == null) return;
            var t = Car.transform;
            var want = t.position - t.forward * 7.5f + Vector3.up * 3.2f;
            cam.position = Vector3.SmoothDamp(cam.position, want, ref camVel, 0.25f);
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(t.position + Vector3.up * 1.2f + t.forward * 3f - cam.position), Time.deltaTime * 8f);
        }

        void OnGUI()
        {
            UIStyle.BeginScaled();
            float W = UIStyle.ScaledWidth;
            const float H = 1080f;

            // 계기판
            var dash = new Rect(24, H - 150, 330, 126);
            UIStyle.Fill(dash, new Color(0.05f, 0.05f, 0.04f, 0.85f));
            GUI.Label(new Rect(dash.x + 16, dash.y + 10, 300, 50), $"{Car.SpeedKmh:0} <size=18>km/h</size>", UIStyle.With(UIStyle.Mono, UIStyle.Amber, 40, TextAnchor.UpperLeft));
            GUI.Label(new Rect(dash.x + 16, dash.y + 64, 300, 24), $"게이트까지 {DistanceToGate:0}m · {Contract.spec.Title}", UIStyle.Small);
            GUI.Label(new Rect(dash.x + 16, dash.y + 90, 310, 24), "WASD 운전 · Space 핸드브레이크 · Tab 건너뛰기 · Esc 취소", UIStyle.With(UIStyle.Small, new Color(1, 1, 1, 0.6f), 13));

            if (!string.IsNullOrEmpty(RadioLine))
            {
                var r = new Rect(W / 2 - 520, H - 110, 1040, 44);
                UIStyle.Fill(r, new Color(0, 0, 0, 0.55f));
                GUI.Label(r, RadioLine, UIStyle.With(UIStyle.Label, Color.white, 19, TextAnchor.MiddleCenter));
            }

            if (phase == Phase.Arrived)
            {
                var r = new Rect(W / 2 - 360, H / 2 - 80, 720, 130);
                UIStyle.Fill(r, new Color(0, 0, 0, 0.7f));
                GUI.Label(new Rect(r.x, r.y + 14, r.width, 34), "게이트 앞. 은주는 아직 이어폰을 끼고 있다.", UIStyle.With(UIStyle.Label, Color.white, 21, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(r.x, r.y + 60, r.width, 30), "[E] 바로 들어간다      [Q] 곡이 끝날 때까지 기다려 준다 (은주 집중)", UIStyle.With(UIStyle.Label, UIStyle.Amber, 19, TextAnchor.MiddleCenter));
            }
            if (phase == Phase.Waiting)
            {
                GUI.Label(new Rect(0, H / 2 - 40, W, 40), "은주가 눈을 감고 이어폰을 누른다… 워크맨 소리가 희미하게 샌다.",
                    UIStyle.With(UIStyle.Label, Color.white, 20, TextAnchor.MiddleCenter));
                UIStyle.Fill(new Rect(W / 2 - 150, H / 2 + 10, 300, 6), new Color(0, 0, 0, 0.5f));
                UIStyle.Fill(new Rect(W / 2 - 150, H / 2 + 10, 300 * Mathf.Clamp01(phaseTime / 7f), 6), UIStyle.Amber);
            }
            if (Fade > 0f) UIStyle.Fill(new Rect(0, 0, W, H), new Color(1f, 1f, 1f, Fade));
        }
    }
}
