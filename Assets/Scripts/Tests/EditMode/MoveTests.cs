using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>WEEK1_MOVEMENT_FALLING.md §3.</summary>
    public sealed class MoveTests
    {
        private static BlockingMoveResolver Build(BoardGrid grid, Vector2Int start, out Player player)
        {
            player = new Player();
            grid.Place(player, start);
            return new BlockingMoveResolver(grid, player);
        }

        [Test]
        public void MovesOneCellInEachDirection()
        {
            var grid = new BoardGrid(8, 12);
            var resolver = Build(grid, new Vector2Int(4, 6), out Player player);

            Assert.AreEqual(MoveOutcome.Moved, resolver.Resolve(Direction.Up));
            Assert.AreEqual(new Vector2Int(4, 5), player.Position);

            Assert.AreEqual(MoveOutcome.Moved, resolver.Resolve(Direction.Down));
            Assert.AreEqual(new Vector2Int(4, 6), player.Position);

            Assert.AreEqual(MoveOutcome.Moved, resolver.Resolve(Direction.Left));
            Assert.AreEqual(new Vector2Int(3, 6), player.Position);

            Assert.AreEqual(MoveOutcome.Moved, resolver.Resolve(Direction.Right));
            Assert.AreEqual(new Vector2Int(4, 6), player.Position);
        }

        [Test]
        public void OutOfBounds_LeavesPlayerInPlace()
        {
            var grid = new BoardGrid(8, 12);
            var resolver = Build(grid, new Vector2Int(0, 0), out Player player);

            Assert.AreEqual(MoveOutcome.OutOfBounds, resolver.Resolve(Direction.Left));
            Assert.AreEqual(MoveOutcome.OutOfBounds, resolver.Resolve(Direction.Up));
            Assert.AreEqual(new Vector2Int(0, 0), player.Position);
        }

        [Test]
        public void Trash_BlocksTheMove()
        {
            var grid = new BoardGrid(8, 12);
            var resolver = Build(grid, new Vector2Int(4, 6), out Player player);
            grid.Place(new Trash(TrashType.C), new Vector2Int(5, 6));

            Assert.AreEqual(MoveOutcome.BlockedByEntity, resolver.Resolve(Direction.Right));
            Assert.AreEqual(new Vector2Int(4, 6), player.Position);
        }
    }
}
