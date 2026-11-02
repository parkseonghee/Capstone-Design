namespace RecycleLife.Core
{
    /// <summary>
    /// "무슨 몬스터가 무슨 재료를 떨구는가, 그리고 몇 % 로 떨구는가" 표.
    /// 확률은 몹이 아니라 <b>스테이지 안 몇 번째 웨이브인지</b>로 정해진다(재화 문서 §3-1: 3% · 4% · 5%).
    ///
    /// 표 자체는 데이터라 Unity 쪽 에셋이 들고 있고, Core는 이 인터페이스로만 묻는다
    /// (Hard Rule 1·5). 테스트는 정해진 답을 주는 페이크를 꽂으면 된다.
    /// </summary>
    public interface IMaterialDropTable
    {
        /// <summary>
        /// 이 종류가 죽었을 때 떨어질 재료를 굴린다.
        /// </summary>
        /// <param name="type">죽은 몬스터의 종류. 어떤 재료가 떨어질지를 정한다.</param>
        /// <param name="waveInStage">스테이지 안에서 몇 번째 웨이브인지(1부터). 확률을 정한다.</param>
        /// <param name="random">확률을 굴릴 난수원. 결정론을 위해 밖에서 받는다.</param>
        /// <param name="materialId">떨어진 재료의 아이템 id. 안 떨어지면 null.</param>
        /// <returns>떨어졌으면 true.</returns>
        /// <param name="bonusChance">표의 확률에 더할 %p(유물 R10 넝마주이). 0이면 표 그대로.</param>
        bool TryRoll(TrashType type, int waveInStage, IRandomSource random, int bonusChance, out string materialId);

        /// <summary>이 수만큼 처치할 때마다 재료 하나를 보장한다. 0이면 보장 없음.</summary>
        int GuaranteeEvery { get; }

        /// <summary>보장 드랍으로 줄 재료를 뽑는다(기초 재료 중 무작위).</summary>
        bool TryPickGuaranteed(IRandomSource random, out string materialId);
    }
}
