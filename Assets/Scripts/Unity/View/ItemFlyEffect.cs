using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 몬스터가 재료를 떨궜을 때의 <b>획득 연출</b>만 맡는다.
    ///
    /// 죽은 칸에서 아이콘이 톡 튀어나오고(팝), 잠깐 멈췄다가, 인벤토리의 그 아이템 칸으로
    /// 곡선을 그리며 날아간다. 닿는 순간 칸이 펀치 스케일되고 수량이 오른다.
    ///
    /// <b>데이터는 건드리지 않는다.</b> 인벤토리에는 ItemService가 드롭 즉시 이미 넣어 둔다.
    /// 여기는 "아직 날아오는 중인 개수"(<see cref="PendingCount"/>)만 들고 있고,
    /// InventoryView가 그만큼을 빼고 그린다. 그래서 연출이 중간에 끊겨도(웨이브 전환·씬 변경)
    /// 날아오던 몫만 0으로 돌리고 다시 그리면 끝이다 — 아이템은 하나도 사라지지 않는다.
    ///
    /// 연출은 턴을 막지 않는다. 입력·스텝과 무관하게 DOTween 위에서 따로 돈다.
    /// </summary>
    public sealed class ItemFlyEffect : MonoBehaviour
    {
        /// <summary>아이템별 아이콘. 표에 없는 아이템은 기본 아이콘으로 날아간다.</summary>
        [System.Serializable]
        public struct IconEntry
        {
            public string id;
            public Sprite sprite;
            public Color color;
        }

        [Header("연결")]
        [SerializeField, Tooltip("드롭 알림을 받을 세션. 웨이브마다 루프가 바뀌어 다시 구독한다.")]
        private GameSession session;

        [SerializeField, Tooltip("날아갈 목적지(아이템 칸)와 수량 표시를 가진 인벤토리.")]
        private InventoryView inventory;

        [SerializeField, Tooltip("죽은 칸의 월드 좌표를 얻을 보드.")]
        private BoardView board;

        [SerializeField, Tooltip("보드를 그리는 카메라. 비우면 Camera.main을 쓴다.")]
        private Camera boardCamera;

        [SerializeField, Tooltip("'빨리' 버튼. 누르고 있는 동안 연출도 같이 빨라진다. 비워도 된다.")]
        private FastDropButton fastButton;

        [Header("그리는 층")]
        [SerializeField, Tooltip("아이콘을 그릴 RectTransform. 비우면 다른 UI보다 위에 그려지는 " +
                                 "Screen Space - Overlay 캔버스를 스스로 만든다.")]
        private RectTransform flyLayer;

        [SerializeField, Tooltip("스스로 만드는 캔버스의 정렬 순서. 다른 캔버스보다 커야 가려지지 않는다.")]
        private int flyLayerSortingOrder = 1000;

        [Header("아이콘")]
        [SerializeField, Tooltip("풀에서 찍어 낼 아이콘 프리팹(Image). 비우면 Image 하나짜리를 코드로 만든다.")]
        private Image iconPrefab;

        [SerializeField, Tooltip("아이콘 크기(캔버스 단위).")]
        private Vector2 iconSize = new Vector2(96f, 96f);

        [SerializeField, Tooltip("아이템별 아이콘. id가 ItemConfig와 같아야 한다.")]
        private IconEntry[] icons = new IconEntry[0];

        [SerializeField, Tooltip("표에 없는 아이템의 아이콘. 비우면 단색 사각형이다.")]
        private Sprite fallbackSprite;

        [SerializeField] private Color fallbackColor = new Color(1f, 0.8f, 0.25f, 1f);

        [SerializeField, Min(0), Tooltip("처음에 미리 만들어 둘 아이콘 수.")]
        private int prewarmCount = 6;

        [Header("1. 팝 (튀어나옴)")]
        [SerializeField, Min(0.01f)] private float popDuration = 0.25f;

        [SerializeField, Tooltip("튀어 오르는 높이(캔버스 단위).")]
        private float popHeight = 40f;

        [SerializeField] private Ease popScaleEase = Ease.OutBack;
        [SerializeField] private Ease popMoveEase = Ease.OutQuad;

        [Header("2. 대기")]
        [SerializeField, Min(0f)] private float holdDuration = 0.15f;

        [Header("3. 비행")]
        [SerializeField, Min(0.01f)] private float flyDuration = 0.45f;

        [SerializeField, Tooltip("곡선이 위로 부푸는 정도(캔버스 단위). 0이면 직선.")]
        private float curveHeight = 250f;

        [SerializeField] private Ease flyEase = Ease.InQuad;

        [SerializeField, Min(0f), Tooltip("도착할 때의 크기 배율. 날아가며 이 값으로 줄어든다.")]
        private float arriveScale = 0.4f;

        [Header("4. 도착")]
        [SerializeField, Tooltip("칸 펀치 스케일 세기.")]
        private float punchStrength = 0.15f;

        [SerializeField, Min(0.01f)] private float punchDuration = 0.2f;
        [SerializeField, Min(0)] private int punchVibrato = 6;
        [SerializeField, Range(0f, 1f)] private float punchElasticity = 0.5f;

        [Header("여러 개가 한꺼번에 떨어질 때")]
        [SerializeField, Min(0f), Tooltip("아이콘끼리 출발 시차(초).")]
        private float stagger = 0.05f;

        [SerializeField, Min(0f), Tooltip("시작 위치를 흩뿌리는 반경(캔버스 단위).")]
        private float scatterRadius = 30f;

        [Header("배속")]
        [SerializeField, Min(1f), Tooltip("'빨리' 버튼을 누르고 있는 동안의 연출 속도 배율.")]
        private float fastSpeedMultiplier = 2.5f;

        /// <summary>날고 있는 아이콘 하나.</summary>
        private sealed class Flight
        {
            public string Id;
            public RectTransform Rect;
            public Image Icon;
            public Vector2 Start;
            public Sequence Sequence;
        }

        private readonly List<Flight> _flights = new List<Flight>(16);
        private readonly Stack<Flight> _pool = new Stack<Flight>(16);
        private readonly Dictionary<string, int> _pending = new Dictionary<string, int>(8);

        private MaterialDropResolver _drops;
        private Canvas _layerCanvas;
        private float _nextLaunchAt;
        private float _appliedSpeed = 1f;

        /// <summary>그 아이템 중 아직 날아오는 중이라 칸에 반영하지 않은 개수.</summary>
        public int PendingCount(string id)
        {
            int n;
            return !string.IsNullOrEmpty(id) && _pending.TryGetValue(id, out n) ? n : 0;
        }

        private float CurrentSpeed => fastButton != null && fastButton.IsHeld ? fastSpeedMultiplier : 1f;

        private void Awake()
        {
            EnsureLayer();

            for (int i = 0; i < prewarmCount; i++)
            {
                Flight f = CreateFlight();
                if (f == null) { break; }
                _pool.Push(f);
            }
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.RunStarted += HandleRunStarted;

                // 이미 판이 돌고 있으면 그 루프의 드롭부터 받는다.
                SubscribeDrops(session.Loop);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.RunStarted -= HandleRunStarted;
            }

            SubscribeDrops(null);
            CancelAll();
        }

        private void Update()
        {
            float speed = CurrentSpeed;
            if (Mathf.Approximately(speed, _appliedSpeed))
            {
                return;
            }

            _appliedSpeed = speed;
            for (int i = 0; i < _flights.Count; i++)
            {
                if (_flights[i].Sequence != null) { _flights[i].Sequence.timeScale = speed; }
            }
        }

        /// <summary>
        /// 웨이브가 바뀌면 루프도 새로 만들어진다. 드롭 알림을 갈아타고, 날던 아이콘은 정리한다 —
        /// 보드가 통째로 바뀌었으니 이어서 날려 봐야 어색하다. 수량은 CancelAll이 바로 맞춘다.
        /// </summary>
        private void HandleRunStarted(GameLoop loop)
        {
            SubscribeDrops(loop);
            CancelAll();
        }

        private void SubscribeDrops(GameLoop loop)
        {
            if (_drops != null)
            {
                _drops.DroppedAt -= HandleDroppedAt;
            }

            _drops = loop != null ? loop.MaterialDrops : null;

            if (_drops != null)
            {
                _drops.DroppedAt += HandleDroppedAt;
            }
        }

        /// <summary>
        /// 재료가 떨어졌다. 이 시점엔 ItemService가 이미 인벤토리에 넣어 둔 뒤다
        /// (MaterialDropResolver가 Dropped를 먼저 울린다).
        /// </summary>
        private void HandleDroppedAt(string id, Vector2Int cell)
        {
            if (inventory == null || flyLayer == null)
            {
                return;     // 연출을 못 하면 그냥 둔다 — 수량은 이미 즉시 반영돼 있다
            }

            Vector2 start;
            if (!TryCellToLayer(cell, out start))
            {
                return;
            }

            Flight f = _pool.Count > 0 ? _pool.Pop() : CreateFlight();
            if (f == null)
            {
                return;
            }

            // 먼저 "날아오는 중"으로 잡아 두고 다시 그린다. 처음 얻는 아이템이면 이때
            // 칸이 수량 없이 먼저 생기고, 아이콘은 그 칸을 향해 날아간다.
            AddPending(id, 1);
            inventory.RefreshDisplay();

            f.Id = id;
            f.Start = start + Random.insideUnitCircle * scatterRadius;
            ApplyIcon(f, id);

            f.Rect.SetAsLastSibling();
            f.Rect.localPosition = f.Start;
            f.Rect.localScale = Vector3.zero;
            f.Rect.gameObject.SetActive(true);

            // 동시에 여러 개가 떨어지면 하나씩 시차를 두고 출발한다.
            float now = Time.time;
            float delay = Mathf.Max(0f, _nextLaunchAt - now);
            _nextLaunchAt = now + delay + stagger;

            Vector2 popTop = f.Start + new Vector2(0f, popHeight);

            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(delay);
            seq.Append(f.Rect.DOScale(1f, popDuration).SetEase(popScaleEase));
            seq.Join(DOTween.To(() => 0f, h => f.Rect.localPosition = f.Start + new Vector2(0f, h), popHeight, popDuration)
                .SetEase(popMoveEase));
            seq.AppendInterval(holdDuration);
            seq.Append(DOTween.To(() => 0f, t => Fly(f, popTop, t), 1f, flyDuration).SetEase(flyEase));
            seq.OnComplete(() => Land(f));
            seq.timeScale = _appliedSpeed;

            f.Sequence = seq;
            _flights.Add(f);
        }

        /// <summary>
        /// 비행 한 프레임. 목적지를 매 프레임 다시 잰다 — 처음 생긴 칸은 레이아웃이
        /// 다음 프레임에야 자리를 잡기 때문이다.
        /// </summary>
        private void Fly(Flight f, Vector2 from, float t)
        {
            Vector2 to = TargetInLayer(f.Id, from);
            Vector2 control = (from + to) * 0.5f + new Vector2(0f, curveHeight);

            float u = 1f - t;
            Vector2 p = u * u * from + 2f * u * t * control + t * t * to;

            f.Rect.localPosition = p;

            float s = Mathf.LerpUnclamped(1f, arriveScale, t);
            f.Rect.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>도착. 수량을 올리고 칸을 톡 친다.</summary>
        private void Land(Flight f)
        {
            string id = f.Id;
            Release(f);

            AddPending(id, -1);

            if (inventory == null)
            {
                return;
            }

            inventory.RefreshDisplay();

            RectTransform slot;
            if (inventory.TryGetSlotRect(id, out slot))
            {
                // 연달아 도착하면 이전 펀치를 끝까지 감아 원래 크기로 돌린 뒤 다시 친다.
                slot.DOKill(true);
                slot.DOPunchScale(Vector3.one * punchStrength, punchDuration, punchVibrato, punchElasticity)
                    .timeScale = _appliedSpeed;
            }
        }

        /// <summary>
        /// 날던 아이콘을 전부 거둔다. 수량은 날아오던 몫까지 즉시 다 보이게 된다 —
        /// 데이터는 처음부터 들어가 있었으므로 잃는 것이 없다.
        /// </summary>
        private void CancelAll()
        {
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                Flight f = _flights[i];
                if (f.Sequence != null)
                {
                    f.Sequence.Kill(false);     // OnComplete(Land)는 부르지 않는다
                }

                Release(f);
            }

            _flights.Clear();
            _nextLaunchAt = 0f;

            if (_pending.Count == 0)
            {
                return;
            }

            _pending.Clear();

            if (inventory != null && inventory.isActiveAndEnabled)
            {
                inventory.RefreshDisplay();
            }
        }

        private void Release(Flight f)
        {
            _flights.Remove(f);
            f.Sequence = null;
            f.Id = null;

            if (f.Rect != null)
            {
                f.Rect.gameObject.SetActive(false);
                _pool.Push(f);
            }
        }

        private void AddPending(string id, int delta)
        {
            int n = PendingCount(id) + delta;
            if (n > 0)
            {
                _pending[id] = n;
            }
            else
            {
                _pending.Remove(id);
            }
        }

        // ── 좌표 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 보드 칸(월드) → 화면 → 그리는 층(UI) 좌표. 보드는 원근 카메라로, UI는 캔버스로
        /// 그려지므로 화면 좌표를 한 번 거친다. 카메라 뒤쪽이면 false.
        /// </summary>
        private bool TryCellToLayer(Vector2Int cell, out Vector2 local)
        {
            local = Vector2.zero;

            Camera cam = boardCamera != null ? boardCamera : Camera.main;
            if (board == null || board.CellRoot == null || cam == null)
            {
                return false;
            }

            Vector3 world = board.CellRoot.TransformPoint(board.CellToLocalPosition(cell));
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f)
            {
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                flyLayer, screen, UiCamera(_layerCanvas), out local);
        }

        /// <summary>그 아이템 칸의 가운데를 층 좌표로. 칸이 없으면 패널 가운데, 그것도 없으면 제자리.</summary>
        private Vector2 TargetInLayer(string id, Vector2 fallback)
        {
            RectTransform target;
            if (!inventory.TryGetSlotRect(id, out target) || !target.gameObject.activeInHierarchy)
            {
                target = inventory.PanelRect;
            }

            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return fallback;
            }

            Vector3 world = target.TransformPoint(target.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(UiCamera(target.GetComponentInParent<Canvas>()), world);

            Vector2 local;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(flyLayer, screen, UiCamera(_layerCanvas), out local)
                ? local
                : fallback;
        }

        /// <summary>Overlay 캔버스는 카메라 없이(null) 변환해야 맞다.</summary>
        private static Camera UiCamera(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        // ── 층·풀 ────────────────────────────────────────────────────────

        /// <summary>
        /// 그리는 층이 없으면 맨 위에 그려지는 Overlay 캔버스를 만든다.
        /// 크기 맞춤은 인벤토리가 든 캔버스의 CanvasScaler를 그대로 따라 해서, 아이콘 크기·높이 값이
        /// 다른 UI와 같은 단위가 되게 한다.
        /// </summary>
        private void EnsureLayer()
        {
            if (flyLayer != null)
            {
                _layerCanvas = flyLayer.GetComponentInParent<Canvas>();
                return;
            }

            var go = new GameObject("ItemFlyLayer", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            _layerCanvas = go.AddComponent<Canvas>();
            _layerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _layerCanvas.overrideSorting = true;
            _layerCanvas.sortingOrder = flyLayerSortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            Canvas source = inventory != null ? inventory.GetComponentInParent<Canvas>() : null;
            CanvasScaler sourceScaler = source != null ? source.rootCanvas.GetComponent<CanvasScaler>() : null;
            if (sourceScaler != null)
            {
                scaler.uiScaleMode = sourceScaler.uiScaleMode;
                scaler.referenceResolution = sourceScaler.referenceResolution;
                scaler.screenMatchMode = sourceScaler.screenMatchMode;
                scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
                scaler.scaleFactor = sourceScaler.scaleFactor;
                scaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
            }

            // 레이캐스터를 달지 않는다 — 날아가는 아이콘이 터치를 가로채면 안 된다.
            flyLayer = (RectTransform)go.transform;
        }

        private Flight CreateFlight()
        {
            if (flyLayer == null)
            {
                return null;
            }

            Image image;
            if (iconPrefab != null)
            {
                image = Instantiate(iconPrefab, flyLayer, false);
            }
            else
            {
                var go = new GameObject("FlyIcon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(flyLayer, false);
                image = go.GetComponent<Image>();
            }

            image.raycastTarget = false;
            image.preserveAspect = true;

            var rect = (RectTransform)image.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = iconSize;
            image.gameObject.SetActive(false);

            return new Flight { Rect = rect, Icon = image };
        }

        private void ApplyIcon(Flight f, string id)
        {
            Sprite sprite;
            Color color;
            ResolveIcon(id, out sprite, out color);
            f.Icon.sprite = sprite;
            f.Icon.color = color;
        }

        /// <summary>
        /// 아이템 id의 아이콘과 색. 날아가는 아이콘과 같은 표를 쓴다 — 클리어 영수증도 이걸 빌려 간다.
        /// 표에 없으면 기본 아이콘을 준다.
        /// </summary>
        public void ResolveIcon(string id, out Sprite sprite, out Color color)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i].id != id)
                {
                    continue;
                }

                sprite = icons[i].sprite != null ? icons[i].sprite : fallbackSprite;

                // 색을 안 고른 채(투명)로 두면 아이콘이 안 보인다. 그때는 흰색으로 본다.
                Color c = icons[i].color;
                color = c.a > 0f ? c : Color.white;
                return;
            }

            sprite = fallbackSprite;
            color = fallbackColor;
        }
    }
}
