using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 화면 아래쪽(y가 작은) 오브젝트가 앞에 그려지도록 y 위치를 Sorting Order로 바꾼다.
    /// 건물과 걸어다니는 캐릭터가 같은 규칙을 써야 서로 자연스럽게 겹친다.
    /// </summary>
    public static class YSorting
    {
        /// <param name="y">발 위치의 월드 y.</param>
        /// <param name="ordersPerUnit">1 유닛당 Sorting Order 차이. 클수록 촘촘하게 구분한다.</param>
        /// <param name="offset">같은 y에서 더 앞(+)/뒤(-)로 밀 값.</param>
        public static int OrderFor(float y, float ordersPerUnit, int offset = 0)
        {
            return Mathf.RoundToInt(-y * ordersPerUnit) + offset;
        }
    }
}
