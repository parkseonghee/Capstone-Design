namespace RecycleLife.Core
{
    /// <summary>
    /// 확정된 연쇄 기준: <b>같은 종류끼리 묶인다</b>(기획 확인 완료, CORE_COMBAT.md §8-1).
    /// 원작(Pocket Dungeon)과 같은 규칙이며, 재질 기준 콤보는 나중에
    /// 별도 IChainRule 구현이나 점수 계층에서 얹는다.
    ///
    /// 단, 종류 자체가 "연쇄 안 함"으로 설정돼 있으면(벽) 묶이지 않는다.
    /// 그 판단은 여기서 하드코딩하지 않고 TrashStatsConfig 값이 정한다.
    ///
    /// 상태가 없어 런당 하나만 만들어 재사용한다.
    /// </summary>
    public sealed class SameTypeChainRule : IChainRule
    {
        public bool AreConnected(Trash origin, Trash candidate)
            => origin.ChainsWithSameType
               && candidate.ChainsWithSameType
               && origin.Type == candidate.Type;
    }
}
