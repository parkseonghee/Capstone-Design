namespace RecycleLife.Core
{
    /// <summary>WEEK1_MOVEMENT_FALLING.md §6.</summary>
    public enum GameOverReason
    {
        None,

        /// <summary>보드에 빈 칸이 0개 — 오버플로 패배.</summary>
        BoardFull,

        /// <summary>플레이어의 4방향이 모두 경계 밖이거나 막힘.</summary>
        PlayerTrapped,
    }
}
