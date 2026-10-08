using System;
using UnityEngine;

namespace Manyworld
{
    /// <summary>맞는 부위. 머리는 배율이 붙고, 진화한 머리 장갑이 배율을 깎는다.</summary>
    public class HitZone : MonoBehaviour
    {
        public Health owner;
        public bool isHead;
        public float multiplier = 1f;

        public void Hit(float baseDamage, DamageSource source, Vector3 point)
        {
            if (owner == null) return;
            if (owner.GetComponent<Monster>() != null)
                ArtCatalog.SpawnFx(ArtCatalog.Instance?.bloodSplash, point, Quaternion.identity, isHead ? 0.6f : 0.45f, 1.2f);
            owner.TakeDamage(new DamageInfo
            {
                amount = baseDamage * multiplier,
                source = source,
                headshot = isHead,
                point = point,
            });
        }
    }
}
