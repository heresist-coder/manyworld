using UnityEngine;
using UnityEngine.InputSystem;

namespace Manyworld
{
    /// <summary>
    /// M1 카빈. 쏠 때마다 미터기가 "딸깍" 오른다 (brainstorm-01 4장).
    /// V: 쇠파이프 근접 — 위험하지만 공짜.
    /// </summary>
    public class PlayerWeapon : MonoBehaviour
    {
        public PlayerController owner;
        public int magSize = 15;
        public int mag = 15;
        public float damage = 26f;
        public float fireInterval = 0.14f;
        public float reloadTime = 1.6f;

        float nextFire, reloadEnd, meleeReady, lastFire = -10f;
        public bool Reloading => Time.time < reloadEnd;
        public bool RecentlyFired => Time.time - lastFire < 0.6f;

        const int EnvMask = ~(1 << 2);

        void Update()
        {
            var mc = MissionController.Instance;
            if (mc == null || !mc.InputEnabled || owner.IsDown || owner.InVehicle) return;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || kb == null) return;

            if (Reloading) return;
            if (mag < magSize && kb.rKey.wasPressedThisFrame) StartReload();

            if (mouse.leftButton.wasPressedThisFrame && Time.time >= nextFire)
            {
                if (mag <= 0) StartReload();
                else Fire(mc);
            }

            if (kb.vKey.wasPressedThisFrame && Time.time >= meleeReady) Melee();
        }

        void StartReload()
        {
            reloadEnd = Time.time + reloadTime;
            mag = magSize;
            Sfx.Play("reload", 0.6f, 0.03f);
            MissionController.Instance?.LogAudible("(철컥, 탄창 가는 소리)");
        }

        void Fire(MissionController mc)
        {
            nextFire = Time.time + fireInterval;
            lastFire = Time.time;
            mag--;

            mc.Ledger.playerShots++;
            mc.Ledger.Charge("카빈 일반탄", CallLedger.PlayerShotCost);

            var ray = owner.cam.CenterRay;
            float spread = owner.cam.aiming ? 0.4f : 2.2f;
            ray.direction = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0) * ray.direction;

            var muzzle = owner.transform.position + owner.transform.rotation * new Vector3(0.28f, 1.3f, 0.9f);
            Vector3 end = ray.origin + ray.direction * 150f;
            if (Physics.Raycast(ray, out var hit, 150f, EnvMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var zone = hit.collider.GetComponent<HitZone>();
                if (zone != null) zone.Hit(damage, DamageSource.PlayerGun, hit.point);
            }
            Tracer.Spawn(muzzle, end, new Color(1f, 0.85f, 0.4f));
            ArtCatalog.SpawnFx(ArtCatalog.Instance?.muzzleFlash, muzzle, Quaternion.LookRotation(end - muzzle), 0.25f, 0.2f);
            Noise.Emit(owner.transform.position, 35f);
            Sfx.Play("carbine_shot", 0.75f, 0.05f);
            mc.OnPlayerShot();
            owner.cam.Kick(owner.cam.aiming ? 0.8f : 1.4f);
        }

        void Melee()
        {
            meleeReady = Time.time + 0.75f;
            var center = owner.transform.position + Vector3.up * 1.1f + owner.transform.forward * 1.2f;
            var hits = Physics.OverlapSphere(center, 1.2f, EnvMask, QueryTriggerInteraction.Ignore);
            Health already = null;
            foreach (var h in hits)
            {
                var zone = h.GetComponent<HitZone>();
                if (zone == null || zone.owner == already) continue;
                already = zone.owner;
                zone.owner.TakeDamage(new DamageInfo { amount = 20f, source = DamageSource.PlayerMelee, point = h.transform.position });
            }
            Noise.Emit(owner.transform.position, 4f);
            if (already != null) Sfx.PlayAt("pipe_hit", center, 0.9f, 25f, 0.1f);
            MissionController.Instance.Toast(already != null ? "쇠파이프! (공짜)" : "헛스윙");
        }
    }
}
