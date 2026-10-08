using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Manyworld
{
    /// <summary>
    /// 우리 동네 운전 (brainstorm-01 2장 기본 루프).
    /// · 출동: 콜을 받은 뒤 기사식당 앞에서 게이트 공터까지. 게이트 앞에서 곡을 끝까지 듣게 해 주면 은주가 집중 상태.
    /// · 동네 일: 배달 일감으로 푼돈을 벌다가 "띠리릭─ 다세계 콜입니다"가 오면 받는다. 망설이면 다른 기사가 채 간다 (5장 콜 경쟁).
    /// 라디오는 보도 통제된 9시 뉴스, 은주는 조수석에서 워크맨. 단골이 되면 은주가 말을 건다.
    /// </summary>
    public class DriveSession : MonoBehaviour
    {
        enum Phase { Driving, Arrived, Waiting, Entering }

        public CallContract Contract { get; private set; }
        public TicoCar Car { get; private set; }
        public bool Daily => Contract == null;
        public float DistanceToGate => Vector3.Distance(Flat(Car.transform.position), Flat(gatePos));

        Phase phase;
        Vector3 gatePos;
        MissionLayout home;
        Transform cam;
        Vector3 camVel;
        float phaseTime, radioTimer;
        int radioIndex;
        bool focused;
        public string RadioLine { get; private set; }
        public float Fade { get; private set; }

        // 동네 일
        public class Job
        {
            public string title, fromName, toName, pickupLine, dropLine;
            public Vector3 from, to;
            public int pay;
            public bool picked;
            public bool suspicious;
        }
        public Job CurrentJob { get; private set; }
        public CallContract Offer { get; private set; }
        public float OfferTime { get; private set; }
        public string Notice { get; private set; }
        float noticeTime, nextOfferIn, nextJobIn;
        System.Random rng;
        const float OfferWindow = 9f;
        public const int JobsPerDay = 6;

        static readonly string[] Radio =
        {
            "♪ 라디오: \"…어제 ○○동 일대에서 발생한 지반 침하는 노후 가스관 누출에 의한 것으로…\"",
            "♪ 라디오: \"…올림픽을 앞두고 정부는 도시 환경 정비 사업을 앞당기기로…\"",
            "♪ 라디오: \"…지반 연구 시설 주변은 통행을 삼가 주시기 바랍니다…\"",
            "♪ 라디오: \"오늘 날씨입니다. 오후 한때 서쪽 관문 쪽에 짙은 안개…\"",
            "은주: (이어폰 한쪽을 낀 채 창밖을 본다)",
            "♪ 라디오: \"…공사 측은 이번 사고와 관련해 '확인된 피해는 없다'고 밝혔습니다…\"",
        };

        struct JobTemplate
        {
            public string title, from, pickup, drop;
            public int pay;
            public bool suspicious;
        }

        static readonly JobTemplate[] Templates =
        {
            new JobTemplate { title = "연탄 배달", from = "연탄", pay = 4000, pickup = "연탄 스무 장을 실었다. 트렁크가 꽉 찼다.", drop = "\"아이고, 기사 양반 고마워요. 올겨울은 따뜻하겠네.\"" },
            new JobTemplate { title = "약 심부름", from = "약국", pay = 3500, pickup = "감기약 봉투를 받았다.", drop = "\"애가 열이 펄펄 끓어서… 고마워요.\"" },
            new JobTemplate { title = "우유 배달", from = "슈퍼", pay = 3000, pickup = "우유 한 상자를 실었다.", drop = "\"내일도 부탁해요.\"" },
            new JobTemplate { title = "백반 배달", from = "기사식당", pay = 4500, pickup = "철가방에 백반 두 상을 실었다.", drop = "\"국 안 식었네. 운전 잘하시네.\"" },
            new JobTemplate { title = "수상한 소포", from = "복덕방", pay = 7000, suspicious = true, pickup = "끈으로 꽁꽁 묶인 소포. 안에서 뭔가 째깍거린다.", drop = "받는 사람은 말없이 소포를 받고 문을 닫았다." },
            new JobTemplate { title = "작은 이삿짐", from = "동사무소", pay = 5500, pickup = "이불 보따리와 라디오를 실었다.", drop = "\"새 동네는 조용하다던데… 고마워요.\"" },
        };

        public static DriveSession Begin(CallContract contract, MissionLayout home, Transform camera)
        {
            var d = Create(home, camera);
            d.Contract = contract;
            Sfx.Play("radio_call", 0.6f);
            d.RadioLine = $"무전: \"{contract.spec.Title} 콜 확인. 서남쪽 게이트 공터로 가세요.\"";
            return d;
        }

        /// <summary>동네 일 나가기: 배달하다가 무전 콜이 오면 받는다.</summary>
        public static DriveSession BeginDaily(MissionLayout home, Transform camera)
        {
            var d = Create(home, camera);
            var save = GameManager.Instance.Save;
            if (save.dailyDay != save.day)
            {
                save.dailyDay = save.day;
                save.dailyEarned = 0;
                save.dailyJobs = 0;
            }
            d.rng = new System.Random(save.day * 37 + save.dailyJobs * 11 + 5);
            d.nextOfferIn = 30f + (float)d.rng.NextDouble() * 20f;
            d.nextJobIn = 1.5f;
            d.RadioLine = "♪ 라디오: \"…오늘도 안전 운전 하십시오.\" (동네 일을 기다린다)";
            return d;
        }

        static DriveSession Create(MissionLayout home, Transform camera)
        {
            var go = new GameObject("Drive");
            var d = go.AddComponent<DriveSession>();
            d.home = home;
            d.cam = camera;
            d.gatePos = home.gatePos;

            // 기사식당 앞 도로에서 출발. 도로를 따라 게이트가 있는 서쪽을 본다
            Vector3 start = home.landmarks.TryGetValue("기사식당", out var diner) ? TownBuilder.NearestRoadPoint(diner) : new Vector3(30, 0, 30);
            var heading = new Vector3(home.gatePos.x < start.x ? -1f : 1f, 0, 0);
            d.Car = TicoCar.Create(go.transform, start, Quaternion.LookRotation(heading), true);
            d.cam.position = start - heading * 8f + Vector3.up * 4f;

            Sfx.Play("car_door", 0.8f);
            d.radioTimer = 6f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return d;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        void Toast(string text)
        {
            Notice = text;
            noticeTime = Time.time;
        }

        void Update()
        {
            var kb = Keyboard.current;
            phaseTime += Time.deltaTime;
            var save = GameManager.Instance.Save;

            radioTimer -= Time.deltaTime;
            if (radioTimer <= 0f && phase == Phase.Driving && Offer == null)
            {
                radioTimer = 7f;
                // 단골부터 은주가 말을 건다
                bool talk = Bond.Stage(save) >= 2 && radioIndex % 2 == 1;
                RadioLine = Bond.Speak(talk ? Bond.CarTalk[(radioIndex / 2) % Bond.CarTalk.Length] : Radio[radioIndex % Radio.Length]);
                radioIndex++;
            }

            if (kb != null && kb.escapeKey.wasPressedThisFrame && phase == Phase.Driving)
            {
                GameManager.Instance.CancelDrive();
                return;
            }

            if (Daily)
            {
                UpdateDaily(kb, save);
                return;
            }

            switch (phase)
            {
                case Phase.Driving:
                    // Tab: 운전 건너뛰기 (테스트용)
                    if (kb != null && kb.tabKey.wasPressedThisFrame)
                    {
                        Car.Teleport(gatePos + new Vector3(8, 0.3f, 6));
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
                        RadioLine = Bond.Stage(save) >= 3
                            ? "은주가 이어폰 한쪽을 건넨다. \"…같이 들어요. 금방 끝나요.\""
                            : "은주: \"잠깐만요. 이 곡만 다 듣고 내릴게요.\"";
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

        // ---------------- 동네 일 ----------------

        void UpdateDaily(Keyboard kb, SaveData save)
        {
            // 일감
            if (CurrentJob == null)
            {
                nextJobIn -= Time.deltaTime;
                if (nextJobIn <= 0f && save.dailyJobs < JobsPerDay) CurrentJob = MakeJob();
            }
            else
            {
                var target = CurrentJob.picked ? CurrentJob.to : CurrentJob.from;
                if (Vector3.Distance(Flat(Car.transform.position), Flat(target)) < 7f && Car.SpeedKmh < 12f)
                {
                    Sfx.Play("car_door", 0.7f);
                    if (!CurrentJob.picked)
                    {
                        CurrentJob.picked = true;
                        Toast($"{CurrentJob.fromName}: {CurrentJob.pickupLine}");
                    }
                    else CompleteJob(save);
                }
            }

            // 무전 콜
            if (Offer == null)
            {
                nextOfferIn -= Time.deltaTime;
                if (nextOfferIn <= 0f) MakeOffer(save);
            }
            else
            {
                OfferTime += Time.deltaTime;
                if (kb != null && kb.yKey.wasPressedThisFrame) AcceptOffer(Offer);
                else if (kb != null && kb.nKey.wasPressedThisFrame) DropOffer("무전: \"네, 다른 기사님께 넘깁니다.\"");
                else if (OfferTime > OfferWindow) DropOffer($"{OtherDriver()}: \"제가 받겠습니다!\" — 콜을 놓쳤다");
            }
        }

        string OtherDriver()
        {
            var names = new[] { "김 기사", "박 기사", "최 기사", "노 기사" };
            return names[rng.Next(names.Length)];
        }

        Job MakeJob()
        {
            var names = new List<string>(home.landmarks.Keys);
            if (names.Count < 2) return null;
            var choices = new List<JobTemplate>();
            foreach (var t in Templates) if (home.landmarks.ContainsKey(t.from)) choices.Add(t);
            if (choices.Count == 0) choices.AddRange(Templates);
            var tpl = choices[rng.Next(choices.Count)];
            string from = home.landmarks.ContainsKey(tpl.from) ? tpl.from : names[rng.Next(names.Count)];
            string to;
            do { to = names[rng.Next(names.Count)]; } while (to == from);
            var job = new Job
            {
                title = tpl.title, fromName = from, toName = to, pay = tpl.pay, suspicious = tpl.suspicious,
                pickupLine = tpl.pickup, dropLine = tpl.drop,
                from = TownBuilder.NearestRoadPoint(home.landmarks[from]),
                to = TownBuilder.NearestRoadPoint(home.landmarks[to]),
            };
            Sfx.Play("radio_call", 0.35f);
            Toast($"일감: {job.title} — {from} → {to} ({UIStyle.Won(job.pay)})");
            return job;
        }

        void CompleteJob(SaveData save)
        {
            var job = CurrentJob;
            CurrentJob = null;
            save.money += job.pay;
            save.dailyEarned += job.pay;
            save.dailyJobs++;
            Sfx.Play("meter_tick", 0.6f, 0f);
            string line = $"{job.toName}: {job.dropLine} (+{UIStyle.Won(job.pay)})";

            // 배달 손님이 흘리는 소문 (신뢰도 반반). 수상한 소포는 더 자주
            if (rng.NextDouble() < (job.suspicious ? 0.8 : 0.35))
            {
                int world = 1 + rng.Next(999);
                var r = InfoExchange.Make(save, "배달 손님", 0.5f, world, rng, 900000 + save.day * 50 + save.dailyJobs);
                if (r != null)
                {
                    r.owned = true;
                    save.rumors.Add(r);
                    line += $"  ·  소문: \"{r.text}\"";
                }
            }
            // 같이 일하면 조금씩 가까워진다
            var note = Bond.Add(save, 1);
            if (note != null) line = note;
            save.Save();
            Toast(line);
            nextJobIn = save.dailyJobs >= JobsPerDay ? 999f : 3f + (float)rng.NextDouble() * 4f;
            if (save.dailyJobs >= JobsPerDay) RadioLine = "오늘 동네 일은 다 했다. 무전 콜을 기다리거나 Esc로 들어가자.";
        }

        void MakeOffer(SaveData save)
        {
            int world = rng.NextDouble() < 0.6 ? 1 + rng.Next(150) : 1 + rng.Next(999);
            var c = CallContract.Create(world, rng);
            // 해골 콜은 자격이 있어야 들어온다
            if (c.grade == CallGrade.Skull && save.cleanStreak < 2) c = CallContract.Create(1 + rng.Next(99), rng);
            Offer = c;
            OfferTime = 0f;
            Sfx.Play("radio_call", 0.8f);
            RadioLine = "띠리릭─ 다세계 콜입니다";
        }

        void AcceptOffer(CallContract c)
        {
            Offer = null;
            if (CurrentJob != null) Toast($"{CurrentJob.title}은(는) 다음에. 콜이 먼저다.");
            CurrentJob = null;
            Contract = c;
            Sfx.Play("radio_call", 0.6f);
            RadioLine = $"나: \"제가 받겠습니다.\" — 무전: \"{c.spec.Title} 콜 확인. 서남쪽 게이트 공터로 가세요.\"";
        }

        void DropOffer(string why)
        {
            Offer = null;
            Toast(why);
            nextOfferIn = 35f + (float)rng.NextDouble() * 25f;
        }

        /// <summary>테스트용: 지금 들어온 콜을 받는다.</summary>
        public void AcceptCurrentOffer()
        {
            if (Offer != null) AcceptOffer(Offer);
        }

        /// <summary>테스트용: 콜을 바로 띄운다.</summary>
        public void ForceOffer() => MakeOffer(GameManager.Instance.Save);

        // ---------------- 게이트 ----------------

        public void Enter(bool withFocus)
        {
            focused = withFocus;
            var save = GameManager.Instance.Save;
            if (withFocus)
            {
                RadioLine = Bond.Speak("은주: (이어폰을 빼며) \"…됐어요. 가요, 기사님.\"");
                // 곡을 끝까지 듣게 해 주면 조금 가까워진다
                var note = Bond.Add(save, 2);
                if (note != null) Toast(note);
                save.Save();
            }
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

        static string Arrow(Vector3 from, Vector3 fwd, Vector3 to)
        {
            var d = to - from;
            float ang = Vector3.SignedAngle(new Vector3(fwd.x, 0, fwd.z), new Vector3(d.x, 0, d.z), Vector3.up);
            string[] arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
            return arrows[Mathf.RoundToInt(Mathf.Repeat(ang, 360f) / 45f) % 8];
        }

        void OnGUI()
        {
            UIStyle.BeginScaled();
            float W = UIStyle.ScaledWidth;
            const float H = 1080f;
            var save = GameManager.Instance.Save;

            // 계기판
            var dash = new Rect(24, H - 150, 360, 126);
            UIStyle.Fill(dash, new Color(0.05f, 0.05f, 0.04f, 0.85f));
            GUI.Label(new Rect(dash.x + 16, dash.y + 10, 300, 50), $"{Car.SpeedKmh:0} <size=18>km/h</size>", UIStyle.With(UIStyle.Mono, UIStyle.Amber, 40, TextAnchor.UpperLeft));
            string where = Daily ? $"오늘 동네 일 {save.dailyJobs}/{JobsPerDay}건 · {UIStyle.Won(save.dailyEarned)}" : $"게이트까지 {DistanceToGate:0}m · {Contract.spec.Title}";
            GUI.Label(new Rect(dash.x + 16, dash.y + 64, 330, 24), where, UIStyle.Small);
            GUI.Label(new Rect(dash.x + 16, dash.y + 90, 340, 24), Daily ? "WASD 운전 · Space 핸드브레이크 · Esc 기사식당으로" : "WASD 운전 · Space 핸드브레이크 · Tab 건너뛰기 · Esc 취소",
                UIStyle.With(UIStyle.Small, new Color(1, 1, 1, 0.6f), 13));

            // 일감 / 목적지
            string goal = null;
            Vector3 goalPos = Vector3.zero;
            if (!Daily) { goal = "게이트 공터"; goalPos = gatePos; }
            else if (CurrentJob != null)
            {
                goal = CurrentJob.picked ? $"{CurrentJob.title}: {CurrentJob.toName}에 내려 주기" : $"{CurrentJob.title}: {CurrentJob.fromName}에서 싣기";
                goalPos = CurrentJob.picked ? CurrentJob.to : CurrentJob.from;
            }
            if (goal != null)
            {
                var r = new Rect(W / 2 - 360, 24, 720, 64);
                UIStyle.Fill(r, new Color(0, 0, 0, 0.45f));
                GUI.Label(new Rect(r.x, r.y + 6, r.width, 30), goal + (Daily ? $"  ({UIStyle.Won(CurrentJob.pay)})" : ""), UIStyle.With(UIStyle.Label, Color.white, 20, TextAnchor.MiddleCenter));
                float dist = Vector3.Distance(Flat(Car.transform.position), Flat(goalPos));
                GUI.Label(new Rect(r.x, r.y + 36, r.width, 24), $"{Arrow(Car.transform.position, Car.transform.forward, goalPos)}  {dist:0}m",
                    UIStyle.With(UIStyle.Small, UIStyle.Amber, 16, TextAnchor.MiddleCenter));
            }

            // 무전 콜 카드
            if (Offer != null)
            {
                var c = Offer;
                var r = new Rect(W - 520, 120, 490, 210);
                UIStyle.Fill(r, new Color(0.35f, 0.06f, 0.05f, 0.92f));
                GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 32, 30), "띠리릭─ 다세계 콜입니다", UIStyle.With(UIStyle.Label, Color.white, 22));
                string stars = new string('★', c.spec.estimatedDanger) + new string('☆', 5 - c.spec.estimatedDanger);
                GUI.Label(new Rect(r.x + 16, r.y + 46, r.width - 32, 28), $"{(c.grade == CallGrade.Skull ? "☠ " : "")}{c.spec.Title} · 공사 추정 {stars}", UIStyle.With(UIStyle.Label, Color.white, 19));
                GUI.Label(new Rect(r.x + 16, r.y + 76, r.width - 32, 24), $"{c.task} · 총 {UIStyle.Won(c.Total)} (중도금 {c.MidRatio * 100:0}%)", UIStyle.Small);
                GUI.Label(new Rect(r.x + 16, r.y + 102, r.width - 32, 24), RumorHint(save, c.worldNumber), UIStyle.With(UIStyle.Small, new Color(0.75f, 0.9f, 1f), 14));
                GUI.Label(new Rect(r.x + 16, r.y + 140, r.width - 32, 28), "[Y] \"제가 받겠습니다\"    [N] 넘긴다", UIStyle.With(UIStyle.Label, UIStyle.Amber, 19));
                float left = Mathf.Clamp01(1f - OfferTime / OfferWindow);
                UIStyle.Fill(new Rect(r.x + 16, r.y + 180, r.width - 32, 8), new Color(0, 0, 0, 0.5f));
                UIStyle.Fill(new Rect(r.x + 16, r.y + 180, (r.width - 32) * left, 8), left > 0.33f ? UIStyle.Amber : UIStyle.Red);
            }

            if (!string.IsNullOrEmpty(RadioLine))
            {
                var r = new Rect(W / 2 - 560, H - 110, 1120, 44);
                UIStyle.Fill(r, new Color(0, 0, 0, 0.55f));
                GUI.Label(r, RadioLine, UIStyle.With(UIStyle.Label, Color.white, 18, TextAnchor.MiddleCenter));
            }
            if (Notice != null && Time.time - noticeTime < 6f)
            {
                var r = new Rect(W / 2 - 560, H - 160, 1120, 40);
                GUI.Label(r, Notice, UIStyle.With(UIStyle.Label, new Color(1, 1, 1, Mathf.Clamp01(6f - (Time.time - noticeTime))), 18, TextAnchor.MiddleCenter));
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

        /// <summary>그 세계에 대해 들은 소문 한 줄.</summary>
        public static string RumorHint(SaveData save, int world)
        {
            foreach (var r in save.rumors)
                if (r.world == world && r.owned)
                    return $"소문 ({r.source}{(r.verified == 1 ? ", 맞았음" : r.verified == -1 ? ", 틀렸음" : ", 미확인")}): {r.text}";
            var ws = save.GetWorld(world);
            return ws == null ? "처음 듣는 번호" : $"세계 수첩: 제{ws.generation}세대 · 방문 {ws.visits}회";
        }
    }
}
