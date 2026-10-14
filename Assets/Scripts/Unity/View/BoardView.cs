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

        [SerializeField, Tooltip("플레이어 전용 연출(기울기·돌진 등)을 재생하는 프리팹. " +
                                 "Player 엔티티의 뷰가 처음 만들어질 때 그 밑에 자식(Visual)으로 붙는다. " +
                                 "비우면 연출 없이 기존 방식(직접 스프라이트 표시)대로 그린다.")]
        private PlayerVisualFeedback playerVisualPrefab;

        [SerializeField, Tooltip("몬스터·아이템(물약) 전용 연출(숨쉬기·피격·공격·사망 등)을 재생하는 프리팹. " +
                                 "Trash 엔티티의 뷰가 처음 만들어질 때 그 밑에 자식(Visual)으로 붙는다. " +
                                 "비우면 연출 없이 기존 방식대로 그린다.")]
        private MonsterVisualFeedback monsterVisualPrefab;

        [SerializeField, Tooltip("TrashEntry.feedback을 안 꽂아 둔 종류가 쓸 기본 연출 수치. " +
                                 "이것도 비어 있으면 그 종류는 연출 없이(스폰/착지/피격/공격/사망 전부 생략) 그려진다.")]
        private MonsterFeedbackSettings defaultMonsterFeedback;

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

        [SerializeField, Min(1f), Tooltip("EmphasizedEntity로 지정된 상품의 가격 숫자 배율. " +
                                          "플레이어가 가까이 간 상품만 눈에 띄게 커 보이게 한다(상점 기믹). 1이면 효과 없음.")]
        private float emphasizedFuseScale = 1.8f;

        [SerializeField, Tooltip("상점 상품 가격 숫자를 칸 중심에서 위로 띄울 거리(칸 크기 배수). " +
                                 "상품을 가리지 않고 머리 위에 뜨게 한다. 0이면 칸 한가운데.")]
        private float shopPriceYOffset = 0.75f;

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

            /// <summary>
            /// 이 슬롯이 플레이어를 그리고 있을 때만 쓰는 연출 컴포넌트. Renderer의 자식(Visual)이다.
            /// 슬롯은 종류를 가리지 않고 재사용되므로(풀링), 한 번 만들어지면 계속 붙어 있고
            /// ApplyVisual이 매번 지금 종류에 맞게 켜고 끈다.
            /// </summary>
            public PlayerVisualFeedback PlayerVisual;

            /// <summary>
            /// 이 슬롯이 몬스터/아이템(Trash)을 그리고 있을 때만 쓰는 연출 컴포넌트.
            /// PlayerVisual과 마찬가지로 한 번 만들어지면 계속 붙어 있다.
            /// </summary>
            public MonsterVisualFeedback MonsterVisual;

            /// <summary>
            /// 사망/획득 연출이 도는 중이라 그리드에서는 이미 사라졌지만 아직 풀에 돌려주지
            /// 않은 상태. Sync의 stale 처리가 이 슬롯을 다시 집어도 또 Release하거나
            /// 연출을 다시 재생하지 않게 막는다.
            /// </summary>
            public bool ReleasePending;

            /// <summary>
            /// 막 스폰돼 PlaySpawn이 재생 중이라, 보드 위쪽에서 떨어져 내려와 자리를 잡는
            /// 첫 정착에는 PlayLand(착지 스쿼시)를 건너뛰어야 함. 스폰 팝업과 겹치면
            /// 같은 scale 채널을 두고 서로 죽이며 튀어 보인다.
            /// </summary>
            public bool SkipNextLandAnimation;
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
        /// 이번 스텝의 방향. DirectedStep이 Stepped보다 먼저 울리므로(GameSession 참조),
        /// Sync가 몬스터 피격 넉백 방향을 고를 때 이미 최신값이다. 방향 없는 스텝(대기 등)엔
        /// 바뀌지 않고 직전 값을 그대로 쓴다 — 아주 드문 경우(폭탄/독 피해)의 넉백 방향이
        /// 다소 부정확할 수 있지만, 피격 연출 자체는 정상적으로 재생된다.
        /// </summary>
        private Direction _lastStepDirection;

        /// <summary>이번 스텝에 반격 돌진(PlayAttack)을 재생한 몬스터. 같은 스텝에 PlayHit과 겹치지 않게 막는다.</summary>
        private Entity _lastRetaliator;

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

        private Entity _emphasizedEntity;

        /// <summary>
        /// 가격 숫자를 키워서 강조할 엔티티(상점 상품). ShopRoom이 "플레이어와 가까운 상품"을
        /// 매 스텝 여기에 꽂는다. null이면 아무것도 강조하지 않는다.
        ///
        /// 세터에서 옛/새 슬롯을 깨우는 이유: 정착한 엔티티는 LateUpdate가 매 프레임 건너뛰므로
        /// (Hard Rule 8), 값이 바뀐 바로 그 프레임에 다시 그리라고 Settled를 풀어 줘야 한다.
        /// </summary>
        public Entity EmphasizedEntity
        {
            get => _emphasizedEntity;
            set
            {
                if (_emphasizedEntity == value)
                {
                    return;
                }

                WakeSlot(_emphasizedEntity);
                _emphasizedEntity = value;
                WakeSlot(_emphasizedEntity);
            }
        }

        private void WakeSlot(Entity entity)
        {
            if (entity == null)
            {
                return;
            }

            ViewSlot slot;
            if (_views.TryGetValue(entity, out slot))
            {
                slot.Settled = false;
            }
        }

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
            session.DirectedStep += HandleDirectedStep;

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
            session.DirectedStep -= HandleDirectedStep;
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

                bool wasSettled = slot.Settled;
                slot.Position = Vector3.Lerp(slot.Position, target, t);

                // 충분히 가까워지면 목표에 딱 붙이고 정착으로 표시한다.
                slot.Settled = (slot.Position - target).sqrMagnitude <= snapSqr;
                if (slot.Settled)
                {
                    slot.Position = target;

                    // 막 내려와 자리를 잡은 순간 — 몇 칸을 떨어졌든 한 칸 내려설 때마다 다시 불린다
                    // (한 스텝에 한 칸씩만 내려가므로 GravityResolver와 자연히 맞물린다).
                    // 방금 스폰된 슬롯은 PlaySpawn의 팝업과 겹치지 않게 이번 한 번만 건너뛴다.
                    if (!wasSettled && slot.MonsterVisual != null)
                    {
                        if (slot.SkipNextLandAnimation)
                        {
                            slot.SkipNextLandAnimation = false;
                        }
                        else
                        {
                            slot.MonsterVisual.PlayLand();
                        }
                    }
                }

                bool fullyRevealed = ApplyReveal(slot, revealLine, fullSize);
                LayoutHearts(slot, fullyRevealed);
                LayoutFuse(slot, pair.Key, fullyRevealed);
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
            // 같은 숫자 라벨을 둘이 나눠 쓴다: 폭탄은 남은 턴, 상점 상품은 가격.
            // 둘 다 "칸 위에 숫자 하나"라 굳이 별도 풀을 둘 이유가 없다.
            int value = -1;

            var bomb = entity as Bomb;
            if (showFuseNumber && bomb != null && bomb.IsArmed)
            {
                value = bomb.FuseRemaining;
            }
            else
            {
                var shopItem = entity as ShopItem;
                if (shopItem != null)
                {
                    value = shopItem.Price;
                }
            }

            if (value < 0)
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

            if (slot.Fuse == null || slot.ShownFuse == value)
            {
                return;
            }

            slot.ShownFuse = value;
            slot.Fuse.text = value >= 0 && value < FuseLabels.Length
                ? FuseLabels[value]
                : value.ToString();
        }

        /// <summary>
        /// 숫자를 폭탄 칸 한가운데에 올린다. 상점 상품의 가격은 상품 머리 위에 띄운다.
        /// entity가 EmphasizedEntity면 더 크게 그린다.
        /// </summary>
        private void LayoutFuse(ViewSlot slot, Entity entity, bool visible)
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

            float scale = fuseTextScale
                * (_emphasizedEntity != null && entity == _emphasizedEntity ? emphasizedFuseScale : 1f);

            Vector3 position = slot.Position;
            if (entity is ShopItem)
            {
                position.y += shopPriceYOffset * cellSize;
            }

            tr.localPosition = position;
            tr.localScale = new Vector3(scale, scale, 1f);
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

            // 상품 색이 "살 수 있는가"로 갈리므로 보유 골드를 읽을 통로를 꽂아 준다.
            if (visuals != null)
            {
                visuals.BindGoldSource(delegate { return _loop != null ? _loop.Player.Gold : 0; });
            }

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

        /// <summary>
        /// 방향 입력 한 번의 결과. 이동/공격일 때 플레이어 연출을, 반격을 받았으면 그 몬스터의
        /// 공격 연출을 재생한다. Sync(HandleStepped)보다 <b>먼저</b> 불린다 — 이 방향을
        /// _lastStepDirection에 먼저 캐시해 둬야, 뒤이어 도는 Sync가 몬스터 피격 넉백 방향을
        /// 정확히 고를 수 있다(GameSession이 DirectedStep을 Stepped보다 먼저 울린다).
        /// </summary>
        private void HandleDirectedStep(Direction direction, StepResult result)
        {
            _lastStepDirection = direction;
            _lastRetaliator = null;

            if (_loop == null)
            {
                return;
            }

            Vector2Int worldDir = ToWorldDirection(direction);
            Player player = _loop.Player;

            if (player != null && _views.TryGetValue(player, out ViewSlot playerSlot) && playerSlot.PlayerVisual != null)
            {
                switch (result.Move)
                {
                    case MoveOutcome.Moved:
                        playerSlot.PlayerVisual.PlayMove(worldDir);
                        break;

                    case MoveOutcome.Attacked:
                        playerSlot.PlayerVisual.PlayAttack(worldDir, null);
                        break;
                }
            }

            // 반격(CORE_COMBAT.md §5-5)은 부딪힌 그 한 마리만, 실제로 피해를 입혔을 때만 한다.
            // 0피해 "반격"(공격력 0인 벽)까지 돌진 애니메이션을 태우면 가만히 있어야 할 벽이
            // 플레이어 쪽으로 튀어나가는 것처럼 보인다.
            if (player == null || result.Move != MoveOutcome.Attacked || result.DamageTaken <= 0)
            {
                return;
            }

            Vector2Int bumpedCell = player.Position + direction.ToOffset();
            if (!_loop.Grid.InBounds(bumpedCell))
            {
                return;
            }

            Entity bumped = _loop.Grid[bumpedCell];
            if (bumped == null || !_views.TryGetValue(bumped, out ViewSlot bumpedSlot) || bumpedSlot.MonsterVisual == null)
            {
                return;
            }

            _lastRetaliator = bumped;
            Vector2Int towardPlayer = new Vector2Int(-worldDir.x, -worldDir.y);
            bumpedSlot.MonsterVisual.PlayAttack(towardPlayer, null);
        }

        /// <summary>논리 방향을 <b>월드</b> 기준 단위 벡터 성분으로. Direction.ToOffset()은 그리드(행)
        /// 기준이라 Up이 y=-1인데, 연출은 화면 좌표를 써야 하므로 따로 둔다.</summary>
        private static Vector2Int ToWorldDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return new Vector2Int(0, 1);
                case Direction.Down: return new Vector2Int(0, -1);
                case Direction.Left: return new Vector2Int(-1, 0);
                default: return new Vector2Int(1, 0);
            }
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

                    bool isNewSlot = !_views.TryGetValue(entity, out ViewSlot slot);
                    if (isNewSlot)
                    {
                        slot = Rent();
                        _views.Add(entity, slot);
                        ApplyVisual(entity, slot);

                        // 새 쓰레기는 보드 위쪽 바깥에서 시작해야 "떨어져 내리는" 것으로 보인다.
                        bool fallsIn = dropFromAbove && entity.Kind != EntityKind.Player;
                        slot.Position = fallsIn
                            ? CellToLocal(new Vector2Int(entity.Position.x, -1))
                            : CellToLocal(entity.Position);
                        slot.Settled = false;

                        // 스폰 팝업이 따로 돈다 — 떨어져 내려와 처음 자리를 잡을 때는
                        // 착지 스쿼시(PlayLand)를 건너뛴다. 같은 scale 채널을 두고 서로
                        // 죽이며 튀어 보이기 때문이다(LateUpdate 참조).
                        slot.SkipNextLandAnimation = true;
                        slot.MonsterVisual?.PlaySpawn();
                    }
                    else if (snap)
                    {
                        slot.Position = CellToLocal(entity.Position);
                        slot.Settled = false;
                    }

                    // 폭탄은 도화선이 줄면 색이 달라진다. 뷰를 만들 때 한 번 칠하고 끝내면
                    // 카운트다운이 화면에 안 보이므로, 폭탄만 매번 다시 칠한다(몇 개 안 된다).
                    // 상품도 매번 칠한다 — 돈을 쓰면 "살 수 있음/없음" 색이 바뀌어야 한다.
                    if (entity.Kind == EntityKind.Bomb || entity.Kind == EntityKind.ShopItem)
                    {
                        ApplyVisual(entity, slot);
                    }

                    // 기존에 있던 몬스터/아이템만 피격 판정을 한다 — 막 만들어진 슬롯은 풀에서
                    // 재사용됐을 수 있어 ShownHp가 직전 점유자의 값일 수 있다(오탐 방지).
                    // 반격으로 PlayAttack이 이미 재생 중인 몬스터는 넉백과 겹치지 않게 뺀다.
                    if (!isNewSlot && slot.MonsterVisual != null && !ReferenceEquals(entity, _lastRetaliator))
                    {
                        var health = entity as IHasHealth;
                        if (health != null && slot.ShownHp > health.Hp)
                        {
                            slot.MonsterVisual.PlayHit(ToWorldDirection(_lastStepDirection));
                        }
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
                Entity entity = _stale[i];
                ViewSlot slot = _views[entity];

                if (slot.ReleasePending)
                {
                    continue;   // 사망/획득 연출이 아직 도는 중 — 끝나면 콜백이 정리한다
                }

                if (TryPlayExitAnimation(entity, slot))
                {
                    slot.ReleasePending = true;
                    continue;
                }

                Release(slot);
                _views.Remove(entity);
            }
        }

        /// <summary>
        /// 그리드에서 이미 사라진 엔티티를 바로 풀에 돌려주는 대신, 몬스터/아이템이면
        /// 사망·획득 연출을 먼저 재생한다. 연출이 끝나야(콜백) 실제로 Release + _views.Remove가
        /// 일어난다 — 그 사이엔 Sync가 매번 이 엔티티를 다시 stale로 집어도 ReleasePending이
        /// 막아 주므로 중복 재생되지 않는다.
        /// </summary>
        /// <returns>연출을 재생하기 시작했으면 true(= 아직 풀에 돌려주면 안 된다).</returns>
        private bool TryPlayExitAnimation(Entity entity, ViewSlot slot)
        {
            if (slot.MonsterVisual == null)
            {
                return false;
            }

            var trash = entity as Trash;
            if (trash == null)
            {
                return false;
            }

            // 사라지는 애 밑에 빈 하트 줄/도화선 숫자가 남아 있을 이유가 없다.
            ReleaseHearts(slot);
            ReleaseFuse(slot);

            if (trash.IsConsumable)
            {
                slot.MonsterVisual.PlayPickup(() => FinishDeferredRelease(entity, slot));
            }
            else
            {
                slot.MonsterVisual.PlayDeath(() => FinishDeferredRelease(entity, slot));
            }

            return true;
        }

        private void FinishDeferredRelease(Entity entity, ViewSlot slot)
        {
            slot.ReleasePending = false;
            Release(slot);
            _views.Remove(entity);
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

        private void ApplyVisual(Entity entity, ViewSlot slot)
        {
            bool isPlayer = entity.Kind == EntityKind.Player;
            bool isTrash = !isPlayer && entity is Trash;

            if (isPlayer)
            {
                EnsurePlayerVisual(slot);
            }
            else if (isTrash)
            {
                EnsureMonsterVisual(slot);
            }

            // 전용 Visual이 있으면 그쪽에 그린다 — 루트(slot.Renderer)는 위치/판정만 맡고,
            // 보이는 건 Visual이 전담해야 연출(기울기·돌진·숨쉬기 등)이 루트 좌표와 안 섞인다.
            SpriteRenderer playerView = isPlayer && slot.PlayerVisual != null ? slot.PlayerVisual.SpriteRenderer : null;
            SpriteRenderer monsterView = isTrash && slot.MonsterVisual != null ? slot.MonsterVisual.SpriteRenderer : null;
            SpriteRenderer view = playerView != null ? playerView : (monsterView != null ? monsterView : slot.Renderer);

            // 루트 자신의 렌더러는 전용 Visual이 대신 그릴 때만 끈다. 슬롯은 종류를 가리지 않고
            // 재사용되므로(풀링) 과거엔 다른 종류였을 수도, 지금 막 그 종류가 됐을 수도 있다 —
            // 매번 지금 종류에 맞게 다시 맞춘다.
            slot.Renderer.enabled = view == slot.Renderer;
            if (slot.PlayerVisual != null)
            {
                slot.PlayerVisual.gameObject.SetActive(isPlayer);
            }

            if (slot.MonsterVisual != null)
            {
                slot.MonsterVisual.gameObject.SetActive(isTrash);
            }

            Color color = Color.white;
            Sprite sprite = null;

            // 플레이어는 지금 고른 캐릭터의 스프라이트를 먼저 써 본다 — 캐릭터마다 그림이
            // 다르므로(기획: 개성은 공격 범위 + 겉모습) EntityVisualSet의 단일 플레이어 슬롯보다
            // 우선한다. 캐릭터에 아이콘을 안 꽂았으면(아직 아트 전) 기존 경로로 떨어진다.
            Sprite characterIcon = isPlayer && session != null
                ? session.ActiveCharacterConfig?.Icon
                : null;

            if (characterIcon != null)
            {
                sprite = characterIcon;
            }
            else if (visuals != null)
            {
                visuals.Resolve(entity, out color, out sprite);
            }

            view.sprite = sprite != null ? sprite : PlaceholderSprite.Square;
            view.color = color;

            if (isTrash && slot.MonsterVisual != null)
            {
                var trash = (Trash)entity;
                MonsterFeedbackSettings feedback = visuals != null ? visuals.ResolveFeedback(trash.Type) : null;
                slot.MonsterVisual.Configure(feedback != null ? feedback : defaultMonsterFeedback);
                slot.MonsterVisual.SetBaseColor(color);
            }

            // localScale은 여기서 안 잡는다 — 같은 프레임의 LateUpdate가 ApplyReveal로 덮어쓴다.

#if UNITY_EDITOR
            // 하이어라키에서 알아보기 위한 이름. 순수 디버깅용이라 에디터에서만 붙인다.
            // $"Trash_{type}"은 부를 때마다 enum 박싱 + 문자열 할당을 만든다(Hard Rule 8).
            slot.Renderer.name = NameFor(entity);
#endif
        }

        /// <summary>
        /// 플레이어 뷰 밑에 연출용 Visual 자식을 한 번만 만든다. 이미 있으면(슬롯 재사용)
        /// 아무것도 안 한다. 프리팹을 안 꽂아 뒀으면 조용히 포기한다 — 그러면 ApplyVisual이
        /// 루트에 직접 그리는 기존 방식으로 자동 대체된다(연출만 없을 뿐 렌더링은 멀쩡하다).
        /// </summary>
        private void EnsurePlayerVisual(ViewSlot slot)
        {
            if (slot.PlayerVisual != null || playerVisualPrefab == null)
            {
                return;
            }

            slot.PlayerVisual = Instantiate(playerVisualPrefab, slot.Renderer.transform);

            Transform visual = slot.PlayerVisual.transform;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;

#if UNITY_EDITOR
            visual.name = "Visual";
#endif
        }

        /// <summary>
        /// 몬스터/아이템 뷰 밑에 연출용 Visual 자식을 한 번만 만든다. 로컬 y를 -0.5로 두는 이유:
        /// 이 트랜스폼 자체가 숨쉬기 스케일의 피벗이 되므로, 칸의 "발 밑"에 둬야 스케일이
        /// 커져도 발이 땅에서 뜨지 않는다(프리팹 안의 Sprite 자식이 반대로 +0.5만큼 올라가
        /// 있어서, 정지 상태의 보이는 위치는 칸 한가운데 그대로다).
        /// </summary>
        private void EnsureMonsterVisual(ViewSlot slot)
        {
            if (slot.MonsterVisual != null || monsterVisualPrefab == null)
            {
                return;
            }

            slot.MonsterVisual = Instantiate(monsterVisualPrefab, slot.Renderer.transform);

            Transform visual = slot.MonsterVisual.transform;
            visual.localPosition = new Vector3(0f, -0.5f, 0f);
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;

#if UNITY_EDITOR
            visual.name = "Visual";
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

            if (entity.Kind == EntityKind.Trap)
            {
                return "Trap";
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

            // 트윈이 도는 채로 풀에 돌아가면, 다음에 이 슬롯을 다른 종류가 받았을 때
            // 자식(Visual)이 비뚤어지거나 밀려난 자세로 남아 있을 수 있다.
            if (slot.PlayerVisual != null)
            {
                slot.PlayerVisual.SnapIdle();
            }

            if (slot.MonsterVisual != null)
            {
                slot.MonsterVisual.SnapIdle();
            }

            slot.ReleasePending = false;
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
