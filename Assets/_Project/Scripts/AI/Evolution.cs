using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 콜 한 건 동안의 사냥 기록 → 다음 세대 (brainstorm-01 8장).
    /// 자연선택은 세계 규칙이 초기값으로, 인간선택은 여기서 처리한다.
    /// 죽은 개체는 빠지고, 살아남은 개체가 번식한다. 죽인 방식에 강한 쪽으로 돌연변이가 기운다.
    /// </summary>
    public class Evolution
    {
        readonly HashSet<int> encountered = new HashSet<int>();
        readonly HashSet<int> killed = new HashSet<int>();
        public int headKills, bodyKills, meleeKills, companionKills, flashlightKills;
        public readonly List<string> killLog = new List<string>();

        public void RecordEncounter(Monster m) => encountered.Add(m.genome.id);

        public void RecordKill(Monster m, DamageInfo info)
        {
            killed.Add(m.genome.id);
            if (info.source == DamageSource.PlayerMelee) meleeKills++;
            else if (info.headshot) headKills++;
            else bodyKills++;
            if (info.source == DamageSource.Companion) companionKills++;
            var mc = MissionController.Instance;
            if (mc != null && mc.Spec.rules.HasFlag(WorldRule.EternalNight) && mc.Player.FlashlightOn) flashlightKills++;
        }

        public int KillCount => killed.Count;

        /// <summary>세대 교체. 반환값은 뷰로 생태 현황 고시문.</summary>
        public string Advance(WorldState ws, WorldSpec spec, SaveData save)
        {
            var before = Mean(ws.crawlers, ws.brutes);
            var rng = new System.Random(ws.number * 977 + ws.generation * 31 + save.day);

            // 인간선택 방향
            var bias = new Genome { size = 0, speed = 0, headArmor = 0, hearing = 0, aggression = 0, nocturnal = 0 };
            int total = Mathf.Max(1, headKills + bodyKills + meleeKills);
            if (headKills > 0) bias.headArmor += 0.05f * headKills / total;                // 머리만 쏘면 → 머리 장갑
            if (bodyKills > 0) bias.size += 0.03f * bodyKills / total;                     // 몸통 → 덩치
            if (meleeKills > 0) bias.speed += 0.04f * meleeKills / total;                  // 근접 → 빠른 놈
            if (flashlightKills > 0) bias.nocturnal -= 0.05f;                              // 불빛 사냥 → 빛을 피하는 놈
            bias.aggression -= 0.03f * Mathf.Min(killed.Count, 6) / 6f;                   // 덤빈 놈이 죽으니 신중한 놈이 남는다

            Replace(ws.crawlers, rng, bias, 0);
            Replace(ws.brutes, rng, bias, 100);

            // 오래 비운 세계는 그동안 자연스럽게 진화가 더 진행된다
            int idle = Mathf.Clamp((save.day - ws.lastVisitDay) / 14, 0, 4);
            for (int i = 0; i < idle; i++)
            {
                Replace(ws.crawlers, rng, new Genome { size = 0, speed = 0, headArmor = 0, hearing = 0, aggression = 0, nocturnal = 0 }, 0, 2);
            }

            ws.generation += 1 + idle;
            ws.lastVisitDay = save.day;
            var after = Mean(ws.crawlers, ws.brutes);
            ws.lastNotice = Notice(spec, ws, before, after, save);
            return ws.lastNotice;
        }

        void Replace(List<Genome> pop, System.Random rng, Genome bias, int idBase, int forceReplace = 0)
        {
            var survivors = new List<Genome>();
            var dead = new List<int>();
            for (int i = 0; i < pop.Count; i++)
            {
                if (killed.Contains(pop[i].id) && forceReplace == 0) dead.Add(i);
                else survivors.Add(pop[i]);
            }
            for (int k = 0; k < forceReplace && pop.Count > 0; k++) dead.Add(rng.Next(pop.Count));
            if (survivors.Count == 0) survivors.AddRange(pop);

            // 마주쳤다가 살아남은 놈이 번식 확률이 높다
            Genome PickParent()
            {
                for (int t = 0; t < 3; t++)
                {
                    var g = survivors[rng.Next(survivors.Count)];
                    if (encountered.Contains(g.id) || rng.NextDouble() < 0.4) return g;
                }
                return survivors[rng.Next(survivors.Count)];
            }

            int nextId = idBase + 1000 + rng.Next(100000);
            foreach (var i in dead)
                pop[i] = Genome.Breed(PickParent(), PickParent(), rng, nextId++, bias);
        }

        static Genome Mean(List<Genome> a, List<Genome> b)
        {
            var m = new Genome { size = 0, speed = 0, headArmor = 0, hearing = 0, aggression = 0, nocturnal = 0 };
            int n = 0;
            foreach (var list in new[] { a, b })
            foreach (var g in list)
            {
                m.size += g.size; m.speed += g.speed; m.headArmor += g.headArmor;
                m.hearing += g.hearing; m.aggression += g.aggression; m.nocturnal += g.nocturnal;
                n++;
            }
            if (n == 0) return m;
            m.size /= n; m.speed /= n; m.headArmor /= n; m.hearing /= n; m.aggression /= n; m.nocturnal /= n;
            return m;
        }

        /// <summary>동사무소 게시판 공고문 형식. 딱딱한 문장 속에 섬뜩한 내용 (brainstorm-03 3장).</summary>
        static string Notice(WorldSpec spec, WorldState ws, Genome before, Genome after, SaveData save)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"고시 제{save.Date.Year}-{spec.number}-{ws.generation}호");
            sb.AppendLine($"{spec.Title} 제{ws.generation}세대 생태 현황");
            void Line(string label, float b, float a, string up, string down)
            {
                float pct = b > 0.01f ? (a - b) / b * 100f : 0f;
                if (Mathf.Abs(pct) < 2f) return;
                sb.AppendLine($"· {label} {(pct > 0 ? "+" : "")}{pct:0}% — {(pct > 0 ? up : down)}");
            }
            Line("주요 서식종 평균 두부 외피 두께", before.headArmor, after.headArmor, "두부 조준 효율 저하 예상", "두부 노출 증가");
            Line("평균 체구", before.size, after.size, "대형화 경향", "소형화 경향");
            Line("평균 이동 속도", before.speed, after.speed, "추격 개체 주의", "둔화");
            Line("야간 활동 개체 비율", before.nocturnal, after.nocturnal, "야간 출입 자제 바람", "주간 활동 증가");
            Line("공격 성향", before.aggression, after.aggression, "선제 공격 빈도 증가", "매복·회피 성향 증가");
            sb.AppendLine("해당 세계 출입 민원인은 유의 바람.");
            sb.Append("DIMENSION BUREAU");
            return sb.ToString();
        }
    }
}
