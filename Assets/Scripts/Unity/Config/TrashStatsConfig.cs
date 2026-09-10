using System;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 보드에 나오는 블록의 종류별 데이터. CORE_COMBAT.md §1.
    ///
    /// EntityVisualSet(색·스프라이트)과 <b>따로</b> 둔다. 만지는 목적이 다르기 때문이다 —
    /// 저긴 아트, 여긴 밸런스다(Hard Rule 4).
    ///
    /// 한 표에 <b>적과 아이템이 같이</b> 들어간다. 무엇인지는 별도 종류 칸이 아니라 값이 정한다:
    ///  · Heal = 0  → 때려서 없애는 적 (Max Hp / Attack 사용)
    ///  · Heal &gt; 0 → 부딪히면 먹는 아이템 (Max Hp / Attack 은 0으로 강제)
    ///
    /// 새 아이템을 넣을 때 코드에 분기를 더하지 않고 이 표에 줄만 더하게 하려는 구조다(Hard Rule 3).
    ///
    /// 아래 값은 밸런싱 문서 v1의 초안 수치다. 플레이테스트 후 재조정 전제이며,
    /// 조정은 코드가 아니라 인스펙터에서 한다(Hard Rule 1).
    /// </summary>
    [CreateAssetMenu(fileName = "TrashStatsConfig", menuName = "RecycleLife/Trash Stats Config", order = 2)]
    public sealed class TrashStatsConfig : ScriptableObject, ITrashStatsProvider
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("연쇄 판정의 단위. 같은 종류끼리만 이어진다.")]
            public TrashType type;

            [Min(0), Tooltip("적일 때의 체력. 회복량이 0보다 크면(=아이템) 자동으로 0이 된다.")]
            public int maxHp;

            [Min(0), Tooltip("살아남았을 때 플레이어에게 돌려주는 반격 피해량. 아이템이면 0.")]
            public int attack;

            [Min(0), Tooltip("하나를 먹었을 때 회복하는 플레이어 체력. " +
                             "0보다 크면 이 종류는 '먹는 아이템'이 된다. 연쇄로 여러 개를 먹으면 합산된다.")]
            public int heal;

            [Min(0), Tooltip("스폰 추첨 가중치. 클수록 자주 나온다. 0이면 랜덤 스폰에 아예 안 나온다. " +
                             "다른 종류와의 상대값이라 전부 10이면 균등하다.")]
            public int spawnWeight;

            [Tooltip("같은 종류끼리 연쇄로 묶이는지. 적과 아이템은 켜고, 벽은 끈다. " +
                     "벽을 끄는 이유: 붙어 있는 벽 여러 개가 한 방에 같이 맞으면 " +
                     "'때리면 손해'라는 설계와 벽 하나당 타격 횟수 계산이 깨진다(밸런싱 v1 3장).")]
            public bool chainsWithSameType;
        }

        [Header("블록 종류별 (임시 밸런스 — 조절 대상)")]
        [SerializeField]
        private Entry[] entries =
        {
            // 잡몹 수치는 밸런싱 v1 §2, 벽 수치는 §3 표 그대로다.
            new Entry { type = TrashType.Paper, maxHp = 1, attack = 1, heal = 0, spawnWeight = 10, chainsWithSameType = true },
            new Entry { type = TrashType.Plastic, maxHp = 2, attack = 1, heal = 0, spawnWeight = 10, chainsWithSameType = true },
            new Entry { type = TrashType.Glass, maxHp = 3, attack = 2, heal = 0, spawnWeight = 10, chainsWithSameType = true },
            new Entry { type = TrashType.Potion, maxHp = 0, attack = 0, heal = 2, spawnWeight = 3, chainsWithSameType = true },
            new Entry { type = TrashType.Wood, maxHp = 3, attack = 0, heal = 0, spawnWeight = 3, chainsWithSameType = false },
            new Entry { type = TrashType.Concrete, maxHp = 5, attack = 0, heal = 0, spawnWeight = 2, chainsWithSameType = false },
            new Entry { type = TrashType.Steel, maxHp = 6, attack = 0, heal = 0, spawnWeight = 1, chainsWithSameType = false },
        };

        [Header("미지정 종류 폴백")]
        [SerializeField, Min(1), Tooltip("표에 없는 종류가 생겼을 때 쓰는 체력.")]
        private int fallbackMaxHp = 1;

        [SerializeField, Min(0), Tooltip("표에 없는 종류가 생겼을 때 쓰는 공격력.")]
        private int fallbackAttack = 1;

        /// <summary>
        /// 종류 수가 한 자리라 선형 탐색으로 충분하다.
        /// 스폰 시점과 종류 추첨에만 불리며, 할당은 없다(Hard Rule 8).
        /// </summary>
        public TrashStats For(TrashType type)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].type == type)
                    {
                        return new TrashStats(
                            entries[i].maxHp,
                            entries[i].attack,
                            entries[i].heal,
                            entries[i].spawnWeight,
                            entries[i].chainsWithSameType);
                    }
                }
            }

            // 표에 없는 종류는 스폰되면 안 되므로 가중치 0이다.
            return new TrashStats(fallbackMaxHp, fallbackAttack, 0, 0, true);
        }

        private void OnValidate()
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];

                if (entry.heal > 0)
                {
                    // 아이템은 체력·공격력을 쓰지 않는다. 둘 다 남아 있으면 하트가 그려지는 등
                    // 어정쩡한 상태가 되므로 여기서 정리해 표를 정직하게 유지한다.
                    entry.maxHp = 0;
                    entry.attack = 0;
                }
                else
                {
                    // 체력 0짜리 적은 놓자마자 죽은 상태라 의미가 없다.
                    entry.maxHp = Mathf.Max(1, entry.maxHp);
                }

                entries[i] = entry;
            }
        }
    }
}
