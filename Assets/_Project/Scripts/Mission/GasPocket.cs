using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 저지대 가스 고임 (사인 #04). 필터로도 못 막는다. 처음엔 거의 안 보이고 쉭쉭 소리만 난다.
    /// 사인 도감에 기록되면 눈에 보이게 된다.
    /// </summary>
    public class GasPocket : MonoBehaviour
    {
        public float radius = 4.5f;
        float tick;
        bool warnedPlayer;

        public static GasPocket Create(Transform parent, Vector3 pos, float radius, bool known)
        {
            var go = new GameObject("GasPocket");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var gp = go.AddComponent<GasPocket>();
            gp.radius = radius;

            var color = known ? new Color(0.95f, 0.85f, 0.2f, 0.33f) : new Color(0.75f, 0.7f, 0.3f, 0.07f);
            var disc = Graybox.Prim(PrimitiveType.Cylinder, go.transform, "Haze", new Vector3(0, 0.6f, 0), new Vector3(radius * 2f, 0.6f, radius * 2f), color, false);
            disc.layer = 2;
            disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (known)
            {
                var label = Graybox.Label(go.transform, "가스 고임", new Vector3(0, 2.2f, 0), 0.05f, new Color(1f, 0.85f, 0.2f));
                label.gameObject.AddComponent<Billboard>();
            }
            Sfx.Loop("gas_hiss", go.transform, 0.7f, true, 14f);
            return gp;
        }

        public bool Contains(Vector3 p)
        {
            var d = p - transform.position;
            d.y = 0;
            return d.magnitude < radius && p.y - transform.position.y < 2.5f;
        }

        void Update()
        {
            var mc = MissionController.Instance;
            if (mc == null || !mc.SimRunning) return;
            tick -= Time.deltaTime;
            if (tick > 0f) return;
            tick = 0.5f;
            foreach (var h in mc.Targets)
            {
                if (h == null || h.IsDown || !Contains(h.transform.position)) continue;
                h.TakeDamage(new DamageInfo { amount = 4.5f, source = DamageSource.Environment, cause = "gaspocket", point = h.transform.position });
                if (h == mc.Player.health && !warnedPlayer)
                {
                    warnedPlayer = true;
                    mc.Toast("…목이 탄다. 필터를 끼고 있는데도.");
                    mc.LogAudible("기사: (콜록, 콜록)");
                }
            }
        }
    }
}
