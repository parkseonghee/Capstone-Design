using System;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 마지막 페이즈(WEEK1 §2·§7).
    /// 패배 조건은 여기 한 곳에만 있다 — 다른 클래스는 사실만 보고하고 판정하지 않는다.
    /// </summary>
    public sealed class GameOverChecker
    {
        private readonly BoardGrid _grid;
        private readonly Player _player;
        private readonly IMoveResolver _move;

        /// <param name="move">
        /// 갇힘 판정을 위임할 이동 규칙. 전투가 꽂히면 "쓰레기에 둘러싸임"이
        /// 더 이상 갇힘이 아니게 되는데, 그 사실을 아는 건 이동 규칙 쪽이다.
        /// </param>
        public GameOverChecker(BoardGrid grid, Player player, IMoveResolver move)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _move = move ?? throw new ArgumentNullException(nameof(move));
        }

        /// <summary>스폰 페이즈를 거치지 않은 시점(런 시작 직후)의 판정.</summary>
        public GameOverReason Evaluate() => Evaluate(false);

        /// <summary>
        /// §7 확정 규칙: <b>72칸이 모두 찬 상태에서 다음 블록을 생성하려는 턴</b>에 패배.
        ///
        /// "꽉 참"만으로는 부족하다 — 꽉 찬 채로 스폰 차례가 아닌 턴은 아직 살아 있고,
        /// 그 한 턴 안에 빠져나가면 계속 플레이한다. 그래서 스포너가
        /// "차례였는데 놓을 칸이 없었다"고 보고한 턴에만 패배로 본다.
        ///
        /// HP 패배가 가장 먼저다 — 이미 죽은 플레이어에게 보드 상태를 따질 이유가 없다.
        /// </summary>
        /// <param name="spawnBlocked">ITrashSpawner.LastSpawnBlocked 값.</param>
        public GameOverReason Evaluate(bool spawnBlocked)
        {
            if (_player.IsDead)
            {
                return GameOverReason.PlayerDead;
            }

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
        /// 4방향 어디로도 행동할 수 없는 상태 — 기획 스샷의 "빠져나갈 곳이 없습니다".
        /// 무엇이 행동인지는 이동 규칙이 정한다(IMoveResolver.CanAct).
        ///
        /// AdvanceOnBlocked가 false면 갇힌 플레이어의 모든 입력이 거부되어
        /// 이후 페이즈가 영영 돌지 않는 소프트락이 가능하다. 그래서 GameLoop은
        /// 입력이 거부된 시점에도 이 검사를 따로 돌린다(GameLoop.Step 참조).
        /// </summary>
        public bool IsPlayerTrapped()
        {
            for (int i = 0; i < BoardGrid.AllDirections.Length; i++)
            {
                Vector2Int neighbor = _player.Position + BoardGrid.AllDirections[i].ToOffset();
                if (_move.CanAct(neighbor))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
