using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 이번 판에 걸려 있는 유물 효과를 한곳에 모은 값 묶음.
    ///
    /// 유물마다 코드를 여기저기 흩뿌리는 대신, 모든 유물이 <b>이 숫자들만 올린다</b>.
    /// Core의 각 부분은 자기가 쓰는 칸 하나만 읽으면 되고, 유물이 몇 개든 몇 종이든 모른다.
    /// 유물이 늘어도 고칠 곳은 "어느 칸을 올리는가" 한 줄뿐이다(Hard Rule 3).
    ///
    /// 값은 <b>보너스</b>다 — 기본값 0/1은 "유물 없음"과 같다.
    /// 스테이지 하나가 끝날 때까지 유지되며, 웨이브 사이에는 그대로 이어진다.
    /// </summary>
    public sealed class RunModifiers
    {
        /// <summary>최대 체력 보너스. R01 튼튼한 몸(+1), R02 강인한 몸(+2).</summary>
        public int BonusMaxHp { get; private set; }

        /// <summary>시작 폭탄 보너스. R08 폭탄 주머니(+5).</summary>
        public int BonusBombs { get; private set; }

        /// <summary>폭발 반경 보너스. R05 대형 폭약(1 → 2 = 3x3 → 5x5).</summary>
        public int BonusBlastRadius { get; private set; }

        /// <summary>포션 회복량 보너스. R06 진한 포션(+1 = 2 → 3).</summary>
        public int BonusPotionHeal { get; private set; }

        /// <summary>골드 획득 배율(%). 100이 기본. R03 황금 손이 올린다.</summary>
        public int GoldPercent { get; private set; } = 100;

        /// <summary>폭발 피해 면역. R09 방폭 장비 — 자기 폭탄 피해도 무효다.</summary>
        public bool ImmuneToBlast { get; private set; }

        /// <summary>
        /// 상태이상 피해 면역. R10 정화 필터.
        /// <b>지금은 아무 효과가 없다</b> — 상태이상 기믹 자체가 미구현이기 때문이다.
        /// 기믹이 들어오면 그쪽에서 이 값을 읽으면 된다.
        /// </summary>
        public bool ImmuneToStatus { get; private set; }

        /// <summary>남은 부활 횟수. R04 재생의 씨앗.</summary>
        public int Revives { get; private set; }

        /// <summary>부활할 때 돌아오는 체력. 유물을 넣을 때 같이 들어온다.</summary>
        public int ReviveHp { get; private set; }

        /// <summary>벽을 부쉈을 때 회복이 터질 확률(%). R07 정화의 씨앗.</summary>
        public int WallHealPercent { get; private set; }

        /// <summary>그때 회복하는 양.</summary>
        public int WallHealAmount { get; private set; }

        /// <summary>공격력 보너스. R09 날카로운 삽.</summary>
        public int BonusAttack { get; private set; }

        /// <summary>덫에 닿아도 날아가지 않는다. R12 튼튼한 장화.</summary>
        public bool ImmuneToTraps { get; private set; }

        /// <summary>재료 드랍 확률 보너스(%p). R10 넝마주이. 드랍 표의 확률에 그대로 더한다.</summary>
        public int BonusDropChance { get; private set; }

        /// <summary>
        /// 한 번의 공격으로 이 수 이상 처치하면 체력을 회복한다(0이면 효과 없음). R11 재활용 장갑.
        /// </summary>
        public int ChainHealThreshold { get; private set; }

        /// <summary>연쇄 회복량. <see cref="ChainHealThreshold"/>와 짝이다.</summary>
        public int ChainHealAmount { get; private set; }

        /// <summary>
        /// 이번 스테이지 동안만 붙는 드랍 확률 보너스(%p). 수집 장갑(C05).
        /// RunModifiers는 스테이지를 새로 시작할 때 새로 만들어지므로 따로 지울 필요가 없다.
        /// </summary>
        public int StageDropChance { get; private set; }

        /// <summary>드랍 표에 더할 보너스 합계 — 유물(R10)과 소모품(C05).</summary>
        public int TotalDropChance => BonusDropChance + StageDropChance;

        /// <summary>
        /// "N마리 처치마다 재료 1개 보장"을 세는 카운터. 효과가 아니라 런 단위 진행값이지만,
        /// 웨이브마다 루프가 새로 만들어져도 이어져야 해서 런과 수명이 같은 여기에 둔다.
        /// </summary>
        public int KillsSinceGuaranteedDrop { get; set; }

        public void AddMaxHp(int amount) => BonusMaxHp += Mathf.Max(0, amount);

        public void AddBombs(int amount) => BonusBombs += Mathf.Max(0, amount);

        public void AddBlastRadius(int amount) => BonusBlastRadius += Mathf.Max(0, amount);

        public void AddPotionHeal(int amount) => BonusPotionHeal += Mathf.Max(0, amount);

        /// <summary>골드 획득량을 누적해서 올린다. +20이면 100 → 120%.</summary>
        public void AddGoldPercent(int amount) => GoldPercent += Mathf.Max(0, amount);

        public void GrantBlastImmunity() => ImmuneToBlast = true;

        public void GrantStatusImmunity() => ImmuneToStatus = true;

        /// <summary>부활을 한 번 준다. 돌아올 체력은 큰 쪽을 남긴다.</summary>
        public void AddRevive(int hp)
        {
            Revives++;
            ReviveHp = Mathf.Max(ReviveHp, Mathf.Max(1, hp));
        }

        /// <summary>벽 처치 회복. 확률과 양은 각각 큰 쪽으로 덮어쓴다(중복 획득이 없어 실질 1회다).</summary>
        public void AddWallHeal(int percent, int amount)
        {
            WallHealPercent = Mathf.Max(WallHealPercent, Mathf.Clamp(percent, 0, 100));
            WallHealAmount = Mathf.Max(WallHealAmount, Mathf.Max(1, amount));
        }

        public void AddAttack(int amount) => BonusAttack += Mathf.Max(0, amount);

        public void AddStageDropChance(int percent) => StageDropChance += Mathf.Max(0, percent);

        public void GrantTrapImmunity() => ImmuneToTraps = true;

        public void AddDropChance(int percent) => BonusDropChance += Mathf.Max(0, percent);

        /// <summary>연쇄 회복을 건다. 주는 유물이 하나뿐이라 덮어쓴다.</summary>
        public void SetChainHeal(int threshold, int amount)
        {
            ChainHealThreshold = Mathf.Max(1, threshold);
            ChainHealAmount = Mathf.Max(1, amount);
        }

        // ── 되돌리기 (유물을 쓰레기통에 버렸을 때) ─────────────────────────
        // 위의 Add/Grant와 짝을 이룬다. 각각 기본값 밑으로는 내려가지 않는다 —
        // 버린 유물보다 더 많이 깎이면 다른 유물의 몫까지 사라지기 때문이다.

        public void RemoveMaxHp(int amount) => BonusMaxHp = Mathf.Max(0, BonusMaxHp - Mathf.Max(0, amount));

        public void RemoveBombs(int amount) => BonusBombs = Mathf.Max(0, BonusBombs - Mathf.Max(0, amount));

        public void RemoveBlastRadius(int amount) => BonusBlastRadius = Mathf.Max(0, BonusBlastRadius - Mathf.Max(0, amount));

        public void RemovePotionHeal(int amount) => BonusPotionHeal = Mathf.Max(0, BonusPotionHeal - Mathf.Max(0, amount));

        public void RemoveGoldPercent(int amount) => GoldPercent = Mathf.Max(100, GoldPercent - Mathf.Max(0, amount));

        public void RevokeBlastImmunity() => ImmuneToBlast = false;

        public void RevokeStatusImmunity() => ImmuneToStatus = false;

        /// <summary>부활을 하나 뺀다. 이미 써 버렸으면 뺄 게 없다.</summary>
        public void RemoveRevive()
        {
            Revives = Mathf.Max(0, Revives - 1);
            if (Revives == 0)
            {
                ReviveHp = 0;
            }
        }

        public void RemoveAttack(int amount) => BonusAttack = Mathf.Max(0, BonusAttack - Mathf.Max(0, amount));

        public void RevokeTrapImmunity() => ImmuneToTraps = false;

        public void RemoveDropChance(int percent) => BonusDropChance = Mathf.Max(0, BonusDropChance - Mathf.Max(0, percent));

        public void ClearChainHeal()
        {
            ChainHealThreshold = 0;
            ChainHealAmount = 0;
        }

        /// <summary>벽 처치 회복을 끈다. 주는 유물이 하나뿐이라 통째로 0이 된다.</summary>
        public void ClearWallHeal()
        {
            WallHealPercent = 0;
            WallHealAmount = 0;
        }

        /// <summary>부활을 한 번 쓴다. 남은 게 없으면 false.</summary>
        public bool TrySpendRevive()
        {
            if (Revives <= 0)
            {
                return false;
            }

            Revives--;
            return true;
        }

        /// <summary>골드에 배율을 먹인다. 소수점은 버린다.</summary>
        public int ApplyGold(int amount)
        {
            if (amount <= 0 || GoldPercent == 100)
            {
                return Mathf.Max(0, amount);
            }

            return Mathf.Max(0, (amount * GoldPercent) / 100);
        }
    }
}
