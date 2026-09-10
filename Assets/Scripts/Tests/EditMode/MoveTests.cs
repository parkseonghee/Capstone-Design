using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>WEEK1_MOVEMENT_FALLING.md §3.</summary>
    public sealed class MoveTests
    {
        /// <summary>8열 x 9행(프리뷰 1 + 플레이 8) — 확정 규격.</summary>
        private static FakeBoardConfig BoardConfig() => new FakeBoardConfig();

        private static BlockingMoveResolver Build(
            BoardGrid grid, IBoardConfig config, Vector2Int start, out Player player)
        {
            player = Make.Player();
            grid.Place(player, start);
            return new BlockingMoveResolver(grid, player, config);
        }

        [Test]
        public void MovesOneCellInEachDirection()
        {
            FakeBoardConfig config = BoardConfig();
            var grid = new BoardGrid(config.Cols, config.Rows);
            var resolver = Build(grid, config, new Vector2Int(4, 5), out Player player);

            Assert.AreEqual(MoveOutcome.Moved, resolver.Resolve(Direction.Up).Outcome);
            Assert.AreEqual(new Vector2Int(4, 4), player.Position);

            Assert.AreEqual(MoveOutcome.Moved, resolver.Resolve(Direction.Down).Outcome);
            Assert.AreEqual(new Vector2Int(4, 5), player.Position);

            Assert.AreEqual(MoveOutcome.Moved, resolver.Resolve(Direction.Left).Outcome);
            Assert.AreEqual(new Vector2Int(3, 5), player.Position);

            Assert.AreEqual(MoveOutcome.Moved, resolver.Resolve(Direction.Right).Outcome);
            Assert.AreEqual(new Vector2Int(4, 5), player.Position);
        }

        [Test]
        public void OutOfBounds_LeavesPlayerInPlace()
        {
            FakeBoardConfig config = BoardConfig();
            var grid = new BoardGrid(config.Cols, config.Rows);
            var resolver = Build(grid, config, new Vector2Int(0, 8), out Player player);

            Assert.AreEqual(MoveOutcome.OutOfBounds, resolver.Resolve(Direction.Left).Outcome);
            Assert.AreEqual(MoveOutcome.OutOfBounds, resolver.Resolve(Direction.Down).Outcome);
            Assert.AreEqual(new Vector2Int(0, 8), player.Position);
        }

        [Test]
        public void PreviewRow_IsNotEnterable()
        {
            // §1: 프리뷰 줄은 "다음에 뭐가 오는지"만 보여주는 버퍼다. 플레이어 상호작용 X.
            FakeBoardConfig config = BoardConfig();
            var grid = new BoardGrid(config.Cols, config.Rows);
            var resolver = Build(grid, config, new Vector2Int(4, 1), out Player player);

            Assert.AreEqual(1, config.FirstPlayableRow, "row 0이 프리뷰, row 1이 첫 플레이 줄이다.");
            Assert.AreEqual(MoveOutcome.OutOfBounds, resolver.Resolve(Direction.Up).Outcome);
            Assert.AreEqual(new Vector2Int(4, 1), player.Position, "프리뷰 줄로 올라가면 안 된다.");
        }

        [Test]
        public void PreviewRow_IsNotEnterableEvenWhenEmpty()
        {
            // 빈 칸이라 '막힘'이 아니라 '무효 입력'으로 처리돼야 한다 —
            // 그래야 AdvanceOnBlocked를 켜도 보드가 진행되지 않는다(§3).
            FakeBoardConfig config = BoardConfig();
            config.AdvanceOnBlocked = true;

            var grid = new BoardGrid(config.Cols, config.Rows);
            var resolver = Build(grid, config, new Vector2Int(2, 1), out Player _);

            Assert.IsTrue(grid.IsEmpty(new Vector2Int(2, 0)), "프리뷰 칸은 비어 있다.");
            Assert.AreEqual(MoveOutcome.OutOfBounds, resolver.Resolve(Direction.Up).Outcome);
        }

        [Test]
        public void Trash_BlocksTheMove()
        {
            FakeBoardConfig config = BoardConfig();
            var grid = new BoardGrid(config.Cols, config.Rows);
            var resolver = Build(grid, config, new Vector2Int(4, 5), out Player player);
            grid.Place(Make.Trash(TrashType.Glass), new Vector2Int(5, 5));

            Assert.AreEqual(MoveOutcome.BlockedByEntity, resolver.Resolve(Direction.Right).Outcome);
            Assert.AreEqual(new Vector2Int(4, 5), player.Position);
        }
    }
}
