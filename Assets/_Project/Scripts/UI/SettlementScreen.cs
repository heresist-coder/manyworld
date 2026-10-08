using UnityEngine;

namespace Manyworld
{
    /// <summary>정산서. "이번 콜, 남는 장사였나?"</summary>
    public class SettlementScreen : MonoBehaviour
    {
        void OnGUI()
        {
            var gm = GameManager.Instance;
            var s = gm?.LastSettlement;
            if (s == null) return;
            UIStyle.BeginScaled();
            float W = UIStyle.ScaledWidth;

            UIStyle.Fill(new Rect(0, 0, W, 1080), new Color(0, 0, 0, 0.45f));
            var paper = new Rect(W / 2 - 520, 50, 640, 980);
            UIStyle.Fill(paper, UIStyle.Paper);
            var ink = UIStyle.With(UIStyle.Label, UIStyle.Ink, 18);
            var inkR = UIStyle.With(ink, UIStyle.Ink, 18, TextAnchor.UpperRight);

            GUI.Label(new Rect(paper.x + 30, paper.y + 24, 580, 44), "정 산 서", UIStyle.With(UIStyle.Title, UIStyle.Ink, 32, TextAnchor.UpperCenter));
            string outcome = s.outcome switch
            {
                CallOutcome.Success => "콜 성공",
                CallOutcome.CompanionSolo => "콜 성공 (동료 단독 완수)",
                CallOutcome.Withdrawn => "철수",
                _ => "전멸 — 디멘션 뷰로 회수",
            };
            int t = Mathf.FloorToInt(s.elapsed);
            GUI.Label(new Rect(paper.x + 30, paper.y + 76, 580, 26),
                $"{s.contract.spec.Title} · {s.contract.task} · {outcome} · {t / 60}분 {t % 60}초", UIStyle.With(UIStyle.Small, UIStyle.Ink, 16, TextAnchor.UpperCenter));
            UIStyle.Fill(new Rect(paper.x + 30, paper.y + 108, 580, 2), UIStyle.Ink);

            float y = paper.y + 120;
            foreach (var line in s.lines)
            {
                var parts = line.Split('|');
                GUI.Label(new Rect(paper.x + 40, y, 400, 26), parts[0], ink);
                if (parts.Length > 1) GUI.Label(new Rect(paper.x + 40, y, 560, 26), parts[1], inkR);
                y += 28;
            }
            UIStyle.Fill(new Rect(paper.x + 30, y + 6, 580, 2), UIStyle.Ink);
            y += 16;
            var netColor = s.net >= 0 ? new Color(0.1f, 0.4f, 0.15f) : new Color(0.65f, 0.1f, 0.1f);
            GUI.Label(new Rect(paper.x + 40, y, 300, 36), "순수익", UIStyle.With(UIStyle.Title, UIStyle.Ink, 24));
            GUI.Label(new Rect(paper.x + 40, y, 560, 36), $"{(s.net >= 0 ? "+" : "")}{s.net:N0}", UIStyle.With(UIStyle.Title, netColor, 26, TextAnchor.UpperRight));
            y += 44;

            string record = s.outcome == CallOutcome.Wiped ? "무사고 경력: 전멸 기록 (큰 감점)"
                : s.playerKnockouts + s.companionKnockouts > 0 ? $"무사고 경력: 기절 {s.playerKnockouts + s.companionKnockouts}회 감점"
                : s.outcome == CallOutcome.Success ? "무사고 경력: +1건" : "무사고 경력: 변동 없음";
            GUI.Label(new Rect(paper.x + 40, y, 560, 26), record, ink);
            GUI.Label(new Rect(paper.x + 40, y + 28, 560, 26), $"잔고 {UIStyle.Won(gm.Save.money)}", ink);
            y += 66;

            if (s.playerKnockouts > 0)
            {
                int auto = 0;
                for (int i = gm.Save.cases.Count - s.newCases; i < gm.Save.cases.Count; i++)
                    if (i >= 0 && gm.Save.cases[i].autoSolved) auto++;
                string msg = auto == s.newCases && s.newCases > 0
                    ? "사인: 기억은 없지만, 몸에 남은 흔적이 낯익다. (사인 도감과 일치)"
                    : "사인: <b>불명</b> — 쓰러지기 직전 몇 분이 기억나지 않는다.\n→ 기사식당 '사인 분석'에서 흔적과 은주의 테이프를 맞춰 보자.";
                GUI.Label(new Rect(paper.x + 40, y, 560, 50), msg, UIStyle.With(UIStyle.Small, new Color(0.5f, 0.1f, 0.1f), 16));
                y += 52;
            }

            if (s.shotCost > s.income * 0.3f)
                GUI.Label(new Rect(paper.x + 40, y, 560, 30), "차은주: \"저기… 미터기가 많이 올라갔어요.\"", UIStyle.With(UIStyle.Small, UIStyle.Ink, 16));

            // 오른쪽: 뷰로 고시문
            var notice = new Rect(paper.xMax + 20, 50, 380, 420);
            UIStyle.Fill(notice, new Color(0.96f, 0.95f, 0.9f));
            UIStyle.Fill(new Rect(notice.x, notice.y, notice.width, 6), new Color(0.2f, 0.2f, 0.3f));
            GUI.Label(new Rect(notice.x + 16, notice.y + 16, notice.width - 32, notice.height - 32), s.notice ?? "",
                UIStyle.With(UIStyle.Small, UIStyle.Ink, 15));

            if (GUI.Button(new Rect(paper.x + 180, paper.yMax - 70, 280, 48), "기사식당으로", UIStyle.Button))
            {
                gm.EnterHub();
            }
        }
    }
}
