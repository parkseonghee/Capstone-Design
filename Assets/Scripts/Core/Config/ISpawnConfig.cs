namespace RecycleLife.Core
{
    /// <summary>
    /// WEEK1 §5의 스폰 캐이던스. 보드 규격(IBoardConfig)과 일부러 분리해 뒀다 —
    /// 밸런스를 만지는 사람과 보드를 만지는 사람이 서로 다른 에셋을 건드리게 하기 위함이다
    /// (Hard Rule 4: 설정 God object 금지).
    ///
    /// 확정된 규칙:
    ///   누적 스폰 &lt; BlocksBeforeSlowdown  → TurnsPerSpawnEarly턴마다 1개 (= 매 턴)
    ///   누적 스폰 &gt;= BlocksBeforeSlowdown → TurnsPerSpawnLate턴마다 1개 (= 2턴당 1개, 이후 영구)
    ///
    /// 누적 개수에는 시작 3줄이 포함되지 않는다(기획 확인 완료).
    /// </summary>
    public interface ISpawnConfig
    {
        /// <summary>이 개수를 넘기면 캐이던스가 느려진다. 확정값 15.</summary>
        int BlocksBeforeSlowdown { get; }

        /// <summary>느려지기 전 주기(턴). 확정값 1 = 매 턴 스폰.</summary>
        int TurnsPerSpawnEarly { get; }

        /// <summary>느려진 뒤 주기(턴). 확정값 2 = 2턴당 1회 스폰.</summary>
        int TurnsPerSpawnLate { get; }

        /// <summary>한 번 스폰할 때 투입되는 쓰레기 수. 확정값 1.</summary>
        int BlocksPerSpawn { get; }
    }
}
