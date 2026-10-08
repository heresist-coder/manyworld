using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 디멘션 뷰로 (brainstorm-03). 전멸하면 대기실에서 깨어난다.
    /// "여러 세계를 관리하는 동사무소" — 초월적인 존재인데 말투는 1987년 민원 창구.
    /// </summary>
    public class BureauScreen : MonoBehaviour
    {
        enum Step { WaitingRoom, Counter, Documents }

        Step step;
        Settlement s;
        int fee;
        string[] feeLines;
        float t;
        bool chimed;
        int stampCount;

        public void Begin(Settlement settlement)
        {
            s = settlement;
            step = Step.WaitingRoom;
            t = 0f;
            chimed = false;
            stampCount = 0;

            int n = s.contract.worldNumber;
            int perHead = 15000 + n * 30;
            int stamps = 1500 * 3;
            bool overtime = s.elapsed > s.contract.timeLimit;
            fee = perHead * 2 + stamps + (overtime ? 10000 : 0);
            feeLines = overtime
                ? new[] { $"뷰로 회수비 (2인)|-{perHead * 2:N0}", $"인지대 3장|-{stamps:N0}", "체류 초과 과태료|-10,000" }
                : new[] { $"뷰로 회수비 (2인)|-{perHead * 2:N0}", $"인지대 3장|-{stamps:N0}" };
        }

        void Update() => t += Time.deltaTime;

        void OnGUI()
        {
            if (s == null) return;
            UIStyle.BeginScaled();
            float W = UIStyle.ScaledWidth;
            const float H = 1080f;
            // 형광등 아래 연두빛 벽
            UIStyle.Fill(new Rect(0, 0, W, H), new Color(0.72f, 0.76f, 0.68f));
            UIStyle.Fill(new Rect(0, H * 0.62f, W, H * 0.38f), new Color(0.45f, 0.42f, 0.36f));
            UIStyle.Fill(new Rect(W / 2 - 300, 40, 600, 8), new Color(1f, 1f, 0.95f, 0.9f + Mathf.Sin(t * 37f) * 0.1f)); // 깜빡이는 형광등

            var ink = UIStyle.With(UIStyle.Label, UIStyle.Ink, 20);
            var gm = GameManager.Instance;
            int ticket = gm.Save.bureauTicket;

            switch (step)
            {
                case Step.WaitingRoom:
                {
                    GUI.Label(new Rect(80, 90, W - 160, 40), "디멘션 뷰로 · 민원 대기실", UIStyle.With(UIStyle.Title, UIStyle.Ink, 30));
                    GUI.Label(new Rect(80, 150, W - 160, 200),
                        "형광등이 웅웅거린다. 나무 장의자. 벽에 걸린 달력은 1987년.\n" +
                        "\"민원 처리 순서\" 안내판. 손에는 번호표가 쥐어져 있다.\n" +
                        "옆자리에서 차은주가 이어폰을 빼며 담담하게 말한다.", ink);
                    GUI.Label(new Rect(80, 290, W - 160, 40), "차은주: \"번호표 뽑으셨어요? 저 계장님, 점심시간엔 안 받아 주세요.\"",
                        UIStyle.With(UIStyle.Label, new Color(0.15f, 0.25f, 0.45f), 20));

                    var tk = new Rect(80, 360, 220, 140);
                    UIStyle.Fill(tk, UIStyle.Paper);
                    GUI.Label(new Rect(tk.x, tk.y + 14, tk.width, 30), "번 호 표", UIStyle.With(UIStyle.Small, UIStyle.Ink, 16, TextAnchor.UpperCenter));
                    GUI.Label(new Rect(tk.x, tk.y + 46, tk.width, 70), $"{ticket:0000}", UIStyle.With(UIStyle.Title, UIStyle.Ink, 54, TextAnchor.UpperCenter));

                    if (t > 1.5f)
                    {
                        if (!chimed) { chimed = true; Sfx.Play("chime", 0.8f, 0f); }
                        GUI.Label(new Rect(340, 400, W - 420, 60), $"띵동─  \"{ticket}번 민원인, 3번 창구로 오세요.\"", UIStyle.With(UIStyle.Title, new Color(0.6f, 0.1f, 0.1f), 30));
                        if (GUI.Button(new Rect(340, 470, 260, 50), "3번 창구로 간다", UIStyle.Button)) { step = Step.Counter; t = 0f; }
                    }
                    break;
                }
                case Step.Counter:
                {
                    GUI.Label(new Rect(80, 90, W - 160, 40), "3번 창구", UIStyle.With(UIStyle.Title, UIStyle.Ink, 30));
                    GUI.Label(new Rect(80, 150, W - 160, 60), "회색 점퍼, 돋보기안경, 결재판, 식은 커피믹스. 계장은 고개를 들지 않는다.", ink);
                    var ws = gm.Save.GetWorld(s.contract.worldNumber);
                    string gossip = ws != null && ws.visits >= 2
                        ? $"\"…{s.contract.worldNumber}번은 요즘 가지 마세요. 이번 달만 서류가 세 건째라.\""
                        : "\"다음.\"";
                    GUI.Label(new Rect(80, 230, W - 160, 200),
                        "계장: \"회수 확인서. 수수료 고지서. 동일인 확인서. 여기, 여기, 여기 도장.\"\n" +
                        "계장: \"현장 잔류 물품은 뷰로가 안 챙겨 드립니다. 직접 가서 주우세요.\"\n" +
                        $"계장: {gossip}", ink);
                    if (GUI.Button(new Rect(80, 440, 260, 50), "서류를 받는다", UIStyle.Button)) { step = Step.Documents; t = 0f; Sfx.Play("typewriter", 0.7f, 0f); }
                    break;
                }
                case Step.Documents:
                    DrawDocuments(W, gm);
                    break;
            }
        }

        void DrawDocuments(float W, GameManager gm)
        {
            // 쾅, 쾅, 쾅 — 서류마다 도장
            if (Event.current.type == EventType.Repaint && stampCount < 3 && t > 0.6f + stampCount * 0.45f)
            {
                stampCount++;
                Sfx.Play("stamp", 0.9f, 0.04f);
            }
            var ink = UIStyle.With(UIStyle.Small, UIStyle.Ink, 16);
            var d = gm.Save.Date;
            int n = s.contract.worldNumber;

            // 회수 확인서 — 일부 항목이 검은 줄로 가려져 있다
            var a = new Rect(60, 60, 560, 600);
            UIStyle.Fill(a, UIStyle.Paper);
            GUI.Label(new Rect(a.x, a.y + 20, a.width, 40), "회 수 확 인 서", UIStyle.With(UIStyle.Title, UIStyle.Ink, 28, TextAnchor.UpperCenter));
            float y = a.y + 90;
            void Field(string label, string value, bool redacted = false, float width = 220)
            {
                GUI.Label(new Rect(a.x + 30, y, 140, 26), label, ink);
                if (redacted) UIStyle.Fill(new Rect(a.x + 180, y + 3, width, 20), Color.black);
                else GUI.Label(new Rect(a.x + 180, y, a.width - 210, 26), value, ink);
                y += 40;
            }
            Field("회수 일시", $"{d.Year}. {d.Month:00}. {d.Day:00}.  ██:██");
            Field("세계 번호", $"제{n}번");
            Field("발견 위치", "", true, 300);
            Field("발견 상태", "", true, 260);
            Field("현장 잔류 물품", s.contract.task.Contains("회수") ? "카빈 탄피 다수, 측량 장비 일부" : "카빈 탄피 다수");
            var knownCase = LatestCase(gm);
            if (knownCase != null && gm.Save.HasCodex(knownCase.causeId)) Field("사인", DeathCauses.Get(knownCase.causeId).title + "  (도감 기록과 일치)");
            else Field("사인", "", true, 340);
            Field("비고", "", true, 180);
            GUI.Label(new Rect(a.x + 30, y + 10, a.width - 60, 60), "위 사람을 규정에 따라 원적 세계로 회수하였음을 확인함.", ink);
            Stamp(new Rect(a.xMax - 150, a.yMax - 140, 110, 110));

            // 수수료 고지서
            var b = new Rect(650, 60, 520, 420);
            UIStyle.Fill(b, new Color(0.95f, 0.92f, 0.86f));
            GUI.Label(new Rect(b.x, b.y + 20, b.width, 40), "수 수 료 고 지 서", UIStyle.With(UIStyle.Title, UIStyle.Ink, 26, TextAnchor.UpperCenter));
            y = b.y + 90;
            foreach (var line in feeLines)
            {
                var p = line.Split('|');
                GUI.Label(new Rect(b.x + 30, y, 300, 26), p[0], ink);
                GUI.Label(new Rect(b.x + 30, y, b.width - 60, 26), p[1].TrimStart('-'), UIStyle.With(ink, UIStyle.Ink, 16, TextAnchor.UpperRight));
                y += 34;
            }
            UIStyle.Fill(new Rect(b.x + 30, y + 4, b.width - 60, 2), UIStyle.Ink);
            GUI.Label(new Rect(b.x + 30, y + 14, 200, 30), "합계", UIStyle.With(UIStyle.Label, UIStyle.Ink, 20));
            GUI.Label(new Rect(b.x + 30, y + 14, b.width - 60, 30), $"{fee:N0}원", UIStyle.With(UIStyle.Label, UIStyle.Ink, 20, TextAnchor.UpperRight));
            GUI.Label(new Rect(b.x + 30, y + 56, b.width - 60, 60), "동료 몫 회수비도 고용주가 납부한다.\n장비 압류 없음. 현금 납부만 가능 (D10).", ink);

            // 동일인 확인서 — 아무 설명 없이 매번 나온다
            var c = new Rect(650, 500, 520, 160);
            UIStyle.Fill(c, new Color(0.97f, 0.97f, 0.95f));
            GUI.Label(new Rect(c.x, c.y + 14, c.width, 30), "동 일 인 확 인 서", UIStyle.With(UIStyle.Label, UIStyle.Ink, 20, TextAnchor.UpperCenter));
            GUI.Label(new Rect(c.x + 24, c.y + 54, c.width - 160, 80), "본 민원인은 원적 세계 기재 인물과\n동일인임을 확인함.", ink);
            Stamp(new Rect(c.xMax - 120, c.y + 40, 96, 96));

            if (GUI.Button(new Rect(60, 700, 380, 54), $"수수료 {fee:N0}원 납부하고 나간다", UIStyle.Button))
            {
                Sfx.Play("meter_tick", 0.6f, 0f);
                s = null;
                gm.FinishBureau(fee, feeLines);
            }
            GUI.Label(new Rect(470, 712, W - 520, 40), "뷰로 문을 나서면 기사식당 뒷골목이다.", UIStyle.With(UIStyle.Small, UIStyle.Ink, 16));
        }

        KnockoutCase LatestCase(GameManager gm)
        {
            var cases = gm.Save.cases;
            for (int i = cases.Count - 1; i >= 0; i--)
                if (cases[i].worldNumber == s.contract.worldNumber && cases[i].day == gm.Save.day - 1) return cases[i];
            return null;
        }

        static void Stamp(Rect r)
        {
            var red = new Color(0.78f, 0.12f, 0.1f, 0.85f);
            UIStyle.Fill(r, red);
            UIStyle.Fill(new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 10), new Color(0.95f, 0.92f, 0.86f, 0.9f));
            GUI.Label(r, "DIMENSION\nBUREAU\n次元管理局", UIStyle.With(UIStyle.Small, red, 13, TextAnchor.MiddleCenter));
        }
    }
}
