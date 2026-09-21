using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 폭탄 설치 조작. 기획서(폭탄·아이템·기믹 v1) §1-2.
    ///
    /// 흐름은 셋이다:
    ///  1. 설치 버튼을 누르면 한 칸 거리의 설치 가능 칸에 표시가 뜬다.
    ///  2. 표시된 칸을 <b>탭하면</b> 거기에 폭탄이 놓인다.
    ///  3. 다른 곳을 탭하거나 버튼을 다시 누르면 취소된다.
    ///
    /// <b>손을 떼도 표시는 남는다.</b> 처음엔 "누르고 있는 동안만" 띄웠는데,
    /// 그러면 포인터가 하나일 때(에디터 마우스, 한 손 조작) 버튼을 누른 채로 보드를 탭할 수가 없어
    /// 설치 자체가 불가능했다. 표시를 남기면 한 손으로도, 두 손가락으로도 똑같이 동작한다.
    ///
    /// 어디에 놓을 수 있는지는 전부 Core(GameLoop.CollectBombPlacements)가 정한다.
    /// 이 컴포넌트는 그 목록을 화면에 비추고 탭을 칸 좌표로 바꿔 넘길 뿐이다(Hard Rule 5).
    ///
    /// 표시용 스프라이트는 풀링해서 재사용한다 — 켰다 껐다를 반복하는 UI라
    /// 매번 만들고 부수면 GC가 생긴다(Hard Rule 8).
    /// </summary>
    public sealed class BombPlacementController : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("구독할 세션. 씬에서 연결한다.")]
        private GameSession session;

        [SerializeField, Tooltip("보드 좌표를 물어볼 뷰. 표시도 이 보드의 좌표계 위에 얹는다.")]
        private BoardView board;

        [SerializeField, Tooltip("꾹 누르기를 받는 버튼. 씬에서 연결한다.")]
        private BombButton bombButton;

        [SerializeField, Tooltip("설치 가능 칸에 띄울 표시 프리팹(SpriteRenderer).")]
        private SpriteRenderer markerPrefab;

        [Header("표시")]
        [SerializeField, Tooltip("표시 색. 스프라이트를 안 넣으면 반투명 사각형으로 보인다.")]
        private Color markerColor = new Color(1f, 1f, 1f, 0.35f);

        [SerializeField, Tooltip("(선택) 표시 스프라이트. 비우면 단색 사각형을 쓴다.")]
        private Sprite markerSprite;

        [SerializeField, Range(0.1f, 1f), Tooltip("칸 대비 표시 크기.")]
        private float markerFill = 0.9f;

        [SerializeField, Tooltip("표시의 정렬 순서. 블록보다 위에 보여야 한다.")]
        private int markerSortingOrder = 2;

        private readonly List<SpriteRenderer> _markers = new List<SpriteRenderer>(4);
        private readonly List<Vector2Int> _cells = new List<Vector2Int>(4);

        /// <summary>지금 설치 자리를 보여 주고 있는지. 뷰나 HUD가 읽어도 되게 열어 둔다.</summary>
        public bool IsShowingPlacements { get; private set; }

        private void OnEnable()
        {
            if (bombButton != null)
            {
                // 뗄 때는 아무것도 하지 않는다 — 표시를 남겨야 한 손으로 고를 수 있다.
                bombButton.HoldStarted += TogglePlacements;
            }

            if (session != null)
            {
                // 보드가 바뀌면 후보도 바뀐다. 표시 중이면 다시 계산한다.
                session.Stepped += HandleStepped;
                session.RunStarted += HandleRunStarted;
            }
        }

        private void OnDisable()
        {
            if (bombButton != null)
            {
                bombButton.HoldStarted -= TogglePlacements;
            }

            if (session != null)
            {
                session.Stepped -= HandleStepped;
                session.RunStarted -= HandleRunStarted;
            }

            HidePlacements();
        }

        private void Update()
        {
            if (!IsShowingPlacements)
            {
                return;
            }

            ReadTap();
        }

        /// <summary>버튼을 누를 때마다 표시를 켜고 끈다.</summary>
        private void TogglePlacements()
        {
            if (IsShowingPlacements)
            {
                HidePlacements();
                return;
            }

            ShowPlacements();
        }

        /// <summary>설치 가능 칸을 계산해 화면에 띄운다.</summary>
        private void ShowPlacements()
        {
            if (session == null || session.Loop == null || board == null || !session.Loop.IsReady)
            {
                return;
            }

            IsShowingPlacements = true;
            RefreshPlacements();
        }

        private void HidePlacements()
        {
            IsShowingPlacements = false;
            _cells.Clear();

            for (int i = 0; i < _markers.Count; i++)
            {
                if (_markers[i] != null)
                {
                    _markers[i].gameObject.SetActive(false);
                }
            }
        }

        private void HandleRunStarted(GameLoop loop)
        {
            HidePlacements();
        }

        private void HandleStepped(StepResult result)
        {
            if (IsShowingPlacements)
            {
                RefreshPlacements();
            }
        }

        private void RefreshPlacements()
        {
            GameLoop loop = session.Loop;

            _cells.Clear();
            loop.CollectBombPlacements();

            IReadOnlyList<Vector2Int> found = loop.BombPlacements;
            for (int i = 0; i < found.Count; i++)
            {
                _cells.Add(found[i]);
            }

            float size = board.CellSize * markerFill;

            for (int i = 0; i < _cells.Count; i++)
            {
                SpriteRenderer marker = MarkerAt(i);
                if (marker == null)
                {
                    break;
                }

                marker.gameObject.SetActive(true);
                marker.transform.localPosition = board.CellToLocalPosition(_cells[i]);
                marker.transform.localScale = new Vector3(size, size, 1f);
            }

            // 후보가 줄었으면 남는 표시는 꺼 둔다.
            for (int i = _cells.Count; i < _markers.Count; i++)
            {
                if (_markers[i] != null)
                {
                    _markers[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>표시 스프라이트를 필요한 만큼만 만들어 재사용한다.</summary>
        private SpriteRenderer MarkerAt(int index)
        {
            while (_markers.Count <= index)
            {
                if (markerPrefab == null || board == null || board.CellRoot == null)
                {
                    return null;
                }

                SpriteRenderer created = Instantiate(markerPrefab, board.CellRoot);
                created.sprite = markerSprite != null ? markerSprite : PlaceholderSprite.Square;
                created.color = markerColor;
                created.sortingOrder = markerSortingOrder;
                created.gameObject.name = "BombPlacementMarker";
                created.gameObject.SetActive(false);
                _markers.Add(created);
            }

            return _markers[index];
        }

        /// <summary>
        /// 보드를 탭했는지 본다. 표시된 칸을 찍었으면 거기에 폭탄을 놓는다.
        ///
        /// UI 위에서 시작한 탭은 무시한다 — 설치 버튼을 누르는 손가락 자체가
        /// 설치로 오인되면 안 되기 때문이다.
        /// </summary>
        private void ReadTap()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector2 screen = pointer.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

            Vector2Int cell;
            if (!board.TryWorldToCell(world, out cell))
            {
                return;
            }

            if (!_cells.Contains(cell))
            {
                // 표시되지 않은 칸을 찍었으면 취소로 본다. 표시를 켜 둔 채로 두면
                // 방향 입력이 설치 대기 상태에 갇힌 것처럼 느껴진다.
                HidePlacements();
                return;
            }

            // 하나 놓으면 표시를 끈다. 계속 놓고 싶으면 버튼을 다시 누르면 된다.
            session.TryPlaceBomb(cell);
            HidePlacements();
        }
    }
}
