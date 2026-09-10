using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 시작 줄(WEEK1 §5 "하단 3줄이 채워진 상태로 시작")을 깔아 주는 RowTrashSpawner.
    /// 진행 중 스폰과는 다른 스포너다 — 이쪽은 한 줄을 통째로 프리뷰 줄에 얹고,
    /// 낙하는 GameLoop이 중력으로 시킨다.
    /// </summary>
    public sealed class RowSpawnTests
    {
        [Test]
        public void SpawnRow_FillsTheWholeTopRow()
        {
            var config = new FakeBoardConfig { GapsPerRow = 0 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            var spawner = new RowTrashSpawner(grid, config, config, new MinRandom());

            Assert.AreEqual(8, spawner.SpawnRow(), "시작 줄은 갭 없이 꽉 찬다.");
            for (int col = 0; col < grid.Cols; col++)
            {
                Assert.IsFalse(grid.IsEmpty(new Vector2Int(col, 0)));
            }
        }

        [Test]
        public void GapsPerRow_LeavesThatManyColumnsEmpty()
        {
            // 확정값은 0이지만 값으로 남겨 둔다 — 난이도 실험용(Hard Rule 1).
            var config = new FakeBoardConfig { GapsPerRow = 1 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            var spawner = new RowTrashSpawner(grid, config, config, new MinRandom());

            Assert.AreEqual(7, spawner.SpawnRow(), "8칸 중 1칸은 비어야 한다.");
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(0, 0)), "MinRandom은 첫 컬럼을 빈 칸으로 고른다.");
        }

        [Test]
        public void OccupiedColumns_AreSkippedInsteadOfThrowing()
        {
            var config = new FakeBoardConfig { Cols = 4, PlayableRows = 5, GapsPerRow = 0 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            grid.Place(Make.Trash(TrashType.Paper), new Vector2Int(2, 0));

            var spawner = new RowTrashSpawner(grid, config, config, new MinRandom());

            Assert.AreEqual(3, spawner.SpawnRow(), "이미 차 있던 한 칸은 건너뛴다.");
        }

        [Test]
        public void ThreeSeedRows_StackOnTheFloor()
        {
            // 시작하자마자 하단 3줄이 바닥부터 차곡차곡 쌓여 있어야 한다.
            var config = new FakeBoardConfig
            {
                RowsOnStart = 3,
                GapsPerRow = 0,
                PlayerStart = new Vector2Int(4, 5),
            };

            GameLoop loop = Make.Week1(config, new MinRandom());

            for (int row = 6; row <= 8; row++)
            {
                for (int col = 0; col < config.Cols; col++)
                {
                    Assert.IsFalse(
                        loop.Grid.IsEmpty(new Vector2Int(col, row)),
                        "하단 3줄은 꽉 차야 한다: (" + col + ", " + row + ")");
                }
            }

            Assert.AreEqual(new Vector2Int(4, 5), loop.Player.Position, "플레이어는 그 바로 위다.");
            Assert.AreEqual(config.Cols * config.Rows - 24 - 1, loop.Grid.CountEmpty(), "쓰레기 24 + 플레이어 1");
        }

        [Test]
        public void RowsOnStartZero_LeavesTheBoardEmpty()
        {
            var config = new FakeBoardConfig { RowsOnStart = 0 };

            GameLoop loop = Make.Week1(config, new MinRandom());

            Assert.AreEqual(config.Cols * config.Rows - 1, loop.Grid.CountEmpty(), "플레이어 한 칸만 차 있어야 한다.");
        }
    }
}
