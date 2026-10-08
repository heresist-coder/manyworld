using System;
using System.Collections.Generic;
using UnityEngine;

namespace Manyworld
{
    [Flags]
    public enum WorldRule
    {
        None = 0,
        ToxicGas = 1 << 0,      // 독가스: 필터 소모, 없으면 체력 감소
        SoundReactive = 1 << 1, // 소리 반응: 총소리가 멀리 퍼진다, 몬스터는 귀로 사냥
        EternalNight = 1 << 2,  // 영원한 밤: 시야 짧음, 손전등이 어그로
    }

    public enum TerrainMod { None, Fog, Flood, Collapse }

    /// <summary>번호(시드)에서 뽑은 세계 정의. 같은 번호는 언제나 같은 결과 (D6).</summary>
    public class WorldSpec
    {
        public int number;
        public string divergence;     // 갈라진 사건
        public string nickname;
        public WorldRule rules;
        public TerrainMod terrain;
        public int trueDanger;        // 1~5
        public int estimatedDanger;   // 공사 추정치. 틀릴 수 있다
        public float crawlerRatio;    // 기는 놈 / 덩치 비율
        public int monsterCount;
        public Color skyTint;

        public string Title => $"제{number}번 세계";

        public string RuleText(bool estimate)
        {
            if (rules == WorldRule.None) return estimate ? "특이 사항 없음(?)" : "특이 사항 없음";
            var parts = new List<string>();
            if (rules.HasFlag(WorldRule.ToxicGas)) parts.Add(estimate ? "대기 이상(?)" : "독가스");
            if (rules.HasFlag(WorldRule.SoundReactive)) parts.Add(estimate ? "소음 민감(?)" : "소리 반응");
            if (rules.HasFlag(WorldRule.EternalNight)) parts.Add(estimate ? "일조 이상(?)" : "영원한 밤");
            return string.Join(", ", parts);
        }
    }

    public static class WorldGenerator
    {
        struct Divergence
        {
            public string text, nickname;
            public WorldRule favored;
            public TerrainMod terrain;
            public Divergence(string t, string n, WorldRule r, TerrainMod m) { text = t; nickname = n; favored = r; terrain = m; }
        }

        // ② 갈라진 사건이 나머지 레이어를 끌고 간다 (brainstorm-02 4장)
        static readonly Divergence[] Divergences =
        {
            new Divergence("1979년 화학공장 폭발", "노란 안개", WorldRule.ToxicGas, TerrainMod.Fog),
            new Divergence("1982년 가스관 연쇄 파열", "매캐한 동네", WorldRule.ToxicGas, TerrainMod.Collapse),
            new Divergence("1983년 정전 이후 해가 돌아오지 않음", "밤만 있는 곳", WorldRule.EternalNight, TerrainMod.None),
            new Divergence("1980년 개기일식이 끝나지 않음", "검은 낮", WorldRule.EternalNight, TerrainMod.Fog),
            new Divergence("1981년 공습 사이렌 사건 이후 소리가 금기가 됨", "조용한 마을", WorldRule.SoundReactive, TerrainMod.None),
            new Divergence("1984년 주민 집단 실종", "빈 동네", WorldRule.SoundReactive, TerrainMod.Collapse),
            new Divergence("1985년 대홍수", "잠긴 동네", WorldRule.None, TerrainMod.Flood),
            new Divergence("1978년 지반 붕괴", "꺼진 땅", WorldRule.None, TerrainMod.Collapse),
        };

        static readonly WorldRule[] AllRules = { WorldRule.ToxicGas, WorldRule.SoundReactive, WorldRule.EternalNight };

        public static WorldSpec Generate(int number)
        {
            number = Mathf.Clamp(number, 1, 999); // D11: 1~999, 0번은 스토리용
            var rng = new System.Random(number * 7919 + 17);
            var spec = new WorldSpec { number = number };

            // ① 거리: 번호가 클수록 우리 세계에서 멀다
            float distance = number / 999f;
            int ruleCount = number < 100 ? 1 : number < 500 ? (rng.NextDouble() < 0.5 ? 1 : 2) : (rng.NextDouble() < 0.4 ? 2 : 3);

            // ②
            var div = Divergences[rng.Next(Divergences.Length)];
            spec.divergence = div.text;
            spec.nickname = div.nickname;

            // ③ 규칙: 사건이 선호하는 규칙 먼저, 나머지는 무작위
            if (div.favored != WorldRule.None)
            {
                spec.rules |= div.favored;
                ruleCount--;
            }
            var pool = new List<WorldRule>(AllRules);
            pool.Remove(div.favored);
            while (ruleCount > 0 && pool.Count > 0)
            {
                // 작은 번호는 규칙이 없는 세계도 있다
                if (number < 100 && rng.NextDouble() < 0.5) break;
                int i = rng.Next(pool.Count);
                spec.rules |= pool[i];
                pool.RemoveAt(i);
                ruleCount--;
            }

            // ④ 지형 변조
            spec.terrain = rng.NextDouble() < 0.75 ? div.terrain : (TerrainMod)rng.Next(4);

            // 위험도
            int ruleNum = 0;
            foreach (var r in AllRules) if (spec.rules.HasFlag(r)) ruleNum++;
            spec.trueDanger = Mathf.Clamp(1 + Mathf.RoundToInt(distance * 2.5f) + (ruleNum >= 2 ? 1 : 0) + (rng.NextDouble() < 0.15 ? 1 : 0), 1, 5);
            int err = rng.NextDouble() < 0.35 ? (rng.NextDouble() < 0.5 ? -1 : 1) : 0;
            spec.estimatedDanger = Mathf.Clamp(spec.trueDanger + err, 1, 5);

            // ⑤ 생태계 초기값
            spec.crawlerRatio = 0.55f + (float)rng.NextDouble() * 0.35f;
            spec.monsterCount = 6 + spec.trueDanger * 2 + rng.Next(3);

            spec.skyTint = Color.HSVToRGB((float)rng.NextDouble(), 0.25f, 0.55f);
            if (spec.rules.HasFlag(WorldRule.ToxicGas)) spec.skyTint = new Color(0.62f, 0.58f, 0.32f);
            return spec;
        }

        /// <summary>안 간 세계의 초기 개체군. 시드에서 만든다.</summary>
        public static WorldState CreateInitialState(WorldSpec spec)
        {
            var rng = new System.Random(spec.number * 104729 + 3);
            var ws = new WorldState { number = spec.number, generation = 1 };
            for (int i = 0; i < 12; i++) ws.crawlers.Add(Genome.Random(rng, i));
            for (int i = 0; i < 6; i++)
            {
                var g = Genome.Random(rng, 100 + i);
                g.size = Mathf.Clamp01(g.size + 0.2f);
                g.headArmor = Mathf.Clamp01(g.headArmor + 0.15f);
                ws.brutes.Add(g);
            }
            if (spec.rules.HasFlag(WorldRule.EternalNight))
                foreach (var g in ws.crawlers) g.nocturnal = Mathf.Clamp01(g.nocturnal + 0.4f);
            if (spec.rules.HasFlag(WorldRule.SoundReactive))
                foreach (var g in ws.crawlers) g.hearing = Mathf.Clamp01(g.hearing + 0.3f);
            return ws;
        }
    }
}
