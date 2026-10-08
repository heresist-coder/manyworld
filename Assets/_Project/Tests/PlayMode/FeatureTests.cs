using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Manyworld.Tests
{
    /// <summary>동네 운전, 기절 사건(카세트), 사인 도감, 가스 고임, 사운드 리소스.</summary>
    public class FeatureTests
    {
        GameManager gm;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveData.PathOverride = Path.Combine(Application.temporaryCachePath, "manyworld_feature_save.json");
            yield return null;
            gm = GameManager.Instance;
            gm.ResetSave();
            if (gm.Mode != GameMode.Hub) gm.EnterHub();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (MissionController.Instance != null) Object.Destroy(MissionController.Instance.gameObject);
            var drive = Object.FindFirstObjectByType<DriveSession>();
            if (drive != null) Object.Destroy(drive.gameObject);
            gm.ResetSave();
            gm.EnterHub();
            SaveData.PathOverride = null;
            yield return null;
        }

        [Test]
        public void AllSoundClipsLoad()
        {
            string[] names =
            {
                "carbine_shot", "rifle_shot", "meter_tick", "reload", "pipe_hit", "crawler_screech", "brute_roar",
                "body_hit", "radio_call", "typewriter", "stamp", "chime", "cassette_play", "car_door", "car_bump",
                "gate_whoosh", "engine_loop", "ambience_world", "ambience_home", "tape_hiss", "heartbeat", "gas_hiss",
            };
            foreach (var n in names)
            {
                var c = Sfx.Clip(n);
                Assert.IsNotNull(c, n);
                Assert.Greater(c.length, 0.05f, n);
            }
        }

        [UnityTest]
        public IEnumerator DriveToGateStartsMissionWithFocus()
        {
            gm.BeginDrive(CallContract.Create(47, new System.Random(4)));
            yield return null;
            Assert.AreEqual(GameMode.Drive, gm.Mode);
            var drive = Object.FindFirstObjectByType<DriveSession>();
            Assert.IsNotNull(drive);
            float startY = drive.Car.transform.position.y;
            yield return new WaitForSeconds(1f);
            Assert.That(drive.Car.transform.position.y, Is.InRange(startY - 0.5f, startY + 0.5f), "티코가 땅을 뚫고 떨어지면 안 된다");
            Assert.Greater(drive.DistanceToGate, 20f, "기사식당 앞에서 출발한다");

            drive.Enter(true);
            yield return new WaitForSeconds(1.6f);
            Assert.AreEqual(GameMode.Mission, gm.Mode);
            Assert.IsTrue(MissionController.Instance.Companion.Focused, "곡을 다 들으면 은주가 집중 상태");
        }

        [UnityTest]
        public IEnumerator KnockoutLeavesCaseAndTape()
        {
            gm.StartCall(CallContract.Create(47, new System.Random(5)));
            yield return null;
            var mc = MissionController.Instance;
            var brute = mc.Monsters.Find(m => m.species == Species.Brute);
            mc.LogAudible("(테스트 소리)");
            mc.Player.health.TakeDamage(new DamageInfo { amount = 999, source = DamageSource.Monster, attacker = brute });
            yield return null;

            Assert.AreEqual(1, mc.NewCases.Count);
            var kc = mc.NewCases[0];
            Assert.AreEqual("brute", kc.causeId, "덩치에게 맞으면 사인은 덩치의 일격");
            Assert.IsTrue(kc.hasTape, "은주가 서 있었으니 녹음이 남는다");
            Assert.That(kc.tape.Exists(l => l.Contains("테스트 소리")));
            Assert.That(kc.choices, Has.Member("brute"));
            Assert.AreEqual(3, kc.choices.Count);

            mc.Finish(CallOutcome.CompanionSolo);
            yield return null;
            Assert.AreEqual(1, gm.Save.cases.Count);
            Assert.IsFalse(gm.Save.cases[0].solved);
            Assert.AreEqual(1, gm.Save.UnsolvedCases);

            // 도감에 기록되면 같은 사인은 다음부터 바로 풀린다
            gm.Save.UnlockCodex("brute", 47);
            gm.EnterHub();
            gm.StartCall(CallContract.Create(47, new System.Random(6)));
            yield return null;
            mc = MissionController.Instance;
            mc.Player.health.TakeDamage(new DamageInfo { amount = 999, source = DamageSource.Monster, attacker = mc.Monsters.Find(m => m.species == Species.Brute) });
            mc.Finish(CallOutcome.CompanionSolo);
            yield return null;
            Assert.IsTrue(gm.Save.cases[1].autoSolved);
        }

        [UnityTest]
        public IEnumerator DriveTicoInBigWorld()
        {
            gm.StartCall(CallContract.Create(47, new System.Random(8)));
            yield return null;
            var mc = MissionController.Instance;
            Assert.IsNotNull(mc.Layout.terrain, "큰 월드는 지형이다");
            Assert.Greater(mc.Layout.terrain.terrainData.treeInstanceCount, 1000, "자연물이 많아야 한다");
            yield return new WaitForSeconds(1f); // 서스펜션이 자리 잡게

            var cc = mc.Player.GetComponent<CharacterController>();
            cc.enabled = false;
            mc.Player.transform.position = mc.Car.transform.position + mc.Car.transform.right * 2f;
            cc.enabled = true;
            var comp = mc.Companion.GetComponent<UnityEngine.AI.NavMeshAgent>();
            comp.Warp(mc.Car.transform.position + mc.Car.transform.right * 3f);
            mc.EnterCar();
            Assert.IsTrue(mc.PlayerInCar);
            Assert.IsTrue(mc.Companion.InVehicle, "가까이 있으면 은주도 탄다");
            Assert.IsTrue(mc.Player.health.untargetable);

            var start = mc.Car.transform.position;
            mc.Car.externalInput = new Vector2(0f, 1f);
            yield return new WaitForSeconds(4f);
            mc.Car.externalInput = new Vector2(0f, -1f);
            yield return new WaitForSeconds(1.5f);
            mc.Car.externalInput = Vector2.zero;
            float moved = Vector3.Distance(start, mc.Car.transform.position);
            Debug.Log($"[Test] 티코 4초 주행 {moved:0.0}m, 최고 {mc.Car.SpeedKmh:0}km/h, 위쪽 {Vector3.Dot(mc.Car.transform.up, Vector3.up):0.00}");
            Assert.Greater(moved, 20f, "앞으로 나가야 한다");
            Assert.Greater(Vector3.Dot(mc.Car.transform.up, Vector3.up), 0.6f, "뒤집히면 안 된다");
            Assert.Less(Vector3.Distance(mc.Player.transform.position, mc.Car.transform.position), 2f, "기사는 차와 같이 움직인다");

            yield return new WaitForSeconds(1f);
            mc.ExitCar();
            yield return null;
            Assert.IsFalse(mc.PlayerInCar);
            Assert.IsFalse(mc.Companion.InVehicle);
            Assert.IsTrue(comp.isOnNavMesh, "내린 은주는 네비메시 위에 선다");
        }

        [UnityTest]
        public IEnumerator CompanionShootsFromPassengerSeat()
        {
            gm.StartCall(CallContract.Create(47, new System.Random(9)));
            yield return null;
            var mc = MissionController.Instance;
            yield return new WaitForSeconds(0.8f);
            var cc = mc.Player.GetComponent<CharacterController>();
            cc.enabled = false;
            mc.Player.transform.position = mc.Car.transform.position + mc.Car.transform.right * 2f;
            cc.enabled = true;
            mc.Companion.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(mc.Car.transform.position + mc.Car.transform.right * 3f);
            mc.EnterCar();
            Assert.IsTrue(mc.Companion.InVehicle);

            mc.Car.SetHeadlights(true);
            Assert.IsTrue(mc.Car.HeadlightsOn);

            // 차 앞에 몬스터를 세운다
            var m = mc.Monsters[0];
            var p = mc.Car.transform.position + mc.Car.transform.forward * 12f;
            UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas);
            m.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(hit.position);
            yield return new WaitForSeconds(5f);
            Debug.Log($"[Test] 조수석 사격 {mc.Ledger.companionShots}발");
            Assert.Greater(mc.Ledger.companionShots, 0, "은주는 조수석 창밖으로 쏜다");
        }

        [UnityTest]
        public IEnumerator GasPocketHurtsThroughFilter()
        {
            var contract = CallContract.Create(200, new System.Random(7));
            Assume.That(contract.spec.rules.HasFlag(WorldRule.ToxicGas));
            gm.StartCall(contract);
            yield return null;
            var mc = MissionController.Instance;
            Assert.Greater(mc.GasPockets.Count, 0);
            var pocket = mc.GasPockets[0];
            var cc = mc.Player.GetComponent<CharacterController>();
            cc.enabled = false;
            mc.Player.transform.position = pocket.transform.position + Vector3.up * 0.1f;
            cc.enabled = true;
            float hp = mc.Player.health.current;
            yield return new WaitForSeconds(1.2f);
            Assert.Less(mc.Player.health.current, hp, "필터가 있어도 가스 고임은 아프다");

            mc.Player.health.TakeDamage(new DamageInfo { amount = 999, source = DamageSource.Environment, cause = "gaspocket" });
            yield return null;
            Assert.AreEqual("gaspocket", mc.NewCases[0].causeId);
        }
    }
}
