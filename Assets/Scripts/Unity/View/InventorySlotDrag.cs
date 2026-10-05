using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 인벤토리 칸 하나의 드래그앤드롭. 칸을 끌어다 다른 칸에 떨구면 조합이 시도된다.
    ///
    /// 조합이 되는지 안 되는지는 <see cref="InventoryView"/>를 거쳐 ItemService가 정한다.
    /// 이 컴포넌트는 <b>어느 칸에서 어느 칸으로 떨궜는지</b>만 알려 준다(Hard Rule 5).
    ///
    /// 끄는 동안에는 칸을 복제하지 않고 <b>반투명하게 만들고 이름표만 손가락을 따라가게</b> 한다.
    /// 칸 전체를 복제하면 레이아웃 안에서 자리가 비어 다른 칸들이 들썩인다.
    /// </summary>
    public sealed class InventorySlotDrag : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private InventoryView _owner;
        private int _index = -1;
        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _ghost;

        /// <summary>어느 목록의 몇 번째 칸인지 꽂아 준다. InventoryView가 칸을 만들 때 부른다.</summary>
        public void Bind(InventoryView owner, int index)
        {
            _owner = owner;
            _index = index;
        }

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();

            // 끄는 동안 이 칸이 레이캐스트를 먹으면 아래에 있는 '떨굴 칸'을 못 찾는다.
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// 끄는 도중에 이 칸이 꺼지면 치울 사람이 없어진다.
        ///
        /// EventSystem은 <b>꺼진 컴포넌트에 이벤트를 보내지 않으므로</b> OnEndDrag가 영영 오지 않는데,
        /// 이름표(고스트)는 캔버스에 매달려 있어 칸과 같이 사라지지도 않는다. 그게 화면에
        /// "철 x1" 같은 네모가 남아 있던 이유다. 칸이 꺼지는 건 드물지 않다 — 조합이 성사되면
        /// 줄 수가 줄면서 InventoryView.Refresh가 맨 끝 줄을 끄는데, 그 줄에서 끌기 시작했으면
        /// 바로 이 경우다(그래서 "가끔" 남았다).
        /// </summary>
        private void OnDisable()
        {
            ClearDrag();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // 앞선 끌기가 어떻게든 뒤처리를 못 했으면 여기서라도 치우고 시작한다.
            ClearDrag();

            if (_owner == null || !_owner.CanDrag(_index))
            {
                return;
            }

            _group.alpha = 0.45f;
            _group.blocksRaycasts = false;

            _ghost = CreateGhost();
            MoveGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ClearDrag();
        }

        /// <summary>
        /// 끌기의 뒤처리. 이름표를 치우고 칸을 원래대로 돌려놓는다.
        ///
        /// 끝났을 때·칸이 꺼질 때 양쪽에서 부르므로 <b>여러 번 불려도 안전해야 한다</b>.
        /// 반투명(alpha)까지 되돌리는 이유는, 이것도 안 돌리면 그 칸이 다시 켜졌을 때
        /// 혼자 흐릿하게 남기 때문이다.
        /// </summary>
        private void ClearDrag()
        {
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = true;
            }

            if (_ghost != null)
            {
                Destroy(_ghost.gameObject);
                _ghost = null;
            }
        }

        /// <summary>다른 칸을 여기에 떨궜다.</summary>
        public void OnDrop(PointerEventData eventData)
        {
            if (_owner == null || eventData.pointerDrag == null)
            {
                return;
            }

            var from = eventData.pointerDrag.GetComponent<InventorySlotDrag>();
            if (from == null || from == this)
            {
                return;
            }

            _owner.TryCombine(from._index, _index);
        }

        /// <summary>손가락을 따라다닐 이름표. 원본 칸은 자리에 그대로 남는다.</summary>
        private RectTransform CreateGhost()
        {
            if (_canvas == null)
            {
                return null;
            }

            var go = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_canvas.transform, false);
            rt.SetAsLastSibling();
            rt.sizeDelta = ((RectTransform)transform).rect.size * 0.8f;

            // 떨굴 칸을 가리면 안 되니 레이캐스트를 전부 끈다.
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;
            go.GetComponent<CanvasGroup>().alpha = 0.9f;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.98f, 0.82f, 0.35f, 0.35f);
            image.raycastTarget = false;

            // 원본 칸의 이름표를 그대로 베껴 무엇을 끌고 있는지 보이게 한다.
            Text source = GetComponentInChildren<Text>();
            if (source != null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                var labelRt = (RectTransform)labelGo.transform;
                labelRt.SetParent(rt, false);
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                var label = labelGo.AddComponent<Text>();
                label.font = source.font;
                label.fontSize = source.fontSize;
                label.color = source.color;
                label.alignment = TextAnchor.MiddleCenter;
                label.raycastTarget = false;
                label.text = source.text;
            }

            return rt;
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (_ghost == null || _canvas == null)
            {
                return;
            }

            // Overlay 캔버스는 스크린 좌표가 곧 로컬 좌표 변환의 입력이다.
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_canvas.transform, eventData.position, cam, out local))
            {
                _ghost.localPosition = local;
            }
        }
    }
}
