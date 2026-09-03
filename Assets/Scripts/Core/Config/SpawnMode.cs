namespace RecycleLife.Core
{
    /// <summary>
    /// 쓰레기가 보드에 들어오는 방식. ITrashSpawner 구현을 고르는 스위치이며,
    /// 실제 규칙은 각 구현이 갖는다(Hard Rule 3).
    /// </summary>
    public enum SpawnMode
    {
        /// <summary>상단 빈 칸 중 N개를 랜덤으로 채운다. WEEK1 §5의 최초안.</summary>
        SingleBlock,

        /// <summary>가로 한 줄이 통째로 내려온다. 원작(Shovel Knight Pocket Dungeon) 방식.</summary>
        FullRow,
    }
}
