using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 지금 들고 있는 아이템 목록.
    ///
    /// <b>여는 버튼은 없다.</b> 재료·조합 시스템이 들어오면서 인벤토리가 가방 버튼의 자리를
    /// 대신하기 때문에, 전용 버튼 대신 <see cref="Open"/>을 부르는 쪽이 여는 시점을 정한다.
    ///
    /// 유물은 이름과 설명만 보이고, 일회성은 개수와 <b>사용</b> 버튼이 붙는다.
    /// <b>폭탄</b>은 개수와 <b>설치</b> 버튼이 붙고, 누르면 보드에 설치 자리가 표시된다
    /// (전용 설치 버튼은 없어졌다).
    ///
    /// 칸의 위치·크기는 씬이 정하고, 여기서는 글자와 활성 상태만 만진다(Hard Rule 2).
    /// </summary>
    public sealed class InventoryView : MonoBehaviour
    {
        /// <summary>목록 한 줄.</summary>
        [System.Serializable]
        public struct Row
        {
            public GameObject root;
            public Text nameLabel;
            public Text descriptionLabel;
            public Button useButton;
            public Text useLabel;
        }

        [Header("연결")]
        [SerializeField] private ItemService items;

        [SerializeField, Tooltip("폭탄 칸을 눌렀을 때 설치 조준을 켤 컨트롤러. " +
                                 "비워 두면 폭탄 칸이 목록에 뜨지 않는다(설치할 방법이 없으므로).")]
        private BombPlacementController bombPlacement;

        [SerializeField, Tooltip("폭탄을 설치해 개수가 줄어드는 시점을 알려 줄 세션. " +
                                 "비우면 설치 직후 폭탄 개수가 바로 갱신되지 않는다.")]
        private GameSession session;

        [Header("화면")]
        [SerializeField, Tooltip("목록 화면 전체. 화면 아래쪽에 늘 떠 있는 바다.")]
        private GameObject panel;

        [SerializeField, Tooltip("아이템이 하나도 없을 때 띄울 안내. 비우면 표시하지 않는다.")]
        private Text emptyLabel;

        [SerializeField, Tooltip("목록 줄들. 가진 아이템이 이보다 많으면 앞에서부터만 보인다.")]
        private Row[] rows = new Row[0];

        [Header("문구")]
        [SerializeField] private string emptyText = "아직 가진 아이템이 없습니다";
        [SerializeField] private string useText = "사용";
        [SerializeField, Tooltip("폭탄 칸의 버튼 글자. 누르면 보드에 설치 자리가 뜬다.")]
        private string placeText = "설치";

        [SerializeField, Tooltip("설치 자리를 띄워 둔 동안의 폭탄 칸 버튼 글자.")]
        private string cancelPlaceText = "취소";

        [SerializeField] private string countFormat = "{0}  x{1}";

        private bool _wired;

        /// <summary>
        /// 지금 줄에 그릴 것들. <b>폭탄이 맨 앞</b>이고 그 뒤로 인벤토리가 가진 순서다.
        ///
        /// 폭탄만 따로 끼워 넣는 이유는 폭탄이 RunInventory가 아니라 Player.Bombs에
        /// 들어 있기 때문이다(ItemConfig.Effect.PlaceBomb 주석). 맨 앞에 고정하는 건
        /// 재료를 줍고 쓸 때마다 설치 버튼이 줄 사이를 옮겨 다니지 않게 하려는 것이다.
        /// </summary>
        private readonly List<string> _slots = new List<string>(8);

        public bool IsOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            if (items != null)
            {
                items.Inventory.Changed += Refresh;
                items.BombsChanged += Refresh;
            }

            // 폭탄은 설치할 때마다 줄어든다. 설치는 턴을 쓰는 행동이라 Stepped로 들어온다.
            if (session != null)
            {
                session.Stepped += HandleStepped;
                session.RunStarted += HandleRunStarted;
            }

            if (bombPlacement != null)
            {
                bombPlacement.ShowingChanged += HandlePlacementModeChanged;
            }

            WireRows();
        }

        private void OnDisable()
        {
            if (items != null)
            {
                items.Inventory.Changed -= Refresh;
                items.BombsChanged -= Refresh;
            }

            if (session != null)
            {
                session.Stepped -= HandleStepped;
                session.RunStarted -= HandleRunStarted;
            }

            if (bombPlacement != null)
            {
                bombPlacement.ShowingChanged -= HandlePlacementModeChanged;
            }
        }

        private void HandleStepped(StepResult result)
        {
            Refresh();
        }

        private void HandleRunStarted(GameLoop loop)
        {
            Refresh();
        }

        /// <summary>조준이 켜지고 꺼질 때 폭탄 칸의 글자를 "설치"/"취소"로 바꾼다.</summary>
        private void HandlePlacementModeChanged(bool showing)
        {
            Refresh();
        }

        /// <summary>
        /// 바를 띄우는 건 Start다. OnEnable에서 하면 ItemService의 카탈로그가 아직 안 올라와
        /// 첫 Refresh가 빈 채로 지나간다.
        /// </summary>
        private void Start()
        {
            Open();
        }

        /// <summary>사용 버튼을 한 번만 물린다. 어느 아이템인지는 누를 때 줄 번호로 되짚는다.</summary>
        private void WireRows()
        {
            if (_wired)
            {
                return;
            }

            _wired = true;

            for (int i = 0; i < rows.Length; i++)
            {
                // 칸마다 드래그앤드롭을 물린다. 씬에서 일일이 붙이지 않아도 되도록 여기서 단다.
                if (rows[i].root != null)
                {
                    var drag = rows[i].root.GetComponent<InventorySlotDrag>();
                    if (drag == null)
                    {
                        drag = rows[i].root.AddComponent<InventorySlotDrag>();
                    }

                    drag.Bind(this, i);

                    // 칸이 포인터를 못 받으면 끌어도 아무 일도 안 일어난다.
                    // 배경 이미지가 레이캐스트 대상이어야 칸 전체가 손가락에 걸린다.
                    var graphic = rows[i].root.GetComponent<Graphic>();
                    if (graphic != null)
                    {
                        graphic.raycastTarget = true;
                    }
                }

                if (rows[i].useButton == null)
                {
                    continue;
                }

                int index = i;
                rows[i].useButton.onClick.AddListener(delegate { UseRow(index); });
            }
        }

        /// <summary>그 칸을 끌 수 있는지. 재료가 놓인 칸만 끌 수 있다.</summary>
        public bool CanDrag(int index)
        {
            ItemConfig.Entry entry;
            return TryEntryAt(index, out entry) && entry.kind == ItemConfig.Kind.Material;
        }

        /// <summary>
        /// 칸 하나를 다른 칸에 떨궜다. 둘 다 재료면 조합을 시도한다.
        /// 조합이 안 되는 짝이면 아무 일도 일어나지 않는다(재료도 그대로다).
        /// </summary>
        public void TryCombine(int fromIndex, int toIndex)
        {
            ItemConfig.Entry from, to;
            if (items == null || !TryEntryAt(fromIndex, out from) || !TryEntryAt(toIndex, out to))
            {
                return;
            }

            if (from.kind != ItemConfig.Kind.Material || to.kind != ItemConfig.Kind.Material)
            {
                return;
            }

            if (items.TryCraft(from.id, to.id))
            {
                Refresh();
            }
        }

        /// <summary>그 칸에 지금 무엇이 놓여 있는지. 빈 칸이면 false.</summary>
        private bool TryEntryAt(int index, out ItemConfig.Entry entry)
        {
            entry = default(ItemConfig.Entry);

            if (items == null || items.Catalog == null || index < 0 || index >= _slots.Count)
            {
                return false;
            }

            return items.Catalog.TryGet(_slots[index], out entry);
        }

        /// <summary>
        /// 목록에 그릴 것들을 순서대로 모은다. 폭탄 칸이 맨 앞에 붙는다(<see cref="_slots"/>).
        ///
        /// 폭탄이 0개면 칸을 아예 내린다 — 설치할 수도 없는 줄이 자리를 차지하면
        /// 재료가 그만큼 덜 보인다. 보유 수는 HUD에도 계속 떠 있다.
        /// </summary>
        private void RebuildSlots()
        {
            _slots.Clear();

            if (items == null)
            {
                return;
            }

            string bombId = items.BombItemId;
            bool hasBombSlot = bombPlacement != null && !string.IsNullOrEmpty(bombId) && items.BombCount > 0;

            if (hasBombSlot)
            {
                _slots.Add(bombId);
            }

            var owned = items.Inventory.Ids;
            for (int i = 0; i < owned.Count; i++)
            {
                _slots.Add(owned[i]);
            }
        }

        /// <summary>그 아이템을 몇 개 들고 있는지. 폭탄만 인벤토리가 아닌 데서 세어 온다.</summary>
        private int CountOf(string id)
        {
            if (items == null)
            {
                return 0;
            }

            return items.IsBombItem(id) ? items.BombCount : items.Inventory.CountOf(id);
        }

        public void Open()
        {
            if (panel == null)
            {
                return;
            }

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

        private void Refresh()
        {
            if (items == null || items.Catalog == null)
            {
                return;
            }

            RebuildSlots();

            if (emptyLabel != null)
            {
                bool empty = _slots.Count == 0;
                if (emptyLabel.gameObject.activeSelf != empty)
                {
                    emptyLabel.gameObject.SetActive(empty);
                }

                emptyLabel.text = emptyText;
            }

            bool aiming = bombPlacement != null && bombPlacement.IsShowingPlacements;

            for (int i = 0; i < rows.Length; i++)
            {
                bool used = i < _slots.Count;

                if (rows[i].root != null && rows[i].root.activeSelf != used)
                {
                    rows[i].root.SetActive(used);
                }

                if (!used)
                {
                    continue;
                }

                ItemConfig.Entry entry;
                if (!items.Catalog.TryGet(_slots[i], out entry))
                {
                    continue;
                }

                int count = CountOf(entry.id);
                bool bomb = items.IsBombItem(entry.id);

                // 유물만 한 개뿐이다. 일회성·재료·폭탄은 쌓이므로 개수를 같이 보여 준다.
                bool stacks = entry.kind != ItemConfig.Kind.Relic;

                // 누를 수 있는 버튼은 일회성과 폭탄에만 붙는다. 재료는 조합으로만 쓰인다.
                bool pressable = entry.kind == ItemConfig.Kind.Consumable;

                if (rows[i].nameLabel != null)
                {
                    rows[i].nameLabel.text = stacks
                        ? string.Format(countFormat, entry.displayName, count)
                        : entry.displayName;
                }

                if (rows[i].descriptionLabel != null)
                {
                    rows[i].descriptionLabel.text = entry.description;
                }

                // 유물은 쓰는 게 아니라 계속 붙어 있는 것이라 버튼이 없다.
                if (rows[i].useButton != null)
                {
                    GameObject go = rows[i].useButton.gameObject;
                    if (go.activeSelf != pressable) { go.SetActive(pressable); }
                }

                if (rows[i].useLabel != null)
                {
                    // 폭탄은 누르는 즉시 쓰이지 않고 조준이 켜진다. 글자가 그걸 말해 줘야 한다.
                    rows[i].useLabel.text = bomb
                        ? (aiming ? cancelPlaceText : placeText)
                        : useText;
                }
            }
        }

        private void UseRow(int index)
        {
            if (items == null || index < 0 || index >= _slots.Count)
            {
                return;
            }

            string id = _slots[index];

            // 폭탄은 여기서 쓰이지 않는다 — 보드에서 놓을 칸을 고르는 단계가 먼저다.
            // 다시 누르면 조준이 꺼진다.
            if (items.IsBombItem(id))
            {
                if (bombPlacement != null)
                {
                    bombPlacement.Toggle();
                }

                return;
            }

            if (items.Use(id))
            {
                Refresh();
            }
        }
    }
}
