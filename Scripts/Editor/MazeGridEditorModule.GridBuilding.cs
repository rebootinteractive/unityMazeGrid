using UnityEditor;
using UnityEngine;
using ObjectType;

namespace MazeGrid.Editor
{
    /// <summary>
    /// Grid building sub-stage: grid size management, cell state selection, spawner configuration.
    /// </summary>
    public partial class MazeGridEditorModule
    {
        #region Grid Building Internal

        private void DrawGridBuildingInternal(MazeGridConfig grid)
        {
            // Grid size controls
            EditorGUILayout.LabelField("Grid Size", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Rows", GUILayout.Width(60));
            int newRows = EditorGUILayout.IntField(grid.rows, GUILayout.Width(60));

            GUILayout.Space(20);

            EditorGUILayout.LabelField("Columns", GUILayout.Width(60));
            int newColumns = EditorGUILayout.IntField(grid.columns, GUILayout.Width(60));

            if (EditorGUI.EndChangeCheck())
            {
                grid.rows = Mathf.Max(1, newRows);
                grid.columns = Mathf.Max(1, newColumns);
                grid.InitializeCells();
                grid.InitializeSeparators();
                NotifyChanged();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Grid control buttons (centered)
            EditorGUILayout.LabelField("Grid Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Row Top", GUILayout.Width(90)))
            {
                AddRowFromTop(grid);
                NotifyChanged();
            }
            if (GUILayout.Button("+ Row Bottom", GUILayout.Width(100)))
            {
                AddRowFromBottom(grid);
                NotifyChanged();
            }
            if (GUILayout.Button("- Row Top", GUILayout.Width(90)))
            {
                RemoveRowFromTop(grid);
                NotifyChanged();
            }
            if (GUILayout.Button("- Row Bottom", GUILayout.Width(100)))
            {
                RemoveRowFromBottom(grid);
                NotifyChanged();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Col Left", GUILayout.Width(90)))
            {
                AddColumnFromLeft(grid);
                NotifyChanged();
            }
            if (GUILayout.Button("+ Col Right", GUILayout.Width(100)))
            {
                AddColumnFromRight(grid);
                NotifyChanged();
            }
            if (GUILayout.Button("- Col Left", GUILayout.Width(90)))
            {
                RemoveColumnFromLeft(grid);
                NotifyChanged();
            }
            if (GUILayout.Button("- Col Right", GUILayout.Width(100)))
            {
                RemoveColumnFromRight(grid);
                NotifyChanged();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Cell state selection and Grid Log side by side
            EditorGUILayout.BeginHorizontal();

            // Left panel: Cell State Selection and Bulk Operations
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

            EditorGUILayout.LabelField("Cell State", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = _selectedCellState == GridCellState.Invalid ? Color.yellow : Color.white;
            if (GUILayout.Button("Invalid", GUILayout.Height(25)))
            {
                _selectedCellState = GridCellState.Invalid;
            }

            GUI.backgroundColor = _selectedCellState == GridCellState.Valid ? Color.yellow : Color.white;
            if (GUILayout.Button("Valid", GUILayout.Height(25)))
            {
                _selectedCellState = GridCellState.Valid;
            }

            GUI.backgroundColor = _selectedCellState == GridCellState.Spawner ? Color.yellow : Color.white;
            if (GUILayout.Button("Spawner", GUILayout.Height(25)))
            {
                _selectedCellState = GridCellState.Spawner;
            }

            GUI.backgroundColor = _selectedCellState == GridCellState.DummyValid ? Color.yellow : Color.white;
            if (GUILayout.Button("Dummy Valid", GUILayout.Height(25)))
            {
                _selectedCellState = GridCellState.DummyValid;
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Bulk operations
            EditorGUILayout.Space(5);

            if (GUILayout.Button("Make All Invalid", GUILayout.Height(25)))
            {
                MakeAllCells(grid, GridCellState.Invalid);
                NotifyChanged();
            }

            if (GUILayout.Button("Make All Valid", GUILayout.Height(25)))
            {
                MakeAllCells(grid, GridCellState.Valid);
                NotifyChanged();
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Right panel: Grid Log
            DrawGridLogPanel(null, _currentAllocations, GUILayout.ExpandWidth(true));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Grid visualization
            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);

            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;

                DrawUnifiedGrid(grid, OnGridBuildingCellClick);
            }

            EditorGUILayout.Space(10);

            // Spawner queue configuration with collapsible rows
            DrawSpawnerQueueManagement(grid);
        }

        #endregion

        #region Grid Cell Interaction

        private void OnGridBuildingCellClick(int cellIndex, MazeCellData cell)
        {
            var grid = _currentConfig;
            if (grid == null) return;

            // Check if Ctrl is pressed
            Event evt = Event.current;
            bool ctrlPressed = evt != null && (evt.control || evt.command);

            // Ctrl+Click on spawner: Focus on this spawner
            if (ctrlPressed && cell.state == GridCellState.Spawner)
            {
                FocusOnSpawner(cellIndex, grid);
                NotifyChanged();
                return;
            }

            // Special case: If cell is already a Spawner and Spawner is selected, cycle direction
            if (cell.state == GridCellState.Spawner && _selectedCellState == GridCellState.Spawner)
            {
                cell.direction = GetNextDirection(cell.direction);
            }
            else
            {
                // Set cell state on click
                cell.state = _selectedCellState;

                // Reset type index if changing to non-Valid state
                if (cell.state != GridCellState.Valid)
                {
                    cell.itemTypeId = -1;
                }

                // Initialize spawner queue if changing to Spawner
                if (cell.state == GridCellState.Spawner && cell.spawnerQueue == null)
                {
                    cell.spawnerQueue = new System.Collections.Generic.List<MazeCellData>();
                }
            }

            NotifyChanged();
        }

        private static SpawnerDirection GetNextDirection(SpawnerDirection current)
        {
            switch (current)
            {
                case SpawnerDirection.Up: return SpawnerDirection.Right;
                case SpawnerDirection.Right: return SpawnerDirection.Down;
                case SpawnerDirection.Down: return SpawnerDirection.Left;
                case SpawnerDirection.Left: return SpawnerDirection.Up;
                default: return SpawnerDirection.Down;
            }
        }

        #endregion

        #region Spawner Queue Management

        private void DrawSpawnerQueueManagement(MazeGridConfig grid)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header with expand/collapse all buttons
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Spawner Queue Management", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Expand All", GUILayout.Width(80)))
            {
                ExpandAllSpawners(grid);
            }

            if (GUILayout.Button("Collapse All", GUILayout.Width(80)))
            {
                CollapseAllSpawners(grid);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            bool hasSpawners = false;

            for (int i = 0; i < grid.cells.Length; i++)
            {
                var cell = grid.cells[i];
                if (cell.state == GridCellState.Spawner)
                {
                    hasSpawners = true;
                    int row = i / grid.columns;
                    int col = i % grid.columns;

                    // Ensure spawner has an expanded state
                    if (!_spawnerRowExpanded.ContainsKey(i))
                    {
                        _spawnerRowExpanded[i] = false;
                    }

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // Spawner header with foldout
                    EditorGUILayout.BeginHorizontal();

                    bool isExpanded = _spawnerRowExpanded[i];
                    string foldoutSymbol = isExpanded ? "\u25bc" : "\u25b6";
                    int queueCount = cell.spawnerQueue != null ? cell.spawnerQueue.Count : 0;
                    int emptySlots = 0;
                    if (cell.spawnerQueue != null)
                    {
                        foreach (var queueItem in cell.spawnerQueue)
                        {
                            if (queueItem == null || queueItem.itemTypeId < 0) emptySlots++;
                        }
                    }

                    if (GUILayout.Button($"{foldoutSymbol} Spawner [{row},{col}] {GetDirectionSymbol(cell.direction)} ({queueCount} slots, {emptySlots} empty)", EditorStyles.boldLabel, GUILayout.Width(330)))
                    {
                        _spawnerRowExpanded[i] = !_spawnerRowExpanded[i];
                    }

                    GUILayout.FlexibleSpace();

                    // Slot count field
                    EditorGUILayout.LabelField("Slots:", GUILayout.Width(40));
                    EditorGUI.BeginChangeCheck();
                    int newSlotCount = EditorGUILayout.IntField(queueCount, GUILayout.Width(40));
                    if (EditorGUI.EndChangeCheck())
                    {
                        ResizeSpawnerQueue(cell, newSlotCount);
                        NotifyChanged();
                    }

                    // Direction dropdown
                    EditorGUI.BeginChangeCheck();
                    SpawnerDirection newDirection = (SpawnerDirection)EditorGUILayout.EnumPopup(cell.direction, GUILayout.Width(80));
                    if (EditorGUI.EndChangeCheck())
                    {
                        cell.direction = newDirection;
                        NotifyChanged();
                    }

                    EditorGUILayout.EndHorizontal();

                    // Only show queue management if expanded
                    if (_spawnerRowExpanded[i])
                    {
                        EditorGUILayout.Space(5);

                        // Queue management buttons
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("+ Add Slot", GUILayout.Width(80)))
                        {
                            if (cell.spawnerQueue == null)
                                cell.spawnerQueue = new System.Collections.Generic.List<MazeCellData>();

                            cell.spawnerQueue.Add(new MazeCellData { state = GridCellState.Valid, itemTypeId = -1 });
                            NotifyChanged();
                        }

                        if (GUILayout.Button("Clear Queue", GUILayout.Width(80)))
                        {
                            if (cell.spawnerQueue != null)
                            {
                                cell.spawnerQueue.Clear();
                                NotifyChanged();
                            }
                        }

                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();

                        // Show queue slots
                        if (cell.spawnerQueue != null && cell.spawnerQueue.Count > 0)
                        {
                            EditorGUILayout.Space(5);
                            EditorGUILayout.LabelField($"Queue Slots ({cell.spawnerQueue.Count} total):", EditorStyles.miniBoldLabel);

                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();

                            for (int qIndex = 0; qIndex < cell.spawnerQueue.Count; qIndex++)
                            {
                                DrawQueueSlotBox(i, qIndex, cell);

                                if (qIndex < cell.spawnerQueue.Count - 1)
                                {
                                    GUILayout.Space(5);
                                }
                            }

                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();

                            // Queue management buttons (horizontal layout)
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();

                            for (int qIndex = 0; qIndex < cell.spawnerQueue.Count; qIndex++)
                            {
                                EditorGUILayout.BeginHorizontal(GUILayout.Width(GRID_CELL_SIZE));

                                // Left button (move left in queue)
                                if (GUILayout.Button("\u2190", GUILayout.Width(GRID_CELL_SIZE / 3f - 1), GUILayout.Height(18)) && qIndex > 0)
                                {
                                    var temp = cell.spawnerQueue[qIndex];
                                    cell.spawnerQueue[qIndex] = cell.spawnerQueue[qIndex - 1];
                                    cell.spawnerQueue[qIndex - 1] = temp;
                                    NotifyChanged();
                                }

                                // Remove button
                                if (GUILayout.Button("\u2715", GUILayout.Width(GRID_CELL_SIZE / 3f - 1), GUILayout.Height(18)))
                                {
                                    cell.spawnerQueue.RemoveAt(qIndex);
                                    NotifyChanged();
                                    break;
                                }

                                // Right button (move right in queue)
                                if (GUILayout.Button("\u2192", GUILayout.Width(GRID_CELL_SIZE / 3f - 1), GUILayout.Height(18)) && qIndex < cell.spawnerQueue.Count - 1)
                                {
                                    var temp = cell.spawnerQueue[qIndex];
                                    cell.spawnerQueue[qIndex] = cell.spawnerQueue[qIndex + 1];
                                    cell.spawnerQueue[qIndex + 1] = temp;
                                    NotifyChanged();
                                }

                                EditorGUILayout.EndHorizontal();

                                if (qIndex < cell.spawnerQueue.Count - 1)
                                {
                                    GUILayout.Space(5);
                                }
                            }

                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Queue is empty. Click '+ Add Slot' to add slots.", MessageType.Info);
                        }
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
            }

            if (!hasSpawners)
            {
                EditorGUILayout.HelpBox("No spawners configured. Select 'Spawner' cell state and click grid cells to create spawners.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQueueSlotBox(int spawnerIndex, int queueIndex, MazeCellData spawnerCell)
        {
            var queueItem = spawnerCell.spawnerQueue[queueIndex];
            int typeIndex = queueItem != null ? queueItem.itemTypeId : -1;

            Rect cellRect = GUILayoutUtility.GetRect(GRID_CELL_SIZE, GRID_CELL_SIZE);

            // Draw cell background - always use Full cell color
            Color cellColor = new Color(0.9f, 0.9f, 0.9f);
            EditorGUI.DrawRect(cellRect, cellColor);

            // Draw circle (same style as Full cells in grid)
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
                }
            }
            else
            {
                Handles.BeginGUI();
                Handles.color = new Color(0.3f, 0.3f, 0.3f);
                Handles.DrawWireDisc(circleRect.center, Vector3.forward, circleRect.width / 2f, 2f);
                Handles.EndGUI();
            }

            // Draw cell border
            DrawCellBorder(cellRect);
        }

        #endregion

        #region Grid Resize Operations

        private void AddRowFromTop(MazeGridConfig grid)
        {
            int newRows = grid.rows + 1;
            var newCells = new MazeCellData[newRows * grid.columns];

            for (int col = 0; col < grid.columns; col++)
            {
                newCells[col] = new MazeCellData { state = GridCellState.Valid, itemTypeId = -1 };
            }

            for (int row = 0; row < grid.rows; row++)
            {
                for (int col = 0; col < grid.columns; col++)
                {
                    int oldIndex = row * grid.columns + col;
                    int newIndex = (row + 1) * grid.columns + col;
                    newCells[newIndex] = grid.cells[oldIndex];
                }
            }

            int oldRows = grid.rows;
            grid.rows = newRows;
            grid.cells = newCells;

            int newHorzCount = (newRows - 1) * grid.columns;
            var newHorzSeparators = new bool[newHorzCount];

            if (grid.horizontalSeparators != null)
            {
                for (int row = 0; row < oldRows - 1; row++)
                {
                    for (int col = 0; col < grid.columns; col++)
                    {
                        int oldIdx = row * grid.columns + col;
                        int newIdx = (row + 1) * grid.columns + col;
                        newHorzSeparators[newIdx] = grid.horizontalSeparators[oldIdx];
                    }
                }
            }
            grid.horizontalSeparators = newHorzSeparators;

            int newVertCount = newRows * (grid.columns - 1);
            var newVertSeparators = new bool[newVertCount];

            if (grid.verticalSeparators != null)
            {
                for (int row = 0; row < oldRows; row++)
                {
                    for (int col = 0; col < grid.columns - 1; col++)
                    {
                        int oldIdx = row * (grid.columns - 1) + col;
                        int newIdx = (row + 1) * (grid.columns - 1) + col;
                        newVertSeparators[newIdx] = grid.verticalSeparators[oldIdx];
                    }
                }
            }
            grid.verticalSeparators = newVertSeparators;
        }

        private void AddRowFromBottom(MazeGridConfig grid)
        {
            int newRows = grid.rows + 1;
            var newCells = new MazeCellData[newRows * grid.columns];

            System.Array.Copy(grid.cells, newCells, grid.cells.Length);

            for (int col = 0; col < grid.columns; col++)
            {
                int newIndex = grid.rows * grid.columns + col;
                newCells[newIndex] = new MazeCellData { state = GridCellState.Valid, itemTypeId = -1 };
            }

            int oldRows = grid.rows;
            grid.rows = newRows;
            grid.cells = newCells;

            int newHorzCount = (newRows - 1) * grid.columns;
            var newHorzSeparators = new bool[newHorzCount];

            if (grid.horizontalSeparators != null)
            {
                System.Array.Copy(grid.horizontalSeparators, newHorzSeparators, grid.horizontalSeparators.Length);
            }
            grid.horizontalSeparators = newHorzSeparators;

            int newVertCount = newRows * (grid.columns - 1);
            var newVertSeparators = new bool[newVertCount];

            if (grid.verticalSeparators != null)
            {
                System.Array.Copy(grid.verticalSeparators, newVertSeparators, grid.verticalSeparators.Length);
            }
            grid.verticalSeparators = newVertSeparators;
        }

        private void RemoveRowFromTop(MazeGridConfig grid)
        {
            if (grid.rows <= 1) return;

            int newRows = grid.rows - 1;
            var newCells = new MazeCellData[newRows * grid.columns];

            for (int row = 1; row < grid.rows; row++)
            {
                for (int col = 0; col < grid.columns; col++)
                {
                    int oldIndex = row * grid.columns + col;
                    int newIndex = (row - 1) * grid.columns + col;
                    newCells[newIndex] = grid.cells[oldIndex];
                }
            }

            int oldRows = grid.rows;
            grid.rows = newRows;
            grid.cells = newCells;

            int newHorzCount = (newRows - 1) * grid.columns;
            var newHorzSeparators = new bool[newHorzCount];

            if (grid.horizontalSeparators != null)
            {
                for (int row = 1; row < oldRows - 1; row++)
                {
                    for (int col = 0; col < grid.columns; col++)
                    {
                        int oldIdx = row * grid.columns + col;
                        int newIdx = (row - 1) * grid.columns + col;
                        newHorzSeparators[newIdx] = grid.horizontalSeparators[oldIdx];
                    }
                }
            }
            grid.horizontalSeparators = newHorzSeparators;

            int newVertCount = newRows * (grid.columns - 1);
            var newVertSeparators = new bool[newVertCount];

            if (grid.verticalSeparators != null)
            {
                for (int row = 1; row < oldRows; row++)
                {
                    for (int col = 0; col < grid.columns - 1; col++)
                    {
                        int oldIdx = row * (grid.columns - 1) + col;
                        int newIdx = (row - 1) * (grid.columns - 1) + col;
                        newVertSeparators[newIdx] = grid.verticalSeparators[oldIdx];
                    }
                }
            }
            grid.verticalSeparators = newVertSeparators;
        }

        private void RemoveRowFromBottom(MazeGridConfig grid)
        {
            if (grid.rows <= 1) return;

            int newRows = grid.rows - 1;
            var newCells = new MazeCellData[newRows * grid.columns];

            System.Array.Copy(grid.cells, newCells, newCells.Length);

            int oldRows = grid.rows;
            grid.rows = newRows;
            grid.cells = newCells;

            int newHorzCount = (newRows - 1) * grid.columns;
            var newHorzSeparators = new bool[newHorzCount];

            if (grid.horizontalSeparators != null && newHorzCount > 0)
            {
                System.Array.Copy(grid.horizontalSeparators, newHorzSeparators, newHorzCount);
            }
            grid.horizontalSeparators = newHorzSeparators;

            int newVertCount = newRows * (grid.columns - 1);
            var newVertSeparators = new bool[newVertCount];

            if (grid.verticalSeparators != null && newVertCount > 0)
            {
                int oldVertCountPerRow = grid.columns - 1;
                for (int row = 0; row < newRows; row++)
                {
                    for (int col = 0; col < grid.columns - 1; col++)
                    {
                        int idx = row * oldVertCountPerRow + col;
                        newVertSeparators[idx] = grid.verticalSeparators[idx];
                    }
                }
            }
            grid.verticalSeparators = newVertSeparators;
        }

        private void AddColumnFromLeft(MazeGridConfig grid)
        {
            int newColumns = grid.columns + 1;
            var newCells = new MazeCellData[grid.rows * newColumns];

            for (int row = 0; row < grid.rows; row++)
            {
                newCells[row * newColumns] = new MazeCellData { state = GridCellState.Valid, itemTypeId = -1 };

                for (int col = 0; col < grid.columns; col++)
                {
                    int oldIndex = row * grid.columns + col;
                    int newIndex = row * newColumns + (col + 1);
                    newCells[newIndex] = grid.cells[oldIndex];
                }
            }

            int oldColumns = grid.columns;
            grid.columns = newColumns;
            grid.cells = newCells;

            int newHorzCount = (grid.rows - 1) * newColumns;
            var newHorzSeparators = new bool[newHorzCount];

            if (grid.horizontalSeparators != null)
            {
                for (int row = 0; row < grid.rows - 1; row++)
                {
                    for (int col = 0; col < oldColumns; col++)
                    {
                        int oldIdx = row * oldColumns + col;
                        int newIdx = row * newColumns + (col + 1);
                        newHorzSeparators[newIdx] = grid.horizontalSeparators[oldIdx];
                    }
                }
            }
            grid.horizontalSeparators = newHorzSeparators;

            int newVertCount = grid.rows * (newColumns - 1);
            var newVertSeparators = new bool[newVertCount];

            if (grid.verticalSeparators != null)
            {
                for (int row = 0; row < grid.rows; row++)
                {
                    for (int col = 0; col < oldColumns - 1; col++)
                    {
                        int oldIdx = row * (oldColumns - 1) + col;
                        int newIdx = row * (newColumns - 1) + (col + 1);
                        newVertSeparators[newIdx] = grid.verticalSeparators[oldIdx];
                    }
                }
            }
            grid.verticalSeparators = newVertSeparators;
        }

        private void AddColumnFromRight(MazeGridConfig grid)
        {
            int newColumns = grid.columns + 1;
            var newCells = new MazeCellData[grid.rows * newColumns];

            for (int row = 0; row < grid.rows; row++)
            {
                for (int col = 0; col < grid.columns; col++)
                {
                    int oldIndex = row * grid.columns + col;
                    int newIndex = row * newColumns + col;
                    newCells[newIndex] = grid.cells[oldIndex];
                }

                int newColIndex = row * newColumns + grid.columns;
                newCells[newColIndex] = new MazeCellData { state = GridCellState.Valid, itemTypeId = -1 };
            }

            int oldColumns = grid.columns;
            grid.columns = newColumns;
            grid.cells = newCells;

            int newHorzCount = (grid.rows - 1) * newColumns;
            var newHorzSeparators = new bool[newHorzCount];

            if (grid.horizontalSeparators != null)
            {
                for (int row = 0; row < grid.rows - 1; row++)
                {
                    for (int col = 0; col < oldColumns; col++)
                    {
                        int oldIdx = row * oldColumns + col;
                        int newIdx = row * newColumns + col;
                        newHorzSeparators[newIdx] = grid.horizontalSeparators[oldIdx];
                    }
                }
            }
            grid.horizontalSeparators = newHorzSeparators;

            int newVertCount = grid.rows * (newColumns - 1);
            var newVertSeparators = new bool[newVertCount];

            if (grid.verticalSeparators != null)
            {
                for (int row = 0; row < grid.rows; row++)
                {
                    for (int col = 0; col < oldColumns - 1; col++)
                    {
                        int oldIdx = row * (oldColumns - 1) + col;
                        int newIdx = row * (newColumns - 1) + col;
                        newVertSeparators[newIdx] = grid.verticalSeparators[oldIdx];
                    }
                }
            }
            grid.verticalSeparators = newVertSeparators;
        }

        private void RemoveColumnFromLeft(MazeGridConfig grid)
        {
            if (grid.columns <= 1) return;

            int newColumns = grid.columns - 1;
            var newCells = new MazeCellData[grid.rows * newColumns];

            for (int row = 0; row < grid.rows; row++)
            {
                for (int col = 1; col < grid.columns; col++)
                {
                    int oldIndex = row * grid.columns + col;
                    int newIndex = row * newColumns + (col - 1);
                    newCells[newIndex] = grid.cells[oldIndex];
                }
            }

            int oldColumns = grid.columns;
            grid.columns = newColumns;
            grid.cells = newCells;

            int newHorzCount = (grid.rows - 1) * newColumns;
            var newHorzSeparators = new bool[newHorzCount];

            if (grid.horizontalSeparators != null)
            {
                for (int row = 0; row < grid.rows - 1; row++)
                {
                    for (int col = 1; col < oldColumns; col++)
                    {
                        int oldIdx = row * oldColumns + col;
                        int newIdx = row * newColumns + (col - 1);
                        newHorzSeparators[newIdx] = grid.horizontalSeparators[oldIdx];
                    }
                }
            }
            grid.horizontalSeparators = newHorzSeparators;

            int newVertCount = grid.rows * (newColumns - 1);
            var newVertSeparators = new bool[newVertCount];

            if (grid.verticalSeparators != null && newColumns > 1)
            {
                for (int row = 0; row < grid.rows; row++)
                {
                    for (int col = 1; col < oldColumns - 1; col++)
                    {
                        int oldIdx = row * (oldColumns - 1) + col;
                        int newIdx = row * (newColumns - 1) + (col - 1);
                        newVertSeparators[newIdx] = grid.verticalSeparators[oldIdx];
                    }
                }
            }
            grid.verticalSeparators = newVertSeparators;
        }

        private void RemoveColumnFromRight(MazeGridConfig grid)
        {
            if (grid.columns <= 1) return;

            int newColumns = grid.columns - 1;
            var newCells = new MazeCellData[grid.rows * newColumns];

            for (int row = 0; row < grid.rows; row++)
            {
                for (int col = 0; col < newColumns; col++)
                {
                    int oldIndex = row * grid.columns + col;
                    int newIndex = row * newColumns + col;
                    newCells[newIndex] = grid.cells[oldIndex];
                }
            }

            int oldColumns = grid.columns;
            grid.columns = newColumns;
            grid.cells = newCells;

            int newHorzCount = (grid.rows - 1) * newColumns;
            var newHorzSeparators = new bool[newHorzCount];

            if (grid.horizontalSeparators != null)
            {
                for (int row = 0; row < grid.rows - 1; row++)
                {
                    for (int col = 0; col < newColumns; col++)
                    {
                        int oldIdx = row * oldColumns + col;
                        int newIdx = row * newColumns + col;
                        newHorzSeparators[newIdx] = grid.horizontalSeparators[oldIdx];
                    }
                }
            }
            grid.horizontalSeparators = newHorzSeparators;

            int newVertCount = grid.rows * (newColumns - 1);
            var newVertSeparators = new bool[newVertCount];

            if (grid.verticalSeparators != null && newColumns > 1)
            {
                for (int row = 0; row < grid.rows; row++)
                {
                    for (int col = 0; col < newColumns - 1; col++)
                    {
                        int oldIdx = row * (oldColumns - 1) + col;
                        int newIdx = row * (newColumns - 1) + col;
                        newVertSeparators[newIdx] = grid.verticalSeparators[oldIdx];
                    }
                }
            }
            grid.verticalSeparators = newVertSeparators;
        }

        #endregion

        #region Bulk Operations

        private void MakeAllCells(MazeGridConfig grid, GridCellState state)
        {
            for (int i = 0; i < grid.cells.Length; i++)
            {
                grid.cells[i].state = state;

                if (state != GridCellState.Valid)
                {
                    grid.cells[i].itemTypeId = -1;
                }

                if (state == GridCellState.Spawner && grid.cells[i].spawnerQueue == null)
                {
                    grid.cells[i].spawnerQueue = new System.Collections.Generic.List<MazeCellData>();
                }
            }
        }

        #endregion

        #region Spawner Queue Helpers

        /// <summary>
        /// Resizes a spawner queue while preserving existing content.
        /// </summary>
        private void ResizeSpawnerQueue(MazeCellData spawnerCell, int newSize)
        {
            newSize = Mathf.Max(0, newSize);

            if (spawnerCell.spawnerQueue == null)
            {
                spawnerCell.spawnerQueue = new System.Collections.Generic.List<MazeCellData>();
            }

            int currentSize = spawnerCell.spawnerQueue.Count;

            if (newSize > currentSize)
            {
                int slotsToAdd = newSize - currentSize;
                for (int i = 0; i < slotsToAdd; i++)
                {
                    spawnerCell.spawnerQueue.Add(new MazeCellData { state = GridCellState.Valid, itemTypeId = -1 });
                }
            }
            else if (newSize < currentSize)
            {
                int slotsToRemove = currentSize - newSize;
                spawnerCell.spawnerQueue.RemoveRange(newSize, slotsToRemove);
            }
        }

        #endregion
    }
}
