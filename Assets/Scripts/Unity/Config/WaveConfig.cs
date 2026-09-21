using System;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 웨이브 하나의 설정. 밸런싱 v1 §2-1 표의 한 줄에 해당한다.
    ///
    /// 목표 처치 수와 스폰 비중을 <b>전부 인스펙터에서</b> 만진다.
    /// 플레이하면서 바로 값을 고치라고 만든 에셋이라 코드에 숫자를 두지 않는다(Hard Rule 1).
    ///
    /// 여기 적힌 종류만 나온다 — 목록에 없는 종류는 가중치 0으로 취급돼
    /// 이 웨이브에는 아예 등장하지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "Wave", menuName = "RecycleLife/Wave Config", order = 10)]
    public sealed class WaveConfig : ScriptableObject, IWaveConfig
    {
        [Serializable]
        public struct SpawnEntry
        {
            [Tooltip("이 웨이브에 나올 종류.")]
            public TrashType type;

            [Min(0), Tooltip("뽑힐 가중치. 클수록 자주 나온다. 0이면 안 나온다. " +
                             "다른 줄과의 상대값이라 전부 10이면 균등하다.")]
            public int weight;
        }

        [Header("표시용")]
        [SerializeField, Min(1), Tooltip("몇 번째 스테이지인지(1~3). 화면 표시에만 쓴다.")]
        private int stageNumber = 1;

        [SerializeField, Min(1), Tooltip("몇 번째 웨이브인지(1~9). 화면 표시에만 쓴다.")]
        private int waveNumber = 1;

        [Header("클리어 조건")]
        [SerializeField, Min(0), Tooltip("이 웨이브를 넘기는 데 필요한 적 처치 수. " +
                                         "진행도 바가 이만큼 차면 다음 웨이브로 넘어간다. " +
                                         "플레이하면서 조절할 값이다.")]
        private int killGoal = 15;

        [Header("등장 구성")]
        [SerializeField, Tooltip("이 웨이브에 나오는 종류와 비중. 여기 없는 종류는 나오지 않는다. " +
                                 "잡몹 3종 외에 포션과 벽을 넣을지도 여기서 정한다.")]
        private SpawnEntry[] spawns = Array.Empty<SpawnEntry>();

        public int StageNumber => stageNumber;

        public int WaveNumber => waveNumber;

        public int KillGoal => killGoal;

        /// <summary>
        /// 줄 수가 한 자리라 선형 탐색으로 충분하다. 스폰 추첨에서만 불리며 할당은 없다(Hard Rule 8).
        /// </summary>
        public int SpawnWeightFor(TrashType type)
        {
            if (spawns == null)
            {
                return 0;
            }

            for (int i = 0; i < spawns.Length; i++)
            {
                if (spawns[i].type == type)
                {
                    return Mathf.Max(0, spawns[i].weight);
                }
            }

            return 0;
        }
    }
}
