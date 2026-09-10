namespace RecycleLife.Core
{
    /// <summary>WEEK1_MOVEMENT_FALLING.md §3 + CORE_COMBAT.md §3의 결과 종류.</summary>
    public enum MoveOutcome
    {
        /// <summary>이동 성공. 항상 보드를 진행시킨다.</summary>
        Moved,

        /// <summary>목표 칸에 쓰레기가 있어 막힘. 보드 진행 여부는 IBoardConfig.AdvanceOnBlocked가 정한다(§7-2).</summary>
        BlockedByEntity,

        /// <summary>경계 밖 = 무효 입력. §7-2에 따라 항상 무시하며 보드를 진행시키지 않는다.</summary>
        OutOfBounds,

        /// <summary>
        /// 목표 칸의 쓰레기를 공격했다. 플레이어는 <b>이동하지 않는다</b>(CORE_COMBAT.md §3).
        /// 유효한 행동이므로 이동과 똑같이 보드를 한 스텝 진행시킨다.
        /// </summary>
        Attacked,

        /// <summary>
        /// 목표 칸의 아이템을 먹었다. 연결된 같은 아이템까지 한 번에 사라진다.
        /// 공격과 마찬가지로 플레이어는 <b>이동하지 않는다</b>(기획 확정).
        /// </summary>
        Consumed,
    }
}
