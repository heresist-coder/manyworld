using System.Collections.Generic;
using UnityEngine;

namespace Manyworld
{
    /// <summary>기사식당. 무전 콜 보드, 장부, 세계 수첩.</summary>
    public class HubScreen : MonoBehaviour
    {
        List<CallContract> calls;
        int callsDay = -1;
        string customNumber = "47";
        Vector2 notebookScroll;
        string flavor;
        int tab;                         // 0 무전 콜, 1 사인 분석, 2 사인 도감
        readonly CaseBoard caseBoard = new CaseBoard();

        public int Tab { get => tab; set => tab = value; }
        public void PlayTape(KnockoutCase c) => caseBoard.StartTape(c, transform);
        public void StopTape() => caseBoard.StopTape();

        static readonly string[] Rumors =
        {
            "\"12번대 세계에선 총 쏘지 말래. 김 기사가 그러던데.\"",
            "\"공사 추정치 믿지 마. 위험도 2라더니 덩치가 셋이었어.\"",
            "\"중도금 비율 높은 콜은 다 이유가 있는 거야.\"",
            "\"이번엔 누가 갔대? …박 기사? 아이고.\"",
            "\"뷰로 3번 창구 계장, 점심시간엔 서류 안 받는다더라.\"",
            "\"머리만 쏘지 마. 다음에 가면 머리가 단단해져 있어.\"",
        };

        public void Invalidate()
        {
            callsDay = -1;
        }

        void EnsureCalls(SaveData save)
        {
            if (callsDay == save.day && calls != null) return;
            callsDay = save.day;
            var rng = new System.Random(save.day * 7331 + 11);
            calls = new List<CallContract>
            {
                CallContract.Create(rng.Next(1, 100), rng),
                CallContract.Create(rng.Next(100, 500), rng),
                CallContract.Create(rng.Next(1, 1000), rng),
            };
            flavor = Rumors[rng.Next(Rumors.Length)];
        }

        static bool Qualified(SaveData save, CallContract c) =>
            c.grade != CallGrade.Skull || save.cleanStreak >= 2;

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var save = gm.Save;
            EnsureCalls(save);
            UIStyle.BeginScaled();
            float W = UIStyle.ScaledWidth;

            // 장부
            var left = new Rect(30, 30, 420, 420);
            UIStyle.Fill(left, new Color(0.1f, 0.08f, 0.06f, 0.88f));
            GUI.Label(new Rect(left.x + 20, left.y + 16, 380, 40), "기사식당", UIStyle.Title);
            var d = save.Date;
            string[] dow = { "일", "월", "화", "수", "목", "금", "토" };
            GUI.Label(new Rect(left.x + 20, left.y + 62, 380, 26), $"{d.Year}년 {d.Month}월 {d.Day}일 ({dow[(int)d.DayOfWeek]})", UIStyle.Label);
            GUI.Label(new Rect(left.x + 20, left.y + 98, 380, 30), $"잔고  <b>{UIStyle.Won(save.money)}</b>", UIStyle.With(UIStyle.Label, UIStyle.Amber, 24));
            GUI.Label(new Rect(left.x + 20, left.y + 140, 380, 120),
                $"무사고 경력\n· 연속 무사고 {save.cleanStreak}건\n· 기절 {save.knockouts}회 (감점)\n· 전멸 {save.wipes}회 (큰 감점)\n· 경력 점수 {save.RecordScore}",
                UIStyle.Label);
            GUI.Label(new Rect(left.x + 20, left.y + 280, 380, 26), "동료: 차은주 (지분 25%)", UIStyle.Label);
            GUI.Label(new Rect(left.x + 20, left.y + 310, 380, 60), $"<i>{flavor}</i>", UIStyle.With(UIStyle.Small, new Color(1, 1, 1, 0.7f), 15));
            if (GUI.Button(new Rect(left.x + 20, left.y + 370, 180, 36), "다음 날로", UIStyle.Button))
            {
                save.day++;
                save.Save();
            }
            if (GUI.Button(new Rect(left.x + 220, left.y + 370, 180, 36), "세이브 초기화", UIStyle.Button)) gm.ResetSave();

