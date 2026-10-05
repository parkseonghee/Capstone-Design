using System;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 페이즈 2(WEEK1 §2·§4).
    ///
    /// <b>한 스텝에 한 칸씩만 내려간다.</b> 떠 있는 쓰레기가 단번에 바닥까지 순간이동하지 않고
    /// 노드를 하나씩 타고 내려오게 하기 위함이며, 이게 압박이 다가오는 감각을 만든다.
    /// (WEEK1 §4의 "한 번에 완전 정착"에서 바뀐 부분 — 문서 갱신 필요.)
    ///
    /// 한 번에 끝까지 떨어뜨려야 하는 곳(시작 연출을 건너뛸 때 등)은 Settle()을 쓴다.
    ///
    /// §7-1 확정: 플레이어는 낙하 대상이 아니며 벽 역할을 한다.
    /// 갓 설치된 폭탄도 그 턴 한 번은 제자리에 머문다(IGravityHold).
    /// 임시 컬렉션 없이 격자만 훑으므로 할당이 0이다(Hard Rule 8).
    /// </summary>
    public sealed class GravityResolver
    {
        private readonly BoardGrid _grid;
        private readonly TrapResolver _traps;

        /// <summary>
        /// 독 처리. 없으면(null) 블록은 독을 밟아도 아무렇지 않다 — 독 기믹을 안 쓰는 배선(테스트)을
        /// 깨지 않게 선택 값으로 둔다. 내려간 블록이 그 칸의 독을 밟는 것이 기획 5번이다.
        /// </summary>
        private readonly PoisonResolver _poison;

        /// <param name="traps">
        /// 덫 처리. 없으면(null) 덫은 그냥 다른 블록과 똑같이 막는 장애물로 취급된다 —
        /// 덫을 안 쓰는 기존 배선(테스트 등)을 깨지 않으려고 선택 값으로 뒀다.
        /// </param>
        public GravityResolver(BoardGrid grid, TrapResolver traps = null, PoisonResolver poison = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _traps = traps;
            _poison = poison;
        }

        /// <summary>
        /// 떠 있는 쓰레기를 각각 한 칸씩 내린다.
        /// 아래쪽부터 훑기 때문에 쌓인 덩어리는 통째로 한 칸 내려간다.
        /// </summary>
        /// <returns>이번에 자리를 옮긴 쓰레기 수. 0이면 더 내려갈 게 없다는 뜻이다.</returns>
        public int Step()
        {
            int moved = 0;

            for (int col = 0; col < _grid.Cols; col++)
            {
                // 바닥 바로 위(Rows - 2)에서 위로 올라간다.
                // 아래 블록이 먼저 비켜 줘야 그 위 블록이 따라 내려올 수 있다.
                for (int row = _grid.Rows - 2; row >= 0; row--)
                {
                    Entity entity = _grid[col, row];
                    if (entity == null || entity.Kind == EntityKind.Player)
                    {
                        continue;
                    }

                    // 이번 턴만 제자리에 머무는 것(갓 설치된 폭탄). 건너뛰면서 유예를 풀어 줘서
                    // 다음 턴부터는 다른 블록과 똑같이 떨어지게 한다.
                    var hold = entity as IGravityHold;
                    if (hold != null && hold.HoldsPosition)
                    {
                        hold.ReleaseHold();
                        continue;
                    }

                    if (_grid.IsEmpty(col, row + 1))
                    {
                        _grid.Move(new Vector2Int(col, row), new Vector2Int(col, row + 1));
                        moved++;

                        // 한 칸 움직였다 — 내려선 칸이 독이거나 이미 중독이면 여기서 피해가 들어간다.
                        // 죽은 블록을 걷어내는 건 GameLoop의 독 페이즈다 — 골드·재료·웨이브 집계가
                        // 전부 거기 모여 있어서, 여기서 치우면 그 처리가 두 군데로 갈라진다.
                        _poison?.OnMoved(entity);
                        continue;
                    }

                    // 바로 밑이 덫이면 막힌 게 아니라 "닿은" 것이다 — 무작위 빈 칸으로 날아가고
                    // 밟힌 덫은 사라진다(일회용, 덫 몬스터 기믹). 덫 자신은 위의 HoldsPosition
                    // 분기에서 이미 걸러졌으므로 여기 오는 entity는 항상 덫에 닿는 쪽이다.
                    var trapBelow = new Vector2Int(col, row + 1);
                    if (_traps != null && _grid[trapBelow] is Trap)
                    {
                        if (_traps.Trigger(trapBelow, entity))
                        {
                            moved++;

                            // 날아간 자리에 독이 깔려 있을 수도 있다. 옮겨졌으니 똑같은 "움직임"이다.
                            _poison?.OnMoved(entity);
                        }
                    }
                }
            }

            return moved;
        }

        /// <summary>더 내려갈 곳이 없을 때까지 Step을 반복해 완전히 정착시킨다.</summary>
        /// <returns>총 이동 횟수.</returns>
        public int Settle()
        {
            int total = 0;
            int moved;

            do
            {
                moved = Step();
                total += moved;
            }
            while (moved > 0);

            return total;
        }
    }
}
