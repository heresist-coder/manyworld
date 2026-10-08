using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Manyworld.Tests
{
    /// <summary>콜 루프 스모크 테스트: 허브 → 출동 → 전투 → 전멸/성공 → 뷰로 → 정산 → 허브.</summary>
    public class CallLoopTests
    {
        GameManager gm;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveData.PathOverride = Path.Combine(Application.temporaryCachePath, "manyworld_test_save.json");
            yield return null;
            gm = GameManager.Instance;
            Assert.IsNotNull(gm, "GameManager가 자동 생성되어야 한다");
            gm.ResetSave();
            if (gm.Mode != GameMode.Hub) gm.EnterHub();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (MissionController.Instance != null) Object.Destroy(MissionController.Instance.gameObject);
            if (gm != null) gm.ResetSave();
            SaveData.PathOverride = null;
            yield return null;
        }

        [Test]
        public void SameNumberAlwaysSameWorld()
        {
            for (int n = 1; n <= 999; n += 37)
            {
                var a = WorldGenerator.Generate(n);
                var b = WorldGenerator.Generate(n);
                Assert.AreEqual(a.rules, b.rules);
                Assert.AreEqual(a.divergence, b.divergence);
                Assert.AreEqual(a.trueDanger, b.trueDanger);
                Assert.That(a.trueDanger, Is.InRange(1, 5));
            }
        }

        [UnityTest]
        public IEnumerator MissionSpawnsAndFights([Values(3, 47, 512, 999)] int number)
        {
            var contract = CallContract.Create(number, new System.Random(number));
            gm.StartCall(contract);
            yield return null;
            var mc = MissionController.Instance;
            Assert.IsNotNull(mc);
            Assert.AreEqual(GameMode.Mission, gm.Mode);
            Assert.Greater(mc.Monsters.Count, 0, "몬스터가 스폰되어야 한다");
            foreach (var m in mc.Monsters)
                Assert.IsTrue(m.GetComponent<NavMeshAgent>().isOnNavMesh, $"{m.name}이 네비메시 위에 있어야 한다");
            Assert.IsTrue(mc.Companion.GetComponent<NavMeshAgent>().isOnNavMesh);

            // 몬스터 하나를 기사 앞에 데려다 놓는다 → 은주가 쏘고, 몬스터가 덤빈다
            var monster = mc.Monsters[0];
            var front = mc.Player.transform.position + mc.Player.transform.forward * 7f;
            NavMesh.SamplePosition(front, out var hit, 5f, NavMesh.AllAreas);
            monster.GetComponent<NavMeshAgent>().Warp(hit.position);
            monster.transform.LookAt(mc.Player.transform);

            float hp = mc.Player.health.current + mc.Companion.health.current;
            yield return new WaitForSeconds(6f);
            Assert.That(mc.Ledger.companionShots > 0 || monster.health.IsDown || mc.Player.health.current + mc.Companion.health.current < hp,
                "교전이 일어나야 한다 (은주 사격 또는 피해)");
            Debug.Log($"[Test] 제{number}번: 몬스터 {mc.Monsters.Count}, 은주 {mc.Ledger.companionShots}발, 지출 {mc.Ledger.Spent}");
        }

        [UnityTest]
        public IEnumerator WipeGoesThroughBureau()
        {
            int before = gm.Save.money;
            gm.StartCall(CallContract.Create(47, new System.Random(1)));
            yield return null;
            var mc = MissionController.Instance;
            var kill = new DamageInfo { amount = 9999, source = DamageSource.Monster };
            mc.Player.health.TakeDamage(kill);
            mc.Companion.health.TakeDamage(kill);
            yield return new WaitForSeconds(3.2f);

            Assert.AreEqual(GameMode.Bureau, gm.Mode, "전멸하면 디멘션 뷰로로 간다");
            Assert.AreEqual(CallOutcome.Wiped, gm.LastSettlement.outcome);
            gm.FinishBureau(30000, new[] { "뷰로 회수비 (2인)|-30,000" });
            Assert.AreEqual(GameMode.Settlement, gm.Mode);
            Assert.AreEqual(1, gm.Save.wipes);
            Assert.AreEqual(2, gm.Save.knockouts, "기절도 감점 (D9)");
            Assert.AreEqual(before + gm.LastSettlement.net, gm.Save.money);
            Assert.IsNotEmpty(gm.LastSettlement.notice, "생태 현황 고시문이 나와야 한다");
            var ws = gm.Save.GetWorld(47);
            Assert.AreEqual(2, ws.generation, "세대가 넘어가야 한다");
            gm.EnterHub();
            Assert.AreEqual(GameMode.Hub, gm.Mode);
        }

        [UnityTest]
        public IEnumerator SuccessPaysFullContract()
        {
            int before = gm.Save.money;
            var contract = CallContract.Create(12, new System.Random(2));
            gm.StartCall(contract);
            yield return null;
            var mc = MissionController.Instance;
            mc.Ledger.midReached = true;
            mc.Ledger.objectiveDone = true;
            mc.Finish(CallOutcome.Success);
            yield return null;

            var s = gm.LastSettlement;
            Assert.AreEqual(GameMode.Settlement, gm.Mode);
            Assert.AreEqual(contract.Total, s.income);
            Assert.AreEqual(before + s.net, gm.Save.money);
            Assert.AreEqual(1, gm.Save.cleanStreak);
        }

        [UnityTest]
        public IEnumerator HeadshotsBreedHeadArmor()
        {
            // 인간선택: 머리만 쏘면 머리 장갑이 두꺼워져야 한다
            var spec = WorldGenerator.Generate(200);
            var ws = WorldGenerator.CreateInitialState(spec);
            float Mean() { float t = 0; foreach (var g in ws.crawlers) t += g.headArmor; return t / ws.crawlers.Count; }
            float start = Mean();
            var save = new SaveData();
            for (int gen = 0; gen < 6; gen++)
            {
                var evo = new Evolution();
                evo.headKills = 6;
                // 장갑 얇은 개체가 죽는다
                ws.crawlers.Sort((a, b) => a.headArmor.CompareTo(b.headArmor));
                var killedField = typeof(Evolution).GetField("killed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var killed = (System.Collections.Generic.HashSet<int>)killedField.GetValue(evo);
                for (int i = 0; i < 6; i++) killed.Add(ws.crawlers[i].id);
                evo.Advance(ws, spec, save);
                save.day++;
            }
            Debug.Log($"[Test] 머리 장갑 평균 {start:0.00} → {Mean():0.00}");
            Assert.Greater(Mean(), start + 0.1f);
            yield return null;
        }
    }
}
