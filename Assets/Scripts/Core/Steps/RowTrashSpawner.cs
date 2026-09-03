using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 가로 한 줄을 통째로 상단에 투입한다 — 원작 레퍼런스의 낙하 방식.
    ///
    /// 두 가지 축을 인스펙터에서 조절한다:
    ///  - StepsPerRow : 몇 스텝마다 새 줄이 내려오는지 (압박 속도)
    ///  - GapsPerRow  : 한 줄에 몇 칸을 비워 두는지 (0이면 완전히 꽉 찬 줄)
    ///
    /// 런 시작 시의 "3줄이 먼저 내려온다"는 GameLoop.SeedInitialBoard가
    /// SpawnImmediate를 RowsOnStart번 부르고 사이사이 중력을 돌려 만든다.
    /// </summary>
    public sealed class RowTrashSpawner : ITrashSpawner
    {
        private static readonly int TrashTypeCount = Enum.GetValues(typeof(TrashType)).Length;

        private readonly BoardGrid _grid;
        private readonly IBoardConfig _config;
        private readonly IRandomSource _random;

        /// <summary>컬럼 순서 셔플용 재사용 버퍼(Hard Rule 8).</summary>
        private readonly List<int> _columns;

        private int _stepsSinceRow;

        public RowTrashSpawner(BoardGrid grid, IBoardConfig config, IRandomSource random)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _columns = new List<int>(grid.Cols);
        }

        /// <summary>매 스텝 호출된다. 주기가 차지 않으면 아무것도 하지 않는다.</summary>
        public int Spawn()
        {
            int cadence = Mathf.Max(1, _config.StepsPerRow);

            _stepsSinceRow++;
            if (_stepsSinceRow < cadence)
            {
                return 0;
            }

            _stepsSinceRow = 0;
            return SpawnImmediate();
        }

        /// <summary>주기를 무시하고 한 줄을 지금 투입한다.</summary>
        public int SpawnImmediate()
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

                // 상단이 이미 막힌 컬럼은 건너뛴다. 전부 막혔으면 0을 돌려주고,
                // 그 자체가 오버플로 신호가 된다(WEEK1 §6).
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
