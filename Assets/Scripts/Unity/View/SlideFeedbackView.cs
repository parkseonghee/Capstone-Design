using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 슬라이드 조작을 눈에 보이게 만드는 표시. 규칙은 모르고 <b>보여 주기만</b> 한다(Hard Rule 4·5).
    ///
    /// 세 가지를 그린다:
    ///  1. <b>링</b> — 손가락이 닿은 자리. 링의 반지름이 곧 "한 칸 나가는 거리"라,
    ///     어디까지 밀어야 하는지가 설명 없이 보인다.
    ///  2. <b>점과 화살표</b> — 지금 손가락 위치와 밀고 있는 방향.
    ///  3. <b>고스트</b> — 보드 위, 다음 한 칸으로 갈 자리. 손가락이 판을 가려도
    ///     어디로 가는지 알 수 있다. 그 칸에 적이 있으면 "공격" 색으로 바뀐다.
    ///
    /// 참조를 비워 둬도 동작한다 — 씬 배선 없이 붙이기만 하면 되게 런타임에 스스로 찾는다.
    /// 조작 표시는 안 보이면 바로 티가 나는 물건이라, 조용히 꺼져 있는 쪽이 더 나쁘다.
    /// </summary>
    public sealed class SlideFeedbackView : MonoBehaviour
    {
        [Header("연결 (비워 두면 런타임에 찾는다)")]
        [SerializeField, Tooltip("구독할 입력 라우터.")]
        private InputRouter input;

        [SerializeField, Tooltip("플레이어 위치를 물어볼 세션.")]
        private GameSession session;

        [SerializeField, Tooltip("칸 좌표를 월드로 바꿔 줄 보드 뷰.")]
        private BoardView board;

        [SerializeField, Tooltip("스크린 좌표를 월드로 바꿀 카메라.")]
        private Camera worldCamera;

        [Header("손가락 표시")]
        [SerializeField, Tooltip("표시를 그릴지. 끄면 조작은 그대로고 표시만 사라진다.")]
        private bool showTouchGizmo = true;

        [SerializeField, Tooltip("링·점 색.")]
        private Color gizmoColor = new Color(1f, 1f, 1f, 0.45f);

        [SerializeField, Tooltip("방향 화살표 색.")]
        private Color arrowColor = new Color(1f, 0.93f, 0.55f, 0.9f);

        [SerializeField, Range(0.05f, 0.6f), Tooltip("점 크기(링 지름 대비).")]
        private float dotScale = 0.22f;

        [SerializeField, Range(0.1f, 1f), Tooltip("화살표 크기(링 지름 대비).")]
        private float arrowScale = 0.42f;

        [SerializeField, Tooltip("표시의 정렬 순서. 보드 위 무엇보다도 위에 있어야 한다.")]
        private int gizmoSortingOrder = 50;

        [Header("고스트 (다음에 갈 칸)")]
        [SerializeField, Tooltip("갈 자리를 보드 위에 미리 비출지.")]
        private bool showGhost = true;

        [SerializeField, Tooltip("빈 칸으로 갈 때의 색.")]
        private Color ghostMoveColor = new Color(1f, 1f, 1f, 0.35f);

        [SerializeField, Tooltip("그 칸에 무언가 있어 부딪히게 될 때의 색.")]
        private Color ghostAttackColor = new Color(1f, 0.35f, 0.25f, 0.5f);

        [SerializeField, Tooltip("보드 밖이라 못 가는 방향일 때의 색.")]
        private Color ghostBlockedColor = new Color(0.4f, 0.4f, 0.45f, 0.3f);

        [SerializeField, Range(0.1f, 1f), Tooltip("칸 대비 고스트 크기.")]
        private float ghostFill = 0.82f;

        [SerializeField, Tooltip("고스트의 정렬 순서. 블록보다는 위, 손가락 표시보다는 아래.")]
        private int ghostSortingOrder = 4;

        private SpriteRenderer _ring;
        private SpriteRenderer _dot;
        private SpriteRenderer _arrow;
        private SpriteRenderer _ghost;
        private Transform _gizmoRoot;
        private bool _wired;

        private void OnEnable()
        {
            ResolveReferences();

            if (input != null)
            {
                input.SlideChanged += Refresh;
                _wired = true;
            }
            else
            {
                Debug.LogWarning($"{nameof(SlideFeedbackView)}: InputRouter를 찾지 못해 표시가 꺼집니다.", this);
            }

            HideAll();
        }

        private void OnDisable()
        {
            if (_wired && input != null)
            {
                input.SlideChanged -= Refresh;
            }

            _wired = false;
            HideAll();
        }

        private void ResolveReferences()
        {
            // 같은 오브젝트에 얹혀 있는 게 기본 배치라 GetComponent가 가장 싸다.
            if (input == null) input = GetComponent<InputRouter>();
            if (session == null) session = GetComponent<GameSession>();

            if (input == null) input = FindFirstObjectByType<InputRouter>();
            if (session == null) session = FindFirstObjectByType<GameSession>();
            if (board == null) board = FindFirstObjectByType<BoardView>();
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void Refresh()
        {
            InputRouter.SlideState slide = input.Slide;

            if (!slide.Active)
            {
                HideAll();
                return;
            }

            DrawTouchGizmo(slide);
            DrawGhost(slide);
        }

        private void DrawTouchGizmo(InputRouter.SlideState slide)
        {
            if (!showTouchGizmo || worldCamera == null)
            {
                return;
            }

            // 링 지름 = 한 칸 나가는 거리. 화면에서 재는 값이라 월드 크기로 환산해서 쓴다.
            float worldPerPixel = WorldPerPixel();
            float diameter = input.ThresholdPixels * 2f * worldPerPixel;

            Vector3 origin = ScreenToWorld(slide.Origin);
            Vector3 current = ScreenToWorld(slide.Current);

            Ring().gameObject.SetActive(true);
            _ring.transform.position = origin;
            _ring.transform.localScale = new Vector3(diameter, diameter, 1f);
            _ring.color = gizmoColor;

            Dot().gameObject.SetActive(true);
            _dot.transform.position = current;
            float dot = diameter * dotScale;
            _dot.transform.localScale = new Vector3(dot, dot, 1f);
            _dot.color = gizmoColor;

            if (!slide.HasDirection)
            {
                Arrow().gameObject.SetActive(false);
                return;
            }

            Vector2 screenDir = InputRouter.ToScreenVector(slide.Direction);
            float radius = diameter * 0.5f;

            Arrow().gameObject.SetActive(true);
            _arrow.transform.position = origin + new Vector3(screenDir.x, screenDir.y, 0f) * (radius * 1.15f);
            _arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg);

            // 임계값에 가까워질수록 화살표가 또렷해진다 — 얼마나 더 밀어야 하는지가 보인다.
            float t = Mathf.Lerp(0.6f, 1f, slide.Progress);
            float arrow = diameter * arrowScale * t;
            _arrow.transform.localScale = new Vector3(arrow, arrow, 1f);

            Color c = arrowColor;
            c.a *= Mathf.Lerp(0.45f, 1f, slide.Progress);
            _arrow.color = c;
        }

        private void DrawGhost(InputRouter.SlideState slide)
        {
            if (!showGhost || board == null || board.CellRoot == null || !slide.HasDirection)
            {
                HideGhost();
                return;
            }

            GameLoop loop = session != null ? session.Loop : null;
            if (loop == null || !loop.IsReady || loop.IsOver || loop.Player == null)
            {
                HideGhost();
                return;
            }

            Vector2Int target = loop.Player.Position + slide.Direction.ToOffset();

            Ghost().gameObject.SetActive(true);

            if (!loop.Grid.InBounds(target))
            {
                // 보드 밖이면 갈 자리가 없다. 밀고 있는 쪽 가장자리에 흐린 표시만 남긴다.
                _ghost.transform.localPosition = board.CellToLocalPosition(loop.Player.Position);
                _ghost.color = ghostBlockedColor;
            }
            else
            {
                _ghost.transform.localPosition = board.CellToLocalPosition(target);
                _ghost.color = loop.Grid.IsEmpty(target) ? ghostMoveColor : ghostAttackColor;
            }

            float size = board.CellSize * ghostFill;
            _ghost.transform.localScale = new Vector3(size, size, 1f);
        }

        private float WorldPerPixel()
        {
            if (worldCamera != null && worldCamera.orthographic && Screen.height > 0)
            {
                return worldCamera.orthographicSize * 2f / Screen.height;
            }

            return 0.01f;
        }

        private Vector3 ScreenToWorld(Vector2 screen)
        {
            float depth = worldCamera != null ? Mathf.Abs(worldCamera.transform.position.z) : 10f;
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            world.z = 0f;
            return world;
        }

        private Transform GizmoRoot()
        {
            if (_gizmoRoot != null)
            {
                return _gizmoRoot;
            }

            var go = new GameObject("SlideGizmos");
            go.transform.SetParent(null, false);
            _gizmoRoot = go.transform;
            return _gizmoRoot;
        }

        private SpriteRenderer Ring() => _ring != null ? _ring : (_ring = Make("SlideRing", SlideGizmoSprites.Ring, gizmoSortingOrder, GizmoRoot()));

        private SpriteRenderer Dot() => _dot != null ? _dot : (_dot = Make("SlideDot", SlideGizmoSprites.Dot, gizmoSortingOrder + 1, GizmoRoot()));

        private SpriteRenderer Arrow() => _arrow != null ? _arrow : (_arrow = Make("SlideArrow", SlideGizmoSprites.Arrow, gizmoSortingOrder + 2, GizmoRoot()));

        private SpriteRenderer Ghost() => _ghost != null ? _ghost : (_ghost = Make("MoveGhost", PlaceholderSprite.Square, ghostSortingOrder, board.CellRoot));

        private static SpriteRenderer Make(string name, Sprite sprite, int sortingOrder, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void HideAll()
        {
            if (_ring != null) _ring.gameObject.SetActive(false);
            if (_dot != null) _dot.gameObject.SetActive(false);
            if (_arrow != null) _arrow.gameObject.SetActive(false);
            HideGhost();
        }

        private void HideGhost()
        {
            if (_ghost != null)
            {
                _ghost.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_gizmoRoot != null)
            {
                Destroy(_gizmoRoot.gameObject);
            }
        }
    }
}
