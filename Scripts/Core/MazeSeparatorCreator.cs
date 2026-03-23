using UnityEngine;

namespace MazeGrid
{
    /// <summary>
    /// Spawns and destroys separator visuals based on MazeGrid separator data.
    /// Handles only visual representation; pathfinding logic is in MazeGrid.
    /// </summary>
    public class MazeSeparatorCreator : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject horizontalSeparatorPrefab;
        [SerializeField] private GameObject verticalSeparatorPrefab;

        [Header("References")]
        [SerializeField] private MazeGrid mazeGrid;

        private Transform separatorContainer;

        private void Awake()
        {
            if (mazeGrid == null)
            {
                mazeGrid = GetComponent<MazeGrid>();
            }

            if (mazeGrid != null)
            {
                mazeGrid.OnInitialized += OnGridInitialized;
            }
        }

        private void OnDestroy()
        {
            if (mazeGrid != null)
            {
                mazeGrid.OnInitialized -= OnGridInitialized;
            }
        }

        private void OnGridInitialized()
        {
            BuildSeparators();
        }

        public void BuildSeparators()
        {
            if (mazeGrid == null)
            {
                Debug.LogError("MazeSeparatorCreator: MazeGrid reference is null");
                return;
            }

            ClearSeparators();

            separatorContainer = new GameObject("Separators").transform;
            separatorContainer.SetParent(transform);
            separatorContainer.localPosition = Vector3.zero;

            BuildHorizontalSeparators();
            BuildVerticalSeparators();
        }

        private void BuildHorizontalSeparators()
        {
            var horizontalData = mazeGrid.HorizontalSeparators;
            if (horizontalData == null || horizontalSeparatorPrefab == null)
                return;

            int configRows = mazeGrid.ConfigRows;
            int configColumns = mazeGrid.ConfigColumns;
            int borderOffset = mazeGrid.BorderOffsetCount;
            float cellSizeX = mazeGrid.CellSizeX;
            float cellSizeZ = mazeGrid.CellSizeZ;
            Vector3 gridOrigin = mazeGrid.GridOrigin;

            for (int row = 0; row < configRows - 1; row++)
            {
                for (int col = 0; col < configColumns; col++)
                {
                    int index = row * configColumns + col;
                    if (index >= horizontalData.Length)
                        continue;

                    if (!horizontalData[index])
                        continue;

                    int runtimeRow = row + 1;
                    int runtimeCol = col + borderOffset;
                    float x = gridOrigin.x + (runtimeCol * cellSizeX);
                    float z = gridOrigin.z - (runtimeRow * cellSizeZ) - (cellSizeZ * 0.5f);
                    Vector3 position = new Vector3(x, gridOrigin.y, z);

                    SpawnSeparator(horizontalSeparatorPrefab, position, $"HorizontalSeparator_{row}_{col}");
                }
            }
        }

        private void BuildVerticalSeparators()
        {
            var verticalData = mazeGrid.VerticalSeparators;
            if (verticalData == null || verticalSeparatorPrefab == null)
                return;

            int configRows = mazeGrid.ConfigRows;
            int configColumns = mazeGrid.ConfigColumns;
            int borderOffset = mazeGrid.BorderOffsetCount;
            float cellSizeX = mazeGrid.CellSizeX;
            float cellSizeZ = mazeGrid.CellSizeZ;
            Vector3 gridOrigin = mazeGrid.GridOrigin;

            for (int row = 0; row < configRows; row++)
            {
                for (int col = 0; col < configColumns - 1; col++)
                {
                    int index = row * (configColumns - 1) + col;
                    if (index >= verticalData.Length)
                        continue;

                    if (!verticalData[index])
                        continue;

                    int runtimeRow = row + 1;
                    int runtimeCol = col + borderOffset;
                    float x = gridOrigin.x + (runtimeCol * cellSizeX) + (cellSizeX * 0.5f);
                    float z = gridOrigin.z - (runtimeRow * cellSizeZ);
                    Vector3 position = new Vector3(x, gridOrigin.y, z);

                    SpawnSeparator(verticalSeparatorPrefab, position, $"VerticalSeparator_{row}_{col}");
                }
            }
        }

