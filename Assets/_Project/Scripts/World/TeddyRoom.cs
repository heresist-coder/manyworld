using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Manyworld
{
    /// <summary>
    /// 숲속의 빈 방 이상현상.
    /// - 곰인형은 아무도 보지 않을 때만 움직인다 (기사 화면, 은주 시야). 보는 동안엔 절대 안 움직인다.
    /// - 의자 위 곰인형을 집으면 방이 깨어난다: 인형이 사람 쪽으로 오고, 열린 면에 소나무가 솟아 벽이 닫힌다.
    /// - 닫힌 방에 갇히면 인형을 전부 쓰러뜨려야(총·쇠파이프) 벽이 다시 가라앉는다.
    /// </summary>
    public class TeddyRoom : MonoBehaviour
    {
        public enum Phase { Dormant, Awake, Closing, Closed, Reopening, Calm }

        public float half = 9f;
        public Terrain terrain;
        public Transform chair;
        public GameObject prize;

        public Phase State { get; private set; } = Phase.Dormant;
        public bool PrizeTaken { get; private set; }
        public float WallRaise { get; private set; } // 0 = 땅속, 1 = 다 솟음
        public int MovesWhileUnseen { get; private set; }

        public class Bear
        {
            public Transform t;
            public Health health;
            public float nextMove;
            public bool movedSinceSeen;
            public bool fallen;
        }

        public readonly List<Bear> Bears = new List<Bear>();
        readonly List<(Transform t, Vector3 up)> wall = new List<(Transform, Vector3)>();
        float closeAt = -1f;
        bool spokeMoved, spokeEnter, spokeTrapped;

        public int BearsStanding
        {
            get
            {
                int n = 0;
                foreach (var b in Bears) if (!b.health.IsDown) n++;
                return n;
            }
        }

        public void AddBear(GameObject go)
        {
            var b = new Bear { t = go.transform, health = go.GetComponent<Health>(), nextMove = 3f };
            b.health.Downed += _ => Fall(b);
            Bears.Add(b);
        }

        public void AddWallTree(Transform t, Vector3 up)
        {
            wall.Add((t, up));
            t.position = up - Vector3.up * 16f;
        }

        public Vector3 Local(Vector3 world) => Quaternion.Inverse(transform.rotation) * (world - transform.position);
        public bool Inside(Vector3 world, float pad = 0f)
        {
            var l = Local(world);
            return Mathf.Abs(l.x) < half + pad && Mathf.Abs(l.z) < half + pad;
        }
        public Vector3 Entrance => transform.position + transform.rotation * new Vector3(0, 0, -half - 1.5f);

        /// <summary>의자 위 곰인형을 집는다. 방이 깨어난다.</summary>
        public void TakePrize()
        {
            if (PrizeTaken) return;
            PrizeTaken = true;
            if (prize != null) prize.SetActive(false);
            State = Phase.Awake;
            closeAt = Time.time + 2.5f;
            foreach (var b in Bears) b.nextMove = Time.time + 0.8f + Random.value;
        }

        void Fall(Bear b)
        {
            if (b.fallen) return;
            b.fallen = true;
            b.t.rotation = Quaternion.AngleAxis(80f, b.t.right) * b.t.rotation;
            b.t.position += Vector3.up * 0.05f;
            Sfx.PlayAt("plush_step", b.t.position, 0.9f, 30f, 0.15f);
            ArtCatalog.SpawnFx(ArtCatalog.Instance?.smoke, b.t.position + Vector3.up * 0.3f, Quaternion.identity, 0.25f, 1.2f);
            var mc = MissionController.Instance;
            if (mc != null) mc.Toast(BearsStanding > 0 ? $"인형이 쓰러졌다 — 솜이 터진다 (남은 인형 {BearsStanding})" : "마지막 인형이 쓰러졌다");
        }

        void Update()
        {
            var mc = MissionController.Instance;
            if (mc == null || !mc.SimRunning || mc.Player == null) return;
            var player = mc.PlayerInCar ? mc.Car.transform.position : mc.Player.transform.position;
            float dist = Vector3.Distance(player, transform.position);

            if (!spokeEnter && Inside(player))
            {
                spokeEnter = true;
                mc.Toast($"곰인형 {Bears.Count}개. 의자 위에 하나 더.");
                mc.Toast($"{Companion.Name}: \"…누가 여기 앉혀 놨을까요.\"");
            }

            UpdateWall(mc, player);
            if (State == Phase.Calm) return;
            if (!PrizeTaken && dist > 85f) return; // 멀면 잠잠하다

            foreach (var b in Bears)
            {
                if (b.health.IsDown || Time.time < b.nextMove) continue;
                bool seen = Seen(b, mc);
                if (seen)
                {
                    if (b.movedSinceSeen && !spokeMoved && Vector3.Distance(player, b.t.position) < 30f)
                    {
                        spokeMoved = true;
                        mc.Toast($"{Companion.Name}: \"…저 인형, 아까 저기 있었어요?\"");
                    }
                    b.movedSinceSeen = false;
                    b.nextMove = Time.time + 0.3f;
                    continue;
                }
                Step(b, mc, player);
                b.nextMove = Time.time + (PrizeTaken ? 0.8f + Random.value * 0.6f : 2.2f + Random.value * 1.6f);
            }
        }

        void Step(Bear b, MissionController mc, Vector3 player)
        {
            var pos = b.t.position;
            Vector3 target;
            float stop;
            if (!PrizeTaken) { target = chair.position; stop = 1.5f; }
            else if (!mc.PlayerInCar && (Inside(player, 1f) || !Inside(pos))) { target = player; stop = 1.1f; }
            else { target = Entrance; stop = 0.5f; } // 사람이 나갔으면 출구로 모인다
            var to = target - pos;
            to.y = 0;
            float d = to.magnitude;
            var face = player - pos;
            face.y = 0;
            if (d <= stop)
            {
                if (PrizeTaken && target == player && !mc.Player.IsDown)
                {
                    // 발목에 닿았다
                    mc.Player.health.TakeDamage(new DamageInfo { amount = 14f, source = DamageSource.Environment, point = player, cause = "plush" });
                    Sfx.PlayAt("plush_step", pos, 1f, 20f, 0.1f);
                    mc.LogAudible("(발밑에서 뭔가 부드러운 게 부딪힌다)");
                }
                if (face.sqrMagnitude > 0.01f) b.t.rotation = Quaternion.LookRotation(face);
                return;
            }
            float step = Mathf.Min(PrizeTaken ? 1.7f : 0.9f, d - stop);
            var next = pos + to / d * step;
            // 벽(나무)을 뚫고 지나가지 않는다: 닫힌 뒤엔 방 안에 갇혀 있다
            if (State == Phase.Closed && Inside(pos) && !Inside(next)) return;
            if (NavMesh.SamplePosition(next, out var hit, 1.5f, NavMesh.AllAreas)) next = hit.position;
            if (terrain != null) next.y = terrain.SampleHeight(next);
            b.t.position = next;
            if (face.sqrMagnitude > 0.01f) b.t.rotation = Quaternion.LookRotation(face);
            b.movedSinceSeen = true;
            MovesWhileUnseen++;
            Sfx.PlayAt("plush_step", next, 0.55f, 16f, 0.15f);
        }

        /// <summary>기사 카메라나 은주 시야에 들어와 있으면 본 것이다 (가려지면 안 본 것).</summary>
        public bool Seen(Bear b, MissionController mc)
        {
            var c = b.t.position + Vector3.up * 0.4f;
            var cam = Camera.main;
            if (cam != null)
            {
                var vp = cam.WorldToViewportPoint(c);
                if (vp.z > 0f && vp.z < 160f && vp.x > -0.02f && vp.x < 1.02f && vp.y > -0.02f && vp.y < 1.02f && Clear(cam.transform.position, c, b))
                    return true;
            }
            var comp = mc.Companion;
            if (comp != null && !comp.health.IsDown && !comp.InVehicle)
            {
                var eye = comp.transform.position + Vector3.up * 1.5f;
                var to = c - eye;
                if (to.magnitude < 40f && Vector3.Angle(comp.transform.forward, to) < 55f && Clear(eye, c, b)) return true;
            }
            return false;
        }

        static readonly RaycastHit[] hits = new RaycastHit[16];

        static bool Clear(Vector3 from, Vector3 to, Bear b)
        {
            var dir = to - from;
            int n = Physics.RaycastNonAlloc(from, dir.normalized, hits, dir.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = hits[i].transform;
                if (t == b.t || t.IsChildOf(b.t)) continue;
                if (t.GetComponentInParent<PlayerController>() != null || t.GetComponentInParent<Companion>() != null) continue;
                if (t.GetComponentInParent<TicoCar>() != null) continue; // 차 유리 너머로도 보인다
                return false;
            }
            return true;
        }

        void UpdateWall(MissionController mc, Vector3 player)
        {
            float target = WallRaise;
            if (State == Phase.Awake && closeAt > 0f && Time.time >= closeAt)
            {
                State = Phase.Closing;
                Sfx.PlayAt("trees_rise", Entrance, 1f, 70f, 0.05f);
                mc.Toast("땅이 울린다 — 열린 쪽에서 나무가 솟는다! 나가!");
                Noise.Emit(Entrance, 25f);
            }
            if (State == Phase.Closing)
            {
                target = Mathf.MoveTowards(WallRaise, 1f, Time.deltaTime / 7f);
                if (target >= 1f)
                {
                    State = Phase.Closed;
                    if (Inside(player) && !spokeTrapped)
                    {
                        spokeTrapped = true;
                        mc.Toast("갇혔다. 인형을 전부 쓰러뜨리면 벽이 가라앉는다");
                        mc.Toast($"{Companion.Name}: \"…보고 있을 땐 안 움직여요. 하나씩.\"");
                    }
                }
            }
            if (State == Phase.Closed && BearsStanding == 0)
            {
                State = Phase.Reopening;
                Sfx.PlayAt("trees_rise", Entrance, 0.8f, 60f, 0.1f);
                mc.Toast("벽이 가라앉는다");
            }
            if (State == Phase.Reopening)
            {
                target = Mathf.MoveTowards(WallRaise, 0f, Time.deltaTime / 5f);
                if (target <= 0f) State = Phase.Calm;
            }
            if (!Mathf.Approximately(target, WallRaise)) SetWall(target);
        }

        public void SetWall(float t)
        {
            WallRaise = Mathf.Clamp01(t);
            float ease = Mathf.SmoothStep(0f, 1f, WallRaise);
            bool solid = WallRaise > 0.55f;
            foreach (var (tree, up) in wall)
            {
                tree.position = up - Vector3.up * (16f * (1f - ease));
                foreach (var col in tree.GetComponentsInChildren<Collider>()) col.enabled = solid;
                var ob = tree.GetComponent<NavMeshObstacle>();
                if (ob != null) ob.enabled = solid;
            }
        }

        /// <summary>테스트·디버그: 벽을 바로 닫는다.</summary>
        public void ForceClosed()
        {
            if (!PrizeTaken) TakePrize();
            State = Phase.Closed;
            SetWall(1f);
        }
    }
}
