using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    public sealed class BoardGridTests
    {
        [Test]
        public void InBounds_RejectsOutsideCoordinates()
        {
            var grid = new BoardGrid(8, 12);

            Assert.IsTrue(grid.InBounds(new Vector2Int(0, 0)));
            Assert.IsTrue(grid.InBounds(new Vector2Int(7, 11)));
            Assert.IsFalse(grid.InBounds(new Vector2Int(-1, 0)));
            Assert.IsFalse(grid.InBounds(new Vector2Int(8, 0)));
            Assert.IsFalse(grid.InBounds(new Vector2Int(0, 12)));
        }

        [Test]
        public void Place_StoresEntityAndSyncsItsPosition()
        {
            var grid = new BoardGrid(8, 12);
            var trash = Make.Trash(TrashType.Plastic);
            var at = new Vector2Int(3, 5);

            grid.Place(trash, at);

            Assert.AreSame(trash, grid[at]);
            Assert.AreEqual(at, trash.Position);
            Assert.IsFalse(grid.IsEmpty(at));
        }

        [Test]
        public void Move_UpdatesBothCellsAndPosition()
        {
            var grid = new BoardGrid(8, 12);
            var trash = Make.Trash(TrashType.Paper);
            grid.Place(trash, new Vector2Int(2, 2));

            grid.Move(new Vector2Int(2, 2), new Vector2Int(2, 9));

            Assert.IsTrue(grid.IsEmpty(new Vector2Int(2, 2)));
            Assert.AreSame(trash, grid[new Vector2Int(2, 9)]);
            Assert.AreEqual(new Vector2Int(2, 9), trash.Position);
        }

        [Test]
        public void CountEmpty_ReflectsOccupancy()
        {
            var grid = new BoardGrid(4, 4);
            Assert.AreEqual(16, grid.CountEmpty());

            grid.Place(Make.Trash(TrashType.Paper), new Vector2Int(0, 0));
            grid.Place(Make.Trash(TrashType.Paper), new Vector2Int(1, 0));

            Assert.AreEqual(14, grid.CountEmpty());
        }

        [Test]
        public void GetNeighbors4_ClipsAtBoardEdges()
        {
            var grid = new BoardGrid(8, 12);
            var buffer = new Vector2Int[4];

            Assert.AreEqual(4, grid.GetNeighbors4(new Vector2Int(4, 6), buffer));
            Assert.AreEqual(2, grid.GetNeighbors4(new Vector2Int(0, 0), buffer));
            Assert.AreEqual(2, grid.GetNeighbors4(new Vector2Int(7, 11), buffer));
        }

        [Test]
        public void Directions_FollowTopLeftOrigin()
        {
            // WEEK1 §1: row 증가 = 아래
            Assert.AreEqual(new Vector2Int(0, -1), Direction.Up.ToOffset());
            Assert.AreEqual(new Vector2Int(0, 1), Direction.Down.ToOffset());
            Assert.AreEqual(new Vector2Int(-1, 0), Direction.Left.ToOffset());
            Assert.AreEqual(new Vector2Int(1, 0), Direction.Right.ToOffset());
        }
    }
}
