using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을 캐릭터를 눌렀을 때 화면 아래에서 올라오는 "캐릭터를 선택하시겠습니까?" 창.
    /// 선택을 누르면 <see cref="CharacterSelection"/>에 바로 저장하고 닫는다.
    ///
    /// 뒤의 어두운 막(dimmer)을 누르거나 취소를 누르면 그냥 닫는다.
    /// 이미 고른 캐릭터를 다시 누르면 선택 버튼을 숨기고 안내만 보여 준다.
    ///
    /// 아직 안 산 캐릭터(재화 문서 §5)면 같은 창이 구매 창이 된다 — 가격을 보여 주고, 확인 버튼이 "구매"로 바뀐다.
    /// 구매에 성공하면 그 캐릭터를 바로 고른다. 골드가 모자라면 확인 버튼을 숨기고 필요한 골드만 알려 준다.
    /// </summary>
    public sealed class CharacterSelectPopup : MonoBehaviour, IVillageFeature
    {
        [Header("데이터")]
        [SerializeField, Tooltip("이름과 초상화를 읽을 캐릭터 표.")]
        private CharacterRoster roster;
        [SerializeField, Tooltip("고른 캐릭터를 저장할 곳. 게임 씬과 같은 에셋이어야 한다.")]
        private CharacterSelection selection;

        [Header("연결")]
        [SerializeField, Tooltip("켜고 끌 내용 전체(막 + 창). 이 컴포넌트가 붙은 오브젝트는 켜 둔 채로 둔다.")]
        private GameObject content;
        [SerializeField, Tooltip("아래에서 올라오는 창.")]
        private RectTransform sheet;
        [SerializeField, Tooltip("뒤를 어둡게 덮는 막. 누르면 닫힌다.")]
        private Button dimmer;
        [SerializeField] private CanvasGroup dimmerGroup;
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI messageLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField, Tooltip("확인 버튼의 글자. 선택/구매에 따라 바뀐다. 비우면 바꾸지 않는다.")]
        private TextMeshProUGUI confirmLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI cancelLabel;

        [Header("가격 표시 (마을 골드 UI와 같은 모양)")]
        [SerializeField, Tooltip("금화 + 숫자 알약. 안 산 캐릭터일 때만 켠다. 비우면 가격을 따로 보여 주지 않는다.")]
        private GameObject priceTag;
        [SerializeField] private TextMeshProUGUI priceLabel;
        [SerializeField, Min(0f), Tooltip("가격 알약이 켜졌을 때 메시지를 오른쪽으로 미는 거리(캔버스 px). 알약 너비 + 간격.")]
        private float priceIndent = 214f;
        [SerializeField, Tooltip("살 수 있을 때 가격 숫자 색.")]
        private Color affordColor = new Color(0.302f, 0.294f, 0.196f, 1f);
        [SerializeField, Tooltip("골드가 모자랄 때 가격 숫자 색.")]
        private Color shortColor = new Color(0.85f, 0.25f, 0.2f, 1f);

        [Header("문구")]
        [SerializeField] private string askText = "이 캐릭터를 선택하시겠습니까?";
        [SerializeField] private string alreadyText = "지금 선택된 캐릭터입니다";
        [SerializeField, Tooltip("가격은 옆의 금화 알약이 보여 준다. {0}을 넣으면 가격 숫자로 바뀐다.")]
        private string buyAskText = "구매하시겠습니까?";
        [SerializeField, Tooltip("{0}이 가격, {1}이 지금 가진 골드다.")]
        private string notEnoughText = "골드가 부족합니다";
        [SerializeField] private string selectText = "선택";
        [SerializeField] private string buyText = "구매";
        [SerializeField] private string cancelText = "취소";
        [SerializeField] private string closeText = "닫기";

        [Header("연출")]
        [SerializeField, Min(0.01f)] private float slideDuration = 0.25f;
        [SerializeField, Min(0f), Tooltip("닫혀 있을 때 창이 화면 아래로 내려가 있는 거리(캔버스 px).")]
        private float hiddenOffset = 700f;

        private VillageCharacter _target;
        private float _shownY;
        private bool _open;

        public bool IsOpen => _open;

        private void Awake()
        {
            _shownY = sheet.anchoredPosition.y;
            content.SetActive(false);
        }

        private void OnEnable()
        {
            dimmer.onClick.AddListener(Close);
            cancelButton.onClick.AddListener(Close);
            confirmButton.onClick.AddListener(Confirm);
        }

        private void OnDisable()
        {
            dimmer.onClick.RemoveListener(Close);
            cancelButton.onClick.RemoveListener(Close);
            confirmButton.onClick.RemoveListener(Confirm);
        }

        private void OnDestroy()
        {
            this.DOKill();
        }

        /// <summary>이 캐릭터를 대상으로 창을 연다.</summary>
        public void Show(VillageCharacter target)
        {
            _target = target;
            Open();
        }

        public void Open()
        {
            if (_target == null)
            {
                return;
            }

            Fill(_target.CharacterId);
            _target.Hold();

            _open = true;
            content.SetActive(true);
            Animate(1f, Ease.OutCubic, null);
        }

        public void Close()
        {
            if (!_open)
            {
                return;
            }

            if (_target != null)
            {
                _target.Release();
            }

            Animate(0f, Ease.InQuad, () =>
            {
                content.SetActive(false);
                _open = false; // 다 내려간 뒤에 닫힘을 알린다 — 그 전엔 건물·캐릭터 터치가 막혀 있다.
            });
        }

        private void Confirm()
        {
            if (_target != null && selection != null && !selection.TryBuy(_target.CharacterId))
            {
                // 창을 연 사이 골드가 줄었다 — 닫지 말고 부족 안내로 다시 채운다.
                Fill(_target.CharacterId);
                return;
            }

            Close();
        }

        private void Fill(string id)
        {
            CharacterConfig config = null;
            if (roster != null)
            {
                roster.TryGet(id, out config);
            }

            nameLabel.text = config != null ? config.DisplayName : id;
            portrait.sprite = config != null ? config.Icon : null;
            portrait.enabled = portrait.sprite != null;

            bool already = selection != null && selection.SelectedId == id;
            bool locked = selection != null && !selection.IsUnlocked(id);
            bool canBuy = !locked || selection.CanAfford(id);
            int price = selection != null ? selection.PriceOf(id) : 0;

            if (already)
            {
                messageLabel.text = alreadyText;
            }
            else if (!locked)
            {
                messageLabel.text = askText;
            }
            else if (canBuy)
            {
                messageLabel.text = string.Format(buyAskText, price);
            }
            else
            {
                messageLabel.text = string.Format(notEnoughText, price, selection.Gold);
            }

            // 안 산 캐릭터면 메시지 앞에 금화 알약으로 가격을 띄운다. 모자라면 숫자를 빨갛게.
            bool showPrice = priceTag != null && locked && !already;
            if (priceTag != null)
            {
                priceTag.SetActive(showPrice);
            }

            if (showPrice && priceLabel != null)
            {
                priceLabel.text = price.ToString();
                priceLabel.color = canBuy ? affordColor : shortColor;
            }

            messageLabel.margin = new Vector4(showPrice ? priceIndent : 0f, 0f, 0f, 0f);

            bool showConfirm = !already && canBuy;
            confirmButton.gameObject.SetActive(showConfirm);
            if (confirmLabel != null)
            {
                confirmLabel.text = locked ? buyText : selectText;
            }

            cancelLabel.text = showConfirm ? cancelText : closeText;
        }

        private void Animate(float to, Ease ease, TweenCallback onComplete)
        {
            this.DOKill();
            float from = dimmerGroup.alpha;
            DOTween.To(() => from, v =>
                {
                    from = v;
                    dimmerGroup.alpha = v;
                    sheet.anchoredPosition = new Vector2(sheet.anchoredPosition.x, Mathf.LerpUnclamped(_shownY - hiddenOffset, _shownY, v));
                }, to, slideDuration)
                .SetEase(ease)
                .SetTarget(this)
                .OnComplete(onComplete);
        }
    }
}
