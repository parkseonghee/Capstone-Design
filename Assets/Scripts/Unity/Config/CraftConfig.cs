using System;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 조합법 표(재료·조합 시스템 §2 "인벤토리에서 아이템 조합으로 유물·폭탄을 만들게 함").
    ///
    /// 재료 둘을 겹치면 결과 하나가 나온다. <b>순서는 상관없다</b> — 나무+철과 철+나무가
    /// 다른 결과라면 플레이어가 외울 게 공연히 늘어난다. 기획의 "깡으로 때려맞추기"는
    /// 레시피를 몰라도 아무거나 겹쳐 볼 수 있다는 뜻이지, 순서까지 맞히라는 뜻이 아니다.
    ///
    /// 레시피를 안 쓴 조합은 그냥 실패한다 — 재료는 소모되지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "CraftConfig", menuName = "RecycleLife/Craft Config", order = 16)]
    public sealed class CraftConfig : ScriptableObject
    {
        [Serializable]
        public struct Recipe
        {
            [Tooltip("조합법 자체의 id(CR01 …). 배운 조합법을 저장할 때 쓰는 이름표라 " +
                     "한 번 정하면 바꾸지 않는다 — 바꾸면 플레이어가 산 기록을 잃는다.")]
            public string id;

            [Tooltip("재료 A의 아이템 id.")]
            public string inputA;

            [Tooltip("재료 B의 아이템 id.")]
            public string inputB;

            [Tooltip("나올 결과의 아이템 id. ItemConfig에 같은 id가 있어야 한다.")]
            public string result;

            [Min(0), Tooltip("마을에서 이 조합법을 사는 값(골드).")]
            public int price;

            [Tooltip("처음부터 알고 있는 조합법인지. 켜면 사지 않아도 목록에 재료가 보인다.")]
            public bool knownFromStart;
        }

        [SerializeField, Tooltip("조합법 목록.")]
        private Recipe[] recipes = new Recipe[0];

        public Recipe[] Recipes => recipes;

        /// <summary>두 재료로 만들 수 있는 조합법을 찾는다. 넣는 순서는 상관없다.</summary>
        public bool TryFind(string a, string b, out Recipe found)
        {
            found = default(Recipe);

            if (recipes == null || string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }

            for (int i = 0; i < recipes.Length; i++)
            {
                bool match = (recipes[i].inputA == a && recipes[i].inputB == b)
                             || (recipes[i].inputA == b && recipes[i].inputB == a);

                if (match && !string.IsNullOrEmpty(recipes[i].result))
                {
                    found = recipes[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>id로 조합법을 찾는다.</summary>
        public bool TryGet(string recipeId, out Recipe found)
        {
            found = default(Recipe);

            if (recipes == null || string.IsNullOrEmpty(recipeId))
            {
                return false;
            }

            for (int i = 0; i < recipes.Length; i++)
            {
                if (recipes[i].id == recipeId)
                {
                    found = recipes[i];
                    return true;
                }
            }

            return false;
        }
    }
}
