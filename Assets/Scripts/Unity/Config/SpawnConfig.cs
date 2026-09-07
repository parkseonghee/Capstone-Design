using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// WEEK1_MOVEMENT_FALLING.md §5의 스폰 캐이던스를 담는 데이터 에셋.
    ///
    /// 보드 규격(GridConfig)과 따로 둔 이유는 만지는 사람이 다르기 때문이다 —
    /// 여기 값은 밸런스, 저기 값은 판 크기다(Hard Rule 4).
    ///
    /// 확정값: 누적 15개까지는 매 턴 1개, 그 뒤로는 2턴당 1개(영구).
    /// 누적 개수에 시작 3줄은 포함되지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnConfig", menuName = "RecycleLife/Spawn Config", order = 1)]
    public sealed class SpawnConfig : ScriptableObject, ISpawnConfig
    {
        [Header("캐이던스 전환 (WEEK1 §5)")]
        [SerializeField, Min(0), Tooltip("이 개수를 넘기면 스폰이 느려진다. 확정값 15. " +
                                         "시작 3줄은 세지 않는다.")]
        private int blocksBeforeSlowdown = 15;

        [SerializeField, Min(1), Tooltip("느려지기 전 주기(턴). 확정값 1 = 매 턴 스폰.")]
        private int turnsPerSpawnEarly = 1;

        [SerializeField, Min(1), Tooltip("느려진 뒤 주기(턴). 확정값 2 = 2턴당 1개.")]
        private int turnsPerSpawnLate = 2;

        [Header("한 번에 나오는 양")]
        [SerializeField, Min(0), Tooltip("스폰 1회당 투입되는 쓰레기 수. 확정값 1.")]
        private int blocksPerSpawn = 1;

        public int BlocksBeforeSlowdown => blocksBeforeSlowdown;

        public int TurnsPerSpawnEarly => turnsPerSpawnEarly;

        public int TurnsPerSpawnLate => turnsPerSpawnLate;

        public int BlocksPerSpawn => blocksPerSpawn;
    }
}
