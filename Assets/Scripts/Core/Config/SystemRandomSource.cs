using System;

namespace RecycleLife.Core
{
    /// <summary>
    /// 시드를 남길 수 있는 런타임용 난수. 시드를 로그로 남기면 버그 재현이 가능하다.
    /// </summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SystemRandomSource(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Seed { get; }

        public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }
}
