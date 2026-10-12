using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 판 안의 쓰레기통. 인벤토리 칸을 끌어다 여기에 떨구면 그 아이템을 <b>하나</b> 버린다.
    ///
    /// 마을의 <see cref="TrashBinView"/>(보관함 재료를 골드로 바꾸는 화면)와는 다른 물건이다 —
    /// 이쪽은 돈을 돌려주지 않고, 칸을 비우는 게 목적이다. 유물을 버리면 효과도 같이 빠진다
    /// (ItemService.Discard).
    ///
    /// 무엇을 버릴지·버릴 수 있는지는 InventoryView와 ItemService가 정한다.
    /// 여기는 "어느 칸이 떨어졌는지"만 넘기고, 끄는 중일 때 통을 밝혀 준다(Hard Rule 5).
    /// 버튼의 위치·크기는 씬이 정한다(Hard Rule 2).
    /// </summary>
    public sealed class InventoryTrashBin : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Tooltip("통을 칠할 배경. 끄는 칸이 위에 올라오면 색이 바뀐다.")]
        private Graphic background;

        [SerializeField] private Color normalColor = new Color(0.45f, 0.47f, 0.5f, 1f);

        [SerializeField, Tooltip("칸을 끌어 통 위에 올렸을 때의 색.")]
        private Color hoverColor = new Color(0.85f, 0.32f, 0.3f, 1f);

        [SerializeField, Tooltip("버렸을 때 통이 톡 튀는 세기. 0이면 효과 없음.")]
        private float punchStrength = 0.15f;

        [SerializeField, Min(0.01f)] private float punchDuration = 0.2f;

        [Header("유물 확인")]
        [SerializeField, Tooltip("유물을 버리기 전에 띄울 확인 창. 비우면 묻지 않고 바로 버린다.")]
        private ConfirmDialog confirmDialog;

        [SerializeField, Tooltip("확인 창 문구. {0}이 유물 이름이다.")]
        private string relicConfirmFormat = "'{0}'을(를) 버릴까요?\n효과도 함께 사라집니다.";

        private void Awake()
        {
            if (background == null)
            {
                background = GetComponent<Graphic>();
            }

            // 떨굼을 받으려면 레이캐스트에 걸려야 한다.
            if (background != null)
            {
                background.raycastTarget = true;
            }
        }

        private void OnEnable()
        {
            Paint(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Paint(IsDraggingSlot(eventData));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Paint(false);
        }

        public void OnDrop(PointerEventData eventData)
        {
            Paint(false);

            InventorySlotDrag slot = DraggedSlot(eventData);
            if (slot == null || slot.Owner == null)
            {
                return;
            }

            ItemConfig.Entry entry;
            if (!slot.Owner.CanDrag(slot.Index) || !slot.Owner.TryGetItemAt(slot.Index, out entry))
            {
                return;
            }

            // 유물은 하나뿐이고 효과까지 빠지니 한 번 묻는다. 재료·일회성은 바로 버린다.
            InventoryView owner = slot.Owner;
            string id = entry.id;

            if (entry.kind == ItemConfig.Kind.Relic && confirmDialog != null)
            {
                confirmDialog.Show(string.Format(relicConfirmFormat, entry.displayName), () => Discard(owner, id));
                return;
            }

            Discard(owner, id);
        }

        private void Discard(InventoryView owner, string id)
        {
            if (owner == null || !owner.TryDiscardItem(id) || punchStrength <= 0f)
            {
                return;
            }

            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * punchStrength, punchDuration, 6, 0.5f);
        }

        /// <summary>끄는 게 버릴 수 있는 인벤토리 칸일 때만 밝힌다. 폭탄 칸은 아예 끌리지 않는다.</summary>
        private static bool IsDraggingSlot(PointerEventData eventData)
        {
            InventorySlotDrag slot = DraggedSlot(eventData);
            return slot != null && slot.Owner != null && slot.Owner.CanDrag(slot.Index);
        }

        private static InventorySlotDrag DraggedSlot(PointerEventData eventData)
        {
            return eventData != null && eventData.dragging && eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<InventorySlotDrag>()
                : null;
        }

        private void Paint(bool hover)
        {
            if (background != null)
            {
                background.color = hover ? hoverColor : normalColor;
            }
        }
    }
}
