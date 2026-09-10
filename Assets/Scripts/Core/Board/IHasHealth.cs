namespace RecycleLife.Core
{
    /// <summary>
    /// 체력을 가진 엔티티의 <b>읽기 전용</b> 창구.
    ///
    /// 렌더 계층이 "이건 Player인가 Trash인가"를 타입으로 갈라 보지 않게 하려고 뺐다(Hard Rule 3).
    /// 뷰는 이 인터페이스만 보고 하트를 그리므로, 나중에 체력을 가진 엔티티가 늘어나도
    /// BoardView는 손대지 않는다.
    ///
    /// 값을 바꾸는 TakeDamage는 여기 두지 않는다 — 피해를 주는 건 전투 계층의 일이고,
    /// 뷰에까지 그 권한을 열어 줄 이유가 없다.
    /// </summary>
    public interface IHasHealth
    {
        int Hp { get; }

        int MaxHp { get; }
    }
}
