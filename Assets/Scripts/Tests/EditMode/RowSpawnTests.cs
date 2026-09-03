using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 줄 단위 낙하(원작 레퍼런스 방식): 가로 한 줄이 통째로 내려오고,
    /// 런 시작에는 여러 줄이 먼저 쏟아진다.
    /// </summary>
    public sealed class RowSpawnTests
    {
        [Test]
        public void SpawnImmediate_FillsTheWholeTopRowMinusGaps()
        {
            var grid = new BoardGrid(8, 12);
            var config = new FakeBoardConfig { Mode = SpawnMode.FullRow, GapsPerRow = 1 };
            var spawner = new RowTrashSpawner(grid, config, new MinRandom());

            int spawned = spawner.SpawnImmediate();

            Assert.AreEqual(7, spawned, "8칸 중 1칸은 비어야 한다.");
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(0, 0)), "MinRandom은 첫 컬럼을 빈 칸으로 고른다.");
            for (int col = 1; col < grid.Cols; col++)
            {
                Assert.IsFalse(grid.IsEmpty(new Vector2Int(col, 0)), "col " + col + "은 채워져야 한다.");
            }
        }

        [Test]
        public void ZeroGaps_FillsEveryColumn()
        {
            var grid = new BoardGrid(8, 12);
            var config = new FakeBoardConfig { Mode = SpawnMode.FullRow, GapsPerRow = 0 };
            var spawner = new RowTrashSpawner(grid, config, new MinRandom());

            Assert.AreEqual(8, spawner.SpawnImmediate());
            for (int col = 0; col < grid.Cols; col++)
            {
                Assert.IsFalse(grid.IsEmpty(new Vector2Int(col, 0)));
            }
        }

        [Test]
        public void Spawn_OnlyEmitsARowEveryStepsPerRow()
        {
            var grid = new BoardGrid(8, 12);
            var config = new FakeBoardConfig { Mode = SpawnMode.FullRow, GapsPerRow = 0, StepsPerRow = 3 };
            var spawner = new RowTrashSpawner(grid, config, new MinRandom());

            Assert.AreEqual(0, spawner.Spawn(), "1번째 스텝: 아직 아니다.");
            Assert.AreEqual(0, spawner.Spawn(), "2번째 스텝: 아직 아니다.");
            Assert.AreEqual(8, spawner.Spawn(), "3번째 스텝에 한 줄이 내려온다.");
            Assert.AreEqual(0, spawner.Spawn(), "주기가 다시 시작된다.");
        }

        [Test]
        public void OccupiedColumns_AreSkippedInsteadOfThrowing()
        {
            var grid = new BoardGrid(4, 6);
            grid.Place(new Trash(TrashType.A), new Vector2Int(2, 0));

            var config = new FakeBoardConfig { Cols = 4, Rows = 6, Mode = SpawnMode.FullRow, GapsPerRow = 0 };
            var spawner = new RowTrashSpawner(grid, config, new MinRandom());

            Assert.AreEqual(3, spawner.SpawnImmediate(), "이미 차 있던 한 칸은 건너뛴다.");
        }

        [Test]
        public void SeedInitialBoard_StacksRowsFromTheFloorUp()
        {
            // 시작하자마자 3줄이 바닥부터 차곡차곡 쌓여 있어야 한다.
            var config = new FakeBoardConfig
            {
                Cols = 4,
                Rows = 6,
                Mode = SpawnMode.FullRow,
                GapsPerRow = 1,
                RowsOnStart = 3,
                PlayerStart = new Vector2Int(0, 0),
            };

            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());

            // MinRandom은 항상 첫 컬럼을 빈 칸으로 고르므로 col 0은 비어 있다.
            for (int row = 3; row <= 5; row++)
            {
                Assert.IsTrue(loop.Grid.IsEmpty(new Vector2Int(0, row)), "col 0 row " + row + "은 빈 칸이어야 한다.");
                for (int col = 1; col < config.Cols; col++)
                {
                    Assert.IsFalse(loop.Grid.IsEmpty(new Vector2Int(col, row)), "col " + col + " row " + row);
                }
            }

            // 그 위는 플레이어를 빼고 전부 비어 있어야 한다.
            Assert.IsTrue(loop.Grid.IsEmpty(new Vector2Int(1, 2)));
            Assert.AreEqual(new Vector2Int(0, 0), loop.Player.Position);
            Assert.AreEqual(9, config.Cols * config.Rows - loop.Grid.CountEmpty() - 1, "쓰레기 3줄 x 3칸");
        }

        [Test]
        public void RowsOnStartZero_LeavesTheBoardEmpty()
        {
            var config = new FakeBoardConfig { Mode = SpawnMode.FullRow, RowsOnStart = 0 };

            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());

            Assert.AreEqual(config.Cols * config.Rows - 1, loop.Grid.CountEmpty(), "플레이어 한 칸만 차 있어야 한다.");
        }

        [Test]
        public void FullRowRun_StaysDeterministicForTheSameSeed()
        {
            Assert.AreEqual(RunRows(20260903), RunRows(20260903));
            Assert.AreNotEqual(RunRows(20260903), RunRows(7));
        }

        private static string RunRows(int seed)
        {
            var config = new FakeBoardConfig
            {
                Mode = SpawnMode.FullRow,
                RowsOnStart = 3,
                StepsPerRow = 3,
                GapsPerRow = 1,
                PlayerStart = new Vector2Int(4, 6),
            };

            GameLoop loop = GameLoopFactory.CreateWeek1(config, new SystemRandomSource(seed));

            Direction[] script = { Direction.Left, Direction.Up, Direction.Right, Direction.Up, Direction.Down };
            for (int i = 0; i < script.Length; i++)
            {
                loop.Step(script[i]);
            }

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