        private void SpawnSeparator(GameObject prefab, Vector3 position, string name)
        {
            GameObject separator;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                separator = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, separatorContainer);
                separator.transform.position = position;
                separator.transform.rotation = Quaternion.identity;
            }
            else
#endif
            {
                separator = Object.Instantiate(prefab, position, Quaternion.identity, separatorContainer);
            }
            separator.name = name;
        }

        public void ClearSeparators()
        {
            if (separatorContainer != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(separatorContainer.gameObject);
                else
#endif
                    Destroy(separatorContainer.gameObject);

                separatorContainer = null;
            }

            var existingSeparators = GetComponentsInChildren<Transform>();
            foreach (var t in existingSeparators)
            {
                if (t != transform && t.name.Contains("Separator"))
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(t.gameObject);
                    else
#endif
                        Destroy(t.gameObject);
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Separators")]
        private void EditorRebuildSeparators()
        {
            BuildSeparators();
        }

        private void OnDrawGizmosSelected()
        {
            if (mazeGrid == null)
                return;

            var horizontalData = mazeGrid.HorizontalSeparators;
            var verticalData = mazeGrid.VerticalSeparators;

            if (horizontalData == null && verticalData == null)
                return;

            int configRows = mazeGrid.ConfigRows;
            int configColumns = mazeGrid.ConfigColumns;
            int borderOffset = mazeGrid.BorderOffsetCount;
            float cellSizeX = mazeGrid.CellSizeX;
            float cellSizeZ = mazeGrid.CellSizeZ;
            Vector3 gridOrigin = mazeGrid.GridOrigin;

            Gizmos.color = Color.red;

            if (horizontalData != null)
            {
                for (int row = 0; row < configRows - 1; row++)
                {
                    for (int col = 0; col < configColumns; col++)
                    {
                        int index = row * configColumns + col;
                        if (index >= horizontalData.Length || !horizontalData[index])
                            continue;

                        int runtimeRow = row + 1;
                        int runtimeCol = col + borderOffset;
                        float x = gridOrigin.x + (runtimeCol * cellSizeX);
                        float z = gridOrigin.z - (runtimeRow * cellSizeZ) - (cellSizeZ * 0.5f);
                        Vector3 position = new Vector3(x, gridOrigin.y + 0.1f, z);

                        Vector3 start = position - new Vector3(cellSizeX * 0.4f, 0, 0);
                        Vector3 end = position + new Vector3(cellSizeX * 0.4f, 0, 0);
                        Gizmos.DrawLine(start, end);
                        Gizmos.DrawCube(position, new Vector3(cellSizeX * 0.8f, 0.1f, 0.1f));
                    }
                }
            }

            if (verticalData != null)
            {
                for (int row = 0; row < configRows; row++)
                {
                    for (int col = 0; col < configColumns - 1; col++)
                    {
                        int index = row * (configColumns - 1) + col;
                        if (index >= verticalData.Length || !verticalData[index])
                            continue;

                        int runtimeRow = row + 1;
                        int runtimeCol = col + borderOffset;
                        float x = gridOrigin.x + (runtimeCol * cellSizeX) + (cellSizeX * 0.5f);
                        float z = gridOrigin.z - (runtimeRow * cellSizeZ);
                        Vector3 position = new Vector3(x, gridOrigin.y + 0.1f, z);

                        Vector3 start = position - new Vector3(0, 0, cellSizeZ * 0.4f);
                        Vector3 end = position + new Vector3(0, 0, cellSizeZ * 0.4f);
                        Gizmos.DrawLine(start, end);
                        Gizmos.DrawCube(position, new Vector3(0.1f, 0.1f, cellSizeZ * 0.8f));
                    }
                }
            }
        }
#endif
    }
}
