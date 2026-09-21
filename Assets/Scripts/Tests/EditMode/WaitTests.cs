using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 제자리 대기(빨리 내리기). 플레이어는 움직이지 않지만 <b>한 턴은 그대로 쓴다</b> —
    /// 중력·스폰·폭탄 도화선이 평소대로 돈다.
    /// </summary>
    public sealed class WaitTests
    {
        private static FakeBoardConfig Board()
            => new FakeBoardConfig
            {
                RowsOnStart = 0,
                BlocksPerSpawn = 0,
                PlayerStart = new Vector2Int(4, 5),
            };

        private static GameLoop Loop(FakeBoardConfig config)
            => GameLoopFactory.CreateWeek1(config, config, config, config, config, new MinRandom());

        // ── 기본 ─────────────────────────────────────────────────────────

        [Test]
        public void WaitingAdvancesTheBoard()
        {
            GameLoop loop = Loop(Board());
            int before = loop.StepCount;

            StepResult result = loop.Wait();

            Assert.AreEqual(MoveOutcome.Waited, result.Move);
            Assert.IsTrue(result.Advanced, "대기도 한 턴을 쓰는 정식 행동이다");
            Assert.AreEqual(before + 1, loop.StepCount);
        }

        [Test]
        public void ThePlayerDoesNotMove()
        {
            GameLoop loop = Loop(Board());
            Vector2Int before = loop.Player.Position;

            loop.Wait();

            Assert.AreEqual(before, loop.Player.Position);
        }

        [Test]
        public void BlocksFallOneCellPerWait()
        {
            var config = Board();
            GameLoop loop = Loop(config);

            // 플레이어에게서 떨어진 컬럼 꼭대기에 블록을 하나 띄운다.
            var start = new Vector2Int(0, config.FirstPlayableRow);
            loop.Grid.Place(Make.Trash(TrashType.Paper), start);

            loop.Wait();
            Assert.IsNull(loop.Grid[start], "대기 한 번에 원래 칸은 비어야 한다");
            Assert.IsNotNull(loop.Grid[new Vector2Int(0, start.y + 1)], "한 칸 내려와야 한다");

            loop.Wait();
            Assert.IsNotNull(loop.Grid[new Vector2Int(0, start.y + 2)], "또 한 칸 내려와야 한다");
        }

        [Test]
        public void WaitingDoesNotAttackOrEarnGold()
        {
            var config = Board();
            GameLoop loop = Loop(config);

            // 바로 아래 적이 있어도 대기는 때리지 않는다.
            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(new Trash(TrashType.Paper, new TrashStats(1, 1, 0, 0, true, 9)), below);

            StepResult result = loop.Wait();

            Assert.AreEqual(0, result.Killed, "대기는 공격이 아니다");
            Assert.AreEqual(0, result.Gold);
            Assert.AreEqual(0, loop.Player.Gold);
        }

        [Test]
        public void WaitingTakesNoCounterDamage()
        {
            var config = Board();
            GameLoop loop = Loop(config);
            int hpBefore = loop.Player.Hp;

            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(Make.Trash(TrashType.Paper, maxHp: 9, attack: 3), below);

            loop.Wait();

            Assert.AreEqual(hpBefore, loop.Player.Hp, "때리지 않았으니 반격도 없다");
        }

        // ── 공짜가 아니다 ────────────────────────────────────────────────

        [Test]
        public void WaitingStillSpawns()
        {
            // 중력만 공짜로 돌려 주면 스폰 압박 없이 판을 정리할 수 있게 된다.
            var config = Board();
            config.BlocksPerSpawn = 1;
            config.TurnsPerSpawnEarly = 1;

            GameLoop loop = Loop(config);

            StepResult result = loop.Wait();

            Assert.AreEqual(1, result.Spawned, "대기해도 새 블록은 내려온다");
        }

        [Test]
        public void WaitingBurnsBombFuses()
        {
            var config = Board();
            config.FuseTurns = 2;
            config.DamagesPlayer = false;

            GameLoop loop = Loop(config);
            loop.Player.AddBombs(1);

            var spot = new Vector2Int(loop.Player.Position.x - 1, loop.Player.Position.y);
            loop.PlaceBomb(spot);
            loop.Step(Direction.Left);          // 때려서 점화

            int guard = 0;
            while (!loop.Wait().BombsExploded.Equals(1) && guard++ < 10)
            {
            }

            Assert.Less(guard, 10, "대기만으로도 도화선이 타서 결국 터져야 한다");
        }

        // ── 거부되는 경우 ────────────────────────────────────────────────

        [Test]
        public void WaitingBeforeTheRunIsReadyDoesNothing()
        {
            var config = Board();
            config.RowsOnStart = 2;

            GameLoop loop = GameLoopFactory.CreateStaged(
                config, config, config, config, config, new MinRandom());

            StepResult result = loop.Wait();

            Assert.IsFalse(result.Advanced, "인트로 중에는 턴이 넘어가면 안 된다");
            Assert.AreEqual(0, loop.StepCount);
        }

        [Test]
        public void WaitingAfterGameOverDoesNothing()
        {
            var config = Board();
            GameLoop loop = Loop(config);

            // 반격으로 죽인다.
            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(Make.Trash(TrashType.Paper, maxHp: 99, attack: 99), below);
            loop.Step(Direction.Down);
            Assert.IsTrue(loop.IsOver, "먼저 죽어 있어야 하는 전제");

            int stepsBefore = loop.StepCount;
            StepResult result = loop.Wait();

            Assert.IsFalse(result.Advanced);
            Assert.AreEqual(stepsBefore, loop.StepCount);
        }

        // ── 웨이브 ───────────────────────────────────────────────────────

        [Test]
        public void WaitingDoesNotFillTheWaveBar()
        {
            // 진행도는 적 처치로만 찬다(밸런싱 v1 §2-1-4). 대기로 클리어할 수 있으면 안 된다.
            var config = Board();
            var runner = new WaveRunner(new IWaveConfig[] { new FakeWaveConfig(3) });

            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom(), runner);

            for (int i = 0; i < 20; i++)
            {
                loop.Wait();
            }

            Assert.AreEqual(0, runner.Progress);
        }

        private sealed class FakeWaveConfig : IWaveConfig
        {
            public FakeWaveConfig(int killGoal) { KillGoal = killGoal; }

            public int StageNumber => 1;

            public int WaveNumber => 1;

            public int KillGoal { get; }

            public int SpawnWeightFor(TrashType type) => 0;
        }
    }
}
