using System.Collections.Generic;
using UnityEngine;

namespace MazeGrid
{
    /// <summary>
    /// Spawns border visuals around the MazeGrid playable area using marching squares.
    /// Uses GridBorderBuilder (generic algorithm) to determine piece placement,
    /// then instantiates prefabs at the correct positions and rotations.
    ///
    /// Attach to the same GameObject as MazeGrid. Subscribes to OnInitialized
    /// and rebuilds the border whenever the grid is built.
    ///
    /// Requires 4 prefabs from the artist:
    /// - Center: full ground tile, no border edge (half-cell sized)
    /// - Edge: ground on one side, border on the other (half-cell sized)
    /// - Outer corner: convex border curve (half-cell sized)
    /// - Inner corner: concave border curve (half-cell sized)
    ///
    /// All prefabs should have their pivot at the top-left corner,
    /// extend in +X and -Z, with the border edge facing -Z at 0° rotation.
    /// Base size: 0.5 × 0.5 units (system scales by cellSize).
    /// </summary>
    public class MazeBorderCreator : MonoBehaviour
    {
        [Header("Prefabs (pivot: top-left, size: 0.5×0.5 units)")]
        [Tooltip("Full ground tile, no border edges")]
        [SerializeField] private GameObject centerPrefab;
        [Tooltip("Ground on one side, raised border on the other")]
        [SerializeField] private GameObject edgePrefab;
        [Tooltip("Convex border curve (1 solid neighbor)")]
        [SerializeField] private GameObject outerCornerPrefab;
        [Tooltip("Concave border curve (3 solid neighbors)")]
        [SerializeField] private GameObject innerCornerPrefab;

        [Header("References")]
        [SerializeField] private MazeGrid mazeGrid;

        private Transform borderContainer;

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
            BuildBorder();
        }

        /// <summary>
        /// Builds the border by querying GridBorderBuilder and spawning prefabs.
        /// Can be called manually to rebuild.
        /// </summary>
        public void BuildBorder()
        {
            if (mazeGrid == null)
            {
                Debug.LogError("MazeBorderCreator: MazeGrid reference is null");
                return;
            }

            ClearBorder();

            borderContainer = new GameObject("Border").transform;
            borderContainer.SetParent(transform);
            borderContainer.localPosition = Vector3.zero;

            int gridWidth = mazeGrid.GridWidth;
            int gridHeight = mazeGrid.GridHeight;

            // Generate border pieces using the generic algorithm
            var pieces = GridBorderBuilder.Generate(gridWidth, gridHeight, (col, row) =>
            {
                // Exit row cells are not solid (passthrough for gameplay)
                if (row == mazeGrid.ExitRow) return false;
                return mazeGrid.IsSolidCell(new Vector2Int(col, row));
            });

            // Spawn prefabs for each piece
            float cellSizeX = mazeGrid.CellSizeX;
            float cellSizeZ = mazeGrid.CellSizeZ;
            Vector3 gridOrigin = mazeGrid.GridOrigin;

            foreach (var piece in pieces)
            {
                GameObject prefab = GetPrefabForType(piece.type);
                if (prefab == null) continue;

                // Vertex world position: vertex (vx, vy) is at the top-left corner of cell (vx, vy)
                // Offset by -0.5 cell in each axis to sit at the cell corner intersection
                float worldX = gridOrigin.x + (piece.vertexX - 0.5f) * cellSizeX;
                float worldZ = gridOrigin.z - (piece.vertexY - 0.5f) * cellSizeZ;
                Vector3 worldPos = new Vector3(worldX, gridOrigin.y, worldZ);

                Quaternion rotation = Quaternion.Euler(0f, piece.rotationY, 0f);

                // Scale prefab from 0.5-unit base to actual half-cell size
                Vector3 scale = new Vector3(cellSizeX, 1f, cellSizeZ);

                SpawnBorderPiece(prefab, worldPos, rotation, scale, piece);
            }
        }

        /// <summary>
        /// Clears all spawned border visuals.
        /// </summary>
        public void ClearBorder()
        {
            if (borderContainer != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(borderContainer.gameObject);
                else
#endif
                    Destroy(borderContainer.gameObject);

                borderContainer = null;
            }
        }

        private GameObject GetPrefabForType(BorderPieceType type)
        {
            return type switch
            {
                BorderPieceType.Center => centerPrefab,
                BorderPieceType.Edge => edgePrefab,
                BorderPieceType.OuterCorner => outerCornerPrefab,
                BorderPieceType.InnerCorner => innerCornerPrefab,
                _ => null
            };
        }

        private void SpawnBorderPiece(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, BorderPiece piece)
        {
            GameObject instance;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, borderContainer);
                instance.transform.position = position;
                instance.transform.rotation = rotation;
            }
            else
#endif
            {
                instance = Instantiate(prefab, position, rotation, borderContainer);
            }

            instance.transform.localScale = scale;
            instance.name = $"{piece.type}_{piece.vertexX}_{piece.vertexY}";
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Border")]
        private void EditorRebuildBorder()
        {
            BuildBorder();
        }

        private void OnDrawGizmosSelected()
        {
            if (mazeGrid == null) return;

            int gridWidth = mazeGrid.GridWidth;
            int gridHeight = mazeGrid.GridHeight;
            float cellSizeX = mazeGrid.CellSizeX;
            float cellSizeZ = mazeGrid.CellSizeZ;
            Vector3 gridOrigin = mazeGrid.GridOrigin;

            var pieces = GridBorderBuilder.Generate(gridWidth, gridHeight, (col, row) =>
            {
                if (row == mazeGrid.ExitRow) return false;
                return mazeGrid.IsSolidCell(new Vector2Int(col, row));
            });

            foreach (var piece in pieces)
            {
                float worldX = gridOrigin.x + (piece.vertexX - 0.5f) * cellSizeX;
                float worldZ = gridOrigin.z - (piece.vertexY - 0.5f) * cellSizeZ;
                Vector3 worldPos = new Vector3(worldX, gridOrigin.y + 0.1f, worldZ);

                switch (piece.type)
                {
                    case BorderPieceType.Center:
                        Gizmos.color = new Color(0.3f, 0.3f, 0.8f, 0.3f);
                        break;
                    case BorderPieceType.Edge:
                        Gizmos.color = new Color(0.8f, 0.8f, 0.2f, 0.5f);
                        break;
                    case BorderPieceType.OuterCorner:
                        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
                        break;
                    case BorderPieceType.InnerCorner:
                        Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.5f);
                        break;
                }

                float halfX = cellSizeX * 0.25f;
                float halfZ = cellSizeZ * 0.25f;
                Gizmos.DrawCube(worldPos, new Vector3(halfX, 0.05f, halfZ));
            }
        }
#endif
    }
}
