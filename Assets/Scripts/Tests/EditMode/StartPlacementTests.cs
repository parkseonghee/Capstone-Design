using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 초기 줄과 플레이어 배치 순서. 순서가 반대면 §4의 벽 규칙 때문에
    /// 플레이어 컬럼만 바닥까지 비고 머리 위로 탑이 쌓인다.
    /// </summary>
    public sealed class StartPlacementTests
    {
        /// <summary>4열 x 6행(프리뷰 1 + 플레이 5), 시작 3줄.</summary>
        private static FakeBoardConfig RowConfig()
        {
            return new FakeBoardConfig
            {
                Cols = 4,
                PlayableRows = 5,
                GapsPerRow = 0,
                RowsOnStart = 3,
                PlayerStart = new Vector2Int(2, 2),
            };
        }

        [Test]
        public void InitialRowsFillEveryColumn_IncludingThePlayerColumn()
        {
            GameLoop loop = Make.Week1(RowConfig(), new MinRandom());

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
            GameLoop loop = Make.Week1(RowConfig(), new MinRandom());

            Assert.AreEqual(new Vector2Int(2, 2), loop.Player.Position);
            Assert.AreSame(loop.Player, loop.Grid[new Vector2Int(2, 2)]);
        }

        [Test]
        public void OccupiedStartCell_FallsBackToTheFirstEmptyCellAbove()
        {
            // 초기 줄이 시작 칸까지 덮는 경우: 같은 컬럼에서 위로 올라가며 자리를 찾는다.
            var config = RowConfig();
            config.RowsOnStart = 4;              // 플레이 5줄 중 4줄을 채운다 -> row 1만 남는다
            config.PlayerStart = new Vector2Int(2, 3);

            GameLoop loop = Make.Week1(config, new MinRandom());

            Assert.AreEqual(2, loop.Player.Position.x, "같은 컬럼을 유지한다.");
            Assert.AreEqual(1, loop.Player.Position.y, "막히지 않은 가장 가까운 위쪽 칸으로 간다.");
            Assert.IsFalse(loop.IsOver);
        }

        [Test]
        public void PlayerNeverStartsInThePreviewRow()
        {
            // 프리뷰 줄은 플레이어가 들어갈 수 없는 영역이다(§1·§3).
            var config = RowConfig();
            config.RowsOnStart = 4;
            config.PlayerStart = new Vector2Int(2, 5);

            GameLoop loop = Make.Week1(config, new MinRandom());

            Assert.GreaterOrEqual(loop.Player.Position.y, config.FirstPlayableRow);
        }

        [Test]
        public void NoInitialRows_PlacesThePlayerExactlyWhereConfigured()
        {
            var config = RowConfig();
            config.RowsOnStart = 0;

            GameLoop loop = Make.Week1(config, new MinRandom());

            Assert.AreEqual(new Vector2Int(2, 2), loop.Player.Position);
            Assert.AreEqual(config.Cols * config.Rows - 1, loop.Grid.CountEmpty());
        }
    }
}
