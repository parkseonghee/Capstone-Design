using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// WEEK1_MOVEMENT_FALLING.md §7의 확정값을 담는 데이터 에셋.
    ///
    /// 값은 전부 인스펙터에서 조절한다(Hard Rule 1) — 코드 어디에도 8/12/1 리터럴이 없다.
    /// 팀이 8x8을 원하면 이 에셋의 값만 바꾸면 되고 스크립트는 건드리지 않는다(§7-4).
    ///
    /// Core는 이 타입을 모른다. IBoardConfig를 통해서만 읽으므로
    /// EditMode 테스트는 SO 없이 FakeBoardConfig를 넣는다(Hard Rule 5).
    /// </summary>
    [CreateAssetMenu(fileName = "GridConfig", menuName = "RecycleLife/Grid Config", order = 0)]
    public sealed class GridConfig : ScriptableObject, IBoardConfig
    {
        [Header("보드 크기 (WEEK1 §7-4)")]
        [SerializeField, Min(1), Tooltip("가로 칸 수. 확정값 8.")]
        private int cols = 8;

        [SerializeField, Min(1), Tooltip("세로 칸 수. 확정값 12 (모바일 세로).")]
        private int rows = 12;

        [Header("시작 연출 — 초기 줄 (스폰 방식과 무관하게 언제나 줄 단위)")]
        [SerializeField, Min(0), Tooltip("런 시작 시 한 줄씩 떨어지는 줄 수. 낙하 간격은 GameSession에서 정한다.")]
        private int rowsOnStart = 3;

        [SerializeField, Min(0), Tooltip("초기 줄에서 비워 둘 칸 수. 0이면 완전히 꽉 찬 줄이 내려온다.")]
        private int gapsPerRow = 1;

        [Header("진행 중 스폰")]
        [SerializeField, Tooltip("SingleBlock = 이동 1회마다 상단에 한 칸씩(기본). " +
                                 "FullRow = 몇 스텝마다 가로 한 줄이 통째로 내려온다.")]
        private SpawnMode spawnMode = SpawnMode.SingleBlock;

        [SerializeField, Min(0), Tooltip("SingleBlock: 스텝당 투입되는 쓰레기 수. WEEK1 §7-3의 값 1.")]
        private int spawnPerStep = 1;

        [SerializeField, Min(1), Tooltip("FullRow: 몇 스텝마다 새 줄이 내려오는지. 낮출수록 압박이 빨라진다.")]
        private int stepsPerRow = 3;

        [Header("스텝 진행 규칙 (WEEK1 §7-2)")]
        [SerializeField, Tooltip("쓰레기에 막힌 입력도 보드를 진행시킬지. 기본 false. " +
                                 "켜면 '막혀도 압박이 온다'는 원작 감각을 실험할 수 있다.")]
        private bool advanceOnBlocked;

        [Header("시작 배치")]
        [SerializeField, Tooltip("플레이어 시작 칸 (col, row). 좌상단 원점이라 row가 클수록 아래다. " +
                                 "초기 줄이 깔리는 바닥보다 위여야 시작하자마자 갇히지 않는다.")]
        private Vector2Int playerStart = new Vector2Int(4, 6);

        public int Cols => cols;

        public int Rows => rows;

        public SpawnMode Mode => spawnMode;

        public int RowsOnStart => rowsOnStart;

        public int StepsPerRow => stepsPerRow;

        public int GapsPerRow => gapsPerRow;

        public int SpawnPerStep => spawnPerStep;

        public bool AdvanceOnBlocked => advanceOnBlocked;

        public Vector2Int PlayerStart => playerStart;

        private void OnValidate()
        {
            // 잘못된 시작 칸으로 플레이 모드에 들어가면 런타임 예외가 나므로 에디터에서 잡아준다.
            playerStart.x = Mathf.Clamp(playerStart.x, 0, Mathf.Max(0, cols - 1));
            playerStart.y = Mathf.Clamp(playerStart.y, 0, Mathf.Max(0, rows - 1));
        }
    }
}
