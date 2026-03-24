using System;
using UnityEditor;
using UnityEngine;
using ObjectType;

namespace MazeGrid.Editor
{
    /// <summary>
    /// Drag-and-drop state machine and shared helpers for type assignment.
    /// </summary>
    public partial class MazeGridEditorModule
    {
        #region Drag State Machine

        private void StartDragFromTypeSource(int typeIndex)
        {
            _isDragging = true;
            _draggedTypeIndex = typeIndex;
            _dragSourceIsTypeSource = true;
            _dragSourceCellIndex = -1;
            _dragSourceSpawnerCellIndex = -1;
            _dragSourceSpawnerQueueIndex = -1;
        }

        private void StartDragFromCell(int cellIndex, MazeCellData cell)
        {
            _isDragging = true;
            _draggedTypeIndex = cell.itemTypeId;
            _dragSourceIsTypeSource = false;
            _dragSourceCellIndex = cellIndex;
            _dragSourceSpawnerCellIndex = -1;
            _dragSourceSpawnerQueueIndex = -1;
        }

        private void StartDragFromSpawnerQueueCell(int spawnerIndex, int queueIndex, MazeCellData spawnerCell)
        {
            _isDragging = true;
            var queueItem = spawnerCell.spawnerQueue[queueIndex];
            _draggedTypeIndex = queueItem != null ? queueItem.itemTypeId : -1;
            _dragSourceIsTypeSource = false;
            _dragSourceCellIndex = -1;
            _dragSourceSpawnerCellIndex = spawnerIndex;
            _dragSourceSpawnerQueueIndex = queueIndex;
        }

        private void EndDrag()
        {
            _isDragging = false;
            _draggedTypeIndex = -1;
            _dragSourceIsTypeSource = false;
            _dragSourceCellIndex = -1;
            _dragSourceSpawnerCellIndex = -1;
            _dragSourceSpawnerQueueIndex = -1;
        }

        /// <summary>
        /// Removes the type from the drag source (cell or spawner queue slot).
        /// Does nothing if drag source is a type source button.
        /// </summary>
        private void RemoveTypeFromDragSource()
        {
            var grid = _currentConfig;
            if (grid == null) return;

            // If drag source is a type source button, do nothing
            if (_dragSourceIsTypeSource)
            {
                return;
            }

            // If drag source is a grid cell, remove type from that cell
            if (_dragSourceCellIndex >= 0)
            {
                var sourceCell = grid.cells[_dragSourceCellIndex];
                sourceCell.itemTypeId = -1;
                NotifyChanged();
                return;
            }

            // If drag source is a spawner queue slot, remove type from that slot
            if (_dragSourceSpawnerCellIndex >= 0 && _dragSourceSpawnerQueueIndex >= 0)
            {
                var spawnerCell = grid.cells[_dragSourceSpawnerCellIndex];
                if (spawnerCell.spawnerQueue != null && _dragSourceSpawnerQueueIndex < spawnerCell.spawnerQueue.Count)
                {
                    if (spawnerCell.spawnerQueue[_dragSourceSpawnerQueueIndex] != null)
                        spawnerCell.spawnerQueue[_dragSourceSpawnerQueueIndex].itemTypeId = -1;
                    NotifyChanged();
                }
            }
        }

        #endregion

        #region Ghost Rendering

        /// <summary>
        /// Renders the ghost circle during drag operations using the module's library.
        /// </summary>
        private void RenderDragGhost(int typeIndex)
        {
            if (typeIndex < 0) return;
            if (_library == null || typeIndex >= _library.objectTypes.Length) return;

            var type = _library.objectTypes[typeIndex];
            Color typeColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;

            // Semi-transparent ghost
            typeColor.a = 0.6f;

            Event evt = Event.current;
            Vector2 mousePos = evt.mousePosition;

            Rect ghostRect = new Rect(mousePos.x - 15, mousePos.y - 15, 30, 30);

            Handles.BeginGUI();
            Handles.color = typeColor;
            Handles.DrawSolidDisc(ghostRect.center, Vector3.forward, 15f);
            Handles.EndGUI();
        }

        /// <summary>
        /// Public static version for external callers (e.g., game-specific seat editors).
        /// </summary>
        public static void RenderDragGhostStatic(int typeIndex, ObjectTypeLibrary library)
        {
            if (typeIndex < 0) return;
            if (library == null || typeIndex >= library.objectTypes.Length) return;

            var type = library.objectTypes[typeIndex];
            Color typeColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;
            typeColor.a = 0.6f;

            Event evt = Event.current;
            Vector2 mousePos = evt.mousePosition;

            Rect ghostRect = new Rect(mousePos.x - 15, mousePos.y - 15, 30, 30);

            Handles.BeginGUI();
            Handles.color = typeColor;
            Handles.DrawSolidDisc(ghostRect.center, Vector3.forward, 15f);
            Handles.EndGUI();
        }

