using System;

namespace RecycleLife.Core
{
    /// <summary>
    /// 런 종료 정산(재화 문서 §1). 사망하든 스테이지를 클리어하든 런이 끝나면 한 번 돈다.
    ///
    ///   남은 재료 ──(등급별 변환 가치)──→ 스크랩 ─┐
    ///   남은 스크랩 ───────────────────────────┴─(1:1)─→ 골드
    ///
    /// 재료가 몇 스크랩인지는 아이템 표(Unity 계층)가 알므로 합계만 받는다 — Core는 아이템 목록을
    /// 모른다(Hard Rule 5). 여기는 "얼마가 골드로 넘어가는가"와 지갑 비우기만 맡는다.
    ///
    /// 재료를 녹인 스크랩에는 R03 배율을 먹이지 않는다. 배율은 <b>런 중에 줍는</b> 스크랩에만
    /// 먹는다(Player.AddScrap) — 정산에서 또 먹이면 같은 유물이 두 번 계산된다.
    /// </summary>
    public readonly struct RunSettlement
    {
        /// <summary>스크랩 → 골드 환율. 1:1로 확정(§1). 환율 업그레이드는 보류 중이다(§11).</summary>
        public const int GoldPerScrap = 1;

        private RunSettlement(int leftoverScrap, int materialScrap)
        {
            LeftoverScrap = leftoverScrap;
            MaterialScrap = materialScrap;
        }

        /// <summary>런이 끝날 때 쓰지 않고 남아 있던 스크랩.</summary>
        public int LeftoverScrap { get; }

        /// <summary>남은 재료를 녹여서 나온 스크랩.</summary>
        public int MaterialScrap { get; }

        /// <summary>골드로 넘어가는 스크랩 전체.</summary>
        public int TotalScrap => LeftoverScrap + MaterialScrap;

        /// <summary>마을로 이관되는 골드.</summary>
        public int Gold => TotalScrap * GoldPerScrap;

        /// <summary>
        /// 플레이어 지갑을 정산하고 비운다. 같은 스크랩이 두 번 이관되지 않게 여기서 바로 0으로 만든다.
        /// </summary>
        /// <param name="materialScrap">남은 재료를 녹인 스크랩 합계. 호출자가 재료를 인벤토리에서 이미 뺐어야 한다.</param>
        public static RunSettlement Settle(Player player, int materialScrap)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            var settlement = new RunSettlement(Math.Max(0, player.Scrap), Math.Max(0, materialScrap));
            player.BankScrap();
            return settlement;
        }
    }
}