            VolumeUI.Draw(new Rect(left.x, left.y + 440, 420, 30));

            float cx = 480, cw = Mathf.Min(760, W - cx - 470);
            int unsolved = save.UnsolvedCases;
            string[] tabs = { "무전 콜", unsolved > 0 ? $"사인 분석 ({unsolved})" : "사인 분석", $"사인 도감 {save.codex.Count}/{DeathCauses.All.Length}" };
            float tw = cw / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                var tr = new Rect(cx + i * tw, 30, tw - 6, 40);
                UIStyle.Fill(tr, i == tab ? new Color(0.6f, 0.1f, 0.08f, 0.95f) : new Color(0.1f, 0.1f, 0.1f, 0.8f));
                if (GUI.Button(tr, "", GUIStyle.none)) { tab = i; Sfx.Play("meter_tick", 0.4f, 0.05f); }
                GUI.Label(tr, tabs[i], UIStyle.With(UIStyle.Label, i == 1 && unsolved > 0 ? UIStyle.Amber : Color.white, 20, TextAnchor.MiddleCenter));
            }

            if (tab == 1)
            {
                caseBoard.DrawCases(new Rect(cx, 80, cw, 900), save, transform);
                DrawNotebook(new Rect(W - 440, 30, 410, 1020), save);
                caseBoard.DrawTape(W);
                return;
            }
            if (tab == 2)
            {
                caseBoard.DrawCodex(new Rect(cx, 80, cw, 680), save);
                DrawNotebook(new Rect(W - 440, 30, 410, 1020), save);
                return;
            }

            float y = 80;
            foreach (var c in calls)
            {
                DrawCall(new Rect(cx, y, cw, 200), c, save, gm);
                y += 210;
            }

            // 테스트 출동
            var test = new Rect(cx, y, cw, 64);
            UIStyle.Fill(test, new Color(0, 0, 0, 0.6f));
            GUI.Label(new Rect(test.x + 16, test.y + 18, 260, 28), "테스트: 세계 번호 직접 입력", UIStyle.Small);
            customNumber = GUI.TextField(new Rect(test.x + 250, test.y + 14, 90, 36), customNumber, 3, UIStyle.Button);
            if (GUI.Button(new Rect(test.x + 356, test.y + 14, 160, 36), "출동", UIStyle.Button) && int.TryParse(customNumber, out int n))
            {
                var rng = new System.Random(n * 3 + save.day);
                gm.StartCall(CallContract.Create(Mathf.Clamp(n, 1, 999), rng));
            }

