namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝에서 일어난 폭발의 결과. struct라 스텝당 할당이 없다(Hard Rule 8).
    /// </summary>
    public readonly struct BlastResult
    {
        public BlastResult(int exploded, int destroyed, int playerDamage)
            : this(exploded, destroyed, destroyed, 0, playerDamage, 0)
        {
        }

        public BlastResult(
            int exploded, int destroyed, int enemiesKilled, int wallsDestroyed, int playerDamage, int gold)
        {
            Gold = gold;
            Exploded = exploded;
            Destroyed = destroyed;
            EnemiesKilled = enemiesKilled;
            WallsDestroyed = wallsDestroyed;
            PlayerDamage = playerDamage;
        }

        /// <summary>이번 스텝에 터진 폭탄 수(연쇄로 딸려 터진 것 포함).</summary>
        public int Exploded { get; }

        /// <summary>폭발로 사라진 블록 수(적 + 벽).</summary>
        public int Destroyed { get; }

        /// <summary>그중 <b>적</b>만 센 수. 웨이브 진행도가 이 값으로 찬다.</summary>
        public int EnemiesKilled { get; }

        /// <summary>그중 <b>벽</b>만 센 수.</summary>
        public int WallsDestroyed { get; }

        /// <summary>폭발로 떨어진 골드 합계. 폭탄으로 죽여도 돈은 나온다(폭탄 기획 §1-1).</summary>
        public int Gold { get; }

        /// <summary>폭발로 플레이어가 받은 피해.</summary>
        public int PlayerDamage { get; }

        public bool Happened => Exploded > 0;

        public static BlastResult None => new BlastResult(0, 0, 0, 0, 0, 0);
    }
}
