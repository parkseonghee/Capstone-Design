namespace RecycleLife.Core
{
    /// <summary>
    /// 런 시작 상태(WEEK1 §5 "하단 3줄이 채워진 상태로 시작")를 만드는 쪽.
    /// 주기가 없다 — 부르면 그 자리에서 한 줄을 얹고, 낙하는 GameLoop이 중력으로 시킨다.
    ///
    /// 진행 중 스폰(<see cref="ITrashSpawner"/>)과 나눠 둔 이유는 규칙이 아예 다르기 때문이다.
    /// 이쪽은 줄 단위·무조건, 저쪽은 칸 단위·캐이던스·컬럼 잠금.
    /// </summary>
    public interface ISeedSpawner
    {
        /// <summary>프리뷰 줄에 한 줄을 얹는다.</summary>
        /// <returns>실제로 놓인 쓰레기 수.</returns>
        int SpawnRow();
    }
}
