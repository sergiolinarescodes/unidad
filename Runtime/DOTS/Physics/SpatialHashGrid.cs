using Unity.Collections;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Spatial hash grid utility for broadphase collision detection.
    /// Partitions 3D space into uniform cells and maps entity indices to cells,
    /// enabling O(1) average-case neighbor lookups instead of O(N) brute force.
    /// Pure math — Burst inlines these automatically when called from Burst jobs.
    /// </summary>
    public static class SpatialHashGrid
    {
        public static int3 CellCoord(float3 position, float cellSize)
            => new int3(math.floor(position / cellSize));

        public static int HashCell(int3 cell)
            => (cell.x * 73856093) ^ (cell.y * 19349663) ^ (cell.z * 83492791);

        public static int HashPosition(float3 position, float cellSize)
            => HashCell(CellCoord(position, cellSize));

        /// <summary>
        /// Build a spatial hash map from an array of positions.
        /// Each position is inserted at its cell hash, mapping to the array index.
        /// </summary>
        public static NativeParallelMultiHashMap<int, int> Build(
            NativeArray<float3> positions, float cellSize, Allocator allocator)
        {
            var map = new NativeParallelMultiHashMap<int, int>(
                math.max(positions.Length * 2, 64), allocator);

            for (int i = 0; i < positions.Length; i++)
                map.Add(HashPosition(positions[i], cellSize), i);

            return map;
        }

        /// <summary>
        /// Populate an existing hash map (must be cleared first) from positions.
        /// Avoids allocation when reusing a persistent map.
        /// </summary>
        public static void Rebuild(
            ref NativeParallelMultiHashMap<int, int> map,
            NativeArray<float3> positions, float cellSize)
        {
            if (map.Capacity < positions.Length * 2)
                map.Capacity = positions.Length * 2;

            for (int i = 0; i < positions.Length; i++)
                map.Add(HashPosition(positions[i], cellSize), i);
        }
    }
}
