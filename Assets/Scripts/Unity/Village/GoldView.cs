using DG.Tweening;
using TMPro;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// HUD의 골드 숫자. <see cref="GoldManager.GoldChanged"/>를 받아 숫자를 카운트업하고 살짝 커졌다 돌아온다.
    ///
    /// 값은 GoldManager가 갖고 있고 이 클래스는 표시만 한다 — 피그마 HUD로 바꿀 때 이 컴포넌트만 새 텍스트에 옮기면 된다.
    /// </summary>
    public sealed class GoldView : MonoBehaviour
    {
        [SerializeField] private GoldManager goldManager;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField, Tooltip("커졌다 돌아올 대상. 비우면 라벨 자신.")]
        private RectTransform popTarget;

        [SerializeField, Tooltip("TMP SetText 형식. {0:0}이 골드(소수점 없이)다.")]
        private string format = "{0:0}";
        [SerializeField, Min(0.01f)] private float countDuration = 0.5f;
        [SerializeField, Min(1f)] private float popScale = 1.15f;
        [SerializeField, Min(0.01f)] private float popDuration = 0.2f;

        private int _shown;

        private void Awake()
        {
            if (popTarget == null)
            {
                popTarget = label.rectTransform;
            }
        }

        private void OnEnable()
        {
            Show(goldManager.Gold);
            goldManager.GoldChanged += OnGoldChanged;
        }

        private void OnDisable()
        {
            goldManager.GoldChanged -= OnGoldChanged;
        }

        private void OnDestroy()
        {
            this.DOKill();
            popTarget.DOKill();
        }

        private void OnGoldChanged(int gold)
        {
            this.DOKill();
            DOTween.To(() => _shown, Show, gold, countDuration)
                .SetEase(Ease.OutCubic)
                .SetTarget(this);

            popTarget.DOKill();
            popTarget.localScale = Vector3.one;
            popTarget.DOScale(popScale, popDuration * 0.5f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo);
        }

        private void Show(int value)
        {
            _shown = value;
            label.SetText(format, value);
        }
    }
}
