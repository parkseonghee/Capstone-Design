namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을 건물을 눌렀을 때 열리는 기능 화면(스테이지 지도, 조합법 상점, 재활용 센터 등).
    /// <see cref="VillageManager"/>는 이것만 보고 열고, <see cref="IsOpen"/>이 false가 되면 닫혔다고 본다.
    /// </summary>
    public interface IVillageFeature
    {
        bool IsOpen { get; }

        void Open();
    }
}
