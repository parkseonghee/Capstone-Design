using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 보드에 놓이는 모든 것의 베이스. WEEK1_MOVEMENT_FALLING.md §1.
    /// Position은 BoardGrid만 갱신한다(internal set) — 격자와 엔티티 좌표가 어긋나지 않게 하기 위함.
    /// </summary>
    public abstract class Entity
    {
        public abstract EntityKind Kind { get; }

        public Vector2Int Position { get; internal set; }
    }
}
