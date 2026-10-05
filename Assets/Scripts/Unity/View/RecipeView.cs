using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 조합법 목록. 레시피 버튼을 누르면 열린다.
    ///
    /// <see cref="CraftConfig"/>를 그대로 읽어 "재료 + 재료 → 결과"로 풀어 준다.
    /// 이름은 <see cref="ItemConfig"/>에서 가져오므로, 조합법을 에셋에 한 줄 더하면
    /// 여기 코드는 그대로 둔 채 목록에 바로 나타난다(Hard Rule 1·3).
    ///
    /// 칸의 위치·크기는 씬이 정하고, 여기서는 글자와 활성 상태만 만진다(Hard Rule 2).
    /// </summary>
    public sealed class RecipeView : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField, Tooltip("보여 줄 조합법 표.")]
        private CraftConfig recipes;

        [SerializeField, Tooltip("id를 이름으로 바꿀 아이템 표.")]
        private ItemConfig catalog;

        [SerializeField, Tooltip("어떤 조합법을 배웠는지 읽을 곳. 비워 두면 전부 아는 것으로 본다.")]
        private RunProgress progress;

        [Header("화면")]
        [SerializeField, Tooltip("목록 화면 전체. 평소에는 꺼져 있어야 한다.")]
        private GameObject panel;

        [SerializeField, Tooltip("목록을 여는 레시피 버튼.")]
        private Button openButton;

        [SerializeField, Tooltip("닫는 버튼.")]
        private Button closeButton;

        [SerializeField, Tooltip("목록이 떠 있는 동안 조작을 막을 라우터. 비워 두면 같은 오브젝트에서 찾는다.")]
        private InputRouter input;

        [SerializeField, Tooltip("조합법이 하나도 없을 때 띄울 안내. 비우면 표시하지 않는다.")]
        private Text emptyLabel;

        /// <summary>목록 한 줄. 배경까지 같이 숨겨야 해서 뿌리도 같이 들고 있는다.</summary>
        [System.Serializable]
        public struct Row
        {
            [Tooltip("줄 전체. 안 쓰는 줄은 이걸 꺼서 배경까지 숨긴다.")]
            public GameObject root;

            [Tooltip("글자가 들어갈 칸.")]
            public Text label;
        }

        [SerializeField, Tooltip("한 줄씩 쓸 칸들. 조합법이 이보다 많으면 앞에서부터만 보인다.")]
        private Row[] rows = new Row[0];

        [Header("문구")]
        [SerializeField, Tooltip("한 줄의 형식. {0}·{1}이 재료, {2}가 결과다.")]
        private string rowFormat = "{0}  +  {1}   →   {2}";

        [SerializeField, Tooltip("아직 안 배운 조합법에서 재료 자리에 들어갈 글자.")]
        private string unknownInput = "???";

        [SerializeField]
        private string emptyText = "아직 알려진 조합법이 없습니다";

        public bool IsOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            // 기본 배치는 GameSession과 같은 오브젝트다. 안 꽂혀 있으면 거기서 집어 온다.
            if (input == null)
            {
                input = GetComponent<InputRouter>();
            }

            if (openButton != null) { openButton.onClick.AddListener(Open); }
            if (closeButton != null) { closeButton.onClick.AddListener(Close); }

            Close();
        }

        private void OnDisable()
        {
            if (openButton != null) { openButton.onClick.RemoveListener(Open); }
            if (closeButton != null) { closeButton.onClick.RemoveListener(Close); }

            // 꺼지면서 조작을 잠근 채로 두면 판이 멈춘다.
            SetInputBlocked(false);
        }

        /// <summary>
        /// 목록이 떠 있는 동안 방향 입력을 잠근다.
        ///
        /// UI가 화면을 덮고 있으니 레이캐스트로도 막히긴 하지만, 그건 터치에만 통한다 —
        /// 키보드로는 그대로 움직여지고, 새 입력 시스템의 IsPointerOverGameObject는
        /// 터치에서도 믿을 게 못 된다. 그래서 라우터를 직접 잠근다.
        /// </summary>
        private void SetInputBlocked(bool blocked)
        {
            if (input != null)
            {
                input.SetInputBlocked(this, blocked);
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
            SetInputBlocked(true);
        }

        public void Close()
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }

            SetInputBlocked(false);
        }

        private void Refresh()
        {
            int shown = 0;

            if (recipes != null && recipes.Recipes != null)
            {
                var list = recipes.Recipes;
                for (int i = 0; i < list.Length && shown < rows.Length; i++)
                {
                    string line = Describe(list[i]);
                    if (line == null)
                    {
                        continue;   // 이름을 못 찾은 줄은 건너뛴다 — 빈 칸으로 보이느니 없는 게 낫다
                    }

                    if (rows[shown].label != null)
                    {
                        rows[shown].label.text = line;
                    }

                    SetActive(rows[shown].root, true);
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

        /// <summary>
        /// 한 줄을 "나무 + 철 → 튼튼한 몸"으로 푼다. id를 못 찾으면 null.
        ///
        /// <b>아직 안 배운 조합법은 재료를 가린다</b>("??? + ??? → 튼튼한 몸").
        /// 결과까지 숨기지 않는 건, 마을 상점에서 무엇을 사는지는 보여야 하고
        /// 기획이 가리라고 한 것도 "어떤 재료로 만드는가"이기 때문이다.
        /// </summary>
        private string Describe(CraftConfig.Recipe recipe)
        {
            string a = NameOf(recipe.inputA);
            string b = NameOf(recipe.inputB);
            string result = NameOf(recipe.result);

            if (a == null || b == null || result == null)
            {
                Debug.LogWarning(
                    $"{nameof(RecipeView)}: 조합법 '{recipe.inputA}+{recipe.inputB}={recipe.result}'에 " +
                    "ItemConfig에 없는 id가 있어 건너뜁니다.", this);
                return null;
            }

            if (!IsKnown(recipe))
            {
                a = unknownInput;
                b = unknownInput;
            }

            return string.Format(rowFormat, a, b, result);
        }

        /// <summary>그 조합법을 알고 있는지. 기록할 곳이 없으면 전부 아는 것으로 본다.</summary>
        private bool IsKnown(CraftConfig.Recipe recipe)
        {
            if (recipe.knownFromStart)
            {
                return true;
            }

            return progress == null || progress.IsRecipeKnown(recipe.id);
        }

        private string NameOf(string id)
        {
            ItemConfig.Entry entry;
            if (catalog == null || !catalog.TryGet(id, out entry))
            {
                return null;
            }

            return string.IsNullOrEmpty(entry.displayName) ? id : entry.displayName;
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
