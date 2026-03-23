using UnityEngine;

namespace MazeGrid
{
    /// <summary>
    /// Interface for items that occupy cells in a MazeGrid.
    /// Game-specific types (e.g., PersonGroup) implement this to integrate with the grid system.
    /// </summary>
    public interface IMazeItem
    {
        Transform transform { get; }
        GameObject gameObject { get; }

        /// <summary>
        /// Called when the item becomes active (has a valid path to exit).
        /// </summary>
        void OnBecameActive();

        /// <summary>
        /// Called when the item is spawned by a MazeSpawner.
        /// </summary>
        void OnSpawnedBySpawner();

        /// <summary>
        /// Called when the item is revealed (was hidden, adjacent cell became empty).
        /// </summary>
        void OnRevealed();

        /// <summary>
        /// Whether this item is currently hidden.
        /// </summary>
        bool IsHidden { get; set; }
    }
}
