using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 조합대 칸 하나. 인벤토리 칸을 여기에 떨구면 재료가 올라가고, 누르면 빠진다.
    /// 무엇을 받을지·뺄지는 <see cref="CraftingStationView"/>가 정한다(Hard Rule 5).
    /// </summary>
    public sealed class CraftingStationSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        private CraftingStationView _station;
        private int _index;

        public void Bind(CraftingStationView station, int index)
        {
            _station = station;
            _index = index;

            // 떨굼·누름을 받으려면 레이캐스트에 걸려야 한다.
            var graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_station == null || eventData == null || eventData.pointerDrag == null)
            {
                return;
            }

            _station.AcceptDrop(eventData.pointerDrag.GetComponent<InventorySlotDrag>());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 끌다가 놓은 것도 클릭으로 들어온다. 끌기 중의 놓기는 빼기가 아니다.
            if (_station == null || (eventData != null && eventData.dragging))
            {
                return;
            }

            _station.RemoveAt(_index);
        }
    }
}
