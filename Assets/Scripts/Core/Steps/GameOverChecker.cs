using System;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 마지막 페이즈(WEEK1 §2·§7).
    /// 규칙이 작아 지금은 구상 클래스로 둔다. 조건이 늘어나면 그때 인터페이스로 추출한다.
    /// </summary>
    public sealed class GameOverChecker
    {
        private readonly BoardGrid _grid;
        private readonly Player _player;
        private readonly IBoardConfig _config;

        public GameOverChecker(BoardGrid grid, Player player, IBoardConfig config)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>스폰 페이즈를 거치지 않은 시점(런 시작 직후)의 판정.</summary>
        public GameOverReason Evaluate() => Evaluate(false);

        /// <summary>
        /// §7 확정 규칙: <b>72칸이 모두 찬 상태에서 다음 블록을 생성하려는 턴</b>에 패배.
        ///
        /// "꽉 참"만으로는 부족하다 — 꽉 찬 채로 스폰 차례가 아닌 턴은 아직 살아 있고,
        /// 그 한 턴 안에 빠져나가면 계속 플레이한다. 그래서 스포너가
        /// "차례였는데 놓을 칸이 없었다"고 보고한 턴에만 패배로 본다.
        /// </summary>
        /// <param name="spawnBlocked">ITrashSpawner.LastSpawnBlocked 값.</param>
        public GameOverReason Evaluate(bool spawnBlocked)
        {
            if (spawnBlocked && _grid.CountEmpty() == 0)
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
        /// 4방향이 전부 경계 밖이거나 점유된 상태 — 기획 스샷의 "빠져나갈 곳이 없습니다".
        /// 프리뷰 줄은 갈 수 없는 곳이므로 탈출구로 세지 않는다(§3).
        ///
        /// AdvanceOnBlocked가 false면 갇힌 플레이어의 모든 입력이 거부되어
        /// 이후 페이즈가 영영 돌지 않는 소프트락이 가능하다. 그래서 GameLoop은
        /// 입력이 거부된 시점에도 이 검사를 따로 돌린다(GameLoop.Step 참조).
        ///
        /// 전투가 붙으면 막힌 쓰레기를 공격해 치울 수 있으므로 이 조건은 크게 완화된다.
        /// </summary>
        public bool IsPlayerTrapped()
        {
            for (int i = 0; i < BoardGrid.AllDirections.Length; i++)
            {
                Vector2Int neighbor = _player.Position + BoardGrid.AllDirections[i].ToOffset();
                if (neighbor.y < _config.FirstPlayableRow)
                {
                    continue;
                }

                if (_grid.InBounds(neighbor) && _grid.IsEmpty(neighbor))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
