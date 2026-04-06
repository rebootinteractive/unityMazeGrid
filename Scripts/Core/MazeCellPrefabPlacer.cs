using UnityEngine;

namespace MazeGrid
{
    /// <summary>
    /// Spawns a prefab on each cell based on its state (Valid or Invalid).
    /// Attach to the same GameObject as MazeGrid. Rebuilds on OnInitialized.
    /// </summary>
    public class MazeCellPrefabPlacer : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Spawned at the center of each Valid cell. Optional.")]
        [SerializeField] private GameObject validCellPrefab;
        [Tooltip("Spawned at the center of each Invalid cell. Optional.")]
        [SerializeField] private GameObject invalidCellPrefab;
        [Tooltip("Spawned at the center of each Spawner cell. Falls back to validCellPrefab if not set. Optional.")]
        [SerializeField] private GameObject spawnerCellPrefab;

        [Header("Settings")]
        [Tooltip("Y offset applied to all spawned prefabs")]
        [SerializeField] private float yOffset = 0f;

        [Header("References")]
        [SerializeField] private MazeGrid mazeGrid;

        private Transform container;

        private void Awake()
        {
            if (mazeGrid == null)
                mazeGrid = GetComponent<MazeGrid>();

            if (mazeGrid != null)
                mazeGrid.OnInitialized += OnGridInitialized;
        }

        private void OnDestroy()
        {
            if (mazeGrid != null)
                mazeGrid.OnInitialized -= OnGridInitialized;
        }

        private void OnGridInitialized()
        {
            Build();
        }

        public void Build()
        {
            if (mazeGrid == null)
            {
                Debug.LogError("MazeCellPrefabPlacer: MazeGrid reference is null");
                return;
            }

            Clear();

            container = new GameObject("CellPrefabs").transform;
            container.SetParent(transform);
            container.localPosition = Vector3.zero;

            int gridWidth = mazeGrid.GridWidth;
            int gridHeight = mazeGrid.GridHeight;

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    var gridPos = new Vector2Int(x, y);

                    // Skip exit row
                    if (y == mazeGrid.ExitRow)
                        continue;

                    GameObject prefab;
                    string label;

                    if (mazeGrid.IsSpawnerCell(gridPos))
                    {
                        prefab = spawnerCellPrefab != null ? spawnerCellPrefab : validCellPrefab;
                        label = "Spawner";
                    }
                    else if (mazeGrid.IsSolidCell(gridPos))
                    {
                        prefab = validCellPrefab;
                        label = "Valid";
                    }
                    else
                    {
                        prefab = invalidCellPrefab;
                        label = "Invalid";
                    }

                    if (prefab == null)
                        continue;

                    Vector3 worldPos = mazeGrid.GetCellWorldPosition(gridPos);
                    worldPos.y += yOffset;

                    SpawnPrefab(prefab, worldPos, $"{label}_{x}_{y}");
                }
            }
        }

        public void Clear()
        {
            if (container != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(container.gameObject);
                else
#endif
                    Destroy(container.gameObject);

                container = null;
            }
        }

        private void SpawnPrefab(GameObject prefab, Vector3 position, string name)
        {
            GameObject instance;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, container);
                instance.transform.position = position;
                instance.transform.rotation = Quaternion.identity;
            }
            else
#endif
            {
                instance = Instantiate(prefab, position, Quaternion.identity, container);
            }
            instance.name = name;
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Cell Prefabs")]
        private void EditorRebuild()
        {
            Build();
        }
#endif
    }
}
