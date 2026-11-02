using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 스테이지별 스크랩 드랍 확률(재화 문서 §3-2).
    ///
    /// | 스테이지 | 드랍 확률 | 한 런 스크랩(600마리) |
    /// |---|---|---|
    /// | 공원 | 25% | 150 |
    /// | 한강 | 30% | 180 |
    /// | 해수욕장 | 35% | 210 |
    ///
    /// 스테이지마다 차등을 두는 건 필수다 — 어느 스테이지든 600마리로 고정이라 확률이 같으면
    /// 가장 쉬운 공원만 반복하는 게 최적 전략이 된다. 값은 전부 이 에셋에서 고친다(Hard Rule 1).
    /// </summary>
    [CreateAssetMenu(fileName = "ScrapDropConfig", menuName = "RecycleLife/Scrap Drop Config", order = 16)]
    public sealed class ScrapDropConfig : ScriptableObject, IScrapDropTable
    {
        [SerializeField, Tooltip("스테이지 순서대로(0번 = 1스테이지 공원) 적 한 마리당 스크랩 드랍 확률(%). " +
                                 "표보다 뒤 스테이지는 마지막 값을 쓴다.")]
        private int[] chanceByStage = { 25, 30, 35 };

        [SerializeField, Min(0), Tooltip("한 번 떨어질 때 들어오는 스크랩 수.")]
        private int amountPerDrop = 1;

        public int AmountPerDrop => amountPerDrop;

        public int ChanceFor(int stageNumber)
        {
            if (chanceByStage == null || chanceByStage.Length == 0)
            {
                return 0;
            }

            int i = Mathf.Clamp(stageNumber - 1, 0, chanceByStage.Length - 1);
            return Mathf.Clamp(chanceByStage[i], 0, 100);
        }
    }
}
