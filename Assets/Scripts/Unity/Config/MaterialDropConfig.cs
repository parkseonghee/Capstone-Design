using System;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 재료 드랍 표(조합레시피 문서 §1 "50마리 처치마다 랜덤 재료 1개 보장 + 적 처치 시 5% 확률 드랍").
    ///
    ///  - <b>출처 몹</b>: 표에 줄이 있는 몹은 그 재료를 그 확률로 떨군다(캔 → 고철 …).
    ///  - <b>표에 없는 몹</b>: <see cref="defaultChance"/> 확률로 기초 재료 중 하나를 무작위로 떨군다.
    ///  - <b>보장</b>: <see cref="guaranteeEvery"/>마리를 잡을 때마다 기초 재료 하나를 무조건 준다.
    ///
    /// 한 종류가 여러 재료를 떨굴 수 있게 줄을 여러 개 둘 수 있다. 위에서부터 차례로 굴려
    /// <b>처음 맞은 하나</b>만 떨어진다. 확률도 재료도 전부 이 에셋에서 고친다(Hard Rule 1).
    /// </summary>
    [CreateAssetMenu(fileName = "MaterialDropConfig", menuName = "RecycleLife/Material Drop Config", order = 15)]
    public sealed class MaterialDropConfig : ScriptableObject, IMaterialDropTable
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("이 종류를 잡았을 때.")]
            public TrashType type;

            [Tooltip("떨어질 재료의 아이템 id. ItemConfig에 같은 id가 있어야 한다.")]
            public string materialId;

            [Range(0, 100), Tooltip("떨어질 확률(%).")]
            public int chance;
        }

        [SerializeField, Tooltip("출처 몹 드랍 표. 위에서부터 굴려 처음 맞은 하나만 떨어진다.")]
        private Entry[] drops = new Entry[0];

        [Header("표에 없는 몹")]
        [SerializeField, Range(0, 100), Tooltip("표에 줄이 없는 몹을 잡았을 때 드랍 확률(%). 무작위 기초 재료가 나온다.")]
        private int defaultChance = 5;

        [SerializeField, Tooltip("무작위로 고를 기초 재료들. 표에 없는 몹의 드랍과 보장 드랍이 여기서 뽑는다.")]
        private string[] randomPool = { "M01", "M02", "M03", "M04", "M05" };

        [Header("보장 드랍")]
        [SerializeField, Min(0), Tooltip("이 수만큼 처치할 때마다 기초 재료 하나를 무조건 준다. 0이면 끈다. " +
                                         "카운트는 웨이브를 넘어 이어지고 스테이지를 새로 시작하면 처음부터다.")]
        private int guaranteeEvery = 50;

        public Entry[] Drops => drops;

        public int GuaranteeEvery => guaranteeEvery;

        public bool TryRoll(TrashType type, IRandomSource random, int bonusChance, out string materialId)
        {
            materialId = null;

            if (random == null)
            {
                return false;
            }

            int bonus = Mathf.Max(0, bonusChance);
            bool listed = false;

            if (drops != null)
            {
                for (int i = 0; i < drops.Length; i++)
                {
                    if (drops[i].type != type || string.IsNullOrEmpty(drops[i].materialId))
                    {
                        continue;
                    }

                    listed = true;
                    if (drops[i].chance <= 0)
                    {
                        continue;
                    }

                    // 0~99를 굴려 chance 미만이면 당첨. chance가 100이면 항상 맞는다.
                    if (random.NextInt(0, 100) < drops[i].chance + bonus)
                    {
                        materialId = drops[i].materialId;
                        return true;
                    }
                }
            }

            // 표에 있는 몹은 표대로만 떨군다. 기본 확률은 표에 없는 몹 몫이다.
            if (listed || defaultChance <= 0 || random.NextInt(0, 100) >= defaultChance + bonus)
            {
                return false;
            }

            return TryPickGuaranteed(random, out materialId);
        }

        public bool TryPickGuaranteed(IRandomSource random, out string materialId)
        {
            materialId = null;

            if (random == null || randomPool == null || randomPool.Length == 0)
            {
                return false;
            }

            materialId = randomPool[random.NextInt(0, randomPool.Length)];
            return !string.IsNullOrEmpty(materialId);
        }
    }
}
