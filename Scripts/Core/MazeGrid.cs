using System;
using System.Collections.Generic;
using UnityEngine;

namespace MazeGrid
{
    /// <summary>
    /// Generic 2D grid system with A* pathfinding, spawners, separators, and hidden cell reveal.
    /// Game-specific subclasses override factory methods to spawn game-specific prefabs.
    /// </summary>
    public class MazeGrid : MonoBehaviour
    {
        public event Action OnInitialized;
        public event Action<Vector2Int> OnCellCleared;
        public event Action<IMazeItem> OnItemRegistered;
        public event Action<IMazeItem> OnItemRevealed;

        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 4;
        [SerializeField] private int gridHeight = 6;
        [SerializeField] private float cellSizeX = 1f;
        [SerializeField] private float cellSizeZ = 1f;
        [Tooltip("Number of wall-filled border cells to add on each side (left, right) and bottom")]
        [SerializeField] private int borderOffsetCount = 0;
        private Vector3 gridLocalOrigin = Vector3.zero;
        public Vector3 GridOrigin => gridLocalOrigin + transform.position;

        [Header("Exit Configuration")]
        [SerializeField] private int exitRow = 0;

        protected Grid2D<IMazeItem> grid;

        // Separator data
        private bool[] horizontalSeparators;
        private bool[] verticalSeparators;
        private int configRows;

        // Spawner tracking
        private HashSet<Vector2Int> spawnerCells = new HashSet<Vector2Int>();
        private List<MazeSpawner> spawners = new List<MazeSpawner>();

        // Valid cells tracking (cells that are part of the board, may or may not have items)
        private HashSet<Vector2Int> validCells = new HashSet<Vector2Int>();

        // Dummy valid cells (visual containers — behave like valid but skip cell prefab placement)
        private HashSet<Vector2Int> dummyValidCells = new HashSet<Vector2Int>();

        // Hidden item tracking
        private Dictionary<Vector2Int, IMazeItem> hiddenItems = new Dictionary<Vector2Int, IMazeItem>();

        // Public accessors
        public bool[] HorizontalSeparators => horizontalSeparators;
        public bool[] VerticalSeparators => verticalSeparators;
        public int ConfigRows => configRows;
        public int ConfigColumns => gridWidth - (borderOffsetCount * 2);
        public int BorderOffsetCount => borderOffsetCount;
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public float CellSizeX => cellSizeX;
        public float CellSizeZ => cellSizeZ;
        public int ExitRow => exitRow;

        [Tooltip("If true, grid initializes from scene children in Awake. Disable if BuildFromConfig will be called.")]
        [SerializeField] private bool initializeOnAwake = true;

        private void Awake()
        {
            if (initializeOnAwake)
                InitializeGrid();
        }

        private void InitializeGrid()
        {
            gridLocalOrigin.x = -(gridWidth - 1) * cellSizeX / 2f;
            grid = new Grid2D<IMazeItem>(gridWidth, gridHeight, cellSizeX, cellSizeZ, GridOrigin, negativeZDirection: true);

            // Find all existing items and place them in grid
            var existingItems = GetComponentsInChildren<IMazeItem>();
            foreach (var item in existingItems)
            {
                Vector2Int gridPos = grid.WorldToGrid(item.transform.position);
                if (grid.IsValidPosition(gridPos))
                {
                    grid[gridPos] = item;
                    item.transform.position = grid.GridToWorld(gridPos);
                }
            }

            OnCellCleared += OnCellClearedCheckActiveItems;
            OnInitialized?.Invoke();
            CheckAndTriggerActiveItems();
        }

        #region Public API

        public bool CanSendItem(IMazeItem item)
        {
            if (item == null)
                return false;

            Vector2Int itemPos = grid.WorldToGrid(item.transform.position);

            if (!grid.IsValidPosition(itemPos))
                return false;

            if (grid[itemPos] != item)
                return false;

            return GridPathfinder.HasPathToRow(
                itemPos,
                exitRow,
                (pos) => IsWalkableForPathfinding(pos, itemPos),
                CanMoveBetweenCells,
                gridWidth,
                gridHeight
            );
        }

