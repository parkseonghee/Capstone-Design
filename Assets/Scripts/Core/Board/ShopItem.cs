namespace RecycleLife.Core
{
    /// <summary>
    /// 상점 방 바닥에 놓인 상품. 플레이어가 <b>부딪히면 산다</b>(삽질기사 포켓던전 상점과 같은 방식).
    ///
    /// 버튼 목록 대신 보드에 놓은 이유는 조작이 하나로 통일되기 때문이다 —
    /// 상점에서도 평소처럼 방향키로 움직이고 부딪히면 된다.
    ///
    /// <b>무엇을 주는 아이템인지 Core는 모른다.</b> <see cref="Id"/>는 그냥 꼬리표이고,
    /// 효과를 먹이는 건 Unity 계층(ItemService)이다 — Core가 유물 목록을 알 이유가 없다(Hard Rule 5).
    ///
    /// 중력을 받지 않는다. 상점 방에는 중력·스폰이 돌지 않지만, 혹시 일반 보드에 놓이더라도
    /// 흘러내리지 않도록 <see cref="IGravityHold"/>를 항상 붙잡는 쪽으로 구현했다.
    /// </summary>
    public sealed class ShopItem : Entity, IGravityHold
    {
        public ShopItem(string id, int price)
        {
            Id = id;
            Price = price < 0 ? 0 : price;
        }

        /// <summary>아이템 표의 id(R01, C01 …). Unity 계층이 이걸로 효과를 찾는다.</summary>
        public string Id { get; }

        /// <summary>가격(골드).</summary>
        public int Price { get; }

        public override EntityKind Kind => EntityKind.ShopItem;

        /// <summary>상품은 제자리에 놓여 있어야 한다. 절대 떨어지지 않는다.</summary>
        public bool HoldsPosition => true;

        /// <summary>붙잡은 상태를 풀지 않는다 — 상품은 늘 제자리다.</summary>
        public void ReleaseHold()
        {
        }
    }
}
