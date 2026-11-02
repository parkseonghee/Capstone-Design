using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 플레이어 공격 알림(<see cref="GameLoop.PlayerStruck"/>). 피격 이펙트가 이걸 받아 맞은 칸마다 이펙트를 띄운다.
    /// </summary>
    public sealed class PlayerStruckTests
    {
        private static FakeBoardConfig Board()
            => new FakeBoardConfig
            {
                RowsOnStart = 0,
                BlocksPerSpawn = 0,
                PlayerStart = new Vector2Int(4, 5),
            };

        private static Trash Mob(int maxHp)
            => new Trash(TrashType.Paper, new TrashStats(maxHp, 1, 0, 0, true));

        private static GameLoop Loop(FakeBoardConfig config)
            => GameLoopFactory.CreateWeek1(config, config, config, config, config, new MinRandom());

        private static Vector2Int Below(GameLoop loop, int n = 1)
            => new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + n);

        [Test]
        public void HittingAnEnemyReportsTheCell()
        {
            GameLoop loop = Loop(Board());
            Vector2Int target = Below(loop);
            loop.Grid.Place(Mob(9), target);

            var cells = new List<Vector2Int>();
            var kills = new List<bool>();
            loop.PlayerStruck += (cell, killed) => { cells.Add(cell); kills.Add(killed); };

            loop.Step(Direction.Down);

            CollectionAssert.AreEqual(new[] { target }, cells);
            CollectionAssert.AreEqual(new[] { false }, kills, "체력 9라 한 대로는 안 죽는다");
        }

        [Test]
        public void EveryChainedCellIsReported()
        {
            GameLoop loop = Loop(Board());
            for (int i = 1; i <= 3; i++)
            {
                loop.Grid.Place(Mob(1), Below(loop, i));
            }

            int count = 0;
            int killed = 0;
            loop.PlayerStruck += (cell, dead) => { count++; if (dead) { killed++; } };

            loop.Step(Direction.Down);

            Assert.AreEqual(3, count, "연쇄로 같이 맞은 칸도 하나씩 울려야 한다");
            Assert.AreEqual(3, killed);
        }

        [Test]
        public void MovingIntoAnEmptyCellReportsNothing()
        {
            GameLoop loop = Loop(Board());

            int count = 0;
            loop.PlayerStruck += (cell, dead) => count++;

            loop.Step(Direction.Down);

            Assert.AreEqual(0, count);
        }
    }
}
