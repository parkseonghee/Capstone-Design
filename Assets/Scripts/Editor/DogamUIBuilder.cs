using RecycleLife.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Editor
{
    /// <summary>
    /// 열려 있는 씬의 UI 캔버스에 도감 화면(<see cref="DogamView"/>)과 여는 버튼을 만든다.
    /// 이미 있으면 지우고 새로 만든다 — 배치를 바꾸고 싶으면 여기 숫자를 고쳐 다시 돌리면 된다.
    ///
    /// 줄·탭 좌표는 배경 그림(412×917) 위의 픽셀 좌표다. 그림을 <see cref="Scale"/>배로 키워 얹는다.
    /// </summary>
    public static class DogamUIBuilder
    {
        private const float Scale = 2.1f;
        private const float BookOffsetY = 70f;

        private const string DogamMonsterPath = "Assets/Image/dogam.png";
        private const string DogamCraftPath = "Assets/Image/dogam2.png";
        private const string FontPath = "Assets/Fonts/NanumSquareR.ttf";
        private const string ButtonIconPath = "Assets/Image/DogamButton.png";
        private const string RowSpritePath = "Assets/Image/dogam_row.png";

        // 배경 그림(412×917) 위의 목록 치수. 줄 하나는 78px, 줄 사이 6px.
        private const float ListTop = 62f;
        private const float ListBottom = 894f;
        private const float RowHeight = 78f;
        private const float RowGap = 6f;

        /// <summary>그림의 테두리·줄 사이 색(#7C7164).</summary>
        private static readonly Color FrameColor = new Color32(124, 113, 100, 255);

        private static readonly Color TextBright = new Color(0.96f, 0.94f, 0.88f);
        private static readonly Color TextSub = new Color(0.86f, 0.83f, 0.76f);
        private static readonly Color TextDim = new Color(0.78f, 0.75f, 0.68f);

        private static Font _font;

        [MenuItem("RecycleLife/도감 UI 만들기 (현재 씬)")]
        public static void BuildInActiveScene()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            Canvas canvas = FindUiCanvas();
            if (canvas == null)
            {
                return "도감: Screen Space - Overlay 캔버스를 찾지 못했습니다.";
            }

            Remove(canvas.transform, "Dogam");
            Remove(canvas.transform, "Btn_Dogam");

            _font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            Sprite monsterSprite = LoadSprite(DogamMonsterPath);
            Sprite craftSprite = LoadSprite(DogamCraftPath);
            Sprite rounded = FindSprite("RoundedRect");

            // 루트: 컴포넌트는 늘 켜져 있고, 화면(panel)만 켜고 끈다.
            GameObject root = Make("Dogam", canvas.transform);
            Stretch(Rect(root));
            var view = root.AddComponent<DogamView>();

            GameObject panel = Make("DogamPanel", root.transform);
            Stretch(Rect(panel));
            panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f); // 뒤를 누르지 못하게 막는 어두운 판

            GameObject book = Make("Book", panel.transform);
            RectTransform bookRect = Rect(book);
            bookRect.anchorMin = bookRect.anchorMax = bookRect.pivot = new Vector2(0.5f, 0.5f);
            bookRect.sizeDelta = new Vector2(412f * Scale, 917f * Scale);
            bookRect.anchoredPosition = new Vector2(0f, BookOffsetY);
            var background = book.AddComponent<Image>();
            background.sprite = craftSprite;

            Button craftTab = TabButton("Tab_Craft", book.transform, 18f);
            Button monsterTab = TabButton("Tab_Monster", book.transform, 207f);

            var so = new SerializedObject(view);

            // 그림에 박힌 10줄을 테두리색 판으로 덮는다. 줄은 아래 스크롤 목록이 대신 그린다.
            GameObject cover = Make("ListCover", book.transform);
            Place(Rect(cover), 21f, 58f, 370f, 839f);
            var coverImage = cover.AddComponent<Image>();
            coverImage.color = FrameColor;
            coverImage.raycastTarget = false;

            // 스크롤 영역: 그림의 첫 줄 위쪽 ~ 마지막 줄 아래쪽.
            GameObject list = Make("List", book.transform);
            Place(Rect(list), 22f, ListTop, 368f, ListBottom - ListTop);
            var listImage = list.AddComponent<Image>();
            listImage.color = new Color(0f, 0f, 0f, 0.001f); // 빈 곳을 끌어도 스크롤되게 레이를 받는 판
            list.AddComponent<RectMask2D>();
            var scroll = list.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            GameObject contentGo = Make("Content", list.transform);
            RectTransform content = Rect(contentGo);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowGap * Scale;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = Rect(list);

            DogamRowView template = MakeRowTemplate(content, LoadSprite(RowSpritePath));

            // 아래: 닫기
            GameObject bar = Make("BottomBar", panel.transform);
            RectTransform barRect = Rect(bar);
            barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.sizeDelta = new Vector2(412f * Scale, 100f);
            barRect.anchoredPosition = new Vector2(0f, BookOffsetY - 917f * Scale / 2f - 24f);

            Button close = BarButton("Btn_Close", bar.transform, "닫기", 0f, 260f, rounded);

            Button open = MakeOpenButton(canvas.transform, LoadSprite(ButtonIconPath), rounded);
            root.transform.SetAsLastSibling();

            Set(so, "recipes", AssetDatabase.LoadAssetAtPath<CraftConfig>("Assets/GameData/CraftConfig.asset"));
            Set(so, "catalog", AssetDatabase.LoadAssetAtPath<ItemConfig>("Assets/GameData/ItemConfig.asset"));
            Set(so, "itemIcons", AssetDatabase.LoadAssetAtPath<ItemIconSet>("Assets/GameData/ItemIconSet.asset"));
            Set(so, "trashStats", AssetDatabase.LoadAssetAtPath<TrashStatsConfig>("Assets/GameData/TrashStatsConfig.asset"));
            Set(so, "visuals", AssetDatabase.LoadAssetAtPath<EntityVisualSet>("Assets/GameData/EntityVisualSet.asset"));
            Set(so, "progress", AssetDatabase.LoadAssetAtPath<RunProgress>("Assets/GameData/Waves/RunProgress.asset"));
            Set(so, "input", Object.FindObjectOfType<InputRouter>());
            Set(so, "panel", panel);
            Set(so, "background", background);
            Set(so, "craftTabSprite", craftSprite);
            Set(so, "monsterTabSprite", monsterSprite);
            Set(so, "openButton", open);
            Set(so, "closeButton", close);
            Set(so, "craftTabButton", craftTab);
            Set(so, "monsterTabButton", monsterTab);
            Set(so, "scroll", scroll);
            Set(so, "content", content);
            Set(so, "rowTemplate", template);
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

            return string.Format("도감: '{0}' 씬의 '{1}' 캔버스에 만들었습니다 (여는 버튼 {2}, 폰트 {3}).",
                canvas.gameObject.scene.name, canvas.name, open != null ? open.name : "없음", _font != null ? _font.name : "기본");
        }

        /// <summary>
        /// 여는 버튼. 레시피 버튼과 같은 틀의 책 아이콘(DogamButton.png)이다.
        /// 본편은 레시피 버튼을 복제해 모양을 맞추고, 오른쪽 위 고철 카운터 줄의 빈자리에 둔다
        /// (오른쪽 아래 버튼 줄 위는 보드와 겹친다). 레시피 버튼이 없는 씬(마을)은 새로 만든다.
        /// </summary>
        private static Button MakeOpenButton(Transform canvas, Sprite icon, Sprite rounded)
        {
            GameObject go;
            Transform recipe = canvas.Find("Btn_Recipe");
            if (recipe != null)
            {
                go = Object.Instantiate(recipe.gameObject, canvas);
                go.name = "Btn_Dogam";
                go.transform.SetSiblingIndex(recipe.GetSiblingIndex() + 1);
                var caption = go.GetComponentInChildren<Text>(true);
                if (caption != null)
                {
                    caption.text = "도감";
                }
            }
            else
            {
                go = Make("Btn_Dogam", canvas);
                go.AddComponent<Image>();
                go.AddComponent<Button>();
            }

            RectTransform rect = Rect(go);
            if (recipe != null)
            {
                // 본편: 오른쪽 버튼 줄의 네 번째 칸. 위치는 ArrangeSideButtons가 정한다.
                go.transform.SetSiblingIndex(recipe.GetSiblingIndex() + 1);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(130f, 130f);
                rect.anchoredPosition = OpenButtonPositionWithoutRecipe;
            }

            var image = go.GetComponent<Image>();
            image.sprite = icon != null ? icon : rounded;
            image.color = Color.white;
            image.preserveAspect = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick = new Button.ButtonClickedEvent();

            if (recipe != null)
            {
                ArrangeSideButtons(canvas);
            }

            return button;
        }

        /// <summary>본편 오른쪽 버튼 줄. 위에서부터 이 순서로 네 칸을 쌓는다.</summary>
        private static readonly string[] SideButtons = { "Btn_Trash", "Btn_FastDrop", "Btn_Recipe", "Btn_Dogam" };

        [MenuItem("RecycleLife/본편 오른쪽 버튼 줄 다시 맞추기")]
        public static void ArrangeSideButtonsInActiveScene()
        {
            Canvas canvas = FindUiCanvas();
            if (canvas != null && ArrangeSideButtons(canvas.transform))
            {
                EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            }
        }

        /// <summary>
        /// 오른쪽 버튼 네 개를 인벤토리 판의 위아래 끝에 맞춰 같은 크기·같은 간격으로 놓는다.
        /// 가로는 인벤토리 오른쪽 끝과 화면 오른쪽 끝 사이 빈 곳의 가운데다.
        /// 인벤토리 판이나 버튼이 하나라도 없으면 손대지 않는다.
        /// </summary>
        public static bool ArrangeSideButtons(Transform canvas)
        {
            var inventory = canvas.Find("InventoryPanel") as RectTransform;
            var buttons = new RectTransform[SideButtons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i] = canvas.Find(SideButtons[i]) as RectTransform;
                if (buttons[i] == null)
                {
                    return false;
                }
            }

            if (inventory == null)
            {
                return false;
            }

            // 인벤토리는 왼쪽 아래 기준이라 anchoredPosition·sizeDelta가 곧 캔버스 좌표다.
            float bottom = inventory.anchoredPosition.y;
            float top = bottom + inventory.sizeDelta.y;
            float right = inventory.anchoredPosition.x + inventory.sizeDelta.x;
            float canvasWidth = ((RectTransform)canvas).rect.width;

            int count = buttons.Length;
            float size = Mathf.Min(SideButtonMaxSize, (top - bottom) / (count + (count - 1) * SideButtonGapRatio));
            float gap = count > 1 ? (top - bottom - size * count) / (count - 1) : 0f;
            float centerX = -(canvasWidth - right) / 2f; // 화면 오른쪽 끝 기준

            for (int i = 0; i < count; i++)
            {
                RectTransform rect = buttons[i];
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                float centerY = top - size / 2f - i * (size + gap);
                rect.anchoredPosition = new Vector2(centerX, centerY);
            }

            return true;
        }

        private const float SideButtonMaxSize = 146f;
        private const float SideButtonGapRatio = 0.15f;

        /// <summary>마을처럼 레시피 버튼이 없는 씬에서의 자리(오른쪽 위 기준). 마을은 설정 버튼 바로 아래다.</summary>
        public static readonly Vector2 OpenButtonPositionWithoutRecipe = new Vector2(-50f, -205f);

        /// <summary>목록 한 줄의 틀. 배경은 그림에서 오려 낸 한 줄(dogam_row.png)이고, 꺼 둔 채로 content에 둔다.</summary>
        private static DogamRowView MakeRowTemplate(Transform content, Sprite rowSprite)
        {
            GameObject row = Make("RowTemplate", content);
            var bg = row.AddComponent<Image>();
            bg.sprite = rowSprite;
            bg.raycastTarget = false; // 줄 위를 끌어도 뒤 판(List)이 받아 스크롤된다
            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = RowHeight * Scale;
            element.minHeight = RowHeight * Scale;

            var view = row.AddComponent<DogamRowView>();

            GameObject icon = Make("Icon", row.transform);
            Place(Rect(icon), 11f, 20f, 38f, 38f);
            view.icon = icon.AddComponent<Image>();
            view.icon.preserveAspect = true;
            view.icon.raycastTarget = false;
            view.icon.enabled = false;

            view.nameLabel = Label("Name", row.transform, 32, TextBright, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(view.nameLabel.rectTransform, 62f, 9f, 200f, 22f);
            view.nameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            view.tagLabel = Label("Tag", row.transform, 22, TextDim, TextAnchor.UpperRight, FontStyle.Normal);
            Place(view.tagLabel.rectTransform, 240f, 10f, 116f, 18f);

            view.subLabel = Label("Sub", row.transform, 23, TextSub, TextAnchor.UpperLeft, FontStyle.Normal);
            Place(view.subLabel.rectTransform, 62f, 33f, 300f, 17f);
            BestFit(view.subLabel, 23, 14);

            view.descriptionLabel = Label("Desc", row.transform, 22, TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
            Place(view.descriptionLabel.rectTransform, 62f, 52f, 300f, 20f);
            BestFit(view.descriptionLabel, 22, 14);

            row.SetActive(false);
            return view;
        }

        private static Button TabButton(string name, Transform parent, float x)
        {
            GameObject go = Make(name, parent);
            Place(Rect(go), x, 10f, 186f, 42f);
            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f); // 탭 글자는 배경 그림에 있다. 누르는 자리만 만든다.
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            return button;
        }

        private static Button BarButton(string name, Transform parent, string text, float x, float width, Sprite rounded)
        {
            GameObject go = Make(name, parent);
            RectTransform rect = Rect(go);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 100f);
            rect.anchoredPosition = new Vector2(x, 0f);

            var image = go.AddComponent<Image>();
            image.sprite = rounded;
            image.type = rounded != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(0.27f, 0.25f, 0.22f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.49f, 0.40f);
            outline.effectDistance = new Vector2(3f, -3f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.4f);
            button.colors = colors;

            Text label = Label("Label", go.transform, 40, TextBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform);
            label.text = text;
            return button;
        }

        private static Text Label(string name, Transform parent, int size, Color color, TextAnchor align, FontStyle style)
        {
            var text = Make(name, parent).AddComponent<Text>();
            if (_font != null)
            {
                text.font = _font;
            }

            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void BestFit(Text text, int max, int min)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = max;
            text.resizeTextMinSize = min;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>배경 그림 픽셀 좌표(왼쪽 위 기준)로 놓는다.</summary>
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x * Scale, -y * Scale);
            rect.sizeDelta = new Vector2(w * Scale, h * Scale);
        }

        private static GameObject Make(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Rect(GameObject go)
        {
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Remove(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
        }

        private static void Set(SerializedObject so, string property, Object value)
        {
            so.FindProperty(property).objectReferenceValue = value;
        }

        /// <summary>가장 자식이 많은 Overlay 캔버스를 본 UI 캔버스로 본다.</summary>
        private static Canvas FindUiCanvas()
        {
            Canvas best = null;
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>(true))
            {
                // 꺼져 있는 옛 캔버스([Legacy] Canvas 등)는 고르지 않는다.
                if (!c.isRootCanvas || !c.gameObject.activeInHierarchy || c.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    continue;
                }

                if (best == null || c.transform.childCount > best.transform.childCount)
                {
                    best = c;
                }
            }

            return best;
        }

        private static Sprite LoadSprite(string path)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Sprite sprite)
                {
                    return sprite;
                }
            }

            return null;
        }

        private static Sprite FindSprite(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name))
            {
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    if (o is Sprite sprite && sprite.name == name)
                    {
                        return sprite;
                    }
                }
            }

            return null;
        }
    }
}
