using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 스테이지 클리어 영수증의 <b>등장 연출</b>만 맡는다.
    ///
    /// 오버레이 → 프린터처럼 끊어 내려오는 영수증(줄은 출력구를 지날 때 찍힘) → CLEAR 도장 →
    /// 골드 카운트업 → 버튼. 글자 채우기는 <see cref="StageReceiptView"/>가 하고,
    /// 밖에서는 <see cref="Play"/>만 부른다.
    ///
    /// <b>레이아웃 그룹</b>: 줄 수에 맞춰 영수증 길이를 정하려고 레이아웃을 쓰지만, 연출 중에
    /// 레이아웃이 다시 계산되면 움직이던 줄이 제자리로 튄다. 그래서 시작할 때 한 번 강제로
    /// 계산해 최종 위치를 잡아 두고, 연출 동안은 그룹·피터를 꺼 둔다. 끝나면 줄을 그 최종
    /// 위치에 정확히 돌려놓은 다음 다시 켠다 — 켜져도 위치가 같으니 아무것도 튀지 않는다.
    ///
    /// 탭하면 바로 최종 상태로 건너뛴다. 일시정지 중에도 돌도록 전부 unscaled time이다.
    /// </summary>
    public sealed class ReceiptRevealAnimator : MonoBehaviour, IPointerDownHandler
    {
        [Header("연결")]
        [SerializeField] private StageReceiptView view;

        [SerializeField, Tooltip("반투명 검정 오버레이. 알파만 움직인다.")]
        private Graphic overlay;

        [SerializeField, Tooltip("출력구. RectMask2D가 붙어 있고, 윗변이 영수증이 나오는 선이다.")]
        private RectTransform printerSlot;

        [SerializeField, Tooltip("출력구 높이를 영수증 길이에 맞출 LayoutElement(출력구에 붙어 있음).")]
        private LayoutElement printerSlotLayout;

        [SerializeField, Tooltip("내려오는 영수증. 출력구 안에 위쪽 기준(pivot y=1)으로 놓는다.")]
        private RectTransform receipt;

        [SerializeField] private RectTransform stamp;

        [SerializeField, Tooltip("등장 순서대로. 꺼져 있는 버튼(마지막 스테이지의 '다음')은 건너뛴다.")]
        private Button[] buttons = new Button[0];

        [SerializeField, Tooltip("레이아웃을 다시 계산하고, 연출 동안 얼려 둘 최상위.")]
        private RectTransform layoutRoot;

        [Header("0. 배경")]
        [SerializeField, Min(0f)] private float overlayDuration = 0.2f;
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.7f;

        [Header("1. 영수증 출력")]
        [SerializeField, Min(1)] private int printSteps = 8;
        [SerializeField, Min(0.01f)] private float printStepMove = 0.06f;
        [SerializeField, Min(0f)] private float printStepPause = 0.04f;
        [SerializeField, Min(0f), Tooltip("좌우 떨림(캔버스 단위). UI가 Figma의 약 2.6배라 1.5px ≈ 4.")]
        private float printJitter = 4f;
        [SerializeField, Min(0f), Tooltip("마지막에 더 내려갔다 올라오는 거리.")]
        private float bounceOvershoot = 24f;
        [SerializeField, Min(0.01f)] private float bounceDuration = 0.2f;
        [SerializeField, Min(0f), Tooltip("출력구 아래 여유(바운스·그림자가 잘리지 않게).")]
        private float slotBottomPadding = 60f;

        [Header("2. 품목 찍힘")]
        [SerializeField, Min(0.01f)] private float lineFadeDuration = 0.15f;
        [SerializeField, Tooltip("왼쪽에서 밀려 들어오는 거리. 10px ≈ 26.")]
        private float lineSlide = 26f;
        [SerializeField, Range(0f, 1f)] private float iconPopFrom = 0.6f;
        [SerializeField, Min(0.01f)] private float iconPopDuration = 0.2f;

        [Header("3. CLEAR 도장")]
        [SerializeField, Min(0.01f)] private float stampDuration = 0.35f;
        [SerializeField] private float stampStartScale = 2.2f;
        [SerializeField] private float stampOvershootScale = 0.95f;
        [SerializeField] private float stampStartRotation = -12f;
        [SerializeField] private float stampEndRotation = -4f;
        [SerializeField, Min(0f)] private float stampShakeDuration = 0.15f;
        [SerializeField, Min(0f)] private float stampShakeStrength = 10f;

        [Header("4. 골드 카운트업")]
        [SerializeField, Min(0.01f)] private float goldDuration = 0.6f;
        [SerializeField] private float goldPunchScale = 1.2f;
        [SerializeField, Min(0.01f)] private float goldPunchDuration = 0.2f;

        [Header("5. 버튼")]
        [SerializeField, Min(0.01f)] private float buttonDuration = 0.4f;
        [SerializeField, Tooltip("아래에서 올라오는 거리. 40px ≈ 104.")]
        private float buttonSlide = 104f;
        [SerializeField, Min(0f)] private float buttonStagger = 0.1f;

        [Header("효과음 호출 지점 (건너뛰면 울리지 않는다)")]
        public UnityEvent onLineTick = new UnityEvent();
        public UnityEvent onStamp = new UnityEvent();
        public UnityEvent onCoin = new UnityEvent();

        [Tooltip("연출이 끝나 버튼을 누를 수 있게 된 순간. 건너뛰어도 울린다.")]
        public UnityEvent onFinished = new UnityEvent();

        private Sequence _sequence;
        private bool _skipping;
        private StageReceiptData _lastData;
        private bool _hasData;

        private readonly List<Behaviour> _frozen = new List<Behaviour>();

        public bool IsPlaying => _sequence != null && _sequence.IsActive() && _sequence.IsPlaying();

        public void Play(StageReceiptData data)
        {
            _lastData = data;
            _hasData = true;

            Stop();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            List<StageReceiptView.Line> lines = view != null ? view.Bind(data) : new List<StageReceiptView.Line>();

            ResetTransforms();
            float height = MeasureAndFreeze();

            // ---- 시작 상태 ----
            Vector2 rest = receipt.anchoredPosition;
            receipt.anchoredPosition = rest + new Vector2(0f, height);
            SetOverlayAlpha(0f);

            var lineBases = new Vector2[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                lineBases[i] = lines[i].Rect.anchoredPosition;
                GroupOf(lines[i].Rect).alpha = 0f;
                if (lines[i].Icon != null)
                {
                    lines[i].Icon.localScale = Vector3.one * iconPopFrom;
                }
            }

            CanvasGroup stampGroup = stamp != null ? GroupOf(stamp) : null;
            if (stampGroup != null)
            {
                stampGroup.alpha = 0f;
            }

            if (view != null)
            {
                view.SetGold(0, data.PreviousGold);
            }

            var shown = new List<Button>();
            var buttonBases = new List<Vector2>();
            foreach (var button in buttons)
            {
                if (button == null || !button.gameObject.activeSelf)
                {
                    continue;
                }

                var rt = (RectTransform)button.transform;
                shown.Add(button);
                buttonBases.Add(rt.anchoredPosition);
                SetButtonVisible(button, false);
            }

            // ---- 연출 ----
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);

            // 0. 배경
            if (overlay != null)
            {
                seq.Append(DOTween.To(() => overlay.color.a, SetOverlayAlpha, overlayAlpha, overlayDuration));
            }

            // 1. 출력: 1/N씩 끊어서, 멈출 때마다 좌우로 미세하게 흔들린다
            float printStart = seq.Duration(false);
            float stepTime = printStepMove + printStepPause;
            for (int k = 1; k <= printSteps; k++)
            {
                float y = rest.y + height * (1f - (float)k / printSteps);
                float x = rest.x + (k < printSteps ? Random.Range(-printJitter, printJitter) : 0f);
                seq.Append(AnchorTo(receipt, new Vector2(x, y), printStepMove).SetEase(Ease.OutQuad));
                seq.AppendInterval(printStepPause);
            }

            seq.Append(AnchorTo(receipt, rest - new Vector2(0f, bounceOvershoot), bounceDuration * 0.5f).SetEase(Ease.OutQuad));
            seq.Append(AnchorTo(receipt, rest, bounceDuration * 0.5f).SetEase(Ease.InOutQuad));
            float printEnd = seq.Duration(false);

            // 2. 줄은 출력구를 지나는 그 단계가 끝날 때 찍힌다(위치로 계산하니 줄 수가 달라도 맞는다)
            for (int i = 0; i < lines.Count; i++)
            {
                int step = StepWhenVisible(lines[i].Rect, height);
                float at = printStart + step * stepTime - printStepPause;
                InsertLine(seq, at, lines[i], lineBases[i]);
            }

            // 3. 도장 — 쾅 찍히는 순간 영수증이 짧게 흔들린다
            if (stamp != null)
            {
                float impact = stampDuration * 0.6f;
                seq.Insert(printEnd, FadeTo(stampGroup, 1f, stampDuration * 0.3f));
                seq.Insert(printEnd, stamp.DOScale(stampOvershootScale, impact).From(Vector3.one * stampStartScale).SetEase(Ease.InQuad));
                seq.Insert(printEnd + impact, stamp.DOScale(1f, stampDuration - impact).SetEase(Ease.OutQuad));
                seq.Insert(printEnd, stamp.DOLocalRotate(new Vector3(0f, 0f, stampEndRotation), impact)
                    .From(new Vector3(0f, 0f, stampStartRotation)).SetEase(Ease.OutQuad));
                seq.InsertCallback(printEnd + impact, () => Fire(onStamp));
                if (stampShakeDuration > 0f)
                {
                    seq.Insert(printEnd + impact,
                        ShakeAnchor(receipt, stampShakeDuration, stampShakeStrength));
                }
            }

            // 4. 골드 카운트업 — 도장이 다 찍힌 뒤
            float goldStart = printEnd + (stamp != null ? stampDuration : 0f);
            float goldT = 0f;
            seq.Insert(goldStart, DOTween.To(() => goldT, v =>
            {
                goldT = v;
                if (view != null)
                {
                    view.SetGold(
                        Mathf.RoundToInt(Mathf.Lerp(0, data.EarnedGold, v)),
                        Mathf.RoundToInt(Mathf.Lerp(data.PreviousGold, data.TotalGold, v)));
                }
            }, 1f, goldDuration).SetEase(Ease.OutCubic));

            float goldEnd = goldStart + goldDuration;
            seq.InsertCallback(goldEnd, () => Fire(onCoin));
            RectTransform goldRect = view != null ? view.EarnedGoldRect : null;
            if (goldRect != null)
            {
                seq.Insert(goldEnd, goldRect.DOScale(1f, goldPunchDuration).From(Vector3.one * goldPunchScale, false).SetEase(Ease.OutBack));
            }

            // 5. 버튼 — 영수증이 다 끝난 뒤에만
            float buttonsStart = goldEnd + goldPunchDuration;
            for (int i = 0; i < shown.Count; i++)
            {
                var rt = (RectTransform)shown[i].transform;
                CanvasGroup g = GroupOf(rt);
                float at = buttonsStart + i * buttonStagger;
                seq.Insert(at, FadeTo(g, 1f, buttonDuration * 0.6f));
                seq.Insert(at, AnchorTo(rt, buttonBases[i], buttonDuration)
                    .From(buttonBases[i] - new Vector2(0f, buttonSlide)).SetEase(Ease.OutBack));
            }

            seq.OnComplete(() =>
            {
                for (int i = 0; i < shown.Count; i++)
                {
                    ((RectTransform)shown[i].transform).anchoredPosition = buttonBases[i];
                    SetButtonVisible(shown[i], true);
                }

                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i].Rect.anchoredPosition = lineBases[i];
                }

                receipt.anchoredPosition = rest;
                Unfreeze();

                // 효과음이 아니라 "다 끝났다" 알림이라 건너뛰었을 때도 울린다.
                onFinished.Invoke();
            });

            _sequence = seq;
        }

        /// <summary>남은 연출을 전부 최종 상태로 끝낸다. 효과음은 몰아서 울리지 않는다.</summary>
        public void Skip()
        {
            if (!IsPlaying)
            {
                return;
            }

            _skipping = true;
            _sequence.Complete(true);
            _skipping = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Skip();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void Stop()
        {
            if (_sequence != null)
            {
                _sequence.Kill();
                _sequence = null;
            }

            Unfreeze();
        }

        private void InsertLine(Sequence seq, float at, StageReceiptView.Line line, Vector2 basePos)
        {
            seq.InsertCallback(at, () => Fire(onLineTick));
            seq.Insert(at, FadeTo(GroupOf(line.Rect), 1f, lineFadeDuration));
            seq.Insert(at, AnchorTo(line.Rect, basePos, lineFadeDuration)
                .From(basePos - new Vector2(lineSlide, 0f)).SetEase(Ease.OutQuad));

            if (line.Icon != null)
            {
                seq.Insert(at, line.Icon.DOScale(1f, iconPopDuration).SetEase(Ease.OutBack));
            }
        }

        /// <summary>
        /// 줄 가운데가 출력구 아래로 나오는 단계(1~N). 영수증은 아래쪽부터 드러나므로
        /// 영수증 바닥에서 그 줄까지의 거리로 정한다.
        /// </summary>
        private int StepWhenVisible(RectTransform line, float height)
        {
            Vector3 center = receipt.InverseTransformPoint(line.TransformPoint(line.rect.center));
            float fromBottom = center.y - receipt.rect.yMin;
            int step = Mathf.CeilToInt(fromBottom / (height / printSteps));
            return Mathf.Clamp(step, 1, printSteps);
        }

        /// <summary>영수증 길이를 재고, 출력구를 거기에 맞춘 뒤 레이아웃을 얼린다.</summary>
        private float MeasureAndFreeze()
        {
            Unfreeze();

            LayoutRebuilder.ForceRebuildLayoutImmediate(receipt);
            float height = receipt.rect.height;

            if (printerSlotLayout != null)
            {
                printerSlotLayout.preferredHeight = height + slotBottomPadding;
            }

            if (layoutRoot != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
            }

            Transform root = layoutRoot != null ? layoutRoot : receipt;
            foreach (var group in root.GetComponentsInChildren<LayoutGroup>())
            {
                if (group.enabled) { group.enabled = false; _frozen.Add(group); }
            }

            foreach (var fitter in root.GetComponentsInChildren<ContentSizeFitter>())
            {
                if (fitter.enabled) { fitter.enabled = false; _frozen.Add(fitter); }
            }

            return height;
        }

        private void Unfreeze()
        {
            for (int i = 0; i < _frozen.Count; i++)
            {
                if (_frozen[i] != null)
                {
                    _frozen[i].enabled = true;
                }
            }

            _frozen.Clear();
        }

        /// <summary>이전 재생이 중간에 끊겼어도 크기·회전이 남지 않게 되돌린다.</summary>
        private void ResetTransforms()
        {
            if (stamp != null)
            {
                stamp.localScale = Vector3.one;
                stamp.localRotation = Quaternion.Euler(0f, 0f, stampEndRotation);
            }

            RectTransform goldRect = view != null ? view.EarnedGoldRect : null;
            if (goldRect != null)
            {
                goldRect.localScale = Vector3.one;
            }
        }

        private void SetOverlayAlpha(float a)
        {
            if (overlay != null)
            {
                Color c = overlay.color;
                c.a = a;
                overlay.color = c;
            }
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            CanvasGroup g = GroupOf((RectTransform)button.transform);
            g.alpha = visible ? 1f : 0f;
            g.blocksRaycasts = visible;
            button.interactable = visible;
        }

        // DOTween UI 모듈은 이 어셈블리에서 안 보인다 — 다른 View들처럼 DOTween.To로 직접 만든다.

        private static TweenerCore<Vector2, Vector2, VectorOptions> AnchorTo(RectTransform rect, Vector2 target, float duration)
            => DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, target, duration);

        private static Tweener FadeTo(CanvasGroup group, float alpha, float duration)
            => DOTween.To(() => group.alpha, v => group.alpha = v, alpha, duration);

        private static Tweener ShakeAnchor(RectTransform rect, float duration, float strength)
            => DOTween.Shake(() => (Vector3)rect.anchoredPosition, v => rect.anchoredPosition = v,
                duration, new Vector3(strength, strength, 0f), 30, 90f, true);

        private static CanvasGroup GroupOf(RectTransform rect)
        {
            var g = rect.GetComponent<CanvasGroup>();
            return g != null ? g : rect.gameObject.AddComponent<CanvasGroup>();
        }

        private void Fire(UnityEvent e)
        {
            if (!_skipping && e != null)
            {
                e.Invoke();
            }
        }

        [ContextMenu("연출 다시 재생 (Play Mode)")]
        private void Replay()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"{nameof(ReceiptRevealAnimator)}: 플레이 모드에서만 재생됩니다.", this);
                return;
            }

            Play(_hasData ? _lastData : SampleData());
        }

        [ContextMenu("건너뛰기")]
        private void SkipFromMenu()
        {
            Skip();
        }

        private static StageReceiptData SampleData()
        {
            return new StageReceiptData
            {
                StageLabel = "1",
                EnemiesKilled = 12,
                Materials = new List<StageReceiptData.Material>
                {
                    new StageReceiptData.Material { Id = "scrap", DisplayName = "고철", Count = 2 },
                    new StageReceiptData.Material { Id = "pellet", DisplayName = "펠릿", Count = 1 },
                },
                ClearSeconds = 95f,
                EarnedGold = 31,
                PreviousGold = 95,
            };
        }
    }
}
