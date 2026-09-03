using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// WEEK1 §5: 상단 row(0)의 빈 칸 중 SpawnPerStep개를 골라 랜덤 TrashType으로 채운다.
    /// PCG 단계에서 스폰 테이블 기반 구현으로 교체된다.
    /// </summary>
    public sealed class RandomTopRowSpawner : ITrashSpawner
    {
        /// <summary>Enum.GetValues는 배열을 할당하므로 최초 1회만 세어 캐시한다.</summary>
        private static readonly int TrashTypeCount = Enum.GetValues(typeof(TrashType)).Length;

        private readonly BoardGrid _grid;
        private readonly IBoardConfig _config;
        private readonly IRandomSource _random;

        /// <summary>매 스텝 재사용하는 후보 칸 버퍼(Hard Rule 8).</summary>
        private readonly List<int> _emptyColumns;

        public RandomTopRowSpawner(BoardGrid grid, IBoardConfig config, IRandomSource random)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _emptyColumns = new List<int>(grid.Cols);
        }

        /// <summary>이 구현은 주기가 없어 Spawn과 동작이 같다.</summary>
        public int SpawnImmediate() => Spawn();

        public int Spawn()
        {
            int spawned = 0;

            for (int i = 0; i < _config.SpawnPerStep; i++)
            {
                _emptyColumns.Clear();
                for (int col = 0; col < _grid.Cols; col++)
                {
                    if (_grid.IsEmpty(col, 0))
                    {
                        _emptyColumns.Add(col);
                    }
                }

                if (_emptyColumns.Count == 0)
                {
                    break; // 상단이 꽉 찼다 — GameOverChecker가 판단한다(§6).
                }

                int column = _emptyColumns[_random.NextInt(0, _emptyColumns.Count)];
                var type = (TrashType)_random.NextInt(0, TrashTypeCount);

                // 스텝당 1~2회의 불가피한 할당. 전투 단계에서 제거가 생기면 풀링을 검토한다.
                _grid.Place(new Trash(type), new Vector2Int(column, 0));
                spawned++;
            }

            return spawned;
        }
    }
}
