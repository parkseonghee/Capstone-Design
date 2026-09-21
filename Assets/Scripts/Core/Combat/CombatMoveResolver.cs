using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// CORE_COMBAT.md §3·§5·§6의 범프 전투. BlockingMoveResolver를 대체한다.
    ///
    /// 확정된 규칙:
    ///  - 빈 칸이면 이동. 그 외에는 <b>무조건 제자리</b>다 —
    ///    적이면 공격, 아이템이면 먹기. 어느 쪽이든 플레이어는 그 칸으로 들어가지 않는다.
    ///  - 캐릭터의 <b>공격 범위(오프셋 리스트)</b>가 언제나 그대로 적용된다.
    ///    부딪힌 게 적이든 포션이든 상관없이 3칸이면 3칸이다.
    ///  - 범위 안의 각 칸은 <b>자기 성격대로</b> 처리된다 —
    ///    적이면 피해를 받고, 포션이면 먹혀서 플레이어를 회복시킨다.
    ///    그리고 각 칸은 <b>독립적으로</b> 연쇄를 시작한다(기획서 §1 캐릭터 B).
    ///  - 공격 범위는 <b>공격한 방향을 따라 회전</b>한다. "양옆"은 언제나 공격 축의 좌우이므로
    ///    위·아래·좌·우 어디를 쳐도 같은 모양으로 들어간다(기획 확정).
    ///  - 반격은 <b>부딪힌 그 한 마리</b>만 한다. 그 마리를 처치했으면 무피해(§8-2: 원작 원형).
    ///    옆으로 같이 맞은 적은 반격하지 않는다.
    ///  - 범위 안에 <b>폭탄</b>이 있으면 불이 붙는다. 설치된 폭탄은 때려야 카운트다운이 시작된다
    ///    (폭탄 기획 §1-2). 폭탄은 체력이 없어 피해를 받지 않는다.
    ///
    /// 프리뷰 줄은 손댈 수 없다 — 이동도 공격도 대상이 아니다(WEEK1 §1).
    /// BlockedByEntity는 이 구현에서 나오지 않는다. 막는 지형이 생기면 그때 쓴다.
    /// </summary>
    public sealed class CombatMoveResolver : IMoveResolver
    {
        private readonly BoardGrid _grid;
        private readonly Player _player;
        private readonly IBoardConfig _config;
        private readonly ICharacterConfig _character;
        private readonly ChainFinder _chain;

        /// <summary>이번 행동에 피해를 받을 칸들. 매번 재사용해 할당을 만들지 않는다(Hard Rule 8).</summary>
        private readonly List<Vector2Int> _hits;

        /// <summary>이번 행동에 먹힐 아이템 칸들.</summary>
        private readonly List<Vector2Int> _eats;

        /// <summary>이번 행동에 불이 붙을 폭탄 칸들.</summary>
        private readonly List<Vector2Int> _arms;

        /// <summary>같은 칸이 두 번 들어오지 않게 하는 표시. 인덱스는 row * Cols + col.</summary>
        private readonly bool[] _marked;

        public CombatMoveResolver(
            BoardGrid grid,
            Player player,
            IBoardConfig config,
            ICharacterConfig character,
            ChainFinder chain)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _character = character ?? throw new ArgumentNullException(nameof(character));
            _chain = chain ?? throw new ArgumentNullException(nameof(chain));

            _hits = new List<Vector2Int>(grid.CellCount);
            _eats = new List<Vector2Int>(grid.CellCount);
            _arms = new List<Vector2Int>(grid.CellCount);
            _marked = new bool[grid.CellCount];
        }

        public MoveResult Resolve(Direction direction)
        {
            Vector2Int target = _player.Position + direction.ToOffset();

            // 경계 밖과 프리뷰 줄은 둘 다 무효 입력이다 — 보드를 진행시키지 않는다(WEEK1 §3).
            if (!CanAct(target))
            {
                return MoveResult.Simple(MoveOutcome.OutOfBounds);
            }

            Entity bumped = _grid[target];
            if (bumped == null)
            {
                _grid.Move(_player.Position, target);
                return MoveResult.Simple(MoveOutcome.Moved);
            }

            return Strike(target, bumped, direction);
        }

        public bool CanAct(Vector2Int target)
        {
            if (!_grid.InBounds(target))
            {
                return false;
            }

            // 프리뷰 줄은 플레이어에게 보드 밖과 같다. 이동도 공격도 안 된다.
            // 전투가 붙은 뒤에도 플레이 영역 안이면 언제나 할 일이 있다 —
            // 비었으면 이동, 블록이 있으면 공격. 그래서 갇힘이 사실상 사라진다.
            return target.y >= _config.FirstPlayableRow;
        }

        /// <summary>
        /// 한 번의 행동을 해결한다. CORE_COMBAT.md §5의 순서에 아이템 소비를 합친 것이다.
        ///
        /// 적을 치든 포션을 먹든 <b>같은 경로</b>를 탄다. 공격 범위는 부딪힌 대상이 무엇인지와
        /// 무관하게 그대로 펼쳐지고, 각 칸이 자기 성격대로 처리될 뿐이다(기획 확정).
        /// </summary>
        private MoveResult Strike(Vector2Int target, Entity bumped, Direction direction)
        {
            CollectCells(target, direction);

            // 1) 적에게 먼저 피해를 넣는다.
            //    제거를 섞지 않는 이유: 중간에 칸을 비우면 남은 좌표의 조회가 흔들린다.
            for (int i = 0; i < _hits.Count; i++)
            {
                var enemy = (Trash)_grid[_hits[i]];

                // 피해량은 캐릭터 설정이 아니라 플레이어의 현재 공격력에서 온다.
                // 유물로 공격력이 오르면 설정값은 그대로여도 이쪽이 따라 올라야 하기 때문이다.
                enemy.TakeDamage(_player.Attack);
            }

            // 2) 죽은 것만 걷어낸다. 생긴 빈 칸은 다음 페이즈의 중력이 메운다.
            //    적과 벽을 나눠 세는 건 웨이브 진행도가 적만 세기 때문이다(밸런싱 v1 §2-1-4).
            int killed = 0;
            int enemiesKilled = 0;
            int wallsDestroyed = 0;
            int gold = 0;
            for (int i = 0; i < _hits.Count; i++)
            {
                Vector2Int cell = _hits[i];
                var hit = (Trash)_grid[cell];
                if (hit.IsDead)
                {
                    _grid.Remove(cell);
                    killed++;
                    gold += hit.Gold;
                    if (hit.IsEnemy) { enemiesKilled++; } else { wallsDestroyed++; }
                }
            }

            // 3) 아이템을 먹는다. 회복량은 합산되고, 최대 체력에서 잘린다 —
            //    넘치는 만큼은 버려지므로 "언제 먹을지"가 판단거리가 된다.
            int restores = 0;
            for (int i = 0; i < _eats.Count; i++)
            {
                Vector2Int cell = _eats[i];
                restores += ((Trash)_grid[cell]).Heal;
                _grid.Remove(cell);
            }

            int healed = _player.Heal(restores);

            // 4) 폭탄에 불을 붙인다. 폭탄은 피해를 받지 않고 카운트다운만 시작한다.
            for (int i = 0; i < _arms.Count; i++)
            {
                ((Bomb)_grid[_arms[i]]).Arm();
            }

            // 5) 반격. 부딪힌 게 <b>적</b>이고 살아남았을 때만, 그 한 마리의 공격력만큼.
            //    포션이나 폭탄을 부딪혔다면 반격은 없다.
            int damage = 0;
            var bumpedTrash = bumped as Trash;
            if (bumpedTrash != null && !bumpedTrash.IsConsumable && !bumpedTrash.IsDead)
            {
                damage = bumpedTrash.Attack;
                _player.TakeDamage(damage);
            }

            // 결과 이름은 "부딪힌 것"을 따른다 — 무엇을 하러 간 턴인지는 플레이어의 의도이고,
            // 옆에 무엇이 딸려 왔는지는 부수적이다.
            MoveOutcome outcome = bumped.Kind == EntityKind.Bomb
                ? MoveOutcome.Armed
                : (bumpedTrash.IsConsumable ? MoveOutcome.Consumed : MoveOutcome.Attacked);

            _player.AddGold(gold);

            return new MoveResult(
                outcome,
                _hits.Count + _eats.Count + _arms.Count,
                killed,
                enemiesKilled,
                wallsDestroyed,
                damage,
                healed,
                gold);
        }

        /// <summary>
        /// 캐릭터의 공격 범위를 펼쳐 이번에 영향을 받는 칸을 모은다.
        /// 적은 _hits로, 아이템은 _eats로 나눠 담는다.
        ///
        /// 각 오프셋 칸이 <b>독립적으로</b> 연쇄를 시작하되, 같은 칸이 두 번 들어오면 표시로 걸러낸다.
        /// 중복을 허용하면 중앙과 옆이 한 덩어리로 이어져 있을 때 그 덩어리가 두 번 맞아
        /// "B는 체력 2 이상을 원샷 못 한다"는 밸런싱 전제가 깨진다(밸런싱 v1 §1-2).
        /// </summary>
        private void CollectCells(Vector2Int target, Direction direction)
        {
            _hits.Clear();
            _eats.Clear();
            _arms.Clear();
            Array.Clear(_marked, 0, _marked.Length);

            IReadOnlyList<Vector2Int> offsets = _character.AttackOffsets;
            for (int o = 0; o < offsets.Count; o++)
            {
                Vector2Int start = target + Rotate(offsets[o], direction);

                // 보드 밖·프리뷰 줄·빈 칸은 그냥 무시한다(기획서: "공허 타격").
                if (!CanAct(start))
                {
                    continue;
                }

                int startIndex = (start.y * _grid.Cols) + start.x;

                // 폭탄은 연쇄를 타지 않는다. 한 칸짜리로 불만 붙인다.
                if (_grid[start] is Bomb)
                {
                    if (!_marked[startIndex])
                    {
                        _marked[startIndex] = true;
                        _arms.Add(start);
                    }

                    continue;
                }

                if (!(_grid[start] is Trash))
                {
                    continue;
                }

                _chain.Find(start);
                IReadOnlyList<Vector2Int> group = _chain.Group;

                // Group은 다음 Find에서 덮어써지므로 여기서 바로 옮겨 담는다.
                for (int i = 0; i < group.Count; i++)
                {
                    Vector2Int cell = group[i];
                    int index = (cell.y * _grid.Cols) + cell.x;
                    if (_marked[index])
                    {
                        continue;
                    }

                    _marked[index] = true;

                    // 같은 덩어리는 같은 종류라 성격도 같지만, 칸마다 보는 편이 안전하다.
                    if (((Trash)_grid[cell]).IsConsumable)
                    {
                        _eats.Add(cell);
                    }
                    else
                    {
                        _hits.Add(cell);
                    }
                }
            }
        }

        /// <summary>
        /// 오프셋을 공격 방향에 맞춰 돌린다.
        ///
        /// 오프셋은 "아래로 공격할 때"를 기준으로 적혀 있다 — +y가 공격 방향(전방),
        /// +x가 그 오른쪽이다. 회전하지 않으면 좌우로 공격할 때 "양옆"이 공격 축과 겹쳐서
        /// 한쪽은 플레이어 자기 칸, 다른 한쪽은 목표 너머가 되어 옆공격이 사실상 사라진다.
        ///
        /// row가 아래로 증가하는 좌표계라 전방 F에 대한 오른쪽은 (F.y, -F.x)다.
        /// </summary>
        private static Vector2Int Rotate(Vector2Int offset, Direction direction)
        {
            Vector2Int forward = direction.ToOffset();
            Vector2Int right = new Vector2Int(forward.y, -forward.x);

            return new Vector2Int(
                (offset.x * right.x) + (offset.y * forward.x),
                (offset.x * right.y) + (offset.y * forward.y));
        }
    }
}
