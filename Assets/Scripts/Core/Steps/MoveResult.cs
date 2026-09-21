namespace RecycleLife.Core
{
    /// <summary>
    /// 페이즈 1(이동/공격)의 결과. CORE_COMBAT.md §7의 CombatResult에 해당한다.
    ///
    /// 이동만 하던 시절에는 MoveOutcome 하나로 충분했지만, 공격이 붙으면서
    /// "몇 마리를 묶어 때렸고 몇 마리가 죽었고 얼마를 맞았는지"를 같이 돌려줘야 한다.
    /// struct라 스텝당 할당이 없다(Hard Rule 8).
    ///
    /// 플레이어 사망 여부는 여기 담지 않는다 — 패배 판정은 GameOverChecker 한 곳에서만 한다.
    /// </summary>
    public readonly struct MoveResult
    {
        public MoveResult(MoveOutcome outcome, int chainSize, int killed, int damageTaken, int healed)
            : this(outcome, chainSize, killed, killed, 0, damageTaken, healed, 0)
        {
        }

        public MoveResult(
            MoveOutcome outcome,
            int chainSize,
            int killed,
            int enemiesKilled,
            int wallsDestroyed,
            int damageTaken,
            int healed,
            int gold)
        {
            Gold = gold;
            Outcome = outcome;
            ChainSize = chainSize;
            Killed = killed;
            EnemiesKilled = enemiesKilled;
            WallsDestroyed = wallsDestroyed;
            DamageTaken = damageTaken;
            Healed = healed;
        }

        public MoveOutcome Outcome { get; }

        /// <summary>
        /// 이번 행동에 함께 묶인 블록 수(부딪힌 것 포함).
        /// 공격이면 같이 맞은 수, 아이템이면 같이 먹은 수. 이동·무효 입력이면 0.
        /// </summary>
        public int ChainSize { get; }

        /// <summary>이번 공격으로 사라진 쓰레기 수(적 + 벽).</summary>
        public int Killed { get; }

        /// <summary>그중 <b>적</b>만 센 수. 웨이브 진행도가 이 값으로 찬다.</summary>
        public int EnemiesKilled { get; }

        /// <summary>그중 <b>벽</b>만 센 수. 진행도에 넣을지는 설정으로 정한다.</summary>
        public int WallsDestroyed { get; }

        /// <summary>이번 공격으로 떨어진 골드 합계.</summary>
        public int Gold { get; }

        /// <summary>반격으로 플레이어가 받은 피해량.</summary>
        public int DamageTaken { get; }

        /// <summary>
        /// 아이템으로 <b>실제로</b> 회복한 체력. 최대 체력에서 잘린 뒤의 값이라,
        /// 가득 찬 상태로 먹으면 0이다.
        /// </summary>
        public int Healed { get; }

        /// <summary>전투가 없는 결과(이동·막힘·무효 입력)를 만든다.</summary>
        public static MoveResult Simple(MoveOutcome outcome)
            => new MoveResult(outcome, 0, 0, 0, 0, 0, 0, 0);
    }
}
