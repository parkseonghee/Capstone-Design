using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>클리어 영수증에 찍을 한 스테이지 정산.</summary>
    public struct StageReceiptData
    {
        /// <summary>도장에 찍힐 스테이지 이름('1', '1-2' 등).</summary>
        public string StageLabel;

        public int EnemiesKilled;

        /// <summary>획득 재료. 줄 수는 데이터마다 다르다.</summary>
        public List<Material> Materials;

        public float ClearSeconds;

        public int EarnedGold;

        /// <summary>적립 전 보유 골드. 보유 골드는 여기서 (이전 + 획득)까지 카운트업한다.</summary>
        public int PreviousGold;

        public int TotalGold => PreviousGold + EarnedGold;

        public struct Material
        {
            public string Id;
            public string DisplayName;
            public int Count;
        }
    }

    /// <summary>
    /// 클리어 영수증에 <b>글자와 줄만</b> 채운다. 움직임은 <see cref="ReceiptRevealAnimator"/>가 맡는다.
    ///
    /// 재료 줄은 템플릿(꺼 둔 ItemRow)을 복제해서 만든다. 줄이 늘면 영수증이 레이아웃 그룹을
    /// 따라 아래로 길어진다 — 위치·크기는 씬의 레이아웃이 정한다(Hard Rule 2).
    /// </summary>
    public sealed class StageReceiptView : MonoBehaviour
    {
        /// <summary>연출이 하나씩 찍어 낼 한 줄. 아이콘이 있는 줄(재료)은 아이콘이 팝된다.</summary>
        public struct Line
        {
            public RectTransform Rect;
            public RectTransform Icon;
        }

        [Header("도장")]
        [SerializeField] private Text stampStageLabel;
        [SerializeField] private string stampStageFormat = "STAGE {0}";

        [Header("정산 줄")]
        [SerializeField] private RectTransform killsLine;
        [SerializeField] private Text killsValue;

        [SerializeField, Tooltip("'획득 재료' 머리줄. 재료가 없으면 같이 숨긴다.")]
        private RectTransform materialsHeader;

        [SerializeField, Tooltip("ItemRow를 쌓을 부모(VerticalLayoutGroup).")]
        private RectTransform itemList;

        [SerializeField, Tooltip("꺼 둔 ItemRow 템플릿. 자식 'ItemIcon/Icon'(Image), 'ItemName'(Text), 'ItemQuantity'(Text).")]
        private RectTransform itemRowTemplate;

        [SerializeField] private RectTransform timeLine;
        [SerializeField] private Text timeValue;

        [Header("골드")]
        [SerializeField] private Text earnedGoldValue;
        [SerializeField] private Text ownedGoldValue;
        [SerializeField] private string ownedGoldFormat = "{0} G";

        [Header("아이콘")]
        [SerializeField, Tooltip("재료 아이콘 표를 빌려 올 곳. 비우면 아이콘 칸만 남는다.")]
        private ItemFlyEffect iconSource;

        [Header("문구")]
        [SerializeField] private string quantityFormat = "×{0}";

        private readonly List<RectTransform> _rows = new List<RectTransform>();

        public RectTransform EarnedGoldRect => earnedGoldValue != null ? earnedGoldValue.rectTransform : null;

        /// <summary>데이터를 채우고, 찍히는 순서(위→아래)대로 줄 목록을 돌려준다.</summary>
        public List<Line> Bind(StageReceiptData data)
        {
            var lines = new List<Line>();

            if (stampStageLabel != null)
            {
                stampStageLabel.text = string.Format(stampStageFormat, data.StageLabel);
            }

            if (killsValue != null)
            {
                killsValue.text = data.EnemiesKilled.ToString();
            }

            AddLine(lines, killsLine, null);

            int count = data.Materials != null ? data.Materials.Count : 0;
            EnsureRows(count);

            if (materialsHeader != null)
            {
                materialsHeader.gameObject.SetActive(count > 0);
                if (count > 0)
                {
                    AddLine(lines, materialsHeader, null);
                }
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < count;
                _rows[i].gameObject.SetActive(used);
                if (used)
                {
                    AddLine(lines, _rows[i], FillRow(_rows[i], data.Materials[i]));
                }
            }

            if (timeValue != null)
            {
                timeValue.text = FormatTime(data.ClearSeconds);
            }

            AddLine(lines, timeLine, null);

            SetGold(data.EarnedGold, data.TotalGold);
            return lines;
        }

        /// <summary>골드 두 줄을 그 값으로 쓴다. 카운트업이 매 프레임 부른다.</summary>
        public void SetGold(int earned, int owned)
        {
            if (earnedGoldValue != null)
            {
                earnedGoldValue.text = earned.ToString();
            }

            if (ownedGoldValue != null)
            {
                ownedGoldValue.text = string.Format(ownedGoldFormat, owned);
            }
        }

        private static void AddLine(List<Line> lines, RectTransform rect, RectTransform icon)
        {
            if (rect != null)
            {
                lines.Add(new Line { Rect = rect, Icon = icon });
            }
        }

        private void EnsureRows(int count)
        {
            if (itemRowTemplate == null || itemList == null)
            {
                return;
            }

            itemRowTemplate.gameObject.SetActive(false);

            while (_rows.Count < count)
            {
                var row = Instantiate(itemRowTemplate, itemList);
                row.name = "ItemRow_" + _rows.Count;
                _rows.Add(row);
            }
        }

        private RectTransform FillRow(RectTransform row, StageReceiptData.Material material)
        {
            var name = row.Find("ItemName");
            if (name != null)
            {
                name.GetComponent<Text>().text = material.DisplayName;
            }

            var quantity = row.Find("ItemQuantity");
            if (quantity != null)
            {
                quantity.GetComponent<Text>().text = string.Format(quantityFormat, material.Count);
            }

            var icon = row.Find("ItemIcon/Icon") as RectTransform;
            if (icon != null)
            {
                var image = icon.GetComponent<Image>();
                if (iconSource != null)
                {
                    Sprite sprite;
                    Color color;
                    iconSource.ResolveIcon(material.Id, out sprite, out color);
                    image.sprite = sprite;
                    image.color = color;
                }

                image.enabled = image.sprite != null;
            }

            return row.Find("ItemIcon") as RectTransform;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return string.Format("{0:00}:{1:00}", total / 60, total % 60);
        }
    }
}
