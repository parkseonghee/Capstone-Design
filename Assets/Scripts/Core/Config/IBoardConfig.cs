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
        /// <summary>WEEK1 §7-4 확정값 8.</summary>
        int Cols { get; }

        /// <summary>WEEK1 §7-4 확정값 12.</summary>
        int Rows { get; }

        /// <summary>진행 중 쓰레기가 들어오는 방식. 어떤 ITrashSpawner를 쓸지 고른다.</summary>
        SpawnMode Mode { get; }

        /// <summary>
        /// 런 시작 시 한 줄씩 떨어지는 줄 수. 시작 연출은 Mode와 무관하게 언제나 줄 단위다.
        /// 낙하 간격(초)은 시간을 아는 Unity 계층이 정한다.
        /// </summary>
        int RowsOnStart { get; }

        /// <summary>한 줄에서 비워 둘 칸 수. 0이면 완전히 꽉 찬 줄이 내려온다.</summary>
        int GapsPerRow { get; }

        /// <summary>SpawnMode.SingleBlock에서 스텝당 투입되는 쓰레기 수. WEEK1 §7-3의 값 1.</summary>
        int SpawnPerStep { get; }

        /// <summary>SpawnMode.FullRow에서 몇 스텝마다 새 줄이 내려오는지.</summary>
        int StepsPerRow { get; }

        /// <summary>
        /// WEEK1 §7-2. 기본 false — 쓰레기에 막힌 입력은 보드를 진행시키지 않는다.
        /// true로 켜면 "막혀도 압박이 진행되는" 원작식 감각을 실험할 수 있다.
        /// </summary>
        bool AdvanceOnBlocked { get; }

        /// <summary>플레이어 시작 칸. 문서에 값이 없어 설정으로 노출한다.</summary>
        Vector2Int PlayerStart { get; }
    }
}
