using UnityEditor;
using UnityEngine;
using ObjectType;

namespace MazeGrid.Editor
{
    /// <summary>
    /// Shared grid visualization: unified grid drawing, cell rendering, separator indicators.
    /// </summary>
    public partial class MazeGridEditorModule
    {
        #region Constants

        private const float GRID_CELL_SIZE = 50f;
        private const float GRID_SEPARATOR_THICKNESS = 5f;

        private static readonly Color GRID_SEPARATOR_COLOR = new Color(1, 1, 1, 1);

        #endregion

        #region Unified Grid Drawing

        /// <summary>
        /// Draws the grid with optional cell interaction callback and custom cell renderer.
        /// </summary>
        private void DrawUnifiedGrid(
            MazeGridConfig grid,
            System.Action<int, MazeCellData> onCellInteraction = null,
            System.Action<Rect, int, MazeCellData> customCellRenderer = null)
        {
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            // Pre-calculate spawner indices for all cells
            int[] spawnerIndices = CalculateSpawnerIndices(grid);

            for (int row = 0; row < grid.rows; row++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int col = 0; col < grid.columns; col++)
                {
                    int cellIndex = grid.GetCellIndex(row, col);
                    var cell = grid.cells[cellIndex];

                    Rect cellRect = GUILayoutUtility.GetRect(GRID_CELL_SIZE, GRID_CELL_SIZE);

                    // Draw cell with spawner index
                    DrawUnifiedGridCell(cellRect, cellIndex, cell, spawnerIndices[cellIndex], onCellInteraction, customCellRenderer);

                    // Draw vertical separator indicator (if not last column)
                    if (col < grid.columns - 1)
                    {
                        DrawVerticalSeparatorIndicator(grid, row, col, GRID_CELL_SIZE, GRID_SEPARATOR_THICKNESS);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // Draw horizontal separators row (if not last row)
                if (row < grid.rows - 1)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    for (int col = 0; col < grid.columns; col++)
                    {
                        DrawHorizontalSeparatorIndicator(grid, row, col, GRID_CELL_SIZE, GRID_SEPARATOR_THICKNESS);

                        if (col < grid.columns - 1)
                        {
                            GUILayout.Space(GRID_SEPARATOR_THICKNESS);
                        }
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Pre-calculates spawner indices for all cells in the grid.
        /// Returns an array where each cell index maps to its spawner index (or -1 if not a spawner).
        /// </summary>
        public static int[] CalculateSpawnerIndices(MazeGridConfig grid)
        {
            int[] spawnerIndices = new int[grid.cells.Length];
            int currentSpawnerIndex = 0;

            for (int i = 0; i < grid.cells.Length; i++)
            {
                if (grid.cells[i].state == GridCellState.Spawner)
                {
                    spawnerIndices[i] = currentSpawnerIndex;
                    currentSpawnerIndex++;
                }
                else
                {
                    spawnerIndices[i] = -1;
                }
            }

            return spawnerIndices;
        }

        private void DrawUnifiedGridCell(
            Rect cellRect,
            int cellIndex,
            MazeCellData cell,
            int spawnerIndex,
            System.Action<int, MazeCellData> onCellInteraction,
            System.Action<Rect, int, MazeCellData> customCellRenderer)
        {
            // Draw cell background
            Color cellColor = GetCellColor(cell.state);
            EditorGUI.DrawRect(cellRect, cellColor);

            // Custom renderer or default rendering
            if (customCellRenderer != null)
            {
                customCellRenderer(cellRect, cellIndex, cell);
            }
            else
            {
                // Default: Draw cell label with spawner index
                DrawDefaultCellContent(cellRect, cell, spawnerIndex);
            }

            // Draw cell border
            DrawCellBorder(cellRect);

            // Handle interaction
            if (onCellInteraction != null)
            {
                Event evt = Event.current;
                if (evt.type == EventType.MouseDown && evt.button == 0 && cellRect.Contains(evt.mousePosition))
                {
                    onCellInteraction(cellIndex, cell);
                    evt.Use();
                }
            }
        }

        private void DrawDefaultCellContent(Rect cellRect, MazeCellData cell, int spawnerIndex)
        {
            switch (cell.state)
            {
                case GridCellState.Empty:
                    GUIStyle emptyStyle = new GUIStyle(EditorStyles.boldLabel);
                    emptyStyle.alignment = TextAnchor.MiddleCenter;
                    emptyStyle.fontSize = 24;
                    GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    GUI.Label(cellRect, "\u2715", emptyStyle);
                    GUI.color = Color.white;
                    break;

                case GridCellState.Full:
                    DrawDefaultCircle(cellRect, cell.itemTypeId);
                    break;

                case GridCellState.Spawner:
                    string symbol = GetDirectionSymbol(cell.direction);
                    GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = 20;
                    GUI.Label(cellRect, symbol, style);

                    // Draw slot count badge in bottom-right corner
                    int slotCount = cell.spawnerQueue != null ? cell.spawnerQueue.Count : 0;
                    Rect badgeRect = new Rect(cellRect.x + cellRect.width - 18, cellRect.y + cellRect.height - 18, 16, 16);
                    EditorGUI.DrawRect(badgeRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));

                    GUIStyle countStyle = new GUIStyle(EditorStyles.miniLabel);
                    countStyle.alignment = TextAnchor.MiddleCenter;
                    countStyle.fontSize = 10;
                    countStyle.normal.textColor = Color.white;
                    countStyle.fontStyle = FontStyle.Bold;
                    GUI.Label(badgeRect, slotCount.ToString(), countStyle);

                    // Draw spawner index badge in top-left corner (only if valid)
                    if (spawnerIndex >= 0)
                    {
                        Rect indexBadgeRect = new Rect(cellRect.x + 2, cellRect.y + 2, 16, 16);
                        EditorGUI.DrawRect(indexBadgeRect, new Color(0.3f, 0.3f, 0.8f, 0.8f));

                        GUIStyle indexStyle = new GUIStyle(EditorStyles.miniLabel);
                        indexStyle.alignment = TextAnchor.MiddleCenter;
                        indexStyle.fontSize = 10;
                        indexStyle.normal.textColor = Color.white;
                        indexStyle.fontStyle = FontStyle.Bold;
                        GUI.Label(indexBadgeRect, spawnerIndex.ToString(), indexStyle);
                    }
                    break;

                case GridCellState.GridWall:
                    DrawGridWallIcon(cellRect);
                    break;
            }
        }

        private void DrawGridWallIcon(Rect cellRect)
        {
            float lineWidth = cellRect.width * 0.6f;
            float lineHeight = 3f;
            float spacing = 6f;
            float totalHeight = lineHeight * 3 + spacing * 2;

            float startX = cellRect.x + (cellRect.width - lineWidth) / 2f;
            float startY = cellRect.y + (cellRect.height - totalHeight) / 2f;

            Color lineColor = new Color(0.3f, 0.3f, 0.3f);

            for (int i = 0; i < 3; i++)
            {
                Rect lineRect = new Rect(
                    startX,
                    startY + i * (lineHeight + spacing),
                    lineWidth,
                    lineHeight
                );
                EditorGUI.DrawRect(lineRect, lineColor);
            }
        }

        private void DrawDefaultCircle(Rect cellRect, int typeIndex)
        {
            Rect circleRect = new Rect(
                cellRect.x + cellRect.width * 0.3f,
                cellRect.y + cellRect.height * 0.3f,
                cellRect.width * 0.4f,
                cellRect.height * 0.4f
            );

            if (typeIndex >= 0)
            {
                if (_library != null && typeIndex < _library.objectTypes.Length)
                {
                    var type = _library.objectTypes[typeIndex];
                    Color typeColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;

                    Handles.BeginGUI();
                    Handles.color = typeColor;
                    Handles.DrawSolidDisc(circleRect.center, Vector3.forward, circleRect.width / 2f);
                    Handles.EndGUI();
                    return;
                }
            }

            // Draw hollow circle for unassigned types
            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.3f, 0.3f);
            Handles.DrawWireDisc(circleRect.center, Vector3.forward, circleRect.width / 2f, 2f);
            Handles.EndGUI();
        }

        #endregion

        #region Separator Indicators (Shared)

        private void DrawVerticalSeparatorIndicator(MazeGridConfig grid, int row, int col, float cellSize, float separatorThickness)
        {
            int separatorIndex = row * (grid.columns - 1) + col;

            Rect separatorRect = GUILayoutUtility.GetRect(separatorThickness, cellSize);

            if (separatorIndex >= 0 && separatorIndex < grid.verticalSeparators.Length)
            {
                bool hasSeparator = grid.verticalSeparators[separatorIndex];

                if (hasSeparator)
                {
                    float centerX = separatorRect.x + (separatorRect.width - separatorThickness) / 2f;
                    Rect centeredRect = new Rect(centerX, separatorRect.y, separatorThickness, cellSize);
                    EditorGUI.DrawRect(centeredRect, GRID_SEPARATOR_COLOR);
                }
            }
        }

        private void DrawHorizontalSeparatorIndicator(MazeGridConfig grid, int row, int col, float cellSize, float separatorThickness)
        {
            int separatorIndex = row * grid.columns + col;

            Rect separatorRect = GUILayoutUtility.GetRect(cellSize, separatorThickness);

            if (separatorIndex >= 0 && separatorIndex < grid.horizontalSeparators.Length)
            {
                bool hasSeparator = grid.horizontalSeparators[separatorIndex];

                if (hasSeparator)
                {
                    float centerY = separatorRect.y + (separatorRect.height - separatorThickness) / 2f;
                    Rect centeredRect = new Rect(separatorRect.x, centerY, cellSize, separatorThickness);
                    EditorGUI.DrawRect(centeredRect, GRID_SEPARATOR_COLOR);
                }
            }
        }

        #endregion

        #region Cell Visualization Helpers (Public Static)

        /// <summary>
        /// Returns the background color for a given cell state. Public static for reuse by game editors.
        /// </summary>
        public static Color GetCellColor(GridCellState state)
        {
            switch (state)
            {
                case GridCellState.Empty:
                    return new Color(0.5f, 0.5f, 0.5f);
                case GridCellState.Full:
                    return new Color(0.9f, 0.9f, 0.9f);
                case GridCellState.Spawner:
                    return new Color(.7f, .7f, .7f);
                case GridCellState.GridWall:
                    return new Color(0.8f, 0.6f, 0.6f);
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// Returns a Unicode arrow symbol for a spawner direction. Public static for reuse.
        /// </summary>
        public static string GetDirectionSymbol(SpawnerDirection direction)
        {
            switch (direction)
            {
                case SpawnerDirection.Up: return "\u2191";
                case SpawnerDirection.Down: return "\u2193";
                case SpawnerDirection.Left: return "\u2190";
                case SpawnerDirection.Right: return "\u2192";
                default: return "?";
            }
        }

        /// <summary>
        /// Draws a thin black border around a cell rect. Public static for reuse.
        /// </summary>
        public static void DrawCellBorder(Rect cellRect)
        {
            Handles.BeginGUI();
            Handles.color = Color.black;
            Handles.DrawPolyLine(
                new Vector3(cellRect.xMin, cellRect.yMin),
                new Vector3(cellRect.xMax, cellRect.yMin),
                new Vector3(cellRect.xMax, cellRect.yMax),
                new Vector3(cellRect.xMin, cellRect.yMax),
                new Vector3(cellRect.xMin, cellRect.yMin)
            );
            Handles.EndGUI();
        }

        /// <summary>
        /// Draws a colored circle (or hollow if typeIndex is negative) in a cell rect.
        /// Public static overload that takes an explicit library parameter.
        /// </summary>
        public static void DrawTypeCircle(Rect cellRect, int typeIndex, ObjectTypeLibrary library)
        {
            Rect circleRect = new Rect(
                cellRect.x + cellRect.width * 0.3f,
                cellRect.y + cellRect.height * 0.3f,
                cellRect.width * 0.4f,
                cellRect.height * 0.4f
            );

            if (typeIndex >= 0)
            {
                if (library != null && typeIndex < library.objectTypes.Length)
                {
                    var type = library.objectTypes[typeIndex];
                    Color typeColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;

                    Handles.BeginGUI();
                    Handles.color = typeColor;
                    Handles.DrawSolidDisc(circleRect.center, Vector3.forward, circleRect.width / 2f);
                    Handles.EndGUI();
                    return;
                }
            }

            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.3f, 0.3f);
            Handles.DrawWireDisc(circleRect.center, Vector3.forward, circleRect.width / 2f, 2f);
            Handles.EndGUI();
        }

        /// <summary>
        /// Returns a label for a cell based on its state.
        /// </summary>
        public static string GetCellLabel(MazeCellData cell)
        {
            switch (cell.state)
            {
                case GridCellState.Empty:
                    return "";
                case GridCellState.Full:
                    return "F";
                case GridCellState.Spawner:
                    return GetDirectionSymbol(cell.direction);
                case GridCellState.GridWall:
                    return "\u2261"; // Three horizontal lines symbol
                default:
                    return "?";
            }
        }

        #endregion
    }
}
