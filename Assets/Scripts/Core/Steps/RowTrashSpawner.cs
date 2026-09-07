using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 런 시작에 깔리는 줄을 만든다(WEEK1 §5: "하단 3줄이 채워진 상태로 시작").
    /// 가로 한 줄을 통째로 프리뷰 줄에 얹고, 아래로 내리는 건 GameLoop의 중력이 한다.
    ///
    /// GapsPerRow는 한 줄에 몇 칸을 비워 둘지다. 확정값은 0(꽉 찬 줄)이지만
    /// 난이도 실험용으로 인스펙터에 남겨 둔다(Hard Rule 1).
    /// </summary>
    public sealed class RowTrashSpawner : ISeedSpawner
    {
        private static readonly int TrashTypeCount = Enum.GetValues(typeof(TrashType)).Length;

        private readonly BoardGrid _grid;
        private readonly IBoardConfig _config;
        private readonly IRandomSource _random;

        /// <summary>컬럼 순서 셔플용 재사용 버퍼(Hard Rule 8).</summary>
        private readonly List<int> _columns;

        public RowTrashSpawner(BoardGrid grid, IBoardConfig config, IRandomSource random)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _columns = new List<int>(grid.Cols);
        }

        public int SpawnRow()
        {
            int gaps = Mathf.Clamp(_config.GapsPerRow, 0, _grid.Cols - 1);

            _columns.Clear();
            for (int col = 0; col < _grid.Cols; col++)
            {
                _columns.Add(col);
            }

            // 앞쪽 gaps개를 부분 셔플해서 "비워 둘 컬럼"으로 뽑는다.
            // 전체를 섞을 필요가 없어 매 줄마다 Cols번이 아니라 gaps번만 돈다.
            for (int i = 0; i < gaps; i++)
            {
                int j = i + _random.NextInt(0, _columns.Count - i);
                int swap = _columns[i];
                _columns[i] = _columns[j];
                _columns[j] = swap;
            }

            int spawned = 0;
            for (int i = gaps; i < _columns.Count; i++)
            {
                var position = new Vector2Int(_columns[i], 0);

                // 프리뷰 칸이 이미 막힌 컬럼은 건너뛴다. 시작 줄 수가 보드보다 많을 때만 생긴다.
                if (!_grid.IsEmpty(position))
                {
                    continue;
                }

                _grid.Place(new Trash((TrashType)_random.NextInt(0, TrashTypeCount)), position);
                spawned++;
            }

            return spawned;
        }
    }
}
