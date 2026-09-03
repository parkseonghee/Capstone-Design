using System;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// WEEK1 범위의 이동 규칙(§3): 빈 칸이면 이동, 쓰레기가 있으면 그냥 막힘.
    /// 전투가 붙는 단계에서 CombatMoveResolver로 교체된다.
    /// </summary>
    public sealed class BlockingMoveResolver : IMoveResolver
    {
        private readonly BoardGrid _grid;
        private readonly Player _player;

        public BlockingMoveResolver(BoardGrid grid, Player player)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public MoveOutcome Resolve(Direction direction)
        {
            Vector2Int target = _player.Position + direction.ToOffset();

            if (!_grid.InBounds(target))
            {
                return MoveOutcome.OutOfBounds;
            }

            if (!_grid.IsEmpty(target))
            {
                return MoveOutcome.BlockedByEntity;
            }

            _grid.Move(_player.Position, target);
            return MoveOutcome.Moved;
        }
    }
}
