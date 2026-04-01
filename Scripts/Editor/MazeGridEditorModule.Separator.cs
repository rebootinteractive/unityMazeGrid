using UnityEditor;
using UnityEngine;
using ObjectType;

namespace MazeGrid.Editor
{
    /// <summary>
    /// Separator editing sub-stage: wall placement between grid cells.
    /// </summary>
    public partial class MazeGridEditorModule
    {
        #region Separator Internal

        private void DrawSeparatorsInternal(MazeGridConfig grid)
        {
            EditorGUILayout.LabelField("Separator Mode", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Click on lines between cells to toggle walls. Walls block pathfinding in-game.", MessageType.Info);

            EditorGUILayout.Space(10);

            // Grid with separator lines
            EditorGUILayout.LabelField("Grid with Separators", EditorStyles.boldLabel);

            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;

                DrawGridWithSeparators(grid);
            }

            EditorGUILayout.Space(10);

            // Statistics
            DrawSeparatorStats(grid);
        }

        #endregion

        #region Grid with Separators Drawing

        private void DrawGridWithSeparators(MazeGridConfig grid)
        {
            const float cellSize = 50f;
            const float separatorThickness = 20;

            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            for (int row = 0; row < grid.rows; row++)
            {
                // Draw cells and vertical separators
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int col = 0; col < grid.columns; col++)
                {
                    int cellIndex = grid.GetCellIndex(row, col);
                    var cell = grid.cells[cellIndex];

                    Rect cellRect = GUILayoutUtility.GetRect(cellSize, cellSize);

                    // Draw cell background
                    Color cellColor = GetCellColor(cell.state);
                    EditorGUI.DrawRect(cellRect, cellColor);

                    // Draw cell content
                    DrawSeparatorModeCellContent(cellRect, cell);

                    // Draw cell border
                    DrawCellBorder(cellRect);

                    // Draw vertical separator to the right (if not last column)
                    if (col < grid.columns - 1)
                    {
                        DrawVerticalSeparatorButton(grid, row, col, cellSize, separatorThickness);
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
                        if (col == 0)
                        {
                            GUILayout.Space(0);
                        }

                        DrawHorizontalSeparatorButton(grid, row, col, cellSize, separatorThickness);

                        if (col < grid.columns - 1)
                        {
                            GUILayout.Space(separatorThickness);
                        }
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawHorizontalSeparatorButton(MazeGridConfig grid, int row, int col, float cellSize, float thickness)
        {
            int separatorIndex = grid.GetHorizontalSeparatorIndex(row, col);
            if (separatorIndex < 0) return;

            bool hasWall = grid.horizontalSeparators[separatorIndex];

            Color buttonColor = hasWall ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f, 0.3f);
            GUI.backgroundColor = buttonColor;

            string label = hasWall ? "\u2550" : "\u2500";

            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.margin = new RectOffset(0, 0, 0, 0);

            if (GUILayout.Button(label, style, GUILayout.Width(cellSize), GUILayout.Height(thickness)))
            {
                grid.horizontalSeparators[separatorIndex] = !grid.horizontalSeparators[separatorIndex];
                NotifyChanged();
            }

            GUI.backgroundColor = Color.white;
        }

        private void DrawVerticalSeparatorButton(MazeGridConfig grid, int row, int col, float cellSize, float thickness)
        {
            int separatorIndex = grid.GetVerticalSeparatorIndex(row, col);
            if (separatorIndex < 0) return;

            bool hasWall = grid.verticalSeparators[separatorIndex];

            Color buttonColor = hasWall ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f, 0.3f);
            GUI.backgroundColor = buttonColor;

            string label = hasWall ? "\u2551" : "\u2502";

            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.margin = new RectOffset(0, 0, 0, 0);

            if (GUILayout.Button(label, style, GUILayout.Width(thickness), GUILayout.Height(cellSize)))
            {
                grid.verticalSeparators[separatorIndex] = !grid.verticalSeparators[separatorIndex];
                NotifyChanged();
            }

            GUI.backgroundColor = Color.white;
        }

        private void DrawSeparatorModeCellContent(Rect cellRect, MazeCellData cell)
        {
            switch (cell.state)
            {
                case GridCellState.Invalid:
                    GUIStyle emptyStyle = new GUIStyle(EditorStyles.boldLabel);
                    emptyStyle.alignment = TextAnchor.MiddleCenter;
                    emptyStyle.fontSize = 24;
                    GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    GUI.Label(cellRect, "\u2715", emptyStyle);
                    GUI.color = Color.white;
                    break;

                case GridCellState.Valid:
                    Rect circleRect = new Rect(
                        cellRect.x + cellRect.width * 0.3f,
                        cellRect.y + cellRect.height * 0.3f,
                        cellRect.width * 0.4f,
                        cellRect.height * 0.4f
                    );

                    if (cell.itemTypeId >= 0)
                    {
                        if (_library != null && cell.itemTypeId < _library.objectTypes.Length)
                        {
                            var type = _library.objectTypes[cell.itemTypeId];
                            Color typeColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;

                            Handles.BeginGUI();
                            Handles.color = typeColor;
                            Handles.DrawSolidDisc(circleRect.center, Vector3.forward, circleRect.width / 2f);
                            Handles.EndGUI();
                            break;
                        }
                    }

                    Handles.BeginGUI();
                    Handles.color = new Color(0.3f, 0.3f, 0.3f);
                    Handles.DrawWireDisc(circleRect.center, Vector3.forward, circleRect.width / 2f, 2f);
                    Handles.EndGUI();
                    break;

                case GridCellState.Spawner:
                    string symbol = GetDirectionSymbol(cell.direction);
                    GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = 20;
                    GUI.Label(cellRect, symbol, style);
                    break;
            }
        }

        #endregion

        #region Separator Statistics

        private void DrawSeparatorStats(MazeGridConfig grid)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Separator Statistics", EditorStyles.boldLabel);

            int horizontalWalls = 0;
            int verticalWalls = 0;

            if (grid.horizontalSeparators != null)
            {
                foreach (bool wall in grid.horizontalSeparators)
                {
                    if (wall) horizontalWalls++;
                }
            }

            if (grid.verticalSeparators != null)
            {
                foreach (bool wall in grid.verticalSeparators)
                {
                    if (wall) verticalWalls++;
                }
            }

            EditorGUILayout.LabelField($"Horizontal Walls: {horizontalWalls} / {grid.horizontalSeparators?.Length ?? 0}");
            EditorGUILayout.LabelField($"Vertical Walls: {verticalWalls} / {grid.verticalSeparators?.Length ?? 0}");
            EditorGUILayout.LabelField($"Total Walls: {horizontalWalls + verticalWalls}");

            EditorGUILayout.Space(5);

            // Bulk operations
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear All Separators", GUILayout.Width(150)))
            {
                ClearAllSeparators(grid);
                NotifyChanged();
            }

            if (GUILayout.Button("Fill All Separators", GUILayout.Width(150)))
            {
                FillAllSeparators(grid);
                NotifyChanged();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ClearAllSeparators(MazeGridConfig grid)
        {
            if (grid.horizontalSeparators != null)
            {
                for (int i = 0; i < grid.horizontalSeparators.Length; i++)
                {
                    grid.horizontalSeparators[i] = false;
                }
            }

            if (grid.verticalSeparators != null)
            {
                for (int i = 0; i < grid.verticalSeparators.Length; i++)
                {
                    grid.verticalSeparators[i] = false;
                }
            }
        }

        private void FillAllSeparators(MazeGridConfig grid)
        {
            if (grid.horizontalSeparators != null)
            {
                for (int i = 0; i < grid.horizontalSeparators.Length; i++)
                {
                    grid.horizontalSeparators[i] = true;
                }
            }

            if (grid.verticalSeparators != null)
            {
                for (int i = 0; i < grid.verticalSeparators.Length; i++)
                {
                    grid.verticalSeparators[i] = true;
                }
            }
        }

        #endregion
    }
}
