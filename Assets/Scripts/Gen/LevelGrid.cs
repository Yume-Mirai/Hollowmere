using System.Collections.Generic;
using UnityEngine;

namespace Hollowmere.Gen
{
    /// <summary>
    /// Occupancy grid for the generated level plus A* pathfinding over it.
    /// The level is grid-authored anyway, so a custom A* is cheaper, fully deterministic
    /// and easier to debug than runtime NavMesh baking - and it adds no package dependency.
    /// </summary>
    public class LevelGrid
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;

        readonly bool[] _walkable;

        public LevelGrid(int width, int height, float cellSize)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            _walkable = new bool[width * height];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsWalkable(int x, int y) => InBounds(x, y) && _walkable[y * Width + x];

        public void SetWalkable(int x, int y, bool value)
        {
            if (InBounds(x, y)) _walkable[y * Width + x] = value;
        }

        public Vector3 CellToWorld(int x, int y) => new Vector3(x * CellSize, 0f, y * CellSize);

        public Vector2Int WorldToCell(Vector3 world) => new Vector2Int(
            Mathf.RoundToInt(world.x / CellSize),
            Mathf.RoundToInt(world.z / CellSize));

        /// <summary>Nearest walkable cell to a world position, searched in rings outward.</summary>
        public Vector2Int NearestWalkable(Vector3 world, int maxRadius = 12)
        {
            var start = WorldToCell(world);
            if (IsWalkable(start.x, start.y)) return start;

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        // Only the ring, not the filled square.
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        int nx = start.x + dx, ny = start.y + dy;
                        if (IsWalkable(nx, ny)) return new Vector2Int(nx, ny);
                    }
                }
            }
            return start;
        }

        // --- A* --------------------------------------------------------------

        static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int( 1,  0), new Vector2Int(-1,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
            new Vector2Int( 1,  1), new Vector2Int( 1, -1),
            new Vector2Int(-1,  1), new Vector2Int(-1, -1),
        };

        readonly Dictionary<int, int> _cameFrom = new Dictionary<int, int>(512);
        readonly Dictionary<int, float> _gScore = new Dictionary<int, float>(512);

        /// <summary>
        /// World-space waypoints from one point to another, excluding the start cell.
        /// Returns an empty list when no route exists or the search budget is exhausted.
        /// </summary>
        public List<Vector3> FindPath(Vector3 fromWorld, Vector3 toWorld, int maxNodes = 4000)
        {
            var start = NearestWalkable(fromWorld);
            var goal = NearestWalkable(toWorld);
            var result = new List<Vector3>();
            if (start == goal) return result;

            _cameFrom.Clear();
            _gScore.Clear();

            var open = new MinHeap(256);
            int startIdx = Index(start), goalIdx = Index(goal);

            _gScore[startIdx] = 0f;
            open.Push(startIdx, Heuristic(start, goal));

            int expanded = 0;
            bool found = false;

            while (open.Count > 0 && expanded < maxNodes)
            {
                int current = open.Pop();
                if (current == goalIdx) { found = true; break; }
                expanded++;

                int cx = current % Width, cy = current / Width;
                float currentG = _gScore.TryGetValue(current, out var g) ? g : float.MaxValue;

                for (int i = 0; i < Neighbours.Length; i++)
                {
                    int ox = Neighbours[i].x, oy = Neighbours[i].y;
                    int nx = cx + ox, ny = cy + oy;
                    if (!IsWalkable(nx, ny)) continue;

                    // Refuse to cut a diagonal through a wall corner.
                    if (ox != 0 && oy != 0 && (!IsWalkable(cx + ox, cy) || !IsWalkable(cx, cy + oy))) continue;

                    float step = (ox != 0 && oy != 0) ? 1.41421f : 1f;
                    float tentative = currentG + step;
                    int nIdx = ny * Width + nx;

                    if (_gScore.TryGetValue(nIdx, out var existing) && tentative >= existing) continue;

                    _cameFrom[nIdx] = current;
                    _gScore[nIdx] = tentative;
                    open.Push(nIdx, tentative + Heuristic(new Vector2Int(nx, ny), goal));
                }
            }

            if (!found) return result;

            // Walk the parent chain back, then reverse into travel order.
            int node = goalIdx;
            var reversed = new List<int>();
            int guard = 0;
            while (node != startIdx && guard++ < maxNodes)
            {
                reversed.Add(node);
                if (!_cameFrom.TryGetValue(node, out node)) return result;
            }

            for (int i = reversed.Count - 1; i >= 0; i--)
            {
                int idx = reversed[i];
                result.Add(CellToWorld(idx % Width, idx / Width));
            }
            return result;
        }

        int Index(Vector2Int c) => c.y * Width + c.x;

        static float Heuristic(Vector2Int a, Vector2Int b)
        {
            // Octile distance - admissible for 8-way movement with diagonal cost sqrt(2).
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return (dx + dy) + (1.41421f - 2f) * Mathf.Min(dx, dy);
        }

        /// <summary>Minimal binary heap keyed on f-score. Allocation-light, no LINQ.</summary>
        class MinHeap
        {
            int[] _items;
            float[] _priorities;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                _items = new int[capacity];
                _priorities = new float[capacity];
            }

            public void Push(int item, float priority)
            {
                if (Count == _items.Length)
                {
                    System.Array.Resize(ref _items, Count * 2);
                    System.Array.Resize(ref _priorities, Count * 2);
                }
                _items[Count] = item;
                _priorities[Count] = priority;

                int i = Count++;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_priorities[parent] <= _priorities[i]) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public int Pop()
            {
                int top = _items[0];
                Count--;
                _items[0] = _items[Count];
                _priorities[0] = _priorities[Count];

                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1, right = left + 1, smallest = i;
                    if (left < Count && _priorities[left] < _priorities[smallest]) smallest = left;
                    if (right < Count && _priorities[right] < _priorities[smallest]) smallest = right;
                    if (smallest == i) break;
                    Swap(i, smallest);
                    i = smallest;
                }
                return top;
            }

            void Swap(int a, int b)
            {
                int ti = _items[a]; _items[a] = _items[b]; _items[b] = ti;
                float tp = _priorities[a]; _priorities[a] = _priorities[b]; _priorities[b] = tp;
            }
        }
    }
}
