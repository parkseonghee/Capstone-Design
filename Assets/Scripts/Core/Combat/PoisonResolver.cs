using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 독 몬스터 기믹. 기획 확정 다섯 줄을 한 클래스에 모았다(TrapResolver와 같은 이유 — 책임이
    /// "독" 하나로 좁게 묶여 있어서, 규칙이 바뀌어도 여기 한 곳만 고치면 된다. Hard Rule 4):
    ///
    ///  1. 독 몬스터가 죽으면 그 자리에 독이 깔린다(<see cref="MaybeLeavePoison"/>).
    ///  2. 독은 <b>떨어지지 않는다</b>.
    ///  3. 밟으면 움직일 때마다 피해를 입는다(한 턴 동안).
    ///  4. 독은 두 턴 뒤 사라진다(<see cref="Tick"/>).
    ///  5. 몬스터도 밟는다 — 그래서 <see cref="OnMoved"/>는 Entity를 받는다.
    ///
    /// <b>독은 엔티티가 아니다.</b> 칸을 차지하는 물건이 아니라 칸에 깔리는 바닥이라 격자(BoardGrid)
    /// 밖에 따로 들고 있다. 그래서 기획 2번("중력에 안 떨어진다")이 저절로 성립한다 —
    /// 중력은 격자에 놓인 것만 내리므로 독을 볼 일이 아예 없다. 덫(Trap)처럼 엔티티로 만들면
    /// 그 칸을 막아 버려 "밟는다"는 말 자체가 성립하지 않고, 블록이 독 위에 쌓일 수도 없다.
    ///
    /// 한 번의 이동에 피해는 <b>최대 한 번</b>이다. 중독인 채로 또 다른 독 칸에 들어서도
    /// 1회분만 들어간다 — 안 그러면 독밭을 가로지를 때 한 칸에 2씩 맞아 체력 4~5인 판에서
    /// 설명 없이 즉사한다.
    ///
    /// 칸별 남은 턴은 배열로, 뷰가 훑을 목록은 리스트로 들고 재사용한다(Hard Rule 8).
    /// </summary>
    public sealed class PoisonResolver
    {
        private readonly BoardGrid _grid;
        private readonly Player _player;
        private readonly IPoisonConfig _config;

        /// <summary>칸별 남은 턴. 0이면 독이 없다. 인덱스는 row * Cols + col.</summary>
        private readonly int[] _remaining;

        /// <summary>지금 독이 깔린 칸. 뷰가 이 목록만 보고 그린다.</summary>
        private readonly List<Vector2Int> _cells;

        /// <summary>
        /// 이번 턴에 <b>새로 생긴</b> 독 칸. 턴 끝의 <see cref="Tick"/>이 이것만 건너뛴다.
        ///
        /// 안 건너뛰면 몬스터를 잡은 바로 그 턴에 수명이 한 번 깎여, "2턴 뒤 사라진다"가
        /// 실제로는 "1턴 뒤"가 된다 — 생기자마자 늙는 셈이다.
        /// </summary>
        private readonly bool[] _laidThisTurn;

        /// <summary>이번 턴에 새로 중독된 것들. 중독도 같은 이유로 이번 턴에는 늙지 않는다.</summary>
        private readonly List<Entity> _poisonedThisTurn;

        public PoisonResolver(BoardGrid grid, Player player, IPoisonConfig config)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _remaining = new int[grid.CellCount];
            _cells = new List<Vector2Int>(grid.CellCount);
            _laidThisTurn = new bool[grid.CellCount];
            _poisonedThisTurn = new List<Entity>(4);
        }

        /// <summary>지금 독이 깔린 칸들. 뷰가 읽는다.</summary>
        public IReadOnlyList<Vector2Int> Cells => _cells;

        /// <summary>독 장판의 전체 수명. 뷰가 "얼마나 남았는지"를 비율로 그릴 때 쓴다.</summary>
        public int FieldTurns => Mathf.Max(1, _config.FieldTurns);

        public bool IsPoisoned(Vector2Int cell) => RemainingAt(cell) > 0;

        /// <summary>그 칸의 독이 몇 턴 남았는지. 독이 없으면 0.</summary>
        public int RemainingAt(Vector2Int cell)
            => _grid.InBounds(cell) ? _remaining[Index(cell)] : 0;

        /// <summary>
        /// 몬스터가 죽은 자리에 독을 깐다. leavesPoison이 꺼져 있으면 아무 일도 하지 않는다.
        ///
        /// 덫과 달리 <b>칸이 비었는지 묻지 않는다</b> — 독은 바닥에 깔리는 것이라 그 위에
        /// 블록이 떨어져 쌓여도 상관없고, 오히려 그래야 떨어진 블록이 독을 밟을 수 있다(기획 5).
        /// </summary>
        public void MaybeLeavePoison(bool leavesPoison, Vector2Int cell)
        {
            if (!leavesPoison || !_grid.InBounds(cell))
            {
                return;
            }

            int index = Index(cell);
            if (_remaining[index] <= 0)
            {
                _cells.Add(cell);
            }

            _remaining[index] = FieldTurns;
            _laidThisTurn[index] = true;
        }

        /// <summary>
        /// 엔티티가 한 칸 움직였다. 이동한 <b>뒤에</b> 부른다(새 칸을 봐야 하므로).
        ///
        /// 중독이었거나 들어선 칸이 독이면 피해를 한 번 준다. 독을 밟았으면 중독이 새로 걸린다.
        /// 방금 걸린 중독은 이번 턴 끝에 늙지 않는다 — 밟은 턴은 "1턴"에 들어가지 않는다.
        /// </summary>
        /// <returns>이번 이동으로 받은 피해량. 0이면 아무 일도 없었다.</returns>
        public int OnMoved(Entity entity)
        {
            if (entity == null)
            {
                return 0;
            }

            bool wasPoisoned = entity.PoisonTurns > 0;
            bool steppedInPoison = IsPoisoned(entity.Position);

            if (steppedInPoison)
            {
                entity.ApplyPoison(Mathf.Max(0, _config.StatusTurns));

                if (!_poisonedThisTurn.Contains(entity))
                {
                    _poisonedThisTurn.Add(entity);
                }
            }

            if (!wasPoisoned && !steppedInPoison)
            {
                return 0;
            }

            return Hurt(entity, _config.StepDamage);
        }

        /// <summary>
        /// 한 턴이 지났다. 장판 수명과 걸려 있는 중독을 같이 한 턴씩 깎는다.
        /// 스텝의 마지막 페이즈로 한 번 불린다.
        /// </summary>
        public void Tick()
        {
            // 뒤에서부터 지운다 — 앞에서 지우면 뒤 원소의 인덱스가 밀린다.
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                int index = Index(_cells[i]);

                // 이번 턴에 생긴 독은 이번 턴에 늙지 않는다. 다음 턴부터 센다.
                if (_laidThisTurn[index])
                {
                    _laidThisTurn[index] = false;
                    continue;
                }

                _remaining[index]--;

                if (_remaining[index] <= 0)
                {
                    _remaining[index] = 0;
                    _cells.RemoveAt(i);
                }
            }

            // 중독은 보드 위에 있든 없든 사람과 몬스터 모두에게 똑같이 흐른다.
            AgeStatus(_player);

            for (int row = 0; row < _grid.Rows; row++)
            {
                for (int col = 0; col < _grid.Cols; col++)
                {
                    Entity entity = _grid[col, row];
                    if (entity != null && entity != _player)
                    {
                        AgeStatus(entity);
                    }
                }
            }

            _poisonedThisTurn.Clear();
        }

        /// <summary>방금 걸린 중독은 건너뛴다 — 밟은 그 턴은 지속 시간에 들어가지 않는다.</summary>
        private void AgeStatus(Entity entity)
        {
            if (!_poisonedThisTurn.Contains(entity))
            {
                entity.AgePoison();
            }
        }

        /// <summary>판이 새로 깔릴 때 독을 전부 걷는다. 지금은 쓰이지 않지만 같이 둔다.</summary>
        public void Clear()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                int index = Index(_cells[i]);
                _remaining[index] = 0;
                _laidThisTurn[index] = false;
            }

            _cells.Clear();
            _poisonedThisTurn.Clear();
        }

        /// <summary>
        /// 독 피해를 먹인다. 체력을 가진 둘(플레이어·블록)만 아프다 —
        /// 덫이나 상품은 밟혀도 아무 일도 없다.
        /// </summary>
        private static int Hurt(Entity entity, int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            var player = entity as Player;
            if (player != null)
            {
                player.TakeDamage(amount);
                return amount;
            }

            var trash = entity as Trash;
            if (trash != null)
            {
                trash.TakeDamage(amount);
                return amount;
            }

            return 0;
        }

        private int Index(Vector2Int cell) => (cell.y * _grid.Cols) + cell.x;
    }
}
