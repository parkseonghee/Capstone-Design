namespace RecycleLife.Core
{
    /// <summary>
    /// WEEK1 범위에서는 좌표만 가진다.
    /// CORE_COMBAT.md §1에서 hp/maxHp/attack이 추가된다.
    /// </summary>
    public sealed class Player : Entity
    {
        public override EntityKind Kind => EntityKind.Player;
    }
}
