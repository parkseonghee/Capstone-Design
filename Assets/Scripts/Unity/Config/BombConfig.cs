using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 폭탄 설정. 기획서(폭탄·아이템·기믹 v1) §1.
    ///
    /// 블록 스탯(TrashStatsConfig)과 따로 둔 이유는 폭탄이 종류 표의 한 줄이 아니라
    /// <b>플레이어가 쓰는 도구</b>이기 때문이다. 유물 "폭발 범위 증가"·"폭탄 +5개"도
    /// 이 값들을 건드리게 된다.
    ///
    /// 아래 두 체크박스는 <b>기획 미확정</b> 항목이라 코드에 박지 않고 값으로 뺐다(Hard Rule 11).
    /// </summary>
    [CreateAssetMenu(fileName = "BombConfig", menuName = "RecycleLife/Bomb Config", order = 4)]
    public sealed class BombConfig : ScriptableObject, IBombConfig
    {
        [Header("폭발 (기획 확정값)")]
        [SerializeField, Min(1), Tooltip("불을 붙인 뒤 몇 턴 있다가 터지는지. 확정값 3.")]
        private int fuseTurns = 3;

        [SerializeField, Min(0), Tooltip("폭발이 한 칸에 주는 피해. 확정값 5.")]
        private int damage = 5;

        [SerializeField, Min(0), Tooltip("폭심에서의 사거리. 1 = 3x3(확정값), 2 = 5x5. " +
                                         "유물 '폭발 범위 증가'가 이 값을 올린다.")]
        private int blastRadius = 1;

        [Header("보유")]
        [SerializeField, Min(0), Tooltip("런 시작 시 들고 있는 폭탄 수. 수급 경로가 정해질 때까지의 임시값.")]
        private int startingCount = 3;

        [Header("미확정 — 기획 확인 필요")]
        [SerializeField, Tooltip("폭발이 플레이어도 때리는지. 유물 목록에 '폭발 피해 면역'이 있다는 건 " +
                                 "기본이 켜짐이라는 뜻으로 보여 그렇게 뒀다. 체력이 3~4라 맞으면 거의 즉사다.")]
        private bool damagesPlayer = true;

        [SerializeField, Tooltip("폭발 범위 안의 다른 폭탄이 즉시 같이 터지는지.")]
        private bool chainDetonates = true;

        public int FuseTurns => fuseTurns;

        public int Damage => damage;

        public int BlastRadius => blastRadius;

        public int StartingCount => startingCount;

        public bool DamagesPlayer => damagesPlayer;

        public bool ChainDetonates => chainDetonates;
    }
}
