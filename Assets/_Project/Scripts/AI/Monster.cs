using UnityEngine;
using UnityEngine.AI;

namespace Manyworld
{
    /// <summary>
    /// 몬스터. 행동 블록(배회·소리 추적·추격·공격·도주)은 고정이고, 유전자가 수치를 정한다.
    /// 플레이어와 마주쳤다가 살아남은 개체가 다음 세대에 번식한다 (Evolution).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Monster : MonoBehaviour
    {
        enum State { Wander, Investigate, Chase, Attack, Flee, Drag, Dead }

        public Species species;
        public Genome genome;
        public Health health;
        public bool Encountered { get; private set; }
        public bool IsHunting => state == State.Chase || state == State.Attack;
        public bool WindingUp => state == State.Attack && attackWindup;

        NavMeshAgent agent;
        State state = State.Wander;
        Health target;
        Vector3 investigatePos, lastSeenPos;
        float thinkTimer, wanderTimer, lostTimer, attackTimer, fleeTimer;
        bool attackWindup;

        float sightRange, hearingFactor, damage, attackRange, attackCooldown, moveSpeed;
        float eyeHeight, windupTime, nextCry;
        Renderer headRenderer;
        Vector3 nest;
        bool carrying;

        /// <summary>차는 덩치가 커서 조금 멀리서도 닿는다.</summary>
        float Reach(Health h) => attackRange + (h != null && h.GetComponent<TicoCar>() != null ? 1.8f : 0f);
        Material headMat;

        const int EnvMask = ~(1 << 2);

        public static Monster Spawn(Species species, Genome genome, Vector3 pos, Transform parent)
        {
            var go = new GameObject(species == Species.Crawler ? "Crawler" : "Brute");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var m = go.AddComponent<Monster>();
            m.species = species;
            m.genome = genome;
            m.Build();
            return m;
        }

        void Build()
        {
            agent = GetComponent<NavMeshAgent>();
            health = gameObject.AddComponent<Health>();
            var g = genome;

            // 몸 색: 야행성일수록 어둡고, 크기가 크면 붉은 기
            var bodyColor = Color.Lerp(new Color(0.45f, 0.5f, 0.38f), new Color(0.12f, 0.12f, 0.16f), g.nocturnal);
            bodyColor = Color.Lerp(bodyColor, new Color(0.5f, 0.2f, 0.18f), g.size * 0.4f);
            var plateColor = Color.Lerp(new Color(0.5f, 0.45f, 0.4f), new Color(0.85f, 0.85f, 0.8f), g.headArmor);

            Transform head;
            if (species == Species.Crawler)
            {
                float s = 0.75f + g.size * 0.5f;
                health.Init(30f + g.size * 40f);
                moveSpeed = 4.2f + g.speed * 3f - g.size * 1.2f - g.headArmor * 0.8f;
                damage = 7f + g.size * 6f;
                attackRange = 1.5f * s;
                attackCooldown = 0.9f;
                windupTime = 0.35f;
                sightRange = 22f;
                eyeHeight = 0.6f * s;

                var body = Graybox.Prim(PrimitiveType.Capsule, transform, "Body", new Vector3(0, 0.45f * s, 0), new Vector3(0.7f, 0.6f, 0.7f) * s, bodyColor);
                body.transform.localRotation = Quaternion.Euler(90, 0, 0);
                AddZone(body, false, 1f);
                var h = Graybox.Prim(PrimitiveType.Sphere, transform, "Head", new Vector3(0, 0.6f * s, 0.75f * s), Vector3.one * 0.42f * s, bodyColor * 1.2f);
                head = h.transform;
                AddZone(h, true, Mathf.Lerp(2.6f, 1.0f, g.headArmor));
                agent.radius = 0.45f * s;
                agent.height = 1f * s;
            }
            else
            {
                float s = 1.25f + g.size * 0.6f;
                health.Init(120f + g.size * 120f);
                moveSpeed = 2.4f + g.speed * 1.4f - g.headArmor * 0.5f;
                damage = 22f + g.size * 14f;
                attackRange = 2.0f * s * 0.8f;
                attackCooldown = 1.8f;
                windupTime = 0.65f; // 팔을 들었다 내리친다. 피할 틈이 있다
                sightRange = 18f;
                eyeHeight = 1.9f * s;

                var body = Graybox.Box(transform, "Body", new Vector3(0, 0.85f * s, 0), new Vector3(1.1f, 1.7f, 0.8f) * s, bodyColor);
                AddZone(body, false, 1f);
                var h = Graybox.Prim(PrimitiveType.Sphere, transform, "Head", new Vector3(0, 1.85f * s, 0.15f), Vector3.one * 0.45f * s, bodyColor * 1.2f);
                head = h.transform;
                AddZone(h, true, Mathf.Lerp(2.2f, 0.8f, g.headArmor));
                Graybox.Box(transform, "ArmL", new Vector3(-0.7f * s, 0.9f * s, 0.2f), new Vector3(0.3f, 1.3f, 0.3f) * s, bodyColor, false);
                Graybox.Box(transform, "ArmR", new Vector3(0.7f * s, 0.9f * s, 0.2f), new Vector3(0.3f, 1.3f, 0.3f) * s, bodyColor, false);
                agent.radius = 0.6f * s;
                agent.height = 2f * s;
            }

            headRenderer = head.GetComponent<Renderer>();
            headMat = headRenderer.sharedMaterial;

            // 머리 장갑판: 두꺼울수록 크고 밝다 → "저번엔 머리가 이렇지 않았는데…"
            if (g.headArmor > 0.25f)
            {
                var plate = Graybox.Box(head, "Plate", new Vector3(0, 0.25f, 0.2f), new Vector3(0.9f, 0.35f + g.headArmor * 0.4f, 0.6f), plateColor, false);
                plate.transform.localRotation = Quaternion.Euler(-20, 0, 0);
            }

            // 눈: 야행성이면 빛난다
            var eyeColor = g.nocturnal > 0.5f ? new Color(1f, 0.3f, 0.2f) : new Color(0.9f, 0.9f, 0.5f);
            Graybox.Prim(PrimitiveType.Sphere, head, "Eye", new Vector3(0, 0.1f, 0.48f), Vector3.one * 0.18f, eyeColor, false)
                .GetComponent<Renderer>().sharedMaterial = Graybox.Mat(eyeColor, g.nocturnal > 0.5f);

            agent.speed = moveSpeed * 0.4f;
            agent.angularSpeed = 540f;
            agent.acceleration = 20f;
            agent.stoppingDistance = attackRange * 0.8f;
            hearingFactor = Mathf.Lerp(0.5f, 1.6f, g.hearing);

            health.Damaged += OnDamaged;
            health.Downed += OnDowned;
            Noise.Emitted += OnNoise;
            wanderTimer = Random.Range(0f, 3f);
        }

        void AddZone(GameObject part, bool head, float mult)
        {
            var z = part.AddComponent<HitZone>();
            z.owner = health;
            z.isHead = head;
            z.multiplier = mult;
        }

        void OnDestroy()
        {
            Noise.Emitted -= OnNoise;
        }

        void OnNoise(Vector3 pos, float radius)
        {
            if (state == State.Dead || state == State.Chase || state == State.Attack || state == State.Flee) return;
            if (Vector3.Distance(pos, transform.position) > radius * hearingFactor) return;
            investigatePos = pos;
            state = State.Investigate;
            agent.speed = moveSpeed * Mathf.Lerp(0.6f, 1f, genome.aggression);
            agent.SetDestination(pos);
            MarkEncountered();
        }

        void OnDamaged(DamageInfo info)
        {
            MarkEncountered();
            var mc = MissionController.Instance;
            if (mc == null || state == State.Dead) return;
            if (state == State.Drag) StopDrag(mc); // 맞으면 놓는다

            // 겁 많은 개체는 크게 다치면 도망친다 → 살아남아 번식한다
            if (health.current < health.max * 0.35f && genome.aggression < 0.45f && state != State.Flee)
            {
                state = State.Flee;
                fleeTimer = 6f;
                var away = transform.position + (transform.position - info.point).normalized * 18f;
                if (NavMesh.SamplePosition(away, out var hit, 6f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
                agent.speed = moveSpeed * 1.1f;
                return;
            }

            Health attacker = info.source == DamageSource.Companion ? mc.Companion?.health : mc.Player?.health;
            if (attacker != null && !attacker.IsDown && !attacker.untargetable) StartChase(attacker);
        }

        void OnDowned(DamageInfo info)
        {
            SetHeadGlow(false);
            var mcd = MissionController.Instance;
            if (mcd != null && mcd.Dragger == this) mcd.Dragger = null;
            state = State.Dead;
            agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 85f);
            transform.position += Vector3.up * 0.2f;
            MissionController.Instance?.Evolution.RecordKill(this, info);
            MissionController.Instance?.OnMonsterKilled(this, info);
        }

        void MarkEncountered()
        {
            if (Encountered) return;
            Encountered = true;
            MissionController.Instance?.Evolution.RecordEncounter(this);
        }

        void StartChase(Health t)
        {
            bool fresh = state != State.Chase && state != State.Attack;
            target = t;
            state = State.Chase;
            agent.speed = moveSpeed;
            lostTimer = 0f;
            MarkEncountered();
            if (fresh && Time.time >= nextCry)
            {
                nextCry = Time.time + 8f;
                if (species == Species.Brute) Sfx.PlayAt("brute_roar", transform.position, 1f, 70f, 0.08f);
                else Sfx.PlayAt("crawler_screech", transform.position, 0.8f, 45f, 0.15f, 0.15f);
                MissionController.Instance?.LogAudible(species == Species.Brute ? "(낮게 우르릉거리는 소리)" : "(끼이익─ 하는 소리)");
            }
        }

        void Update()
        {
            var mc = MissionController.Instance;
            if (state == State.Dead || mc == null || !mc.SimRunning) return;

            thinkTimer -= Time.deltaTime;
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.2f;
                Think(mc);
            }

            if (state == State.Attack) UpdateAttack();
        }

        void Think(MissionController mc)
        {
            if (state == State.Flee)
            {
                fleeTimer -= 0.2f;
                if (fleeTimer <= 0f) state = State.Wander;
                return;
            }

            if (state == State.Drag)
            {
                ThinkDrag(mc);
                return;
            }
            // 쓰러진 사람을 끌고 가는 행동 (brainstorm-02 2장). 공격성 높은 기는 놈만
            if (species == Species.Crawler && genome.aggression > 0.5f && mc.CanDragPlayer(this))
            {
                StartDrag(mc);
                return;
            }

            var seen = FindVisibleTarget(mc);
            if (seen != null && state != State.Chase && state != State.Attack) StartChase(seen);

            switch (state)
            {
                case State.Wander:
                    wanderTimer -= 0.2f;
                    if (wanderTimer <= 0f)
                    {
                        wanderTimer = Random.Range(4f, 9f);
                        var p = transform.position + new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f));
                        if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas))
                        {
                            agent.speed = moveSpeed * 0.35f;
                            agent.SetDestination(hit.position);
                        }
                    }
                    break;

                case State.Investigate:
                    if (!agent.pathPending && agent.remainingDistance < 1.5f)
                    {
                        state = State.Wander;
                        wanderTimer = 2f;
                    }
                    break;

                case State.Chase:
                    if (target == null || target.IsDown || target.untargetable)
                    {
                        // 쓰러진 사람은 내버려 둔다. 다른 사람을 찾는다
                        target = null;
                        state = State.Wander;
                        break;
                    }
                    bool visible = CanSee(target, sightRange * 1.5f);
                    if (visible) { lastSeenPos = target.transform.position; lostTimer = 0f; }
                    else lostTimer += 0.2f;

                    if (lostTimer > Mathf.Lerp(3f, 9f, genome.aggression))
                    {
                        state = State.Investigate;
                        agent.SetDestination(lastSeenPos);
                        break;
                    }
                    agent.SetDestination(visible ? target.transform.position : lastSeenPos);
                    if (visible && Vector3.Distance(transform.position, target.transform.position) <= Reach(target))
                    {
                        state = State.Attack;
                        attackTimer = windupTime;
                        attackWindup = true;
                        agent.isStopped = true;
                        SetHeadGlow(species == Species.Brute && mc.KnowsCause("brute"));
                    }
                    break;
            }
        }

        void StartDrag(MissionController mc)
        {
            mc.Dragger = this;
            state = State.Drag;
            carrying = false;
            target = null;
            agent.isStopped = false;
            agent.speed = moveSpeed * 0.8f;
            // 은주 반대쪽, 숲으로
            var body = mc.Player.transform.position;
            var away = mc.Companion != null && !mc.Companion.health.IsDown ? (body - mc.Companion.transform.position) : transform.forward;
            away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            var goal = body + away.normalized * 35f;
            nest = NavMesh.SamplePosition(goal, out var hit, 10f, NavMesh.AllAreas) ? hit.position : body;
            agent.SetDestination(body);
            mc.Toast("…무언가가 기사를 끌고 간다");
            mc.LogAudible("(질질 끌리는 소리)");
        }

        void StopDrag(MissionController mc)
        {
            if (mc != null && mc.Dragger == this) mc.Dragger = null;
            carrying = false;
            agent.isStopped = false;
            state = State.Wander;
        }

        void ThinkDrag(MissionController mc)
        {
            if (!mc.Player.IsDown || mc.PlayerInCar)
            {
                StopDrag(mc);
                return;
            }
            var body = mc.Player.transform.position;
            if (!carrying)
            {
                agent.SetDestination(body);
                if (Vector3.Distance(transform.position, body) < 1.8f)
                {
                    carrying = true;
                    agent.speed = moveSpeed * 0.4f;
                    agent.SetDestination(nest);
                }
            }
            else if (!agent.pathPending && agent.remainingDistance < 1.2f)
                agent.isStopped = true; // 둥지에 쌓아 둔다
        }

        void LateUpdate()
        {
            if (state != State.Drag || !carrying) return;
            var mc = MissionController.Instance;
            if (mc == null || !mc.SimRunning) return;
            mc.DragBody(transform.position - transform.forward * 1.3f);
        }

        void SetHeadGlow(bool on)
        {
            if (headRenderer == null) return;
            headRenderer.sharedMaterial = on ? Graybox.Mat(new Color(1f, 0.15f, 0.1f), true) : headMat;
        }

        void UpdateAttack()
        {
            if (target == null || target.IsDown)
            {
                SetHeadGlow(false);
                agent.isStopped = false;
                state = State.Wander;
                return;
            }
            var to = target.transform.position - transform.position;
            to.y = 0;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 10f);

            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f) return;

            if (attackWindup)
            {
                SetHeadGlow(false);
                if (to.magnitude <= Reach(target) * 1.2f)
                {
                    target.TakeDamage(new DamageInfo { amount = damage, source = DamageSource.Monster, point = transform.position, attacker = this });
                    bool car = target.GetComponent<TicoCar>() != null;
                    Sfx.PlayAt(car ? "car_bump" : species == Species.Brute ? "pipe_hit" : "body_hit", target.transform.position + Vector3.up, species == Species.Brute ? 1f : 0.8f, 30f, 0.1f);
                }
                attackWindup = false;
                attackTimer = attackCooldown;
                return;
            }

            agent.isStopped = false;
            state = State.Chase;
        }

        Health FindVisibleTarget(MissionController mc)
        {
            Health best = null;
            float bestDist = float.MaxValue;
            foreach (var h in mc.Targets)
            {
                if (h == null || h.IsDown || h.untargetable) continue;
                float range = EffectiveSight(mc, h);
                float d = Vector3.Distance(transform.position, h.transform.position);
                float pref = h.GetComponent<TicoCar>() != null ? 6f : 0f; // 사람이 먼저다
                if (d > range || d + pref > bestDist) continue;
                // 시야각 140도. 아주 가까우면 등 뒤도 느낀다
                var dir = (h.transform.position - transform.position).normalized;
                if (d > 3f && Vector3.Angle(transform.forward, dir) > 70f) continue;
                if (!CanSee(h, range)) continue;
                best = h;
                bestDist = d + pref;
            }
            return best;
        }

        float EffectiveSight(MissionController mc, Health h)
        {
            float range = sightRange;
            var rules = mc.Spec.rules;
            if (rules.HasFlag(WorldRule.EternalNight))
            {
                range *= Mathf.Lerp(0.3f, 1f, genome.nocturnal);
                // 빛 = 어그로
                if (mc.Player != null && h == mc.Player.health && mc.Player.FlashlightOn) range = Mathf.Max(range, 38f);
                if (mc.Car != null && h == mc.Car.health && mc.Car.HeadlightsOn) range = Mathf.Max(range, 45f);
            }
            if (rules.HasFlag(WorldRule.SoundReactive)) range *= 0.4f; // 귀로 사냥하는 놈들
            if (mc.Spec.terrain == TerrainMod.Fog || rules.HasFlag(WorldRule.ToxicGas)) range *= 0.75f;
            return range;
        }

        bool CanSee(Health h, float range)
        {
            var eye = transform.position + Vector3.up * eyeHeight;
            var chest = h.transform.position + Vector3.up * 1.2f;
            var dir = chest - eye;
            float dist = dir.magnitude;
            if (dist > range) return false;
            return !Physics.Raycast(eye, dir / dist, dist - 0.3f, EnvMask, QueryTriggerInteraction.Ignore)
                   || IsOwnCollider(eye, dir / dist, dist);
        }

        bool IsOwnCollider(Vector3 origin, Vector3 dir, float dist)
        {
            // 자기 몸이나 다른 몬스터에 막힌 건 시야 차단으로 치지 않는다
            if (Physics.Raycast(origin, dir, out var hit, dist - 0.3f, EnvMask, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponent<HitZone>() != null;
            return true;
        }
    }
}