        public bool TrySendItem(IMazeItem item)
        {
            if (!CanSendItem(item))
                return false;

            Vector2Int itemPos = grid.WorldToGrid(item.transform.position);
            grid[itemPos] = null;
            OnCellCleared?.Invoke(itemPos);
            return true;
        }

        public IMazeItem GetItemAt(Vector2Int gridPos)
        {
            if (grid == null || !grid.IsValidPosition(gridPos))
                return null;
            return grid[gridPos];
        }

        public IMazeItem GetItemAtWorldPosition(Vector3 worldPos)
        {
            if (grid == null) return null;
            return grid.GetCellAtWorldPosition(worldPos);
        }

        public Vector3 GetCellWorldPosition(Vector2Int gridPos)
        {
            return grid.GridToWorld(gridPos);
        }

        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return grid.WorldToGrid(worldPos);
        }

        /// <summary>
        /// Returns true if the cell is a valid playable cell with no item currently in it.
        /// Invalid cells, spawner cells, and cells with items return false.
        /// </summary>
        public bool IsCellEmpty(Vector2Int gridPos)
        {
            if (grid == null || !grid.IsValidPosition(gridPos))
                return false;
            if (!validCells.Contains(gridPos))
                return false;
            if (spawnerCells.Contains(gridPos))
                return false;
            return grid[gridPos] == null;
        }

        /// <summary>
        /// Returns true if the cell is a spawner cell.
        /// </summary>
        public bool IsSpawnerCell(Vector2Int gridPos)
        {
            return spawnerCells.Contains(gridPos);
        }

        /// <summary>
        /// Returns true if the cell is a DummyValid cell (visual container, no cell prefab).
        /// </summary>
        public bool IsDummyValidCell(Vector2Int gridPos)
        {
            return dummyValidCells.Contains(gridPos);
        }

        /// <summary>
        /// Returns true if the cell is part of the board (Valid or Spawner).
        /// Invalid cells and out-of-bounds return false.
        /// Used by MazeBorderCreator to determine the grid shape for border generation.
        /// </summary>
        public bool IsSolidCell(Vector2Int gridPos)
        {
            if (grid == null || !grid.IsValidPosition(gridPos))
                return false;
            return validCells.Contains(gridPos) || spawnerCells.Contains(gridPos);
        }

        public void RegisterItem(IMazeItem item, Vector2Int gridPos)
        {
            if (grid == null || !grid.IsValidPosition(gridPos))
            {
                Debug.LogWarning($"MazeGrid.RegisterItem: Invalid position {gridPos}");
                return;
            }

            if (grid[gridPos] != null)
            {
                Debug.LogWarning($"MazeGrid.RegisterItem: Cell {gridPos} is already occupied");
                return;
            }

            grid[gridPos] = item;
            OnItemRegistered?.Invoke(item);
        }

        public List<Vector2Int> FindExitPath(IMazeItem item)
        {
            if (item == null || grid == null)
                return new List<Vector2Int>();

            Vector2Int itemPos = grid.WorldToGrid(item.transform.position);

            if (!grid.IsValidPosition(itemPos))
                return new List<Vector2Int>();

            return GridPathfinder.FindPathToRow(
                itemPos,
                exitRow,
                (pos) => IsWalkableForPathfinding(pos, itemPos),
                CanMoveBetweenCells,
                gridWidth,
                gridHeight
            );
        }

        #endregion

        #region Build From Config

