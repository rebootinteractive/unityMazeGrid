using UnityEditor;
using UnityEngine;
using ObjectType;

namespace MazeGrid.Editor
{
    /// <summary>
    /// Type painting sub-stage: type assignment for Full cells and spawner queues using drag-and-drop.
    /// Also includes the Grid Log panel and Type Log panel.
    /// </summary>
    public partial class MazeGridEditorModule
    {
        #region Type Painting Internal

        private void DrawTypePaintingInternal(MazeGridConfig grid, TypeAllocation[] allocations)
        {
            EditorGUILayout.LabelField("Type Painting", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Drag and drop types onto Full cells and spawner queues.", MessageType.Info);

            EditorGUILayout.Space(10);

            // Type source buttons
            EditorGUILayout.LabelField("Type Sources (Click to start drag)", EditorStyles.boldLabel);
            DrawTypePaintingTypeSourceButtons(allocations);

            EditorGUILayout.Space(5);

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            DrawRandomDistributeButton(grid, allocations);
            GUILayout.Space(10);
            DrawClearTypesButton(grid);
            GUILayout.Space(10);
            DrawMakeAllHiddenButton(grid);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Grid visualization with type painting and trash area side by side
            EditorGUILayout.BeginHorizontal();

            // Left: Grid
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Grid (Drag types onto cells)", EditorStyles.boldLabel);

            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;

                DrawUnifiedGrid(grid, null, RenderTypePaintingCell);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Right: Trash area
            DrawTrashArea(_isDragging, _isDragging && !_dragSourceIsTypeSource, () =>
            {
                RemoveTypeFromDragSource();
                EndDrag();
            });

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Spawner queue configuration with horizontal cells
            DrawSpawnerQueueConfiguration(grid);

            EditorGUILayout.Space(10);

            // Type Log Panel
            DrawTypeLogPanel(grid, allocations);

            // Handle drag rendering (ghost circle)
            HandleDragRendering();
        }

        #endregion

        #region Type Source Buttons (Type Painting)

        private void DrawTypePaintingTypeSourceButtons(TypeAllocation[] allocations)
        {
            if (_library == null || _library.objectTypes == null || _library.objectTypes.Length == 0)
            {
                EditorGUILayout.HelpBox("No object types found in ObjectTypeLibrary.", MessageType.Warning);
                return;
            }

            if (allocations != null && allocations.Length > 0)
            {
                // Use allocations to build type source buttons
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                int typesPerRow = 6;

                for (int i = 0; i < allocations.Length; i += typesPerRow)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    for (int j = 0; j < typesPerRow && (i + j) < allocations.Length; j++)
                    {
                        int idx = i + j;
                        int typeIndex = allocations[idx].typeIndex;

                        if (typeIndex < 0 || typeIndex >= _library.objectTypes.Length) continue;

                        var type = _library.objectTypes[typeIndex];
                        Color buttonColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;
                        GUI.backgroundColor = buttonColor;

                        string buttonLabel = $"{type.typeName} x{allocations[idx].instanceCount}";

                        if (GUILayout.Button(buttonLabel, GUILayout.Width(80), GUILayout.Height(40)))
                        {
                            StartDragFromTypeSource(typeIndex);
                        }

                        GUI.backgroundColor = Color.white;
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
            else
            {
                // Fallback: show all types from library
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                int typesPerRow = 6;
                int typeCount = _library.objectTypes.Length;

                for (int i = 0; i < typeCount; i += typesPerRow)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    for (int j = 0; j < typesPerRow && (i + j) < typeCount; j++)
                    {
                        int typeIndex = i + j;
                        var type = _library.objectTypes[typeIndex];

                        Color buttonColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;
                        GUI.backgroundColor = buttonColor;

                        string buttonLabel = type.typeName;

                        if (GUILayout.Button(buttonLabel, GUILayout.Width(80), GUILayout.Height(40)))
                        {
                            StartDragFromTypeSource(typeIndex);
                        }

                        GUI.backgroundColor = Color.white;
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        #endregion

        #region Grid for Type Painting with Drag-and-Drop

        private void RenderTypePaintingCell(Rect cellRect, int cellIndex, MazeCellData cell)
        {
            switch (cell.state)
            {
                case GridCellState.Empty:
                    break;

                case GridCellState.Full:
                    DrawCellTypeCircle(cellRect, cell.itemTypeId);

                    if (cell.isHidden)
                    {
                        DrawHiddenOverlay(cellRect);
                    }

                    DrawHiddenCheckbox(cellRect, cell);
                    HandleCellMouseInteraction(cellRect, cellIndex, cell);
                    break;

                case GridCellState.Spawner:
                    DrawSpawnerWithBadges(cellRect, cell);
                    HandleCellMouseInteraction(cellRect, cellIndex, cell);
                    break;
            }
        }

        private void DrawCellTypeCircle(Rect cellRect, int typeIndex)
        {
            Rect circleRect = new Rect(
                cellRect.x + cellRect.width * 0.2f,
                cellRect.y + cellRect.height * 0.2f,
                cellRect.width * 0.6f,
                cellRect.height * 0.6f
            );

            if (typeIndex >= 0 && _library != null && typeIndex < _library.objectTypes.Length)
            {
                var type = _library.objectTypes[typeIndex];
                Color typeColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;
                DrawCircle(circleRect, typeColor);
            }
            else
            {
                DrawHollowCircle(circleRect, Color.gray, 2f);
            }
        }

        private static void DrawCircle(Rect rect, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidDisc(rect.center, Vector3.forward, rect.width / 2f);
            Handles.EndGUI();
        }

        private static void DrawHollowCircle(Rect rect, Color color, float thickness)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawWireDisc(rect.center, Vector3.forward, rect.width / 2f, thickness);
            Handles.EndGUI();
        }

        private static void DrawHiddenOverlay(Rect cellRect)
        {
            Color overlayColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
            EditorGUI.DrawRect(cellRect, overlayColor);
        }

        private void DrawHiddenCheckbox(Rect cellRect, MazeCellData cell)
        {
            float checkboxSize = 14f;
            float padding = 0f;

            Rect checkboxRect = new Rect(
                cellRect.x + padding,
                cellRect.yMax - checkboxSize - padding,
                checkboxSize,
                checkboxSize
            );

            EditorGUI.BeginChangeCheck();
            bool newValue = GUI.Toggle(checkboxRect, cell.isHidden, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                cell.isHidden = newValue;
                NotifyChanged();
            }
        }

        private void DrawSpawnerSymbol(Rect cellRect, SpawnerDirection direction)
        {
            string symbol = GetDirectionSymbol(direction);
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 20;
            GUI.Label(cellRect, symbol, style);
        }

        private void DrawSpawnerWithBadges(Rect cellRect, MazeCellData cell)
        {
            DrawSpawnerSymbol(cellRect, cell.direction);

            if (cell.spawnerQueue != null && cell.spawnerQueue.Count > 0)
            {
                // Draw count badge
                Rect badgeRect = new Rect(cellRect.x, cellRect.y, 13, 13);
                EditorGUI.DrawRect(badgeRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));

                GUIStyle countStyle = new GUIStyle(EditorStyles.miniLabel);
                countStyle.alignment = TextAnchor.MiddleCenter;
                countStyle.fontSize = 9;
                countStyle.normal.textColor = Color.white;
                GUI.Label(badgeRect, cell.spawnerQueue.Count.ToString(), countStyle);

                // Draw small colored circles for first few types
                int maxBadges = Mathf.Min(cell.spawnerQueue.Count, 5);
                float badgeSize = 5f;
                float spacing = 5f;
                float startY = cellRect.y + 1;

                for (int i = 0; i < maxBadges; i++)
                {
                    int typeIndex = cell.spawnerQueue[i];
                    if (typeIndex >= 0 && _library != null && typeIndex < _library.objectTypes.Length)
                    {
                        var type = _library.objectTypes[typeIndex];
                        Color typeColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;

                        Vector2 badgeCenter = new Vector2(
                            cellRect.x + cellRect.width - 8,
                            startY + i * (badgeSize + spacing) + badgeSize / 2f
                        );

                        Handles.BeginGUI();
                        Handles.color = typeColor;
                        Handles.DrawSolidDisc(badgeCenter, Vector3.forward, badgeSize / 2f);
                        Handles.color = Color.black;
                        Handles.DrawWireDisc(badgeCenter, Vector3.forward, badgeSize / 2f, 1f);
                        Handles.EndGUI();
                    }
                    else if (typeIndex == -1)
                    {
                        Vector2 badgeCenter = new Vector2(
                            cellRect.x + cellRect.width - 8,
                            startY + i * (badgeSize + spacing) + badgeSize / 2f
                        );

                        Handles.BeginGUI();
                        Handles.color = Color.gray;
                        Handles.DrawWireDisc(badgeCenter, Vector3.forward, badgeSize / 2f, 1f);
                        GUIStyle emptyStyle = new GUIStyle(EditorStyles.boldLabel);
                        emptyStyle.alignment = TextAnchor.MiddleCenter;
                        emptyStyle.fontSize = 10;
                        GUI.color = Color.gray;
                        GUI.Label(new Rect(badgeCenter.x - 10, badgeCenter.y - 10, 20, 20), "\u2715", emptyStyle);
                        GUI.color = Color.white;
                        Handles.EndGUI();
                    }
                }
            }
        }

        private void HandleCellMouseInteraction(Rect cellRect, int cellIndex, MazeCellData cell)
        {
            Event evt = Event.current;

            if (cellRect.Contains(evt.mousePosition))
            {
                // Highlight on hover
                if (!_isDragging)
                {
                    EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, cellRect.height), new Color(1f, 1f, 1f, 0.2f));
                }

                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    if (_isDragging)
                    {
                        DropTypeOnCell(cellIndex, cell);
                        evt.Use();
                    }
                    else if (cell.state == GridCellState.Spawner)
                    {
                        FocusOnSpawner(cellIndex, _currentConfig);
                        evt.Use();
                    }
                    else if (cell.itemTypeId >= 0)
                    {
                        StartDragFromCell(cellIndex, cell);
                        evt.Use();
                    }
                }
            }
        }

        private void DropTypeOnCell(int cellIndex, MazeCellData cell)
        {
            var grid = _currentConfig;
            if (grid == null) return;

            if (_dragSourceIsTypeSource)
            {
                cell.itemTypeId = _draggedTypeIndex;
                NotifyChanged();
            }
            else if (_dragSourceCellIndex >= 0)
            {
                var sourceCell = grid.cells[_dragSourceCellIndex];
                int tempType = sourceCell.itemTypeId;
                sourceCell.itemTypeId = cell.itemTypeId;
                cell.itemTypeId = tempType;
                NotifyChanged();
            }
            else if (_dragSourceSpawnerCellIndex >= 0 && _dragSourceSpawnerQueueIndex >= 0)
            {
                var spawnerCell = grid.cells[_dragSourceSpawnerCellIndex];
                int tempType = spawnerCell.spawnerQueue[_dragSourceSpawnerQueueIndex];
                spawnerCell.spawnerQueue[_dragSourceSpawnerQueueIndex] = cell.itemTypeId;
                cell.itemTypeId = tempType;
                NotifyChanged();
            }

            EndDrag();
        }

        #endregion

        #region Spawner Queue Configuration (Type Painting)

        private void DrawSpawnerQueueConfiguration(MazeGridConfig grid)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header with expand/collapse all buttons
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Spawner Type Assignment", EditorStyles.boldLabel);

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
                        foreach (int typeIndex in cell.spawnerQueue)
                        {
                            if (typeIndex < 0) emptySlots++;
                        }
                    }

