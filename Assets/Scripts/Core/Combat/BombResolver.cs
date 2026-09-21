using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 폭탄의 설치·도화선·폭발을 담당한다. 기획서(폭탄·아이템·기믹 v1) §1.
    ///
    /// 세 가지 일을 한다:
    ///  1. <b>설치 후보</b> — 플레이어에게서 한 칸 거리의 <b>빈 칸</b>을 모은다.
    ///     UI가 "꾹 눌렀을 때 표시할 자리"를 이 목록에서 가져간다.
    ///  2. <b>설치</b> — 후보 칸에 폭탄을 놓고 보유 개수를 깎는다. 이때는 <b>불이 붙지 않는다.</b>
    ///  3. <b>도화선과 폭발</b> — 매 스텝 점화된 폭탄의 카운트를 줄이고, 0이 되면 터뜨린다.
    ///
    /// 불을 붙이는 건 여기가 아니라 CombatMoveResolver다 — 플레이어가 때려야 점화되기 때문이다.
    ///
    /// 버퍼를 필드로 들고 재사용해 스텝당 할당을 만들지 않는다(Hard Rule 8).
    /// </summary>
    public sealed class BombResolver
    {
        private readonly BoardGrid _grid;
        private readonly Player _player;
        private readonly IBoardConfig _board;
        private readonly IBombConfig _bomb;

        private readonly List<Vector2Int> _placements;

        /// <summary>이번 스텝에 터뜨릴 폭탄들. 연쇄로 더 들어올 수 있어 큐처럼 쓴다.</summary>
        private readonly List<Vector2Int> _detonating;

        /// <summary>폭발 범위가 이미 훑은 칸. 인덱스는 row * Cols + col.</summary>
        private readonly bool[] _marked;

        public BombResolver(BoardGrid grid, Player player, IBoardConfig board, IBombConfig bomb)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _bomb = bomb ?? throw new ArgumentNullException(nameof(bomb));

            _placements = new List<Vector2Int>(4);
            _detonating = new List<Vector2Int>(grid.CellCount);
            _marked = new bool[grid.CellCount];
        }

        /// <summary>
        /// 직전 <see cref="CollectPlacements"/>가 찾은 설치 가능 칸.
        /// 다음 호출 전까지만 유효하다(버퍼 재사용).
        /// </summary>
        public IReadOnlyList<Vector2Int> Placements => _placements;

        /// <summary>
        /// 플레이어 한 칸 거리의 설치 가능 칸을 모은다.
        /// 몬스터·블록·벽이 있는 칸은 빠지고 <b>빈 칸만</b> 남는다(기획 확정).
        /// </summary>
        /// <returns>후보 칸 수. 0이면 놓을 곳이 없다.</returns>
        public int CollectPlacements()
        {
            _placements.Clear();

            if (_player.Bombs <= 0)
            {
                return 0;
            }

            for (int i = 0; i < BoardGrid.AllDirections.Length; i++)
            {
                Vector2Int cell = _player.Position + BoardGrid.AllDirections[i].ToOffset();
                if (IsPlaceable(cell))
                {
                    _placements.Add(cell);
                }
            }

            return _placements.Count;
        }

        /// <summary>그 칸에 지금 폭탄을 놓을 수 있는지. 부작용 없는 질의다.</summary>
        public bool CanPlace(Vector2Int cell)
        {
            return _player.Bombs > 0 && IsPlaceable(cell);
        }

        /// <summary>
        /// 폭탄을 설치한다. <b>불은 붙지 않는다</b> — 플레이어가 때려야 카운트다운이 시작된다.
        /// 놓은 자리에 그대로 남고, 다음 턴부터 중력을 받는다.
        /// </summary>
        /// <returns>설치했으면 true. 보유가 없거나 놓을 수 없는 칸이면 false.</returns>
        public bool Place(Vector2Int cell)
        {
            if (!CanPlace(cell))
            {
                return false;
            }

            if (!_player.SpendBomb())
            {
                return false;
            }

            var bomb = new Bomb(_bomb.FuseTurns, _bomb.Damage, _bomb.BlastRadius);

            // 놓은 자리에 일단 머문다. 이번 턴 중력은 건너뛰고, 다음 행동부터 떨어진다.
            bomb.HoldForOneTurn();
            _grid.Place(bomb, cell);
            return true;
        }

        /// <summary>
        /// 한 스텝 분의 도화선을 진행시키고, 다 탄 폭탄을 터뜨린다.
        /// 게임 루프의 페이즈로 매 턴 한 번 불린다.
        /// </summary>
        public BlastResult Tick()
        {
            _detonating.Clear();

            // 보드를 한 번 훑어 이번 턴에 터질 폭탄을 모은다.
            // 도는 도중에 격자를 바꾸면 안 되므로 수집과 폭발을 나눈다.
            for (int row = 0; row < _grid.Rows; row++)
            {
                for (int col = 0; col < _grid.Cols; col++)
                {
                    var bomb = _grid[col, row] as Bomb;
                    if (bomb != null && bomb.TickFuse())
                    {
                        _detonating.Add(new Vector2Int(col, row));
                    }
                }
            }

            if (_detonating.Count == 0)
            {
                return BlastResult.None;
            }

            return Detonate();
        }

        /// <summary>
        /// 수집된 폭탄을 터뜨린다. 연쇄가 켜져 있으면 범위에 걸린 폭탄이 목록에 추가되며
        /// 같은 루프에서 이어 터진다.
        /// </summary>
        private BlastResult Detonate()
        {
            Array.Clear(_marked, 0, _marked.Length);

            int exploded = 0;
            int destroyed = 0;
            int enemiesKilled = 0;
            int wallsDestroyed = 0;
            int gold = 0;
            int playerDamage = 0;

            // _detonating은 루프 도중 늘어날 수 있다(연쇄). 인덱스로 돌아야 하는 이유다.
            for (int b = 0; b < _detonating.Count; b++)
            {
                Vector2Int center = _detonating[b];

                var bomb = _grid[center] as Bomb;
                if (bomb == null)
                {
                    continue;   // 앞선 폭발에 이미 휩쓸려 사라졌다.
                }

                _grid.Remove(center);
                exploded++;

                int radius = bomb.BlastRadius;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        var cell = new Vector2Int(center.x + dx, center.y + dy);
                        if (!_grid.InBounds(cell))
                        {
                            continue;
                        }

                        // 프리뷰 줄은 아직 판에 들어오지 않은 대기 블록이라 손대지 않는다(WEEK1 §1).
                        if (cell.y < _board.FirstPlayableRow)
                        {
                            continue;
                        }

                        playerDamage += ApplyBlast(
                            cell, bomb.Damage, ref destroyed, ref enemiesKilled, ref wallsDestroyed, ref gold);
                    }
                }
            }

            _player.AddGold(gold);

            return new BlastResult(exploded, destroyed, enemiesKilled, wallsDestroyed, playerDamage, gold);
        }

        /// <summary>한 칸에 폭발을 적용한다.</summary>
        /// <returns>이 칸에서 플레이어가 받은 피해.</returns>
        private int ApplyBlast(
            Vector2Int cell, int damage, ref int destroyed, ref int enemiesKilled, ref int wallsDestroyed,
            ref int gold)
        {
            Entity occupant = _grid[cell];
            if (occupant == null)
            {
                return 0;
            }

            int index = (cell.y * _grid.Cols) + cell.x;

            if (occupant.Kind == EntityKind.Player)
            {
                // 여러 폭탄에 겹쳐 맞아도 한 번만 맞는다. 체력이 3~4라 중첩되면 설명 없이 즉사한다.
                if (!_bomb.DamagesPlayer || _marked[index])
                {
                    return 0;
                }

                _marked[index] = true;
                _player.TakeDamage(damage);
                return damage;
            }

            var other = occupant as Bomb;
            if (other != null)
            {
                if (_bomb.ChainDetonates && !_marked[index])
                {
                    _marked[index] = true;
                    other.ForceDetonate();
                    _detonating.Add(cell);
                }

                return 0;
            }

            var trash = occupant as Trash;
            if (trash == null || _marked[index])
            {
                return 0;
            }

            // 같은 블록이 두 폭탄의 범위에 겹쳐도 한 번만 맞는다.
            _marked[index] = true;
            trash.TakeDamage(damage);
            if (trash.IsDead)
            {
                _grid.Remove(cell);
                destroyed++;
                gold += trash.Gold;
                if (trash.IsEnemy) { enemiesKilled++; } else { wallsDestroyed++; }
            }

            return 0;
        }

        private bool IsPlaceable(Vector2Int cell)
        {
            return _grid.InBounds(cell)
                   && cell.y >= _board.FirstPlayableRow
                   && _grid.IsEmpty(cell);
        }
    }
}
