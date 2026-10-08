using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 차은주와의 관계 (characters/cha-eunju.md "관계 성장").
    /// 1 동업자 "기사님" → 2 단골 (차 안에서 조금씩 말을 한다) → 3 파트너 (이어폰 한쪽, 조준을 봐 준다) → 4 비밀 (라벨 없는 테이프).
    /// 존댓말은 끝까지 유지한다.
    /// </summary>
    public static class Bond
    {
        static readonly int[] Thresholds = { 0, 10, 25, 45 };
        static readonly string[] Names = { "동업자", "단골", "파트너", "비밀" };

        public static int Stage(SaveData s)
        {
            int st = 1;
            for (int i = 0; i < Thresholds.Length; i++) if (s.bond >= Thresholds[i]) st = i + 1;
            return st;
        }

        public static string StageName(int stage) => Names[Mathf.Clamp(stage - 1, 0, Names.Length - 1)];

        /// <summary>다음 단계까지 남은 점수. 마지막 단계면 0.</summary>
        public static int ToNext(SaveData s)
        {
            int st = Stage(s);
            return st >= Thresholds.Length ? 0 : Thresholds[st] - s.bond;
        }

        /// <summary>은주가 기사를 부르는 말.</summary>
        public static string Address(SaveData s)
        {
            int st = Stage(s);
            return st <= 2 ? "기사님" : st == 3 ? $"{s.playerName} 씨" : s.playerName;
        }

        /// <summary>대사 속 "기사님"을 지금 관계의 호칭으로 바꾼다.</summary>
        public static string Speak(string line)
        {
            var gm = GameManager.Instance;
            if (gm == null || string.IsNullOrEmpty(line)) return line;
            var a = Address(gm.Save);
            return a == "기사님" ? line : line.Replace("기사님", a);
        }

        /// <summary>점수를 더하고, 단계가 오르면 알려 준다.</summary>
        public static string Add(SaveData s, int amount)
        {
            int before = Stage(s);
            s.bond = Mathf.Clamp(s.bond + amount, 0, 99);
            int after = Stage(s);
            if (after > before) return $"은주와의 관계: {StageName(after)} — {StageLine(after)}";
            return null;
        }

        static string StageLine(int stage) => stage switch
        {
            2 => "차 안에서 조금씩 말을 하기 시작한다.",
            3 => "이어폰 한쪽을 건넨다. \"숨 참지 마시고, 내쉬면서 당기세요.\"",
            4 => "라벨 없는 테이프를 꺼내 보인다. \"…같이 들어 주실래요?\"",
            _ => "",
        };

        /// <summary>단골부터: 운전 중 은주가 하는 말 (부산, 실업팀, 워크맨).</summary>
        public static readonly string[] CarTalk =
        {
            "은주: \"부산은요, 바다 냄새가 이 동네랑 달라요. …가끔 생각나요.\"",
            "은주: \"실업팀 땐 하루에 오백 발씩 쐈어요. 돈 걱정 없이요.\"",
            "은주: (테이프를 뒤집는다) \"이 곡, 코치님이 집중할 때 들으라고 했어요.\"",
            "은주: \"선발전 날 일은… 기억이 안 나요. 정말로요.\"",
            "은주: \"기사님은 왜 이 일 하세요? …아, 대답 안 하셔도 돼요.\"",
            "은주: \"미터기 소리, 이제 좀 익숙해졌어요. 딸깍.\"",
        };

        /// <summary>라벨 없는 테이프 (cha-eunju.md 스토리 떡밥). 4단계에서 같이 듣는다.</summary>
        public static readonly string[] UnlabeledTape =
        {
            "[A면] (발라드 전주. 1987년 봄 라디오에서 녹음한 듯한 잡음)",
            "[A면] (곡이 끝나기 전에 철컥─ 테이프를 뒤집는 소리)",
            "[B면] (탕. …탕. 사격장의 총성. 일정한 간격)",
            "[B면] (웅성거림) \"…선수, 차은주 선수 사대로…\"",
            "[B면] (총성이 멎는다. 아주 낮은 웅웅거림. 형광등 같은)",
            "[B면] (타닥… 타닥… 쾅. 타자기, 그리고 도장 소리)",
            "[B면] (낯선 목소리) \"…원적 세계 기재 인물과 동일인임을 확인함.\"",
            "(치직─ 테이프가 끝났다. 은주는 한참 말이 없다)",
        };
    }
}
