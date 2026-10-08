using System;
using System.Collections.Generic;
using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// "왜 죽는지도 모르는 세계" (brainstorm-01 7장, brainstorm-02 2장).
    /// 기절하면 직전 기억이 날아간다. 몸에 남은 흔적과 은주의 마이마이 녹음으로 사인을 맞히면
    /// 사인 도감에 기록되고, 대응법(경고)이 해금된다.
    /// </summary>
    public class DeathCause
    {
        public string id;
        public int number;          // 사인 #01
        public string title;
        public string trace;        // 깨어났을 때 몸에 남은 흔적
        public string tapeLine;     // 녹음에 남은 결정적인 한 마디
        public string counter;      // 대응법
        public string unlockEffect; // 해금되는 경고
    }

    public static class DeathCauses
    {
        public static readonly DeathCause[] All =
        {
            new DeathCause
            {
                id = "behind", number = 1, title = "등 뒤 기습",
                trace = "등에 길게 긁힌 자국. 앞쪽은 멀쩡하다.",
                tapeLine = "은주: \"기사님, 뒤에─!\"",
                counter = "달리면 발소리로 뒤가 따라붙는다. 멈춰서 돌아봐라.",
                unlockEffect = "등 뒤에서 덤비는 개체가 있으면 화면 아래에 경고",
            },
            new DeathCause
            {
                id = "pack", number = 2, title = "기는 놈 무리에 포위",
                trace = "팔다리 여러 곳에 작은 물린 자국. 방향이 제각각이다.",
                tapeLine = "은주: \"하나, 둘… 옆에도요! 너무 많아요!\"",
                counter = "앞서 나가지 말 것. 은주에게 '엄호'를 주고 무리를 먼저 끊어라.",
                unlockEffect = "기는 놈 셋 이상이 몰려오면 포위 경고",
            },
            new DeathCause
            {
                id = "brute", number = 3, title = "덩치의 일격",
                trace = "가슴에 넓은 타박상. 갈비뼈가 욱신거린다. 한 방이었다.",
                tapeLine = "(쿵─) 은주: \"…기사님? 기사님!\"",
                counter = "덩치는 팔을 들었다가 내리친다. 머리가 붉어지면 물러서라.",
                unlockEffect = "덩치가 내리치기 직전 머리가 붉게 빛난다",
            },
            new DeathCause
            {
                id = "gaspocket", number = 4, title = "저지대 가스 고임",
                trace = "옷에 노란 얼룩. 목이 타고 기침이 멈추지 않는다. 필터는 멀쩡했다.",
                tapeLine = "은주: \"…콜록. 여기 공기가 달라요. 낮은 데로 가지 마세요.\"",
                counter = "필터로도 못 막는 가스 고임이 있다. 쉭쉭 소리 나는 곳에 들어가지 말 것.",
                unlockEffect = "가스 고임이 눈에 보이고, 가까이 가면 경고",
            },
            new DeathCause
            {
                id = "light", number = 5, title = "불빛을 보고 몰려옴",
                trace = "손전등이 켜진 채 발견됐다. 사방에서 몰려온 발자국.",
                tapeLine = "은주: \"기사님, 불… 불 끄세요! 다 보고 있어요.\"",
                counter = "영원한 밤에선 빛이 어그로다. 꼭 필요할 때만 켜라.",
                unlockEffect = "손전등을 켠 채 쫓기면 경고",
            },
            new DeathCause
            {
                id = "gunfire", number = 6, title = "총소리를 듣고 몰려옴",
                trace = "주변에 탄피가 잔뜩 흩어져 있었다. 마지막엔 사방에서 덮쳤다.",
                tapeLine = "(탕, 탕─) 은주: \"쏘지 마세요… 소리가 모여요.\"",
                counter = "소리 반응 세계에선 총소리가 미끼다. 쇠파이프나 은주의 한 발로.",
                unlockEffect = "소리 반응 세계에서 쏘면 반응한 개체 수를 알려 준다",
            },
        };

        public static DeathCause Get(string id)
        {
            foreach (var c in All) if (c.id == id) return c;
            return null;
        }

        /// <summary>기사가 쓰러진 순간의 상황으로 진짜 사인을 정한다. 플레이어에게는 보여 주지 않는다.</summary>
        public static string Diagnose(MissionController mc, DamageInfo info)
        {
            if (info.cause == "gaspocket") return "gaspocket";
            var player = mc.Player;
            var m = info.attacker;
            if (m != null && m.species == Species.Brute) return "brute";
            if (mc.Spec.rules.HasFlag(WorldRule.EternalNight) && player.FlashlightOn) return "light";
            if (mc.Spec.rules.HasFlag(WorldRule.SoundReactive) && Time.time - mc.LastPlayerShotTime < 15f) return "gunfire";

            int near = 0;
            foreach (var mon in mc.Monsters)
                if (mon != null && !mon.health.IsDown && mon.IsHunting && Vector3.Distance(mon.transform.position, player.transform.position) < 9f)
                    near++;
            if (near >= 3) return "pack";

            if (m != null)
            {
                var to = (m.transform.position - player.transform.position);
                to.y = 0;
                if (Vector3.Angle(player.transform.forward, to) > 110f) return "behind";
            }
            return near >= 2 ? "pack" : "behind";
        }

        /// <summary>틀린 보기 2개 + 정답. 그 세계에서 그럴듯한 것 위주.</summary>
        public static List<string> Choices(string trueId, WorldSpec spec, System.Random rng)
        {
            var pool = new List<string> { "behind", "pack", "brute" };
            if (spec.rules.HasFlag(WorldRule.ToxicGas)) pool.Add("gaspocket");
            if (spec.rules.HasFlag(WorldRule.EternalNight)) pool.Add("light");
            if (spec.rules.HasFlag(WorldRule.SoundReactive)) pool.Add("gunfire");
            pool.Remove(trueId);
            var result = new List<string> { trueId };
            while (result.Count < 3 && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                result.Add(pool[i]);
                pool.RemoveAt(i);
            }
            // 섞기
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }
    }

    /// <summary>기절 사건 한 건. 기사식당 "사인 분석"에서 푼다.</summary>
    [Serializable]
    public class KnockoutCase
    {
        public int id;
        public int worldNumber;
        public int day;
        public string causeId;              // 정답 (플레이어에겐 숨김)
        public List<string> traces = new List<string>();
        public bool hasTape;                // 은주가 녹음했나
        public List<string> tape = new List<string>();
        public bool tapeHeard;
        public List<string> choices = new List<string>();
        public List<string> wrong = new List<string>();
        public bool solved;
        public bool autoSolved;             // 이미 도감에 있는 사인이라 바로 풀림
    }

    [Serializable]
    public class CodexEntry
    {
        public string causeId;
        public int firstDay;
        public List<int> worlds = new List<int>();
    }
}