        /// <summary>
        /// Handles drag rendering (ghost circle) and drag cancellation.
        /// Call at the end of type painting draw.
        /// </summary>
        private void HandleDragRendering()
        {
            if (!_isDragging)
                return;

            Event evt = Event.current;

            if (evt.type == EventType.Repaint)
            {
                RenderDragGhost(_draggedTypeIndex);
            }

            // Cancel drag on escape or right click
            if ((evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape) ||
                (evt.type == EventType.MouseDown && evt.button == 1))
            {
                EndDrag();
                evt.Use();
            }

            // Repaint to show ghost
            _repaintCallback?.Invoke();
        }

        #endregion

        #region Trash Area

        /// <summary>
        /// Draws a trash area that accepts drag-and-drop to remove types. Public static for reuse.
        /// </summary>
        /// <param name="isDragging">Whether drag is active</param>
        /// <param name="canAcceptDrop">Whether trash can accept current drag</param>
        /// <param name="onDropInTrash">Callback when item is dropped in trash</param>
        public static void DrawTrashArea(bool isDragging, bool canAcceptDrop, Action onDropInTrash)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("Trash", EditorStyles.boldLabel);

            Rect trashRect = GUILayoutUtility.GetRect(100, 100);

            Color trashColor;
            bool isHovering = false;

            if (isDragging && canAcceptDrop)
            {
                Event evt = Event.current;
                isHovering = trashRect.Contains(evt.mousePosition);

                trashColor = isHovering ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 0.3f, 0.3f);
            }
            else
            {
                trashColor = new Color(0.5f, 0.5f, 0.5f);
            }

            EditorGUI.DrawRect(trashRect, trashColor);

            GUIStyle trashStyle = new GUIStyle(EditorStyles.boldLabel);
            trashStyle.alignment = TextAnchor.MiddleCenter;
            trashStyle.fontSize = 48;
            trashStyle.normal.textColor = Color.white;
            GUI.Label(trashRect, "\ud83d\uddd1", trashStyle);

            Handles.BeginGUI();
            Handles.color = Color.black;
            Handles.DrawPolyLine(
                new Vector3(trashRect.xMin, trashRect.yMin),
                new Vector3(trashRect.xMax, trashRect.yMin),
                new Vector3(trashRect.xMax, trashRect.yMax),
                new Vector3(trashRect.xMin, trashRect.yMax),
                new Vector3(trashRect.xMin, trashRect.yMin)
            );
            Handles.EndGUI();

            if (isDragging && canAcceptDrop)
            {
                Event evt = Event.current;
                if (evt.type == EventType.MouseDown && evt.button == 0 && trashRect.Contains(evt.mousePosition))
                {
                    onDropInTrash?.Invoke();
                    evt.Use();
                }
            }

            EditorGUILayout.Space(5);
            GUIStyle helpStyle = new GUIStyle(EditorStyles.miniLabel);
            helpStyle.alignment = TextAnchor.UpperCenter;
            helpStyle.wordWrap = true;
            EditorGUILayout.LabelField("Drag types here to remove them", helpStyle, GUILayout.Width(100));

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Type Source Buttons

        /// <summary>
        /// Draws type source buttons from TypeAllocation array. Public static for reuse.
        /// </summary>
        public static void DrawTypeSourceButtonsFromAllocations(TypeAllocation[] allocations, ObjectTypeLibrary library, Action<int> onTypeSourceClicked, bool showInfinite = false)
        {
            if (allocations == null || allocations.Length == 0 || library == null || library.objectTypes == null)
            {
                EditorGUILayout.HelpBox("No type allocations defined.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < allocations.Length; i++)
            {
                var allocation = allocations[i];
                int typeIndex = allocation.typeIndex;

                if (typeIndex < 0 || typeIndex >= library.objectTypes.Length) continue;

                var type = library.objectTypes[typeIndex];
                Color typeColor = type.colors != null && type.colors.Length > 0 ? type.colors[0] : Color.white;

                GUI.backgroundColor = typeColor;

                string buttonLabel = showInfinite
                    ? $"{type.typeName}"
                    : $"{type.typeName} x{allocation.instanceCount}";

                if (GUILayout.Button(buttonLabel, GUILayout.Height(30), GUILayout.MinWidth(80)))
                {
                    onTypeSourceClicked?.Invoke(typeIndex);
                }

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Instance method that draws a type circle using the module's library.
        /// </summary>
        private void DrawTypeCircle(Rect cellRect, int typeIndex)
        {
            DrawTypeCircle(cellRect, typeIndex, _library);
        }

        #endregion
    }
}
