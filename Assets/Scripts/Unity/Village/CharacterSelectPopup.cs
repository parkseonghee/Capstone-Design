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
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI cancelLabel;

        [Header("문구")]
        [SerializeField] private string askText = "이 캐릭터를 선택하시겠습니까?";
        [SerializeField] private string alreadyText = "지금 선택된 캐릭터입니다";
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
            if (_target != null && selection != null)
            {
                selection.Select(_target.CharacterId);
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
            messageLabel.text = already ? alreadyText : askText;
            confirmButton.gameObject.SetActive(!already);
            cancelLabel.text = already ? closeText : cancelText;
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
