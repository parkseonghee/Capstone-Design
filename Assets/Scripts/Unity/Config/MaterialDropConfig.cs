using System;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 재료 드랍 표(재화 문서 §3-1).
    ///
    ///  - <b>확률</b>: 몹이 아니라 스테이지 안 몇 번째 웨이브인지로 정한다 — 1웨이브 3% · 2웨이브 4% · 3웨이브 5%.
    ///    마을 강화 '재료 드랍률 +1%p'·유물 R10은 이 값에 더해진다(bonusChance).
    ///  - <b>무엇이 떨어지나</b>: 표에 줄이 있는 몹은 그 재료를(캔 → 고철 …), 표에 없는 몹은 기초 재료 중 하나를 무작위로.
    ///  - <b>보장</b>: <see cref="guaranteeEvery"/>마리를 잡을 때마다 기초 재료 하나를 무조건 준다.
    ///
    /// 한 종류에 줄이 여럿이면 그중 하나를 고르게 뽑는다. 확률도 재료도 전부 이 에셋에서 고친다(Hard Rule 1).
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
        }

        [SerializeField, Tooltip("출처 몹 → 재료. 확률은 아래 웨이브별 값을 쓴다.")]
        private Entry[] drops = new Entry[0];

        [Header("확률")]
        [SerializeField, Tooltip("스테이지 안 웨이브 순서대로(0번 = 1웨이브) 적 한 마리당 재료 드랍 확률(%). " +
                                 "표보다 뒤 웨이브는 마지막 값을 쓴다.")]
        private int[] chanceByWave = { 3, 4, 5 };

        [SerializeField, Tooltip("무작위로 고를 기초 재료들. 표에 없는 몹의 드랍과 보장 드랍이 여기서 뽑는다.")]
        private string[] randomPool = { "M01", "M02", "M03", "M04", "M05" };

        [Header("보장 드랍")]
        [SerializeField, Min(0), Tooltip("이 수만큼 처치할 때마다 기초 재료 하나를 무조건 준다. 0이면 끈다. " +
                                         "카운트는 웨이브를 넘어 이어지고 스테이지를 새로 시작하면 처음부터다.")]
        private int guaranteeEvery = 50;

        public Entry[] Drops => drops;

        public int GuaranteeEvery => guaranteeEvery;

        /// <summary>그 웨이브(스테이지 안 1부터)의 기본 드랍 확률(%). 보너스는 더하지 않은 값이다.</summary>
        public int ChanceFor(int waveInStage)
        {
            if (chanceByWave == null || chanceByWave.Length == 0)
            {
                return 0;
            }

            int i = Mathf.Clamp(waveInStage - 1, 0, chanceByWave.Length - 1);
            return Mathf.Max(0, chanceByWave[i]);
        }

        public bool TryRoll(TrashType type, int waveInStage, IRandomSource random, int bonusChance, out string materialId)
        {
            materialId = null;

            if (random == null)
            {
                return false;
            }

            int chance = ChanceFor(waveInStage) + Mathf.Max(0, bonusChance);

            // 0~99를 굴려 chance 미만이면 당첨. chance가 100 이상이면 항상 맞는다.
            if (chance <= 0 || random.NextInt(0, 100) >= chance)
            {
                return false;
            }

            int listed = CountListed(type);
            if (listed == 0)
            {
                return TryPickGuaranteed(random, out materialId);
            }

            int pick = listed == 1 ? 0 : random.NextInt(0, listed);
            for (int i = 0; i < drops.Length; i++)
            {
                if (!IsListed(drops[i], type))
                {
                    continue;
                }

                if (pick-- == 0)
                {
                    materialId = drops[i].materialId;
                    return true;
                }
            }

            return false;
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

        private int CountListed(TrashType type)
        {
            int n = 0;
            if (drops != null)
            {
                for (int i = 0; i < drops.Length; i++)
                {
                    if (IsListed(drops[i], type)) { n++; }
                }
            }

            return n;
        }

        private static bool IsListed(Entry entry, TrashType type)
            => entry.type == type && !string.IsNullOrEmpty(entry.materialId);
    }
}
