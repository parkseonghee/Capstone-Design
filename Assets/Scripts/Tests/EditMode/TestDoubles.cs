using System;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// ScriptableObject 없이 설정을 넣기 위한 페이크.
    /// Core가 IBoardConfig에만 의존하기 때문에 가능하다.
    /// </summary>
    internal sealed class FakeBoardConfig : IBoardConfig
    {
        public int Cols { get; set; } = 8;

        public int Rows { get; set; } = 12;

        // 기존 테스트가 검사하던 동작을 그대로 유지하려고 페이크의 기본값은 칸 단위 스폰이다.
        // 줄 단위 동작은 RowSpawnTests가 Mode를 명시적으로 켜서 검사한다.
        public SpawnMode Mode { get; set; } = SpawnMode.SingleBlock;

        public int RowsOnStart { get; set; }

        public int StepsPerRow { get; set; } = 3;

        public int GapsPerRow { get; set; } = 1;

        public int SpawnPerStep { get; set; } = 1;

        public bool AdvanceOnBlocked { get; set; }

        public Vector2Int PlayerStart { get; set; } = new Vector2Int(4, 11);
    }

    /// <summary>항상 최솟값을 돌려준다 — 스폰이 가장 왼쪽 빈 칸 / TrashType.A로 고정된다.</summary>
    internal sealed class MinRandom : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }

    /// <summary>정해진 값을 순서대로 돌려주고, 다 쓰면 처음부터 반복한다.</summary>
    internal sealed class ScriptedRandom : IRandomSource
    {
        private readonly int[] _values;
        private int _cursor;

        public ScriptedRandom(params int[] values)
        {
            if (values == null || values.Length == 0)
            {
                throw new ArgumentException("At least one value is required.", nameof(values));
            }

            _values = values;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            int value = _values[_cursor % _values.Length];
            _cursor++;

            if (value < minInclusive || value >= maxExclusive)
            {
                throw new InvalidOperationException(
                    $"Scripted value {value} is outside the requested range [{minInclusive}, {maxExclusive}).");
            }

            return value;
        }
    }
}
