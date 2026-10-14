using DG.Tweening;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// MonsterVisualFeedback이 재생하는 모든 연출 수치. 쓰레기 종류별로 에셋을 따로 만들면
    /// 몬스터마다 다른 느낌(숨쉬는 속도, 밀려나는 거리 등)을 줄 수 있다(Hard Rule 1).
    ///
    /// EntityVisualSet.TrashEntry.feedback에 종류별로 꽂는다. 비워 두면 BoardView의
    /// 기본값(defaultMonsterFeedback)이 대신 쓰인다.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterFeedbackSettings", menuName = "RecycleLife/Monster Feedback Settings", order = 4)]
    public sealed class MonsterFeedbackSettings : ScriptableObject
    {
        public enum IdleMode
        {
            /// <summary>제자리에서 위아래로 숨쉬듯 스쿼시&스트레치. 일반 몬스터·벽용.</summary>
            Breathe,

            /// <summary>위아래로 살짝 떠다님. 아이템(물약)용.</summary>
            Float,
        }

        [Header("1. Idle (상시)")]
        [SerializeField] private IdleMode idleMode = IdleMode.Breathe;

        [SerializeField, Tooltip("숨쉬기 1주기 시간(초).")]
        private float breathePeriod = 0.8f;

        [SerializeField, Tooltip("숨쉴 때 최대 scaleY.")]
        private float breatheScaleYMax = 1.05f;

        [SerializeField, Tooltip("숨쉴 때 최소 scaleX(스쿼시).")]
        private float breatheScaleXMin = 0.97f;

        [SerializeField] private Ease breatheEase = Ease.InOutSine;

        [SerializeField, Tooltip("떠다니는 1주기 시간(초). IdleMode.Float용.")]
        private float floatPeriod = 1f;

        [SerializeField, Tooltip("떠다니는 높이(유닛). IdleMode.Float용.")]
        private float floatDistance = 0.05f;

        [SerializeField] private Ease floatEase = Ease.InOutSine;

        [SerializeField, Range(0f, 1f), Tooltip("몬스터마다 시작 위상을 이 비율만큼 무작위로 어긋나게 한다. " +
                                                "0이면 전부 같은 박자로 숨쉰다.")]
        private float idlePhaseJitter = 1f;

        [SerializeField, Range(0f, 0.5f), Tooltip("몬스터마다 주기 자체도 이 비율만큼 무작위로 다르게 한다.")]
        private float idlePeriodJitter = 0.15f;

        [Header("2. 스폰 / 착지")]
        [SerializeField, Min(0f), Tooltip("스폰 시 scale 0 -> 1 팝업 시간(초).")]
        private float spawnPopDuration = 0.15f;

        [SerializeField] private Ease spawnPopEase = Ease.OutBack;

        [SerializeField, Min(0.01f), Tooltip("착지 순간 늘어나는 scaleX(가로로 퍼짐).")]
        private float landSquashScaleX = 1.2f;

        [SerializeField, Min(0.01f), Tooltip("착지 순간 줄어드는 scaleY(눌림).")]
        private float landSquashScaleY = 0.8f;

        [SerializeField, Min(0f), Tooltip("착지 스쿼시 -> 원래 크기로 돌아오는 시간(초).")]
        private float landRecoverDuration = 0.15f;

        [SerializeField] private Ease landRecoverEase = Ease.OutBack;

        [Header("3. 피격")]
        [SerializeField, Min(0f)] private float hitFlashDuration = 0.08f;
        [SerializeField] private Color hitFlashColor = Color.white;

        [SerializeField, Min(0f), Tooltip("맞은 반대쪽(공격이 날아온 방향 그대로, 넉백)으로 밀려나는 거리(유닛).")]
        private float hitPushDistance = 0.1f;

        [SerializeField, Min(0f)] private float hitPushOutDuration = 0.04f;

        [SerializeField, Min(0f), Tooltip("밀려난 자리에서 원위치로 튕겨 돌아오는 시간(초). " +
                                          "Elastic 이징이 자체적으로 '작은 흔들림'을 만든다.")]
        private float hitReturnDuration = 0.12f;

        [SerializeField, Min(0f), Tooltip("복귀할 때의 흔들림 폭(Elastic amplitude).")]
        private float hitShakeAmplitude = 1f;

        [SerializeField, Min(0.01f), Tooltip("복귀할 때의 흔들림 주기(Elastic period).")]
        private float hitShakePeriod = 0.3f;

        [Header("4. 공격 (반격 범프)")]
        [SerializeField, Tooltip("원본 스프라이트가 왼쪽을 보고 있는지. 켜져 있으면 플레이어가 " +
                                 "오른쪽에 있을 때만 flipX를 켠다.")]
        private bool artFacesLeft = true;

        [SerializeField, Min(0f), Tooltip("플레이어 쪽으로 돌진하는 거리(유닛).")]
        private float attackLungeDistance = 0.25f;

        [SerializeField, Min(0f)] private float attackOutDuration = 0.06f;
        [SerializeField, Min(0f)] private float attackBackDuration = 0.12f;
        [SerializeField] private Ease attackOutEase = Ease.OutQuad;
        [SerializeField] private Ease attackBackEase = Ease.OutQuad;

        [Header("5. 사망")]
        [SerializeField, Min(0f)] private float deathFlashDuration = 0.08f;
        [SerializeField, Min(0.01f)] private float deathPopScale = 1.2f;

        [SerializeField, Min(0f), Tooltip("플래시 이후 팝 -> 소멸까지 걸리는 시간(초). 플래시 시간은 별도다.")]
        private float deathPopDuration = 0.12f;

        [SerializeField] private Ease deathPopEase = Ease.InBack;

        [Header("7. 획득 (아이템)")]
        [SerializeField, Min(0.01f)] private float pickupPopScale = 1.3f;
        [SerializeField, Min(0f)] private float pickupDuration = 0.2f;
        [SerializeField, Min(0f), Tooltip("사라지면서 위로 떠오르는 거리(유닛).")]
        private float pickupRiseDistance = 0.15f;

        [SerializeField] private Ease pickupEase = Ease.OutQuad;

        public IdleMode Idle => idleMode;
        public float BreathePeriod => breathePeriod;
        public float BreatheScaleYMax => breatheScaleYMax;
        public float BreatheScaleXMin => breatheScaleXMin;
        public Ease BreatheEase => breatheEase;
        public float FloatPeriod => floatPeriod;
        public float FloatDistance => floatDistance;
        public Ease FloatEase => floatEase;
        public float IdlePhaseJitter => idlePhaseJitter;
        public float IdlePeriodJitter => idlePeriodJitter;

        public float SpawnPopDuration => spawnPopDuration;
        public Ease SpawnPopEase => spawnPopEase;
        public float LandSquashScaleX => landSquashScaleX;
        public float LandSquashScaleY => landSquashScaleY;
        public float LandRecoverDuration => landRecoverDuration;
        public Ease LandRecoverEase => landRecoverEase;

        public float HitFlashDuration => hitFlashDuration;
        public Color HitFlashColor => hitFlashColor;
        public float HitPushDistance => hitPushDistance;
        public float HitPushOutDuration => hitPushOutDuration;
        public float HitReturnDuration => hitReturnDuration;
        public float HitShakeAmplitude => hitShakeAmplitude;
        public float HitShakePeriod => hitShakePeriod;

        public bool ArtFacesLeft => artFacesLeft;
        public float AttackLungeDistance => attackLungeDistance;
        public float AttackOutDuration => attackOutDuration;
        public float AttackBackDuration => attackBackDuration;
        public Ease AttackOutEase => attackOutEase;
        public Ease AttackBackEase => attackBackEase;

        public float DeathFlashDuration => deathFlashDuration;
        public float DeathPopScale => deathPopScale;
        public float DeathPopDuration => deathPopDuration;
        public Ease DeathPopEase => deathPopEase;

        public float PickupPopScale => pickupPopScale;
        public float PickupDuration => pickupDuration;
        public float PickupRiseDistance => pickupRiseDistance;
        public Ease PickupEase => pickupEase;
    }
}
