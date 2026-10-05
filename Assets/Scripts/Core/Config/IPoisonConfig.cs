namespace RecycleLife.Core
{
    /// <summary>
    /// 독 기믹 설정의 읽기 전용 창구. Core는 ScriptableObject를 모른다(Hard Rule 5).
    /// 값은 전부 인스펙터에서 조절한다(Hard Rule 1).
    ///
    /// 이름이 <see cref="IBombConfig"/>와 겹치지 않게 지어져 있다 — 테스트 페이크 하나가
    /// 두 인터페이스를 같이 구현하는데, Damage 같은 이름이 겹치면 폭탄 피해와 독 피해가
    /// 같은 값으로 묶여 버린다.
    /// </summary>
    public interface IPoisonConfig
    {
        /// <summary>독 장판이 바닥에 남아 있는 턴 수. 기획 확정값 2.</summary>
        int FieldTurns { get; }

        /// <summary>독을 밟았을 때 걸리는 중독의 지속 턴 수. 기획 확정값 1.</summary>
        int StatusTurns { get; }

        /// <summary>중독 상태로 한 칸 움직일 때마다 받는 피해. 기획 확정값 1.</summary>
        int StepDamage { get; }
    }
}
