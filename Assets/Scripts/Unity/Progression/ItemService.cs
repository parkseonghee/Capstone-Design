using System.Collections.Generic;
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

        /// <summary>재료 둘을 겹쳐 조합한다. 인벤토리 UI의 드래그앤드롭이 부른다.</summary>
        /// <returns>조합에 성공했으면 true.</returns>
        public bool TryCraft(string idA, string idB)
        {
            return TryCraft(new[] { idA, idB });
        }

        /// <summary>
        /// 재료 묶음(2~5개)으로 조합한다. 인벤토리의 두 칸 겹치기와 조합대가 부른다.
        ///
        /// 성공하면 넣은 재료가 빠지고 결과가 들어온다. 레시피가 없거나 결과를 못 받으면
        /// <b>재료는 그대로 둔다</b> — 문서의 "실패 시 재료 소멸"은 아직 넣지 않기로 했다.
        /// </summary>
        /// <returns>조합에 성공했으면 true.</returns>
        public bool TryCraft(IReadOnlyList<string> ingredients)
        {
            CraftConfig.Recipe recipe;
            ItemConfig.Entry result;
            if (!TryResolve(ingredients, out recipe, out result))
            {
                return false;
            }

            // 하나씩 빼다가 중간에 실패하면 이미 뺀 것을 되돌린다 — 일부만 사라지면 안 된다.
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (Inventory.Consume(ingredients[i]))
                {
                    continue;
                }

                for (int back = 0; back < i; back++)
                {
                    Inventory.Add(ingredients[back], true);
                }

                return false;
            }

            // 조합법을 몰라도 시도하는 건 막지 않는다(블라인드 조합).
            // 대신 한 번 맞히면 배운 것으로 남겨, 조합법 목록에 ???로 계속 뜨지 않게 한다.
            if (progress != null)
            {
                progress.LearnRecipe(recipe.id);
            }

            return Acquire(result);
        }

        /// <summary>두 재료를 겹치면 무엇이 나오는지 미리 본다. 부작용 없는 질의다.</summary>
        public bool TryPreviewCraft(string idA, string idB, out ItemConfig.Entry result, out bool known)
        {
            return TryPreviewCraft(new[] { idA, idB }, out result, out known);
        }

        /// <summary>
        /// 재료 묶음으로 무엇이 나오는지 미리 본다. 부작용 없는 질의다 — 미리보기 창·조합대가 계속 부른다.
        ///
        /// 판단 조건은 <see cref="TryCraft(IReadOnlyList{string})"/>와 같다(개수·레시피·결과를 받을 수 있는지).
        /// 여기서 true면 지금 조합하면 성공한다.
        /// </summary>
        /// <param name="known">그 조합법을 이미 아는지. 모르면 미리보기가 결과를 가린다.</param>
        public bool TryPreviewCraft(IReadOnlyList<string> ingredients, out ItemConfig.Entry result, out bool known)
        {
            known = false;

            CraftConfig.Recipe recipe;
            if (!TryResolve(ingredients, out recipe, out result))
            {
                return false;
            }

            known = recipe.knownFromStart || progress == null || progress.IsRecipeKnown(recipe.id);
            return true;
        }

        /// <summary>
        /// 재료 묶음이 지금 조합 가능한지 — 재료를 그만큼 갖고 있는지, 레시피가 있는지,
        /// 결과를 받을 수 있는지. 셋 다 되면 그 레시피와 결과를 돌려준다.
        /// </summary>
        private bool TryResolve(IReadOnlyList<string> ingredients, out CraftConfig.Recipe recipe, out ItemConfig.Entry result)
        {
            recipe = default(CraftConfig.Recipe);
            result = default(ItemConfig.Entry);

            if (catalog == null || recipes == null || ingredients == null || ingredients.Count < 2)
            {
                return false;
            }

            if (!HasAll(ingredients) || !recipes.TryFind(ingredients, out recipe))
            {
                return false;
            }

            if (!catalog.TryGet(recipe.result, out result))
            {
                Debug.LogWarning($"{nameof(ItemService)}: 조합 결과 '{recipe.result}'가 ItemConfig에 없습니다.", this);
                return false;
            }

            // 결과를 못 받는 경우를 먼저 걸러야 재료가 헛되이 사라지지 않는다.
            return CanReceive(result);
        }

        /// <summary>그 묶음만큼 재료를 갖고 있는지. 같은 재료가 두 번 들어 있으면 두 개가 있어야 한다.</summary>
        private bool HasAll(IReadOnlyList<string> ingredients)
        {
            for (int i = 0; i < ingredients.Count; i++)
            {
                string id = ingredients[i];
                if (string.IsNullOrEmpty(id))
                {
                    return false;
                }

                int need = 0;
                for (int j = 0; j < ingredients.Count; j++)
                {
                    if (ingredients[j] == id) { need++; }
                }

                if (Inventory.CountOf(id) < need)
                {
                    return false;
                }
            }

            return true;
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

        /// <summary>
        /// 아이템 하나를 버린다(쓰레기통). 쌓이는 물건은 하나만 빠지고, 유물은 <b>효과까지 되돌린다</b> —
        /// 효과를 남긴 채 칸만 비우면 버리는 게 공짜 이득이 된다.
        ///
        /// 폭탄은 버릴 수 없다. 인벤토리가 아니라 Player.Bombs에 들어 있고, 버릴 이유도 없다.
        /// </summary>
        /// <returns>실제로 버렸으면 true.</returns>
        public bool Discard(string id)
        {
            ItemConfig.Entry item;
            if (catalog == null || !catalog.TryGet(id, out item) || IsBomb(item))
            {
                return false;
            }

            if (!Inventory.Consume(id))
            {
                return false;
            }

            if (item.kind == ItemConfig.Kind.Relic)
            {
                RevertRelic(item);
            }

            return true;
        }

        /// <summary><see cref="ApplyRelic"/>의 반대. 두 switch는 항목이 늘 짝을 이뤄야 한다.</summary>
        private void RevertRelic(ItemConfig.Entry item)
        {
            RunModifiers mods = session != null ? session.Modifiers : null;
            if (mods == null)
            {
                return;
            }

            Player player = ActivePlayer;

            switch (item.effect)
            {
                case ItemConfig.Effect.MaxHp:
                    mods.RemoveMaxHp(item.amount);
                    if (player != null) { player.DecreaseMaxHp(item.amount); }
                    break;

                case ItemConfig.Effect.Bombs:
                    mods.RemoveBombs(item.amount);
                    if (player != null)
                    {
                        player.RemoveBombs(item.amount);
                        BombsChanged?.Invoke();
                    }
                    break;

                case ItemConfig.Effect.GoldPercent:
                    mods.RemoveGoldPercent(item.amount);
                    break;

                case ItemConfig.Effect.Revive:
                    mods.RemoveRevive();
                    break;

                case ItemConfig.Effect.BlastRadius:
                    mods.RemoveBlastRadius(item.amount);
                    break;

                case ItemConfig.Effect.PotionHeal:
                    mods.RemovePotionHeal(item.amount);
                    break;

                case ItemConfig.Effect.WallHeal:
                    mods.ClearWallHeal();
                    break;

                case ItemConfig.Effect.BlastImmunity:
                    mods.RevokeBlastImmunity();
                    break;

                case ItemConfig.Effect.StatusImmunity:
                    mods.RevokeStatusImmunity();
                    break;

                case ItemConfig.Effect.AttackUp:
                    mods.RemoveAttack(item.amount);
                    if (player != null) { player.DecreaseAttack(item.amount); }
                    break;

                case ItemConfig.Effect.TrapImmunity:
                    mods.RevokeTrapImmunity();
                    break;

                case ItemConfig.Effect.DropChance:
                    mods.RemoveDropChance(item.amount);
                    break;

                case ItemConfig.Effect.ChainHeal:
                    mods.ClearChainHeal();
                    break;
            }
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

            // 적 처치·덫 제거·몬스터 섞기는 턴을 안 쓰므로, 알려 주지 않으면 다음 턴까지 화면이 그대로다.
            if (session != null)
            {
                session.NotifyBoardChanged();
            }

            return true;
        }

        /// <summary>그 아이템이 재료 전환기(C10)인지. 인벤토리가 "이 칸에 재료를 떨궜을 때"를 가르는 데 쓴다.</summary>
        public bool IsConverter(string id)
        {
            ItemConfig.Entry entry;
            return catalog != null && catalog.TryGet(id, out entry)
                   && entry.kind == ItemConfig.Kind.Consumable && entry.effect == ItemConfig.Effect.ConvertMaterial;
        }

        /// <summary>기초 재료 목록(드랍·상점으로 얻는 재료). 재료 전환기가 고를 수 있는 대상이다.</summary>
        public List<ItemConfig.Entry> BaseMaterials()
        {
            var list = new List<ItemConfig.Entry>(8);
            if (catalog == null)
            {
                return list;
            }

            var all = catalog.Items;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].kind == ItemConfig.Kind.Material && !all[i].craftOnly)
                {
                    list.Add(all[i]);
                }
            }

            return list;
        }

        /// <summary>
        /// 재료 전환기(C10)를 써서 재료 하나를 다른 기초 재료로 바꾼다.
        /// 전환기·바꿀 재료가 하나씩 빠지고 고른 기초 재료가 하나 들어온다.
        /// </summary>
        /// <returns>바꿨으면 true. 전환기나 재료가 없거나, 같은 재료로 바꾸려 하면 false.</returns>
        public bool ConvertMaterial(string converterId, string fromId, string toId)
        {
            ItemConfig.Entry from, to;
            if (!IsConverter(converterId) || fromId == toId
                || catalog == null || !catalog.TryGet(fromId, out from) || !catalog.TryGet(toId, out to)
                || from.kind != ItemConfig.Kind.Material
                || to.kind != ItemConfig.Kind.Material || to.craftOnly)
            {
                return false;
            }

            if (!Inventory.Has(converterId) || !Inventory.Has(fromId))
            {
                return false;
            }

            if (!Inventory.Consume(converterId))
            {
                return false;
            }

            if (!Inventory.Consume(fromId))
            {
                Inventory.Add(converterId, true);   // 재료를 못 뺐으면 전환기도 돌려놓는다
                return false;
            }

            return Inventory.Add(toId, true);
        }

        private IRandomSource _random;

        /// <summary>재배치 물약이 쓸 난수. 판 진행과 엮이지 않게 따로 둔다.</summary>
        private IRandomSource Random => _random ?? (_random = new SystemRandomSource(System.Environment.TickCount));

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

                case ItemConfig.Effect.AttackUp:
                    mods.AddAttack(item.amount);
                    if (player != null) { player.IncreaseAttack(item.amount); }
                    break;

                case ItemConfig.Effect.TrapImmunity:
                    mods.GrantTrapImmunity();
                    break;

                case ItemConfig.Effect.DropChance:
                    mods.AddDropChance(item.amount);
                    break;

                case ItemConfig.Effect.ChainHeal:
                    mods.SetChainHeal(item.chance, item.amount);
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

                // 회복류는 체력이 가득이면 쓰지 않는다 — 버튼을 잘못 눌러 날리지 않게.
                case ItemConfig.Effect.HealPlayer:
                    return loop.Player.Hp < loop.Player.MaxHp && loop.Player.Heal(Mathf.Max(1, item.amount)) > 0;

                case ItemConfig.Effect.FullHeal:
                    return loop.Player.HealFull() > 0;

                // 덫이 하나도 없으면 쓰지 않는다.
                case ItemConfig.Effect.RemoveTraps:
                    return loop.RemoveAllTraps() > 0;

                case ItemConfig.Effect.StageDropChance:
                    if (session.Modifiers == null) { return false; }
                    session.Modifiers.AddStageDropChance(item.amount);
                    return true;

                case ItemConfig.Effect.Shield:
                    loop.Player.AddShield(Mathf.Max(1, item.amount));
                    return true;

                case ItemConfig.Effect.Invulnerable:
                    loop.Player.GrantInvulnerability(Mathf.Max(1, item.amount));
                    return true;

                // 섞을 적이 둘 미만이면 쓰지 않는다.
                case ItemConfig.Effect.ShuffleEnemies:
                    return loop.ShuffleEnemies(Random) > 0;
            }

            return false;
        }
    }
}
