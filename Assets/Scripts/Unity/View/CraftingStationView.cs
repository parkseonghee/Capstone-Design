using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 조합대. 레시피 버튼으로 여는 창의 위쪽에 있다(아래쪽은 조합법 목록).
    ///
    ///   [재료][재료][재료][ ][ ]          ← 인벤토리 칸을 끌어다 넣는다. 칸을 누르면 뺀다.
    ///   [결과]  튼튼한 몸                   ← 레시피와 맞으면 결과와 BAR가 뜬다(모르는 조합은 ???)
    ///           최대 체력 +1
    ///           조합 가능  ████████   [조합]
    ///
    /// 재료 3~5개짜리(유물 등)를 만드는 곳이다. 2개짜리는 인벤토리에서 두 칸을 겹쳐도 되고 여기서 해도 된다.
    ///
    /// <b>올려 둔 재료는 인벤토리에서 빠지지 않는다.</b> 예약만 해 두고(<see cref="ReservedCount"/>),
    /// 인벤토리는 그만큼을 빼고 보여 준다. 그래서 창을 닫거나 칸을 눌러 빼면 따로 돌려줄 것 없이
    /// 예약만 지우면 되고, 조합에 성공했을 때만 ItemService가 실제로 꺼낸다.
    ///
    /// 레시피에 없는 조합은 실패 안내만 띄운다 — 문서의 "실패 시 재료 소멸"은 아직 넣지 않았다.
    /// </summary>
    public sealed class CraftingStationView : MonoBehaviour
    {
        /// <summary>조합대 칸 하나.</summary>
        [System.Serializable]
        public struct Slot
        {
            public CraftingStationSlot root;
            public Text label;
        }

        [Header("연결")]
        [SerializeField] private ItemService items;
        [SerializeField] private InventoryView inventory;

        [SerializeField, Tooltip("새 조합법을 배우면 목록을 다시 그릴 조합법 창. 비워도 된다.")]
        private RecipeView recipeList;

        [SerializeField, Tooltip("조합 가능할 때·완성될 때의 이펙트. 비워도 된다.")]
        private CombineEffects effects;

        [Header("칸")]
        [SerializeField, Tooltip("재료 칸들. 최대 5개(T3 유물).")]
        private Slot[] slots = new Slot[0];

        [Header("결과")]
        [SerializeField] private RectTransform resultTile;
        [SerializeField] private Text resultTileLabel;
        [SerializeField] private Text resultNameLabel;
        [SerializeField] private Text resultDescriptionLabel;
        [SerializeField] private Text statusLabel;

        [SerializeField, Tooltip("BAR. Image Type이 Filled여야 차오른다.")]
        private Image bar;

        [SerializeField] private Button craftButton;

        [Header("BAR")]
        [SerializeField, Min(0f)] private float barFillDuration = 0.15f;
        [SerializeField] private Color readyBarColor = new Color(0.18f, 0.74f, 0.70f, 1f);
        [SerializeField] private Color idleBarColor = new Color(0.4f, 0.4f, 0.45f, 1f);

        [Header("문구")]
        [SerializeField] private string emptySlotText = "";
        [SerializeField] private string idleText = "재료를 끌어다 넣으세요";
        [SerializeField] private string readyText = "조합 가능";
        [SerializeField] private string unknownComboText = "알 수 없는 조합";
        [SerializeField, Tooltip("{0}이 결과 이름이다.")] private string craftedFormat = "{0} 완성!";
        [SerializeField] private string failedText = "조합법이 없는 조합입니다";
        [SerializeField] private string unknownName = "???";
        [SerializeField] private string unknownDescription = "아직 모르는 조합입니다. 조합하면 알게 됩니다.";

        private readonly List<string> _contents = new List<string>(CraftConfig.MaxInputs);
        private bool _ready;
        private bool _barDrawn;

        public IReadOnlyList<string> Contents => _contents;

        private void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].root != null)
                {
                    slots[i].root.Bind(this, i);
                }
            }

            if (craftButton != null)
            {
                craftButton.onClick.AddListener(Craft);
            }
        }

        private void OnEnable()
        {
            Refresh(null);
        }

        /// <summary>창이 닫히면 올려 둔 재료를 전부 돌려준다(예약만 지우면 된다).</summary>
        private void OnDisable()
        {
            if (effects != null) { effects.EndCharging(); }
            if (bar != null) { bar.DOKill(); }
            _barDrawn = false;
            _ready = false;

            if (_contents.Count > 0)
            {
                _contents.Clear();
                RefreshInventory();
            }
        }

        /// <summary>그 재료를 조합대에 몇 개 올려 뒀는지. 인벤토리가 그만큼 빼고 보여 준다.</summary>
        public int ReservedCount(string id)
        {
            int n = 0;
            for (int i = 0; i < _contents.Count; i++)
            {
                if (_contents[i] == id) { n++; }
            }

            return n;
        }

        /// <summary>
        /// 인벤토리 칸이 조합대 칸에 떨어졌다. 재료만 받고, 남은 개수가 없으면 받지 않는다.
        /// 어느 칸에 떨어뜨렸든 앞에서부터 채운다 — 순서는 조합에 상관없다.
        /// </summary>
        public void AcceptDrop(InventorySlotDrag dragged)
        {
            if (dragged == null || dragged.Owner == null || _contents.Count >= slots.Length)
            {
                return;
            }

            ItemConfig.Entry entry;
            if (!dragged.Owner.TryGetItemAt(dragged.Index, out entry)
                || entry.kind != ItemConfig.Kind.Material
                || dragged.Owner.Available(entry.id) < 1)
            {
                return;
            }

            _contents.Add(entry.id);
            RefreshInventory();
            Refresh(null);
        }

        /// <summary>조합대 칸을 눌렀다. 그 재료를 인벤토리로 돌려보낸다.</summary>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _contents.Count)
            {
                return;
            }

            _contents.RemoveAt(index);
            RefreshInventory();
            Refresh(null);
        }

        private void Craft()
        {
            if (items == null || _contents.Count < 2)
            {
                return;
            }

            var ingredients = new List<string>(_contents);
            ItemConfig.Entry result;
            bool known;
            bool resolvable = items.TryPreviewCraft(ingredients, out result, out known);

            // 예약은 인벤토리 표시용일 뿐이라 ItemService는 가진 수 그대로 보고 꺼낸다.
            // 실패하면 조합대에 그대로 남는다 — 재료는 사라지지 않는다.
            if (!resolvable || !items.TryCraft(ingredients))
            {
                Refresh(failedText);
                return;
            }

            _contents.Clear();
            RefreshInventory();

            if (recipeList != null)
            {
                recipeList.RefreshList();
            }

            if (effects != null)
            {
                RectTransform slot;
                if (inventory != null && inventory.TryGetSlotRect(result.id, out slot) && slot.gameObject.activeInHierarchy)
                {
                    effects.PlayComplete(CombineEffects.ScreenCenterOf(slot));
                }
                else if (resultTile != null)
                {
                    effects.PlayComplete(CombineEffects.ScreenCenterOf(resultTile));
                }
            }

            Refresh(string.Format(craftedFormat, result.displayName));
        }

        /// <summary>칸과 결과를 다시 그린다. message가 있으면 안내 줄에 그것을 띄운다.</summary>
        private void Refresh(string message)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].label == null)
                {
                    continue;
                }

                slots[i].label.text = i < _contents.Count ? NameOf(_contents[i]) : emptySlotText;
            }

            ItemConfig.Entry result = default(ItemConfig.Entry);
            bool known = false;
            bool ready = items != null && _contents.Count >= 2 && items.TryPreviewCraft(_contents, out result, out known);

            if (ready)
            {
                SetText(resultTileLabel, known ? result.displayName : unknownName);
                SetText(resultNameLabel, known ? result.displayName : unknownName);
                SetText(resultDescriptionLabel, known ? result.description : unknownDescription);
                SetText(statusLabel, message ?? readyText);
            }
            else
            {
                SetText(resultTileLabel, "");
                SetText(resultNameLabel, "");
                SetText(resultDescriptionLabel, "");
                SetText(statusLabel, message ?? (_contents.Count >= 2 ? unknownComboText : idleText));
            }

            if (craftButton != null)
            {
                // 모르는 조합도 시도는 해 볼 수 있다(블라인드 조합). 두 개 이상이면 누를 수 있다.
                craftButton.interactable = _contents.Count >= 2;
            }

            UpdateBar(ready);
            UpdateCharging(ready);
            _ready = ready;
        }

        /// <summary>레시피와 맞는 순간 BAR가 차오르고, 안 맞으면 비운다.</summary>
        private void UpdateBar(bool ready)
        {
            if (bar == null)
            {
                return;
            }

            bar.color = ready ? readyBarColor : idleBarColor;

            // 이미 같은 상태면 다시 채우지 않는다 — 재료를 바꿀 때마다 BAR가 깜빡이면 거슬린다.
            if (ready == _ready && _barDrawn)
            {
                return;
            }

            _barDrawn = true;

            bar.DOKill();

            if (!ready)
            {
                bar.fillAmount = 0f;
                return;
            }

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

        /// <summary>조합 가능한 동안 결과 칸 위에서 번개가 계속 튄다(인벤토리 겹치기와 같은 이펙트).</summary>
        private void UpdateCharging(bool ready)
        {
            if (effects == null)
            {
                return;
            }

            if (ready && resultTile != null && isActiveAndEnabled)
            {
                effects.BeginCharging(resultTile);
            }
            else
            {
                effects.EndCharging();
            }
        }

        private void RefreshInventory()
        {
            if (inventory != null)
            {
                inventory.RefreshDisplay();
            }
        }

        private string NameOf(string id)
        {
            ItemConfig.Entry entry;
            return items != null && items.Catalog != null && items.Catalog.TryGet(id, out entry) ? entry.displayName : id;
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
