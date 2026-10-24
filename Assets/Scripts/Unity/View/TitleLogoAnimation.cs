using DG.Tweening;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 시작화면 제목 로고의 등장·대기 연출. 로고 오브젝트에 붙인다.
    ///
    /// 1) scale 0으로 숨겨 두었다가 지연 후 화면 위쪽 밖에서 원래 위치로 통통 튀며 떨어진다.
    /// 2) 착지하면 젤리처럼 찌그러졌다 복원된다.
    /// 3) 이후 위아래로 둥실둥실 떠 있으면서 살짝 좌우로 흔들린다.
    ///
    /// 원래 위치는 씬에 배치된 값을 Awake에서 저장해 쓴다. 수치는 인스펙터에서 조절한다(Hard Rule 1).
    /// DOTweenModuleUI는 Plugins(firstpass) 어셈블리라 참조할 수 없으므로 anchoredPosition 트윈은 DOTween.To로 만든다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleLogoAnimation : MonoBehaviour
    {
        [Header("등장")]
        [SerializeField, Min(0f)] private float appearDelay = 0.3f;
        [SerializeField, Min(0.01f)] private float dropDuration = 0.6f;
        [SerializeField] private Ease dropEase = Ease.OutBounce;
        [SerializeField, Min(0f), Tooltip("화면 위쪽 밖으로 더 띄워둘 여유 거리(px).")]
        private float dropStartMargin = 40f;

        [Header("착지 젤리")]
        [SerializeField] private Vector2 squashScale = new Vector2(1.2f, 0.8f);
        [SerializeField] private Vector2 stretchScale = new Vector2(0.9f, 1.1f);
        [SerializeField, Min(0.01f)] private float squashDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float stretchDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float settleDuration = 0.14f;

        [Header("둥실둥실")]
        [SerializeField, Min(0f)] private float floatAmplitude = 12f;
        [SerializeField, Min(0.01f)] private float floatDuration = 2f;
        [SerializeField, Min(0f)] private float wobbleAngle = 2f;
        [SerializeField, Min(0.01f)] private float wobbleDuration = 2.4f;

        private RectTransform _rect;
        private Vector2 _homePosition;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _homePosition = _rect.anchoredPosition;
            _rect.localScale = Vector3.zero;
        }

        private void Start()
        {
            Vector2 dropStart = _homePosition + Vector2.up * GetOffscreenDropDistance();

            DOTween.Sequence()
                .SetTarget(this)
                .AppendInterval(appearDelay)
                .AppendCallback(() =>
                {
                    _rect.anchoredPosition = dropStart;
                    _rect.localScale = Vector3.one;
                })
                .Append(DOTween.To(() => _rect.anchoredPosition, v => _rect.anchoredPosition = v, _homePosition, dropDuration)
                    .SetEase(dropEase))
                .Append(_rect.DOScale(new Vector3(squashScale.x, squashScale.y, 1f), squashDuration).SetEase(Ease.OutQuad))
                .Append(_rect.DOScale(new Vector3(stretchScale.x, stretchScale.y, 1f), stretchDuration).SetEase(Ease.InOutQuad))
                .Append(_rect.DOScale(Vector3.one, settleDuration).SetEase(Ease.OutQuad))
                .OnComplete(StartIdle);
        }

        private void OnDestroy()
        {
            this.DOKill();
            _rect.DOKill();
        }

        /// <summary>로고 아래 끝이 부모(화면) 위쪽 끝을 넘을 때까지 올려야 하는 거리.</summary>
        private float GetOffscreenDropDistance()
        {
            var parent = (RectTransform)_rect.parent;
            float anchorToParentTop = parent.rect.height * (1f - _rect.anchorMin.y);
            float logoBelowPivot = _rect.rect.height * _rect.pivot.y;
            return anchorToParentTop - _homePosition.y + logoBelowPivot + dropStartMargin;
        }

        private void StartIdle()
        {
            // 원래 자리에서 꼭대기까지 반 주기로 출발해, 이후 +진폭 ↔ -진폭을 무한 왕복한다(속도 끊김 없음).
            float topY = _homePosition.y + floatAmplitude;
            float bottomY = _homePosition.y - floatAmplitude;
            DOTween.To(() => _rect.anchoredPosition.y, SetY, topY, floatDuration * 0.5f)
                .SetEase(Ease.OutSine)
                .SetTarget(this)
                .OnComplete(() => DOTween.To(() => _rect.anchoredPosition.y, SetY, bottomY, floatDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetTarget(this));

            _rect.DOLocalRotate(new Vector3(0f, 0f, wobbleAngle), wobbleDuration * 0.5f)
                .SetEase(Ease.OutSine)
                .OnComplete(() => _rect.DOLocalRotate(new Vector3(0f, 0f, -wobbleAngle), wobbleDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo));
        }

        private void SetY(float y)
        {
            _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, y);
        }
    }
}
