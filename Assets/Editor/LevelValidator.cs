using System.Collections.Generic;
using System.Text;
using Hollowmere.Gen;
using UnityEditor;
using UnityEngine;

namespace Hollowmere.EditorTools
{
    /// <summary>
    /// Headless soak test for level generation. Procedural layouts fail on rare seeds,
    /// not on the one you happen to play, so the only honest way to trust the generator
    /// is to run it hundreds of times and assert the invariants that make a level
    /// completable at all.
    ///
    ///   Unity.exe -quit -batchmode -projectPath . -executeMethod Hollowmere.EditorTools.LevelValidator.ValidateAll
    /// </summary>
    public static class LevelValidator
    {
        const int SeedCount = 500;
        const int FuseCount = 4;

        [MenuItem("Hollowmere/Validate Level Generation", priority = 40)]
        public static void ValidateAll()
        {
            int failures = 0;
            var report = new StringBuilder();

            // Aggregate stats, to catch "technically valid but no fun" layouts too.
            int minRooms = int.MaxValue, maxRooms = 0;
            float minExitDistance = float.MaxValue;
            int seedsWithTooFewFuses = 0;

            for (int i = 0; i < SeedCount; i++)
            {
                int seed = 1000 + i * 7919;
                LevelData data;

                try
                {
                    data = LevelGenerator.GenerateData(seed, FuseCount);
                }
                catch (System.Exception e)
                {
                    report.AppendLine($"seed {seed}: THREW {e.GetType().Name}: {e.Message}");
                    failures++;
                    continue;
                }

                var problems = Check(data, FuseCount, ref minRooms, ref maxRooms,
                    ref minExitDistance, ref seedsWithTooFewFuses);

                if (problems.Count > 0)
                {
                    failures++;
                    report.AppendLine($"seed {seed}: {string.Join("; ", problems)}");
                }
            }

            var summary = new StringBuilder();
            summary.AppendLine($"[Hollowmere] Level validation: {SeedCount - failures}/{SeedCount} seeds passed.");
            summary.AppendLine($"  rooms per level: {minRooms}-{maxRooms}");
            summary.AppendLine($"  closest exit:    {minExitDistance:F1}m from spawn");
            summary.AppendLine($"  seeds short on fuse rooms: {seedsWithTooFewFuses}");

            if (failures == 0)
            {
                Debug.Log(summary.ToString());
            }
            else
            {
                // Cap the dump; 500 failing seeds would bury the summary.
                var lines = report.ToString().Split('\n');
                summary.AppendLine("  first failures:");
                for (int i = 0; i < lines.Length && i < 15; i++)
                {
                    if (lines[i].Length > 0) summary.AppendLine("    " + lines[i]);
                }
                Debug.LogError(summary.ToString());
            }

            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static List<string> Check(LevelData data, int fuseCount, ref int minRooms, ref int maxRooms,
            ref float minExitDistance, ref int seedsWithTooFewFuses)
        {
            var problems = new List<string>();
            var grid = data.Grid;

            int rooms = data.PatrolPoints.Count;
            minRooms = Mathf.Min(minRooms, rooms);
            maxRooms = Mathf.Max(maxRooms, rooms);

            if (rooms == 0) problems.Add("no rooms carved");

            // The single most important invariant: the run must be finishable.
            if (!Reachable(grid, data.PlayerSpawn, data.ExitPosition))
                problems.Add("exit unreachable from spawn");

            float exitDistance = Vector3.Distance(data.PlayerSpawn, data.ExitPosition);
            minExitDistance = Mathf.Min(minExitDistance, exitDistance);
            if (exitDistance < LevelGenerator.CellSize * 4f)
                problems.Add($"exit only {exitDistance:F1}m from spawn");

            // Every fuse must be gettable, or the objective is impossible.
            if (data.FuseSpots.Count < fuseCount) seedsWithTooFewFuses++;

            for (int i = 0; i < data.FuseSpots.Count; i++)
            {
                if (!Reachable(grid, data.PlayerSpawn, data.FuseSpots[i]))
                    problems.Add($"fuse {i} unreachable");
            }

            // Spawn points must sit on open floor, not inside a wall.
            if (!IsOnFloor(grid, data.PlayerSpawn)) problems.Add("player spawn inside a wall");
            if (!IsOnFloor(grid, data.EntitySpawn)) problems.Add("entity spawn inside a wall");

            for (int i = 0; i < data.HidingSpots.Count; i++)
            {
                if (!IsOnFloor(grid, data.HidingSpots[i])) problems.Add($"locker {i} inside a wall");
            }

            for (int i = 0; i < data.BatterySpots.Count; i++)
            {
                if (!IsOnFloor(grid, data.BatterySpots[i])) problems.Add($"battery {i} inside a wall");
            }

            return problems;
        }

        static bool IsOnFloor(LevelGrid grid, Vector3 world)
        {
            var cell = grid.WorldToCell(world);
            return grid.IsWalkable(cell.x, cell.y);
        }

        /// <summary>
        /// Uses the same A* the entity uses, so this validates the pathfinder too - if the
        /// entity cannot path somewhere, this test fails for the same reason.
        /// </summary>
        static bool Reachable(LevelGrid grid, Vector3 from, Vector3 to)
        {
            if (Vector3.Distance(from, to) < 0.01f) return true;
            return grid.FindPath(from, to, 8000).Count > 0;
        }
    }
}
