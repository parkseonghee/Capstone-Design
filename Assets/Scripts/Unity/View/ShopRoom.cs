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
        [SerializeField, Tooltip("상품을 놓을 칸. 재료 지정 구매 → 폭탄 → 소모품 → (지정 구매 추가분) 순서로 채운다. " +
                                 "보드는 8x9이고 0번 줄은 프리뷰라 쓸 수 없다.")]
        private Vector2Int[] slots =
        {
            // 한 칸씩 띄워 깐다. 넷째 칸은 마을 강화 '상점 지정 구매 +1'(재화 문서 §5)로 늘어나는 몫이다.
            new Vector2Int(1, 3),
            new Vector2Int(3, 3),
            new Vector2Int(5, 3),
            new Vector2Int(7, 3),
        };

        [Header("상품 (재화 문서 §4) — 회차별 가격. 0번 = 1회차, 1번 = 2회차")]
        [SerializeField, Tooltip("재료 지정 구매. 사면 기초 재료 중 하나를 고른다(기초 재료 4개를 녹인 값).")]
        private int[] materialPrices = { 20, 30 };

        [SerializeField, Tooltip("폭탄 1개. 조합으로도 만들 수 있다.")]
        private int[] bombPrices = { 35, 52 };

        [SerializeField, Tooltip("무작위 소모품 1종. 상점이 열릴 때 정해진다.")]
        private int[] consumablePrices = { 40, 60 };

        [SerializeField, Min(0), Tooltip("상점당 재료 지정 구매 개수. 마을 강화 '상점 지정 구매 +1'이 2로 올린다. " +
                                         "품목은 상점당 하나씩 한정이고 리롤은 없다.")]
        private int materialPickCount = 1;

        [SerializeField, Tooltip("재료를 고를 창. 판의 C10 재료 전환기와 같은 창을 쓴다.")]
        private MaterialPickerView materialPicker;

        [SerializeField] private string materialGoodName = "재료 지정 구매";
        [SerializeField, TextArea] private string materialGoodDescription = "원하는 기초 재료 1개를 골라서 산다";
        [SerializeField] private string materialPickTitle = "받을 재료를 고르세요";

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
        [SerializeField] private string tooExpensiveText = "스크랩이 부족합니다";

        /// <summary>
        /// 재료 지정 구매 상품의 꼬리표. 아이템 표에 없는 상품이라 Core에는 이 id로 놓고,
        /// 사고 나면 여기서 재료 고르기 창을 띄운다.
        /// </summary>
        private const string MaterialPickId = "SHOP_PICK_MATERIAL";

        /// <summary>무작위 소모품 후보. 상점이 열릴 때마다 다시 채운다.</summary>
        private readonly List<ItemConfig.Entry> _pool = new List<ItemConfig.Entry>(16);

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

            if (result.PurchasedId == MaterialPickId)
            {
                OpenMaterialPick();
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

            PlaceGoods(loop, waves.WaveInStage);

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
        /// 상품을 슬롯 칸에 놓는다(재화 문서 §4). 품목은 고정이다:
        ///   재료 지정 구매 · 폭탄 · 무작위 소모품 1종 (+ 강화로 늘어난 지정 구매)
        ///
        /// <b>각 품목은 상점당 하나</b>라 한 번 사면 그 칸이 비고 다시 채워지지 않는다. 리롤도 없다 —
        /// 지정 구매 방식이라 돌릴 대상이 없다.
        /// 가격은 회차(N웨이브 뒤 상점 = N회차)로 고른다. 2회차는 1.5배라 "지금 살지 아껴 둘지"가 판단거리다.
        /// </summary>
        private void PlaceGoods(GameLoop loop, int visit)
        {
            _placedCells.Clear();
            _placedEntries.Clear();

            if (catalog == null || items == null)
            {
                return;
            }

            int slot = 0;

            if (materialPickCount > 0)
            {
                Place(loop, ref slot, MaterialPickGood(), PriceFor(materialPrices, visit));
            }

            ItemConfig.Entry bomb;
            if (catalog.TryGet(items.BombItemId, out bomb))
            {
                Place(loop, ref slot, bomb, PriceFor(bombPrices, visit));
            }

            ItemConfig.Entry consumable;
            if (TryPickConsumable(out consumable))
            {
                Place(loop, ref slot, consumable, PriceFor(consumablePrices, visit));
            }

            for (int i = 1; i < materialPickCount; i++)
            {
                Place(loop, ref slot, MaterialPickGood(), PriceFor(materialPrices, visit));
            }
        }

        private void Place(GameLoop loop, ref int slot, ItemConfig.Entry good, int price)
        {
            if (slots == null || slot >= slots.Length)
            {
                return;     // 칸이 모자라면 뒤쪽 상품은 깔리지 않는다(씬에서 칸을 늘리면 된다)
            }

            if (loop.PlaceShopItem(good.id, price, slots[slot]))
            {
                _placedCells.Add(slots[slot]);
                _placedEntries.Add(good);
            }

            slot++;
        }

        /// <summary>회차에 맞는 가격. 표보다 뒤 회차는 마지막 값을 쓴다.</summary>
        private static int PriceFor(int[] prices, int visit)
        {
            if (prices == null || prices.Length == 0)
            {
                return 0;
            }

            return Mathf.Max(0, prices[Mathf.Clamp(visit - 1, 0, prices.Length - 1)]);
        }

        /// <summary>재료 지정 구매 상품. 아이템 표에 없는 상품이라 이름·설명만 채운 항목을 만든다.</summary>
        private ItemConfig.Entry MaterialPickGood()
        {
            return new ItemConfig.Entry
            {
                id = MaterialPickId,
                displayName = materialGoodName,
                description = materialGoodDescription,
            };
        }

        /// <summary>폭탄을 뺀 소모품 중 하나를 무작위로 고른다. 후보가 없으면 false.</summary>
        private bool TryPickConsumable(out ItemConfig.Entry picked)
        {
            _pool.Clear();
            ItemConfig.Entry[] all = catalog.Items;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].kind == ItemConfig.Kind.Consumable && !items.IsBombItem(all[i].id))
                {
                    _pool.Add(all[i]);
                }
            }

            if (_pool.Count == 0)
            {
                picked = default(ItemConfig.Entry);
                return false;
            }

            picked = _pool[Random.Range(0, _pool.Count)];
            return true;
        }

        /// <summary>
        /// 재료 지정 구매를 샀다. 값은 이미 냈으니 기초 재료 하나를 고르게 한다.
        /// 창을 닫아 버리면 돈을 돌려주고 상품을 제자리에 다시 놓는다 — 상점당 하나라는 한정은 그대로다.
        /// </summary>
        private void OpenMaterialPick()
        {
            GameLoop loop = session != null ? session.Loop : null;
            if (loop == null || items == null)
            {
                return;
            }

            // 방금 팔려서 빈 지정 구매 칸. 환불할 때 상품을 여기에 되돌린다.
            int index = -1;
            for (int i = 0; i < _placedCells.Count; i++)
            {
                if (_placedEntries[i].id == MaterialPickId && loop.Grid[_placedCells[i]] == null)
                {
                    index = i;
                    break;
                }
            }

            int price = PriceFor(materialPrices, loop.Waves != null ? loop.Waves.WaveInStage : 1);
            List<ItemConfig.Entry> choices = items.BaseMaterials();

            if (materialPicker == null || choices.Count == 0)
            {
                // 고를 수가 없으면 무작위로라도 준다. 돈만 받고 아무것도 안 주는 일은 없게.
                if (choices.Count > 0)
                {
                    Give(choices[Random.Range(0, choices.Count)].id);
                }
                else
                {
                    Refund(loop, index, price);
                }

                return;
            }

            materialPicker.Show(materialPickTitle, choices, Give, delegate { Refund(loop, index, price); });
        }

        private void Give(string materialId)
        {
            ItemConfig.Entry material;
            if (catalog == null || items == null || !catalog.TryGet(materialId, out material))
            {
                return;
            }

            items.Acquire(material);
            if (hintLabel != null)
            {
                hintLabel.text = string.Format(boughtFormat, material.displayName);
            }
        }

        private void Refund(GameLoop loop, int index, int price)
        {
            if (loop == null || loop != (session != null ? session.Loop : null))
            {
                return;     // 그새 판이 바뀌었다
            }

            loop.Player.DepositScrap(price);

            if (index >= 0)
            {
                loop.PlaceShopItem(MaterialPickId, price, _placedCells[index]);
            }

            // 턴을 쓰지 않고 보드가 바뀌었다 — 뷰에 바로 알려야 상품이 다시 보인다.
            session.NotifyBoardChanged();

            UpdateNearbyItem(loop);
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

            // 상점을 지나 다음 웨이브로 들어갈 때는 체력을 가득 채워 준다.
            session.StartNextWave(healFull: true);
        }
    }
}
