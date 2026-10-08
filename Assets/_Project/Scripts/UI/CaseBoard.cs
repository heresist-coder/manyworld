using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 기사식당의 "사인 분석"과 "사인 도감".
    /// 모르고 죽음 → 단서(몸의 흔적, 은주의 마이마이 테이프) → 깨달음 (brainstorm-01 11장 확인 질문).
    /// 사인을 맞히면 회수 확인서의 검은 줄이 걷히듯 도감 칸이 드러난다 (brainstorm-03 5장).
    /// </summary>
    public class CaseBoard
    {
        int selectedCase = -1;
        Vector2 listScroll;

        // 카세트 재생
        KnockoutCase playing;
        float playTime;
        AudioSource hiss;

        // 검은 줄 걷히는 연출
        string revealCause;
        float revealTime;
        string feedback;

        const float LineInterval = 1.7f;

        public bool PlayingTape => playing != null;

        public void DrawCases(Rect r, SaveData save, Transform audioParent)
        {
            UIStyle.Fill(r, new Color(0.08f, 0.08f, 0.1f, 0.9f));
            if (save.cases.Count == 0)
            {
                GUI.Label(new Rect(r.x + 20, r.y + 20, r.width - 40, 120),
                    "아직 기절한 적이 없다.\n\n쓰러지면 직전 몇 분이 기억나지 않는다. 정산서엔 '사인 불명'이 찍힌다.\n몸에 남은 흔적과 은주의 마이마이 녹음으로 무슨 일이 있었는지 맞혀 보자.",
                    UIStyle.Label);
                return;
            }

            // 왼쪽: 사건 목록
            var list = new Rect(r.x + 10, r.y + 10, 250, r.height - 20);
            float contentH = save.cases.Count * 64;
            listScroll = GUI.BeginScrollView(list, listScroll, new Rect(0, 0, list.width - 20, contentH));
            float y = 0;
            for (int i = save.cases.Count - 1; i >= 0; i--)
            {
                var c = save.cases[i];
                if (selectedCase < 0 && !c.solved) selectedCase = i;
                var d = new System.DateTime(1987, 4, 1).AddDays(c.day);
                string status = c.solved ? (c.autoSolved ? "아는 사인" : "해결") : "<color=#ff8866>미해결</color>";
                bool sel = i == selectedCase;
                if (GUI.Button(new Rect(0, y, list.width - 24, 58), "", UIStyle.Button)) { selectedCase = i; feedback = null; }
                if (sel) UIStyle.Fill(new Rect(0, y, 4, 58), UIStyle.Amber);
                GUI.Label(new Rect(12, y + 6, list.width - 40, 24), $"사건 #{c.id} · 제{c.worldNumber}번", UIStyle.With(UIStyle.Label, Color.white, 17));
                GUI.Label(new Rect(12, y + 30, list.width - 40, 22), $"{d.Month}월 {d.Day}일 · {status}", UIStyle.Small);
                y += 64;
            }
            GUI.EndScrollView();

            if (selectedCase < 0 || selectedCase >= save.cases.Count) selectedCase = save.cases.Count - 1;
            DrawCaseDetail(new Rect(r.x + 275, r.y + 10, r.width - 285, r.height - 20), save.cases[selectedCase], save, audioParent);
        }

        void DrawCaseDetail(Rect r, KnockoutCase c, SaveData save, Transform audioParent)
        {
            var spec = WorldGenerator.Generate(c.worldNumber);
            GUI.Label(new Rect(r.x, r.y, r.width, 34), $"사건 #{c.id} — {spec.Title} · {spec.nickname}", UIStyle.With(UIStyle.Title, Color.white, 22));
            DrawRedactedCause(new Rect(r.x, r.y + 38, r.width, 28), c);

            float y = r.y + 80;
            GUI.Label(new Rect(r.x, y, r.width, 24), "깨어났을 때 남아 있던 것", UIStyle.With(UIStyle.Small, UIStyle.Amber, 16));
            y += 26;
            foreach (var t in c.traces)
            {
                GUI.Label(new Rect(r.x + 10, y, r.width - 10, 44), "· " + t, UIStyle.With(UIStyle.Label, Color.white, 17));
                y += 30;
            }

            y += 10;
            if (c.hasTape)
            {
                string label = c.tapeHeard ? "▶ 은주의 테이프 다시 듣기" : "▶ 은주의 마이마이 테이프 듣기";
                if (GUI.Button(new Rect(r.x, y, 360, 40), label, UIStyle.Button)) StartTape(c, audioParent);
                if (!c.tapeHeard) GUI.Label(new Rect(r.x + 376, y + 9, r.width - 376, 26), "녹음 버튼이 눌려 있었다.", UIStyle.With(UIStyle.Small, new Color(1, 1, 1, 0.6f), 15));
            }
            else GUI.Label(new Rect(r.x, y + 8, r.width, 26), "녹음 없음 — 그때 은주도 먼저 쓰러져 있었다.", UIStyle.With(UIStyle.Small, new Color(1, 1, 1, 0.6f), 16));
            y += 56;

            if (c.solved)
            {
                var cause = DeathCauses.Get(c.causeId);
                GUI.Label(new Rect(r.x, y, r.width, 80), $"<b>사인 도감 #{cause.number:00} {cause.title}</b>\n대응: {cause.counter}", UIStyle.With(UIStyle.Label, UIStyle.Green, 17));
                return;
            }

            GUI.Label(new Rect(r.x, y, r.width, 24), "무슨 일이 있었을까? (사인 추정)", UIStyle.With(UIStyle.Small, UIStyle.Amber, 16));
            y += 28;
            foreach (var id in c.choices)
            {
                var cause = DeathCauses.Get(id);
                bool wrong = c.wrong.Contains(id);
                GUI.enabled = !wrong;
                string text = wrong ? $"<color=#888888><s>{cause.title}</s></color>" : cause.title;
                if (GUI.Button(new Rect(r.x, y, 360, 38), text, new GUIStyle(UIStyle.Button) { richText = true }))
                    Guess(c, id, save);
                GUI.enabled = true;
                y += 44;
            }
            if (feedback != null) GUI.Label(new Rect(r.x + 380, y - 120, r.width - 380, 80), feedback, UIStyle.With(UIStyle.Label, new Color(1f, 0.7f, 0.6f), 17));
        }

        void DrawRedactedCause(Rect r, KnockoutCase c)
        {
            GUI.Label(new Rect(r.x, r.y, 60, r.height), "사인:", UIStyle.With(UIStyle.Label, Color.white, 18));
            var bar = new Rect(r.x + 60, r.y + 3, 300, r.height - 6);
            if (c.solved)
            {
                var cause = DeathCauses.Get(c.causeId);
                GUI.Label(new Rect(bar.x, r.y, r.width - 60, r.height), cause.title, UIStyle.With(UIStyle.Label, UIStyle.Green, 18));
                // 걷히는 중인 검은 줄
                if (revealCause == c.causeId)
                {
                    float t = Mathf.Clamp01((Time.unscaledTime - revealTime) / 1.2f);
                    UIStyle.Fill(new Rect(bar.x + bar.width * t, bar.y, bar.width * (1 - t), bar.height), Color.black);
                }
            }
            else UIStyle.Fill(bar, Color.black);
        }

        void Guess(KnockoutCase c, string id, SaveData save)
        {
            if (id == c.causeId)
            {
                c.solved = true;
                bool fresh = !save.HasCodex(id);
                save.UnlockCodex(id, c.worldNumber);
                // 같은 사인의 다른 미해결 사건도 풀린다
                foreach (var other in save.cases)
                    if (!other.solved && other.causeId == id) { other.solved = true; other.autoSolved = true; }
                revealCause = id;
                revealTime = Time.unscaledTime;
                feedback = null;
                Sfx.Play("stamp", 1f, 0f);
                if (fresh) Sfx.Play("typewriter", 0.5f, 0f);
                save.Save();
            }
            else
            {
                c.wrong.Add(id);
                feedback = c.hasTape && !c.tapeHeard ? "…아닌 것 같다. 테이프를 들어 보자." : "…아닌 것 같다. 흔적을 다시 보자.";
                Sfx.Play("meter_tick", 0.6f, 0f);
                save.Save();
            }
        }

        public void StartTape(KnockoutCase c, Transform audioParent)
        {
            StopTape();
            playing = c;
            playTime = 0f;
            Sfx.Play("cassette_play", 0.8f, 0f);
            hiss = Sfx.Loop("tape_hiss", audioParent, 0.6f, false);
        }

        public void StopTape()
        {
            if (hiss != null) Object.Destroy(hiss.gameObject);
            hiss = null;
            if (playing != null)
            {
                playing.tapeHeard = true;
                GameManager.Instance?.Save.Save();
            }
            playing = null;
        }

        /// <summary>카세트 플레이어 모달. 줄이 하나씩 흘러나온다.</summary>
        public void DrawTape(float W)
        {
            if (playing == null) return;
            playTime += Time.unscaledDeltaTime;
            UIStyle.Fill(new Rect(0, 0, W, 1080), new Color(0, 0, 0, 0.75f));

            var deck = new Rect(W / 2 - 460, 120, 920, 760);
            UIStyle.Fill(deck, new Color(0.16f, 0.15f, 0.14f));
            GUI.Label(new Rect(deck.x + 24, deck.y + 16, 600, 34), "마이마이 · 라벨: \"출동 녹음\" (은주 글씨)", UIStyle.With(UIStyle.Label, new Color(0.9f, 0.85f, 0.7f), 19));

            // 카세트와 돌아가는 릴
            var tape = new Rect(deck.x + 260, deck.y + 64, 400, 150);
            UIStyle.Fill(tape, new Color(0.08f, 0.08f, 0.08f));
            UIStyle.Fill(new Rect(tape.x + 20, tape.y + 14, tape.width - 40, 40), new Color(0.92f, 0.88f, 0.75f));
            GUI.Label(new Rect(tape.x + 30, tape.y + 18, tape.width - 60, 34), $"제{playing.worldNumber}번 · 사건 #{playing.id}", UIStyle.With(UIStyle.Label, UIStyle.Ink, 18, TextAnchor.MiddleCenter));
            Reel(new Vector2(tape.x + 110, tape.y + 105));
            Reel(new Vector2(tape.xMax - 110, tape.y + 105));

            int shown = Mathf.Min(playing.tape.Count, Mathf.FloorToInt(playTime / LineInterval) + 1);
            float y = deck.y + 240;
            for (int i = 0; i < shown; i++)
            {
                float a = Mathf.Clamp01((playTime - i * LineInterval) * 2f);
                // 잡음 때문에 일부 글자가 뭉개진다
                GUI.Label(new Rect(deck.x + 40, y, deck.width - 80, 40), playing.tape[i], UIStyle.With(UIStyle.Label, new Color(1, 1, 1, a), 20));
                y += 44;
            }

            bool done = playTime > playing.tape.Count * LineInterval + 0.8f;
            if (done && hiss != null) { Object.Destroy(hiss.gameObject); hiss = null; Sfx.Play("cassette_play", 0.6f, 0f); }
            if (GUI.Button(new Rect(deck.x + deck.width / 2 - 120, deck.yMax - 70, 240, 46), done ? "■ 테이프 꺼내기" : "■ 정지", UIStyle.Button))
                StopTape();
        }

        void Reel(Vector2 c)
        {
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(playTime * 220f, c * (Screen.height / 1080f));
            UIStyle.Fill(new Rect(c.x - 28, c.y - 28, 56, 56), new Color(0.85f, 0.85f, 0.85f));
            UIStyle.Fill(new Rect(c.x - 6, c.y - 26, 12, 52), new Color(0.1f, 0.1f, 0.1f));
            UIStyle.Fill(new Rect(c.x - 26, c.y - 6, 52, 12), new Color(0.1f, 0.1f, 0.1f));
            GUI.matrix = old;
        }

        public void DrawCodex(Rect r, SaveData save)
        {
            UIStyle.Fill(r, new Color(0.88f, 0.85f, 0.76f, 0.95f));
            GUI.Label(new Rect(r.x + 20, r.y + 12, r.width - 40, 34), $"사인 도감 {save.codex.Count}/{DeathCauses.All.Length}", UIStyle.With(UIStyle.Title, UIStyle.Ink, 24));
            float cardW = (r.width - 60) / 2f, cardH = 190;
            for (int i = 0; i < DeathCauses.All.Length; i++)
            {
                var cause = DeathCauses.All[i];
                var card = new Rect(r.x + 20 + (i % 2) * (cardW + 20), r.y + 60 + (i / 2) * (cardH + 14), cardW, cardH);
                UIStyle.Fill(card, new Color(1f, 0.99f, 0.95f));
                var ink = UIStyle.With(UIStyle.Small, UIStyle.Ink, 15);
                GUI.Label(new Rect(card.x + 14, card.y + 10, 120, 26), $"사인 #{cause.number:00}", UIStyle.With(UIStyle.Label, UIStyle.Ink, 18));
                CodexEntry entry = null;
                foreach (var e in save.codex) if (e.causeId == cause.id) entry = e;
                if (entry == null)
                {
                    UIStyle.Fill(new Rect(card.x + 130, card.y + 14, 220, 20), Color.black);
                    UIStyle.Fill(new Rect(card.x + 14, card.y + 50, card.width - 60, 16), Color.black);
                    UIStyle.Fill(new Rect(card.x + 14, card.y + 76, card.width - 120, 16), Color.black);
                    GUI.Label(new Rect(card.x + 14, card.y + 140, card.width - 28, 40), "사인 불명. 기절 사건을 분석하면 검은 줄이 걷힌다.", UIStyle.With(ink, new Color(0.3f, 0.3f, 0.3f), 14));
                    continue;
                }
                GUI.Label(new Rect(card.x + 130, card.y + 10, card.width - 140, 26), cause.title, UIStyle.With(UIStyle.Label, new Color(0.55f, 0.1f, 0.1f), 18));
                string worlds = string.Join(", ", entry.worlds.ConvertAll(n => $"제{n}번"));
                GUI.Label(new Rect(card.x + 14, card.y + 42, card.width - 28, card.height - 50),
                    $"확인된 세계: {worlds}\n대응: {cause.counter}\n<b>해금:</b> {cause.unlockEffect}", ink);
            }
        }
    }
}
