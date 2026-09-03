namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 결과. 스텝당 1회만 만들어지고 struct라 GC를 만들지 않는다(Hard Rule 8).
    /// </summary>
    public readonly struct StepResult
    {
        public StepResult(MoveOutcome move, bool advanced, int settled, int spawned, GameOverReason gameOver)
        {
            Move = move;
            Advanced = advanced;
            Settled = settled;
            Spawned = spawned;
            GameOver = gameOver;
        }

        /// <summary>페이즈 1의 결과.</summary>
        public MoveOutcome Move { get; }

        /// <summary>페이즈 2~4가 실제로 돌았는지(= 보드가 한 스텝 진행했는지).</summary>
        public bool Advanced { get; }

        /// <summary>중력으로 자리를 옮긴 쓰레기 수.</summary>
        public int Settled { get; }

        /// <summary>이번 스텝에 새로 투입된 쓰레기 수.</summary>
        public int Spawned { get; }

        public GameOverReason GameOver { get; }

        public bool IsGameOver => GameOver != GameOverReason.None;
    }
}
