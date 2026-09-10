using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// WEEK1_MOVEMENT_FALLING.md §4 + §7-1(플레이어는 벽).
    /// 중력은 한 스텝에 한 칸씩만 내린다 — 순간이동하지 않고 노드를 타고 내려온다.
    /// </summary>
    public sealed class GravityTests
    {
        // ── 한 칸씩 내려가는지 ──────────────────────────────────────────────

        [Test]
        public void Step_MovesFloatingTrashExactlyOneCell()
        {
            var grid = new BoardGrid(8, 12);
            var trash = Make.Trash(TrashType.Paper);
            grid.Place(trash, new Vector2Int(3, 0));

            var gravity = new GravityResolver(grid);

            Assert.AreEqual(1, gravity.Step());
            Assert.AreEqual(new Vector2Int(3, 1), trash.Position, "한 번에 바닥까지 가면 안 된다.");

            gravity.Step();
            Assert.AreEqual(new Vector2Int(3, 2), trash.Position);
        }

        [Test]
        public void Step_TakesOneCallPerCellToReachTheFloor()
        {
            var grid = new BoardGrid(8, 12);
            var trash = Make.Trash(TrashType.Paper);
            grid.Place(trash, new Vector2Int(3, 0));

            var gravity = new GravityResolver(grid);
            for (int i = 0; i < 11; i++)
            {
                gravity.Step();
            }

            Assert.AreEqual(new Vector2Int(3, 11), trash.Position);
            Assert.AreEqual(0, gravity.Step(), "바닥에 닿으면 더 움직이지 않는다.");
        }

        [Test]
        public void Step_MovesAStackDownTogether()
        {
            // 붙어 있는 덩어리는 통째로 한 칸 내려간다.
            var grid = new BoardGrid(8, 12);
            var upper = Make.Trash(TrashType.Paper);
            var lower = Make.Trash(TrashType.Plastic);
            grid.Place(upper, new Vector2Int(2, 4));
            grid.Place(lower, new Vector2Int(2, 5));

            Assert.AreEqual(2, new GravityResolver(grid).Step());
            Assert.AreEqual(new Vector2Int(2, 5), upper.Position);
            Assert.AreEqual(new Vector2Int(2, 6), lower.Position);
        }

        [Test]
        public void Step_LeavesSettledTrashAlone()
        {
            var grid = new BoardGrid(8, 12);
            grid.Place(Make.Trash(TrashType.Paper), new Vector2Int(0, 11));
            grid.Place(Make.Trash(TrashType.Paper), new Vector2Int(0, 10));

            Assert.AreEqual(0, new GravityResolver(grid).Step());
        }

        [Test]
        public void Step_StopsOnThePlayer()
        {
            // §7-1 확정: 플레이어 위로 낙하하면 막힌다. 게임오버가 아니다.
            var grid = new BoardGrid(8, 12);
            var player = Make.Player();
            var trash = Make.Trash(TrashType.Glass);
            grid.Place(player, new Vector2Int(5, 8));
            grid.Place(trash, new Vector2Int(5, 6));

            var gravity = new GravityResolver(grid);

            gravity.Step();
            Assert.AreEqual(new Vector2Int(5, 7), trash.Position);

            Assert.AreEqual(0, gravity.Step(), "플레이어 바로 위에서 멈춘다.");
            Assert.AreEqual(new Vector2Int(5, 8), player.Position, "플레이어는 낙하 대상이 아니다.");
        }

        // ── 완전 정착(Settle) ───────────────────────────────────────────────

        [Test]
        public void Settle_DropsTrashAllTheWayDown()
        {
            var grid = new BoardGrid(8, 12);
            var trash = Make.Trash(TrashType.Paper);
            grid.Place(trash, new Vector2Int(3, 0));

            Assert.AreEqual(11, new GravityResolver(grid).Settle());
            Assert.AreEqual(new Vector2Int(3, 11), trash.Position);
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(3, 0)));
        }

        [Test]
        public void Settle_KeepsRelativeOrder()
        {
            var grid = new BoardGrid(8, 12);
            var top = Make.Trash(TrashType.Paper);
            var middle = Make.Trash(TrashType.Plastic);
            var bottom = Make.Trash(TrashType.Glass);

            grid.Place(top, new Vector2Int(2, 1));
            grid.Place(middle, new Vector2Int(2, 4));
            grid.Place(bottom, new Vector2Int(2, 7));

            new GravityResolver(grid).Settle();

            Assert.AreEqual(new Vector2Int(2, 11), bottom.Position);
            Assert.AreEqual(new Vector2Int(2, 10), middle.Position);
            Assert.AreEqual(new Vector2Int(2, 9), top.Position);
        }

        [Test]
        public void Settle_TrashBelowPlayerStillReachesTheFloor()
        {
            var grid = new BoardGrid(8, 12);
            var player = Make.Player();
            var trash = Make.Trash(TrashType.Paper);
            grid.Place(player, new Vector2Int(5, 4));
            grid.Place(trash, new Vector2Int(5, 6));

            new GravityResolver(grid).Settle();

            Assert.AreEqual(new Vector2Int(5, 4), player.Position);
            Assert.AreEqual(new Vector2Int(5, 11), trash.Position);
        }

        [Test]
        public void Settle_ColumnsAreIndependent()
        {
            var grid = new BoardGrid(8, 12);
            var left = Make.Trash(TrashType.Paper);
            var right = Make.Trash(TrashType.Plastic);
            grid.Place(left, new Vector2Int(0, 3));
            grid.Place(right, new Vector2Int(7, 5));

            new GravityResolver(grid).Settle();

            Assert.AreEqual(new Vector2Int(0, 11), left.Position);
            Assert.AreEqual(new Vector2Int(7, 11), right.Position);
        }
    }
}
