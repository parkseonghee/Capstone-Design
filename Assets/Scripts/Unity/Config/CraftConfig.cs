using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 조합법 표(리사이클라이프_아이템_조합레시피.md).
    ///
    /// 재료 2~5개를 넣으면 결과 하나가 나온다. <b>순서는 상관없다</b>(문서 §1 "정렬 후 비교") —
    /// 같은 재료를 두 번 넣는 레시피(펠릿 ×2 + 필름)도 있어서, 개수까지 맞아야 일치로 본다.
    ///
    /// 2개짜리(승급·혼합 재료, C1 소모품)는 인벤토리에서 두 칸을 겹쳐 만들고,
    /// 3개 이상은 조합대에서 만든다. 조합법 자체는 한 표에 같이 있다.
    ///
    /// 레시피에 없는 조합은 그냥 실패한다 — 지금은 재료가 소모되지 않는다
    /// (문서의 "실패 시 재료 소멸"은 아직 넣지 않기로 했다).
    /// </summary>
    [CreateAssetMenu(fileName = "CraftConfig", menuName = "RecycleLife/Craft Config", order = 16)]
    public sealed class CraftConfig : ScriptableObject
    {
        /// <summary>조합대 한 번에 넣을 수 있는 최대 재료 수(T3 유물 = 5개).</summary>
        public const int MaxInputs = 5;

        [Serializable]
        public struct Recipe
        {
            [Tooltip("조합법 자체의 id. 배운 조합법을 저장할 때 쓰는 이름표라 " +
                     "한 번 정하면 바꾸지 않는다 — 바꾸면 플레이어가 산 기록을 잃는다.")]
            public string id;

            [Tooltip("재료 아이템 id들(2~5개). 순서는 상관없고, 같은 재료를 두 번 넣으면 두 개가 필요하다.")]
            public string[] inputs;

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
            return TryFind(new[] { a, b }, out found);
        }

        /// <summary>
        /// 재료 묶음으로 만들 수 있는 조합법을 찾는다. 순서는 상관없고 개수는 정확히 맞아야 한다
        /// (펠릿 ×2 + 필름 레시피에 펠릿 하나 + 필름을 넣으면 안 맞는다).
        /// </summary>
        public bool TryFind(IReadOnlyList<string> ingredients, out Recipe found)
        {
            found = default(Recipe);

            if (recipes == null || ingredients == null || ingredients.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < ingredients.Count; i++)
            {
                if (string.IsNullOrEmpty(ingredients[i]))
                {
                    return false;
                }
            }

            string[] wanted = Sorted(ingredients);

            for (int i = 0; i < recipes.Length; i++)
            {
                string[] inputs = recipes[i].inputs;
                if (inputs == null || inputs.Length != wanted.Length || string.IsNullOrEmpty(recipes[i].result))
                {
                    continue;
                }

                if (SameSorted(Sorted(inputs), wanted))
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

        private static string[] Sorted(IReadOnlyList<string> ids)
        {
            var copy = new string[ids.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = ids[i];
            }

            Array.Sort(copy, StringComparer.Ordinal);
            return copy;
        }

        private static bool SameSorted(string[] a, string[] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
