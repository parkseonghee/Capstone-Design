namespace RecycleLife.Core
{
    /// <summary>
    /// WEEK1에서는 "부딪히면 막히는 낙하 블록". 필드를 최소화해 두었고,
    /// CORE_COMBAT.md 단계에서 material/hp/attack이 붙어 Enemy로 확장된다.
    /// </summary>
    public sealed class Trash : Entity
    {
        public Trash(TrashType type)
        {
            Type = type;
        }

        public TrashType Type { get; }

        public override EntityKind Kind => EntityKind.Trash;
    }
}