                    if (GUILayout.Button($"{foldoutSymbol} Spawner [{row},{col}] {GetDirectionSymbol(cell.direction)} ({queueCount} items, {emptySlots} empty)", EditorStyles.boldLabel, GUILayout.Width(350)))
                    {
                        _spawnerRowExpanded[i] = !_spawnerRowExpanded[i];
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    // Only show queue content if expanded
                    if (_spawnerRowExpanded[i])
                    {
                        if (cell.spawnerQueue != null && cell.spawnerQueue.Count > 0)
                        {
                            EditorGUILayout.Space(5);
                            EditorGUILayout.LabelField($"Queue ({cell.spawnerQueue.Count} items):", EditorStyles.miniBoldLabel);

                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();

                            for (int qIndex = 0; qIndex < cell.spawnerQueue.Count; qIndex++)
                            {
                                DrawSpawnerQueueCell(i, qIndex, cell);

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

                                if (GUILayout.Button("\u2190", GUILayout.Width(GRID_CELL_SIZE / 3f - 1), GUILayout.Height(18)) && qIndex > 0)
                                {
                                    int temp = cell.spawnerQueue[qIndex];
                                    cell.spawnerQueue[qIndex] = cell.spawnerQueue[qIndex - 1];
                                    cell.spawnerQueue[qIndex - 1] = temp;
                                    NotifyChanged();
                                }

                                if (GUILayout.Button("\u2715", GUILayout.Width(GRID_CELL_SIZE / 3f - 1), GUILayout.Height(18)))
                                {
                                    cell.spawnerQueue.RemoveAt(qIndex);
                                    NotifyChanged();
                                    break;
                                }

                                if (GUILayout.Button("\u2192", GUILayout.Width(GRID_CELL_SIZE / 3f - 1), GUILayout.Height(18)) && qIndex < cell.spawnerQueue.Count - 1)
                                {
                                    int temp = cell.spawnerQueue[qIndex];
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
                            EditorGUILayout.HelpBox("Queue is empty. Go to Grid Building to add queue slots.", MessageType.Info);
                        }
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
            }

            if (!hasSpawners)
            {
                EditorGUILayout.HelpBox("No spawners configured. Go to Grid Building to add spawners.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void ExpandAllSpawners(MazeGridConfig grid)
        {
            for (int i = 0; i < grid.cells.Length; i++)
            {
                if (grid.cells[i].state == GridCellState.Spawner)
                {
                    _spawnerRowExpanded[i] = true;
                }
            }
        }

        private void CollapseAllSpawners(MazeGridConfig grid)
        {
            for (int i = 0; i < grid.cells.Length; i++)
            {
                if (grid.cells[i].state == GridCellState.Spawner)
                {
                    _spawnerRowExpanded[i] = false;
                }
            }
        }

        private void FocusOnSpawner(int spawnerIndex, MazeGridConfig grid)
        {
            CollapseAllSpawners(grid);
            _spawnerRowExpanded[spawnerIndex] = true;
        }

        private void DrawSpawnerQueueCell(int spawnerIndex, int queueIndex, MazeCellData spawnerCell)
        {
            int typeIndex = spawnerCell.spawnerQueue[queueIndex];

            Rect cellRect = GUILayoutUtility.GetRect(GRID_CELL_SIZE, GRID_CELL_SIZE);

            Color cellColor = new Color(0.9f, 0.9f, 0.9f);
            EditorGUI.DrawRect(cellRect, cellColor);

            DrawSpawnerQueueCellContent(cellRect, typeIndex);
            DrawCellBorder(cellRect);
            HandleSpawnerQueueCellInteraction(cellRect, spawnerIndex, queueIndex, spawnerCell);
        }

        private void DrawSpawnerQueueCellContent(Rect cellRect, int typeIndex)
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
                }
            }
            else
            {
                Handles.BeginGUI();
                Handles.color = new Color(0.3f, 0.3f, 0.3f);
                Handles.DrawWireDisc(circleRect.center, Vector3.forward, circleRect.width / 2f, 2f);
                Handles.EndGUI();
            }
        }

        private void HandleSpawnerQueueCellInteraction(Rect cellRect, int spawnerIndex, int queueIndex, MazeCellData spawnerCell)
        {
            Event evt = Event.current;

            if (cellRect.Contains(evt.mousePosition))
            {
                if (!_isDragging)
                {
                    EditorGUI.DrawRect(cellRect, new Color(1f, 1f, 1f, 0.2f));
                }

                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    if (_isDragging)
                    {
                        DropTypeOnSpawnerQueueCell(spawnerIndex, queueIndex, spawnerCell);
                        evt.Use();
                    }
                    else if (spawnerCell.spawnerQueue[queueIndex] >= 0)
                    {
                        StartDragFromSpawnerQueueCell(spawnerIndex, queueIndex, spawnerCell);
                        evt.Use();
                    }
                }
            }
        }

        private void DropTypeOnSpawnerQueueCell(int spawnerIndex, int queueIndex, MazeCellData spawnerCell)
        {
            var grid = _currentConfig;
            if (grid == null) return;

            if (_dragSourceIsTypeSource)
            {
                spawnerCell.spawnerQueue[queueIndex] = _draggedTypeIndex;
                NotifyChanged();
            }
            else if (_dragSourceCellIndex >= 0)
            {
                var sourceCell = grid.cells[_dragSourceCellIndex];
                int tempType = sourceCell.itemTypeId;
                sourceCell.itemTypeId = spawnerCell.spawnerQueue[queueIndex];
                spawnerCell.spawnerQueue[queueIndex] = tempType;
                NotifyChanged();
            }
            else if (_dragSourceSpawnerCellIndex >= 0 && _dragSourceSpawnerQueueIndex >= 0)
            {
                var sourceSpawnerCell = grid.cells[_dragSourceSpawnerCellIndex];
                int tempType = sourceSpawnerCell.spawnerQueue[_dragSourceSpawnerQueueIndex];
                sourceSpawnerCell.spawnerQueue[_dragSourceSpawnerQueueIndex] = spawnerCell.spawnerQueue[queueIndex];
                spawnerCell.spawnerQueue[queueIndex] = tempType;
                NotifyChanged();
            }

            EndDrag();
        }

        #endregion

        #region Random Distribute

        private void DrawRandomDistributeButton(MazeGridConfig grid, TypeAllocation[] allocations)
        {
            bool canDistribute = CanRandomDistribute(grid, allocations);

            EditorGUI.BeginDisabledGroup(!canDistribute);

            if (GUILayout.Button("Random Distribute", GUILayout.Height(30)))
            {
                RandomDistributeTypes(grid, allocations);
            }

            EditorGUI.EndDisabledGroup();

            if (!canDistribute)
            {
                int totalInstances = CalculateTotalInstanceCount(allocations);
                int totalSlots = CalculateTotalSlotCount(grid);
                EditorGUILayout.HelpBox($"Random Distribute requires: Total instances ({totalInstances}) == Total slots ({totalSlots})", MessageType.Info);
            }
        }

        private void DrawClearTypesButton(MazeGridConfig grid)
        {
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);

            if (GUILayout.Button("Clear Types", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear All Types",
                    "Are you sure you want to clear all type assignments from grid cells and spawner queues?",
                    "Clear", "Cancel"))
                {
                    ClearAllTypes(grid);
                }
            }

