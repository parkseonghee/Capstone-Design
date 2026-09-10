using System;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 전투 이전(WEEK1) 이동 규칙(§3): 빈 칸이면 이동, 쓰레기가 있으면 그냥 막힘.
    /// 프리뷰 줄로는 올라갈 수 없다 — 그 줄은 "다음에 뭐가 오는지"만 보여주는 버퍼다.
    ///
    /// 지금 GameLoopFactory가 꽂는 건 CombatMoveResolver이므로 런타임 경로에는 쓰이지 않는다.
    /// "쓰레기를 못 때리는 보드"의 기준 동작으로 남겨 두었다(갇힘 판정 테스트가 이걸 쓴다).
    /// </summary>
    public sealed class BlockingMoveResolver : IMoveResolver
    {
        private readonly BoardGrid _grid;
        private readonly Player _player;
        private readonly IBoardConfig _config;

        public BlockingMoveResolver(BoardGrid grid, Player player, IBoardConfig config)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public MoveResult Resolve(Direction direction)
        {
            Vector2Int target = _player.Position + direction.ToOffset();

            if (!_grid.InBounds(target))
            {
                return MoveResult.Simple(MoveOutcome.OutOfBounds);
            }

            // 프리뷰 줄은 플레이어에게 보드 밖과 같다(§1: "플레이어 상호작용 X").
            // OutOfBounds로 돌려주는 이유는 §3의 무효 입력 처리를 그대로 타기 위함이다 —
            // AdvanceOnBlocked와 무관하게 보드가 진행되지 않는다.
            if (target.y < _config.FirstPlayableRow)
            {
                return MoveResult.Simple(MoveOutcome.OutOfBounds);
            }

            if (!_grid.IsEmpty(target))
            {
                return MoveResult.Simple(MoveOutcome.BlockedByEntity);
            }

            _grid.Move(_player.Position, target);
            return MoveResult.Simple(MoveOutcome.Moved);
        }

        /// <summary>이 구현에서 "행동 가능"은 곧 "빈 칸으로 이동 가능"이다.</summary>
        public bool CanAct(Vector2Int target)
        {
            return _grid.InBounds(target)
                   && target.y >= _config.FirstPlayableRow
                   && _grid.IsEmpty(target);
        }
    }
}
