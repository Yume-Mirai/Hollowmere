using System.Collections.Generic;
using UnityEngine;

namespace Hollowmere.Gen
{
    /// <summary>Everything the game needs to know about a generated level.</summary>
    public class LevelData
    {
        public LevelGrid Grid;
        public Transform Root;
        public Vector3 PlayerSpawn;
        public Vector3 ExitPosition;
        public Vector3 EntitySpawn;
        public readonly List<Vector3> FuseSpots = new List<Vector3>();
        public readonly List<Vector3> BatterySpots = new List<Vector3>();
        public readonly List<Vector3> HidingSpots = new List<Vector3>();
        public readonly List<Vector3> PatrolPoints = new List<Vector3>();
    }

    /// <summary>
    /// Rooms-and-corridors generator. Deliberately carves a few redundant connections:
    /// a pure tree of corridors makes every chase a dead-end lottery, and loops are what
    /// let a player actually out-manoeuvre the entity.
    /// </summary>
    public static class LevelGenerator
    {
        public const float CellSize = 3f;
        public const float WallHeight = 3.4f;

        // Public so the level's size and density are tunable from one obvious place.
        public const int GridW = 41;
        public const int GridH = 41;
        public const int RoomAttempts = 140;
        public const int MinRoom = 3;
        public const int MaxRoom = 8;

        /// <summary>
        /// Layout only - grid, rooms and key positions, with no GameObjects created.
        /// Split out from <see cref="Generate"/> so the layout can be validated across
        /// hundreds of seeds headlessly, without instantiating a level's worth of geometry.
        /// </summary>
        public static LevelData GenerateData(int seed, int fuseCount)
        {
            var rng = new System.Random(seed);
            var data = new LevelData { Grid = new LevelGrid(GridW, GridH, CellSize) };

            var rooms = CarveRooms(data.Grid, rng);
            CarveCorridors(data.Grid, rooms, rng);
            ChooseKeyPositions(data, rooms, rng, fuseCount);

            return data;
        }

        public static LevelData Generate(int seed, int fuseCount)
        {
            var data = GenerateData(seed, fuseCount);

            // Separate stream for cosmetics: light and crate placement must not shift the
            // layout, so decorating cannot share the rng that carved it.
            var rng = new System.Random(unchecked(seed * 31 + 17));

            var root = new GameObject("Level").transform;
            data.Root = root;
            BuildGeometry(data, root, rng);

            // Collapse the wall cubes into batched meshes; ~700 renderers is otherwise
            // enough draw calls to matter on a low-end GPU.
            StaticBatchingUtility.Combine(root.gameObject);

            return data;
        }

        // --- carving ---------------------------------------------------------

        static List<RectInt> CarveRooms(LevelGrid grid, System.Random rng)
        {
            var rooms = new List<RectInt>();

            for (int attempt = 0; attempt < RoomAttempts; attempt++)
            {
                int w = rng.Next(MinRoom, MaxRoom + 1);
                int h = rng.Next(MinRoom, MaxRoom + 1);
                int x = rng.Next(2, Mathf.Max(3, GridW - w - 2));
                int y = rng.Next(2, Mathf.Max(3, GridH - h - 2));
                var candidate = new RectInt(x, y, w, h);

                // One cell of padding so rooms never share a wall.
                var padded = new RectInt(x - 1, y - 1, w + 2, h + 2);
                bool overlaps = false;
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (Overlaps(padded, rooms[i])) { overlaps = true; break; }
                }
                if (overlaps) continue;

                rooms.Add(candidate);
                for (int cy = candidate.yMin; cy < candidate.yMax; cy++)
                    for (int cx = candidate.xMin; cx < candidate.xMax; cx++)
                        grid.SetWalkable(cx, cy, true);
            }

            return rooms;
        }

        static bool Overlaps(RectInt a, RectInt b) =>
            a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;

        static void CarveCorridors(LevelGrid grid, List<RectInt> rooms, System.Random rng)
        {
            if (rooms.Count < 2) return;

            // Spanning chain guarantees connectivity...
            for (int i = 1; i < rooms.Count; i++)
            {
                CarveL(grid, Center(rooms[i - 1]), Center(rooms[i]), rng);
            }

            // ...and these extras add the loops that make evasion possible.
            int extra = Mathf.Max(2, rooms.Count / 3);
            for (int i = 0; i < extra; i++)
            {
                var a = rooms[rng.Next(rooms.Count)];
                var b = rooms[rng.Next(rooms.Count)];
                if (a.Equals(b)) continue;
                CarveL(grid, Center(a), Center(b), rng);
            }
        }

