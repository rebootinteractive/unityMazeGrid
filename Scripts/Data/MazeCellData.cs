using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace MazeGrid
{
    [Serializable]
    public class MazeCellData
    {
        public GridCellState state = GridCellState.Valid;

        [FormerlySerializedAs("typeIndex")]
        [Tooltip("Integer ID representing the item type (game interprets this)")]
        public int itemTypeId = -1;

        [Tooltip("If true, the item in this cell starts hidden")]
        public bool isHidden = false;

        [Tooltip("Generic per-cell metadata flags (game interprets this)")]
        public int metadata = 0;

        public SpawnerDirection direction = SpawnerDirection.Down;

        [Tooltip("Cell data for spawner queue items")]
        public List<MazeCellData> spawnerQueue = new List<MazeCellData>();
    }
}
