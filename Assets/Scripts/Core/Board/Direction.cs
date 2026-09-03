using UnityEngine;

namespace RecycleLife.Core
{
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
    }

    public static class DirectionExtensions
    {
        /// <summary>
        /// 좌상단 원점 좌표계(WEEK1 §1): row 증가 = 아래.
        /// 따라서 Up은 row -1, Down은 row +1이다.
        /// </summary>
        public static Vector2Int ToOffset(this Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return new Vector2Int(0, -1);
                case Direction.Down: return new Vector2Int(0, 1);
                case Direction.Left: return new Vector2Int(-1, 0);
                default: return new Vector2Int(1, 0);
            }
        }
    }
}
