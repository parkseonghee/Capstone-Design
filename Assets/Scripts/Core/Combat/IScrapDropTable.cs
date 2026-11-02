namespace RecycleLife.Core
{
    /// <summary>
    /// "어느 스테이지에서 적 한 마리가 스크랩을 몇 % 로 떨구는가" 표(재화 문서 §3-2).
    ///
    /// 스크랩은 몹 종류가 아니라 <b>스테이지</b>마다 확률이 다르다 — 어느 스테이지든 600마리로
    /// 고정이라, 확률이 같으면 가장 쉬운 공원만 반복하는 게 최적이 돼 버리기 때문이다.
    ///
    /// 표 자체는 데이터라 Unity 쪽 에셋이 들고 있고, Core는 이 인터페이스로만 묻는다
    /// (Hard Rule 1·5). 테스트는 정해진 답을 주는 페이크를 꽂으면 된다.
    /// </summary>
    public interface IScrapDropTable
    {
        /// <summary>그 스테이지(IWaveConfig.StageNumber, 1부터)에서 적을 잡았을 때 스크랩이 떨어질 확률(%).</summary>
        int ChanceFor(int stageNumber);

        /// <summary>한 번 떨어질 때 들어오는 스크랩 수.</summary>
        int AmountPerDrop { get; }
    }
}
