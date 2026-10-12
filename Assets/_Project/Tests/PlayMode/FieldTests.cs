using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Manyworld.Tests
{
    /// <summary>자연 월드 첫 현장: 숲속의 빈 방과 곰인형, 차로 돌아와 콜 대기.</summary>
    public class FieldTests
    {
        GameManager gm;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveData.PathOverride = Path.Combine(Application.temporaryCachePath, "manyworld_field_save.json");
            yield return null;
            gm = GameManager.Instance;
            gm.ResetSave();
            gm.StartField();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            gm.ResetSave();
            gm.EnterHub();
            SaveData.PathOverride = null;
            yield return null;
        }

        MissionController Mc => MissionController.Instance;

        /// <summary>방 앞 숲길에서 방 안쪽을 보는 자리.</summary>
        static (Vector3 pos, float yaw) LookIntoRoom(MissionController mc, float back)
        {
            var room = mc.Room.transform;
            var p = room.position - room.forward * (ForestSiteBuilder.RoomHalf + back);
            p.y = mc.Layout.terrain.SampleHeight(p) + 0.1f;
            return (p, room.eulerAngles.y);
        }

        [UnityTest]
        public IEnumerator GameStartsInForestSite()
        {
            Assert.AreEqual(GameMode.Mission, gm.Mode);
            Assert.IsTrue(Mc.Field, "숲 현장으로 시작한다");
            Assert.AreEqual(6, Mc.Room.Bears.Count, "벽 밑 곰인형 여섯");
            Assert.IsTrue(Mc.Room.prize.activeSelf, "의자 위 곰인형");
            Assert.IsNotNull(Mc.Car);
            Assert.Greater(Mc.Monsters.Count, 0, "숲에는 몬스터가 있다");
            Assert.Less(Vector3.Distance(Mc.Player.transform.position, Mc.Car.transform.position), 8f, "차 옆에서 시작한다");

            // 차에서 방 입구까지 걸어갈 수 있다 (숲길과 열린 면이 막히지 않았다)
            var path = new NavMeshPath();
            NavMesh.SamplePosition(Mc.Layout.ticoPos, out var a, 6f, NavMesh.AllAreas);
            NavMesh.SamplePosition(Mc.Layout.objectivePos, out var b, 3f, NavMesh.AllAreas);
            Assert.IsTrue(NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "차 → 숲길 → 방 → 의자까지 이어진다");

            if (ArtCatalog.HasCharacters && ArtCatalog.Instance.driverModel.name.StartsWith("Character_"))
                Assert.IsNotNull(Mc.Player.GetComponentInChildren<Animator>(), "신티 인물 모델");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BearsOnlyMoveWhenNobodyLooks()
        {
            var mc = Mc;
            var (p, yaw) = LookIntoRoom(mc, 9f);
            // 은주는 멀리 다른 쪽을 보게 둔다
            mc.WarpParty(p, yaw, mc.Layout.ticoPos + Vector3.right * 3f);
            mc.Companion.transform.rotation = Quaternion.LookRotation(-mc.Room.transform.forward);
            mc.Player.health.Init(100000f);
            yield return new WaitForSeconds(0.5f);

            var watched = new List<(TeddyRoom.Bear b, Vector3 pos)>();
            foreach (var b in mc.Room.Bears)
                if (mc.Room.Seen(b, mc)) watched.Add((b, b.t.position));
            Assert.Greater(watched.Count, 0, "입구에서 방 안의 인형이 보인다");
            foreach (var b in mc.Room.Bears) b.nextMove = 0f;
            yield return new WaitForSeconds(5f);
            foreach (var (b, pos) in watched)
                if (mc.Room.Seen(b, mc)) Assert.Less(Vector3.Distance(b.t.position, pos), 0.01f, "보는 동안엔 움직이지 않는다");

            // 등을 돌리면 움직인다
            int before = mc.Room.MovesWhileUnseen;
            mc.Player.cam.yaw = yaw + 180f;
            foreach (var b in mc.Room.Bears) b.nextMove = 0f;
            yield return new WaitForSeconds(6f);
            Assert.Greater(mc.Room.MovesWhileUnseen, before, "안 보면 움직인다");
        }

        [UnityTest]
        public IEnumerator TakingPrizeClosesRoomAndDowningBearsReopensIt()
        {
            var mc = Mc;
            var c = mc.Room.transform.position + mc.Room.transform.forward * 2f;
            mc.WarpParty(c + Vector3.up * 0.2f, mc.Room.transform.eulerAngles.y);
            mc.Player.health.Init(100000f);
            mc.Companion.health.Init(100000f);
            mc.CompleteFieldObjective();
            Assert.IsFalse(mc.Room.prize.activeSelf, "곰인형을 집었다");
            yield return new WaitForSeconds(11f);
            Assert.AreEqual(TeddyRoom.Phase.Closed, mc.Room.State, "열린 면에 나무가 솟아 방이 닫힌다");
            Assert.AreEqual(1f, mc.Room.WallRaise, 0.001f);
            Assert.IsTrue(mc.Room.Inside(mc.Player.transform.position), "기사는 방 안에 갇혔다");

            foreach (var b in mc.Room.Bears) b.health.TakeDamage(new DamageInfo { amount = 999, source = DamageSource.PlayerGun });
            Assert.AreEqual(0, mc.Room.BearsStanding);
            yield return new WaitForSeconds(6.5f);
            Assert.AreEqual(TeddyRoom.Phase.Calm, mc.Room.State, "인형을 다 쓰러뜨리면 벽이 가라앉는다");
            Assert.AreEqual(0f, mc.Room.WallRaise, 0.001f);
        }

        [UnityTest]
        public IEnumerator BackInCarWaitsForNextCall()
        {
            var mc = Mc;
            mc.CompleteFieldObjective();
            var car = mc.Car.transform;
            mc.WarpParty(car.position - car.right * 2.2f + Vector3.up * 0.3f, car.eulerAngles.y, car.position + car.right * 2.2f);
            yield return null;
            mc.EnterCar();
            Assert.IsTrue(mc.FieldWaiting, "현장을 마치고 차에 타면 콜을 기다린다");
            Assert.IsFalse(mc.CallOffered);
            yield return new WaitForSeconds(8.5f);
            Assert.IsTrue(mc.CallOffered, "띠리릭─ 다음 콜");
            mc.AcceptNextCall();
            yield return null;
            Assert.AreNotSame(mc, Mc, "같은 월드에서 다음 현장이 시작된다");
            Assert.IsTrue(Mc.Field);
            Assert.IsFalse(Mc.ObjectiveDone);
            Assert.AreEqual(GameMode.Mission, gm.Mode);
        }

        [UnityTest]
        public IEnumerator OldFlowIsStillReachable()
        {
            gm.EnterHub();
            yield return null;
            Assert.AreEqual(GameMode.Hub, gm.Mode);
            Assert.IsNull(MissionController.Instance, "숲 현장을 치우고 기사식당으로");
            gm.StartField();
            yield return null;
            Assert.IsTrue(Mc.Field);
        }
    }
}
