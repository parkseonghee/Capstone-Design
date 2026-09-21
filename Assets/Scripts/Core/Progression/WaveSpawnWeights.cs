using System;

namespace RecycleLife.Core
{
    /// <summary>
    /// 전역 스탯 표를 그대로 흘려보내되 <b>스폰 가중치만</b> 현재 웨이브 값으로 바꿔치기한다.
    ///
    /// 이렇게 감싼 이유: 스포너와 TrashTypePicker는 이미 ITrashStatsProvider에서
    /// 가중치를 읽고 있다. 그 경로에 웨이브를 끼워 넣으려고 스포너 두 개와 피커의
    /// 생성자를 전부 고치는 대신, 같은 인터페이스로 한 겹 덮었다 —
    /// 기존 코드는 자기가 웨이브를 쓰는지도 모른다(Hard Rule 3·9).
    ///
    /// 체력·공격력·회복량은 웨이브와 무관하게 전역 표의 값을 쓴다.
    /// TrashStats는 struct라 감싸도 할당이 생기지 않는다(Hard Rule 8).
    /// </summary>
    public sealed class WaveSpawnWeights : ITrashStatsProvider
    {
        private readonly ITrashStatsProvider _stats;
        private readonly WaveRunner _waves;

        public WaveSpawnWeights(ITrashStatsProvider stats, WaveRunner waves)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _waves = waves ?? throw new ArgumentNullException(nameof(waves));
        }

        public TrashStats For(TrashType type)
        {
            TrashStats stats = _stats.For(type);

            IWaveConfig wave = _waves.Current;
            if (wave == null)
            {
                // 웨이브를 안 쓰거나 이미 다 끝난 런. 전역 표를 그대로 쓴다.
                return stats;
            }

            // 가중치만 갈아끼운다. 나머지 값(체력·공격력·회복량·골드)은 전역 표 그대로 따라온다.
            return stats.WithSpawnWeight(wave.SpawnWeightFor(type));
        }
    }
}
