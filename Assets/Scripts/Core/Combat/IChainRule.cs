namespace RecycleLife.Core
{
    /// <summary>
    /// CORE_COMBAT.md §4. "이 두 쓰레기가 한 덩어리로 묶이는가"를 판단한다.
    ///
    /// 인터페이스로 뺀 이유는 문서에 적힌 그대로다 — 기획이 연쇄 기준을
    /// 종류에서 재질로 바꿔도 ChainFinder와 CombatMoveResolver는 손대지 않는다(Hard Rule 3).
    /// </summary>
    public interface IChainRule
    {
        bool AreConnected(Trash origin, Trash candidate);
    }
}
