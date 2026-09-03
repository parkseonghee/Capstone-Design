using System.Text;
using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>WEEK1_MOVEMENT_FALLING.md §2(4페이즈 순서) · §6(게임오버) · §7-2(진행 조건).</summary>
    public sealed class GameLoopTests
    {
        [Test]
        public void PhasesRunInDocumentedOrder_SpawnHappensAfterGravity()
        {
            // 스폰이 중력보다 먼저 돌면 새 쓰레기가 곧바로 바닥에 박힌다.
            // 문서 순서(중력 → 스폰)라면 새 쓰레기는 이번 스텝에는 상단에 남아 있어야 한다.
            var config = new FakeBoardConfig();
            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());

            StepResult first = loop.Step(Direction.Up);

            Assert.IsTrue(first.Advanced);
            Assert.AreEqual(1, first.Spawned);
            Assert.AreEqual(0, first.Settled);
            Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(0, 0)), "새 쓰레기는 스폰된 자리에 그대로 있어야 한다.");
            Assert.IsTrue(loop.Grid.IsEmpty(new Vector2Int(0, 11)));

            // 다음 스텝에 그 쓰레기가 딱 한 칸 내려오고, 빈 상단에 새 쓰레기가 들어온다.
            StepResult second = loop.Step(Direction.Up);

            Assert.AreEqual(1, second.Settled);
            Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(0, 1)), "한 칸만 내려와야 한다.");
            Assert.IsTrue(loop.Grid.IsEmpty(new Vector2Int(0, 11)), "바닥까지 순간이동하면 안 된다.");
            Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(0, 0)), "그 사이 새 쓰레기가 다시 스폰된다.");
        }

        [Test]
        public void FloatingTrash_TakesOneStepPerCellToReachTheFloor()
        {
            var config = new FakeBoardConfig { SpawnPerStep = 0, PlayerStart = new Vector2Int(4, 11) };
            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());

            var trash = new Trash(TrashType.A);
            loop.Grid.Place(trash, new Vector2Int(0, 0));

            for (int i = 1; i <= 5; i++)
            {
                loop.Step(Direction.Up);
                Assert.AreEqual(new Vector2Int(0, i), trash.Position, i + "스텝 뒤에는 " + i + "칸만 내려와 있어야 한다.");
            }
        }

        [Test]
        public void OutOfBoundsInput_NeverAdvancesTheBoard()
        {
            // §7-2: 무효 입력(경계 밖)은 AdvanceOnBlocked와 무관하게 항상 무시한다.
            var config = new FakeBoardConfig { AdvanceOnBlocked = true, PlayerStart = new Vector2Int(0, 11) };
            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());

            StepResult result = loop.Step(Direction.Left);

            Assert.AreEqual(MoveOutcome.OutOfBounds, result.Move);
            Assert.IsFalse(result.Advanced);
            Assert.AreEqual(0, result.Spawned);
            Assert.AreEqual(0, loop.StepCount);
        }

        [Test]
        public void BlockedInput_DoesNotAdvance_WhenAdvanceOnBlockedIsFalse()
        {
            var config = new FakeBoardConfig { AdvanceOnBlocked = false, PlayerStart = new Vector2Int(4, 11) };
            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(5, 11));

            StepResult result = loop.Step(Direction.Right);

            Assert.AreEqual(MoveOutcome.BlockedByEntity, result.Move);
            Assert.IsFalse(result.Advanced);
            Assert.AreEqual(0, result.Spawned);
            Assert.AreEqual(0, loop.StepCount);
        }

        [Test]
        public void BlockedInput_Advances_WhenAdvanceOnBlockedIsTrue()
        {
            var config = new FakeBoardConfig { AdvanceOnBlocked = true, PlayerStart = new Vector2Int(4, 11) };
            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(5, 11));

            StepResult result = loop.Step(Direction.Right);

            Assert.AreEqual(MoveOutcome.BlockedByEntity, result.Move);
            Assert.IsTrue(result.Advanced);
            Assert.AreEqual(1, result.Spawned);
            Assert.AreEqual(1, loop.StepCount);
        }

        [Test]
        public void TrashAccumulatesOneStepAtATime()
        {
            var config = new FakeBoardConfig();
            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());

            for (int i = 0; i < 5; i++)
            {
                loop.Step(Direction.Up);
            }

            // 플레이어 1 + 쓰레기 5
            Assert.AreEqual(config.Cols * config.Rows - 6, loop.Grid.CountEmpty());
            Assert.AreEqual(5, loop.StepCount);
        }

        [Test]
        public void BoardFull_EndsTheRun()
        {
            // 1 x 3 보드: 두 스텝이면 정확히 꽉 찬다.
            var config = new FakeBoardConfig { Cols = 1, Rows = 3, PlayerStart = new Vector2Int(0, 2) };
            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());

            Assert.AreEqual(GameOverReason.None, loop.Step(Direction.Up).GameOver);

            StepResult second = loop.Step(Direction.Down);

            Assert.AreEqual(GameOverReason.BoardFull, second.GameOver);
            Assert.IsTrue(loop.IsOver);
            Assert.AreEqual(0, loop.Grid.CountEmpty());
        }

        [Test]
        public void TrappedPlayer_IsDetectedEvenThoughTheBoardNeverAdvances()
        {
            // AdvanceOnBlocked = false면 갇힌 플레이어의 입력이 전부 거부되어
            // 페이즈 4가 영영 돌지 않는다. GameLoop이 거부 시점에 갇힘을 따로 본다.
            var config = new FakeBoardConfig { Cols = 3, Rows = 3, PlayerStart = new Vector2Int(1, 1), AdvanceOnBlocked = false };
            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());

            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(0, 1));
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(2, 1));
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(1, 0));
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(1, 2));

            StepResult result = loop.Step(Direction.Right);

            Assert.IsFalse(result.Advanced, "보드는 진행하지 않는다.");
            Assert.AreEqual(GameOverReason.PlayerTrapped, result.GameOver);
            Assert.IsTrue(loop.IsOver);
        }

        [Test]
        public void StepsAfterGameOver_AreRejected()
        {
            var config = new FakeBoardConfig { Cols = 1, Rows = 3, PlayerStart = new Vector2Int(0, 2) };
            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());
            loop.Step(Direction.Up);
            loop.Step(Direction.Down);
            Assert.IsTrue(loop.IsOver);

            int stepsBefore = loop.StepCount;
            StepResult afterOver = loop.Step(Direction.Up);

            Assert.IsFalse(afterOver.Advanced);
            Assert.AreEqual(stepsBefore, loop.StepCount);
        }

        [Test]
        public void SameSeed_ProducesTheSameBoard()
        {
            // WEEK1 §0의 완료 기준: "루프가 결정론적으로 도는가".
            Direction[] script =
            {
                Direction.Left, Direction.Up, Direction.Right, Direction.Up, Direction.Up,
                Direction.Left, Direction.Down, Direction.Right, Direction.Up, Direction.Left,
            };

            Assert.AreEqual(RunScripted(script, 20260903), RunScripted(script, 20260903));
            Assert.AreNotEqual(RunScripted(script, 20260903), RunScripted(script, 1));
        }

        private static string RunScripted(Direction[] script, int seed)
        {
            var config = new FakeBoardConfig();
            GameLoop loop = GameLoopFactory.CreateWeek1(config, new SystemRandomSource(seed));

            foreach (Direction direction in script)
            {
                loop.Step(direction);
            }

            var builder = new StringBuilder();
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
