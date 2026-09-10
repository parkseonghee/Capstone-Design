namespace RecycleLife.Core
{
    /// <summary>WEEK1_MOVEMENT_FALLING.md §7 + CORE_COMBAT.md §5-6.</summary>
    public enum GameOverReason
    {
        None,

        /// <summary>보드에 빈 칸이 0개 — 오버플로 패배.</summary>
        BoardFull,

        /// <summary>
        /// 플레이어의 4방향이 모두 경계 밖이거나 손댈 수 없는 칸.
        /// 전투가 붙은 뒤에는 쓰레기를 때려 치울 수 있어 사실상 발생하지 않는다
        /// (CombatMoveResolver.CanAct 주석 참조).
        /// </summary>
        PlayerTrapped,

        /// <summary>반격 누적으로 플레이어 HP가 0이 됐다.</summary>
        PlayerDead,
    }
}
