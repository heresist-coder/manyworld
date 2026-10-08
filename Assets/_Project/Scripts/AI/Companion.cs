using UnityEngine;
using UnityEngine.AI;

namespace Manyworld
{
    public enum CompanionOrder { Follow, Hold, Cover, HoldFire, ReviveMe, GoAhead }

    /// <summary>
    /// 첫 동료 차은주 (characters/cha-eunju.md). 정밀 사수, 짠돌이형의 끝판왕.
    /// 확실할 때만 한 발 쏜다. 근접에 약하다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Companion : MonoBehaviour
    {
        public const string Name = "차은주";

        public Health health;
        public CompanionOrder order = CompanionOrder.Follow;
        public float ReviveProgress { get; private set; }
        public Monster AimTarget => aimTarget;
        public float AimProgress => aimTarget == null ? 0f : 1f - Mathf.Clamp01(aimTimer / AimTime);

        NavMeshAgent agent;
        Transform visual;
        float thinkTimer, aimTimer, fireReady, barkReady;
        Monster aimTarget;
        bool goAheadPhaseReturn;
        float focusUntil;
        CharacterRig rig;

        /// <summary>출동 전 차 안에서 곡을 끝까지 들으면 집중 상태 (cha-eunju.md 워크맨).</summary>
        public bool Focused => Time.time < focusUntil;

        public void GiveFocus(float seconds)
        {
            focusUntil = Time.time + seconds;
            MissionController.Instance?.Toast($"{Name}: 집중 상태 — {seconds / 60f:0}분간 명중률↑, 조준 빠름");
        }

        const int EnvMask = ~(1 << 2);
        const float Damage = 60f;
        float AimTime => (order == CompanionOrder.Cover ? 0.6f : 0.95f) * (Focused ? 0.7f : 1f);

        public bool InVehicle { get; private set; }
        readonly System.Collections.Generic.List<Renderer> hiddenRenderers = new System.Collections.Generic.List<Renderer>();

        public void SetInVehicle(bool inside, Vector3 exitPos, Transform car = null)
        {
            InVehicle = inside;
            if (inside && car != null)
            {
                transform.SetParent(car, true);
                transform.localPosition = new Vector3(0.35f, 0.3f, 0f);
            }
            else if (!inside && transform.parent != null && transform.parent.GetComponent<TicoCar>() != null)
                transform.SetParent(transform.parent.parent, true);
            health.untargetable = inside;
            PlayerController.HideRenderers(transform, inside, hiddenRenderers);
            if (inside)
            {
                agent.enabled = false;
                aimTarget = null;
            }
            else
            {
                if (NavMesh.SamplePosition(exitPos, out var hit, 6f, NavMesh.AllAreas)) transform.position = hit.position;
                else transform.position = exitPos;
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(transform.position);
            }
        }

        public static Companion Spawn(Vector3 pos)
        {
            var go = new GameObject("Companion_ChaEunju");
            go.transform.position = pos;
            var c = go.AddComponent<Companion>();
            c.health = go.AddComponent<Health>();
            c.health.Init(90f);

            c.visual = new GameObject("Visual").transform;
            c.visual.SetParent(go.transform, false);
            // 흰 크롭티 + 청바지. 바랜 배경에서 흰색이 튄다
            Graybox.Prim(PrimitiveType.Capsule, c.visual, "Legs", new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.45f, 0.45f), new Color(0.22f, 0.3f, 0.5f), false);
            Graybox.Prim(PrimitiveType.Capsule, c.visual, "Torso", new Vector3(0, 1.15f, 0), new Vector3(0.5f, 0.38f, 0.42f), new Color(0.97f, 0.97f, 0.95f), false);
            Graybox.Prim(PrimitiveType.Sphere, c.visual, "Head", new Vector3(0, 1.6f, 0), Vector3.one * 0.32f, new Color(0.9f, 0.76f, 0.64f), false);
            Graybox.Box(c.visual, "Hair", new Vector3(0, 1.55f, -0.17f), new Vector3(0.18f, 0.45f, 0.08f), new Color(0.08f, 0.06f, 0.05f), false);
            Graybox.Box(c.visual, "Rifle", new Vector3(0.22f, 1.2f, 0.45f), new Vector3(0.06f, 0.08f, 1.05f), new Color(0.35f, 0.25f, 0.15f), false);
            var tag = Graybox.Label(go.transform, Name, new Vector3(0, 2.15f, 0), 0.035f, Color.white);
            tag.gameObject.AddComponent<Billboard>();
            Graybox.SetLayerRecursive(go, 2);
            c.rig = CharacterRig.Attach(c.visual, ArtCatalog.Instance != null ? ArtCatalog.Instance.companionModel : null, 1.68f);
            if (c.rig != null) Graybox.SetLayerRecursive(go, 2);

            c.agent = go.GetComponent<NavMeshAgent>();
            c.agent.speed = 4.6f;
            c.agent.radius = 0.3f;
            c.agent.height = 1.75f;
            c.agent.angularSpeed = 720f;
            c.agent.acceleration = 24f;
            c.agent.stoppingDistance = 0.3f;
            return c;
        }

