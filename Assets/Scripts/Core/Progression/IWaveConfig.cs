namespace RecycleLife.Core
{
    /// <summary>
    /// 웨이브 하나의 데이터. 밸런싱 v1 §2-1의 표 한 줄에 해당한다.
    ///
    /// Core는 ScriptableObject를 모르므로 인터페이스로 받는다(Hard Rule 5).
    /// 런타임 구현은 Unity 계층의 WaveConfig(SO), 테스트 구현은 FakeWave다.
    ///
    /// <b>이 웨이브에 무엇이 나오는지를 전부 여기가 정한다.</b> 전역 스폰 표를 덮어쓴다 —
    /// "웨이브마다 3종만 나온다"가 성립하려면 다른 종류의 가중치가 0이어야 하기 때문이다.
    /// </summary>
    public interface IWaveConfig
    {
        /// <summary>몇 번째 스테이지인지(1~3). 표시용이다.</summary>
        int StageNumber { get; }

        /// <summary>몇 번째 웨이브인지(1~9). 표시용이다.</summary>
        int WaveNumber { get; }

        /// <summary>
        /// 이 웨이브를 넘기는 데 필요한 처치 수. 진행도 바가 이 값을 채운다.
        /// 0 이하면 즉시 클리어된 것으로 본다(설정 실수를 무한 루프로 만들지 않기 위함).
        /// </summary>
        int KillGoal { get; }

        /// <summary>
        /// 이 웨이브에서 그 종류가 뽑힐 가중치. 0이면 이 웨이브에는 아예 안 나온다.
        /// 전역 TrashStatsConfig의 가중치 대신 이 값이 쓰인다.
        /// </summary>
        int SpawnWeightFor(TrashType type);
    }
}
