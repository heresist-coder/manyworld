using System;
using UnityEngine;

namespace Manyworld
{
    public enum Species { Crawler, Brute }

    /// <summary>
    /// 몬스터 유전자. 모든 값은 0~1. 행동 블록은 고정이고 유전자가 수치와 빈도를 정한다 (brainstorm-01 8장).
    /// 트레이드오프: 크기↑ → 속도↓, 머리 장갑↑ → 속도↓, 청각↑ ↔ 시각↓
    /// </summary>
    [Serializable]
    public class Genome
    {
        public int id;
        public float size = 0.5f;
        public float speed = 0.5f;
        public float headArmor = 0.2f;
        public float hearing = 0.5f;
        public float aggression = 0.5f;
        public float nocturnal = 0.3f;   // 높을수록 어둠 속에서 잘 본다

        public Genome Clone()
        {
            return (Genome)MemberwiseClone();
        }

        public static Genome Random(System.Random rng, int id)
        {
            return new Genome
            {
                id = id,
                size = Next(rng, 0.3f, 0.7f),
                speed = Next(rng, 0.3f, 0.7f),
                headArmor = Next(rng, 0.0f, 0.35f),
                hearing = Next(rng, 0.2f, 0.8f),
                aggression = Next(rng, 0.3f, 0.8f),
                nocturnal = Next(rng, 0.1f, 0.5f),
            };
        }

        /// <summary>두 부모의 평균 + 돌연변이. bias는 인간선택 방향 (같은 크기의 유전자 변화량).</summary>
        public static Genome Breed(Genome a, Genome b, System.Random rng, int id, Genome bias)
        {
            float Mix(float x, float y, float bx) =>
                Mathf.Clamp01((x + y) * 0.5f + Next(rng, -0.08f, 0.08f) + bx);

            return new Genome
            {
                id = id,
                size = Mix(a.size, b.size, bias.size),
                speed = Mix(a.speed, b.speed, bias.speed),
                headArmor = Mix(a.headArmor, b.headArmor, bias.headArmor),
                hearing = Mix(a.hearing, b.hearing, bias.hearing),
                aggression = Mix(a.aggression, b.aggression, bias.aggression),
                nocturnal = Mix(a.nocturnal, b.nocturnal, bias.nocturnal),
            };
        }

        static float Next(System.Random rng, float min, float max) =>
            min + (float)rng.NextDouble() * (max - min);
    }
}
