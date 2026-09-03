using System;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 페이즈 4(WEEK1 §2·§6).
    /// 규칙이 작아 지금은 구상 클래스로 둔다. 조건이 늘어나면 그때 인터페이스로 추출한다.
    /// </summary>
    public sealed class GameOverChecker
    {
        private readonly BoardGrid _grid;
        private readonly Player _player;

        public GameOverChecker(BoardGrid grid, Player player)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public GameOverReason Evaluate()
        {
            if (_grid.CountEmpty() == 0)
            {
                return GameOverReason.BoardFull;
            }

            if (IsPlayerTrapped())
            {
                return GameOverReason.PlayerTrapped;
            }

            return GameOverReason.None;
        }

        /// <summary>
        /// 4방향이 전부 경계 밖이거나 점유된 상태.
        ///
        /// AdvanceOnBlocked가 false면 갇힌 플레이어의 모든 입력이 거부되어
        /// 페이즈 2~4가 영영 돌지 않는 소프트락이 가능하다. 그래서 GameLoop은
        /// 입력이 거부된 시점에도 이 검사를 따로 돌린다(GameLoop.Step 참조).
        /// </summary>
        public bool IsPlayerTrapped()
        {
            for (int i = 0; i < BoardGrid.AllDirections.Length; i++)
            {
                Vector2Int neighbor = _player.Position + BoardGrid.AllDirections[i].ToOffset();
                if (_grid.InBounds(neighbor) && _grid.IsEmpty(neighbor))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
