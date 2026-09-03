using System;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// WEEK1_MOVEMENT_FALLING.md §1의 Grid. 좌상단 원점, row 증가 = 아래.
    ///
    /// 이름이 Grid가 아닌 BoardGrid인 이유: UnityEngine.Grid(Tilemap 모듈)와 이름이 충돌한다.
    /// 문서의 Cell(= occupant 필드 하나)은 배열 슬롯과 동일하므로 Cell 객체를 따로 할당하지 않는다
    /// (8x12 = 96개 객체 할당 회피 — Hard Rule 8).
    /// </summary>
    public sealed class BoardGrid
    {
        private readonly Entity[] _cells;

        public BoardGrid(int cols, int rows)
        {
            if (cols <= 0) throw new ArgumentOutOfRangeException(nameof(cols));
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

            Cols = cols;
            Rows = rows;
            _cells = new Entity[cols * rows];
        }

        public int Cols { get; }

        public int Rows { get; }

        public int CellCount => _cells.Length;

        public Entity this[int col, int row] => _cells[Index(col, row)];

        public Entity this[Vector2Int position] => _cells[Index(position.x, position.y)];

        public bool InBounds(int col, int row)
        {
            return col >= 0 && col < Cols && row >= 0 && row < Rows;
        }

        public bool InBounds(Vector2Int position) => InBounds(position.x, position.y);

        public bool IsEmpty(int col, int row) => _cells[Index(col, row)] == null;

        public bool IsEmpty(Vector2Int position) => IsEmpty(position.x, position.y);

        public void Place(Entity entity, Vector2Int position)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            int index = Index(position.x, position.y);
            if (_cells[index] != null)
            {
                throw new InvalidOperationException($"Cell {position} is already occupied.");
            }

            _cells[index] = entity;
            entity.Position = position;
        }

        public Entity Remove(Vector2Int position)
        {
            int index = Index(position.x, position.y);
            Entity removed = _cells[index];
            _cells[index] = null;
            return removed;
        }

        public void Move(Vector2Int from, Vector2Int to)
        {
            if (from == to) return;

            int fromIndex = Index(from.x, from.y);
            int toIndex = Index(to.x, to.y);

            Entity entity = _cells[fromIndex];
            if (entity == null)
            {
                throw new InvalidOperationException($"No entity at {from}.");
            }

            if (_cells[toIndex] != null)
            {
                throw new InvalidOperationException($"Cell {to} is already occupied.");
            }

            _cells[fromIndex] = null;
            _cells[toIndex] = entity;
            entity.Position = to;
        }

        public int CountEmpty()
        {
            int count = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == null) count++;
            }

            return count;
        }

        public void Clear()
        {
            Array.Clear(_cells, 0, _cells.Length);
        }

        /// <summary>
        /// 4방향 인접 좌표를 호출자가 넘긴 버퍼에 채우고 개수를 돌려준다.
        /// 버퍼를 재사용하게 해서 매 호출 배열 할당을 피한다(Hard Rule 8).
        /// CORE_COMBAT.md §4의 Flood Fill이 이 유틸을 그대로 쓴다.
        /// </summary>
        public int GetNeighbors4(Vector2Int position, Vector2Int[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < 4) throw new ArgumentException("Buffer must hold at least 4 entries.", nameof(buffer));

            int count = 0;
            for (int i = 0; i < AllDirections.Length; i++)
            {
                Vector2Int neighbor = position + AllDirections[i].ToOffset();
                if (InBounds(neighbor))
                {
                    buffer[count++] = neighbor;
                }
            }

            return count;
        }

        /// <summary>열거 시 배열이 새로 생기지 않도록 한 번만 만들어 재사용한다.</summary>
        public static readonly Direction[] AllDirections =
        {
            Direction.Up,
            Direction.Down,
            Direction.Left,
            Direction.Right,
        };

        private int Index(int col, int row)
        {
            if (!InBounds(col, row))
            {
                throw new ArgumentOutOfRangeException($"({col}, {row}) is outside the {Cols}x{Rows} board.");
            }

            return row * Cols + col;
        }
    }
}
