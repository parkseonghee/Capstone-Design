using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을의 쓰레기통. 판에서 들고 나온 재료를 넣으면 골드로 바뀐다
    /// (재료·조합 시스템 §6 "클리어 후 남은 인벤토리의 재료는 마을로 가져와서 재화로 추가").
    ///
    /// <b>넣지 않은 재료는 그대로 남아 다음 판에 따라 들어간다.</b> 그래서 이 화면은
    /// "쓸모없는 재료를 돈으로 바꾸는 곳"이지 "재료를 정리당하는 곳"이 아니다 —
    /// 조합에 쓸 것은 남기고 남는 것만 버리는 판단이 생긴다.
    ///
    /// 칸의 위치·크기는 씬이 정하고, 여기서는 글자와 활성 상태만 만진다(Hard Rule 2).
    /// </summary>
    public sealed class TrashBinView : MonoBehaviour
    {
        /// <summary>목록 한 줄.</summary>
        [System.Serializable]
        public struct Row
        {
            [Tooltip("줄 전체. 안 쓰는 줄은 이걸 꺼서 배경까지 숨긴다.")]
            public GameObject root;

            [Tooltip("재료 이름과 개수.")]
            public Text label;

            [Tooltip("한 개를 넣는 버튼.")]
            public Button dropButton;

            [Tooltip("버튼 안의 글자. 바뀌는 값(골드)이 들어간다.")]
            public Text dropLabel;
        }

        [Header("데이터")]
        [SerializeField, Tooltip("재료 이름과 값을 읽을 아이템 표.")]
        private ItemConfig catalog;

        [SerializeField, Tooltip("보관함과 골드를 읽고 쓸 곳.")]
        private RunProgress progress;

        [SerializeField, Range(0.05f, 1f), Tooltip("상점가 대비 되사는 비율. " +
                                                   "1이면 산 값 그대로 돌려받아 상점과 쓰레기통을 오가며 " +
                                                   "골드를 만들 수 있으니 1보다 낮게 둔다.")]
        private float refundRatio = 0.5f;

        [Header("화면")]
        [SerializeField, Tooltip("쓰레기통 화면 전체. 평소에는 꺼져 있어야 한다.")]
        private GameObject panel;

        [SerializeField, Tooltip("여는 버튼.")]
        private Button openButton;

        [SerializeField, Tooltip("닫는 버튼.")]
        private Button closeButton;

        [SerializeField, Tooltip("전부 넣는 버튼. 비워도 된다.")]
        private Button dropAllButton;

        [SerializeField, Tooltip("보유 골드 표시. 비우면 표시하지 않는다.")]
        private Text goldLabel;

        [SerializeField, Tooltip("보관함이 비었을 때의 안내. 비우면 표시하지 않는다.")]
        private Text emptyLabel;

        [SerializeField, Tooltip("재료 줄들. 종류가 이보다 많으면 앞에서부터만 보인다.")]
        private Row[] rows = new Row[0];

        [Header("문구")]
        [SerializeField, Tooltip("한 줄의 설명. {0}이 이름, {1}이 개수다.")]
        private string rowFormat = "{0}  x{1}";

        [SerializeField, Tooltip("넣기 버튼의 글자. {0}이 받을 골드다.")]
        private string dropFormat = "+{0} G";

        [SerializeField] private string goldFormat = "보유 골드 {0}";
        [SerializeField] private string emptyText = "가져온 재료가 없습니다";

        private bool _wired;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            if (openButton != null) { openButton.onClick.AddListener(Open); }
            if (closeButton != null) { closeButton.onClick.AddListener(Close); }
            if (dropAllButton != null) { dropAllButton.onClick.AddListener(DropAll); }
            if (progress != null) { progress.Changed += Refresh; }

            WireRows();
            Close();
        }

        private void OnDisable()
        {
            if (openButton != null) { openButton.onClick.RemoveListener(Open); }
            if (closeButton != null) { closeButton.onClick.RemoveListener(Close); }
            if (dropAllButton != null) { dropAllButton.onClick.RemoveListener(DropAll); }
            if (progress != null) { progress.Changed -= Refresh; }
        }

        /// <summary>넣기 버튼을 한 번만 물린다. 어느 재료인지는 누를 때 줄 번호로 되짚는다.</summary>
        private void WireRows()
        {
            if (_wired)
            {
                return;
            }

            _wired = true;

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].dropButton == null)
                {
                    continue;
                }

                int index = i;
                rows[i].dropButton.onClick.AddListener(delegate { DropOne(index); });
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

        /// <summary>그 재료 하나를 넣고 받을 골드. 값이 0인 재료도 최소 1골드는 준다.</summary>
        public int RefundOf(ItemConfig.Entry entry)
        {
            return Mathf.Max(1, Mathf.FloorToInt(entry.price * refundRatio));
        }

        private void Refresh()
        {
            if (goldLabel != null)
            {
                goldLabel.text = string.Format(goldFormat, progress != null ? progress.Gold : 0);
            }

            int shown = 0;

            if (progress != null && catalog != null)
            {
                var ids = progress.StashIds;
                for (int i = 0; i < ids.Count && shown < rows.Length; i++)
                {
                    ItemConfig.Entry entry;
                    if (!catalog.TryGet(ids[i], out entry))
                    {
                        continue;
                    }

                    FillRow(shown, ids[i], entry);
                    shown++;
                }
            }

            for (int i = shown; i < rows.Length; i++)
            {
                SetActive(rows[i].root, false);
            }

            if (emptyLabel != null)
            {
                emptyLabel.text = emptyText;
                SetActive(emptyLabel.gameObject, shown == 0);
            }

            if (dropAllButton != null)
            {
                dropAllButton.gameObject.SetActive(shown > 0);
            }
        }

        private void FillRow(int index, string id, ItemConfig.Entry entry)
        {
            SetActive(rows[index].root, true);

            if (rows[index].label != null)
            {
                rows[index].label.text = string.Format(rowFormat, entry.displayName, progress.StashCount(id));
            }

            if (rows[index].dropLabel != null)
            {
                rows[index].dropLabel.text = string.Format(dropFormat, RefundOf(entry));
            }
        }

        private void DropOne(int index)
        {
            string id;
            ItemConfig.Entry entry;
            if (!TryStashAt(index, out id, out entry) || progress == null)
            {
                return;
            }

            // 빼기부터 한다 — 못 빼면 골드를 주면 안 된다.
            if (!progress.RemoveFromStash(id, 1))
            {
                return;
            }

            progress.AddGold(RefundOf(entry));
            Refresh();
        }

        private void DropAll()
        {
            if (progress == null || catalog == null)
            {
                return;
            }

            // 넣는 동안 목록이 줄어드니, 지금 들어 있는 것을 먼저 베껴 두고 돈다.
            var ids = new System.Collections.Generic.List<string>(progress.StashIds);

            for (int i = 0; i < ids.Count; i++)
            {
                ItemConfig.Entry entry;
                if (!catalog.TryGet(ids[i], out entry))
                {
                    continue;
                }

                int count = progress.StashCount(ids[i]);
                if (count <= 0 || !progress.RemoveFromStash(ids[i], count))
                {
                    continue;
                }

                progress.AddGold(RefundOf(entry) * count);
            }

            Refresh();
        }

        /// <summary>화면의 index번째 줄이 가리키는 재료. Refresh와 같은 규칙으로 훑는다.</summary>
        private bool TryStashAt(int index, out string id, out ItemConfig.Entry entry)
        {
            id = null;
            entry = default(ItemConfig.Entry);

            if (progress == null || catalog == null || index < 0)
            {
                return false;
            }

            var ids = progress.StashIds;
            int shown = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                ItemConfig.Entry found;
                if (!catalog.TryGet(ids[i], out found))
                {
                    continue;
                }

                if (shown == index)
                {
                    id = ids[i];
                    entry = found;
                    return true;
                }

                shown++;
            }

            return false;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }
}
