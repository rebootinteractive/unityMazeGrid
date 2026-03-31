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
        [Header("Prefabs")]
        [Tooltip("Background tile for empty areas (1.0×1.0, pivot at center). Optional.")]
        [SerializeField] private GameObject emptyPrefab;
        [Tooltip("Full ground tile, no border edges (1.0×1.0, pivot at center)")]
        [SerializeField] private GameObject centerPrefab;
        [Tooltip("Ground on one side, raised border on the other (1.0×0.5, pivot at center of border edge)")]
        [SerializeField] private GameObject edgePrefab;
        [Tooltip("Convex border curve (0.5×0.5, pivot at corner)")]
        [SerializeField] private GameObject outerCornerPrefab;
        [Tooltip("Concave border curve (0.5×0.5, pivot at corner)")]
        [SerializeField] private GameObject innerCornerPrefab;

        [Header("Half Prefabs (for open edges)")]
        [Tooltip("Half ground tile for open edge cells (1.0×0.5, pivot at center). Optional.")]
        [SerializeField] private GameObject halfCenterPrefab;
        [Tooltip("Half edge with wall on the left side (looking from open edge inward). Optional.")]
        [SerializeField] private GameObject halfEdgeLeftPrefab;
        [Tooltip("Half edge with wall on the right side (looking from open edge inward). Optional.")]
        [SerializeField] private GameObject halfEdgeRightPrefab;

        [Header("Settings")]
        [Tooltip("The target half-cell size in world units. Prefabs are authored at 0.5×0.5 and scaled to this value. For a grid with cellSize=1.5, set this to 0.75.")]
        [SerializeField] private float prefabBaseSize = 0.5f;

        [Tooltip("Extra Y rotation added to all pieces to correct for prefab orientation. Adjust until pieces face the right direction.")]
        [SerializeField] private float rotationOffset = 0f;

        [Tooltip("Position offset applied to all pieces (world space)")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;

        [Header("Open Edge")]
        // TODO: Currently only supports open top edge. Open bottom/left/right edges need
        // additional half prefab variants and rotation logic to work correctly.
        [Tooltip("If true, the top edge is open (no border). Half prefabs fill the boundary row.")]
        [SerializeField] private bool openTopEdge = true;

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

            // Find the exit row vertex boundary for open edge filtering
            int exitVertexRow = mazeGrid.ExitRow + 1; // vertex row just below the exit row

            foreach (var piece in pieces)
            {
                var openEdgeResult = GetOpenEdgeAction(piece, gridWidth, gridHeight, exitVertexRow);

                // Skip = completely remove this piece
                if (openEdgeResult == OpenEdgeAction.Skip)
                    continue;

                // Use half prefab or normal prefab
                GameObject prefab;
                if (openEdgeResult == OpenEdgeAction.UseHalf)
                    prefab = GetHalfPrefabForType(piece);
                else
                    prefab = GetPrefabForType(piece.type);

                if (prefab == null) continue;

                // Vertex world position: vertex (vx, vy) is at the corner where 4 cells meet
                // Cell centers are at gridOrigin + (col * cellSizeX, 0, -row * cellSizeZ)
                // Vertex sits half a cell offset from cell centers
                float worldX = gridOrigin.x + (piece.vertexX - 0.5f) * cellSizeX + positionOffset.x;
                float worldZ = gridOrigin.z - (piece.vertexY - 0.5f) * cellSizeZ + positionOffset.z;
                Vector3 worldPos = new Vector3(worldX, gridOrigin.y + positionOffset.y, worldZ);

                Quaternion rotation = Quaternion.Euler(0f, piece.rotationY + rotationOffset, 0f);

                // Prefabs are 0.5×0.5 units. Scale them to prefabBaseSize.
                float scaleFactor = prefabBaseSize / 0.5f;
                Vector3 scale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

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

        private enum OpenEdgeAction { Normal, UseHalf, Skip }

        /// <summary>
        /// Determines what to do with a piece near an open edge:
        /// - Normal: use full prefab
        /// - UseHalf: use half-sized prefab (edge row of cells adjacent to open edge)
        /// - Skip: don't place anything (on the open edge boundary itself)
        /// </summary>
        private OpenEdgeAction GetOpenEdgeAction(BorderPiece piece, int gridWidth, int gridHeight, int exitVertexRow)
        {
            if (!openTopEdge)
                return OpenEdgeAction.Normal;

            if (piece.vertexY < exitVertexRow)
                return OpenEdgeAction.Skip;
            if (piece.vertexY == exitVertexRow)
                return OpenEdgeAction.UseHalf;

            return OpenEdgeAction.Normal;
        }

        private GameObject GetHalfPrefabForType(BorderPiece piece)
        {
            // At the open edge boundary:
            // - Edge pieces face toward the open side → wall is removed → use halfCenter (ground only)
            // - Corner pieces lose one wall → use halfEdge (left or right based on position)
            // - Center pieces (if any) → use halfCenter
            switch (piece.type)
            {
                case BorderPieceType.Center:
                case BorderPieceType.Edge:
                    return halfCenterPrefab;

                case BorderPieceType.OuterCorner:
                case BorderPieceType.InnerCorner:
                    // Determine left vs right based on position relative to grid center
                    bool isLeftSide = IsLeftSideOfOpenEdge(piece);
                    return isLeftSide ? halfEdgeLeftPrefab : halfEdgeRightPrefab;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Determines if a corner piece at the open top edge boundary is on the left or right side.
        /// Uses the original rotation to determine which direction the solid area is.
        /// TODO: Currently only supports open top edge. Extend for other directions when needed.
        /// </summary>
        private bool IsLeftSideOfOpenEdge(BorderPiece piece)
        {
            // For top open edge:
            //   case 1 (BR solid) → rotation 0° → left side
            //   case 2 (BL solid) → rotation 90° → right side
            return piece.rotationY == 0f;
        }

        private GameObject GetPrefabForType(BorderPieceType type)
        {
            return type switch
            {
                BorderPieceType.Empty => emptyPrefab,
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
