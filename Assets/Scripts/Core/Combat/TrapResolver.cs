using System;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 덫과 관련된 두 동작을 모은다: 몬스터가 죽은 자리에 덫을 남기고,
    /// 플레이어나 몬스터가 덫에 닿으면 무작위 빈 칸으로 보낸다.
    ///
    /// 두 동작을 한 클래스에 둔 이유는 책임이 "덫" 하나로 좁게 묶여 있어서다(Hard Rule 4) —
    /// CombatMoveResolver·BombResolver·GravityResolver·GameLoop 전부 이 클래스를 통해서만
    /// 덫을 다루므로, 나중에 규칙이 바뀌어도(예: 일회용으로 바뀐다) 여기 한 곳만 고치면 된다.
    /// </summary>
    public sealed class TrapResolver
    {
        private readonly BoardGrid _grid;
        private readonly IRandomSource _random;

        public TrapResolver(BoardGrid grid, IRandomSource random)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// 몬스터가 죽은 자리에 덫을 놓는다. leavesTrap이 꺼져 있거나 칸이 이미 차 있으면
        /// 아무 일도 하지 않는다(죽은 자리는 방금 비웠으니 보통은 비어 있다).
        /// </summary>
        public void MaybeLeaveTrap(bool leavesTrap, Vector2Int cell)
        {
            if (leavesTrap && _grid.IsEmpty(cell))
            {
                _grid.Place(new Trap(), cell);
            }
        }

        /// <summary>
        /// 덫을 발동시킨다: entity를 무작위 빈 칸으로 보내고, <b>밟힌 덫은 사라진다</b>
        /// (일회용, 기획 변경). 갈 빈 칸이 없어 실패하면(보드가 꽉 참) 덫도 그대로 남는다 —
        /// 아무 일도 안 일어난 셈이라 지워질 이유가 없다.
        /// </summary>
        /// <param name="trapCell">밟힌 덫이 있는 칸.</param>
        /// <param name="entity">덫에 닿아 옮겨질 대상(플레이어 또는 몬스터).</param>
        /// <param name="firstAllowedRow">텔레포트 후보 칸 제한. <see cref="TeleportToRandomEmptyCell"/> 참고.</param>
        /// <returns>실제로 옮겼으면(=덫이 발동해 사라졌으면) true.</returns>
        public bool Trigger(Vector2Int trapCell, Entity entity, int firstAllowedRow = 0)
        {
            bool moved = TeleportToRandomEmptyCell(entity, firstAllowedRow);
            if (moved)
            {
                _grid.Remove(trapCell);
            }

            return moved;
        }

        /// <summary>
        /// entity를 보드 안의 무작위 빈 칸으로 옮긴다. 지금 있는 칸은 후보에서 뺀다 —
        /// 제자리 "텔레포트"는 의미가 없다.
        /// </summary>
        /// <param name="firstAllowedRow">
        /// 이 행보다 위(작은 값)는 후보에서 뺀다. 플레이어는 프리뷰 줄에 들어갈 수 없으므로
        /// 호출자가 IBoardConfig.FirstPlayableRow를 넘긴다. 몬스터는 0(전부 허용)이면 된다.
        /// </param>
        /// <returns>실제로 옮겼으면 true. 갈 빈 칸이 없으면(보드가 꽉 참) false.</returns>
        public bool TeleportToRandomEmptyCell(Entity entity, int firstAllowedRow = 0)
        {
            Vector2Int current = entity.Position;
            Vector2Int chosen = default;
            int found = 0;

            // 컬렉션을 만들지 않고 훑으면서 고른다(저수지 표본추출) — 매번 리스트를
            // 새로 만들지 않기 위함이다(Hard Rule 8).
            for (int row = firstAllowedRow; row < _grid.Rows; row++)
            {
                for (int col = 0; col < _grid.Cols; col++)
                {
                    var cell = new Vector2Int(col, row);
                    if (cell == current || !_grid.IsEmpty(cell))
                    {
                        continue;
                    }

                    found++;
                    if (_random.NextInt(0, found) == 0)
                    {
                        chosen = cell;
                    }
                }
            }

            if (found == 0)
            {
                return false;
            }

            _grid.Move(current, chosen);
            return true;
        }
    }
}
