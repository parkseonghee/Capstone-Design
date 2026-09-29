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

        /// <summary>이번 스테이지에 들고 있는 것. 목록 UI가 읽는다.</summary>
        public RunInventory Inventory { get; } = new RunInventory();

        public ItemConfig Catalog => catalog;

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
            if (session == null || loop == null || !session.IsNewRun)
            {
                return;
            }

            Inventory.Clear();
        }

        /// <summary>그 유물을 이미 갖고 있어 더 살 수 없는지.</summary>
        public bool AlreadyOwned(ItemConfig.Entry item)
            => item.kind == ItemConfig.Kind.Relic && Inventory.Has(item.id);

        /// <summary>
        /// 아이템을 얻는다. 유물이면 효과가 즉시 걸리고, 일회성이면 가방에 들어간다.
        /// </summary>
        /// <returns>실제로 들어갔으면 true.</returns>
        public bool Acquire(ItemConfig.Entry item)
        {
            bool stackable = item.kind == ItemConfig.Kind.Consumable;

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
