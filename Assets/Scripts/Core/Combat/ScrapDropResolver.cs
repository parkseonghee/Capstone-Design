using System;

namespace RecycleLife.Core
{
    /// <summary>
    /// 몬스터가 죽을 때 스크랩을 떨구는 역할만 맡는다(재화 문서 §1·§3-2).
    ///
    /// <see cref="MaterialDropResolver"/>와 같은 자리에 놓인다 — 적이 죽는 네 군데(평타·폭발·
    /// 전체 피해 아이템·처치 아이템)가 전부 이 클래스 하나를 거치게 해서
    /// 드롭 규칙이 바뀌어도 여기만 고치면 되게 한다(Hard Rule 4).
    ///
    /// 재료와 달리 이벤트로 흘리지 않고 <b>양을 돌려준다</b>. 스크랩은 지갑 숫자 하나라
    /// 호출자가 한 스텝 동안 모아서 <see cref="Player.AddScrap"/>에 한 번에 넣는다 —
    /// R03 배율이 한 마리씩이 아니라 합계에 먹어야 소수점 버림으로 사라지지 않는다.
    /// </summary>
    public sealed class ScrapDropResolver
    {
        private readonly IScrapDropTable _table;
        private readonly IRandomSource _random;
        private readonly WaveRunner _waves;

        /// <param name="waves">지금 몇 번째 스테이지인지 읽는다. 없으면 1스테이지로 본다.</param>
        public ScrapDropResolver(IScrapDropTable table, IRandomSource random, WaveRunner waves = null)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _waves = waves;
        }

        /// <summary>
        /// 방금 죽은 쓰레기로 스크랩을 굴린다. 적이 아니면(벽·포션) 굴리지 않는다 —
        /// 스크랩은 몹 드랍이라는 기획이기 때문이다.
        /// </summary>
        /// <returns>떨어진 스크랩 수. 안 떨어지면 0.</returns>
        public int Roll(Trash killed)
        {
            if (killed == null || !killed.IsEnemy)
            {
                return 0;
            }

            int chance = _table.ChanceFor(CurrentStage());
            if (chance <= 0)
            {
                return 0;
            }

            // 0~99를 굴려 chance 미만이면 당첨. chance가 100이면 항상 맞는다.
            return _random.NextInt(0, 100) < chance ? Math.Max(0, _table.AmountPerDrop) : 0;
        }

        private int CurrentStage()
        {
            IWaveConfig wave = _waves != null ? _waves.Current : null;
            return wave != null ? wave.StageNumber : 1;
        }
    }
}
