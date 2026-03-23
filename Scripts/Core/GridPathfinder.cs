using System.Collections.Generic;
using UnityEngine;

namespace MazeGrid
{
    /// <summary>
    /// A* pathfinding implementation for 2D grids
    /// Supports 4-directional movement (no diagonals)
    /// Uses Manhattan distance as heuristic
    /// Supports optional separator checks between cells
    /// </summary>
    public static class GridPathfinder
    {
        private static readonly Vector2Int[] Directions = new Vector2Int[]
        {
            new Vector2Int(0, -1),  // Up (decreasing row index)
            new Vector2Int(0, 1),   // Down (increasing row index)
            new Vector2Int(-1, 0),  // Left (decreasing X)
            new Vector2Int(1, 0)    // Right (increasing X)
        };

        public delegate bool CanMoveBetweenDelegate(Vector2Int from, Vector2Int to);

        public static List<Vector2Int> FindPath(
            Vector2Int start,
            Vector2Int goal,
            System.Func<Vector2Int, bool> isWalkable,
            int gridWidth,
            int gridHeight)
        {
            if (!isWalkable(start) || !isWalkable(goal))
                return new List<Vector2Int>();

            if (start == goal)
                return new List<Vector2Int> { start };

            var openSet = new PriorityQueue<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, int>();
            var fScore = new Dictionary<Vector2Int, int>();

            gScore[start] = 0;
            fScore[start] = ManhattanDistance(start, goal);
            openSet.Enqueue(start, fScore[start]);

            while (openSet.Count > 0)
            {
                Vector2Int current = openSet.Dequeue();

                if (current == goal)
                {
                    return ReconstructPath(cameFrom, current);
                }

                foreach (var direction in Directions)
                {
                    Vector2Int neighbor = current + direction;

                    if (neighbor.x < 0 || neighbor.x >= gridWidth ||
                        neighbor.y < 0 || neighbor.y >= gridHeight)
                        continue;

                    if (!isWalkable(neighbor))
                        continue;

                    int tentativeGScore = gScore[current] + 1;

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + ManhattanDistance(neighbor, goal);

                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Enqueue(neighbor, fScore[neighbor]);
                        }
                    }
                }
            }

