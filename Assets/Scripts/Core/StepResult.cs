namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 결과. 스텝당 1회만 만들어지고 struct라 GC를 만들지 않는다(Hard Rule 8).
    /// 전투 수치는 페이즈 1의 MoveResult를 그대로 감싸 흘려보낸다.
    /// </summary>
    public readonly struct StepResult
    {
        private readonly MoveResult _move;

        public StepResult(
            MoveResult move,
            bool advanced,
            int settled,
            int spawned,
            bool spawnBlocked,
            GameOverReason gameOver)
        {
            _move = move;
            Advanced = advanced;
            Settled = settled;
            Spawned = spawned;
            SpawnBlocked = spawnBlocked;
            GameOver = gameOver;
        }

        /// <summary>페이즈 1의 결과 종류.</summary>
        public MoveOutcome Move => _move.Outcome;

        /// <summary>이번 공격에 함께 맞은 쓰레기 수. 공격이 아니면 0.</summary>
        public int ChainSize => _move.ChainSize;

        /// <summary>이번 공격으로 사라진 쓰레기 수.</summary>
        public int Killed => _move.Killed;

        /// <summary>반격으로 플레이어가 받은 피해량.</summary>
        public int DamageTaken => _move.DamageTaken;

        /// <summary>아이템으로 실제로 회복한 체력.</summary>
        public int Healed => _move.Healed;

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
