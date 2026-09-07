using System.Text;
using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>WEEK1_MOVEMENT_FALLING.md §2(페이즈 순서) · §3(진행 조건) · §7(게임오버).</summary>
    public sealed class GameLoopTests
    {
        [Test]
        public void PhasesRunInDocumentedOrder_SpawnHappensAfterGravity()
        {
            // 스폰이 중력보다 먼저 돌면 프리뷰 칸이 막힌 채로 새 블록을 넣으려 한다.
            // 문서 순서(중력 → 스폰)라면 새 쓰레기는 이번 스텝에는 프리뷰 줄에 남아 있어야 한다.
            var config = new FakeBoardConfig();
            GameLoop loop = Make.Week1(config, new MinRandom());

            StepResult first = loop.Step(Direction.Up);

            Assert.IsTrue(first.Advanced);
            Assert.AreEqual(1, first.Spawned);
            Assert.AreEqual(0, first.Settled);
            Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(0, 0)), "새 쓰레기는 프리뷰 줄에 있다.");
            Assert.IsTrue(loop.Grid.IsEmpty(new Vector2Int(0, 8)));

            // 다음 스텝에 그 쓰레기가 딱 한 칸 내려와 플레이 영역으로 들어가고,
            // 비워진 프리뷰 칸에 새 쓰레기가 들어온다.
            StepResult second = loop.Step(Direction.Up);

            Assert.AreEqual(1, second.Settled);
            Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(0, 1)), "한 칸만 내려와야 한다.");
            Assert.IsTrue(loop.Grid.IsEmpty(new Vector2Int(0, 8)), "바닥까지 순간이동하면 안 된다.");
            Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(0, 0)), "그 사이 새 쓰레기가 다시 스폰된다.");
        }

        [Test]
        public void FloatingTrash_TakesOneStepPerCellToReachTheFloor()
        {
            // 기획 확정: 낙하는 턴당 한 칸. 공중에 뜬 블록이 존재하고 그 밑을 지나갈 수 있다.
            var config = new FakeBoardConfig { BlocksPerSpawn = 0, PlayerStart = new Vector2Int(4, 8) };
            GameLoop loop = Make.Week1(config, new MinRandom());

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
            // §3: 무효 입력(경계 밖)은 AdvanceOnBlocked와 무관하게 항상 무시한다.
            var config = new FakeBoardConfig { AdvanceOnBlocked = true, PlayerStart = new Vector2Int(0, 8) };
            GameLoop loop = Make.Week1(config, new MinRandom());

            StepResult result = loop.Step(Direction.Left);

            Assert.AreEqual(MoveOutcome.OutOfBounds, result.Move);
            Assert.IsFalse(result.Advanced);
            Assert.AreEqual(0, result.Spawned);
            Assert.AreEqual(0, loop.StepCount);
        }

        [Test]
        public void PreviewRowInput_NeverAdvancesTheBoard()
        {
            // 프리뷰 줄로의 이동도 무효 입력이다 — 보드가 공짜로 진행되면 안 된다.
            var config = new FakeBoardConfig { AdvanceOnBlocked = true, PlayerStart = new Vector2Int(4, 1) };
            GameLoop loop = Make.Week1(config, new MinRandom());

            StepResult result = loop.Step(Direction.Up);

            Assert.AreEqual(MoveOutcome.OutOfBounds, result.Move);
            Assert.IsFalse(result.Advanced);
            Assert.AreEqual(0, loop.StepCount);
            Assert.AreEqual(new Vector2Int(4, 1), loop.Player.Position);
        }

        [Test]
        public void BlockedInput_DoesNotAdvance_WhenAdvanceOnBlockedIsFalse()
        {
            var config = new FakeBoardConfig { AdvanceOnBlocked = false, PlayerStart = new Vector2Int(4, 8) };
            GameLoop loop = Make.Week1(config, new MinRandom());
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(5, 8));

            StepResult result = loop.Step(Direction.Right);

            Assert.AreEqual(MoveOutcome.BlockedByEntity, result.Move);
            Assert.IsFalse(result.Advanced);
            Assert.AreEqual(0, result.Spawned);
            Assert.AreEqual(0, loop.StepCount);
        }

        [Test]
        public void BlockedInput_Advances_WhenAdvanceOnBlockedIsTrue()
        {
            var config = new FakeBoardConfig { AdvanceOnBlocked = true, PlayerStart = new Vector2Int(4, 8) };
            GameLoop loop = Make.Week1(config, new MinRandom());
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(5, 8));

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
            GameLoop loop = Make.Week1(config, new MinRandom());

            for (int i = 0; i < 5; i++)
            {
                loop.Step(Direction.Up);
            }

            // 플레이어 1 + 쓰레기 5
            Assert.AreEqual(config.Cols * config.Rows - 6, loop.Grid.CountEmpty());
            Assert.AreEqual(5, loop.StepCount);
        }

        [Test]
        public void FullBoard_IsLostOnTheTurnTheNextBlockCannotBeCreated()
        {
            // §7 확정: "72칸이 모두 차고 다음 블록을 생성하려는 턴"에 패배.
            // 판정만 떼어 검사한다 — 루프로 이 상태를 만들면 그 전에 갇힘이 먼저 잡힌다(아래 주석).
            var config = new FakeBoardConfig { Cols = 2, PlayableRows = 2 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            var player = new Player();
            grid.Place(player, new Vector2Int(0, 2));

            for (int col = 0; col < config.Cols; col++)
            {
                for (int row = 0; row < config.Rows; row++)
                {
                    var cell = new Vector2Int(col, row);
                    if (grid.IsEmpty(cell))
                    {
                        grid.Place(new Trash(TrashType.A), cell);
                    }
                }
            }

            Assert.AreEqual(0, grid.CountEmpty(), "보드가 완전히 찼다.");

            var checker = new GameOverChecker(grid, player, config);

            Assert.AreEqual(GameOverReason.BoardFull, checker.Evaluate(true));

            // 꽉 찬 보드는 플레이어도 가둔다. 스폰이 막히지 않은 턴에는 '갇힘'으로 잡힌다.
            // 실전에서는 대개 이쪽(기획 스샷의 "빠져나갈 곳이 없습니다")이 먼저 뜬다.
            Assert.AreEqual(GameOverReason.PlayerTrapped, checker.Evaluate(false));
        }

        [Test]
        public void RoomLeft_SurvivesEvenWhenTheSpawnIsBlocked()
        {
            // 스폰이 막혔다고 바로 지는 게 아니다 — 빈 칸이 남아 있으면 그 턴은 그냥 넘어간다.
            var config = new FakeBoardConfig { Cols = 2, PlayableRows = 3 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            var player = new Player();
            grid.Place(player, new Vector2Int(0, 2));

            var checker = new GameOverChecker(grid, player, config);

            Assert.AreEqual(GameOverReason.None, checker.Evaluate(true));
        }

        [Test]
        public void TrappedPlayer_IsDetectedEvenThoughTheBoardNeverAdvances()
        {
            // AdvanceOnBlocked = false면 갇힌 플레이어의 입력이 전부 거부되어
            // 이후 페이즈가 영영 돌지 않는다. GameLoop이 거부 시점에 갇힘을 따로 본다.
            var config = new FakeBoardConfig
            {
                Cols = 3,
                PlayableRows = 2,
                PlayerStart = new Vector2Int(1, 1),
                AdvanceOnBlocked = false,
            };
            GameLoop loop = Make.Week1(config, new MinRandom());

            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(0, 1));
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(2, 1));
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(1, 2));

            StepResult result = loop.Step(Direction.Right);

            Assert.IsFalse(result.Advanced, "보드는 진행하지 않는다.");
            Assert.AreEqual(GameOverReason.PlayerTrapped, result.GameOver, "위는 프리뷰 줄이라 탈출구가 아니다.");
            Assert.IsTrue(loop.IsOver);
        }

        [Test]
        public void StepsAfterGameOver_AreRejected()
        {
            var config = new FakeBoardConfig
            {
                Cols = 3,
                PlayableRows = 2,
                PlayerStart = new Vector2Int(1, 1),
            };
            GameLoop loop = Make.Week1(config, new MinRandom());
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(0, 1));
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(2, 1));
            loop.Grid.Place(new Trash(TrashType.A), new Vector2Int(1, 2));

            loop.Step(Direction.Right);
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
            var config = new FakeBoardConfig { RowsOnStart = 3, PlayerStart = new Vector2Int(4, 5) };
            GameLoop loop = Make.Week1(config, new SystemRandomSource(seed));

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
