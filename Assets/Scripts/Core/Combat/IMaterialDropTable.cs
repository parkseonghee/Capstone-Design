namespace RecycleLife.Core
{
    /// <summary>
    /// "무슨 몬스터가 무슨 재료를 몇 % 로 떨구는가" 표.
    ///
    /// 표 자체는 데이터라 Unity 쪽 에셋이 들고 있고, Core는 이 인터페이스로만 묻는다
    /// (Hard Rule 1·5). 테스트는 정해진 답을 주는 페이크를 꽂으면 된다.
    /// </summary>
    public interface IMaterialDropTable
    {
        /// <summary>
        /// 이 종류가 죽었을 때 떨어질 재료를 굴린다.
        /// </summary>
        /// <param name="type">죽은 몬스터의 종류.</param>
        /// <param name="random">확률을 굴릴 난수원. 결정론을 위해 밖에서 받는다.</param>
        /// <param name="materialId">떨어진 재료의 아이템 id. 안 떨어지면 null.</param>
        /// <returns>떨어졌으면 true.</returns>
        bool TryRoll(TrashType type, IRandomSource random, out string materialId);
    }
}
