using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// CORE_COMBAT.md §4의 Flood Fill. 시작 칸의 쓰레기와 IChainRule로 이어지는
    /// 이웃을 전부 모은다. <b>인접은 4방향</b>이다(기획 확인 완료, §8-3).
    ///
    /// 버퍼(그룹·프론티어·방문표)를 필드로 들고 재사용하기 때문에
    /// 매 공격마다 컬렉션을 새로 만들지 않는다(Hard Rule 8).
    /// 그래서 <see cref="Group"/>은 <b>다음 Find 호출 전까지만</b> 유효하다.
    ///
    /// <b>프리뷰 줄은 덩어리에 절대 포함하지 않는다.</b> 아직 판에 들어오지 않은 블록이라
    /// 플레이어가 어떤 방식으로도 손댈 수 없어야 하는데(WEEK1 §1), 경계를 모르면
    /// 첫 행에서 친 연쇄가 위쪽 대기 블록까지 타고 올라간다.
    /// </summary>
    public sealed class ChainFinder
    {
        private readonly BoardGrid _grid;
        private readonly IChainRule _rule;
        private readonly int _firstPlayableRow;

        private readonly List<Vector2Int> _group;
        private readonly List<Vector2Int> _frontier;
        private readonly bool[] _visited;
        private readonly Vector2Int[] _neighbors = new Vector2Int[4];

        /// <param name="firstPlayableRow">이 행보다 위(작은 값)는 프리뷰 줄이라 덩어리에 넣지 않는다.</param>
        public ChainFinder(BoardGrid grid, IChainRule rule, int firstPlayableRow)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _firstPlayableRow = firstPlayableRow;

            _group = new List<Vector2Int>(grid.CellCount);
            _frontier = new List<Vector2Int>(grid.CellCount);
            _visited = new bool[grid.CellCount];
        }

        /// <summary>직전 Find가 찾은 덩어리. 다음 Find를 부르면 덮어써진다.</summary>
        public IReadOnlyList<Vector2Int> Group => _group;

        /// <summary>
        /// start 칸의 쓰레기와 연결된 덩어리를 찾는다.
        /// start가 비었거나 플레이어면 빈 결과(0)를 돌려준다.
        /// </summary>
        /// <returns>덩어리 크기(자기 자신 포함).</returns>
        public int Find(Vector2Int start)
        {
            _group.Clear();
            _frontier.Clear();
            Array.Clear(_visited, 0, _visited.Length);

            if (start.y < _firstPlayableRow)
            {
                return 0;
            }

            var origin = _grid[start] as Trash;
            if (origin == null)
            {
                return 0;
            }

            _visited[IndexOf(start)] = true;
            _frontier.Add(start);

            while (_frontier.Count > 0)
            {
                // 마지막 원소를 빼면 List가 내부 배열을 옮기지 않는다(순서는 무관).
                int last = _frontier.Count - 1;
                Vector2Int cell = _frontier[last];
                _frontier.RemoveAt(last);

                _group.Add(cell);

                int count = _grid.GetNeighbors4(cell, _neighbors);
                for (int i = 0; i < count; i++)
                {
                    Vector2Int neighbor = _neighbors[i];

                    // 프리뷰 줄로는 번지지 않는다.
                    if (neighbor.y < _firstPlayableRow)
                    {
                        continue;
                    }

                    int index = IndexOf(neighbor);

                    if (_visited[index])
                    {
                        continue;
                    }

                    var candidate = _grid[neighbor] as Trash;
                    if (candidate == null || !_rule.AreConnected(origin, candidate))
                    {
                        continue;
                    }

                    _visited[index] = true;
                    _frontier.Add(neighbor);
                }
            }

            return _group.Count;
        }

        private int IndexOf(Vector2Int cell) => cell.y * _grid.Cols + cell.x;
    }
}
