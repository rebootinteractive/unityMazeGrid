using System;

namespace MazeGrid
{
    [Serializable]
    public class MazeGridConfig
    {
        public int rows = 6;
        public int columns = 4;
        public int exitRow = 0;
        public MazeCellData[] cells;

        public bool[] horizontalSeparators;
        public bool[] verticalSeparators;

        public void InitializeCells()
        {
            if (cells == null || cells.Length != rows * columns)
            {
                cells = new MazeCellData[rows * columns];
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = new MazeCellData();
                }
            }

            InitializeSeparators();
        }

        public void InitializeSeparators()
        {
            int horzCount = (rows - 1) * columns;
            int vertCount = rows * (columns - 1);

            if (horizontalSeparators == null || horizontalSeparators.Length != horzCount)
            {
                horizontalSeparators = new bool[horzCount];
            }

            if (verticalSeparators == null || verticalSeparators.Length != vertCount)
            {
                verticalSeparators = new bool[vertCount];
            }
        }

        public int GetCellIndex(int row, int col)
        {
            if (row < 0 || row >= rows || col < 0 || col >= columns)
                return -1;
            return row * columns + col;
        }

        public int GetHorizontalSeparatorIndex(int row, int col)
        {
            if (row < 0 || row >= rows - 1 || col < 0 || col >= columns)
                return -1;
            return row * columns + col;
        }

        public int GetVerticalSeparatorIndex(int row, int col)
        {
            if (row < 0 || row >= rows || col < 0 || col >= columns - 1)
                return -1;
            return row * (columns - 1) + col;
        }
    }
}