        void Start()
        {
            health.Downed += _ =>
            {
                if (rig != null) rig.Down = true;
                else visual.localRotation = Quaternion.Euler(-80, 0, 0);
                agent.isStopped = true;
                aimTarget = null;
                MissionController.Instance?.Toast($"{Name}이(가) 쓰러졌다! (가까이 가서 E 길게)");
            };
            health.Revived += () =>
            {
                if (rig != null) rig.Down = false;
                else visual.localRotation = Quaternion.identity;
                agent.isStopped = false;
                Bark("…워크맨은 괜찮네요. 몇 발 썼어요?");
            };
        }

        public void SetOrder(CompanionOrder o)
        {
            order = o;
            goAheadPhaseReturn = false;
            switch (o)
            {
                case CompanionOrder.Follow: Bark("따라갈게요."); break;
                case CompanionOrder.Hold: Bark("여기 있을게요."); break;
                case CompanionOrder.Cover: Bark("엄호할게요. 보이면 쏩니다."); break;
                case CompanionOrder.HoldFire: Bark("네. 안 쏠게요."); break;
                case CompanionOrder.ReviveMe: Bark("기사님, 지금 가요."); break;
                case CompanionOrder.GoAhead: Bark("…알겠어요. 금방 올게요."); break;
            }
        }

        void Bark(string line, bool force = true)
        {
            if (!force && Time.time < barkReady) return;
            barkReady = Time.time + 6f;
            MissionController.Instance?.Toast($"{Name}: \"{line}\"");
            MissionController.Instance?.LogAudible($"은주: \"{line}\"");
        }

        void Update()
        {
            var mc = MissionController.Instance;
            if (mc == null || !mc.SimRunning || health.IsDown || InVehicle) return;
            if (!agent.isOnNavMesh) return;

            // 기사가 쓰러지면 기본은 깨우러 간다. 각성제를 아끼지 않는다
            if (mc.Player.IsDown && order != CompanionOrder.GoAhead && order != CompanionOrder.ReviveMe)
                SetOrder(CompanionOrder.ReviveMe);
            if (!mc.Player.IsDown && order == CompanionOrder.ReviveMe) order = CompanionOrder.Follow;

            thinkTimer -= Time.deltaTime;
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.25f;
                Move(mc);
            }

            if (order == CompanionOrder.ReviveMe) UpdateRevive(mc);
            else ReviveProgress = 0f;

