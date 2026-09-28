using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 지금 들고 있는 아이템 목록. 가방 버튼을 누르면 열린다.
    ///
    /// 유물은 이름과 설명만 보이고, 일회성은 개수와 <b>사용</b> 버튼이 붙는다.
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

        [Header("화면")]
        [SerializeField, Tooltip("목록 화면 전체. 평소에는 꺼져 있어야 한다.")]
        private GameObject panel;

        [SerializeField, Tooltip("목록을 여는 가방 버튼.")]
        private Button openButton;

        [SerializeField, Tooltip("닫는 버튼.")]
        private Button closeButton;

        [SerializeField, Tooltip("아이템이 하나도 없을 때 띄울 안내. 비우면 표시하지 않는다.")]
        private Text emptyLabel;

        [SerializeField, Tooltip("목록 줄들. 가진 아이템이 이보다 많으면 앞에서부터만 보인다.")]
        private Row[] rows = new Row[0];

        [Header("문구")]
        [SerializeField] private string emptyText = "아직 가진 아이템이 없습니다";
        [SerializeField] private string useText = "사용";
        [SerializeField] private string countFormat = "{0}  x{1}";

        private bool _wired;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            if (openButton != null) { openButton.onClick.AddListener(Open); }
            if (closeButton != null) { closeButton.onClick.AddListener(Close); }

            if (items != null) { items.Inventory.Changed += Refresh; }

            WireRows();
            Close();
        }

        private void OnDisable()
        {
            if (openButton != null) { openButton.onClick.RemoveListener(Open); }
            if (closeButton != null) { closeButton.onClick.RemoveListener(Close); }

            if (items != null) { items.Inventory.Changed -= Refresh; }
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
                if (rows[i].useButton == null)
                {
                    continue;
                }

                int index = i;
                rows[i].useButton.onClick.AddListener(delegate { UseRow(index); });
            }
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

            var owned = items.Inventory.Ids;

            if (emptyLabel != null)
            {
                bool empty = owned.Count == 0;
                if (emptyLabel.gameObject.activeSelf != empty)
                {
                    emptyLabel.gameObject.SetActive(empty);
                }

                emptyLabel.text = emptyText;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                bool used = i < owned.Count;

                if (rows[i].root != null && rows[i].root.activeSelf != used)
                {
                    rows[i].root.SetActive(used);
                }

                if (!used)
                {
                    continue;
                }

                ItemConfig.Entry entry;
                if (!items.Catalog.TryGet(owned[i], out entry))
                {
                    continue;
                }

                int count = items.Inventory.CountOf(entry.id);
                bool consumable = entry.kind == ItemConfig.Kind.Consumable;

                if (rows[i].nameLabel != null)
                {
                    rows[i].nameLabel.text = consumable
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
                    if (go.activeSelf != consumable) { go.SetActive(consumable); }
                }

                if (rows[i].useLabel != null)
                {
                    rows[i].useLabel.text = useText;
                }
            }
        }

        private void UseRow(int index)
        {
            if (items == null)
            {
                return;
            }

            var owned = items.Inventory.Ids;
            if (index < 0 || index >= owned.Count)
            {
                return;
            }

            if (items.Use(owned[index]))
            {
                Refresh();
            }
        }
    }
}
