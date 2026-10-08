using System;
using UnityEngine;

namespace Manyworld
{
    public enum DamageSource { PlayerGun, PlayerMelee, Companion, Monster, Environment, Vehicle }

    public struct DamageInfo
    {
        public float amount;
        public DamageSource source;
        public bool headshot;
        public Vector3 point;
        public Monster attacker;   // 몬스터가 때렸을 때
        public string cause;       // 환경 피해 종류 (예: "gaspocket")
    }

    /// <summary>소리 이벤트. 소리 반응 세계(12번류)에서는 반경이 크게 늘어난다.</summary>
    public static class Noise
    {
        public static event Action<Vector3, float> Emitted;
        public static float WorldMultiplier = 1f;

        public static void Emit(Vector3 pos, float radius)
        {
            Emitted?.Invoke(pos, radius * WorldMultiplier);
        }

        public static void Reset()
        {
            Emitted = null;
            WorldMultiplier = 1f;
        }
    }
}
