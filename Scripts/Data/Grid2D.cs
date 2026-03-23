using UnityEngine;

namespace MazeGrid
{
    /// <summary>
    /// Generic 2D grid data structure with world-space conversion
    /// Grid[0,0] is top-left corner
    /// X axis = columns (horizontal), Z axis = rows (vertical)
    /// </summary>
    public class Grid2D<T>
    {
        private readonly T[,] cells;
        private readonly int width;
        private readonly int height;
        private readonly float cellSizeX;
        private readonly float cellSizeZ;
        private readonly Vector3 origin;
        private readonly bool negativeZDirection;

        public Grid2D(int width, int height, float cellSize, Vector3 origin, bool negativeZDirection = false)
            : this(width, height, cellSize, cellSize, origin, negativeZDirection)
        {
        }

        public Grid2D(int width, int height, float cellSizeX, float cellSizeZ, Vector3 origin, bool negativeZDirection = false)
        {
            this.width = width;
            this.height = height;
            this.cellSizeX = cellSizeX;
            this.cellSizeZ = cellSizeZ;
            this.origin = origin;
            this.negativeZDirection = negativeZDirection;
            this.cells = new T[width, height];
        }

        public int Width => width;
        public int Height => height;
        public float CellSizeX => cellSizeX;
        public float CellSizeZ => cellSizeZ;
        public Vector3 Origin => origin;

        public T this[int x, int z]
        {
            get => cells[x, z];
            set => cells[x, z] = value;
        }

        public T this[Vector2Int gridPos]
        {
            get => cells[gridPos.x, gridPos.y];
            set => cells[gridPos.x, gridPos.y] = value;
        }

        public bool IsValidPosition(int x, int z)
        {
            return x >= 0 && x < width && z >= 0 && z < height;
        }

        public bool IsValidPosition(Vector2Int gridPos)
        {
            return IsValidPosition(gridPos.x, gridPos.y);
        }

        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 localPos = worldPos - origin;
            int x = Mathf.RoundToInt(localPos.x / cellSizeX);
            int z;
            if (negativeZDirection)
            {
                z = Mathf.RoundToInt(-localPos.z / cellSizeZ);
            }
            else
            {
                z = Mathf.RoundToInt(localPos.z / cellSizeZ);
            }
            return new Vector2Int(x, z);
        }

        public Vector3 GridToWorld(int x, int z)
        {
            float worldZ = negativeZDirection ? -z * cellSizeZ : z * cellSizeZ;
            return origin + new Vector3(
                x * cellSizeX,
                0,
                worldZ
            );
        }

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return GridToWorld(gridPos.x, gridPos.y);
        }

        public T GetCellAtWorldPosition(Vector3 worldPos)
        {
            Vector2Int gridPos = WorldToGrid(worldPos);
            if (!IsValidPosition(gridPos))
                return default(T);
            return this[gridPos];
        }

        public void SetCellAtWorldPosition(Vector3 worldPos, T value)
        {
            Vector2Int gridPos = WorldToGrid(worldPos);
            if (!IsValidPosition(gridPos))
                return;
            this[gridPos] = value;
        }

        public void Clear()
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    cells[x, z] = default(T);
                }
            }
        }

        public void ForEach(System.Action<int, int, T> action)
        {
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    action(x, z, cells[x, z]);
                }
            }
        }
    }
}