            GUI.backgroundColor = Color.white;
        }

        private void ClearAllTypes(MazeGridConfig grid)
        {
            foreach (var cell in grid.cells)
            {
                if (cell.state == GridCellState.Full)
                {
                    cell.itemTypeId = -1;
                }
                else if (cell.state == GridCellState.Spawner && cell.spawnerQueue != null)
                {
                    for (int i = 0; i < cell.spawnerQueue.Count; i++)
                    {
                        cell.spawnerQueue[i] = -1;
                    }
                }
            }

            NotifyChanged();
        }

        private void DrawMakeAllHiddenButton(MazeGridConfig grid)
        {
            GUI.backgroundColor = new Color(0.6f, 0.6f, 0.8f);

            if (GUILayout.Button("Make All Hidden", GUILayout.Height(30)))
            {
                MakeAllCellsHidden(grid);
            }

            GUI.backgroundColor = Color.white;
        }

        private void MakeAllCellsHidden(MazeGridConfig grid)
        {
            foreach (var cell in grid.cells)
            {
                if (cell.state == GridCellState.Full)
                {
                    cell.isHidden = true;
                }
            }

            NotifyChanged();
        }

        private bool CanRandomDistribute(MazeGridConfig grid, TypeAllocation[] allocations)
        {
            if (allocations == null || allocations.Length == 0)
                return false;

            int totalInstances = CalculateTotalInstanceCount(allocations);
            int totalSlots = CalculateTotalSlotCount(grid);

            return totalInstances == totalSlots;
        }

