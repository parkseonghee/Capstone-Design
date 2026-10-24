using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을 패널 공통 베이스. 열기/닫기 연출과 닫기 버튼만 담당한다.
    ///
    /// 패널별 내용(상점 목록 등)은 이 클래스를 상속해 <see cref="OnOpened"/>/<see cref="OnClosed"/>에서 채운다.
    /// 화면 구성은 프리팹이 정한다 — 피그마 디자인으로 바꿀 때 프리팹의 그래픽만 갈아끼우고
    /// 아래 참조(창·CanvasGroup·닫기 버튼)만 다시 연결하면 된다.
    /// </summary>
    public class UIPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("BuildingData의 Panel Id와 맞추는 키.")]
        private string panelId;

        [Header("연결 (프리팹에서 배치)")]
        [SerializeField, Tooltip("커졌다 작아지는 창 본체.")]
        private RectTransform window;
        [SerializeField, Tooltip("패널 전체(배경 막 포함)의 알파와 입력 차단.")]
        private CanvasGroup canvasGroup;
        [SerializeField] private Button closeButton;

        [Header("연출")]
        [SerializeField, Range(0f, 1f)] private float hiddenScale = 0.8f;
        [SerializeField, Min(0.01f)] private float duration = 0.25f;
        [SerializeField] private Ease openEase = Ease.OutBack;
        [SerializeField] private Ease closeEase = Ease.InBack;

        /// <summary>닫기 연출까지 끝나 완전히 닫혔을 때.</summary>
        public event Action<UIPanel> Closed;

        public string PanelId => panelId;
        public bool IsOpen { get; private set; }

        protected virtual void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
        }

        protected virtual void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }

            this.DOKill();
            window.DOKill();
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            gameObject.SetActive(true);
            OnOpened();

            this.DOKill();
            window.DOKill();
            window.localScale = Vector3.one * hiddenScale;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            window.DOScale(1f, duration).SetEase(openEase);
            DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 1f, duration).SetTarget(this);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            // 닫히는 동안 버튼을 또 누르지 못하게 한다. 화면 차단은 끝날 때까지 유지한다.
            canvasGroup.interactable = false;

            this.DOKill();
            window.DOKill();
            window.DOScale(hiddenScale, duration).SetEase(closeEase);
            DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 0f, duration)
                .SetTarget(this)
                .OnComplete(FinishClose);
        }

        /// <summary>창이 열리기 시작할 때. 상속한 패널이 내용을 채운다.</summary>
        protected virtual void OnOpened()
        {
        }

        /// <summary>완전히 닫힌 뒤.</summary>
        protected virtual void OnClosed()
        {
        }

        private void FinishClose()
        {
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            OnClosed();
            Closed?.Invoke(this);
        }
    }
}
