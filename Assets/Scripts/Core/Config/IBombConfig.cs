namespace RecycleLife.Core
{
    /// <summary>
    /// 폭탄 설정의 읽기 전용 창구. Core는 ScriptableObject를 모른다(Hard Rule 5).
    /// 값은 전부 인스펙터에서 조절한다(Hard Rule 1).
    ///
    /// 아직 기획이 확정되지 않은 두 가지(<see cref="DamagesPlayer"/>,
    /// <see cref="ChainDetonates"/>)를 코드에 박지 않고 값으로 뺐다(Hard Rule 11).
    /// </summary>
    public interface IBombConfig
    {
        /// <summary>점화 후 몇 턴 뒤에 터지는지. 확정값 3.</summary>
        int FuseTurns { get; }

        /// <summary>폭발이 한 칸에 주는 피해. 확정값 5.</summary>
        int Damage { get; }

        /// <summary>폭심에서의 사거리. 1 = 3×3(확정값), 2 = 5×5(유물 강화 시).</summary>
        int BlastRadius { get; }

        /// <summary>런 시작 시 들고 있는 폭탄 수.</summary>
        int StartingCount { get; }

        /// <summary>
        /// 폭발이 플레이어도 때리는지. <b>기획 미확정</b> —
        /// 유물 목록에 "폭발 피해 면역"이 있다는 건 기본이 true라는 뜻으로 보여 그렇게 뒀다.
        /// </summary>
        bool DamagesPlayer { get; }

        /// <summary>
        /// 폭발 범위 안의 다른 폭탄이 즉시 연쇄 폭발하는지. <b>기획 미확정</b>.
        /// </summary>
        bool ChainDetonates { get; }
    }
}
