using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 독 몬스터 기믹의 수치. 기획 확정 다섯 줄 중 숫자인 것 셋을 담는다.
    ///
    /// 블록 표(TrashStatsConfig)와 따로 둔 이유는 폭탄과 같다 — <b>어느 몬스터가</b> 독을
    /// 남기는지는 블록 표의 체크박스(leavesPoison)지만, <b>독이 어떻게 작동하는지</b>는
    /// 몬스터 종류와 무관한 기믹 자체의 값이기 때문이다(Hard Rule 4).
    /// </summary>
    [CreateAssetMenu(fileName = "PoisonConfig", menuName = "RecycleLife/Poison Config", order = 5)]
    public sealed class PoisonConfig : ScriptableObject, IPoisonConfig
    {
        [Header("독 장판 (기획 확정값)")]
        [SerializeField, Min(1), Tooltip("독이 바닥에 남아 있는 턴 수. 확정값 2.")]
        private int fieldTurns = 2;

        [Header("중독")]
        [SerializeField, Min(1), Tooltip("독을 밟았을 때 걸리는 중독의 지속 턴 수. 확정값 1. " +
                                         "중독인 동안에는 한 칸 움직일 때마다 아래 피해를 받는다.")]
        private int statusTurns = 1;

        [SerializeField, Min(0), Tooltip("중독 상태로 한 칸 움직일 때마다 받는 피해. 확정값 1. " +
                                         "한 번의 이동에 한 번만 들어간다 — 중독인 채로 또 다른 독 칸에 " +
                                         "들어서도 1회분이다.")]
        private int stepDamage = 1;

        public int FieldTurns => fieldTurns;

        public int StatusTurns => statusTurns;

        public int StepDamage => stepDamage;
    }
}
