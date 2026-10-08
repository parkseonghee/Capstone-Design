using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 웨이브 사이의 상점 <b>방</b>. 버튼 목록이 아니라 보드에 상품을 깔아 두고,
    /// 플레이어가 걸어가 <b>부딪히면 산다</b>(삽질기사 포켓던전 상점 방식).
    ///
    /// 조작이 평소와 같아진다 — 상점에서도 방향키로 움직이고 부딪히기만 하면 된다.
    /// 상점 방에서는 중력·스폰·도화선이 멈추므로(GameLoop.InShop) 마음 놓고 둘러볼 수 있다.
    ///
    /// 고른 상품을 <b>어디에 놓을지</b>는 인스펙터의 좌표 목록이 정한다(Hard Rule 1) —
    /// 방 배치를 바꾸고 싶으면 값만 고치면 되고 코드는 그대로다.
    /// </summary>
    public sealed class ShopRoom : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private GameSession session;

        [SerializeField, Tooltip("아이템 표(CSV 옮긴 것). 여기서 뽑아 깐다.")]
        private ItemConfig catalog;

        [SerializeField, Tooltip("산 아이템을 넣고 효과를 거는 곳.")]
        private ItemService items;

        [SerializeField, Tooltip("가격 숫자를 그리는 보드 뷰. 가까이 간 상품의 가격을 키우는 데 쓴다.")]
        private BoardView board;

        [Header("상품 배치")]
        [SerializeField, Tooltip("상품을 놓을 칸. 개수가 곧 한 상점에 깔리는 상품 수다. " +
                                 "보드는 8x9이고 0번 줄은 프리뷰라 쓸 수 없다.")]
        private Vector2Int[] slots =
        {
            new Vector2Int(2, 3),
            new Vector2Int(4, 3),
            new Vector2Int(6, 3),
        };

        [SerializeField, Tooltip("수치가 아직 '미정'인 아이템도 깔지. 끄면 확정된 것만 나온다.")]
        private bool includeUnconfirmed = true;

        [SerializeField, Tooltip("상점이 취급하는 종류. 재료·조합 시스템 도입으로 유물 직판을 접고 " +
                                 "재료만 팔도록 바꿨다. 되돌리려면 여기에 Relic을 더하면 된다.")]
        private ItemConfig.Kind[] soldKinds = { ItemConfig.Kind.Material };

        [Header("가까이 간 상품 안내 (삽질기사 포켓던전 상점 방식)")]
        [SerializeField, Min(1), Tooltip("플레이어와 이 칸 거리(맨해튼) 안에 있는 상품 중 " +
                                         "가장 가까운 하나만 '가까이 간 상품'이 된다.")]
        private int nearbyRange = 2;

        [SerializeField, Tooltip("가까이 간 상품의 이름을 띄울 라벨. 비우면 표시하지 않는다.")]
        private Text itemNameLabel;

        [Header("HUD (씬에서 배치)")]
        [SerializeField, Tooltip("상점에 있는 동안만 켜지는 안내. 비워도 된다.")]
        private GameObject banner;

        [SerializeField, Tooltip("'1웨이브 클리어 — 상점' 같은 제목.")]
        private Text titleLabel;

        [SerializeField, Tooltip("부딪혀 사라는 안내. 방금 산 물건 이름·가까이 간 상품의 설명도 여기 뜬다.")]
        private Text hintLabel;

        [SerializeField, Tooltip("다음 웨이브로 나가는 버튼.")]
        private Button exitButton;

        [Header("문구")]
        [SerializeField] private string titleFormat = "{0}웨이브 클리어 — 상점";
        [SerializeField] private string hintText = "상품에 부딪히면 구입합니다";
        [SerializeField] private string boughtFormat = "{0} 획득!";
        [SerializeField] private string tooExpensiveText = "골드가 부족합니다";

        /// <summary>이번 상점에 깔린 상품(아이템 표 인덱스). 후보를 다시 뽑을 때만 바뀐다.</summary>
        private readonly List<int> _pool = new List<int>(16);

        /// <summary>이번 상점에 실제로 놓인 상품의 칸과 표 항목. "가까이 갔는지" 판정에 쓴다.</summary>
        private readonly List<Vector2Int> _placedCells = new List<Vector2Int>(8);
        private readonly List<ItemConfig.Entry> _placedEntries = new List<ItemConfig.Entry>(8);

        public bool IsOpen { get; private set; }

        private void OnEnable()
        {
            if (session != null)
            {
                session.RunStarted += HandleRunStarted;
                session.Stepped += HandleStepped;
            }

            if (exitButton != null) { exitButton.onClick.AddListener(Exit); }

            Hide();
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.RunStarted -= HandleRunStarted;
                session.Stepped -= HandleStepped;
            }

            if (exitButton != null) { exitButton.onClick.RemoveListener(Exit); }
        }

        private void HandleRunStarted(GameLoop loop)
        {
            Hide();
        }

        private void HandleStepped(StepResult result)
        {
            GameLoop loop = session != null ? session.Loop : null;
            if (loop == null)
            {
                return;
            }

            // 상점에 있는 동안은 가까이 간 상품부터 갱신하고, 그 위에 구매 결과를 덮어 보여 준다 —
            // 순서가 바뀌면 "획득!" 메시지가 뜨자마자 설명 문구로 바로 덮여 사라진다.
            if (IsOpen)
            {
                UpdateNearbyItem(loop);
                ShowPurchaseFeedback(result);
                return;
            }

            // 스테이지의 마지막 웨이브라면 상점이 아니라 클리어 화면 차례다.
            WaveRunner waves = loop.Waves;
            if (waves == null || !waves.IsBetweenWaves)
            {
                return;
            }

            Open(loop, waves);
        }

        private void ShowPurchaseFeedback(StepResult result)
        {
            if (hintLabel == null)
            {
                return;
            }

            if (result.Move == MoveOutcome.TooExpensive)
            {
                hintLabel.text = tooExpensiveText;
                return;
            }

            if (string.IsNullOrEmpty(result.PurchasedId))
            {
                return;
            }

            // Core는 꼬리표만 넘긴다. 효과를 먹이는 건 여기다.
            ItemConfig.Entry bought;
            if (catalog != null && catalog.TryGet(result.PurchasedId, out bought))
            {
                if (items != null)
                {
                    items.Acquire(bought);
                }

                hintLabel.text = string.Format(boughtFormat, bought.displayName);
            }
        }

        /// <summary>보드를 상점 방으로 바꾸고 상품을 깐다.</summary>
        private void Open(GameLoop loop, WaveRunner waves)
        {
            IsOpen = true;
            loop.EnterShop();

            RollAndPlace(loop);

            if (titleLabel != null && waves.Current != null)
            {
                titleLabel.text = string.Format(titleFormat, waves.Current.WaveNumber);
            }

            if (hintLabel != null)
            {
                hintLabel.text = hintText;
            }

            if (banner != null && !banner.activeSelf)
            {
                banner.SetActive(true);
            }

            UpdateNearbyItem(loop);
        }

        /// <summary>
        /// 상품을 뽑아 슬롯 칸에 놓는다.
        ///
        /// <see cref="soldKinds"/>에 든 종류만 깐다. <b>이미 가진 유물은 빼고</b> 뽑는다 —
        /// 중복 획득이 안 되니 깔아 봐야 소용이 없다.
        ///
        /// <b>쌓이는 물건은 한 상점에 두 번 나올 수 있다.</b> 재료는 종류가 몇 안 되는데
        /// 한 번 뽑힌 걸 후보에서 빼 버리면 슬롯 수보다 종류가 적을 때 매대가 빈 채로 열린다.
        /// 나무를 두 칸에 까는 건 이상하지 않지만 빈 매대는 이상하다.
        /// </summary>
        private void RollAndPlace(GameLoop loop)
        {
            _pool.Clear();
            _placedCells.Clear();
            _placedEntries.Clear();

            if (catalog == null || items == null)
            {
                return;
            }

            ItemConfig.Entry[] all = catalog.Items;
            for (int i = 0; i < all.Length; i++)
            {
                if (!Sells(all[i].kind)) { continue; }
                if (!includeUnconfirmed && !all[i].confirmed) { continue; }
                if (items.AlreadyOwned(all[i])) { continue; }

                _pool.Add(i);
            }

            for (int s = 0; s < slots.Length && _pool.Count > 0; s++)
            {
                int pick = Random.Range(0, _pool.Count);
                ItemConfig.Entry item = all[_pool[pick]];

                // 유물은 하나뿐이라 뽑고 나면 후보에서 빠진다. 쌓이는 물건은 남겨 둔다.
                if (item.kind == ItemConfig.Kind.Relic)
                {
                    _pool.RemoveAt(pick);
                }

                loop.PlaceShopItem(item.id, item.price, slots[s]);
                _placedCells.Add(slots[s]);
                _placedEntries.Add(item);
            }
        }

        /// <summary>
        /// 플레이어와 가장 가까운 상품을 찾아 이름·설명을 띄우고, BoardView에 가격을
        /// 키울 대상으로 알려 준다. 가까운 상품이 없으면(또는 전부 팔렸으면) 안내를 지운다.
        /// </summary>
        private void UpdateNearbyItem(GameLoop loop)
        {
            if (loop == null)
            {
                ClearNearbyItem();
                return;
            }

            Vector2Int playerPos = loop.Player.Position;
            int bestDistance = int.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < _placedCells.Count; i++)
            {
                // 이미 팔린 상품은 칸이 비어 있다 — 후보에서 자연히 빠진다.
                if (!(loop.Grid[_placedCells[i]] is ShopItem))
                {
                    continue;
                }

                Vector2Int cell = _placedCells[i];
                int distance = Mathf.Abs(cell.x - playerPos.x) + Mathf.Abs(cell.y - playerPos.y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestDistance > nearbyRange)
            {
                ClearNearbyItem();
                return;
            }

            ItemConfig.Entry entry = _placedEntries[bestIndex];

            if (itemNameLabel != null)
            {
                itemNameLabel.text = entry.displayName;
                if (!itemNameLabel.gameObject.activeSelf) { itemNameLabel.gameObject.SetActive(true); }
            }

            if (hintLabel != null) { hintLabel.text = entry.description; }

            if (board != null)
            {
                board.EmphasizedEntity = loop.Grid[_placedCells[bestIndex]];
            }
        }

        /// <summary>가까이 간 상품이 없을 때(또는 상점을 나갈 때)로 되돌린다.</summary>
        private void ClearNearbyItem()
        {
            if (itemNameLabel != null && itemNameLabel.gameObject.activeSelf)
            {
                itemNameLabel.gameObject.SetActive(false);
            }

            if (hintLabel != null)
            {
                hintLabel.text = hintText;
            }

            if (board != null)
            {
                board.EmphasizedEntity = null;
            }
        }

        /// <summary>이 상점이 그 종류를 취급하는지.</summary>
        private bool Sells(ItemConfig.Kind kind)
        {
            if (soldKinds == null || soldKinds.Length == 0)
            {
                return true;    // 지정이 없으면 예전처럼 전부 취급한다
            }

            for (int i = 0; i < soldKinds.Length; i++)
            {
                if (soldKinds[i] == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private void Hide()
        {
            IsOpen = false;

            if (banner != null && banner.activeSelf)
            {
                banner.SetActive(false);
            }

            ClearNearbyItem();
        }

        /// <summary>상점을 나와 다음 웨이브로. 보드는 세션이 새로 깐다.</summary>
        private void Exit()
        {
            if (session == null || session.Loop == null)
            {
                return;
            }

            session.Loop.ExitShop();
            Hide();
            session.StartNextWave();
        }
    }
}
