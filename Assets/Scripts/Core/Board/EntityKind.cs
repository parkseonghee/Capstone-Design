namespace RecycleLife.Core
{
    /// <summary>보드 칸을 차지할 수 있는 것들의 종류.</summary>
    public enum EntityKind
    {
        Player,

        /// <summary>적·아이템·벽. 무엇인지는 TrashStatsConfig의 값이 정한다.</summary>
        Trash,

        /// <summary>
        /// 플레이어가 설치하는 폭탄. 체력이 없고 도화선 상태를 갖는다는 점이
        /// Trash와 근본적으로 달라서 별도 종류로 뒀다.
        /// </summary>
        Bomb,

        /// <summary>
        /// 상점 방 바닥에 놓인 상품. 부딪히면 산다.
        /// 체력도 공격력도 없고 가격과 꼬리표만 있어서 Trash와 섞을 수 없다.
        /// </summary>
        ShopItem,

        /// <summary>마을 화면에서 플레이어가 조작하는 아바타. 본편 Player와 달리 체력·공격이 없다.</summary>
        VillageAvatar,

        /// <summary>
        /// 마을에 세워진 캐릭터 상(像). 부딪히면(공격하면) 그 캐릭터로 즉시 바뀐다 — 확인창 없음.
        /// </summary>
        CharacterStand,

        /// <summary>
        /// 덫 몬스터가 죽은 자리에 남는 덫. 체력이 없어 공격 대상이 아니며,
        /// 플레이어나 몬스터가 닿으면 무작위 빈 칸으로 보낸다(TrapResolver).
        /// </summary>
        Trap,
    }
}
