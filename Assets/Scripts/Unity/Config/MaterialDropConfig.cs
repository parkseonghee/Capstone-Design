using System;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 몬스터 종류별 재료 드롭 표(재료·조합 시스템 §1 "몬스터를 처치하면 일정 확률로 재료가 나옴").
    ///
    /// 한 종류가 여러 재료를 떨굴 수 있게 줄을 여러 개 둘 수 있다. 위에서부터 차례로 굴려
    /// <b>처음 맞은 하나</b>만 떨어진다 — 한 번 죽을 때 재료가 우르르 쏟아지면 조합이
    /// 너무 쉬워지기 때문이다.
    ///
    /// 확률도 어떤 재료가 나오는지도 전부 이 에셋에서 고친다(Hard Rule 1).
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

        [SerializeField, Tooltip("드롭 표. 위에서부터 굴려 처음 맞은 하나만 떨어진다.")]
        private Entry[] drops = new Entry[0];

        public Entry[] Drops => drops;

        public bool TryRoll(TrashType type, IRandomSource random, out string materialId)
        {
            materialId = null;

            if (drops == null || random == null)
            {
                return false;
            }

            for (int i = 0; i < drops.Length; i++)
            {
                if (drops[i].type != type || drops[i].chance <= 0 || string.IsNullOrEmpty(drops[i].materialId))
                {
                    continue;
                }

                // 0~99를 굴려 chance 미만이면 당첨. chance가 100이면 항상 맞는다.
                if (random.NextInt(0, 100) < drops[i].chance)
                {
                    materialId = drops[i].materialId;
                    return true;
                }
            }

            return false;
        }
    }
}
