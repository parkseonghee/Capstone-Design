using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 시작 연출: 초기 줄이 <b>한 줄씩</b> 나오고 <b>한 칸씩</b> 내려와 정착한 뒤,
    /// 다 끝나면 플레이어가 등장한다.
    /// 시간은 Unity 계층이 재고, 여기서는 순서와 상태만 검사한다.
    /// </summary>
    public sealed class IntroSequenceTests
    {
        private static FakeBoardConfig IntroConfig()
        {
            return new FakeBoardConfig
            {
                Cols = 4,
                Rows = 6,
                Mode = SpawnMode.SingleBlock,
                SpawnPerStep = 1,
                GapsPerRow = 0,
                RowsOnStart = 3,
                PlayerStart = new Vector2Int(2, 1),
            };
        }

        /// <summary>GameSession이 타이머로 하는 일을 테스트에서 즉시 돌린다.</summary>
        private static void SeedAndSettleOneRow(GameLoop loop)
        {
            loop.SeedNextRow();
            while (loop.TickGravity() > 0)
            {
            }
        }

        [Test]
        public void CreateStaged_StartsWithAnEmptyBoardAndNoPlayer()
        {
            GameLoop loop = GameLoopFactory.CreateStaged(IntroConfig(), new MinRandom());

            Assert.AreEqual(24, loop.Grid.CountEmpty(), "보드가 완전히 비어 있어야 한다.");
            Assert.AreEqual(3, loop.PendingSeedRows);
            Assert.IsFalse(loop.IsPlayerPlaced);
            Assert.IsFalse(loop.IsReady);
        }

        [Test]
        public void SeedNextRow_PutsTheRowOnTopWithoutDroppingIt()
        {
            GameLoop loop = GameLoopFactory.CreateStaged(IntroConfig(), new MinRandom());

            Assert.AreEqual(4, loop.SeedNextRow(), "한 번에 한 줄(4칸)만 나온다.");
            Assert.AreEqual(2, loop.PendingSeedRows);

            for (int col = 0; col < 4; col++)
            {
                Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(col, 0)), "줄은 아직 최상단에 있다.");
                Assert.IsTrue(loop.Grid.IsEmpty(new Vector2Int(col, 5)), "낙하는 TickGravity가 시킨다.");
            }
        }

        [Test]
        public void TickGravity_LowersTheIntroRowOneCellAtATime()
        {
            GameLoop loop = GameLoopFactory.CreateStaged(IntroConfig(), new MinRandom());
            loop.SeedNextRow();

            for (int row = 1; row <= 5; row++)
            {
                Assert.AreEqual(4, loop.TickGravity(), "줄 전체가 한 칸씩 내려간다.");
                for (int col = 0; col < 4; col++)
                {
                    Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(col, row)), "row " + row + "에 와 있어야 한다.");
                }
            }

            Assert.AreEqual(0, loop.TickGravity(), "바닥에 닿으면 멈춘다.");
        }

        [Test]
        public void RowsStackUp_OneAfterAnother()
        {
            GameLoop loop = GameLoopFactory.CreateStaged(IntroConfig(), new MinRandom());

            SeedAndSettleOneRow(loop);
            for (int col = 0; col < 4; col++)
            {
                Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(col, 5)));
                Assert.IsTrue(loop.Grid.IsEmpty(new Vector2Int(col, 4)), "두 번째 줄은 아직 없다.");
            }

            SeedAndSettleOneRow(loop);
            for (int col = 0; col < 4; col++)
            {
                Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(col, 4)), "두 번째 줄이 그 위에 쌓인다.");
            }

            SeedAndSettleOneRow(loop);
            Assert.AreEqual(0, loop.PendingSeedRows);
            Assert.AreEqual(0, loop.SeedNextRow(), "남은 줄이 없으면 아무 일도 하지 않는다.");
        }

        [Test]
        public void PlayerAppearsOnlyAfterEveryRowHasLanded()
        {
            GameLoop loop = GameLoopFactory.CreateStaged(IntroConfig(), new MinRandom());

            SeedAndSettleOneRow(loop);
            Assert.IsFalse(loop.IsPlayerPlaced, "줄이 남았는데 플레이어가 나오면 안 된다.");

            SeedAndSettleOneRow(loop);
            SeedAndSettleOneRow(loop);
            Assert.AreEqual(0, loop.PendingSeedRows);

            Assert.AreEqual(new Vector2Int(2, 1), loop.PlacePlayer());
            Assert.IsTrue(loop.IsReady);

            // 플레이어가 나중에 오므로 모든 컬럼이 바닥까지 고르게 찬다.
            for (int col = 0; col < 4; col++)
            {
                Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(col, 5)));
            }
        }

        [Test]
        public void StepsAreRejectedWhileTheIntroIsStillRunning()
        {
            GameLoop loop = GameLoopFactory.CreateStaged(IntroConfig(), new MinRandom());

            StepResult duringIntro = loop.Step(Direction.Left);

            Assert.IsFalse(duringIntro.Advanced);
            Assert.AreEqual(0, loop.StepCount);

            loop.CompleteSetup();
            Assert.IsTrue(loop.Step(Direction.Left).Advanced, "준비가 끝나면 입력을 받는다.");
        }

        [Test]
        public void CompleteSetup_MatchesRowByRowSeeding()
        {
            // 연출을 켜든 끄든 최종 보드는 같아야 한다.
            GameLoop staged = GameLoopFactory.CreateStaged(IntroConfig(), new SystemRandomSource(99));
            while (staged.PendingSeedRows > 0)
            {
                SeedAndSettleOneRow(staged);
            }

            staged.PlacePlayer();

            GameLoop instant = GameLoopFactory.CreateWeek1(IntroConfig(), new SystemRandomSource(99));

            Assert.AreEqual(Dump(instant), Dump(staged));
        }

        [Test]
        public void OngoingSpawn_AddsOneBlockPerMove_NotAWholeRow()
        {
            var config = IntroConfig();
            config.Cols = 8;
            config.Rows = 12;
            config.RowsOnStart = 3;
            config.PlayerStart = new Vector2Int(4, 6);

            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());
            int afterIntro = loop.Grid.CountEmpty();

            StepResult first = loop.Step(Direction.Left);

            Assert.AreEqual(1, first.Spawned, "이동 1회에 쓰레기 1개만 들어온다.");
            Assert.AreEqual(afterIntro - 1, loop.Grid.CountEmpty());
        }

        private static string Dump(GameLoop loop)
        {
            var builder = new System.Text.StringBuilder();
            for (int row = 0; row < loop.Grid.Rows; row++)
            {
                for (int col = 0; col < loop.Grid.Cols; col++)
                {
                    Entity entity = loop.Grid[col, row];
                    if (entity == null) builder.Append('.');
                    else if (entity.Kind == EntityKind.Player) builder.Append('@');
                    else builder.Append((char)('A' + (int)((Trash)entity).Type));
                }

                builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
