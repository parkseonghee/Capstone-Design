using System;
using System.Collections.Generic;
using RecycleLife.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 도감. [조합 도감] / [몬스터도감] 두 탭이고, 목록은 위아래로 스크롤해 본다.
    ///
    /// 배경은 탭 강조가 그려진 통짜 그림 두 장이다(dogam = 몬스터 탭 선택, dogam2 = 조합 탭 선택).
    /// 탭을 바꾸면 그림만 갈아 끼운다. 그림에 박힌 10줄은 씬에서 판으로 덮고,
    /// 그 위 스크롤 영역에 한 줄짜리 틀(<see cref="rowTemplate"/>)을 항목 수만큼 복제해 쌓는다.
    ///
    /// 내용은 전부 기존 표에서 읽는다 — 조합은 <see cref="CraftConfig"/>·<see cref="ItemConfig"/>,
    /// 몬스터 수치는 <see cref="TrashStatsConfig"/>, 그림은 <see cref="EntityVisualSet"/>.
    /// 몬스터 이름·서식지·설명만 표에 없어서 아래 <see cref="monsters"/>에 둔다.
    ///
    /// 아직 배우지 않은 조합법은 재료를 "???"로 가린다(조합법 상점과 같은 규칙).
    /// 마을에서 건물에 물리고 싶으면 <see cref="IVillageFeature"/>라 VillageManager에 그대로 연결된다.
    /// </summary>
    public sealed class DogamView : MonoBehaviour, IVillageFeature
    {
        public enum Tab
        {
            Craft,
            Monster,
        }

        /// <summary>몬스터 한 종류의 도감 글. 수치는 TrashStatsConfig에서 읽는다.</summary>
        [Serializable]
        public struct MonsterInfo
        {
            public TrashType type;
            public string displayName;

            [Tooltip("나오는 곳.")]
            public string habitat;

            [Tooltip("특징 한 마디. 수치 앞에 붙는다.")]
            public string trait;

            [TextArea]
            public string description;
        }

        [Header("데이터")]
        [SerializeField] private CraftConfig recipes;
        [SerializeField] private ItemConfig catalog;
        [SerializeField] private ItemIconSet itemIcons;
        [SerializeField] private TrashStatsConfig trashStats;
        [SerializeField] private EntityVisualSet visuals;

        [SerializeField, Tooltip("배운 조합법 기록. 비우면 전부 아는 것으로 본다.")]
        private RunProgress progress;

        [SerializeField, Tooltip("열려 있는 동안 보드 입력을 막는다. 마을처럼 없으면 비워 둔다.")]
        private InputRouter input;

        [Header("화면")]
        [SerializeField, Tooltip("도감 화면 전체(어두운 배경 포함). 평소에는 꺼져 있다.")]
        private GameObject panel;

        [SerializeField, Tooltip("탭 그림을 갈아 끼울 배경.")]
        private Image background;

        [SerializeField, Tooltip("조합 탭이 선택된 그림(dogam2).")]
        private Sprite craftTabSprite;

        [SerializeField, Tooltip("몬스터 탭이 선택된 그림(dogam).")]
        private Sprite monsterTabSprite;

        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button craftTabButton;
        [SerializeField] private Button monsterTabButton;

        [Header("스크롤 목록")]
        [SerializeField, Tooltip("목록 스크롤. 탭을 바꾸면 맨 위로 돌린다.")]
        private ScrollRect scroll;

        [SerializeField, Tooltip("줄을 쌓을 곳(VerticalLayoutGroup + ContentSizeFitter).")]
        private RectTransform content;

        [SerializeField, Tooltip("복제해 쓸 한 줄. 꺼 둔 채로 content 안에 둔다.")]
        private DogamRowView rowTemplate;

        [Header("문구")]
        [SerializeField, Tooltip("조합 재료 줄. {0}이 재료들이다.")]
        private string craftSubFormat = "{0} → 조합";
        [SerializeField] private string inputSeparator = " + ";
        [SerializeField] private string unknownInput = "???";
        [SerializeField, Tooltip("몬스터 둘째 줄. {0} 특징, {1} 체력, {2} 공격력.")]
        private string monsterSubFormat = "{0} · 체력 {1} · 공격 {2}";
        [SerializeField, Tooltip("공격력이 0인 것(벽)의 둘째 줄. {0} 특징, {1} 체력.")]
        private string wallSubFormat = "{0} · 체력 {1} · 반격 없음";
        [SerializeField] private string relicTag = "유물";
        [SerializeField] private string consumableTag = "소모품";
        [SerializeField] private string materialTag = "재료";

        [Header("몬스터 도감 글")]
        [SerializeField]
        private MonsterInfo[] monsters =
        {
            new MonsterInfo { type = TrashType.Paper, displayName = "종잇조각", habitat = "강", trait = "잡몹 · 연쇄 기본형", description = "바람에 실려 떠도는 종이 쓰레기. 같은 무리끼리 잘 엮인다." },
            new MonsterInfo { type = TrashType.Plastic, displayName = "페트병", habitat = "강", trait = "잡몹 · 질긴 몸", description = "물가에 버려진 페트병. 가벼워 보여도 한 대로는 잘 안 쓰러진다." },
            new MonsterInfo { type = TrashType.Can, displayName = "찌그러진 캔", habitat = "강", trait = "잡몹 · 들이받기", description = "찌그러진 몸으로 데굴데굴 굴러와 부딪힌다." },
            new MonsterInfo { type = TrashType.Glass, displayName = "유리조각", habitat = "산", trait = "잡몹 · 날카로움", description = "깨진 모서리가 날카로워 반격이 아프다. 단단하기도 하다." },
            new MonsterInfo { type = TrashType.Vinyl, displayName = "비닐봉지", habitat = "산", trait = "잡몹 · 바람 타기", description = "나뭇가지에 걸려 펄럭이다가 지나가는 이를 덮친다." },
            new MonsterInfo { type = TrashType.Styrofoam, displayName = "스티로폼", habitat = "산", trait = "잡몹 · 부스러기", description = "부서질 때마다 하얀 알갱이를 사방에 흩뿌린다." },
            new MonsterInfo { type = TrashType.Tire, displayName = "폐타이어", habitat = "바다", trait = "잡몹 · 묵직한 돌진", description = "파도에 밀려온 낡은 타이어. 굴러오는 힘이 세다." },
            new MonsterInfo { type = TrashType.Net, displayName = "폐어망", habitat = "바다", trait = "잡몹 · 얽어매기", description = "바다를 떠도는 그물. 걸리면 쉽게 빠져나오기 어렵다." },
            new MonsterInfo { type = TrashType.Sludge, displayName = "오염 폐기물", habitat = "바다", trait = "잡몹 · 오염", description = "바닥에 고인 끈적한 폐기물. 가까이 가면 해롭다." },

            // 벽 3종. 공격력이 없어 피해는 주지 않지만 길을 막고 연쇄를 끊는다(밸런싱 v1 §3).
            new MonsterInfo { type = TrashType.Wood, displayName = "나무 더미", habitat = "장애물", trait = "벽 · 파괴 가능", description = "쌓여 있는 폐목재. 몇 번 치면 부서지지만 그만큼 시간이 든다." },
            new MonsterInfo { type = TrashType.Concrete, displayName = "콘크리트", habitat = "장애물", trait = "벽 · 단단함", description = "깨진 콘크리트 덩어리. 부술 수는 있지만 돌아가는 편이 빠르다." },
            new MonsterInfo { type = TrashType.Steel, displayName = "철제 구조물", habitat = "장애물", trait = "벽 · 사실상 지형", description = "녹슨 철골. 부수기엔 너무 단단해 지형처럼 피해 다녀야 한다." },
        };

        private Tab _tab = Tab.Craft;
        private readonly List<DogamRowView> _rows = new List<DogamRowView>();
        private readonly List<CraftConfig.Recipe> _recipeList = new List<CraftConfig.Recipe>();

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (openButton != null) { openButton.onClick.AddListener(Open); }
            if (closeButton != null) { closeButton.onClick.AddListener(Close); }
            if (craftTabButton != null) { craftTabButton.onClick.AddListener(delegate { SelectTab(Tab.Craft); }); }
            if (monsterTabButton != null) { monsterTabButton.onClick.AddListener(delegate { SelectTab(Tab.Monster); }); }

            if (rowTemplate != null) { rowTemplate.gameObject.SetActive(false); }
            if (panel != null) { panel.SetActive(false); }
        }

        private void OnDisable()
        {
            BlockInput(false);
        }

        public void Open()
        {
            if (panel == null)
            {
                return;
            }

            panel.SetActive(true);
            BlockInput(true);
            SelectTab(_tab);
        }

        public void Close()
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }

            BlockInput(false);
        }

        public void SelectTab(Tab tab)
        {
            _tab = tab;
            Refresh();
            ScrollToTop();
        }

        private void Refresh()
        {
            if (background != null)
            {
                Sprite sprite = _tab == Tab.Craft ? craftTabSprite : monsterTabSprite;
                if (sprite != null)
                {
                    background.sprite = sprite;
                }
            }

            int shown = 0;
            if (_tab == Tab.Craft)
            {
                CollectRecipes();
                for (int i = 0; i < _recipeList.Count; i++)
                {
                    FillRecipe(RowAt(shown++), _recipeList[i]);
                }
            }
            else if (monsters != null)
            {
                for (int i = 0; i < monsters.Length; i++)
                {
                    FillMonster(RowAt(shown++), monsters[i]);
                }
            }

            // 남는 줄은 숨겨 두고 다음에 다시 쓴다.
            for (int i = shown; i < _rows.Count; i++)
            {
                if (_rows[i] != null && _rows[i].gameObject.activeSelf)
                {
                    _rows[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>index번째 줄. 모자라면 틀을 복제해 늘린다.</summary>
        private DogamRowView RowAt(int index)
        {
            while (_rows.Count <= index)
            {
                if (rowTemplate == null)
                {
                    return null;
                }

                Transform parent = content != null ? content : rowTemplate.transform.parent;
                DogamRowView row = Instantiate(rowTemplate, parent, false);
                row.name = "Row_" + _rows.Count;
                _rows.Add(row);
            }

            DogamRowView result = _rows[index];
            if (result != null && !result.gameObject.activeSelf)
            {
                result.gameObject.SetActive(true);
            }

            return result;
        }

        private void ScrollToTop()
        {
            if (scroll == null)
            {
                return;
            }

            // 줄 수가 바뀐 직후라 높이부터 다시 잰다. 안 그러면 이전 탭의 높이 기준으로 올라간다.
            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }

            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>결과가 아이템 표에 있는 조합법만 추린다.</summary>
        private void CollectRecipes()
        {
            _recipeList.Clear();

            if (recipes == null || recipes.Recipes == null || catalog == null)
            {
                return;
            }

            ItemConfig.Entry unused;
            foreach (CraftConfig.Recipe recipe in recipes.Recipes)
            {
                if (recipe.inputs != null && recipe.inputs.Length > 0 && catalog.TryGet(recipe.result, out unused))
                {
                    _recipeList.Add(recipe);
                }
            }
        }

        private void FillRecipe(DogamRowView row, CraftConfig.Recipe recipe)
        {
            if (row == null)
            {
                return;
            }

            ItemConfig.Entry result;
            catalog.TryGet(recipe.result, out result);

            bool known = recipe.knownFromStart || progress == null || progress.IsRecipeKnown(recipe.id);

            var names = new string[recipe.inputs.Length];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = known ? NameOf(recipe.inputs[i]) : unknownInput;
            }

            Sprite sprite = null;
            Color color = Color.white;
            if (itemIcons != null)
            {
                itemIcons.TryGet(recipe.result, out sprite, out color);
            }

            row.Set(
                NameOf(recipe.result),
                KindTag(result.kind),
                string.Format(craftSubFormat, string.Join(inputSeparator, names)),
                result.description,
                sprite,
                color);
        }

        private void FillMonster(DogamRowView row, MonsterInfo info)
        {
            if (row == null)
            {
                return;
            }

            int hp = 0;
            int attack = 0;
            if (trashStats != null)
            {
                TrashStats stats = trashStats.For(info.type);
                hp = stats.MaxHp;
                attack = stats.Attack;
            }

            Sprite sprite = null;
            Color color = Color.white;
            if (visuals != null)
            {
                visuals.TryResolveTrash(info.type, out color, out sprite);
            }

            // 그림이 없는 몬스터는 표의 색으로 칠한 네모를 보여 준다.
            row.Set(
                info.displayName,
                info.habitat,
                attack > 0
                    ? string.Format(monsterSubFormat, info.trait, hp, attack)
                    : string.Format(wallSubFormat, info.trait, hp),
                info.description,
                sprite != null ? sprite : PlaceholderSprite.Square,
                sprite != null ? Color.white : color);
        }

        private string KindTag(ItemConfig.Kind kind)
        {
            switch (kind)
            {
                case ItemConfig.Kind.Relic: return relicTag;
                case ItemConfig.Kind.Consumable: return consumableTag;
                default: return materialTag;
            }
        }

        private string NameOf(string id)
        {
            ItemConfig.Entry entry;
            if (catalog != null && catalog.TryGet(id, out entry) && !string.IsNullOrEmpty(entry.displayName))
            {
                return entry.displayName;
            }

            return id;
        }

        private void BlockInput(bool blocked)
        {
            if (input != null)
            {
                input.SetInputBlocked(this, blocked);
            }
        }
    }
}
