using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// CORE_COMBAT.md §1의 Player { hp, maxHp, attack, pos }.
    /// 좌표는 Entity가, 능력치는 여기가 갖는다. 값은 PlayerStatsConfig에서 주입된다(Hard Rule 1).
    /// </summary>
    public sealed class Player : Entity, IHasHealth
    {
        public Player(int maxHp, int attack)
        {
            MaxHp = Mathf.Max(1, maxHp);
            Attack = Mathf.Max(0, attack);
            Hp = MaxHp;
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
