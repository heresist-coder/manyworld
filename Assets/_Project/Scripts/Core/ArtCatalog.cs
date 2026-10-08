using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 외부 아트(Synty, Tripo, Mixamo, 이펙트) 목록. Editor의 ArtSetup이 채운다.
    /// 외부 에셋은 라이선스 때문에 레포에 없으니, 비어 있으면 그레이박스로 그린다.
    /// </summary>
    [CreateAssetMenu(menuName = "Manyworld/Art Catalog")]
    public class ArtCatalog : ScriptableObject
    {
        [Header("동네 (Synty POLYGON Town / City)")]
        public GameObject[] houses;
        public GameObject[] shops;
        public GameObject[] parkedCars;
        public GameObject[] streetProps;
        public GameObject tico;

        [Header("티코 (ithappy Car_06: 차체 + 바퀴 FL, FR, RL, RR)")]
        public GameObject ticoBody;
        public GameObject[] ticoWheels;

        [Header("큰 월드 (Synty Nature, Meadow Forest, Farm)")]
        public GameObject[] trees;
        public GameObject[] deadTrees;
        public GameObject[] bushes;
        public GameObject[] rocks;
        public GameObject[] grass;
        public GameObject[] farmBuildings;
        public GameObject[] fences;

        [Header("인물 (Tripo, Mixamo 뼈대)")]
        public GameObject driverModel;
        public GameObject companionModel;
        public RuntimeAnimatorController humanoidController;
        public GameObject rifle;

        [Header("이펙트")]
        public GameObject muzzleFlash;
        public GameObject bloodSplash;
        public GameObject smoke;

        static ArtCatalog instance;
        static bool loaded;

        public static ArtCatalog Instance
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    instance = Resources.Load<ArtCatalog>("ArtCatalog");
                }
                return instance;
            }
        }

        public static bool HasTown => Instance != null && Instance.houses != null && Instance.houses.Length > 0 && Instance.houses[0] != null;
        public static bool HasNature => Instance != null && Instance.trees != null && Instance.trees.Length > 0 && Instance.trees[0] != null;
        public static bool HasCharacters => Instance != null && Instance.driverModel != null && Instance.humanoidController != null;

        public static GameObject Pick(GameObject[] list, int index)
        {
            if (list == null || list.Length == 0) return null;
            return list[Mathf.Abs(index) % list.Length];
        }

        /// <summary>프리팹을 놓고, 렌더러 바운드 기준으로 size 안에 들어가게 균일 스케일한다. 바닥을 y=0에 맞춘다.</summary>
        public static GameObject PlaceFitted(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot, Vector3 maxSize, out Bounds worldBounds)
        {
            var go = Object.Instantiate(prefab, parent);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one;
            var b = RendererBounds(go);
            // 회전된 상태의 월드 바운드 기준
            float sx = maxSize.x / Mathf.Max(0.01f, b.size.x);
            float sz = maxSize.z / Mathf.Max(0.01f, b.size.z);
            float sy = maxSize.y > 0 ? maxSize.y / Mathf.Max(0.01f, b.size.y) : float.MaxValue;
            float s = Mathf.Min(sx, sz, sy);
            go.transform.localScale = Vector3.one * s;
            b = RendererBounds(go);
            go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
            worldBounds = RendererBounds(go);
            // 외부 프리팹의 콜라이더는 끈다. 게임 판정은 그레이박스 콜라이더로 한다
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            return go;
        }

        /// <summary>원래 크기 그대로 놓고 바닥만 맞춘다 (Synty는 미터 단위).</summary>
        public static GameObject PlaceNatural(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot, out Bounds worldBounds)
        {
            var go = Object.Instantiate(prefab, parent);
            go.transform.SetPositionAndRotation(pos, rot);
            var b = RendererBounds(go);
            go.transform.position += new Vector3(0, pos.y - b.min.y, 0);
            worldBounds = RendererBounds(go);
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            return go;
        }

        public static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>스프라이트 이펙트를 잠깐 띄운다. 아트가 없으면 아무것도 안 한다.</summary>
        public static void SpawnFx(GameObject prefab, Vector3 pos, Quaternion rot, float scale, float life)
        {
            if (prefab == null) return;
            var fx = Object.Instantiate(prefab, pos, rot);
            fx.transform.localScale *= scale;
            SetLayer(fx, 2);
            Object.Destroy(fx, life);
        }

        public static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
