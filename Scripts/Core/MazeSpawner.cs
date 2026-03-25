using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MazeGrid
{
    /// <summary>
    /// A spawner that generates IMazeItems into the grid when its target cell is empty.
    /// Spawners occupy a grid cell (impassable for pathfinding) and push new items
    /// in their configured direction.
    /// Game subclass overrides OnInitialized() and OnItemCreated() for custom effects.
    /// </summary>
    public class MazeSpawner : MonoBehaviour
    {
        [SerializeField] private TMP_Text remainingCountText;

        private MazeGrid parentGrid;
        private Vector2Int gridPosition;
        private SpawnerDirection direction;
        private Queue<MazeCellData> spawnQueue = new Queue<MazeCellData>();

        /// <summary>Fires after a new item is created and registered.</summary>
        public event Action<IMazeItem, Vector2Int> OnItemSpawned;

        /// <summary>Fires when the spawn queue becomes empty.</summary>
        public event Action OnQueueEmpty;

        public Vector2Int GridPosition => gridPosition;
        public Vector2Int TargetCellPosition => GetTargetCellPosition();
        public bool HasItemsToSpawn => spawnQueue.Count > 0;
        public int RemainingCount => spawnQueue.Count;
        protected MazeGrid ParentGrid => parentGrid;

        public void Initialize(
            MazeGrid grid,
            Vector2Int position,
            SpawnerDirection dir,
            List<MazeCellData> typeQueue)
        {
            parentGrid = grid;
            gridPosition = position;
            direction = dir;

            spawnQueue.Clear();
            if (typeQueue != null)
            {
                foreach (var cellData in typeQueue)
                {
                    spawnQueue.Enqueue(cellData);
                }
            }

            transform.rotation = GetRotationForDirection(direction);

            if (parentGrid != null)
            {
                parentGrid.OnCellCleared += OnCellCleared;
            }

            UpdateRemainingCountText();
            OnInitialized();
        }

        private void OnDestroy()
        {
            if (parentGrid != null)
            {
                parentGrid.OnCellCleared -= OnCellCleared;
            }
        }

        private void OnCellCleared(Vector2Int clearedPosition)
        {
            if (clearedPosition == TargetCellPosition)
            {
                TrySpawn();
            }
        }

        public void TrySpawn()
        {
            if (!Application.isPlaying)
                return;

            if (!HasItemsToSpawn)
                return;

            Vector2Int targetPos = TargetCellPosition;

            if (!parentGrid.IsCellEmpty(targetPos))
                return;

            MazeCellData cellData = spawnQueue.Dequeue();
            Vector3 worldPos = parentGrid.GetCellWorldPosition(targetPos);

            var item = parentGrid.CreateItemForSpawner(cellData, targetPos, worldPos);

            if (item != null)
            {
                parentGrid.RegisterItem(item, targetPos);
                item.OnBecameActive();
                item.OnSpawnedBySpawner();
                OnItemCreated(item, targetPos);
                OnItemSpawned?.Invoke(item, targetPos);
            }

            UpdateRemainingCountText();
            OnQueueChanged();

            if (spawnQueue.Count == 0)
            {
                OnQueueEmpty?.Invoke();
                OnQueueExhausted();
            }
        }

        /// <summary>
        /// Called after Initialize completes. Override for custom setup (e.g., update UI).
        /// </summary>
        protected virtual void OnInitialized() { }

        /// <summary>
        /// Called after a new item is created and registered. Override for custom effects.
        /// </summary>
        protected virtual void OnItemCreated(IMazeItem item, Vector2Int gridPos) { }

        /// <summary>
        /// Called when the spawn queue count changes (after dequeue).
        /// </summary>
        protected virtual void OnQueueChanged() { }

        /// <summary>
        /// Called when the spawn queue becomes empty.
        /// </summary>
        protected virtual void OnQueueExhausted() { }

        private void UpdateRemainingCountText()
        {
            if (remainingCountText != null)
            {
                remainingCountText.text = RemainingCount.ToString();
            }
        }

        private Vector2Int GetTargetCellPosition()
        {
            return gridPosition + GetDirectionOffset(direction);
        }

        private static Vector2Int GetDirectionOffset(SpawnerDirection dir)
        {
            return dir switch
            {
                SpawnerDirection.Up => new Vector2Int(0, -1),
                SpawnerDirection.Down => new Vector2Int(0, 1),
                SpawnerDirection.Left => new Vector2Int(-1, 0),
                SpawnerDirection.Right => new Vector2Int(1, 0),
                _ => Vector2Int.zero
            };
        }

        private static Quaternion GetRotationForDirection(SpawnerDirection dir)
        {
            return dir switch
            {
                SpawnerDirection.Up => Quaternion.Euler(0, 0, 0),
                SpawnerDirection.Down => Quaternion.Euler(0, 180, 0),
                SpawnerDirection.Left => Quaternion.Euler(0, -90, 0),
                SpawnerDirection.Right => Quaternion.Euler(0, 90, 0),
                _ => Quaternion.identity
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 0.2f, 0.8f));

            Vector3 dirOffset = direction switch
            {
                SpawnerDirection.Up => new Vector3(0, 0, -1),
                SpawnerDirection.Down => new Vector3(0, 0, 1),
                SpawnerDirection.Left => new Vector3(-1, 0, 0),
                SpawnerDirection.Right => new Vector3(1, 0, 0),
                _ => Vector3.zero
            };

            Gizmos.color = Color.yellow;
            Vector3 start = transform.position + Vector3.up * 0.2f;
            Vector3 end = start + dirOffset * 0.5f;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.1f);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.5f,
                $"Queue: {spawnQueue.Count}");
        }
#endif
    }
}
