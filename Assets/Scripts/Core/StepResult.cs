namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 결과. 스텝당 1회만 만들어지고 struct라 GC를 만들지 않는다(Hard Rule 8).
    /// 전투 수치는 페이즈 1의 MoveResult를 그대로 감싸 흘려보낸다.
    /// </summary>
    public readonly struct StepResult
    {
        private readonly MoveResult _move;
        private readonly BlastResult _blast;

        public StepResult(
            MoveResult move,
            bool advanced,
            int settled,
            int spawned,
            bool spawnBlocked,
            GameOverReason gameOver)
            : this(move, BlastResult.None, advanced, settled, spawned, spawnBlocked, gameOver)
        {
        }

        public StepResult(
            MoveResult move,
            BlastResult blast,
            bool advanced,
            int settled,
            int spawned,
            bool spawnBlocked,
            GameOverReason gameOver)
        {
            _move = move;
            _blast = blast;
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

        /// <summary>
        /// 이번 스텝에 처치한 <b>적</b> 수. 공격과 폭발을 모두 합친 값이다.
        /// 웨이브 진행도가 이 값으로 찬다(밸런싱 v1 §2-1-4).
        /// </summary>
        public int EnemiesKilled => _move.EnemiesKilled + _blast.EnemiesKilled;

        /// <summary>이번 스텝에 부순 <b>벽</b> 수. 공격과 폭발을 합친 값이다.</summary>
        public int WallsDestroyed => _move.WallsDestroyed + _blast.WallsDestroyed;

        /// <summary>이번 스텝에 번 골드. 공격과 폭발을 합친 값이다.</summary>
        public int Gold => _move.Gold + _blast.Gold;

        /// <summary>반격으로 플레이어가 받은 피해량.</summary>
        public int DamageTaken => _move.DamageTaken;

        /// <summary>아이템으로 실제로 회복한 체력.</summary>
        public int Healed => _move.Healed;

        /// <summary>이번 스텝에 터진 폭탄 수(연쇄 포함).</summary>
        public int BombsExploded => _blast.Exploded;

        /// <summary>폭발로 사라진 블록 수.</summary>
        public int BlastDestroyed => _blast.Destroyed;

        /// <summary>폭발로 플레이어가 받은 피해.</summary>
        public int BlastDamage => _blast.PlayerDamage;

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
