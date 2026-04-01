namespace MazeGrid
{
    public enum GridCellState
    {
        Invalid = 0,   // Not part of the board (was "Empty")
        Valid = 1,     // Playable cell — may or may not have an item (was "Full")
        Spawner = 2,   // Spawner occupying this cell

        [System.Obsolete("Use Invalid instead. Kept for serialization backward compatibility.")]
        GridWall = 3   // Treated as Invalid at runtime
    }

    public enum SpawnerDirection { Up, Down, Left, Right }
}
