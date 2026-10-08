using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Manyworld
{
    /// <summary>플레이어 진행 상태. JSON 하나로 저장한다.</summary>
    [Serializable]
    public class SaveData
    {
        public int money = 80000;
        public int day;                  // 1987-04-01 기준 경과 일수
        public int cleanStreak;          // 연속 무사고
        public int knockouts;            // 누적 기절 (D9: 감점)
        public int wipes;                // 누적 전멸
        public int bureauTicket = 140;   // 뷰로 번호표. 갈 때마다 올라간다
        public List<WorldState> worlds = new List<WorldState>();
        public List<KnockoutCase> cases = new List<KnockoutCase>();   // 기절 사건 (사인 분석)
        public List<CodexEntry> codex = new List<CodexEntry>();       // 사인 도감
        public int nextCaseId = 1;

        /// <summary>무사고 경력 점수. 기절 -1, 전멸 -3 (D9).</summary>
        public int RecordScore => cleanStreak - knockouts - wipes * 3;

        public DateTime Date => new DateTime(1987, 4, 1).AddDays(day);

        public WorldState GetWorld(int number)
        {
            foreach (var w in worlds)
                if (w.number == number) return w;
            return null;
        }

        /// <summary>테스트용. 설정하면 이 경로에 저장한다.</summary>
        public static string PathOverride;

        public bool HasCodex(string causeId)
        {
            foreach (var c in codex) if (c.causeId == causeId) return true;
            return false;
        }

        public CodexEntry UnlockCodex(string causeId, int world)
        {
            foreach (var c in codex)
                if (c.causeId == causeId)
                {
                    if (!c.worlds.Contains(world)) c.worlds.Add(world);
                    return c;
                }
            var e = new CodexEntry { causeId = causeId, firstDay = day };
            e.worlds.Add(world);
            codex.Add(e);
            return e;
        }

        public int UnsolvedCases
        {
            get
            {
                int n = 0;
                foreach (var c in cases) if (!c.solved) n++;
                return n;
            }
        }

        static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "manyworld_save.json");

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"세이브 로드 실패, 새로 시작: {e.Message}");
            }
            return new SaveData();
        }

        public void Save()
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }

    /// <summary>한 번이라도 간 세계만 저장한다 (brainstorm-02 4장).</summary>
    [Serializable]
    public class WorldState
    {
        public int number;
        public int generation;
        public int lastVisitDay;
        public List<Genome> crawlers = new List<Genome>();
        public List<Genome> brutes = new List<Genome>();
        public string lastNotice;        // 마지막 생태 현황 고시문
        public int knownRules;           // 직접 확인한 규칙 (WorldRule 플래그). 세계 수첩
        public int visits;
    }
}
