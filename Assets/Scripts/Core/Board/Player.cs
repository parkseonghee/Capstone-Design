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

        public int MaxHp { get; private set; }

        /// <summary>연쇄에 묶인 모든 적에게 각각 들어가는 피해량(§5-2).</summary>
        public int Attack { get; private set; }

        /// <summary>공격력을 올린다(유물 R09).</summary>
        public void IncreaseAttack(int amount)
        {
            if (amount > 0)
            {
                Attack += amount;
            }
        }

        /// <summary>공격력을 내린다(유물을 버렸을 때). 0 밑으로는 안 내려간다.</summary>
        public void DecreaseAttack(int amount)
        {
            if (amount > 0)
            {
                Attack = Mathf.Max(0, Attack - amount);
            }
        }

        public int Hp { get; private set; }

        public bool IsDead => Hp <= 0;

        public override EntityKind Kind => EntityKind.Player;

        /// <summary>반격 피해를 적용하고 남은 체력을 돌려준다.</summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0)
            {
                return Hp;
            }

            // 방독면(C06): 남은 턴 동안은 무엇에 맞아도 아프지 않다.
            if (InvulnerableTurns > 0)
            {
                return Hp;
            }

            // 방호복(C11): 다음 한 번을 막고 사라진다.
            if (Shields > 0)
            {
                Shields--;
                return Hp;
            }

            Hp = Mathf.Max(0, Hp - amount);
            return Hp;
        }

        /// <summary>남은 피격 무효 횟수. 방호복(C11).</summary>
        public int Shields { get; private set; }

        /// <summary>피해 무효가 남은 턴 수. 방독면(C06).</summary>
        public int InvulnerableTurns { get; private set; }

        public void AddShield(int count)
        {
            if (count > 0)
            {
                Shields += count;
            }
        }

        /// <summary>그 턴 수만큼 피해 무효. 이미 걸려 있으면 긴 쪽을 남긴다(겹쳐 늘어나지 않는다).</summary>
        public void GrantInvulnerability(int turns)
        {
            InvulnerableTurns = Mathf.Max(InvulnerableTurns, turns);
        }

        /// <summary>한 턴이 끝났다. 피해 무효를 한 턴 깎는다. GameLoop가 턴 끝마다 부른다.</summary>
        public void TickInvulnerability()
        {
            if (InvulnerableTurns > 0)
            {
                InvulnerableTurns--;
            }
        }

        /// <summary>체력을 가득 채운다(완전 회복 키트 C12).</summary>
        /// <returns>실제로 찬 양.</returns>
        public int HealFull()
        {
            int before = Hp;
            Hp = MaxHp;
            return Hp - before;
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

        /// <summary>
        /// 이번 판에 걸린 유물 효과. 없으면 null이며 그때는 보너스가 전부 0이다.
        /// 골드 배율(R03)과 부활(R04)이 여기서 걸린다.
        /// </summary>
        public RunModifiers Modifiers { get; private set; }

        public void AttachModifiers(RunModifiers modifiers)
        {
            Modifiers = modifiers;
        }

        /// <summary>골드를 얻는다. R03 황금 손이 붙어 있으면 배율이 먹는다.</summary>
        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Gold += Modifiers != null ? Modifiers.ApplyGold(amount) : amount;
        }

        /// <summary>
        /// 죽었을 때 부활 유물(R04 재생의 씨앗)이 남아 있으면 한 번 되살린다.
        /// 패배 판정 직전에 불린다.
        /// </summary>
        /// <returns>되살아났으면 true.</returns>
        public bool TryRevive()
        {
            if (!IsDead || Modifiers == null || !Modifiers.TrySpendRevive())
            {
                return false;
            }

            Hp = Mathf.Clamp(Modifiers.ReviveHp, 1, MaxHp);
            return true;
        }

        /// <summary>
        /// 최대 체력을 올린다(상점·유물). 늘어난 만큼 현재 체력도 같이 채워 준다 —
        /// 돈을 내고 산 칸이 비어 있으면 산 느낌이 안 난다.
        /// </summary>
        public void IncreaseMaxHp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            MaxHp += amount;
            Hp += amount;
        }

        /// <summary>
        /// 최대 체력을 내린다(유물을 버렸을 때). 1 밑으로는 안 내려가고,
        /// 현재 체력이 새 최대치를 넘으면 거기에 맞춰 깎는다.
        /// </summary>
        public void DecreaseMaxHp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            MaxHp = Mathf.Max(1, MaxHp - amount);
            Hp = Mathf.Min(Hp, MaxHp);
        }

        /// <summary>폭탄을 뺀다(유물을 버렸을 때). 이미 써 버린 만큼은 뺄 수 없다.</summary>
        public void RemoveBombs(int amount)
        {
            if (amount > 0)
            {
                Bombs = Mathf.Max(0, Bombs - amount);
            }
        }

        /// <summary>
        /// 웨이브 사이에 상태를 물려받는다. 스테이지 안에서는 체력·폭탄·골드가 이어져야
        /// 상점이 의미를 갖기 때문이다(팀 확정 구조: 1스테이지 = 3웨이브 + 상점 2회).
        ///
        /// 보드는 새로 깔리지만 플레이어는 같은 사람이다.
        /// </summary>
        public void CarryOver(Player previous)
        {
            if (previous == null)
            {
                return;
            }

            // 상점에서 산 최대 체력 증가분까지 따라와야 한다.
            if (previous.MaxHp > MaxHp)
            {
                MaxHp = previous.MaxHp;
            }

            Hp = Mathf.Clamp(previous.Hp, 1, MaxHp);
            Shields = previous.Shields;
            InvulnerableTurns = previous.InvulnerableTurns;
            Bombs = Mathf.Max(0, previous.Bombs);
            Gold = Mathf.Max(0, previous.Gold);
        }

        /// <summary>
        /// 이번 판 지갑을 0으로 되돌린다. 스테이지를 깨서 총액(RunProgress)에 적립한 직후에 부른다 —
        /// 안 그러면 이어지는 다음 스테이지에서 같은 돈을 또 적립하게 된다(이제 스테이지 사이에도
        /// 체력·폭탄과 함께 지갑이 그대로 이어지기 때문에 생긴 필요다. 로그라이크: 실패해야만 비워진다).
        /// </summary>
        public void BankGold()
        {
            Gold = 0;
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
