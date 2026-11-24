using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 상점 방의 좌판. 상품 줄 뒤에 탁자(StoreTable)를 깔고, 그 뒤에 상인 할머니를 세운다.
    /// 상품은 보드 칸(<see cref="ShopRoom"/>의 슬롯)에 그대로 있고, 이건 <b>그림만</b> 얹는다 —
    /// 판정·구매는 전부 ShopRoom/Core 몫이다.
    ///
    /// 배치 기준은 상품 줄이다: 탁자는 첫 상품부터 끝 상품까지 덮는 폭으로 늘이고,
    /// 상판 높이를 상품 아래쪽에 맞춰 상품이 탁자 위에 놓인 것처럼 보이게 한다.
    /// 상인은 탁자 가운데 뒤에 서서 상반신만 탁자 위로 보인다.
    ///
    /// 그림은 보드 칸 부모(CellRoot) 밑에 두어서 보드와 같은 좌표로 움직인다.
    /// 상인은 상점이 열릴 때마다 <see cref="keeperSprites"/>에서 무작위로 한 명 고른다.
    /// </summary>
    public sealed class ShopStallView : MonoBehaviour
    {
        [SerializeField, Tooltip("칸 좌표를 월드로 바꿀 보드.")]
        private BoardView board;

        [Header("그림")]
        [SerializeField] private Sprite tableSprite;

        [SerializeField, Tooltip("상인 그림들. 상점이 열릴 때마다 하나를 무작위로 고른다.")]
        private Sprite[] keeperSprites = new Sprite[0];

        [Header("탁자")]
        [SerializeField, Min(0f), Tooltip("양 끝 상품 바깥으로 더 늘릴 폭(칸 단위, 양쪽 합).")]
        private float tableExtraWidth = 0f;

        [SerializeField, Range(0f, 1f), Tooltip("탁자 그림에서 상품이 놓일 상판 높이(그림 위에서부터의 비율).")]
        private float tableSurfaceFromTop = 0.3f;

        [SerializeField, Tooltip("상판 높이를 상품 칸 중심에서 얼마나 아래에 둘지(칸 단위). " +
                                 "상품 그림의 아래쪽 끝 근처에 맞추면 상판 위에 놓인 것처럼 보인다.")]
        private float surfaceBelowItemCenter = 0.38f;

        [Header("상인")]
        [SerializeField, Min(0.1f), Tooltip("상인 그림 전체 높이(칸 단위).")]
        private float keeperHeight = 3.4f;

        [SerializeField, Range(0f, 1f), Tooltip("상인 키 중 탁자 상판 위로 보이는 비율. 나머지는 탁자 뒤에 가린다.")]
        private float keeperAboveTable = 0.62f;

        [Header("그리는 순서 (보드 칸·플레이어는 0)")]
        [SerializeField] private int tableSortingOrder = -1;
        [SerializeField] private int keeperSortingOrder = -2;

        private SpriteRenderer _table;
        private SpriteRenderer _keeper;

        /// <summary>상품 칸들 뒤에 좌판을 펼친다. 칸이 없으면 숨긴다.</summary>
        public void Show(IReadOnlyList<Vector2Int> cells)
        {
            if (board == null || board.CellRoot == null || cells == null || cells.Count == 0)
            {
                Hide();
                return;
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int row = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                minX = Mathf.Min(minX, cells[i].x);
                maxX = Mathf.Max(maxX, cells[i].x);
                row = Mathf.Max(row, cells[i].y); // 여러 줄이면 맨 아래 줄을 기준으로 깐다
            }

            float cell = board.CellSize;
            Vector3 left = board.CellToLocalPosition(new Vector2Int(minX, row));
            Vector3 right = board.CellToLocalPosition(new Vector2Int(maxX, row));
            float centerX = (left.x + right.x) * 0.5f;
            float surfaceY = left.y - surfaceBelowItemCenter * cell;

            if (tableSprite != null)
            {
                _table = Ensure(_table, "ShopTable", tableSortingOrder);
                _table.sprite = tableSprite;

                Bounds b = tableSprite.bounds;
                float width = (right.x - left.x) + cell * (1f + tableExtraWidth);
                float scale = width / b.size.x;
                float surfaceLocal = b.max.y - tableSurfaceFromTop * b.size.y;
                _table.transform.localScale = new Vector3(scale, scale, 1f);
                _table.transform.localPosition = new Vector3(centerX - b.center.x * scale, surfaceY - surfaceLocal * scale, 0f);
                _table.gameObject.SetActive(true);
            }

            Sprite keeperSprite = PickKeeper();
            if (keeperSprite != null)
            {
                _keeper = Ensure(_keeper, "ShopKeeper", keeperSortingOrder);
                _keeper.sprite = keeperSprite;

                Bounds b = keeperSprite.bounds;
                float height = keeperHeight * cell;
                float scale = height / b.size.y;
                float topY = surfaceY + keeperAboveTable * height;
                _keeper.transform.localScale = new Vector3(scale, scale, 1f);
                _keeper.transform.localPosition = new Vector3(centerX - b.center.x * scale, topY - b.max.y * scale, 0f);
                _keeper.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_table != null) { _table.gameObject.SetActive(false); }
            if (_keeper != null) { _keeper.gameObject.SetActive(false); }
        }

        private Sprite PickKeeper()
        {
            if (keeperSprites == null || keeperSprites.Length == 0)
            {
                return null;
            }

            // 빈 칸이 섞여 있어도 그림이 있는 것만 고른다.
            int count = 0;
            for (int i = 0; i < keeperSprites.Length; i++)
            {
                if (keeperSprites[i] != null) { count++; }
            }

            if (count == 0)
            {
                return null;
            }

            int pick = Random.Range(0, count);
            for (int i = 0; i < keeperSprites.Length; i++)
            {
                if (keeperSprites[i] == null) { continue; }
                if (pick-- == 0) { return keeperSprites[i]; }
            }

            return null;
        }

        private SpriteRenderer Ensure(SpriteRenderer renderer, string name, int sortingOrder)
        {
            if (renderer == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(board.CellRoot, false);
                renderer = go.AddComponent<SpriteRenderer>();
            }
            else if (renderer.transform.parent != board.CellRoot)
            {
                renderer.transform.SetParent(board.CellRoot, false);
            }

            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
