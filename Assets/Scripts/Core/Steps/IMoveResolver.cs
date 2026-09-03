namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 페이즈 1(WEEK1 §2).
    /// 인터페이스로 뺀 이유는 §3 주석 그대로다 —
    /// 다음 단계에서 CombatSystem.ResolveMove를 감싸는 구현으로 통째로 교체된다(CORE_COMBAT.md §6).
    /// 그때 GameLoop은 손대지 않는다.
    /// </summary>
    public interface IMoveResolver
    {
        MoveOutcome Resolve(Direction direction);
    }
}
