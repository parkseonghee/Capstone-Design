using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 보드에 쌓이는 블록 = CORE_COMBAT.md §1의 Enemy, 그리고 소비 아이템까지 겸한다.
    ///
    /// 클래스 이름을 Enemy로 바꾸지 않은 이유는 스폰·중력·뷰가 전부 이 이름을 쓰고 있어
    /// 전면 개명이 요청 범위를 넘기 때문이다(Hard Rule 9).
    ///
    /// 능력치는 생성 시 주입받는다 — 여기에 기본값 리터럴을 두지 않는다(Hard Rule 1).
    /// 적이냐 아이템이냐도 주입된 값이 정한다(TrashStats 주석 참조).
    /// </summary>
    public sealed class Trash : Entity, IHasHealth
    {
        public Trash(TrashType type, TrashStats stats)
        {
            Type = type;
            Heal = Mathf.Max(0, stats.Heal);
            ChainsWithSameType = stats.ChainsWithSameType;
            Gold = Mathf.Max(0, stats.Gold);

            // 아이템은 체력·공격력을 쓰지 않는다. MaxHp 0이면 뷰가 하트를 그리지 않는다.
            MaxHp = Mathf.Max(0, stats.MaxHp);
            Attack = Mathf.Max(0, stats.Attack);
            Hp = MaxHp;
        }

        /// <summary>연쇄 판정의 기준(SameTypeChainRule). 색·모양 구분도 겸한다.</summary>
        public TrashType Type { get; }

        public int MaxHp { get; }

        /// <summary>살아남았을 때 플레이어에게 돌려주는 피해량.</summary>
        public int Attack { get; }

        /// <summary>먹었을 때 플레이어가 회복하는 체력. 0이면 아이템이 아니다.</summary>
        public int Heal { get; }

        /// <summary>같은 종류끼리 연쇄로 묶이는지. 벽은 false다(TrashStats 주석 참조).</summary>
        public bool ChainsWithSameType { get; }

        /// <summary>처치했을 때 떨어뜨리는 골드. 0이면 안 떨어진다.</summary>
        public int Gold { get; }

        /// <summary>
        /// 부딪히면 때리는 게 아니라 <b>먹는</b> 대상인지.
        /// 체력이 아니라 회복량이 기준이다 — 체력 0짜리 적을 만들 이유가 없기 때문이다.
        /// </summary>
        public bool IsConsumable => Heal > 0;

        /// <summary>
        /// 웨이브 진행도에 들어가는 <b>적</b>인지. 값이 역할을 정한다는 규칙 그대로다:
        /// 회복량이 있으면 아이템, 공격력이 0이면 벽, 나머지가 적이다.
        ///
        /// 공격력 0짜리 적을 만들면 벽으로 취급되니 주의할 것.
        /// 잡몹 9종은 전부 공격력 1 이상이라 현재는 문제가 없다(밸런싱 v1 §2).
        /// </summary>
        public bool IsEnemy => !IsConsumable && Attack > 0;

        /// <summary>벽인지. 때릴 수는 있지만 반격이 없는 장애물이다(밸런싱 v1 §3).</summary>
        public bool IsWall => !IsConsumable && Attack <= 0;

        public int Hp { get; private set; }

        public bool IsDead => Hp <= 0;

        public override EntityKind Kind => EntityKind.Trash;

        /// <summary>피해를 적용하고 남은 체력을 돌려준다. 0 아래로는 내려가지 않는다.</summary>
        public int TakeDamage(int amount)
        {
            if (amount > 0)
            {
                Hp = Mathf.Max(0, Hp - amount);
            }

            return Hp;
        }
    }
}