        static Vector2Int Center(RectInt r) => new Vector2Int(r.xMin + r.width / 2, r.yMin + r.height / 2);

        static void CarveL(LevelGrid grid, Vector2Int a, Vector2Int b, System.Random rng)
        {
            bool horizontalFirst = rng.Next(2) == 0;
            if (horizontalFirst)
            {
                CarveHorizontal(grid, a.x, b.x, a.y);
                CarveVertical(grid, a.y, b.y, b.x);
            }
            else
            {
                CarveVertical(grid, a.y, b.y, a.x);
                CarveHorizontal(grid, a.x, b.x, b.y);
            }
        }

        static void CarveHorizontal(LevelGrid grid, int x0, int x1, int y)
        {
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++) grid.SetWalkable(x, y, true);
        }

        static void CarveVertical(LevelGrid grid, int y0, int y1, int x)
        {
            for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++) grid.SetWalkable(x, y, true);
        }

        // --- placement -------------------------------------------------------

        static void ChooseKeyPositions(LevelData data, List<RectInt> rooms, System.Random rng, int fuseCount)
        {
            if (rooms.Count == 0)
            {
                // Degenerate seed - carve a fallback chamber so the game is still playable.
                for (int y = 5; y < 15; y++)
                    for (int x = 5; x < 15; x++)
                        data.Grid.SetWalkable(x, y, true);
                rooms.Add(new RectInt(5, 5, 10, 10));
            }

            var startRoom = rooms[0];
            data.PlayerSpawn = ToWorld(data.Grid, Center(startRoom));

            // Exit goes in the room furthest from the start, so the level has a real span.
            int exitIndex = 0;
            float bestDistance = -1f;
            for (int i = 1; i < rooms.Count; i++)
            {
                float d = Vector2Int.Distance(Center(startRoom), Center(rooms[i]));
                if (d > bestDistance) { bestDistance = d; exitIndex = i; }
            }
            data.ExitPosition = ToWorld(data.Grid, Center(rooms[exitIndex]));

            // Fuses spread across the rooms that are neither start nor exit.
            var candidates = new List<int>();
            for (int i = 0; i < rooms.Count; i++)
            {
                if (i != 0 && i != exitIndex) candidates.Add(i);
            }
            Shuffle(candidates, rng);

            for (int i = 0; i < fuseCount && i < candidates.Count; i++)
            {
                data.FuseSpots.Add(ToWorld(data.Grid, Center(rooms[candidates[i]])));
            }

            // Every room is a patrol target; hiding spots and batteries scatter more widely.
            for (int i = 0; i < rooms.Count; i++) data.PatrolPoints.Add(ToWorld(data.Grid, Center(rooms[i])));

            int hidingCount = Mathf.Clamp(rooms.Count / 2, 3, 10);
            for (int i = 0; i < hidingCount && i < candidates.Count; i++)
            {
                var room = rooms[candidates[(i * 3) % candidates.Count]];
                var cell = new Vector2Int(room.xMin, room.yMin + rng.Next(room.height));
                data.HidingSpots.Add(ToWorld(data.Grid, cell));
            }

            int batteryCount = Mathf.Clamp(fuseCount + 3, 4, 12);
            for (int i = 0; i < batteryCount; i++)
            {
                var room = rooms[rng.Next(rooms.Count)];
                var cell = new Vector2Int(room.xMin + rng.Next(room.width), room.yMin + rng.Next(room.height));
                data.BatterySpots.Add(ToWorld(data.Grid, cell));
            }

            // Start the entity as far from the player as the room list allows.
            data.EntitySpawn = data.ExitPosition;
        }

        static Vector3 ToWorld(LevelGrid grid, Vector2Int cell) => grid.CellToWorld(cell.x, cell.y);

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        // --- geometry --------------------------------------------------------

        static void BuildGeometry(LevelData data, Transform root, System.Random rng)
        {
            var grid = data.Grid;
            float spanX = grid.Width * CellSize;
            float spanZ = grid.Height * CellSize;
            var centre = new Vector3(spanX * 0.5f - CellSize * 0.5f, 0f, spanZ * 0.5f - CellSize * 0.5f);

            // Floor and ceiling are single slabs - cheaper than per-cell tiles and the
            // player can never see the level edge anyway.
            var floor = MakeBox(root, "Floor", centre + new Vector3(0f, -0.25f, 0f),
                new Vector3(spanX, 0.5f, spanZ), ProceduralMaterials.Floor);
            floor.isStatic = true;

            var ceiling = MakeBox(root, "Ceiling", centre + new Vector3(0f, WallHeight + 0.25f, 0f),
                new Vector3(spanX, 0.5f, spanZ), ProceduralMaterials.Ceiling);
            ceiling.isStatic = true;

            // Only wall cells that touch open space need geometry.
            var wallParent = new GameObject("Walls").transform;
            wallParent.SetParent(root, false);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.IsWalkable(x, y)) continue;
                    if (!TouchesOpenSpace(grid, x, y)) continue;

                    var pos = grid.CellToWorld(x, y) + new Vector3(0f, WallHeight * 0.5f, 0f);
                    var wall = MakeBox(wallParent, "Wall", pos,
                        new Vector3(CellSize, WallHeight, CellSize), ProceduralMaterials.Concrete);
                    wall.isStatic = true;
                }
            }

            BuildLights(data, root, rng);
            BuildProps(data, root, rng);
        }

        static bool TouchesOpenSpace(LevelGrid grid, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (grid.IsWalkable(x + dx, y + dy)) return true;
            return false;
        }

        static void BuildLights(LevelData data, Transform root, System.Random rng)
        {
            var parent = new GameObject("Lights").transform;
            parent.SetParent(root, false);

            // Sparse, failing ceiling lights. They exist to give the darkness a shape,
            // not to actually light the level - that is the flashlight's job.
            for (int i = 0; i < data.PatrolPoints.Count; i++)
            {
                if (rng.NextDouble() > 0.55) continue;

                var go = new GameObject("CeilingLight");
                go.transform.SetParent(parent, false);
                go.transform.position = data.PatrolPoints[i] + new Vector3(0f, WallHeight - 0.35f, 0f);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = CellSize * 3.2f;
                light.intensity = 0.9f;
                light.color = new Color(0.85f, 0.80f, 0.68f);
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.85f;

                var flicker = go.AddComponent<FlickerLight>();
                flicker.Configure(0.9f, (float)rng.NextDouble() * 10f, rng.NextDouble() > 0.7);

                // A small emissive plate so the source is visible, not just its pool of light.
                var bulb = MakeBox(go.transform, "Bulb", go.transform.position + Vector3.up * 0.18f,
                    new Vector3(0.9f, 0.08f, 0.35f), ProceduralMaterials.Emissive(new Color(1f, 0.94f, 0.78f), 1.6f));
                Object.Destroy(bulb.GetComponent<Collider>());
            }
        }

        static void BuildProps(LevelData data, Transform root, System.Random rng)
        {
            var parent = new GameObject("Props").transform;
            parent.SetParent(root, false);

            // Crates break sightlines, which is the whole point - a chase through an
            // empty box is not a chase.
            int crateCount = data.PatrolPoints.Count * 2;
            for (int i = 0; i < crateCount; i++)
            {
                var anchor = data.PatrolPoints[rng.Next(data.PatrolPoints.Count)];
                var offset = new Vector3((float)(rng.NextDouble() - 0.5) * CellSize * 2.5f, 0f,
                                         (float)(rng.NextDouble() - 0.5) * CellSize * 2.5f);
                var pos = anchor + offset;

                var cell = data.Grid.WorldToCell(pos);
                if (!data.Grid.IsWalkable(cell.x, cell.y)) continue;

                float h = 0.6f + (float)rng.NextDouble() * 0.9f;
                var crate = MakeBox(parent, "Crate", pos + new Vector3(0f, h * 0.5f, 0f),
                    new Vector3(0.8f + (float)rng.NextDouble() * 0.5f, h, 0.8f + (float)rng.NextDouble() * 0.5f),
                    ProceduralMaterials.RustMetal);
                crate.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                crate.isStatic = true;
            }
        }

        /// <summary>Creates a scaled cube with a collider. The workhorse of the whole level.</summary>
        public static GameObject MakeBox(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
