namespace RecycleLife.Core
{
    /// <summary>
    /// 난수 주입 지점. WEEK1 §0의 완료 기준이 "루프가 결정론적으로 도는가"이므로
    /// Core가 UnityEngine.Random을 직접 부르면 스폰 테스트를 쓸 수 없다.
    /// 테스트는 정해진 값을 뱉는 페이크를, 런타임은 시드 기반 구현을 넣는다.
    /// </summary>
    public interface IRandomSource
    {
        int NextInt(int minInclusive, int maxExclusive);
    }
}
