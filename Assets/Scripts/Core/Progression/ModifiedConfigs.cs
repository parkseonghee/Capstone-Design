using System;

namespace RecycleLife.Core
{
    /// <summary>
    /// 폭탄 설정에 유물 보너스를 얹는다. R05 대형 폭약(3x3 → 5x5)이 여기로 들어온다.
    ///
    /// BombResolver를 고치는 대신 설정을 한 겹 덮었다 — 기존 코드는 자기가 유물을 쓰는지도 모른다.
    /// WaveSpawnWeights와 같은 방식이다(Hard Rule 3·9).
    /// </summary>
    public sealed class ModifiedBombConfig : IBombConfig
    {
        private readonly IBombConfig _base;
        private readonly RunModifiers _mods;

        public ModifiedBombConfig(IBombConfig baseConfig, RunModifiers mods)
        {
            _base = baseConfig ?? throw new ArgumentNullException(nameof(baseConfig));
            _mods = mods ?? throw new ArgumentNullException(nameof(mods));
        }

        public int FuseTurns => _base.FuseTurns;

        public int Damage => _base.Damage;

        public int BlastRadius => _base.BlastRadius + _mods.BonusBlastRadius;

        public int StartingCount => _base.StartingCount + _mods.BonusBombs;

        /// <summary>R09 방폭 장비를 들면 자기 폭탄에도 안 맞는다(CSV 비고: "자신의 폭탄 피해 무효").</summary>
        public bool DamagesPlayer => _base.DamagesPlayer && !_mods.ImmuneToBlast;

        public bool ChainDetonates => _base.ChainDetonates;
    }

    /// <summary>
    /// 블록 능력치에 유물 보너스를 얹는다. R06 진한 포션(회복 2 → 3)이 여기로 들어온다.
    ///
    /// 회복량이 있는 종류(=아이템)에만 더한다 — 적이나 벽의 값은 건드리지 않는다.
    /// </summary>
    public sealed class ModifiedTrashStats : ITrashStatsProvider
    {
        private readonly ITrashStatsProvider _base;
        private readonly RunModifiers _mods;

        public ModifiedTrashStats(ITrashStatsProvider baseStats, RunModifiers mods)
        {
            _base = baseStats ?? throw new ArgumentNullException(nameof(baseStats));
            _mods = mods ?? throw new ArgumentNullException(nameof(mods));
        }

        public TrashStats For(TrashType type)
        {
            TrashStats stats = _base.For(type);

            if (_mods.BonusPotionHeal <= 0 || !stats.IsConsumable)
            {
                return stats;
            }

            return stats.WithHeal(stats.Heal + _mods.BonusPotionHeal);
        }
    }
}
