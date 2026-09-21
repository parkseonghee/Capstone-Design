using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 스테이지 선택 화면. <b>고른 스테이지 하나가 가운데 크게</b> 뜨고,
    /// 좌우로 밀면 이웃 스테이지가 가운데로 온다(1-1 → 1-2 → …).
    ///
    /// 카드는 셋뿐이다 — 왼쪽(작게) · 가운데(크게) · 오른쪽(작게).
    /// 미는 동작은 카드를 옮기는 게 아니라 <b>세 카드가 가리키는 스테이지를 바꿔 끼우는</b> 것이다.
    /// 덕분에 카드의 크기·위치를 코드가 계산하지 않는다 — 전부 씬에 잡혀 있다(Hard Rule 2).
    ///
    /// 스테이지 목록은 챕터를 가로질러 쭉 이어진다(1-1 … 1-3 → 2-1 …).
    /// 챕터 라벨은 지금 가운데 카드가 속한 챕터를 따라간다.
    ///
    /// 잠금·클리어 판정은 전부 RunProgress가 한다(Hard Rule 5).
    /// </summary>
    public sealed class StageMapView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("연결")]
        [SerializeField, Tooltip("스테이지 목록과 클리어 기록. 마을과 게임 씬이 같은 에셋을 물어야 한다.")]
        private RunProgress runProgress;

        [Header("화면")]
        [SerializeField, Tooltip("선택 화면 전체. 평소에는 꺼져 있어야 한다.")]
        private GameObject panel;

        [SerializeField, Tooltip("선택 화면을 여는 버튼.")]
        private Button openButton;

        [SerializeField, Tooltip("닫는 버튼.")]
        private Button closeButton;

        [Header("카드 (씬에 크기·위치를 잡아 둔다)")]
        [SerializeField, Tooltip("가운데 카드. 크게 띄울 자리이며 누르면 그 스테이지로 들어간다.")]
        private StageMapNode centerCard;

        [SerializeField, Tooltip("왼쪽에 살짝 보이는 이전 스테이지 카드. 누르면 그쪽으로 넘어간다.")]
        private StageMapNode leftCard;

        [SerializeField, Tooltip("오른쪽에 살짝 보이는 다음 스테이지 카드. 누르면 그쪽으로 넘어간다.")]
        private StageMapNode rightCard;

        [Header("라벨 / 버튼")]
        [SerializeField, Tooltip("'챕터 1' 라벨. 가운데 카드가 속한 챕터를 따라간다.")]
        private Text chapterLabel;

        [SerializeField, Tooltip("이전 스테이지 화살표.")]
        private Button prevButton;

        [SerializeField, Tooltip("다음 스테이지 화살표.")]
        private Button nextButton;

        [SerializeField] private string chapterFormat = "챕터 {0}";

        [SerializeField, Tooltip("가운데 카드가 잠겨 있을 때 띄울 안내. 비우면 표시하지 않는다.")]
        private Text hintLabel;

        [SerializeField] private string lockedHint = "이전 스테이지를 먼저 클리어하세요";

        [SerializeField] private string unlockedHint = "눌러서 시작";

        [Header("카드 색")]
        [SerializeField] private Color unlockedColor = new Color(0.62f, 0.26f, 0.78f, 1f);
        [SerializeField] private Color clearedColor = new Color(0.24f, 0.62f, 0.38f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.27f, 0.28f, 0.36f, 1f);

        [Header("밀기")]
        [SerializeField, Min(1f), Tooltip("스테이지를 한 칸 넘길 최소 드래그 거리(픽셀).")]
        private float swipeThresholdPixels = 80f;

        [Header("이동")]
        [SerializeField, Tooltip("게임 씬 이름. Build Settings에 등록돼 있어야 한다.")]
        private string gameSceneName = "SampleScene";

        private int _index;
        private bool _wired;
        private float _dragStartX;
        private bool _dragging;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>지금 가운데 카드가 가리키는 스테이지 인덱스.</summary>
        public int CurrentIndex => _index;

        private void OnEnable()
        {
            if (openButton != null) { openButton.onClick.AddListener(Open); }
            if (closeButton != null) { closeButton.onClick.AddListener(Close); }
            if (prevButton != null) { prevButton.onClick.AddListener(ShowPrevious); }
            if (nextButton != null) { nextButton.onClick.AddListener(ShowNext); }

            if (runProgress != null) { runProgress.Changed += Refresh; }

            WireCards();
            Close();
        }

        private void OnDisable()
        {
            if (openButton != null) { openButton.onClick.RemoveListener(Open); }
            if (closeButton != null) { closeButton.onClick.RemoveListener(Close); }
            if (prevButton != null) { prevButton.onClick.RemoveListener(ShowPrevious); }
            if (nextButton != null) { nextButton.onClick.RemoveListener(ShowNext); }

            if (runProgress != null) { runProgress.Changed -= Refresh; }
        }

        /// <summary>
        /// 카드 클릭을 한 번만 물린다. 가운데는 "들어가기", 양옆은 "그쪽으로 넘기기"다.
        /// 어느 스테이지인지는 클릭 시점에 카드가 들고 있는 값을 읽으므로 다시 물릴 필요가 없다.
        /// </summary>
        private void WireCards()
        {
            if (_wired)
            {
                return;
            }

            _wired = true;

            if (centerCard != null && centerCard.Button != null)
            {
                centerCard.Button.onClick.AddListener(EnterCurrent);
            }

            if (leftCard != null && leftCard.Button != null)
            {
                leftCard.Button.onClick.AddListener(ShowPrevious);
            }

            if (rightCard != null && rightCard.Button != null)
            {
                rightCard.Button.onClick.AddListener(ShowNext);
            }
        }

        public void Open()
        {
            if (panel == null || runProgress == null)
            {
                return;
            }

            // 열면 지금 도전할 스테이지가 가운데 오게 한다.
            _index = runProgress.FurthestUnlockedIndex;

            Refresh();
            panel.SetActive(true);
        }

        public void Close()
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }
        }

        public void ShowNext() => Move(1);

        public void ShowPrevious() => Move(-1);

        private void Move(int delta)
        {
            if (runProgress == null)
            {
                return;
            }

            int target = _index + delta;
            if (target < 0 || target >= runProgress.StageCount)
            {
                return;
            }

            _index = target;
            Refresh();
        }

        // ── 밀기 ─────────────────────────────────────────────────────────
        //
        // 카드가 아니라 화면 전체가 드래그를 받는다. 버튼은 드래그를 처리하지 않아
        // 카드 위에서 시작한 드래그도 여기까지 올라온다.

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _dragStartX = eventData.position.x;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // 넘길지 말지는 손을 뗄 때 정한다. 끄는 도중에 판정하면 한 번에 여러 칸이 넘어간다.
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;

            float dx = eventData.position.x - _dragStartX;
            if (Mathf.Abs(dx) < swipeThresholdPixels)
            {
                return;
            }

            // 왼쪽으로 밀면(dx < 0) 다음 스테이지가 따라 들어온다 — 종이를 넘기는 방향이다.
            Move(dx < 0f ? 1 : -1);
        }

        // ── 표시 ─────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (runProgress == null)
            {
                return;
            }

            Bind(leftCard, _index - 1);
            Bind(centerCard, _index);
            Bind(rightCard, _index + 1);

            if (chapterLabel != null)
            {
                int chapter = runProgress.ChapterNumber(runProgress.ChapterIndexOfStage(_index));
                chapterLabel.text = string.Format(chapterFormat, chapter);
            }

            if (prevButton != null)
            {
                prevButton.interactable = _index > 0;
            }

            if (nextButton != null)
            {
                nextButton.interactable = _index + 1 < runProgress.StageCount;
            }

            if (hintLabel != null)
            {
                hintLabel.text = runProgress.IsUnlocked(_index) ? unlockedHint : lockedHint;
            }
        }

        /// <summary>카드 하나를 그 스테이지로 칠한다. 범위를 벗어나면 감춘다(양 끝에서 한쪽이 빈다).</summary>
        private void Bind(StageMapNode card, int index)
        {
            if (card == null)
            {
                return;
            }

            if (index < 0 || index >= runProgress.StageCount)
            {
                card.Hide();
                return;
            }

            bool cleared = runProgress.IsCleared(index);
            bool unlocked = runProgress.IsUnlocked(index);

            // 양옆 카드는 "넘기기"용이라 잠겨 있어도 눌러서 볼 수 있어야 한다.
            bool pressable = card == centerCard ? unlocked : true;

            card.Bind(index, runProgress.LabelFor(index), pressable, cleared);
            card.Tint(cleared ? clearedColor : (unlocked ? unlockedColor : lockedColor));
        }

        private void EnterCurrent()
        {
            if (runProgress == null || !runProgress.IsUnlocked(_index))
            {
                return;
            }

            runProgress.Select(_index);

            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogWarning($"{nameof(StageMapView)}: gameSceneName이 비어 있습니다.", this);
                return;
            }

            SceneManager.LoadScene(gameSceneName);
        }
    }
}
