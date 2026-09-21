using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// CORE_COMBAT.md §1의 Player { hp, maxHp, attack, pos }.
    /// 좌표는 Entity가, 능력치는 여기가 갖는다. 값은 PlayerStatsConfig에서 주입된다(Hard Rule 1).
    /// </summary>
    public sealed class Player : Entity, IHasHealth
    {
        /// <param name="bombs">런 시작 시 들고 있는 폭탄 수. 기본 0이라 기존 호출은 그대로 둔다.</param>
        public Player(int maxHp, int attack, int bombs = 0)
        {
            MaxHp = Mathf.Max(1, maxHp);
            Attack = Mathf.Max(0, attack);
            Hp = MaxHp;
            Bombs = Mathf.Max(0, bombs);
        }

        public int MaxHp { get; }

        /// <summary>연쇄에 묶인 모든 적에게 각각 들어가는 피해량(§5-2).</summary>
        public int Attack { get; }

        public int Hp { get; private set; }

        public bool IsDead => Hp <= 0;

        public override EntityKind Kind => EntityKind.Player;

        /// <summary>반격 피해를 적용하고 남은 체력을 돌려준다.</summary>
        public int TakeDamage(int amount)
        {
            if (amount > 0)
            {
                Hp = Mathf.Max(0, Hp - amount);
            }

            return Hp;
        }

        /// <summary>
        /// 들고 있는 폭탄 수. 설치하면 줄고, 유물 "폭탄 +5개"가 늘린다.
        /// 골드·인벤토리 시스템이 생기기 전까지는 이 숫자 하나가 전부다.
        /// </summary>
        public int Bombs { get; private set; }

        /// <summary>폭탄 하나를 꺼낸다. 없으면 false — 설치는 일어나지 않는다.</summary>
        public bool SpendBomb()
        {
            if (Bombs <= 0)
            {
                return false;
            }

            Bombs--;
            return true;
        }

        /// <summary>폭탄을 보충한다(유물·드롭).</summary>
        public void AddBombs(int amount)
        {
            if (amount > 0)
            {
                Bombs += amount;
            }
        }

        /// <summary>
        /// 이번 스테이지에서 번 골드. 몬스터를 처치하면 그 종류의 값만큼 들어온다.
        ///
        /// 여기 있는 건 <b>판 위에서 번 돈</b>이다. 스테이지를 넘어 쌓이는 총액은
        /// Unity 계층의 RunProgress가 따로 들고 있다 — Core는 저장을 모른다(Hard Rule 5).
        /// </summary>
        public int Gold { get; private set; }

        /// <summary>골드를 얻는다. 유물 "골드 획득량 증가"가 붙으면 이 호출 앞에서 배율을 먹이면 된다.</summary>
        public void AddGold(int amount)
        {
            if (amount > 0)
            {
                Gold += amount;
            }
        }

        /// <summary>골드를 쓴다(상점). 모자라면 아무것도 안 하고 false.</summary>
        public bool SpendGold(int amount)
        {
            if (amount <= 0 || Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            return true;
        }

        /// <summary>
        /// 체력을 회복한다. 최대 체력을 넘지 않는다.
        /// </summary>
        /// <returns>실제로 회복된 양. 이미 가득 찼으면 0이다 — 넘치는 만큼은 버려진다.</returns>
        public int Heal(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            return Hp - before;
        }
    }
}
