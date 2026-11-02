using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 게임 씬 HUD의 스크랩 표시(재화 문서 §1). 이번 런에서 모은 스크랩 — 런 중 상점에서 쓰는 돈이다.
    ///
    /// 마을의 골드 표시(<see cref="GoldView"/>)와 같은 모양으로, 값이 바뀌면 숫자를 카운트업하고 살짝 커졌다 돌아온다.
    /// 스크랩은 스텝(처치 드랍·상점 구매) 말고도 버리기·환불처럼 스텝 밖에서도 바뀌므로
    /// <see cref="GameSession.BoardChanged"/>도 함께 듣는다.
    ///
    /// 배치·크기는 전부 씬에서 정한다. 이 스크립트는 숫자와 크기 연출만 바꾼다(Hard Rule 2).
    /// </summary>
    public sealed class ScrapCounterView : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private GameSession session;
        [SerializeField] private Text label;
        [SerializeField, Tooltip("커졌다 돌아올 대상. 비우면 이 오브젝트.")]
        private RectTransform popTarget;

        [Header("연출")]
        [SerializeField, Tooltip("표시 형식. {0}이 스크랩이다.")]
        private string format = "{0}";
        [SerializeField, Min(0.01f)] private float countDuration = 0.35f;
        [SerializeField, Min(1f)] private float popScale = 1.15f;
        [SerializeField, Min(0.01f)] private float popDuration = 0.2f;

        /// <summary>지금 찍혀 있는 값(카운트업 중간값 포함).</summary>
        private int _shown = int.MinValue;

        /// <summary>마지막으로 향하던 목표값. 같으면 연출을 다시 걸지 않는다.</summary>
        private int _target = int.MinValue;

        private void Awake()
        {
            if (popTarget == null)
            {
                popTarget = (RectTransform)transform;
            }
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.RunStarted += HandleRunStarted;
                session.Stepped += HandleStepped;
                session.BoardChanged += Refresh;

                if (session.Loop != null)
                {
                    Snap(session.Loop.Player.Scrap);
                }
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.RunStarted -= HandleRunStarted;
                session.Stepped -= HandleStepped;
                session.BoardChanged -= Refresh;
            }
        }

        private void OnDestroy()
        {
            this.DOKill();
            if (popTarget != null)
            {
                popTarget.DOKill();
            }
        }

        private void HandleRunStarted(GameLoop loop)
        {
            // 새 런(재시작 포함)은 연출 없이 바로 맞춘다.
            Snap(loop.Player.Scrap);
        }

        private void HandleStepped(StepResult result) => Refresh();

        private void Refresh()
        {
            if (session == null || session.Loop == null)
            {
                return;
            }

            int scrap = session.Loop.Player.Scrap;
            if (scrap == _target)
            {
                return;
            }

            _target = scrap;

            this.DOKill();
            DOTween.To(() => _shown, Show, scrap, countDuration)
                .SetEase(Ease.OutCubic)
                .SetTarget(this);

            popTarget.DOKill();
            popTarget.localScale = Vector3.one;
            popTarget.DOScale(popScale, popDuration * 0.5f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo);
        }

        private void Snap(int scrap)
        {
            this.DOKill();
            if (popTarget != null)
            {
                popTarget.DOKill();
                popTarget.localScale = Vector3.one;
            }

            _target = scrap;
            Show(scrap);
        }

        private void Show(int value)
        {
            // 값이 그대로면 문자열을 새로 만들지 않는다(Hard Rule 8).
            if (label == null || _shown == value)
            {
                return;
            }

            _shown = value;
            label.text = string.Format(format, value);
        }
    }
}
