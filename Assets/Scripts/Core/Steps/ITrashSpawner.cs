namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 스폰 페이즈(WEEK1 §2·§5). 매 턴 한 번씩 불린다.
    /// 인터페이스로 뺀 이유는 §5 주석 그대로 — "나중에 스폰 테이블로 대체"(PCG.md).
    ///
    /// 런 시작에 줄을 까는 일은 여기 없다. 그건 <see cref="ISeedSpawner"/>의 몫이다.
    /// </summary>
    public interface ITrashSpawner
    {
        /// <returns>이번 스텝에 실제로 스폰된 개수. 주기가 있는 구현은 0을 돌려줄 수 있다.</returns>
        int Spawn();

        /// <summary>
        /// 직전 Spawn()이 <b>"이번 턴이 스폰 차례였는데 놓을 칸이 하나도 없어서"</b> 실패했는지.
        ///
        /// 0을 돌려주는 두 경우 — "아직 차례가 아님"과 "차례인데 자리가 없음" — 를
        /// 구분해야 §7의 게임오버("72칸이 꽉 찬 상태에서 다음 블록을 생성하려는 턴")를 판정할 수 있다.
        /// </summary>
        bool LastSpawnBlocked { get; }
    }
}
