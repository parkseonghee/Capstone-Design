using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 아이템을 실제로 "먹이는" 곳. 상점·보상·인벤토리가 전부 여기를 거친다.
    ///
    /// 효과를 적용하는 코드가 여기 한 군데에만 있어서, 새 아이템이 생기면
    /// ItemConfig.Effect에 항목을 더하고 아래 switch에 한 줄을 더하면 끝난다(Hard Rule 3·4).
    ///
    /// 유물은 <see cref="RunModifiers"/>의 숫자를 올리고, 일회성은 보드에 즉시 작용한다.
    /// </summary>
    public sealed class ItemService : MonoBehaviour
    {
        [SerializeField, Tooltip("아이템 표(CSV 옮긴 것).")]
        private ItemConfig catalog;

        [SerializeField, Tooltip("효과를 걸 세션.")]
        private GameSession session;

        [SerializeField, Tooltip("조합법 표. 비워 두면 조합이 전부 실패한다.")]
        private CraftConfig recipes;

        [SerializeField, Tooltip("배운 조합법을 기록할 곳. 비워 두면 기록만 안 남고 조합은 된다.")]
        private RunProgress progress;

        /// <summary>이번 스테이지에 들고 있는 것. 목록 UI가 읽는다.</summary>
        public RunInventory Inventory { get; } = new RunInventory();

        public ItemConfig Catalog => catalog;

        /// <summary>
        /// 보유 폭탄 수가 바뀌었을 때(조합·구매로 늘었을 때). 인벤토리 목록이 구독한다.
        ///
        /// 설치로 <b>줄어드는</b> 쪽은 여기로 오지 않는다 — 그건 턴을 쓰는 행동이라
        /// GameSession.Stepped가 이미 울린다.
        /// </summary>
        public event System.Action BombsChanged;

        /// <summary>
        /// 폭탄 아이템의 id. 효과가 <see cref="ItemConfig.Effect.PlaceBomb"/>인 첫 항목을 쓴다.
        /// 표에 그런 항목이 없으면 null이고, 그때는 인벤토리에 폭탄 칸이 뜨지 않는다.
        ///
        /// id를 인스펙터로 또 받지 않는 이유가 그것이다 — 표와 필드가 어긋날 자리를 아예 안 만든다.
        /// </summary>
        public string BombItemId
        {
            get
            {
                if (!_bombIdResolved)
                {
                    _bombIdResolved = true;
                    _bombId = FindBombId();
                }

                return _bombId;
            }
        }

        /// <summary>
        /// 지금 들고 있는 폭탄 수. 인벤토리가 아니라 <b>판 위의 플레이어</b>가 들고 있다
        /// (<see cref="ItemConfig.Effect.PlaceBomb"/> 주석). 판 밖이면 0이다.
        /// </summary>
        public int BombCount
        {
            get
            {
                Player player = ActivePlayer;
                return player != null ? player.Bombs : 0;
            }
        }

        /// <summary>그 id가 폭탄인지. 목록 UI가 폭탄 칸을 따로 그리는 데 쓴다.</summary>
        public bool IsBombItem(string id)
        {
            ItemConfig.Entry entry;
            return catalog != null && catalog.TryGet(id, out entry) && IsBomb(entry);
        }

        private static bool IsBomb(ItemConfig.Entry item)
            => item.kind == ItemConfig.Kind.Consumable && item.effect == ItemConfig.Effect.PlaceBomb;

        private Player ActivePlayer => session != null && session.Loop != null ? session.Loop.Player : null;

        private string _bombId;

        private bool _bombIdResolved;

        private string FindBombId()
        {
            if (catalog == null)
            {
                return null;
            }

            var all = catalog.Items;
            for (int i = 0; i < all.Length; i++)
            {
                if (IsBomb(all[i]))
                {
                    return all[i].id;
                }
            }

            return null;
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.RunStarted += HandleRunStarted;
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.RunStarted -= HandleRunStarted;
            }

            SubscribeDrops(null);
        }

        /// <summary>
        /// 진짜 새 런이 시작됐을 때만 인벤토리를 비운다. 웨이브·스테이지 인계라면 그대로 둔다.
        ///
        /// GameSession.IsNewRun을 그대로 믿는다 — 예전에는 유물 효과(RunModifiers)가 전부
        /// 기본값인지로 "새 런인지"를 추측했는데, 유물을 하나도 안 사고 일회용 아이템만 산
        /// 경우엔 그 값이 웨이브를 넘어가도 항상 기본값이라 매번 새 런으로 오판해서
        /// 방금 산 일회용 아이템까지 비워 버리는 버그가 있었다.
        /// </summary>
        private void HandleRunStarted(GameLoop loop)
        {
            // 루프는 웨이브마다 새로 만들어진다. 드롭 알림도 새 루프 것으로 갈아탄다.
            SubscribeDrops(loop);

            if (session == null || loop == null || !session.IsNewRun)
            {
                return;
            }

            Inventory.Clear();
            LoadStash();
        }

        /// <summary>
        /// 마을 보관함에 둔 재료를 이번 판으로 들고 들어온다.
        ///
        /// <b>옮기는 게 아니라 복사한다</b> — 보관함은 그대로 두고, 마을로 돌아올 때
        /// <see cref="DepositToVillage"/>가 통째로 덮어쓴다. 이러면 판에서 죽어도
        /// 보관함이 판에 들어가기 전 상태로 남아, 들고 들어간 재료가 조용히 사라지지 않는다.
        /// </summary>
        private void LoadStash()
        {
            if (progress == null || catalog == null)
            {
                return;
            }

            var ids = progress.StashIds;
            for (int i = 0; i < ids.Count; i++)
            {
                ItemConfig.Entry entry;
                if (!catalog.TryGet(ids[i], out entry) || entry.kind != ItemConfig.Kind.Material)
                {
                    continue;
                }

                int count = progress.StashCount(ids[i]);
                for (int n = 0; n < count; n++)
                {
                    Inventory.Add(ids[i], true);
                }
            }
        }

        /// <summary>
        /// 마을로 돌아가며 인벤토리를 정리한다.
        ///
        /// <b>재료만 보관함으로 옮기고 유물은 버린다</b> — 유물은 런 한정이라는
        /// 밸런싱 전제(§0-2) 그대로다. 일회성도 같이 버려진다.
        ///
        /// 마을 씬으로 넘어가기 직전에 부른다.
        /// </summary>
        public void DepositToVillage()
        {
            if (progress == null || catalog == null)
            {
                return;
            }

            var ids = new System.Collections.Generic.List<string>(Inventory.DistinctCount);
            var counts = new System.Collections.Generic.List<int>(Inventory.DistinctCount);

            var owned = Inventory.Ids;
            for (int i = 0; i < owned.Count; i++)
            {
                ItemConfig.Entry entry;
                if (!catalog.TryGet(owned[i], out entry) || entry.kind != ItemConfig.Kind.Material)
                {
                    continue;   // 유물·일회성은 마을로 못 간다
                }

                ids.Add(owned[i]);
                counts.Add(Inventory.CountOf(owned[i]));
            }

            progress.ReplaceStash(ids, counts);
        }

        /// <summary>지금 구독 중인 드롭 알림. 루프가 바뀔 때 떼어 내려고 들고 있는다.</summary>
        private MaterialDropResolver _drops;

        private void SubscribeDrops(GameLoop loop)
        {
            if (_drops != null)
            {
                _drops.Dropped -= HandleMaterialDropped;
            }

            _drops = loop != null ? loop.MaterialDrops : null;

            if (_drops != null)
            {
                _drops.Dropped += HandleMaterialDropped;
            }
        }

        /// <summary>몬스터가 재료를 떨궜다. 바로 인벤토리에 넣는다.</summary>
        private void HandleMaterialDropped(string materialId)
        {
            ItemConfig.Entry entry;
            if (catalog == null || !catalog.TryGet(materialId, out entry))
            {
                Debug.LogWarning($"{nameof(ItemService)}: 드롭된 재료 '{materialId}'가 ItemConfig에 없습니다.", this);
                return;
            }

            Acquire(entry);
        }

        /// <summary>
        /// 재료 둘을 겹쳐 조합한다. 인벤토리 UI의 드래그앤드롭이 부른다.
        ///
        /// 성공하면 재료가 하나씩 빠지고 결과가 들어온다. 레시피가 없거나 결과를 못 받으면
        /// <b>재료는 그대로 둔다</b> — 실패한 조합으로 재료를 잃으면 기획이 말한
        /// "깡으로 때려맞추기"를 할 수가 없다.
        /// </summary>
        /// <returns>조합에 성공했으면 true.</returns>
        public bool TryCraft(string idA, string idB)
        {
            if (catalog == null || recipes == null || string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB))
            {
                return false;
            }

            // 같은 재료끼리의 조합이면 두 개를 갖고 있어야 한다.
            if (idA == idB)
            {
                if (Inventory.CountOf(idA) < 2)
                {
                    return false;
                }
            }
            else if (Inventory.CountOf(idA) < 1 || Inventory.CountOf(idB) < 1)
            {
                return false;
            }

            CraftConfig.Recipe recipe;
            if (!recipes.TryFind(idA, idB, out recipe))
            {
                return false;
            }

            ItemConfig.Entry result;
            if (!catalog.TryGet(recipe.result, out result))
            {
                Debug.LogWarning($"{nameof(ItemService)}: 조합 결과 '{recipe.result}'가 ItemConfig에 없습니다.", this);
                return false;
            }

            // 결과를 못 받는 경우를 먼저 걸러야 재료가 헛되이 사라지지 않는다.
            if (!CanReceive(result))
            {
                return false;
            }

            if (!Inventory.Consume(idA))
            {
                return false;
            }

            if (!Inventory.Consume(idB))
            {
                // A만 사라지고 끝나면 안 된다. 되돌려 놓고 실패로 친다.
                Inventory.Add(idA, true);
                return false;
            }

            // 조합법을 몰라도 겹쳐 보는 건 막지 않는다(기획: "깡으로 조합 때려맞추기 O").
            // 대신 한 번 맞히면 배운 것으로 남겨, 조합법 목록에 ???로 계속 뜨지 않게 한다.
            if (progress != null)
            {
                progress.LearnRecipe(recipe.id);
            }

            return Acquire(result);
        }

        /// <summary>그 유물을 이미 갖고 있어 더 살 수 없는지.</summary>
        public bool AlreadyOwned(ItemConfig.Entry item)
            => item.kind == ItemConfig.Kind.Relic && Inventory.Has(item.id);

        /// <summary>
        /// 그 아이템을 지금 받을 수 있는지. 부작용 없는 질의다.
        ///
        /// <see cref="TryCraft"/>가 <b>재료를 소모하기 전에</b> 먼저 묻는다 — 받을 수 없는 결과를
        /// 모르고 조합하면 재료만 사라진다.
        /// </summary>
        public bool CanReceive(ItemConfig.Entry item)
        {
            if (AlreadyOwned(item))
            {
                return false;
            }

            // 폭탄은 판 위의 플레이어에게 들어간다. 판 밖(마을)에서는 받을 데가 없다.
            return !IsBomb(item) || ActivePlayer != null;
        }

        /// <summary>
        /// 아이템을 얻는다. 유물이면 효과가 즉시 걸리고, 일회성이면 가방에 들어간다.
        /// 폭탄만은 가방이 아니라 보유 폭탄 수로 들어간다.
        /// </summary>
        /// <returns>실제로 들어갔으면 true.</returns>
        public bool Acquire(ItemConfig.Entry item)
        {
            // 폭탄은 인벤토리 칸이 아니라 Player.Bombs로 들어간다
            // (이유는 ItemConfig.Effect.PlaceBomb 주석). 목록에는 그 숫자가 한 줄로 비친다.
            if (IsBomb(item))
            {
                Player bomber = ActivePlayer;
                if (bomber == null)
                {
                    return false;
                }

                // amount가 0인 표를 넣어도 "얻었는데 안 늘어나는" 일이 없게 최소 1개는 준다.
                bomber.AddBombs(Mathf.Max(1, item.amount));
                BombsChanged?.Invoke();
                return true;
            }

            // 유물만 중복 불가다. 일회성과 재료는 개수로 쌓인다.
            bool stackable = item.kind != ItemConfig.Kind.Relic;

            if (!Inventory.Add(item.id, stackable))
            {
                return false;   // 이미 가진 유물
            }

            if (item.kind == ItemConfig.Kind.Relic)
            {
                ApplyRelic(item);
            }

            return true;
        }

        /// <summary>가방의 일회성 아이템을 쓴다.</summary>
        /// <returns>실제로 썼으면 true.</returns>
        public bool Use(string id)
        {
            ItemConfig.Entry item;
            if (catalog == null || !catalog.TryGet(id, out item))
            {
                return false;
            }

            // 폭탄은 "쓰기"가 아니라 "설치"다 — 어디에 놓을지 고르는 단계가 있어서
            // 여기서 처리할 수 없다. 인벤토리의 폭탄 칸이 BombPlacementController를 켠다.
            if (IsBomb(item))
            {
                return false;
            }

            if (item.kind != ItemConfig.Kind.Consumable || !Inventory.Has(id))
            {
                return false;
            }

            if (!ApplyConsumable(item))
            {
                return false;
            }

            Inventory.Consume(id);
            return true;
        }

        /// <summary>
        /// 유물 효과를 효과 묶음에 더한다.
        ///
        /// 체력·폭탄처럼 <b>이미 만들어진 플레이어</b>에게도 즉시 반영해야 하는 것은
        /// 여기서 한 번 더 먹인다 — 다음 웨이브를 기다릴 이유가 없기 때문이다.
        /// </summary>
        private void ApplyRelic(ItemConfig.Entry item)
        {
            RunModifiers mods = session != null ? session.Modifiers : null;
            if (mods == null)
            {
                return;
            }

            Player player = session.Loop != null ? session.Loop.Player : null;

            switch (item.effect)
            {
                case ItemConfig.Effect.MaxHp:
                    mods.AddMaxHp(item.amount);
                    if (player != null) { player.IncreaseMaxHp(item.amount); }
                    break;

                case ItemConfig.Effect.Bombs:
                    mods.AddBombs(item.amount);
                    if (player != null) { player.AddBombs(item.amount); }
                    break;

                case ItemConfig.Effect.GoldPercent:
                    mods.AddGoldPercent(item.amount);
                    break;

                case ItemConfig.Effect.Revive:
                    mods.AddRevive(item.amount);
                    break;

                case ItemConfig.Effect.BlastRadius:
                    // 폭발 반경과 포션 회복은 설정을 덮어쓰는 방식이라
                    // 다음 웨이브의 보드부터 반영된다(이미 놓인 폭탄은 자기 값을 들고 있다).
                    mods.AddBlastRadius(item.amount);
                    break;

                case ItemConfig.Effect.PotionHeal:
                    mods.AddPotionHeal(item.amount);
                    break;

                case ItemConfig.Effect.WallHeal:
                    mods.AddWallHeal(item.chance, item.amount);
                    break;

                case ItemConfig.Effect.BlastImmunity:
                    mods.GrantBlastImmunity();
                    break;

                case ItemConfig.Effect.StatusImmunity:
                    // 상태이상 기믹이 아직 없어 표시만 되고 아무 일도 일어나지 않는다.
                    mods.GrantStatusImmunity();
                    break;
            }
        }

        /// <summary>일회성 효과를 보드에 적용한다.</summary>
        private bool ApplyConsumable(ItemConfig.Entry item)
        {
            GameLoop loop = session != null ? session.Loop : null;
            if (loop == null || !loop.IsReady || loop.IsOver)
            {
                return false;
            }

            switch (item.effect)
            {
                case ItemConfig.Effect.DamageAllEnemies:
                    return loop.DamageAllEnemies(item.amount) >= 0;

                case ItemConfig.Effect.KillEnemies:
                    return loop.KillEnemies(item.amount) >= 0;
            }

            return false;
        }
    }
}
