using System.Collections.Generic;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 보드 상태를 스프라이트로 비추기만 하는 계층. 규칙 판단은 하나도 하지 않는다.
    ///
    /// WEEK1 §4대로 로직은 이미 완전 정착된 상태를 주고, 여기서 그 위치로 보간해
    /// "떨어지는 것처럼" 보여준다.
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

        [SerializeField, Tooltip("(선택) 보드 배경. 연결하면 보드 크기에 맞춰 스케일만 조정한다.")]
        private SpriteRenderer boardBackground;

        [Header("레이아웃")]
        [SerializeField, Min(0.05f), Tooltip("한 칸의 월드 크기.")]
        private float cellSize = 1f;

        [SerializeField, Range(0.1f, 1f), Tooltip("칸 안에서 스프라이트가 차지하는 비율. 낮추면 칸 사이 여백이 생긴다.")]
        private float cellFill = 0.9f;

        [Header("연출")]
        [SerializeField, Min(0f), Tooltip("낙하·이동 보간 속도. 0이면 즉시 이동한다.")]
        private float moveSmoothing = 16f;

        [SerializeField, Tooltip("새로 등장한 쓰레기를 보드 위쪽 바깥에서 떨어뜨릴지. " +
                                 "런 시작의 초기 줄도 이 경로로 쏟아져 내린다.")]
        private bool dropFromAbove = true;

        private readonly Dictionary<Entity, SpriteRenderer> _views = new Dictionary<Entity, SpriteRenderer>(128);
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>(64);
        private readonly HashSet<Entity> _alive = new HashSet<Entity>();
        private readonly List<Entity> _stale = new List<Entity>(32);

        private GameLoop _loop;

        /// <summary>보드가 차지하는 월드 크기. CameraBoardFitter가 읽는다.</summary>
        public Vector2 BoardWorldSize =>
            _loop == null ? Vector2.zero : new Vector2(_loop.Grid.Cols * cellSize, _loop.Grid.Rows * cellSize);

        /// <summary>보드 중심의 월드 좌표.</summary>
        public Vector3 BoardWorldCenter => cellRoot != null ? cellRoot.position : transform.position;

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

            // Dictionary 열거자는 struct라 매 프레임 돌아도 할당이 없다.
            foreach (KeyValuePair<Entity, SpriteRenderer> pair in _views)
            {
                Vector3 target = CellToLocal(pair.Key.Position);
                Transform view = pair.Value.transform;

                view.localPosition = moveSmoothing <= 0f
                    ? target
                    : Vector3.Lerp(view.localPosition, target, 1f - Mathf.Exp(-moveSmoothing * Time.deltaTime));
            }
        }

        private void HandleRunStarted(GameLoop loop)
        {
            _loop = loop;
            ReleaseAll();
            ResizeBackground();
            Sync(snap: true);
        }

        /// <summary>시작 연출 중 줄 하나가 떨어졌거나 플레이어가 등장했을 때.</summary>
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

                    if (!_views.TryGetValue(entity, out SpriteRenderer view))
                    {
                        view = Rent();
                        _views.Add(entity, view);
                        ApplyVisual(entity, view);

                        // 로직은 이미 정착까지 끝낸 상태를 준다(WEEK1 §4).
                        // 보드 위쪽 바깥에서 시작시켜야 "떨어져 내리는" 것으로 보인다.
                        bool fallsIn = dropFromAbove && entity.Kind != EntityKind.Player;
                        view.transform.localPosition = fallsIn
                            ? CellToLocal(new Vector2Int(entity.Position.x, -1))
                            : CellToLocal(entity.Position);
                    }
                    else if (snap)
                    {
                        view.transform.localPosition = CellToLocal(entity.Position);
                    }
                }
            }

            _stale.Clear();
            foreach (KeyValuePair<Entity, SpriteRenderer> pair in _views)
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

        private SpriteRenderer Rent()
        {
            SpriteRenderer view = _pool.Count > 0 ? _pool.Pop() : Instantiate(entityViewPrefab, cellRoot);
            view.transform.SetParent(cellRoot, worldPositionStays: false);
            view.gameObject.SetActive(true);
            return view;
        }

        private void Release(SpriteRenderer view)
        {
            view.gameObject.SetActive(false);
            _pool.Push(view);
        }

        private void ReleaseAll()
        {
            foreach (KeyValuePair<Entity, SpriteRenderer> pair in _views)
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

            boardBackground.transform.localPosition = Vector3.zero;
            boardBackground.transform.localScale = new Vector3(BoardWorldSize.x, BoardWorldSize.y, 1f);
        }
    }
}