        private int CalculateTotalInstanceCount(TypeAllocation[] allocations)
        {
            if (allocations == null) return 0;

            int total = 0;
            foreach (var allocation in allocations)
            {
                total += allocation.instanceCount;
            }
            return total;
        }

        private int CalculateTotalSlotCount(MazeGridConfig grid)
        {
            int fullCellCount = 0;
            int spawnerQueueCount = 0;

            foreach (var cell in grid.cells)
            {
                if (cell.state == GridCellState.Full)
                    fullCellCount++;
                else if (cell.state == GridCellState.Spawner && cell.spawnerQueue != null)
                    spawnerQueueCount += cell.spawnerQueue.Count;
            }

            return fullCellCount + spawnerQueueCount;
        }

        private void RandomDistributeTypes(MazeGridConfig grid, TypeAllocation[] allocations)
        {
            // Create a list of all type indices to assign
            var typePool = new System.Collections.Generic.List<int>();
            foreach (var allocation in allocations)
            {
                for (int i = 0; i < allocation.instanceCount; i++)
                {
                    typePool.Add(allocation.typeIndex);
                }
            }

            // Shuffle the pool (Fisher-Yates)
            var rng = new System.Random();
            for (int i = typePool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                int temp = typePool[i];
                typePool[i] = typePool[j];
                typePool[j] = temp;
            }

            int poolIndex = 0;

            // Assign to Full cells
            foreach (var cell in grid.cells)
            {
                if (cell.state == GridCellState.Full && poolIndex < typePool.Count)
                {
                    cell.itemTypeId = typePool[poolIndex++];
                }
            }

            // Assign to Spawner queues
            foreach (var cell in grid.cells)
            {
                if (cell.state == GridCellState.Spawner && cell.spawnerQueue != null)
                {
                    for (int i = 0; i < cell.spawnerQueue.Count && poolIndex < typePool.Count; i++)
                    {
                        cell.spawnerQueue[i] = typePool[poolIndex++];
                    }
                }
            }

            NotifyChanged();
        }

