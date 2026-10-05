using System.Collections.Generic;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 바닥에 깔린 독을 비추는 계층. 규칙은 하나도 모르고, Core가 들고 있는 칸 목록
    /// (<see cref="PoisonResolver.Cells"/>)을 그대로 그리기만 한다(Hard Rule 5).
    ///
    /// 독은 엔티티가 아니라서 BoardView의 칸 그리기를 타지 않는다 — 그래서 이 뷰가 따로 있다.
    /// 블록보다 <b>아래</b>에 깔려야 그 위에 떨어진 블록이 가려지지 않는다
    /// (정렬 순서 기본값이 음수인 이유).
    ///
    /// 표시용 스프라이트는 풀링해서 재사용한다 — 매 턴 생겼다 사라지는 것이라
    /// 만들고 부수기를 반복하면 GC가 생긴다(Hard Rule 8).
    /// </summary>
    public sealed class PoisonFieldView : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("구독할 세션. 씬에서 연결한다.")]
        private GameSession session;

        [SerializeField, Tooltip("칸 좌표를 물어볼 보드. 표시도 이 보드의 좌표계 위에 얹는다.")]
        private BoardView board;

        [SerializeField, Tooltip("색·스프라이트를 가져올 아트 에셋. 비우면 기본 초록으로 그린다.")]
        private EntityVisualSet visuals;

        [SerializeField, Tooltip("독 한 칸을 그릴 프리팹(SpriteRenderer). 비우면 단색 사각형을 만든다.")]
        private SpriteRenderer markerPrefab;

        [Header("표시")]
        [SerializeField, Range(0.1f, 1f), Tooltip("칸 대비 독의 크기.")]
        private float fill = 0.98f;

        [SerializeField, Tooltip("정렬 순서. 블록보다 아래여야 그 위에 떨어진 블록이 보인다.")]
        private int sortingOrder = -1;

        private readonly List<SpriteRenderer> _markers = new List<SpriteRenderer>(8);

        private void OnEnable()
        {
            if (session != null)
            {
                session.RunStarted += HandleRunStarted;
                session.Stepped += HandleStepped;
                session.BoardChanged += Refresh;
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.RunStarted -= HandleRunStarted;
                session.Stepped -= HandleStepped;
                session.BoardChanged -= Refresh;
            }

            HideAll();
        }

        private void HandleRunStarted(GameLoop loop)
        {
            // 새 판은 새 PoisonResolver다. 이전 판의 표시가 남지 않게 전부 내린다.
            HideAll();
        }

        private void HandleStepped(StepResult result)
        {
            Refresh();
        }

        private void Refresh()
        {
            PoisonResolver poison = session != null && session.Loop != null ? session.Loop.Poison : null;
            if (poison == null || board == null)
            {
                HideAll();
                return;
            }

            IReadOnlyList<Vector2Int> cells = poison.Cells;
            float size = board.CellSize * fill;

            Color baseColor = visuals != null ? visuals.PoisonColor : new Color(0.45f, 0.85f, 0.25f, 0.55f);
            Sprite sprite = visuals != null ? visuals.PoisonSprite : null;
            float fadingAlpha = visuals != null ? visuals.PoisonFadingAlpha : 0.5f;

            for (int i = 0; i < cells.Count; i++)
            {
                SpriteRenderer marker = MarkerAt(i, sprite);
                if (marker == null)
                {
                    break;
                }

                // 남은 턴이 하나뿐이면 흐려진다 — 언제 사라지는지가 보여야
                // "한 턴만 버티면 지나갈 수 있다"는 판단이 선다.
                Color color = baseColor;
                if (poison.RemainingAt(cells[i]) <= 1)
                {
                    color.a *= fadingAlpha;
                }

                marker.gameObject.SetActive(true);
                marker.color = color;
                marker.transform.localPosition = board.CellToLocalPosition(cells[i]);
                marker.transform.localScale = new Vector3(size, size, 1f);
            }

            for (int i = cells.Count; i < _markers.Count; i++)
            {
                if (_markers[i] != null)
                {
                    _markers[i].gameObject.SetActive(false);
                }
            }
        }

        private void HideAll()
        {
            for (int i = 0; i < _markers.Count; i++)
            {
                if (_markers[i] != null)
                {
                    _markers[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>표시 스프라이트를 필요한 만큼만 만들어 재사용한다.</summary>
        private SpriteRenderer MarkerAt(int index, Sprite sprite)
        {
            while (_markers.Count <= index)
            {
                if (board == null || board.CellRoot == null)
                {
                    return null;
                }

                SpriteRenderer created = markerPrefab != null
                    ? Instantiate(markerPrefab, board.CellRoot)
                    : new GameObject("PoisonCell", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();

                if (markerPrefab == null)
                {
                    created.transform.SetParent(board.CellRoot, false);
                }

                created.sprite = sprite != null ? sprite : PlaceholderSprite.Square;
                created.sortingOrder = sortingOrder;
                created.gameObject.name = "PoisonCell";
                created.gameObject.SetActive(false);
                _markers.Add(created);
            }

            return _markers[index];
        }
    }
}
