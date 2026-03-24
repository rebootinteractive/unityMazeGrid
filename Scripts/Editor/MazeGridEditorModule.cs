using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ObjectType;

namespace MazeGrid.Editor
{
    /// <summary>
    /// Reusable editor module for editing MazeGridConfig data.
    /// Provides grid building, separator editing, and type painting sub-stages.
    /// Embed this in any game's level editor to get full grid editing capabilities.
    /// </summary>
    public partial class MazeGridEditorModule
    {
        #region Sub-Stage Enum

        public enum SubStage
        {
            GridBuilding = 0,
            Separator = 1,
            TypePainting = 2
        }

        #endregion

        #region State Fields

        // Core references
        private ObjectTypeLibrary _library;
        private Action _onConfigChanged;
        private Action _repaintCallback;

        // Sub-stage navigation
        private SubStage _subStage = SubStage.GridBuilding;

        // Grid building state
        private GridCellState _selectedCellState = GridCellState.Empty;

        // Scroll positions
        private Vector2 _scrollPos;

        // Drag-and-drop state
        private bool _isDragging = false;
        private int _draggedTypeIndex = -1;
        private bool _dragSourceIsTypeSource = false;
        private int _dragSourceCellIndex = -1;
        private int _dragSourceSpawnerCellIndex = -1;
        private int _dragSourceSpawnerQueueIndex = -1;

        // Spawner row collapse state
        private Dictionary<int, bool> _spawnerRowExpanded = new Dictionary<int, bool>();

        // Current config and allocations set at start of DrawGUI for use by all internal methods
        private MazeGridConfig _currentConfig;
        private TypeAllocation[] _currentAllocations;

        /// <summary>
        /// Optional callback invoked after default cell rendering. Game editors can use this
        /// to draw custom overlays (badges, icons, labels) on top of grid cells.
        /// Parameters: (Rect cellRect, int cellIndex, MazeCellData cellData)
        /// </summary>
        public Action<Rect, int, MazeCellData> CustomCellOverlay { get; set; }

        #endregion

        #region Constructor

        public MazeGridEditorModule(ObjectTypeLibrary library, Action onConfigChanged, Action repaintCallback = null)
        {
            _library = library;
            _onConfigChanged = onConfigChanged;
            _repaintCallback = repaintCallback;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets or updates the ObjectTypeLibrary reference.
        /// </summary>
        public void SetObjectTypeLibrary(ObjectTypeLibrary library)
        {
            _library = library;
        }

        /// <summary>
        /// Main entry point. Draws the full grid editor GUI with sub-tabs.
        /// </summary>
        /// <param name="config">The MazeGridConfig to edit</param>
        /// <param name="allocations">Optional type allocations for type painting and grid log</param>
        public void DrawGUI(MazeGridConfig config, TypeAllocation[] allocations = null)
        {
            _currentConfig = config;
            _currentAllocations = allocations;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Maze Grid Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Initialize grid config if needed
            EnsureConfigInitialized(config);

            // Draw sub-stage tabs
            DrawSubStageTabs();

            EditorGUILayout.Space(10);

            // Draw current sub-stage
            switch (_subStage)
            {
                case SubStage.GridBuilding:
                    DrawGridBuildingInternal(config);
                    break;

                case SubStage.Separator:
                    DrawSeparatorsInternal(config);
                    break;

                case SubStage.TypePainting:
                    DrawTypePaintingInternal(config, allocations);
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws only the grid building sub-stage.
        /// </summary>
        public void DrawGridBuilding(MazeGridConfig config)
        {
            _currentConfig = config;
            EnsureConfigInitialized(config);
            DrawGridBuildingInternal(config);
        }

        /// <summary>
        /// Draws only the separator editing sub-stage.
        /// </summary>
        public void DrawSeparators(MazeGridConfig config)
        {
            _currentConfig = config;
            EnsureConfigInitialized(config);
            DrawSeparatorsInternal(config);
        }

        /// <summary>
        /// Draws only the type painting sub-stage.
        /// </summary>
        public void DrawTypePainting(MazeGridConfig config, TypeAllocation[] allocations)
        {
            _currentConfig = config;
            _currentAllocations = allocations;
            EnsureConfigInitialized(config);
            DrawTypePaintingInternal(config, allocations);
        }

        #endregion

        #region Internal Helpers

        private void DrawSubStageTabs()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = _subStage == SubStage.GridBuilding ? Color.green : Color.white;
            if (GUILayout.Button("Grid Building", GUILayout.Height(25)))
            {
                _subStage = SubStage.GridBuilding;
            }

            GUI.backgroundColor = _subStage == SubStage.Separator ? Color.green : Color.white;
            if (GUILayout.Button("Separator Mode", GUILayout.Height(25)))
            {
                _subStage = SubStage.Separator;
            }

            GUI.backgroundColor = _subStage == SubStage.TypePainting ? Color.green : Color.white;
            if (GUILayout.Button("Type Painting", GUILayout.Height(25)))
            {
                _subStage = SubStage.TypePainting;
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void EnsureConfigInitialized(MazeGridConfig config)
        {
            if (config.cells == null || config.cells.Length != config.rows * config.columns)
            {
                if (config.rows <= 0) config.rows = 6;
                if (config.columns <= 0) config.columns = 4;
                config.InitializeCells();
                config.InitializeSeparators();
                NotifyChanged();
            }
        }

        private void NotifyChanged()
        {
            _onConfigChanged?.Invoke();
        }

        #endregion
    }
}
