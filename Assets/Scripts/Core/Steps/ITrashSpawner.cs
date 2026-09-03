namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 페이즈 3(WEEK1 §2·§5).
    /// 인터페이스로 뺀 이유는 §5 주석 그대로 — "나중에 스폰 테이블로 대체"(PCG.md).
    /// </summary>
    public interface ITrashSpawner
    {
        /// <returns>이번 스텝에 실제로 스폰된 개수. 주기가 있는 구현은 0을 돌려줄 수 있다.</returns>
        int Spawn();

        /// <summary>
        /// 주기를 무시하고 한 배치를 지금 투입한다.
        /// 런 시작 시 초기 줄을 깔 때 GameLoop이 부른다.
        /// </summary>
        int SpawnImmediate();
    }
}
