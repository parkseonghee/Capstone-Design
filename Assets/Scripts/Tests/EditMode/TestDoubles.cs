using System;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// ScriptableObject 없이 설정을 넣기 위한 페이크.
    /// Core가 IBoardConfig / ISpawnConfig에만 의존하기 때문에 가능하다.
    ///
    /// 한 클래스가 두 인터페이스를 다 구현하는 건 테스트 편의일 뿐이다 —
    /// 런타임에서는 GridConfig와 SpawnConfig 두 에셋으로 갈라져 있다.
    /// </summary>
    internal sealed class FakeBoardConfig : IBoardConfig, ISpawnConfig
    {
        // ── 보드 (WEEK1 §1 확정값) ──────────────────────────────────────────
        public int Cols { get; set; } = 8;

        public int PlayableRows { get; set; } = 8;

        public int PreviewRows { get; set; } = 1;

        public int Rows => PlayableRows + PreviewRows;

        public int FirstPlayableRow => PreviewRows;

        public int RowsOnStart { get; set; }

        public int GapsPerRow { get; set; }

        public bool AdvanceOnBlocked { get; set; }

        public Vector2Int PlayerStart { get; set; } = new Vector2Int(4, 8);

        // ── 스폰 캐이던스 (WEEK1 §5 확정값) ─────────────────────────────────
        public int BlocksBeforeSlowdown { get; set; } = 15;

        public int TurnsPerSpawnEarly { get; set; } = 1;

        public int TurnsPerSpawnLate { get; set; } = 2;

        public int BlocksPerSpawn { get; set; } = 1;
    }

    /// <summary>
    /// 배선을 한곳에 모아 테스트가 팩토리 시그니처 변경에 흔들리지 않게 한다.
    /// 페이크가 보드와 스폰 설정을 겸하므로 같은 객체를 두 번 넘긴다.
    /// </summary>
    internal static class Make
    {
        public static GameLoop Week1(FakeBoardConfig config, IRandomSource random)
            => GameLoopFactory.CreateWeek1(config, config, random);

        public static GameLoop Staged(FakeBoardConfig config, IRandomSource random)
            => GameLoopFactory.CreateStaged(config, config, random);
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
