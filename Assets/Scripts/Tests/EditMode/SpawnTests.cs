using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>WEEK1_MOVEMENT_FALLING.md §5.</summary>
    public sealed class SpawnTests
    {
        [Test]
        public void SpawnsConfiguredCountOnTheTopRow()
        {
            var grid = new BoardGrid(8, 12);
            var config = new FakeBoardConfig { SpawnPerStep = 1 };
            var spawner = new RandomTopRowSpawner(grid, config, new MinRandom());

            Assert.AreEqual(1, spawner.Spawn());
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(0, 0)), "MinRandom은 가장 왼쪽 빈 칸을 고른다.");
            Assert.AreEqual(TrashType.A, ((Trash)grid[new Vector2Int(0, 0)]).Type);
        }

        [Test]
        public void SpawnCountIsDrivenByConfig_NotALiteral()
        {
            var grid = new BoardGrid(8, 12);
            var config = new FakeBoardConfig { SpawnPerStep = 3 };
            var spawner = new RandomTopRowSpawner(grid, config, new MinRandom());

            Assert.AreEqual(3, spawner.Spawn());
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(0, 0)));
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(1, 0)));
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(2, 0)));
        }

        [Test]
        public void ScriptedRandom_MakesSpawnFullyDeterministic()
        {
            var grid = new BoardGrid(8, 12);
            var config = new FakeBoardConfig { SpawnPerStep = 1 };
            // 첫 값 = 빈 칸 후보 인덱스, 둘째 값 = TrashType 인덱스
            var spawner = new RandomTopRowSpawner(grid, config, new ScriptedRandom(5, 2));

            spawner.Spawn();

            Assert.IsTrue(grid.IsEmpty(new Vector2Int(0, 0)));
            Assert.AreEqual(TrashType.C, ((Trash)grid[new Vector2Int(5, 0)]).Type);
        }

        [Test]
        public void FullTopRow_SpawnsNothing()
        {
            var grid = new BoardGrid(4, 4);
            for (int col = 0; col < grid.Cols; col++)
            {
                grid.Place(new Trash(TrashType.A), new Vector2Int(col, 0));
            }

            var spawner = new RandomTopRowSpawner(grid, new FakeBoardConfig(), new MinRandom());

            Assert.AreEqual(0, spawner.Spawn());
        }
    }
}
