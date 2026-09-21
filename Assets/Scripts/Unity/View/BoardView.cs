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

        [Header("폭탄 카운트다운")]
        [SerializeField, Tooltip("불이 붙은 폭탄 위에 남은 턴 숫자를 띄울지.")]
        private bool showFuseNumber = true;

        [SerializeField, Tooltip("숫자 색.")]
        private Color fuseTextColor = Color.white;

        [SerializeField, Min(0.001f), Tooltip("숫자 크기. 칸 크기에 맞춰 조절한다. " +
                                              "TextMesh는 글리프 단위가 픽셀이라 이 값으로 월드 크기를 맞춘다.")]
        private float fuseTextScale = 0.05f;

        [SerializeField, Tooltip("숫자의 정렬 순서. 폭탄보다 위여야 보인다.")]
        private int fuseSortingOrder = 3;

        [Header("체력 하트")]
        [SerializeField, Tooltip("엔티티 밑에 체력 하트를 그릴지. 끄면 하트를 아예 만들지 않는다.")]
        private bool showHealthHearts = true;

        [SerializeField, Min(0.01f), Tooltip("하트 한 개의 크기(월드). 한 줄이 칸 폭을 넘으면 자동으로 줄어든다.")]
        private float heartSize = 0.2f;

        [SerializeField, Min(0f), Tooltip("하트 사이 간격(월드).")]
        private float heartSpacing = 0.04f;

        [SerializeField, Tooltip("칸 중심에서 하트 줄까지의 세로 오프셋(칸 크기 비율). 음수 = 아래.")]
        private float heartYOffset = -0.36f;

        [SerializeField, Range(0.1f, 1f), Tooltip("하트 줄이 차지할 수 있는 최대 폭(칸 폭 비율). " +
                                                  "체력이 많으면 이 폭에 맞춰 하트가 균등하게 작아진다.")]
        private float heartRowMaxWidth = 0.95f;

        [SerializeField, Tooltip("하트의 정렬 순서. 엔티티 스프라이트보다 커야 위에 그려진다.")]
        private int heartSortingOrder = 1;

        [SerializeField, Min(0f), Tooltip("목표 칸에 이 거리보다 가까워지면 딱 붙이고 보간을 멈춘다. " +
                                          "멈춘 뒤로는 transform을 갱신하지 않는다 — 보드가 정지해 있을 때의 " +
                                          "매 프레임 비용이 0이 된다. 너무 키우면 이동이 뚝 끊겨 보인다.")]
        private float snapDistance = 0.002f;

        /// <summary>
        /// 뷰 하나의 상태. 잘린 위치가 아니라 <b>진짜 위치</b>를 따로 들고 있어야
        /// 다음 프레임 보간이 어긋나지 않는다(ApplyReveal이 transform을 아래로 당기기 때문).
        /// </summary>
        private sealed class ViewSlot
        {
            public SpriteRenderer Renderer;
            public Vector3 Position;

            /// <summary>
            /// 목표 칸에 도달해 더 움직일 게 없는 상태.
            /// 이게 서 있는 동안은 transform을 아예 건드리지 않는다(LateUpdate 참조).
            /// </summary>
            public bool Settled;

            /// <summary>이 엔티티의 체력 하트들. 최대 체력만큼 쓰고, 남는 칸은 풀에 돌려준다.</summary>
            public SpriteRenderer[] Hearts;

            /// <summary>지금 쓰고 있는 하트 수(= 엔티티의 MaxHp).</summary>
            public int HeartCount;

            /// <summary>마지막으로 그린 체력. 값이 그대로면 스프라이트를 다시 안 바꾼다.</summary>
            public int ShownHp;

            /// <summary>폭탄 위에 뜨는 남은 턴 숫자. 폭탄이 아닌 칸은 null이다.</summary>
            public TextMesh Fuse;

            /// <summary>마지막으로 찍은 숫자. 같으면 문자열을 새로 만들지 않는다(Hard Rule 8).</summary>
            public int ShownFuse;
        }

        private readonly Dictionary<Entity, ViewSlot> _views = new Dictionary<Entity, ViewSlot>(128);
        private readonly Stack<ViewSlot> _pool = new Stack<ViewSlot>(64);
        private readonly Stack<SpriteRenderer> _heartPool = new Stack<SpriteRenderer>(128);
        private readonly Stack<TextMesh> _fusePool = new Stack<TextMesh>(8);

        /// <summary>
        /// 숫자를 미리 문자열로 만들어 둔다. int.ToString()은 부를 때마다 할당을 만드는데,
        /// 카운트다운은 매 스텝 바뀌므로 그걸 피한다(Hard Rule 8).
        /// 범위를 넘는 값은 드물어서 그때만 ToString을 쓴다.
        /// </summary>
        private static readonly string[] FuseLabels = BuildFuseLabels();

        private static string[] BuildFuseLabels()
        {
            var labels = new string[10];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = i.ToString();
            }

            return labels;
        }

        /// <summary>내장 폰트. 에셋을 따로 임포트하지 않으려고 런타임 기본 폰트를 쓴다.</summary>
        private static Font _builtinFont;

        private static Font BuiltinFont
        {
            get
            {
                if (_builtinFont == null)
                {
                    // TextMeshPro는 필수 리소스를 프로젝트에 임포트해야 해서 피했다.
                    // 이건 엔진에 내장된 폰트라 추가 에셋이 없다.
                    _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return _builtinFont;
            }
        }
        private readonly HashSet<Entity> _alive = new HashSet<Entity>();
        private readonly List<Entity> _stale = new List<Entity>(32);

        private GameLoop _loop;

        // 레이아웃 값이 인스펙터에서 바뀌면 정착한 뷰도 한 번은 다시 그려야 한다.
        // NaN으로 시작해 첫 프레임은 무조건 '바뀐 것'으로 잡히게 한다.
        private float _lastRevealLine = float.NaN;
        private float _lastFullSize = float.NaN;

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

        /// <summary>
        /// 칸 스프라이트가 들어가는 부모. 폭탄 설치 표시처럼 보드 위에 무언가를 더 얹을 때
        /// 같은 좌표계를 쓰라고 열어 둔다.
        /// </summary>
        public Transform CellRoot => cellRoot;

        /// <summary>한 칸의 월드 크기.</summary>
        public float CellSize => cellSize;

        /// <summary>칸 좌표 -> cellRoot 기준 로컬 위치. 보드 위에 마커를 놓을 때 쓴다.</summary>
        public Vector3 CellToLocalPosition(Vector2Int cell) => CellToLocal(cell);

        /// <summary>
        /// 화면에서 찍은 월드 좌표가 어느 칸인지 되돌린다. 보드를 탭해서 고를 때 쓴다.
        /// </summary>
        /// <returns>보드 안이면 true.</returns>
        public bool TryWorldToCell(Vector3 world, out Vector2Int cell)
        {
            cell = default;

            if (_loop == null || cellRoot == null || cellSize <= 0f)
            {
                return false;
            }

            Vector3 local = cellRoot.InverseTransformPoint(world);

            // CellToLocal의 역산이다. 좌상단 원점이라 y는 부호가 뒤집힌다.
            float x = (local.x / cellSize) + ((_loop.Grid.Cols - 1) * 0.5f);
            float y = ((_loop.Grid.Rows - 1) * 0.5f) - (local.y / cellSize);

            var candidate = new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
            if (!_loop.Grid.InBounds(candidate))
            {
                return false;
            }

            cell = candidate;
            return true;
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

            // 레이아웃 값이 바뀌었으면 이번 프레임은 전부 다시 그린다.
            bool layoutChanged = !Mathf.Approximately(revealLine, _lastRevealLine)
                                 || !Mathf.Approximately(fullSize, _lastFullSize);
            _lastRevealLine = revealLine;
            _lastFullSize = fullSize;

            float t = moveSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-moveSmoothing * Time.deltaTime);
            float snapSqr = snapDistance * snapDistance;

            // Dictionary 열거자는 struct고 값(ViewSlot)은 참조형이라, 매 프레임 돌아도 할당이 없다.
            foreach (KeyValuePair<Entity, ViewSlot> pair in _views)
            {
                ViewSlot slot = pair.Value;
                Vector3 target = CellToLocal(pair.Key.Position);
                float distanceSqr = (slot.Position - target).sqrMagnitude;

                // 이미 도착해 있고 레이아웃도 그대로면 transform을 아예 건드리지 않는다.
                // 지수 보간은 목표에 정확히 닿지 않아서, 이 가지가 없으면 보드가 멈춰 있어도
                // 매 프레임 뷰 수만큼 localScale/localPosition을 쓰게 된다. SpriteRenderer는
                // 그때마다 바운즈를 다시 잡으므로 안드로이드에서 그냥 새는 비용이다(Hard Rule 8).
                if (slot.Settled && !layoutChanged && distanceSqr <= snapSqr)
                {
                    continue;
                }

                slot.Position = Vector3.Lerp(slot.Position, target, t);

                // 충분히 가까워지면 목표에 딱 붙이고 정착으로 표시한다.
                slot.Settled = (slot.Position - target).sqrMagnitude <= snapSqr;
                if (slot.Settled)
                {
                    slot.Position = target;
                }

                bool fullyRevealed = ApplyReveal(slot, revealLine, fullSize);
                LayoutHearts(slot, fullyRevealed);
                LayoutFuse(slot, fullyRevealed);
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
        /// <returns>스프라이트가 잘리지 않고 온전히 보이는지. 하트는 이때만 그린다.</returns>
        private bool ApplyReveal(ViewSlot slot, float revealLine, float fullSize)
        {
            Transform view = slot.Renderer.transform;

            float half = fullSize * 0.5f;
            float bottom = slot.Position.y - half;
            float visible = Mathf.Clamp(revealLine - bottom, 0f, fullSize);

            if (visible >= fullSize)
            {
                view.localScale = new Vector3(fullSize, fullSize, 1f);
                view.localPosition = slot.Position;
                return true;
            }

            view.localScale = new Vector3(fullSize, visible, 1f);
            view.localPosition = new Vector3(slot.Position.x, bottom + visible * 0.5f, slot.Position.z);
            return false;
        }

        // ── 체력 하트 ────────────────────────────────────────────────────
        //
        // 하트는 엔티티 스프라이트의 자식이 아니라 형제로 둔다. 프리뷰 줄을 자를 때
        // 엔티티 transform에 세로로 찌그러진 스케일이 들어가는데, 자식이면 하트까지 같이
        // 찌그러지기 때문이다(ApplyReveal 참조).

        /// <summary>
        /// 엔티티의 체력에 맞춰 하트 개수와 꽉참/빔 상태를 맞춘다. Sync에서만 부른다 —
        /// 체력은 스텝 단위로만 바뀌므로 매 프레임 확인할 필요가 없다.
        /// </summary>
        private void UpdateHearts(ViewSlot slot, Entity entity)
        {
            var health = entity as IHasHealth;
            bool wanted = showHealthHearts
                          && health != null
                          && visuals != null
                          && visuals.HasHeartSprites
                          && health.MaxHp > 0;

            int count = wanted ? health.MaxHp : 0;

            if (count != slot.HeartCount)
            {
                ResizeHearts(slot, count);
                slot.ShownHp = -1;      // 개수가 바뀌었으니 스프라이트도 다시 칠한다
                slot.Settled = false;   // 배치가 필요하므로 LateUpdate를 한 번 깨운다
            }

            if (count == 0 || slot.ShownHp == health.Hp)
            {
                return;
            }

            slot.ShownHp = health.Hp;
            for (int i = 0; i < count; i++)
            {
                slot.Hearts[i].sprite = i < health.Hp ? visuals.HeartFull : visuals.HeartEmpty;
            }
        }

        private void ResizeHearts(ViewSlot slot, int count)
        {
            if (slot.Hearts == null)
            {
                slot.Hearts = new SpriteRenderer[count];
            }
            else if (slot.Hearts.Length < count)
            {
                // 슬롯의 체력 상한이 늘어날 때만 도는 경로다(엔티티당 사실상 1회).
                System.Array.Resize(ref slot.Hearts, count);
            }

            for (int i = count; i < slot.HeartCount; i++)
            {
                ReleaseHeart(slot.Hearts[i]);
                slot.Hearts[i] = null;
            }

            for (int i = slot.HeartCount; i < count; i++)
            {
                slot.Hearts[i] = RentHeart();
            }

            slot.HeartCount = count;
        }

        /// <summary>
        /// 하트 줄을 엔티티 바로 밑에 가운데 정렬로 깐다.
        /// 줄이 칸 폭(heartRowMaxWidth)을 넘으면 하트와 간격을 같은 비율로 줄여 밀어 넣는다 —
        /// 최대 체력이 큰 엔티티도 옆 칸을 침범하지 않는다.
        /// </summary>
        private void LayoutHearts(ViewSlot slot, bool visible)
        {
            int count = slot.HeartCount;
            if (count <= 0)
            {
                return;
            }

            if (!visible)
            {
                // 프리뷰 줄에서 아직 잘려 있는 블록. 하트만 먼저 튀어나오면 어색하다.
                for (int i = 0; i < count; i++)
                {
                    slot.Hearts[i].gameObject.SetActive(false);
                }

                return;
            }

            float natural = count * heartSize + (count - 1) * heartSpacing;
            float maxWidth = heartRowMaxWidth * cellSize;
            float shrink = natural > maxWidth && natural > 0f ? maxWidth / natural : 1f;

            float size = heartSize * shrink;
            float step = size + heartSpacing * shrink;
            float startX = slot.Position.x - (count - 1) * step * 0.5f;
            float y = slot.Position.y + heartYOffset * cellSize;

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer heart = slot.Hearts[i];
                Transform tr = heart.transform;

                if (!heart.gameObject.activeSelf)
                {
                    heart.gameObject.SetActive(true);
                }

                tr.localPosition = new Vector3(startX + i * step, y, slot.Position.z);
                tr.localScale = HeartScale(heart, size);
            }
        }

        /// <summary>
        /// 스프라이트 원본 크기가 제각각이라 스케일을 그대로 쓰면 안 된다.
        /// 각 하트를 자기 경계 상자 기준으로 정규화해 한 줄의 하트가 같은 크기로 보이게 한다.
        /// </summary>
        private static Vector3 HeartScale(SpriteRenderer heart, float size)
        {
            Sprite sprite = heart.sprite;
            if (sprite == null)
            {
                return new Vector3(size, size, 1f);
            }

            Vector3 bounds = sprite.bounds.size;
            float longest = Mathf.Max(bounds.x, bounds.y);
            float k = longest > 0f ? size / longest : size;
            return new Vector3(k, k, 1f);
        }

        // ── 폭탄 카운트다운 ──────────────────────────────────────────────

        /// <summary>
        /// 불이 붙은 폭탄에 남은 턴 숫자를 붙이고, 그 외에는 떼어 낸다.
        /// 아직 안 붙은 폭탄은 숫자가 없다 — 카운트가 돌지 않으니 보여 줄 게 없다.
        /// </summary>
        private void UpdateFuse(ViewSlot slot, Entity entity)
        {
            var bomb = entity as Bomb;
            bool wanted = showFuseNumber && bomb != null && bomb.IsArmed;

            if (!wanted)
            {
                ReleaseFuse(slot);
                return;
            }

            if (slot.Fuse == null)
            {
                slot.Fuse = RentFuse();
                slot.ShownFuse = -1;
                slot.Settled = false;   // 배치가 필요하므로 LateUpdate를 한 번 깨운다
            }

            if (slot.Fuse == null || slot.ShownFuse == bomb.FuseRemaining)
            {
                return;
            }

            slot.ShownFuse = bomb.FuseRemaining;
            int value = bomb.FuseRemaining;
            slot.Fuse.text = value >= 0 && value < FuseLabels.Length
                ? FuseLabels[value]
                : value.ToString();
        }

        /// <summary>숫자를 폭탄 칸 한가운데에 올린다.</summary>
        private void LayoutFuse(ViewSlot slot, bool visible)
        {
            if (slot.Fuse == null)
            {
                return;
            }

            if (!visible)
            {
                // 프리뷰 줄에서 아직 잘려 있는 칸. 숫자만 먼저 떠 있으면 어색하다.
                if (slot.Fuse.gameObject.activeSelf)
                {
                    slot.Fuse.gameObject.SetActive(false);
                }

                return;
            }

            Transform tr = slot.Fuse.transform;
            if (!slot.Fuse.gameObject.activeSelf)
            {
                slot.Fuse.gameObject.SetActive(true);
            }

            tr.localPosition = slot.Position;
            tr.localScale = new Vector3(fuseTextScale, fuseTextScale, 1f);
        }

        private TextMesh RentFuse()
        {
            if (_fusePool.Count > 0)
            {
                TextMesh pooled = _fusePool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            if (cellRoot == null || BuiltinFont == null)
            {
                return null;
            }

            var go = new GameObject("BombFuseLabel");
            go.transform.SetParent(cellRoot, worldPositionStays: false);

            TextMesh text = go.AddComponent<TextMesh>();
            text.font = BuiltinFont;
            text.fontSize = 64;                 // 크게 렌더하고 스케일로 줄여야 또렷하다
            text.characterSize = 1f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = fuseTextColor;

            // 폰트가 자기 머티리얼을 들고 있다. 이걸 안 물리면 분홍색으로 나온다.
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltinFont.material;
            renderer.sortingOrder = fuseSortingOrder;

            return text;
        }

        private void ReleaseFuse(ViewSlot slot)
        {
            if (slot.Fuse == null)
            {
                return;
            }

            slot.Fuse.gameObject.SetActive(false);
            _fusePool.Push(slot.Fuse);
            slot.Fuse = null;
            slot.ShownFuse = -1;
        }

        private SpriteRenderer RentHeart()
        {
            SpriteRenderer heart = _heartPool.Count > 0 ? _heartPool.Pop() : null;
            if (heart == null)
            {
                heart = Instantiate(entityViewPrefab, cellRoot);
                heart.color = Color.white;
                heart.sortingOrder = heartSortingOrder;
            }

            heart.gameObject.SetActive(true);
            return heart;
        }

        private void ReleaseHeart(SpriteRenderer heart)
        {
            if (heart == null)
            {
                return;
            }

            heart.gameObject.SetActive(false);
            _heartPool.Push(heart);
        }

        private void ReleaseHearts(ViewSlot slot)
        {
            for (int i = 0; i < slot.HeartCount; i++)
            {
                ReleaseHeart(slot.Hearts[i]);
                slot.Hearts[i] = null;
            }

            slot.HeartCount = 0;
            slot.ShownHp = -1;
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
                        slot.Settled = false;
                    }
                    else if (snap)
                    {
                        slot.Position = CellToLocal(entity.Position);
                        slot.Settled = false;
                    }

                    // 폭탄은 도화선이 줄면 색이 달라진다. 뷰를 만들 때 한 번 칠하고 끝내면
                    // 카운트다운이 화면에 안 보이므로, 폭탄만 매번 다시 칠한다(몇 개 안 된다).
                    if (entity.Kind == EntityKind.Bomb)
                    {
                        ApplyVisual(entity, slot.Renderer);
                    }

                    // 체력과 도화선은 스텝 단위로만 바뀐다. 매 프레임이 아니라 여기서만 맞춘다.
                    UpdateHearts(slot, entity);
                    UpdateFuse(slot, entity);
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

            // localScale은 여기서 안 잡는다 — 같은 프레임의 LateUpdate가 ApplyReveal로 덮어쓴다.

#if UNITY_EDITOR
            // 하이어라키에서 알아보기 위한 이름. 순수 디버깅용이라 에디터에서만 붙인다.
            // $"Trash_{type}"은 부를 때마다 enum 박싱 + 문자열 할당을 만든다(Hard Rule 8).
            view.name = NameFor(entity);
#endif
        }

#if UNITY_EDITOR
        private const string PlayerViewName = "Player";
        private const string UnknownViewName = "Trash";

        /// <summary>종류별 이름을 최초 1회만 만들어 재사용한다.</summary>
        private static readonly string[] TrashViewNames = BuildTrashViewNames();

        private static string[] BuildTrashViewNames()
        {
            var values = (TrashType[])System.Enum.GetValues(typeof(TrashType));
            var names = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                names[i] = "Trash_" + values[i];
            }

            return names;
        }

        private static string NameFor(Entity entity)
        {
            if (entity.Kind == EntityKind.Player)
            {
                return PlayerViewName;
            }

            var trash = entity as Trash;
            if (trash == null)
            {
                return UnknownViewName;
            }

            int index = (int)trash.Type;
            return index >= 0 && index < TrashViewNames.Length ? TrashViewNames[index] : UnknownViewName;
        }
#endif

        private ViewSlot Rent()
        {
            ViewSlot slot = _pool.Count > 0 ? _pool.Pop() : new ViewSlot();
            if (slot.Renderer == null)
            {
                // Instantiate가 이미 cellRoot 밑에 붙여 준다. 반납할 때 부모를 떼지 않으므로
                // 재사용할 때 SetParent를 다시 부를 필요가 없다(계층 갱신 비용 회피).
                slot.Renderer = Instantiate(entityViewPrefab, cellRoot);
            }

            slot.Renderer.gameObject.SetActive(true);
            return slot;
        }

        private void Release(ViewSlot slot)
        {
            ReleaseHearts(slot);
            ReleaseFuse(slot);
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
