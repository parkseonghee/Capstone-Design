namespace RecycleLife.Core
{
    /// <summary>
    /// 종류별 전투 능력치의 읽기 전용 창구. Core는 ScriptableObject를 모른다(Hard Rule 5).
    /// 런타임 구현은 Unity 계층의 TrashStatsConfig(SO), 테스트 구현은 FakeTrashStats.
    ///
    /// 조회를 인터페이스로 둔 덕분에 "난이도가 오르면 스탯 세트를 통째로 갈아끼운다" 같은
    /// 확장이 코어 수정 없이 가능하다(Hard Rule 3).
    /// </summary>
    public interface ITrashStatsProvider
    {
        TrashStats For(TrashType type);
    }
}
