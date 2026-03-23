using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace MazeGrid
{
    [Serializable]
    public class MazeCellData
    {
        public GridCellState state = GridCellState.Full;

        [FormerlySerializedAs("typeIndex")]
        [Tooltip("Integer ID representing the item type (game interprets this)")]
        public int itemTypeId = -1;

        [Tooltip("If true, the item in this cell starts hidden")]
        public bool isHidden = false;

        public SpawnerDirection direction = SpawnerDirection.Down;

        [Tooltip("Type IDs for spawner queue")]
        public List<int> spawnerQueue = new List<int>();
    }
}