            DrawNotebook(new Rect(W - 440, 30, 410, 1020), save);
        }

        void DrawCall(Rect r, CallContract c, SaveData save, GameManager gm)
        {
            bool skull = c.grade == CallGrade.Skull;
            UIStyle.Fill(r, skull ? new Color(0.18f, 0.05f, 0.05f, 0.9f) : new Color(0.08f, 0.08f, 0.1f, 0.88f));
            var s = c.spec;
            string stars = new string('★', s.estimatedDanger) + new string('☆', 5 - s.estimatedDanger);
            GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 32, 34),
                $"{(skull ? "☠ 해골 콜  " : "")}{s.Title}  <size=16>공사 추정 위험도 {stars}</size>", UIStyle.With(UIStyle.Label, skull ? new Color(1f, 0.6f, 0.5f) : Color.white, 24));
            GUI.Label(new Rect(r.x + 16, r.y + 46, r.width - 32, 24), $"의뢰: {c.client} · {c.task} · 규칙: {s.RuleText(true)}", UIStyle.Small);

            var ws = save.GetWorld(c.worldNumber);
            string notebook = ws == null ? "세계 수첩: 처음 가는 번호" :
                $"세계 수첩: 제{ws.generation}세대 · 방문 {ws.visits}회 · 확인된 규칙: {new WorldSpec { rules = (WorldRule)ws.knownRules }.RuleText(false)}";
            GUI.Label(new Rect(r.x + 16, r.y + 72, r.width - 32, 24), notebook, UIStyle.With(UIStyle.Small, new Color(0.7f, 0.9f, 1f)));

            GUI.Label(new Rect(r.x + 16, r.y + 102, r.width - 32, 26),
                $"계약금 {UIStyle.Won(c.deposit)}  ·  중도금 {UIStyle.Won(c.midPayment)}  ·  잔금 {UIStyle.Won(c.balance)}", UIStyle.Label);
            string midHint = c.MidRatio > 0.42f ? "  ← 중도금 비율이 높다. 의뢰인이 뭔가 알고 있다" : "";
            GUI.Label(new Rect(r.x + 16, r.y + 130, r.width - 32, 22), $"총 {UIStyle.Won(c.Total)} · 중도금 비율 {c.MidRatio * 100:0}%{midHint}",
                UIStyle.With(UIStyle.Small, c.MidRatio > 0.42f ? UIStyle.Amber : new Color(1, 1, 1, 0.7f)));

            bool ok = Qualified(save, c);
            GUI.enabled = ok;
            if (GUI.Button(new Rect(r.xMax - 270, r.y + 152, 254, 38), ok ? "\"제가 받겠습니다\"" : "자격 미달 (연속 무사고 2건)", UIStyle.Button))
                gm.BeginDrive(c);
            GUI.enabled = true;
        }

        void DrawNotebook(Rect r, SaveData save)
        {
            UIStyle.Fill(r, new Color(0.88f, 0.85f, 0.76f, 0.94f));
            GUI.Label(new Rect(r.x + 16, r.y + 12, r.width - 32, 36), "세계 수첩 · 게시판", UIStyle.With(UIStyle.Title, UIStyle.Ink, 24));
            var inner = new Rect(r.x + 10, r.y + 56, r.width - 20, r.height - 66);
            var ink = UIStyle.With(UIStyle.Small, UIStyle.Ink, 15);
            float contentH = 40;
            foreach (var w in save.worlds) contentH += 230;
            notebookScroll = GUI.BeginScrollView(inner, notebookScroll, new Rect(0, 0, inner.width - 20, contentH));
            float y = 0;
            if (save.worlds.Count == 0)
                GUI.Label(new Rect(6, y, inner.width - 30, 60), "아직 다녀온 세계가 없다.\n게시판엔 뷰로 고시문이 붙는다.", ink);
            for (int i = save.worlds.Count - 1; i >= 0; i--)
            {
                var w = save.worlds[i];
                var spec = WorldGenerator.Generate(w.number);
                GUI.Label(new Rect(6, y, inner.width - 30, 24), $"<b>{spec.Title}</b> · {spec.nickname}", UIStyle.With(UIStyle.Label, UIStyle.Ink, 18));
                GUI.Label(new Rect(6, y + 24, inner.width - 30, 40),
                    $"갈라진 사건: {spec.divergence}\n확인된 규칙: {new WorldSpec { rules = (WorldRule)w.knownRules }.RuleText(false)}", ink);
                if (!string.IsNullOrEmpty(w.lastNotice))
                {
                    var box = new Rect(6, y + 70, inner.width - 30, 150);
                    UIStyle.Fill(box, new Color(1f, 0.99f, 0.95f));
                    GUI.Label(new Rect(box.x + 8, box.y + 6, box.width - 16, box.height - 12), w.lastNotice, UIStyle.With(UIStyle.Small, UIStyle.Ink, 13));
                }
                y += 230;
            }
            GUI.EndScrollView();
        }
    }
}
