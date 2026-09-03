namespace RecycleLife.Core
{
    /// <summary>WEEK1_MOVEMENT_FALLING.md §3의 세 가지 결과.</summary>
    public enum MoveOutcome
    {
        /// <summary>이동 성공. 항상 보드를 진행시킨다.</summary>
        Moved,

        /// <summary>목표 칸에 쓰레기가 있어 막힘. 보드 진행 여부는 IBoardConfig.AdvanceOnBlocked가 정한다(§7-2).</summary>
        BlockedByEntity,

        /// <summary>경계 밖 = 무효 입력. §7-2에 따라 항상 무시하며 보드를 진행시키지 않는다.</summary>
        OutOfBounds,
    }
}
