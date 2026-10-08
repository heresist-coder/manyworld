using System;
using UnityEngine;

namespace Manyworld
{
    /// <summary>체력. 0이 되면 Downed (영구 사망 없음, D3).</summary>
    public class Health : MonoBehaviour
    {
        public float max = 100f;
        public float current;
        public bool IsDown { get; private set; }
        /// <summary>차 안에 있으면 몬스터가 노리지 않는다.</summary>
        public bool untargetable;

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Downed;
        public event Action Revived;

        void Awake()
        {
            if (current <= 0f) current = max;
        }

        public void Init(float maxHp)
        {
            max = maxHp;
            current = maxHp;
            IsDown = false;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDown) return;
            current = Mathf.Max(0f, current - info.amount);
            Damaged?.Invoke(info);
            if (current <= 0f)
            {
                IsDown = true;
                Downed?.Invoke(info);
            }
        }

        public void Heal(float amount)
        {
            if (IsDown) return;
            current = Mathf.Min(max, current + amount);
        }

        public void Revive(float hpFraction)
        {
            IsDown = false;
            current = max * hpFraction;
            Revived?.Invoke();
        }
    }
}
