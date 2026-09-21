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
    }
}