        public void BuildFromConfig(MazeGridConfig config)
        {
            if (config == null)
            {
                Debug.LogError("MazeGrid.BuildFromConfig: Config is null");
                return;
            }

            ClearGrid();

            gridWidth = config.columns + (borderOffsetCount * 2);
            gridHeight = config.rows + 1 + borderOffsetCount;
            exitRow = config.exitRow;
            configRows = config.rows;

            horizontalSeparators = config.horizontalSeparators;
            verticalSeparators = config.verticalSeparators;

            spawnerCells.Clear();
            spawners.Clear();
            validCells.Clear();

            gridLocalOrigin.x = -(gridWidth - 1) * cellSizeX / 2f;
            grid = new Grid2D<IMazeItem>(gridWidth, gridHeight, cellSizeX, cellSizeZ, GridOrigin, negativeZDirection: true);

            SpawnBorderInvalidCells(config.columns, config.rows);

            if (config.cells != null)
            {
                for (int i = 0; i < config.cells.Length; i++)
                {
                    var cellData = config.cells[i];
                    if (cellData == null)
                        continue;

                    int configRow = i / config.columns;
                    int configCol = i % config.columns;
                    int gridCol = configCol + borderOffsetCount;
                    int gridRow = configRow + 1;
                    Vector2Int gridPos = new Vector2Int(gridCol, gridRow);
                    Vector3 worldPos = grid.GridToWorld(gridPos);

#pragma warning disable CS0618 // GridWall is obsolete
                    // Invalid and GridWall (obsolete) are not part of the board — skip
                    if (cellData.state == GridCellState.Invalid || cellData.state == GridCellState.GridWall)
                        continue;
#pragma warning restore CS0618

                    if (cellData.state == GridCellState.Valid || cellData.state == GridCellState.DummyValid)
                    {
                        // Register as a valid cell (part of the board)
                        validCells.Add(gridPos);

                        if (cellData.state == GridCellState.DummyValid)
                            dummyValidCells.Add(gridPos);

                        // Only spawn an item if a type is assigned
                        if (cellData.itemTypeId >= 0)
                        {
                            var item = CreateItem(cellData, gridPos, worldPos);
                            if (item != null)
                            {
                                grid[gridPos] = item;
                                if (cellData.isHidden)
                                {
                                    item.IsHidden = true;
                                    hiddenItems[gridPos] = item;
                                }
                            }
                        }
                    }
                    else if (cellData.state == GridCellState.Spawner)
                    {
                        SpawnSpawnerInternal(gridPos, worldPos, cellData);
                    }
                }
            }

            OnCellCleared += CheckAndRevealHiddenItems;
            OnCellCleared += OnCellClearedCheckActiveItems;

            CheckAllHiddenItemsForReveal();
            OnInitialized?.Invoke();
            CheckAndTriggerActiveItems();

            foreach (var spawner in spawners)
            {
                spawner.TrySpawn();
            }
        }

        #endregion

        #region Virtual Factory Methods

        /// <summary>
        /// Creates an item for a Full cell during BuildFromConfig.
        /// Override in game subclass to spawn game-specific prefabs.
        /// </summary>
        protected virtual IMazeItem CreateItem(MazeCellData cellData, Vector2Int gridPos, Vector3 worldPos)
        {
            return null;
        }

        /// <summary>
        /// Creates a spawner for a Spawner cell during BuildFromConfig.
        /// Override in game subclass to spawn game-specific spawner prefabs.
        /// </summary>
        protected virtual MazeSpawner CreateSpawner(MazeCellData cellData, Vector2Int gridPos, Vector3 worldPos)
        {
            return null;
        }

        /// <summary>
        /// Creates an item for a spawner to push into the grid.
        /// Override in game subclass to spawn game-specific prefabs.
        /// </summary>
        public virtual IMazeItem CreateItemForSpawner(MazeCellData cellData, Vector2Int gridPos, Vector3 worldPos)
        {
            return null;
        }

        #endregion

        #region Pathfinding

        private bool IsWalkableForPathfinding(Vector2Int gridPos, Vector2Int startPos)
        {
            if (!grid.IsValidPosition(gridPos))
                return false;
            if (gridPos == startPos)
                return true;
            // Exit row is always walkable
            if (gridPos.y == exitRow)
                return true;
            // Invalid cells (not in validCells) are not walkable
            if (!validCells.Contains(gridPos))
                return false;
            if (spawnerCells.Contains(gridPos))
                return false;
            // Walkable if no item occupying the cell
            return grid[gridPos] == null;
        }

