using System;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 아이템 표. `리사이클라이프_아이템리스트csv.csv`의 R01~R10 / C01~C02를 그대로 옮긴 것이다.
    ///
    /// 유물(Relic)은 <b>중복 획득 불가</b> — CSV의 "중복 획득: 불가" 그대로다.
    /// 일회성(Consumable)은 여러 개 쌓아 두고 쓴다.
    ///
    /// CSV에서 "미정"인 수치는 임시값을 넣고 <see cref="Entry.confirmed"/>를 꺼 뒀다.
    /// 인스펙터에서 한눈에 구분되고, 확정되면 수치만 고치면 된다(Hard Rule 1·11).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemConfig", menuName = "RecycleLife/Item Config", order = 14)]
    public sealed class ItemConfig : ScriptableObject
    {
        public enum Kind
        {
            /// <summary>유물. 한 번 얻으면 스테이지가 끝날 때까지 계속 붙어 있다. 중복 불가.</summary>
            Relic,

            /// <summary>일회성. 들고 있다가 눌러서 쓴다. 여러 개 살 수 있다.</summary>
            Consumable,
        }

        public enum Effect
        {
            /// <summary>최대 체력 증가. R01 / R02.</summary>
            MaxHp,

            /// <summary>골드 획득량 증가(%). R03.</summary>
            GoldPercent,

            /// <summary>사망 시 1회 부활. R04. amount = 부활 시 회복 체력.</summary>
            Revive,

            /// <summary>폭발 범위 증가. R05. amount = 반경 보너스(1이면 3x3 → 5x5).</summary>
            BlastRadius,

            /// <summary>포션 회복량 증가. R06.</summary>
            PotionHeal,

            /// <summary>벽 처치 시 확률 회복. R07. amount = 회복량, chance = 확률(%).</summary>
            WallHeal,

            /// <summary>폭탄 보유 수 증가. R08.</summary>
            Bombs,

            /// <summary>폭발 피해 면역. R09.</summary>
            BlastImmunity,

            /// <summary>상태이상 피해 면역. R10. 상태이상 기믹이 없어 <b>지금은 무효과</b>다.</summary>
            StatusImmunity,

            /// <summary>맵 전체 적에게 피해. C01. amount = 피해량.</summary>
            DamageAllEnemies,

            /// <summary>맵의 일반 몬스터를 처치. C02. amount = 마리 수.</summary>
            KillEnemies,
        }

        [Serializable]
        public struct Entry
        {
            [Tooltip("CSV의 번호. R01, C01 …")]
            public string id;

            [Tooltip("이름(가칭).")]
            public string displayName;

            [Tooltip("유물인지 일회성인지.")]
            public Kind kind;

            [Tooltip("무슨 효과인지.")]
            public Effect effect;

            [Min(0), Tooltip("효과의 크기. 효과 종류마다 뜻이 다르다(Effect 주석 참조).")]
            public int amount;

            [Range(0, 100), Tooltip("확률이 필요한 효과에서만 쓴다(R07). 나머지는 무시된다.")]
            public int chance;

            [Min(0), Tooltip("상점 가격(골드).")]
            public int price;

            [Tooltip("CSV에서 수치가 '확정'인지. 꺼져 있으면 임시값이라는 뜻이다.")]
            public bool confirmed;

            [TextArea, Tooltip("설명. 인벤토리와 상점에 그대로 뜬다.")]
            public string description;
        }

        [SerializeField, Tooltip("전체 아이템. 상점은 이 중에서 뽑아 판다.")]
        private Entry[] items = new Entry[0];

        public Entry[] Items => items;

        /// <summary>id로 찾는다. 없으면 false.</summary>
        public bool TryGet(string id, out Entry entry)
        {
            if (items != null && !string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].id == id)
                    {
                        entry = items[i];
                        return true;
                    }
                }
            }

            entry = default(Entry);
            return false;
        }
    }
}
