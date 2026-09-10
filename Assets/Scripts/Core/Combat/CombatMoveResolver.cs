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
    ///  - 공격은 캐릭터의 <b>공격 범위(오프셋 리스트)</b>만큼 여러 칸에 동시에 들어가고,
    ///    각 칸이 독립적으로 연쇄를 시작한다(기획서 §1 캐릭터 B).
    ///  - 공격 범위는 <b>공격한 방향을 따라 회전</b>한다. "양옆"은 언제나 공격 축의 좌우이므로
    ///    위·아래·좌·우 어디를 쳐도 같은 모양으로 들어간다(기획 확정).
    ///  - 같은 종류로 이어진 덩어리 전체가 <b>각각</b> 캐릭터 공격력만큼 맞는다(§8-1: 종류 기준).
    ///  - 반격은 <b>부딪힌 그 한 마리</b>만 한다. 그 마리를 처치했으면 무피해(§8-2: 원작 원형).
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

        /// <summary>이번 공격에 맞는 칸들. 매 공격 재사용해 할당을 만들지 않는다(Hard Rule 8).</summary>
        private readonly List<Vector2Int> _hits;

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

            var bumped = _grid[target] as Trash;
            if (bumped == null)
            {
                _grid.Move(_player.Position, target);
                return MoveResult.Simple(MoveOutcome.Moved);
            }

            // 무엇을 할지는 블록의 값이 정한다 — 종류 enum을 여기서 갈라 보지 않는다(Hard Rule 3).
            return bumped.IsConsumable ? Consume(target) : Attack(target, bumped, direction);
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

        /// <summary>CORE_COMBAT.md §5의 데미지 해결 순서 + 기획서 §1의 다중 시작점.</summary>
        private MoveResult Attack(Vector2Int target, Trash bumped, Direction direction)
        {
            CollectHits(target, direction);

            // 1) 맞는 칸 전원에게 먼저 피해를 넣는다.
            //    제거를 섞지 않는 이유: 중간에 칸을 비우면 남은 좌표의 조회가 흔들린다.
            for (int i = 0; i < _hits.Count; i++)
            {
                var enemy = (Trash)_grid[_hits[i]];

                // 피해량은 캐릭터 설정이 아니라 플레이어의 <b>현재</b> 공격력에서 온다.
                // 유물로 공격력이 오르면 설정값은 그대로여도 이쪽이 따라 올라야 하기 때문이다.
                enemy.TakeDamage(_player.Attack);
            }

            // 2) 죽은 것만 걷어낸다. 생긴 빈 칸은 다음 페이즈의 중력이 메운다.
            int killed = 0;
            for (int i = 0; i < _hits.Count; i++)
            {
                Vector2Int cell = _hits[i];
                var enemy = (Trash)_grid[cell];
                if (enemy.IsDead)
                {
                    _grid.Remove(cell);
                    killed++;
                }
            }

            // 3) 반격. 부딪힌 한 마리가 살아남았을 때만, 그 마리의 공격력만큼.
            //    옆으로 같이 맞은 적은 반격하지 않는다 — 부딪힌 대상만 반격한다는 §8-2 규칙 그대로다.
            int damage = 0;
            if (!bumped.IsDead)
            {
                damage = bumped.Attack;
                _player.TakeDamage(damage);
            }

            return new MoveResult(MoveOutcome.Attacked, _hits.Count, killed, damage, 0);
        }

        /// <summary>
        /// 캐릭터의 공격 범위를 펼쳐 이번에 맞는 칸을 모은다.
        ///
        /// 각 오프셋 칸이 <b>독립적으로</b> 연쇄를 시작하되, 같은 칸이 두 번 들어오면 표시로 걸러낸다.
        /// 중복을 허용하면 중앙과 옆이 한 덩어리로 이어져 있을 때 그 덩어리가 두 번 맞아
        /// "B는 체력 2 이상을 원샷 못 한다"는 밸런싱 전제가 깨진다(밸런싱 v1 §1-2).
        /// </summary>
        private void CollectHits(Vector2Int target, Direction direction)
        {
            _hits.Clear();
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

                var origin = _grid[start] as Trash;

                // 아이템은 때리는 대상이 아니다. 부딪혀서 먹는 것만 소비된다.
                if (origin == null || origin.IsConsumable)
                {
                    continue;
                }

                _chain.Find(start);
                IReadOnlyList<Vector2Int> group = _chain.Group;

                // Group은 다음 Find에서 덮어써지므로 여기서 바로 옮겨 담는다.
                for (int i = 0; i < group.Count; i++)
                {
                    Vector2Int cell = group[i];
                    int index = cell.y * _grid.Cols + cell.x;
                    if (_marked[index])
                    {
                        continue;
                    }

                    _marked[index] = true;
                    _hits.Add(cell);
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

        /// <summary>
        /// 아이템을 먹는다. 공격과 같은 연쇄 규칙을 타므로 붙어 있는 같은 아이템까지 한 번에 사라지고,
        /// 회복량은 개수만큼 합산된다. 공격 범위(오프셋)는 쓰지 않는다 — 먹는 건 부딪힌 것뿐이다.
        ///
        /// <b>플레이어는 제자리에 있는다.</b> 공격과 규칙을 맞춘 것이다 —
        /// 버튼 한 번이 "때리거나 먹는다"이지 "때리고 들어간다"가 아니어야,
        /// 한 칸 전진할 생각이 없을 때 실수로 밀려 들어가는 일이 없다(기획 확정).
        ///
        /// 넘치는 회복은 버려진다 — 체력이 2/3일 때 2회복 포션 세 개를 한 번에 먹으면 6이 아니라 1만 찬다.
        /// 그래서 "언제 먹을지" 자체가 판단거리가 된다.
        /// </summary>
        private MoveResult Consume(Vector2Int target)
        {
            int size = _chain.Find(target);
            IReadOnlyList<Vector2Int> group = _chain.Group;

            int restores = 0;
            for (int i = 0; i < group.Count; i++)
            {
                var item = (Trash)_grid[group[i]];
                restores += item.Heal;
                _grid.Remove(group[i]);
            }

            int healed = _player.Heal(restores);

            // 비워진 칸으로 들어가지 않는다. 빈자리는 다음 페이즈의 중력이 메운다 — 공격했을 때와 똑같다.
            return new MoveResult(MoveOutcome.Consumed, size, 0, 0, healed);
        }
    }
}
