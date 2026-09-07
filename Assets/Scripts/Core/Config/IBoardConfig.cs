using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 보드 설정의 읽기 전용 창구. Core는 ScriptableObject를 모른다(Hard Rule 5).
    /// 런타임 구현은 Unity 계층의 GridConfig(SO), 테스트 구현은 FakeBoardConfig.
    /// 값 자체는 인스펙터에서 조절한다(Hard Rule 1) — 코드 리터럴 금지.
    /// </summary>
    public interface IBoardConfig
    {
        /// <summary>WEEK1 §1 확정값 8.</summary>
        int Cols { get; }

        /// <summary>
        /// 플레이어가 실제로 돌아다니는 줄 수. WEEK1 §1 확정값 8.
        /// </summary>
        int PlayableRows { get; }

        /// <summary>
        /// 최상단의 프리뷰(스폰 버퍼) 줄 수. WEEK1 §1 확정값 1.
        /// 이 줄은 플레이어가 들어갈 수 없고, 화면에도 일부만 노출된다(View 담당).
        /// 값으로 빼 둔 이유는 "프리뷰 2줄" 같은 실험을 스크립트 수정 없이 하기 위함이다.
        /// </summary>
        int PreviewRows { get; }

        /// <summary>
        /// 보드 전체 행 수 = PreviewRows + PlayableRows. 확정값 9.
        /// 구현체가 두 값에서 파생시킨다 — 따로 어긋난 값을 넣을 수 없게 하기 위함.
        /// </summary>
        int Rows { get; }

        /// <summary>
        /// 플레이 가능한 첫 행(= 프리뷰 바로 아래). row 인덱스 기준.
        /// 이동 규칙(§3)과 플레이어 배치가 이 경계를 쓴다.
        /// </summary>
        int FirstPlayableRow { get; }

        /// <summary>
        /// 런 시작 시 깔리는 줄 수. WEEK1 §5 확정값 3(하단 3줄 꽉 참).
        /// 낙하 간격(초)은 시간을 아는 Unity 계층이 정한다.
        /// </summary>
        int RowsOnStart { get; }

        /// <summary>한 줄에서 비워 둘 칸 수. 시작 3줄은 꽉 차야 하므로 0이 확정값이다.</summary>
        int GapsPerRow { get; }

        /// <summary>
        /// WEEK1 §3. 기본 false — 쓰레기에 막힌 입력은 보드를 진행시키지 않는다.
        /// true로 켜면 "막혀도 압박이 진행되는" 원작식 감각을 실험할 수 있다.
        /// </summary>
        bool AdvanceOnBlocked { get; }

        /// <summary>
        /// 플레이어 시작 칸. 시작 3줄 바로 위가 기본이며, 막혀 있으면
        /// GameLoop이 같은 컬럼에서 위로 올라가며 빈 칸을 찾는다.
        /// </summary>
        Vector2Int PlayerStart { get; }
    }
}
