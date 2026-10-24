using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을 건물 하나의 표시와 터치. 평소에는 숨쉬듯 커졌다 작아지고, 눌리면 젤리처럼 찌그러졌다 복원된다.
    ///
    /// 터치는 EventSystem(InputSystemUIInputModule) + 카메라의 Physics2DRaycaster로 받는다 —
    /// 마우스·터치가 같은 경로로 들어오고, 열린 UI 패널이 자연히 먼저 판정돼 건물 터치를 가린다.
    /// 눌렸을 때 무엇을 할지는 정하지 않는다. <see cref="Clicked"/>를 받은 <see cref="VillageManager"/>가 정한다.
    ///
    /// 계층: Building(이 컴포넌트, 콜라이더, 숨쉬기) → Jelly(젤리 연출, 발 기준) → Visual(SpriteRenderer).
    /// 루트 위치가 건물의 발(아래 가운데)이다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Building : MonoBehaviour, IPointerClickHandler
    {
        [Header("연결 (프리팹에서 배치)")]
        [SerializeField, Tooltip("젤리 연출을 받을 노드. 건물 발 위치에 둔다.")]
        private Transform jelly;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private TextMeshPro nameLabel;
        [SerializeField, Tooltip("이름표 뒤의 나무 판자. 비워도 된다.")]
        private SpriteRenderer nameSign;

        [Header("플레이스홀더")]
        [SerializeField, Tooltip("BuildingData에 스프라이트가 없을 때 쓸 흰 사각형.")]
        private Sprite placeholderSprite;
        [SerializeField, Tooltip("플레이스홀더 박스 크기(유닛).")]
        private Vector2 placeholderSize = new Vector2(1.4f, 1.4f);

        [Header("터치 젤리")]
        [SerializeField] private Vector2 squashScale = new Vector2(1.15f, 0.85f);
        [SerializeField] private Vector2 stretchScale = new Vector2(0.95f, 1.05f);
        [SerializeField, Min(0.01f)] private float squashDuration = 0.1f;
        [SerializeField, Min(0.01f)] private float stretchDuration = 0.1f;
        [SerializeField, Min(0.01f)] private float settleDuration = 0.1f;

        [Header("숨쉬기")]
        [SerializeField, Min(1f)] private float breathScale = 1.03f;
        [SerializeField, Min(0.01f), Tooltip("1.0 → 최대 → 1.0 한 번에 걸리는 시간(초).")]
        private float breathCycle = 2f;

        [Header("정렬")]
        [SerializeField, Tooltip("1 유닛당 Sorting Order 차이. 캐릭터(Wanderer)와 같은 값을 쓴다.")]
        private float ordersPerUnit = 100f;
        [SerializeField, Tooltip("이름표를 건물보다 이만큼 앞에 그린다.")]
        private int labelOrderOffset = 1;

        /// <summary>건물이 눌렸을 때. 열 패널은 받는 쪽이 정한다.</summary>
        public event Action<Building> Clicked;

        public BuildingData Data { get; private set; }

        /// <summary>false면 눌러도 반응하지 않는다(패널이 열려 있는 동안 등).</summary>
        public bool Interactable { get; set; } = true;

        private BoxCollider2D _collider;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
        }

        private void Start()
        {
            transform.DOScale(breathScale, breathCycle * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(UnityEngine.Random.Range(0f, breathCycle));
        }

        private void OnDestroy()
        {
            transform.DOKill();
            jelly.DOKill();
        }

        /// <summary>데이터대로 모양·이름·정렬을 맞춘다. 스프라이트가 없으면 색상 박스로 표시한다.</summary>
        public void Setup(BuildingData data)
        {
            Data = data;
            name = data.name;

            bool hasArt = data.Sprite != null;
            Sprite sprite = hasArt ? data.Sprite : placeholderSprite;
            visual.sprite = sprite;
            visual.color = hasArt ? Color.white : data.Color;

            Vector2 spriteSize = sprite.bounds.size;
            Vector2 size = hasArt ? spriteSize : placeholderSize;
            visual.transform.localScale = hasArt
                ? Vector3.one
                : new Vector3(placeholderSize.x / spriteSize.x, placeholderSize.y / spriteSize.y, 1f);
            visual.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);

            _collider.size = size;
            _collider.offset = new Vector2(0f, size.y * 0.5f);

            nameLabel.text = data.DisplayName;

            int order = YSorting.OrderFor(transform.position.y, ordersPerUnit);
            visual.sortingOrder = order;
            if (nameSign != null)
            {
                nameSign.sortingOrder = order + labelOrderOffset;
            }
            nameLabel.sortingOrder = order + labelOrderOffset + 1;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable)
            {
                Clicked?.Invoke(this);
            }
        }

        /// <summary>젤리 연출을 재생하고 끝나면 <paramref name="onComplete"/>를 부른다.</summary>
        public void PlayPressed(Action onComplete)
        {
            jelly.DOKill();
            jelly.localScale = Vector3.one;
            DOTween.Sequence()
                .Append(jelly.DOScale(new Vector3(squashScale.x, squashScale.y, 1f), squashDuration).SetEase(Ease.OutQuad))
                .Append(jelly.DOScale(new Vector3(stretchScale.x, stretchScale.y, 1f), stretchDuration).SetEase(Ease.InOutQuad))
                .Append(jelly.DOScale(Vector3.one, settleDuration).SetEase(Ease.OutQuad))
                .SetTarget(jelly)
                .OnComplete(() => onComplete?.Invoke());
        }
    }
}
