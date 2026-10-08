using System;
using System.Collections.Generic;
using UnityEngine;

namespace Manyworld
{
    public enum CallGrade { Normal, Skull }

    /// <summary>
    /// 콜 한 건. 80년대 부동산 계약처럼 계약금 / 중도금 / 잔금 (brainstorm-01 4장).
    /// 전멸하면 중도금까지만 받는다.
    /// </summary>
    public class CallContract
    {
        public int worldNumber;
        public WorldSpec spec;
        public CallGrade grade;
        public string client = "○○개발공사";
        public string task;
        public int deposit;      // 계약금
        public int midPayment;   // 중도금
        public int balance;      // 잔금
        public float timeLimit = 900f; // 큰 월드: 차로 오가는 시간 포함

        public int Total => deposit + midPayment + balance;

        /// <summary>중도금 비율이 높으면 의뢰인이 위험을 알고 있다는 뜻 (복선).</summary>
        public float MidRatio => (float)midPayment / Total;

        static readonly string[] Tasks =
        {
            "측량 말뚝 회수",
            "공사 계측기 회수",
            "실종 측량반 장비 회수",
            "게이트 안정기 교체 부품 회수",
        };

        public static CallContract Create(int worldNumber, System.Random rng)
        {
            var spec = WorldGenerator.Generate(worldNumber);
            var c = new CallContract
            {
                worldNumber = spec.number,
                spec = spec,
                grade = spec.estimatedDanger >= 4 ? CallGrade.Skull : CallGrade.Normal,
                task = Tasks[rng.Next(Tasks.Length)],
            };
            int total = 60000 + spec.estimatedDanger * 35000 + rng.Next(0, 6) * 5000;
            if (c.grade == CallGrade.Skull) total = Mathf.RoundToInt(total * 1.5f);
            float midRatio = 0.25f + (float)rng.NextDouble() * 0.2f + (spec.trueDanger > spec.estimatedDanger ? 0.1f : 0f);
            c.deposit = Round(total * 0.25f);
            c.midPayment = Round(total * midRatio);
            c.balance = total - c.deposit - c.midPayment;
            return c;
        }

        static int Round(float v) => Mathf.RoundToInt(v / 500f) * 500;
    }

    public enum CallOutcome { Success, Withdrawn, CompanionSolo, Wiped }

    /// <summary>콜 진행 중 장부. 사격 미터기가 이걸 보여 준다.</summary>
    public class CallLedger
    {
        public readonly List<(string label, int amount)> expenses = new List<(string, int)>();
        public int playerShots, companionShots, filtersUsed;
        public bool depositPaid, midReached, objectiveDone;

        public const int PlayerShotCost = 330;     // 카빈 일반탄
        public const int CompanionShotCost = 520;  // 은주 경기탄
        public const int FilterCost = 4000;

        public int Spent
        {
            get
            {
                int s = 0;
                foreach (var e in expenses) s += e.amount;
                return s;
            }
        }

        public event Action<int> Charged;

        public void Charge(string label, int amount)
        {
            expenses.Add((label, amount));
            Charged?.Invoke(amount);
        }

        public int ShotSpend => playerShots * PlayerShotCost + companionShots * CompanionShotCost;
    }

    /// <summary>정산서 한 장.</summary>
    public class Settlement
    {
        public CallContract contract;
        public CallOutcome outcome;
        public int income;
        public int shotCost;
        public int filterCost;
        public int companionShare;
        public int recoveryFee;
        public int net;
        public int playerKnockouts;
        public int companionKnockouts;
        public float elapsed;
        public int newCases;    // 이번 콜에서 생긴 기절 사건
        public string notice;   // 생태 현황 고시
        public List<string> lines = new List<string>();
    }
}
