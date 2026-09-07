using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// WEEK1_MOVEMENT_FALLING.md §1의 보드 규격을 담는 데이터 에셋.
    ///
    /// 값은 전부 인스펙터에서 조절한다(Hard Rule 1) — 코드 어디에도 8/9/1 리터럴이 없다.
    /// 팀이 보드를 9열로 넓히고 싶으면 이 에셋만 바꾸면 되고 스크립트는 건드리지 않는다.
    ///
    /// 스폰 캐이던스는 여기 없다 — 밸런스는 SpawnConfig 에셋이 따로 갖는다(Hard Rule 4).
    ///
    /// Core는 이 타입을 모른다. IBoardConfig를 통해서만 읽으므로
    /// EditMode 테스트는 SO 없이 FakeBoardConfig를 넣는다(Hard Rule 5).
    /// </summary>
    [CreateAssetMenu(fileName = "GridConfig", menuName = "RecycleLife/Grid Config", order = 0)]
    public sealed class GridConfig : ScriptableObject, IBoardConfig
    {
        [Header("보드 규격 (WEEK1 §1)")]
        [SerializeField, Min(1), Tooltip("가로 칸 수. 확정값 8.")]
        private int cols = 8;

        [SerializeField, Min(1), Tooltip("플레이어가 돌아다니는 줄 수. 확정값 8 (8x8 = 64칸).")]
        private int playableRows = 8;

        [SerializeField, Min(0), Tooltip("최상단 프리뷰(스폰 버퍼) 줄 수. 확정값 1. " +
                                         "플레이어는 이 줄에 못 들어가고, 화면에도 1/6만 노출된다.")]
        private int previewRows = 1;

        [Header("시작 상태 (WEEK1 §5)")]
        [SerializeField, Min(0), Tooltip("런 시작 시 하단에 깔리는 줄 수. 확정값 3.")]
        private int rowsOnStart = 3;

        [SerializeField, Min(0), Tooltip("시작 줄에서 비워 둘 칸 수. 확정값 0 = 꽉 찬 줄.")]
        private int gapsPerRow;

        [SerializeField, Tooltip("플레이어 시작 칸 (col, row). 좌상단 원점이라 row가 클수록 아래다. " +
                                 "시작 3줄 바로 위가 기본이다. 막혀 있으면 같은 컬럼에서 위로 자리를 찾는다.")]
        private Vector2Int playerStart = new Vector2Int(4, 5);

        [Header("스텝 진행 규칙 (WEEK1 §3)")]
        [SerializeField, Tooltip("쓰레기에 막힌 입력도 보드를 진행시킬지. 기본 false. " +
                                 "켜면 '막혀도 압박이 온다'는 원작 감각을 실험할 수 있다.")]
        private bool advanceOnBlocked;

        public int Cols => cols;

        public int PlayableRows => playableRows;

        public int PreviewRows => previewRows;

        public int Rows => playableRows + previewRows;

        public int FirstPlayableRow => previewRows;

        public int RowsOnStart => rowsOnStart;

        public int GapsPerRow => gapsPerRow;

        public bool AdvanceOnBlocked => advanceOnBlocked;

        public Vector2Int PlayerStart => playerStart;

        private void OnValidate()
        {
            // 잘못된 시작 칸으로 플레이 모드에 들어가면 런타임 예외가 나므로 에디터에서 잡아준다.
            playerStart.x = Mathf.Clamp(playerStart.x, 0, Mathf.Max(0, cols - 1));
            playerStart.y = Mathf.Clamp(playerStart.y, FirstPlayableRow, Mathf.Max(FirstPlayableRow, Rows - 1));

            // 시작 줄이 플레이 영역보다 많으면 플레이어가 설 자리가 없다.
            rowsOnStart = Mathf.Clamp(rowsOnStart, 0, Mathf.Max(0, playableRows - 1));
            gapsPerRow = Mathf.Clamp(gapsPerRow, 0, Mathf.Max(0, cols - 1));
        }
    }
}