            return new List<Vector2Int>();
        }

        public static bool HasPath(
            Vector2Int start,
            Vector2Int goal,
            System.Func<Vector2Int, bool> isWalkable,
            int gridWidth,
            int gridHeight)
        {
            var path = FindPath(start, goal, isWalkable, gridWidth, gridHeight);
            return path.Count > 0;
        }

        public static List<Vector2Int> FindPathToRow(
            Vector2Int start,
            int targetRow,
            System.Func<Vector2Int, bool> isWalkable,
            int gridWidth,
            int gridHeight)
        {
            if (start.y == targetRow)
                return new List<Vector2Int> { start };

            List<Vector2Int> shortestPath = new List<Vector2Int>();
            int shortestDistance = int.MaxValue;

            for (int x = 0; x < gridWidth; x++)
            {
                Vector2Int targetCell = new Vector2Int(x, targetRow);

                if (!isWalkable(targetCell))
                    continue;

                var path = FindPath(start, targetCell, isWalkable, gridWidth, gridHeight);

                if (path.Count > 0 && path.Count < shortestDistance)
                {
                    shortestPath = path;
                    shortestDistance = path.Count;
                }
            }

            return shortestPath;
        }

        public static bool HasPathToRow(
            Vector2Int start,
            int targetRow,
            System.Func<Vector2Int, bool> isWalkable,
            int gridWidth,
            int gridHeight)
        {
            var path = FindPathToRow(start, targetRow, isWalkable, gridWidth, gridHeight);
            return path.Count > 0;
        }

        #region Overloads with Separator Support

        public static List<Vector2Int> FindPath(
            Vector2Int start,
            Vector2Int goal,
            System.Func<Vector2Int, bool> isWalkable,
            CanMoveBetweenDelegate canMoveBetween,
            int gridWidth,
            int gridHeight)
        {
            if (!isWalkable(start) || !isWalkable(goal))
                return new List<Vector2Int>();

            if (start == goal)
                return new List<Vector2Int> { start };

            var openSet = new PriorityQueue<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, int>();
            var fScore = new Dictionary<Vector2Int, int>();

            gScore[start] = 0;
            fScore[start] = ManhattanDistance(start, goal);
            openSet.Enqueue(start, fScore[start]);

            while (openSet.Count > 0)
            {
                Vector2Int current = openSet.Dequeue();

                if (current == goal)
                {
                    return ReconstructPath(cameFrom, current);
                }

                foreach (var direction in Directions)
                {
                    Vector2Int neighbor = current + direction;

                    if (neighbor.x < 0 || neighbor.x >= gridWidth ||
                        neighbor.y < 0 || neighbor.y >= gridHeight)
                        continue;

                    if (!isWalkable(neighbor))
                        continue;

                    if (canMoveBetween != null && !canMoveBetween(current, neighbor))
                        continue;

                    int tentativeGScore = gScore[current] + 1;

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + ManhattanDistance(neighbor, goal);

                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Enqueue(neighbor, fScore[neighbor]);
                        }
                    }
                }
            }

            return new List<Vector2Int>();
        }

        public static bool HasPath(
            Vector2Int start,
            Vector2Int goal,
            System.Func<Vector2Int, bool> isWalkable,
            CanMoveBetweenDelegate canMoveBetween,
            int gridWidth,
            int gridHeight)
        {
            var path = FindPath(start, goal, isWalkable, canMoveBetween, gridWidth, gridHeight);
            return path.Count > 0;
        }

        public static List<Vector2Int> FindPathToRow(
            Vector2Int start,
            int targetRow,
            System.Func<Vector2Int, bool> isWalkable,
            CanMoveBetweenDelegate canMoveBetween,
            int gridWidth,
            int gridHeight)
        {
            if (start.y == targetRow)
                return new List<Vector2Int> { start };

            List<Vector2Int> shortestPath = new List<Vector2Int>();
            int shortestDistance = int.MaxValue;

            for (int x = 0; x < gridWidth; x++)
            {
                Vector2Int targetCell = new Vector2Int(x, targetRow);

                if (!isWalkable(targetCell))
                    continue;

                var path = FindPath(start, targetCell, isWalkable, canMoveBetween, gridWidth, gridHeight);

                if (path.Count > 0 && path.Count < shortestDistance)
                {
                    shortestPath = path;
                    shortestDistance = path.Count;
                }
            }

            return shortestPath;
        }

        public static bool HasPathToRow(
            Vector2Int start,
            int targetRow,
            System.Func<Vector2Int, bool> isWalkable,
            CanMoveBetweenDelegate canMoveBetween,
            int gridWidth,
            int gridHeight)
        {
            var path = FindPathToRow(start, targetRow, isWalkable, canMoveBetween, gridWidth, gridHeight);
            return path.Count > 0;
        }

        #endregion

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
        {
            List<Vector2Int> path = new List<Vector2Int> { current };

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        private class PriorityQueue<T>
        {
            private List<(T item, int priority)> elements = new List<(T, int)>();

            public int Count => elements.Count;

            public void Enqueue(T item, int priority)
            {
                elements.Add((item, priority));
            }

            public T Dequeue()
            {
                int bestIndex = 0;
                for (int i = 1; i < elements.Count; i++)
                {
                    if (elements[i].priority < elements[bestIndex].priority)
                    {
                        bestIndex = i;
                    }
                }

                T bestItem = elements[bestIndex].item;
                elements.RemoveAt(bestIndex);
                return bestItem;
            }

            public bool Contains(T item)
            {
                foreach (var element in elements)
                {
                    if (EqualityComparer<T>.Default.Equals(element.item, item))
                        return true;
                }
                return false;
            }
        }
    }
}
