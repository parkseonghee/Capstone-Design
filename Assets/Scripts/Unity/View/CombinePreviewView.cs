using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 조합 미리보기 창(롤토체스 아이템 조합 방식). 재료 칸을 끌어 다른 재료 칸 위에 올리면 뜬다.
    ///
    ///   [결과 이름]
    ///   결과 설명
    ///   [A] + [B]   놓으면 조합 완료  [결과]
    ///               ████████████████
    ///
    /// <b>놓는 순간 조합된다.</b> BAR는 기다리는 시간이 아니라 "지금 놓으면 된다"는 표시라,
    /// 뜰 때 한 번 차오르고 꽉 찬 채로 있다.
    ///
    /// 아직 배우지 않은 조합은 결과를 ???로 가린다 — 겹쳐 보며 때려 맞히는 재미를 남긴다.
    /// 조합이 안 되는 짝에는 아예 뜨지 않는다(InventoryView가 정한다).
    ///
    /// 이 창은 손가락 밑의 칸을 가리면 안 되므로 터치를 전부 통과시킨다.
    /// 위치·크기는 씬이 정한다(Hard Rule 2).
    /// </summary>
    public sealed class CombinePreviewView : MonoBehaviour
    {
        [Header("화면")]
        [SerializeField, Tooltip("창 전체. 평소에는 꺼져 있다.")]
        private GameObject panel;

        [SerializeField] private Text resultNameLabel;
        [SerializeField] private Text resultDescriptionLabel;

        [SerializeField, Tooltip("재료 A 칸의 글자.")]
        private Text inputALabel;

        [SerializeField, Tooltip("재료 B 칸의 글자.")]
        private Text inputBLabel;

        [SerializeField, Tooltip("결과 칸의 글자.")]
        private Text resultTileLabel;

        [SerializeField, Tooltip("'놓으면 조합 완료' 안내.")]
        private Text statusLabel;

        [SerializeField, Tooltip("BAR. Image Type이 Filled여야 차오른다.")]
        private Image bar;

        [Header("BAR")]
        [SerializeField, Min(0f), Tooltip("창이 뜰 때 BAR가 차오르는 시간(초). 0이면 처음부터 꽉 차 있다.")]
        private float barFillDuration = 0.15f;

        [Header("문구")]
        [SerializeField] private string readyText = "놓으면 조합 완료";
        [SerializeField] private string unknownName = "???";
        [SerializeField] private string unknownDescription = "아직 모르는 조합입니다. 놓으면 알게 됩니다.";

        private string _shownA;
        private string _shownB;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            // 창이 칸을 가려 떨굴 자리를 못 찾으면 조합이 안 된다. 레이캐스트를 통째로 끈다.
            if (panel != null)
            {
                var group = panel.GetComponent<CanvasGroup>();
                if (group == null) { group = panel.AddComponent<CanvasGroup>(); }
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        /// <summary>창을 띄운다. 같은 짝을 다시 띄우면 BAR를 처음부터 다시 채우지 않는다.</summary>
        public void Show(ItemConfig.Entry a, ItemConfig.Entry b, ItemConfig.Entry result, bool known)
        {
            bool samePair = IsOpen && _shownA == a.id && _shownB == b.id;
            _shownA = a.id;
            _shownB = b.id;

            SetText(resultNameLabel, known ? result.displayName : unknownName);
            SetText(resultDescriptionLabel, known ? result.description : unknownDescription);
            SetText(inputALabel, a.displayName);
            SetText(inputBLabel, b.displayName);
            SetText(resultTileLabel, known ? result.displayName : unknownName);
            SetText(statusLabel, readyText);

            if (panel != null)
            {
                panel.transform.SetAsLastSibling();
                panel.SetActive(true);
            }

            if (!samePair)
            {
                FillBar();
            }
        }

        public void Hide()
        {
            _shownA = null;
            _shownB = null;

            if (bar != null)
            {
                bar.DOKill();
            }

            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }
        }

        private void FillBar()
        {
            if (bar == null)
            {
                return;
            }

            bar.DOKill();

            if (barFillDuration <= 0f)
            {
                bar.fillAmount = 1f;
                return;
            }

            bar.fillAmount = 0f;
            DOTween.To(() => bar.fillAmount, v => bar.fillAmount = v, 1f, barFillDuration)
                .SetEase(Ease.OutQuad)
                .SetTarget(bar);
        }

        private static void SetText(Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
