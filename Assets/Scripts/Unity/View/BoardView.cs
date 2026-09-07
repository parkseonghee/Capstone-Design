using System.Collections.Generic;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 보드 상태를 스프라이트로 비추기만 하는 계층. 규칙 판단은 하나도 하지 않는다.
    ///
    /// 로직은 스텝당 한 칸씩 내려간 결과를 주고, 여기서 그 위치로 보간해 부드럽게 잇는다.
    ///
    /// 프리뷰 줄(row 0)은 기획대로 <b>1/6만 드러난다</b>. 씬에 마스크 오브젝트를 심는 대신
    /// 스프라이트를 천장선에서 잘라 그린다(§1, ApplyReveal 참조).
    ///
    /// 뷰 오브젝트는 풀링한다. 스텝마다 스폰이 생기므로 Instantiate/Destroy 반복은
    /// 안드로이드에서 GC를 만든다(Hard Rule 8).
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("구독할 세션. 씬에서 연결한다.")]
        private GameSession session;

        [SerializeField, Tooltip("칸 스프라이트가 들어갈 부모. 이 오브젝트의 위치가 보드 중심이 된다.")]
        private Transform cellRoot;

        [SerializeField, Tooltip("칸 하나를 그리는 프리팹(SpriteRenderer).")]
        private SpriteRenderer entityViewPrefab;

        [SerializeField, Tooltip("종류별 색·스프라이트 매핑 에셋.")]
        private EntityVisualSet visuals;

        [SerializeField, Tooltip("(선택) 보드 배경. 연결하면 보이는 영역에 맞춰 스케일만 조정한다.")]
        private SpriteRenderer boardBackground;

        [Header("레이아웃")]
        [SerializeField, Min(0.05f), Tooltip("한 칸의 월드 크기.")]
        private float cellSize = 1f;

        [SerializeField, Range(0.1f, 1f), Tooltip("칸 안에서 스프라이트가 차지하는 비율. 낮추면 칸 사이 여백이 생긴다.")]
        private float cellFill = 0.9f;

        [SerializeField, Range(0f, 1f), Tooltip("프리뷰 줄이 화면에 드러나는 비율. 기획 확정값 1/6. " +
                                                "0이면 다음 블록이 아예 안 보이고, 1이면 한 줄이 통째로 보인다.")]
        private float previewVisibleFraction = 1f / 6f;

        [Header("연출")]
        [SerializeField, Min(0f), Tooltip("낙하·이동 보간 속도. 0이면 즉시 이동한다.")]
        private float moveSmoothing = 16f;

        [SerializeField, Tooltip("새로 등장한 쓰레기를 보드 위쪽 바깥에서 떨어뜨릴지. " +
                                 "런 시작의 초기 줄도 이 경로로 쏟아져 내린다.")]
        private bool dropFromAbove = true;

        /// <summary>
        /// 뷰 하나의 상태. 잘린 위치가 아니라 <b>진짜 위치</b>를 따로 들고 있어야
        /// 다음 프레임 보간이 어긋나지 않는다(ApplyReveal이 transform을 아래로 당기기 때문).
        /// </summary>
        private sealed class ViewSlot
        {
            public SpriteRenderer Renderer;
            public Vector3 Position;
        }

        private readonly Dictionary<Entity, ViewSlot> _views = new Dictionary<Entity, ViewSlot>(128);
        private readonly Stack<ViewSlot> _pool = new Stack<ViewSlot>(64);
        private readonly HashSet<Entity> _alive = new HashSet<Entity>();
        private readonly List<Entity> _stale = new List<Entity>(32);

        private GameLoop _loop;

        /// <summary>
        /// 화면에 보여야 할 영역의 월드 크기. CameraBoardFitter가 읽는다.
        /// 프리뷰 줄은 통째로 세지 않는다 — 기획대로 일부만 드러나기 때문이다.
        /// </summary>
        public Vector2 BoardWorldSize
        {
            get
            {
                if (_loop == null)
                {
                    return Vector2.zero;
                }

                float visibleRows = _loop.Grid.Rows - HiddenRows;
                return new Vector2(_loop.Grid.Cols * cellSize, visibleRows * cellSize);
            }
        }

        /// <summary>보이는 영역 중심의 월드 좌표. 프리뷰가 잘린 만큼 아래로 내려간다.</summary>
        public Vector3 BoardWorldCenter
        {
            get
            {
                Vector3 origin = cellRoot != null ? cellRoot.position : transform.position;
                return _loop == null
                    ? origin
                    : origin + new Vector3(0f, -HiddenRows * cellSize * 0.5f, 0f);
            }
        }

        /// <summary>화면 위쪽에서 잘려 나간 행 수(소수). 프리뷰 줄 중 안 보이는 부분이다.</summary>
        private float HiddenRows =>
            _loop == null ? 0f : _loop.FirstPlayableRow * (1f - previewVisibleFraction);

        /// <summary>플레이 영역의 윗변(로컬 y). 프리뷰 블록은 이 선 위로 조금만 삐져나온다.</summary>
        private float PlayAreaTopLocalY =>
            _loop == null
                ? 0f
                : ((_loop.Grid.Rows - 1) * 0.5f - _loop.FirstPlayableRow + 0.5f) * cellSize;

        private void OnEnable()
        {
            if (session == null)
            {
                Debug.LogError($"{nameof(BoardView)}: GameSession이 연결되지 않았습니다.", this);
                return;
            }

            session.RunStarted += HandleRunStarted;
            session.BoardChanged += HandleBoardChanged;
            session.Stepped += HandleStepped;

            // 컴포넌트가 도중에 다시 켜진 경우에도 현재 보드를 따라잡는다.
            if (session.Loop != null)
            {
                HandleRunStarted(session.Loop);
            }
        }

        private void OnDisable()
        {
            if (session == null)
            {
                return;
            }

            session.RunStarted -= HandleRunStarted;
            session.BoardChanged -= HandleBoardChanged;
            session.Stepped -= HandleStepped;
        }

        private void LateUpdate()
        {
            if (_loop == null)
            {
                return;
            }

            float revealLine = PlayAreaTopLocalY + previewVisibleFraction * cellSize;
            float fullSize = cellSize * cellFill;
            float t = moveSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-moveSmoothing * Time.deltaTime);

            // Dictionary 열거자는 struct고 값(ViewSlot)은 참조형이라, 매 프레임 돌아도 할당이 없다.
            foreach (KeyValuePair<Entity, ViewSlot> pair in _views)
            {
                ViewSlot slot = pair.Value;
                Vector3 target = CellToLocal(pair.Key.Position);

                slot.Position = Vector3.Lerp(slot.Position, target, t);
                ApplyReveal(slot, revealLine, fullSize);
            }
        }

        /// <summary>
        /// 프리뷰 줄에 대기 중인 블록을 "천장 밑으로 살짝만" 보이게 깎는다(기획: 1/6 노출).
        ///
        /// SpriteMask 없이 스프라이트를 직접 자르는 이유는 씬에 오브젝트를 더 심지 않기 위함이다
        /// (Hard Rule 2: 배치는 씬에서, 코드는 상태만).
        /// 잘리는 양을 논리 행이 아니라 <b>보간된 y</b>로 계산해서, 블록이 내려오는 동안
        /// 천장 밑에서 서서히 모습을 드러내게 한다 — 줄이 바뀌는 순간 튀지 않는다.
        /// </summary>
        private void ApplyReveal(ViewSlot slot, float revealLine, float fullSize)
        {
            Transform view = slot.Renderer.transform;

            float half = fullSize * 0.5f;
            float bottom = slot.Position.y - half;
            float visible = Mathf.Clamp(revealLine - bottom, 0f, fullSize);

            if (visible >= fullSize)
            {
                view.localScale = new Vector3(fullSize, fullSize, 1f);
                view.localPosition = slot.Position;
                return;
            }

            view.localScale = new Vector3(fullSize, visible, 1f);
            view.localPosition = new Vector3(slot.Position.x, bottom + visible * 0.5f, slot.Position.z);
        }

        private void HandleRunStarted(GameLoop loop)
        {
            _loop = loop;
            ReleaseAll();
            ResizeBackground();
            Sync(snap: true);
        }

        /// <summary>시작 연출 중 줄이 한 칸 내려왔거나 플레이어가 등장했을 때.</summary>
        private void HandleBoardChanged()
        {
            Sync(snap: false);
        }

        private void HandleStepped(StepResult result)
        {
            // 거부된 입력이라도 뷰가 어긋나 있을 수 있으니 그대로 맞춘다(비용은 보드 한 번 훑기).
            Sync(snap: false);
        }

        private void Sync(bool snap)
        {
            if (_loop == null || entityViewPrefab == null || cellRoot == null)
            {
                return;
            }

            BoardGrid grid = _loop.Grid;
            _alive.Clear();

            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Cols; col++)
                {
                    Entity entity = grid[col, row];
                    if (entity == null)
                    {
                        continue;
                    }

                    _alive.Add(entity);

                    if (!_views.TryGetValue(entity, out ViewSlot slot))
                    {
                        slot = Rent();
                        _views.Add(entity, slot);
                        ApplyVisual(entity, slot.Renderer);

                        // 새 쓰레기는 보드 위쪽 바깥에서 시작해야 "떨어져 내리는" 것으로 보인다.
                        bool fallsIn = dropFromAbove && entity.Kind != EntityKind.Player;
                        slot.Position = fallsIn
                            ? CellToLocal(new Vector2Int(entity.Position.x, -1))
                            : CellToLocal(entity.Position);
                    }
                    else if (snap)
                    {
                        slot.Position = CellToLocal(entity.Position);
                    }
                }
            }

            _stale.Clear();
            foreach (KeyValuePair<Entity, ViewSlot> pair in _views)
            {
                if (!_alive.Contains(pair.Key))
                {
                    _stale.Add(pair.Key);
                }
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                Release(_views[_stale[i]]);
                _views.Remove(_stale[i]);
            }
        }

        private Vector3 CellToLocal(Vector2Int cell)
        {
            if (_loop == null)
            {
                return Vector3.zero;
            }

            // 좌상단 원점(row 증가 = 아래)을 월드의 y 감소 방향에 맞춘다.
            float x = (cell.x - (_loop.Grid.Cols - 1) * 0.5f) * cellSize;
            float y = ((_loop.Grid.Rows - 1) * 0.5f - cell.y) * cellSize;
            return new Vector3(x, y, 0f);
        }

        private void ApplyVisual(Entity entity, SpriteRenderer view)
        {
            Color color = Color.white;
            Sprite sprite = null;

            if (visuals != null)
            {
                visuals.Resolve(entity, out color, out sprite);
            }

            view.sprite = sprite != null ? sprite : PlaceholderSprite.Square;
            view.color = color;
            view.transform.localScale = Vector3.one * (cellSize * cellFill);
            view.name = entity.Kind == EntityKind.Player ? "Player" : $"Trash_{((Trash)entity).Type}";
        }

        private ViewSlot Rent()
        {
            ViewSlot slot = _pool.Count > 0 ? _pool.Pop() : new ViewSlot();
            if (slot.Renderer == null)
            {
                slot.Renderer = Instantiate(entityViewPrefab, cellRoot);
            }

            slot.Renderer.transform.SetParent(cellRoot, worldPositionStays: false);
            slot.Renderer.gameObject.SetActive(true);
            return slot;
        }

        private void Release(ViewSlot slot)
        {
            slot.Renderer.gameObject.SetActive(false);
            _pool.Push(slot);
        }

        private void ReleaseAll()
        {
            foreach (KeyValuePair<Entity, ViewSlot> pair in _views)
            {
                Release(pair.Value);
            }

            _views.Clear();
        }

        private void ResizeBackground()
        {
            if (boardBackground == null || _loop == null)
            {
                return;
            }

            if (boardBackground.sprite == null)
            {
                boardBackground.sprite = PlaceholderSprite.Square;
            }

            // 배경도 보이는 영역에만 깔아야 프리뷰 줄이 판 위에 떠 있는 것처럼 보인다.
            Vector2 size = BoardWorldSize;
            boardBackground.transform.localPosition = new Vector3(0f, -HiddenRows * cellSize * 0.5f, 0f);
            boardBackground.transform.localScale = new Vector3(size.x, size.y, 1f);
        }
    }
}