        private bool CanMoveBetweenCells(Vector2Int from, Vector2Int to)
        {
            if (horizontalSeparators == null && verticalSeparators == null)
                return true;

            if (from.y == exitRow || to.y == exitRow)
                return true;

            int fromConfigRow = from.y - 1;
            int toConfigRow = to.y - 1;
            int fromConfigCol = from.x - borderOffsetCount;
            int toConfigCol = to.x - borderOffsetCount;

            int configColumns = gridWidth - (borderOffsetCount * 2);

            int dRow = toConfigRow - fromConfigRow;
            int dCol = toConfigCol - fromConfigCol;

            if (dRow != 0 && dCol == 0)
            {
                if (horizontalSeparators == null)
                    return true;

                int separatorRow = dRow > 0 ? fromConfigRow : toConfigRow;
                int col = fromConfigCol;

                if (separatorRow < 0 || separatorRow >= configRows - 1 || col < 0 || col >= configColumns)
                    return true;

                int separatorIndex = separatorRow * configColumns + col;
                if (separatorIndex >= 0 && separatorIndex < horizontalSeparators.Length)
                {
                    return !horizontalSeparators[separatorIndex];
                }
            }
            else if (dCol != 0 && dRow == 0)
            {
                if (verticalSeparators == null)
                    return true;

                int separatorCol = dCol > 0 ? fromConfigCol : toConfigCol;
                int row = fromConfigRow;

                if (row < 0 || row >= configRows || separatorCol < 0 || separatorCol >= configColumns - 1)
                    return true;

                int separatorIndex = row * (configColumns - 1) + separatorCol;
                if (separatorIndex >= 0 && separatorIndex < verticalSeparators.Length)
                {
                    return !verticalSeparators[separatorIndex];
                }
            }

            return true;
        }

        #endregion

        #region Hidden Item Reveal

        private void CheckAndRevealHiddenItems(Vector2Int clearedPos)
        {
            if (hiddenItems.Count == 0)
                return;

            if (!IsCellEmpty(clearedPos))
                return;

            if (IsSpawnerTargetWithItemsRemaining(clearedPos))
                return;

            Vector2Int[] neighbors = new Vector2Int[]
            {
                clearedPos + Vector2Int.up,
                clearedPos + Vector2Int.down,
                clearedPos + Vector2Int.left,
                clearedPos + Vector2Int.right
            };

            foreach (var neighborPos in neighbors)
            {
                if (CanMoveBetweenCells(neighborPos, clearedPos))
                {
                    TryRevealItemAt(neighborPos);
                }
            }
        }

        private bool IsSpawnerTargetWithItemsRemaining(Vector2Int pos)
        {
            foreach (var spawner in spawners)
            {
                if (spawner != null &&
                    spawner.TargetCellPosition == pos &&
                    spawner.HasItemsToSpawn)
                {
                    return true;
                }
            }
            return false;
        }

        private void CheckAllHiddenItemsForReveal()
        {
            if (hiddenItems.Count == 0)
                return;

            var positionsToCheck = new List<Vector2Int>(hiddenItems.Keys);

            foreach (var pos in positionsToCheck)
            {
                if (HasEmptyAdjacentCell(pos))
                {
                    TryRevealItemAt(pos);
                }
            }
        }

        private bool HasEmptyAdjacentCell(Vector2Int pos)
        {
            Vector2Int[] neighbors = new Vector2Int[]
            {
                pos + Vector2Int.up,
                pos + Vector2Int.down,
                pos + Vector2Int.left,
                pos + Vector2Int.right
            };

            foreach (var neighborPos in neighbors)
            {
                if (IsAdjacentCellAccessibleAndEmpty(pos, neighborPos))
                    return true;
            }

            return false;
        }

        private bool IsAdjacentCellAccessibleAndEmpty(Vector2Int fromPos, Vector2Int toPos)
        {
            if (!CanMoveBetweenCells(fromPos, toPos))
                return false;
            if (!IsCellEmpty(toPos))
                return false;
            if (IsSpawnerTargetWithItemsRemaining(toPos))
                return false;
            return true;
        }

        private void TryRevealItemAt(Vector2Int pos)
        {
            if (!hiddenItems.TryGetValue(pos, out IMazeItem item))
                return;

            if (item == null || item.gameObject == null)
            {
                hiddenItems.Remove(pos);
                return;
            }

            item.IsHidden = false;
            item.OnRevealed();
            hiddenItems.Remove(pos);
            OnItemRevealed?.Invoke(item);
        }

        #endregion

        #region Active Item Detection

        private void OnCellClearedCheckActiveItems(Vector2Int clearedPos)
        {
            CheckAndTriggerActiveItems();
        }

