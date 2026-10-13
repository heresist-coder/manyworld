using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 나무 프리팹에 붙는 먼 거리 메시 (신티 LOD1·LOD2). 프리팹 자체는 LOD0만 그린다 — 신티 LOD 셰이더가 이 URP에서 하얗게 나와서.
    /// TreeField가 거리에 따라 이 메시를 LOD0의 무광 머티리얼로 그린다 (신티 LOD는 같은 텍스처 아틀라스를 쓴다).
    /// ArtSetup.WithTrunkColliders가 채운다.
    /// </summary>
    public class TreeLods : MonoBehaviour
    {
        public Mesh[] lods;        // [0] = LOD1, [1] = LOD2 ...
        public float[] fromDistance; // 이 거리부터 해당 LOD
    }
}
