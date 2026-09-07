namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 결과. 스텝당 1회만 만들어지고 struct라 GC를 만들지 않는다(Hard Rule 8).
    /// </summary>
    public readonly struct StepResult
    {
        public StepResult(
            MoveOutcome move,
            bool advanced,
            int settled,
            int spawned,
            bool spawnBlocked,
            GameOverReason gameOver)
        {
            Move = move;
            Advanced = advanced;
            Settled = settled;
            Spawned = spawned;
            SpawnBlocked = spawnBlocked;
            GameOver = gameOver;
        }

        /// <summary>페이즈 1의 결과.</summary>
        public MoveOutcome Move { get; }

        /// <summary>페이즈 2~4가 실제로 돌았는지(= 보드가 한 스텝 진행했는지).</summary>
        public bool Advanced { get; }

        /// <summary>중력으로 한 칸 내려온 쓰레기 수. 낙하는 스텝당 한 칸이다(기획 확정).</summary>
        public int Settled { get; }

        /// <summary>이번 스텝에 프리뷰 줄로 새로 투입된 쓰레기 수.</summary>
        public int Spawned { get; }

        /// <summary>
        /// 스폰 차례였는데 놓을 칸이 하나도 없었는지.
        /// 보드가 꽉 찬 상태에서 이 값이 서면 패배다(§7).
        /// </summary>
        public bool SpawnBlocked { get; }

        public GameOverReason GameOver { get; }

        public bool IsGameOver => GameOver != GameOverReason.None;
    }
}
