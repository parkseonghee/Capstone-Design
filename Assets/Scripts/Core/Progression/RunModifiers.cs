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
