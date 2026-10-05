using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을의 조합법 상점. 모아 둔 골드로 조합법을 사면 그 뒤로 재료가 보인다
    /// (재료·조합 시스템 §5 "마을에서 유물 조합법을 사게 하여 골드 소비처 추가").
    ///
    /// 사는 것은 <b>아이템이 아니라 지식</b>이다. 산 기록은 <see cref="RunProgress"/>에
    /// 남아 런을 넘어 살아남는다 — 스테이지 상점에서 재료를 사는 것과 성격이 다르다.
    ///
    /// 칸의 위치·크기는 씬이 정하고, 여기서는 글자와 활성 상태만 만진다(Hard Rule 2).
    /// </summary>
    public sealed class RecipeShopView : MonoBehaviour
    {
        /// <summary>목록 한 줄.</summary>
        [System.Serializable]
        public struct Row
        {
            [Tooltip("줄 전체. 안 쓰는 줄은 이걸 꺼서 배경까지 숨긴다.")]
            public GameObject root;

            [Tooltip("무엇을 만드는 조합법인지.")]
            public Text label;

            [Tooltip("사는 버튼. 이미 샀거나 돈이 모자라면 꺼진다.")]
            public Button buyButton;

            [Tooltip("버튼 안의 글자. 값 또는 '보유 중'이 뜬다.")]
            public Text buyLabel;
        }

        [Header("데이터")]
        [SerializeField, Tooltip("팔 조합법 표.")]
        private CraftConfig recipes;

        [SerializeField, Tooltip("id를 이름으로 바꿀 아이템 표.")]
        private ItemConfig catalog;

        [SerializeField, Tooltip("골드를 깎고 산 기록을 남길 곳.")]
        private RunProgress progress;

        [Header("화면")]
        [SerializeField, Tooltip("상점 화면 전체. 평소에는 꺼져 있어야 한다.")]
        private GameObject panel;

        [SerializeField, Tooltip("상점을 여는 버튼.")]
        private Button openButton;

        [SerializeField, Tooltip("닫는 버튼.")]
        private Button closeButton;

        [SerializeField, Tooltip("보유 골드 표시. 비우면 표시하지 않는다.")]
        private Text goldLabel;

        [SerializeField, Tooltip("팔 것이 하나도 없을 때의 안내. 비우면 표시하지 않는다.")]
        private Text emptyLabel;

        [SerializeField, Tooltip("조합법 줄들. 조합법이 이보다 많으면 앞에서부터만 보인다.")]
        private Row[] rows = new Row[0];

        [Header("문구")]
        [SerializeField, Tooltip("한 줄의 설명. {0}이 결과 아이템 이름이다.")]
        private string rowFormat = "{0} 조합법";

        [SerializeField, Tooltip("살 수 있을 때의 버튼 글자. {0}이 값이다.")]
        private string priceFormat = "{0} G";

        [SerializeField] private string ownedText = "보유 중";
        [SerializeField] private string goldFormat = "보유 골드 {0}";
        [SerializeField] private string emptyText = "살 수 있는 조합법이 없습니다";

        private bool _wired;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            if (openButton != null) { openButton.onClick.AddListener(Open); }
            if (closeButton != null) { closeButton.onClick.AddListener(Close); }
            if (progress != null) { progress.Changed += Refresh; }

            WireRows();
            Close();
        }

        private void OnDisable()
        {
            if (openButton != null) { openButton.onClick.RemoveListener(Open); }
            if (closeButton != null) { closeButton.onClick.RemoveListener(Close); }
            if (progress != null) { progress.Changed -= Refresh; }
        }

        /// <summary>사는 버튼을 한 번만 물린다. 어느 조합법인지는 누를 때 줄 번호로 되짚는다.</summary>
        private void WireRows()
        {
            if (_wired)
            {
                return;
            }

            _wired = true;

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].buyButton == null)
                {
                    continue;
                }

                int index = i;
                rows[i].buyButton.onClick.AddListener(delegate { BuyRow(index); });
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
            if (goldLabel != null)
            {
                goldLabel.text = string.Format(goldFormat, progress != null ? progress.Gold : 0);
            }

            int shown = 0;

            if (recipes != null && recipes.Recipes != null && catalog != null)
            {
                var list = recipes.Recipes;
                for (int i = 0; i < list.Length && shown < rows.Length; i++)
                {
                    // 처음부터 아는 조합법은 팔 것이 없다.
                    if (list[i].knownFromStart)
                    {
                        continue;
                    }

                    ItemConfig.Entry result;
                    if (!catalog.TryGet(list[i].result, out result))
                    {
                        Debug.LogWarning(
                            $"{nameof(RecipeShopView)}: 조합법 '{list[i].id}'의 결과 " +
                            $"'{list[i].result}'가 ItemConfig에 없어 건너뜁니다.", this);
                        continue;
                    }

                    FillRow(shown, list[i], result);
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
        }

        private void FillRow(int index, CraftConfig.Recipe recipe, ItemConfig.Entry result)
        {
            SetActive(rows[index].root, true);

            if (rows[index].label != null)
            {
                rows[index].label.text = string.Format(rowFormat, result.displayName);
            }

            bool owned = progress != null && progress.IsRecipeKnown(recipe.id);
            bool affordable = progress != null && progress.Gold >= recipe.price;

            if (rows[index].buyLabel != null)
            {
                rows[index].buyLabel.text = owned ? ownedText : string.Format(priceFormat, recipe.price);
            }

            if (rows[index].buyButton != null)
            {
                // 이미 샀거나 돈이 모자라면 누를 수 없다. 숨기지 않는 건 값을 보고
                // "얼마를 더 모아야 하는지" 알 수 있어야 하기 때문이다.
                rows[index].buyButton.interactable = !owned && affordable;
            }
        }

        private void BuyRow(int index)
        {
            CraftConfig.Recipe recipe;
            if (!TryRecipeAt(index, out recipe) || progress == null)
            {
                return;
            }

            if (progress.IsRecipeKnown(recipe.id))
            {
                return;
            }

            if (!progress.SpendGold(recipe.price))
            {
                return;     // 골드 부족 — 아무 일도 일어나지 않는다
            }

            progress.LearnRecipe(recipe.id);

            // SpendGold·LearnRecipe가 Changed를 울려 Refresh가 이미 돌지만,
            // progress를 안 꽂은 구성에서도 목록이 갱신되게 한 번 더 부른다.
            Refresh();
        }

        /// <summary>화면의 index번째 줄이 가리키는 조합법. 목록을 Refresh와 같은 규칙으로 훑는다.</summary>
        private bool TryRecipeAt(int index, out CraftConfig.Recipe recipe)
        {
            recipe = default(CraftConfig.Recipe);

            if (recipes == null || recipes.Recipes == null || catalog == null || index < 0)
            {
                return false;
            }

            var list = recipes.Recipes;
            int shown = 0;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].knownFromStart)
                {
                    continue;
                }

                ItemConfig.Entry result;
                if (!catalog.TryGet(list[i].result, out result))
                {
                    continue;
                }

                if (shown == index)
                {
                    recipe = list[i];
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