        #endregion

        #region Type Log Panel

        private void DrawTypeLogPanel(MazeGridConfig grid, TypeAllocation[] allocations)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Type Log", EditorStyles.boldLabel);

            if (allocations == null || allocations.Length == 0)
            {
                EditorGUILayout.HelpBox("No type allocations defined.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            foreach (var allocation in allocations)
            {
                if (_library == null || allocation.typeIndex < 0 || allocation.typeIndex >= _library.objectTypes.Length)
                    continue;

                var type = _library.objectTypes[allocation.typeIndex];
                int placedCount = CountPlacedTypes(grid, allocation.typeIndex);
                int expectedCount = allocation.instanceCount;

                EditorGUILayout.BeginHorizontal();

                // Type name with color swatch
                Color typeColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;
                GUI.backgroundColor = typeColor;
                GUILayout.Label("", GUILayout.Width(20), GUILayout.Height(20));
                GUI.backgroundColor = Color.white;

                EditorGUILayout.LabelField(type.typeName, GUILayout.Width(100));
                EditorGUILayout.LabelField($"{expectedCount} instances", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{placedCount} placed", GUILayout.Width(100));

                // Status
                if (placedCount > expectedCount)
                {
                    int exceeds = placedCount - expectedCount;
                    GUI.color = Color.red;
                    EditorGUILayout.LabelField($"{exceeds} Exceeds", GUILayout.Width(100));
                    GUI.color = Color.white;
                }
                else if (placedCount < expectedCount)
                {
                    int needed = expectedCount - placedCount;
                    GUI.color = new Color(1f, 0.6f, 0f);
                    EditorGUILayout.LabelField($"{needed} Needed", GUILayout.Width(100));
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = Color.green;
                    EditorGUILayout.LabelField("\u2713 Complete", GUILayout.Width(100));
                    GUI.color = Color.white;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private int CountPlacedTypes(MazeGridConfig grid, int typeIndex)
        {
            int count = 0;

            foreach (var cell in grid.cells)
            {
                if (cell.state == GridCellState.Full && cell.itemTypeId == typeIndex)
                    count++;
                else if (cell.state == GridCellState.Spawner && cell.spawnerQueue != null)
                {
                    foreach (var queueType in cell.spawnerQueue)
                    {
                        if (queueType == typeIndex)
                            count++;
                    }
                }
            }

            return count;
        }

        #endregion

        #region Grid Log Panel

        /// <summary>
        /// Draws the Grid Log panel with cell count validation.
        /// Compares grid capacity to required cells from allocations.
        /// </summary>
        public void DrawGridLogPanel(MazeGridConfig config, TypeAllocation[] allocations, params GUILayoutOption[] layoutOptions)
        {
            var grid = config ?? _currentConfig;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, layoutOptions);
            EditorGUILayout.LabelField("Grid Log", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Count full cells in grid
            int fullCellsInGrid = 0;
            int spawnerCount = 0;
            int totalSpawnerQueueSlots = 0;

            if (grid != null && grid.cells != null)
            {
                for (int i = 0; i < grid.cells.Length; i++)
                {
                    if (grid.cells[i].state == GridCellState.Full)
                    {
                        fullCellsInGrid++;
                    }
                    else if (grid.cells[i].state == GridCellState.Spawner)
                    {
                        spawnerCount++;
                        if (grid.cells[i].spawnerQueue != null)
                        {
                            totalSpawnerQueueSlots += grid.cells[i].spawnerQueue.Count;
                        }
                    }
                }
            }

            int totalFullCells = fullCellsInGrid + totalSpawnerQueueSlots;

            // Calculate total needed cells from allocations
            int totalNeededCells = 0;
            if (allocations != null)
            {
                foreach (var allocation in allocations)
                {
                    totalNeededCells += allocation.instanceCount;
                }
            }

            int difference = totalFullCells - totalNeededCells;

            // Display statistics
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Full Cells in Grid:", GUILayout.Width(150));
            EditorGUILayout.LabelField(fullCellsInGrid.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Spawner Queue Slots:", GUILayout.Width(150));
            EditorGUILayout.LabelField(totalSpawnerQueueSlots.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Total Full Cells:", GUILayout.Width(150));
            EditorGUILayout.LabelField(totalFullCells.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Total Needed Cells:", GUILayout.Width(150));
            EditorGUILayout.LabelField(totalNeededCells.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // Show difference with color coding
            EditorGUILayout.BeginHorizontal();
            if (difference == 0)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("Status:", GUILayout.Width(150));
                EditorGUILayout.LabelField("\u2713 Valid (Exact Match)", EditorStyles.boldLabel);
            }
            else if (difference > 0)
            {
                GUI.color = new Color(1f, 0.5f, 0f);
                EditorGUILayout.LabelField("Status:", GUILayout.Width(150));
                EditorGUILayout.LabelField($"\u26a0 Exceeding by {difference} cells", EditorStyles.boldLabel);
            }
            else
            {
                GUI.color = Color.red;
                EditorGUILayout.LabelField("Status:", GUILayout.Width(150));
                EditorGUILayout.LabelField($"\u2717 Needed {Mathf.Abs(difference)} more cells", EditorStyles.boldLabel);
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        #endregion
    }
}
