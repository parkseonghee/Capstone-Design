using System;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 스폰할 블록 종류를 <b>가중치</b>에 따라 뽑는다.
    ///
    /// 균등 추첨을 쓰지 않는 이유: 종류를 하나 더할 때마다 기존 종류의 출현율이 저절로 깎인다.
    /// 포션처럼 드물게 나와야 하는 것을 넣으면서 적 4종의 비율은 그대로 두려면 가중치가 필요하다.
    /// 값은 TrashStatsConfig 에셋에서 종류별로 조절한다(Hard Rule 1).
    ///
    /// 난수는 뽑기당 정확히 한 번만 쓴다 — 시드 재현성이 종류 수에 흔들리지 않게 하기 위함이다.
    /// </summary>
    public sealed class TrashTypePicker
    {
        /// <summary>Enum.GetValues는 배열을 할당하므로 최초 1회만 세어 캐시한다(Hard Rule 8).</summary>
        private static readonly int TypeCount = Enum.GetValues(typeof(TrashType)).Length;

        private readonly ITrashStatsProvider _stats;
        private readonly IRandomSource _random;

        public TrashTypePicker(ITrashStatsProvider stats, IRandomSource random)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public TrashType Next()
        {
            int total = 0;
            for (int i = 0; i < TypeCount; i++)
            {
                total += WeightOf(i);
            }

            // 설정이 비어 있으면(전부 0) 아무것도 못 뽑는 상태가 된다. 균등 추첨으로 물러선다.
            if (total <= 0)
            {
                return (TrashType)_random.NextInt(0, TypeCount);
            }

            int roll = _random.NextInt(0, total);
            for (int i = 0; i < TypeCount; i++)
            {
                roll -= WeightOf(i);
                if (roll < 0)
                {
                    return (TrashType)i;
                }
            }

            return (TrashType)(TypeCount - 1);
        }

        private int WeightOf(int index) => Mathf.Max(0, _stats.For((TrashType)index).SpawnWeight);
    }
}