        private void CheckAndTriggerActiveItems()
        {
            if (grid == null)
                return;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    var item = grid[x, y];
                    if (item != null && CanSendItem(item))
                    {
                        item.OnBecameActive();
                    }
                }
            }
        }

        #endregion

        #region Internal Spawning

        private void SpawnSpawnerInternal(Vector2Int gridPos, Vector3 worldPos, MazeCellData cellData)
        {
            spawnerCells.Add(gridPos);

            var spawner = CreateSpawner(cellData, gridPos, worldPos);
            if (spawner == null)
                return;

            spawner.Initialize(this, gridPos, cellData.direction, cellData.spawnerQueue);
            spawners.Add(spawner);
        }

        /// <summary>
        /// Marks border offset cells as invalid (not part of the board).
        /// These cells remain unregistered in validCells, making them impassable.
        /// No prefabs are spawned — the border builder handles visuals.
        /// </summary>
        private void SpawnBorderInvalidCells(int configColumns, int configRowCount)
        {
            // Border offset cells are simply not added to validCells,
            // so they're automatically treated as Invalid (not walkable, not solid).
            // Nothing to spawn — just ensure they stay out of validCells.
            // The exit row (row 0) is also not in validCells but is handled
            // specially by pathfinding (always walkable).
            if (borderOffsetCount <= 0)
                return;

            // No action needed — cells default to Invalid by not being in validCells.
            // This method exists for clarity and as a hook for future border logic.
        }

        #endregion

        #region Cleanup

        public virtual void ClearGrid()
        {
            // Destroy existing items
            var existingItems = GetComponentsInChildren<IMazeItem>();
            foreach (var item in existingItems)
            {
                SafeDestroy(item.gameObject);
            }

            // Destroy spawners
            var existingSpawners = GetComponentsInChildren<MazeSpawner>();
            foreach (var spawner in existingSpawners)
            {
                SafeDestroy(spawner.gameObject);
            }

            spawnerCells.Clear();
            spawners.Clear();
            validCells.Clear();
            dummyValidCells.Clear();
            hiddenItems.Clear();

            OnCellCleared -= CheckAndRevealHiddenItems;
            OnCellCleared -= OnCellClearedCheckActiveItems;

            grid = null;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Safely destroys a GameObject in both editor and runtime contexts.
        /// </summary>
        protected static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(go);
                return;
            }
#endif
            Destroy(go);
        }

        /// <summary>
        /// Instantiates a prefab, handling both editor and runtime contexts.
        /// </summary>
        protected static T SpawnPrefab<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent) where T : Component
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var instance = (T)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                return instance;
            }
#endif
            return Instantiate(prefab, position, rotation, parent);
        }

        /// <summary>
        /// Instantiates a GameObject prefab, handling both editor and runtime contexts.
        /// </summary>
        protected static GameObject SpawnGameObject(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                return instance;
            }
#endif
            return Instantiate(prefab, position, rotation, parent);
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (grid == null && Application.isPlaying)
                return;

            var tempGrid = grid ?? new Grid2D<IMazeItem>(gridWidth, gridHeight, cellSizeX, cellSizeZ, GridOrigin, negativeZDirection: true);

            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    Vector3 cellCenter = tempGrid.GridToWorld(x, z);
                    Vector2Int cellPos = new Vector2Int(x, z);

                    if (z == exitRow)
                    {
                        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                    }
                    else if (spawnerCells.Contains(cellPos))
                    {
                        Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
                    }
                    else if (validCells.Contains(cellPos))
                    {
                        if (grid != null && grid[x, z] != null)
                            Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Valid with item
                        else
                            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.3f); // Valid empty
                    }
                    else
                    {
                        Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.1f); // Invalid
                    }

                    Gizmos.DrawCube(cellCenter, new Vector3(cellSizeX * 0.9f, 0.1f, cellSizeZ * 0.9f));

                    Gizmos.color = Color.gray;
                    Gizmos.DrawWireCube(cellCenter, new Vector3(cellSizeX, 0.1f, cellSizeZ));
                }
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(GridOrigin, 0.2f);
        }
#endif
    }
}
