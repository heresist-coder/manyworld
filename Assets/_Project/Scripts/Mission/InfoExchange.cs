using System;
using System.Collections.Generic;
using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 기사식당 정보 교환 (brainstorm-01 7장 "정보 교환 (솔플 버전)").
    /// 정보에는 신뢰도가 있다: 헛소문, 낡은 정보(진화로 이미 바뀜), 일부러 틀린 정보(경쟁 기사의 함정).
    /// 직접 가서 확인하면 그 기사가 정직한지 드러난다. 내가 확인한 정보는 팔 수 있다.
    /// </summary>
    [Serializable]
    public class Rumor
    {
        public int id;
        public int world;
        public string kind;          // rule / danger / armor
        public int claimRules;       // rule: 주장하는 규칙 플래그
        public int claimValue;       // danger: 위험도, armor: 1 단단함 / 0 무름
        public string text;
        public string source;
        public int day;
        public int generationAtRumor;
        public int price;
        public bool owned;
        public int verified;         // 0 미확인, 1 맞음, -1 틀림
    }

    [Serializable]
    public class NpcDriver
    {
        public string name;
        public string look;          // 한 줄 인상
        public float honesty;        // 숨김. 참말을 할 확률
        public int confirmed;        // 맞은 소문 수 (플레이어가 확인)
        public int refuted;          // 틀린 소문 수
        public List<string> bought = new List<string>(); // 이미 사 간 내 정보
    }

    public static class InfoExchange
    {
        public const int SellPriceRule = 3000;
        public const int SellPriceCodex = 4000;

        public static void EnsureDrivers(SaveData s)
        {
            if (s.drivers.Count > 0) return;
            s.drivers.Add(new NpcDriver { name = "김 기사", look = "말수 적은 고참. 순직 기사 사진 앞에서 늘 묵념한다", honesty = 0.92f });
            s.drivers.Add(new NpcDriver { name = "박 기사", look = "입이 싸다. 들은 얘기를 반쯤 부풀린다", honesty = 0.62f });
            s.drivers.Add(new NpcDriver { name = "최 기사", look = "웃는 얼굴. 같은 콜을 자주 노린다", honesty = 0.3f });
            s.drivers.Add(new NpcDriver { name = "노 기사", look = "공사 하청 출신. 서류 얘기를 잘 안다", honesty = 0.8f });
        }

        /// <summary>오늘 기사식당에 나와 있는 기사 셋.</summary>
        public static List<NpcDriver> Present(SaveData s)
        {
            EnsureDrivers(s);
            var rng = new System.Random(s.day * 131 + 7);
            var list = new List<NpcDriver>(s.drivers);
            while (list.Count > 3) list.RemoveAt(rng.Next(list.Count));
            return list;
        }

        /// <summary>오늘의 소문 (아직 안 산 것). 날마다 같은 결과.</summary>
        public static List<Rumor> TodayOffers(SaveData s, IList<int> callWorlds)
        {
            var offers = new List<Rumor>();
            var present = Present(s);
            var rng = new System.Random(s.day * 977 + 3);
            var worlds = new List<int>(callWorlds);
            foreach (var w in s.worlds) if (!worlds.Contains(w.number)) worlds.Add(w.number);
            int id = s.day * 100;
            foreach (var d in present)
            {
                int n = 1 + rng.Next(2);
                for (int k = 0; k < n && worlds.Count > 0; k++)
                {
                    int world = worlds[rng.Next(worlds.Count)];
                    var r = Make(s, d.name, d.honesty, world, rng, id++);
                    if (r == null) continue;
                    // 이미 산 소문은 다시 안 판다
                    bool have = false;
                    foreach (var o in s.rumors) if (o.id == r.id) have = true;
                    if (!have) offers.Add(r);
                }
            }
            return offers;
        }

        public static Rumor Make(SaveData s, string source, float honesty, int world, System.Random rng, int id)
        {
            var spec = WorldGenerator.Generate(world);
            var ws = s.GetWorld(world);
            bool truthful = rng.NextDouble() < honesty;
            var r = new Rumor { id = id, world = world, source = source, day = s.day, generationAtRumor = ws != null ? ws.generation : 1, price = 2000 + rng.Next(5) * 1000 };
            int roll = rng.Next(ws != null ? 3 : 2);
            if (roll == 0)
            {
                r.kind = "rule";
                int rules = (int)spec.rules;
                if (!truthful)
                {
                    // 규칙 하나를 뒤집는다
                    var flags = new[] { WorldRule.ToxicGas, WorldRule.SoundReactive, WorldRule.EternalNight };
                    rules ^= (int)flags[rng.Next(flags.Length)];
                }
                r.claimRules = rules;
                var claim = new WorldSpec { rules = (WorldRule)rules };
                r.text = rules == 0 ? $"제{world}번? 거긴 별거 없어. 그냥 동네야." : $"제{world}번은 {claim.RuleText(false)}이래. 직접 들었어.";
            }
            else if (roll == 1)
            {
                r.kind = "danger";
                int d = truthful ? spec.trueDanger : Mathf.Clamp(spec.trueDanger + (rng.NextDouble() < 0.5 ? -2 : 2), 1, 5);
                r.claimValue = d;
                r.text = $"제{world}번 공사 추정 믿지 마. 실제론 위험도 {d}짜리야.";
            }
            else
            {
                r.kind = "armor";
                bool hard = MeanHeadArmor(ws) > 0.42f;
                bool claim = truthful ? hard : !hard;
                r.claimValue = claim ? 1 : 0;
                r.text = claim ? $"제{world}번 놈들 머리가 단단해졌어. 몸통을 쏴." : $"제{world}번은 아직 머리 쏘면 한 방이야.";
            }
            return r;
        }

        static float MeanHeadArmor(WorldState ws)
        {
            if (ws == null || ws.crawlers.Count == 0) return 0f;
            float t = 0f;
            foreach (var g in ws.crawlers) t += g.headArmor;
            return t / ws.crawlers.Count;
        }

        /// <summary>가서 확인했다: 그 세계의 소문들을 맞음/틀림으로 판정하고 기사 신뢰를 갱신한다.</summary>
        public static List<string> VerifyAfterVisit(SaveData s, int world)
        {
            var notes = new List<string>();
            var spec = WorldGenerator.Generate(world);
            var ws = s.GetWorld(world);
            foreach (var r in s.rumors)
            {
                if (r.world != world || r.verified != 0) continue;
                bool ok = r.kind switch
                {
                    "rule" => r.claimRules == (int)spec.rules,
                    "danger" => r.claimValue == spec.trueDanger,
                    _ => (r.claimValue == 1) == (MeanHeadArmor(ws) > 0.42f),
                };
                // 낡은 정보: 세대가 바뀌었으면 진화 소문은 틀릴 수 있다 (거짓말과 구분해 표시)
                bool stale = r.kind == "armor" && ws != null && ws.generation > r.generationAtRumor + 1;
                r.verified = ok ? 1 : -1;
                var d = s.drivers.Find(x => x.name == r.source);
                if (d != null && !stale)
                {
                    if (ok) d.confirmed++; else d.refuted++;
                }
                notes.Add($"소문 확인 — {r.source}: \"{r.text}\" → {(ok ? "맞았다" : stale ? "낡은 정보였다" : "틀렸다")}");
            }
            return notes;
        }

        /// <summary>내가 직접 확인한, 팔 수 있는 정보.</summary>
        public static List<(string key, string text, int price)> Sellable(SaveData s)
        {
            var list = new List<(string, string, int)>();
            foreach (var w in s.worlds)
                if (w.knownRules != 0)
                    list.Add(($"rule:{w.number}", $"제{w.number}번 규칙: {new WorldSpec { rules = (WorldRule)w.knownRules }.RuleText(false)}", SellPriceRule));
            foreach (var c in s.codex)
            {
                var cause = DeathCauses.Get(c.causeId);
                if (cause != null) list.Add(($"codex:{c.causeId}", $"사인 #{cause.number:00} {cause.title} 대응법", SellPriceCodex));
            }
            return list;
        }

        /// <summary>그 기사가 아는 신뢰 표시: 확인한 적 없으면 ?, 맞은 비율로 별.</summary>
        public static string TrustText(NpcDriver d)
        {
            int n = d.confirmed + d.refuted;
            if (n == 0) return "신뢰 ?";
            float ratio = d.confirmed / (float)n;
            int stars = Mathf.Clamp(Mathf.RoundToInt(ratio * 5f), 0, 5);
            return $"신뢰 {new string('★', stars)}{new string('☆', 5 - stars)} ({d.confirmed}/{n})";
        }
    }
}