            UpdateShooting(mc);
            if (rig != null) rig.Aim = aimTarget != null;
        }

        void Move(MissionController mc)
        {
            var player = mc.Player.transform;
            switch (order)
            {
                case CompanionOrder.Follow:
                case CompanionOrder.Cover:
                case CompanionOrder.HoldFire:
                {
                    var slot = player.position + player.rotation * new Vector3(-1.9f, 0, -0.4f); // 왼쪽 옆. 어깨 카메라(오른쪽)를 가리지 않는다
                    if (Vector3.Distance(transform.position, slot) > 2.2f || Vector3.Distance(transform.position, player.position) > 4f)
                    {
                        agent.isStopped = false;
                        agent.speed = Vector3.Distance(transform.position, player.position) > 8f ? 6.2f : 4.6f;
                        agent.SetDestination(slot);
                    }
                    break;
                }
                case CompanionOrder.Hold:
                    agent.isStopped = true;
                    break;
                case CompanionOrder.ReviveMe:
                    agent.isStopped = false;
                    agent.speed = 6.2f;
                    agent.SetDestination(player.position);
                    break;
                case CompanionOrder.GoAhead:
                    agent.isStopped = false;
                    agent.speed = 4.6f;
                    if (!mc.ObjectiveDone) agent.SetDestination(mc.Layout.objectivePos);
                    else agent.SetDestination(mc.Layout.ticoPos);
                    if (!goAheadPhaseReturn && mc.ObjectiveDone)
                    {
                        goAheadPhaseReturn = true;
                        Bark("찾았어요. 차로 갈게요.");
                    }
                    break;
            }

            // 근접에 약하다: 너무 붙으면 물러선다
            var close = NearestMonster(mc, 2.6f, false);
            if (close != null && order != CompanionOrder.GoAhead)
            {
                var away = transform.position + (transform.position - close.transform.position).normalized * 3f;
                if (NavMesh.SamplePosition(away, out var hit, 2f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                }
            }
        }

        void UpdateRevive(MissionController mc)
        {
            float d = Vector3.Distance(transform.position, mc.Player.transform.position);
            if (d > 1.8f) { ReviveProgress = 0f; return; }
            ReviveProgress += Time.deltaTime / 4f;
            if (ReviveProgress >= 1f)
            {
                ReviveProgress = 0f;
                mc.Player.health.Revive(0.4f);
                mc.Ledger.Charge("각성제", 3000);
                order = CompanionOrder.Follow;
                Bark("일어나셨어요? …운전은 기사님이 하셔야죠.");
            }
        }

        void UpdateShooting(MissionController mc)
        {
            if (order == CompanionOrder.HoldFire || order == CompanionOrder.ReviveMe && ReviveProgress > 0f)
            {
                aimTarget = null;
                return;
            }

            float range = order == CompanionOrder.Cover ? 42f : 30f;
            if (aimTarget == null || aimTarget.health.IsDown || !CanSee(aimTarget, range))
            {
                aimTarget = PickTarget(mc, range);
                aimTimer = AimTime;
                if (aimTarget == null) return;
            }

            var to = aimTarget.transform.position - transform.position;
            to.y = 0;
            if (to.sqrMagnitude > 0.01f && agent.velocity.sqrMagnitude < 0.5f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 10f);

            aimTimer -= Time.deltaTime;
            if (aimTimer > 0f || Time.time < fireReady) return;

            Fire(mc, aimTarget);
            fireReady = Time.time + 1.3f;
            aimTimer = AimTime;
        }

        /// <summary>짠돌이: 이미 덤벼드는 놈이나 가까운 놈만 쏜다. 엄호 명령이면 보이는 대로.</summary>
        Monster PickTarget(MissionController mc, float range)
        {
            Monster best = null;
            float bestScore = float.MaxValue;
            foreach (var m in mc.Monsters)
            {
                if (m == null || m.health.IsDown) continue;
                float d = Vector3.Distance(transform.position, m.transform.position);
                if (d > range) continue;
                bool threat = m.Encountered || d < 14f;
                if (!threat && order != CompanionOrder.Cover) continue;
                if (!CanSee(m, range)) continue;
                float score = d - (m.Encountered ? 10f : 0f);
                if (score < bestScore) { bestScore = score; best = m; }
            }
            return best;
        }

        void Fire(MissionController mc, Monster m)
        {
            mc.Ledger.companionShots++;
            mc.Ledger.Charge("은주 경기탄", CallLedger.CompanionShotCost);

            var muzzle = transform.position + Vector3.up * 1.3f + transform.forward * 0.6f;
            var head = m.transform.Find("Head");
            bool aimHead = head != null;
            // 정밀 사수. 그래도 겁먹으면(가까우면) 흔들린다
            float dist = Vector3.Distance(transform.position, m.transform.position);
            float accuracy = dist < 4f ? 0.55f : Focused ? 0.97f : 0.88f;
            Vector3 aimPoint = aimHead ? head.position : m.transform.position + Vector3.up * 0.8f;
            if (Random.value > accuracy) aimPoint += Random.insideUnitSphere * 1.2f;

            var dir = (aimPoint - muzzle).normalized;
            Vector3 end = muzzle + dir * 60f;
            if (Physics.Raycast(muzzle, dir, out var hit, 60f, EnvMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var zone = hit.collider.GetComponent<HitZone>();
                if (zone != null)
                {
                    zone.Hit(Damage, DamageSource.Companion, hit.point);
                    if (zone.isHead && zone.multiplier < 1.3f)
                        Bark("머리가… 단단해졌어요. 몸통 노리세요.", false);
                }
            }
            Tracer.Spawn(muzzle, end, new Color(0.8f, 0.95f, 1f));
            ArtCatalog.SpawnFx(ArtCatalog.Instance?.muzzleFlash, muzzle, Quaternion.LookRotation(dir), 0.25f, 0.2f);
            Noise.Emit(transform.position, 35f);
            Sfx.PlayAt("rifle_shot", muzzle, 0.85f, 90f, 0.04f);
            mc.LogAudible("(짧고 날카로운 총성 한 발)");
        }

        Monster NearestMonster(MissionController mc, float within, bool needSight)
        {
            Monster best = null;
            float bestD = within;
            foreach (var m in mc.Monsters)
            {
                if (m == null || m.health.IsDown) continue;
                float d = Vector3.Distance(transform.position, m.transform.position);
                if (d < bestD && (!needSight || CanSee(m, within))) { bestD = d; best = m; }
            }
            return best;
        }

        bool CanSee(Monster m, float range)
        {
            var eye = transform.position + Vector3.up * 1.55f;
            var tgt = m.transform.position + Vector3.up * 0.7f;
            var dir = tgt - eye;
            float dist = dir.magnitude;
            if (dist > range) return false;
            if (!Physics.Raycast(eye, dir / dist, out var hit, dist, EnvMask, QueryTriggerInteraction.Ignore)) return true;
            var zone = hit.collider.GetComponent<HitZone>();
            return zone != null && zone.owner == m.health;
        }
    }
}
