using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 초기 줄과 플레이어 배치 순서. 순서가 반대면 §7-1의 벽 규칙 때문에
    /// 플레이어 컬럼만 바닥까지 비고 머리 위로 탑이 쌓인다.
    /// </summary>
    public sealed class StartPlacementTests
    {
        private static FakeBoardConfig RowConfig()
        {
            return new FakeBoardConfig
            {
                Cols = 4,
                Rows = 6,
                Mode = SpawnMode.FullRow,
                GapsPerRow = 0,
                RowsOnStart = 3,
                PlayerStart = new Vector2Int(2, 1),
            };
        }

        [Test]
        public void InitialRowsFillEveryColumn_IncludingThePlayerColumn()
        {
            GameLoop loop = GameLoopFactory.CreateWeek1(RowConfig(), new MinRandom());

            for (int row = 3; row <= 5; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    Assert.IsFalse(
                        loop.Grid.IsEmpty(new Vector2Int(col, row)),
                        "플레이어 컬럼 아래도 채워져 있어야 한다: (" + col + ", " + row + ")");
                }
            }
        }

        [Test]
        public void PlayerSitsAboveTheStack_NotUnderneathIt()
        {
            GameLoop loop = GameLoopFactory.CreateWeek1(RowConfig(), new MinRandom());

            Assert.AreEqual(new Vector2Int(2, 1), loop.Player.Position);
            Assert.AreSame(loop.Player, loop.Grid[new Vector2Int(2, 1)]);
        }

        [Test]
        public void OccupiedStartCell_FallsBackToTheFirstEmptyCellAbove()
        {
            // 초기 줄이 시작 칸까지 덮는 경우: 같은 컬럼에서 위로 올라가며 자리를 찾는다.
            var config = RowConfig();
            config.RowsOnStart = 5;              // 6행 중 5행을 채운다 -> row 1이 막힌다
            config.PlayerStart = new Vector2Int(2, 3);

            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());

            Assert.AreEqual(2, loop.Player.Position.x, "같은 컬럼을 유지한다.");
            Assert.AreEqual(0, loop.Player.Position.y, "막히지 않은 가장 가까운 위쪽 칸으로 간다.");
            Assert.IsFalse(loop.IsOver);
        }

        [Test]
        public void NoInitialRows_PlacesThePlayerExactlyWhereConfigured()
        {
            var config = RowConfig();
            config.RowsOnStart = 0;

            GameLoop loop = GameLoopFactory.CreateWeek1(config, new MinRandom());

            Assert.AreEqual(new Vector2Int(2, 1), loop.Player.Position);
            Assert.AreEqual(config.Cols * config.Rows - 1, loop.Grid.CountEmpty());
        }
    }
}
