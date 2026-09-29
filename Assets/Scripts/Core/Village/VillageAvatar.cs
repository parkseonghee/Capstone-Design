namespace RecycleLife.Core
{
    /// <summary>
    /// 마을 화면에서 플레이어가 조작하는 아바타. 본편 Player와 달리 체력·공격·골드가 없다 —
    /// 마을은 걸어 다니다 캐릭터 상에 부딪히는 것 말고는 아무 규칙도 없기 때문이다(Hard Rule 4).
    /// </summary>
    public sealed class VillageAvatar : Entity
    {
        public override EntityKind Kind => EntityKind.VillageAvatar;
    }
}
