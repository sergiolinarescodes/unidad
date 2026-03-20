using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class GridUtility
    {
        public static int ToIndex(int x, int y, int width)
        {
            return y * width + x;
        }

        public static int2 ToCoord(int index, int width)
        {
            return new int2(index % width, index / width);
        }

        public static int2 WorldToGrid(in float3 worldPos, float cellSize)
        {
            return new int2(
                (int)math.floor(worldPos.x / cellSize),
                (int)math.floor(worldPos.z / cellSize));
        }

        public static float3 GridToWorld(in int2 coord, float cellSize)
        {
            return new float3(
                (coord.x + 0.5f) * cellSize,
                0f,
                (coord.y + 0.5f) * cellSize);
        }

        public static bool IsInBounds(in int2 coord, int width, int height)
        {
            return coord.x >= 0 && coord.x < width &&
                   coord.y >= 0 && coord.y < height;
        }

        public static int ManhattanDistance(in int2 a, in int2 b)
        {
            return math.abs(a.x - b.x) + math.abs(a.y - b.y);
        }

        /// <summary>
        /// Writes up to 4 cardinal neighbors into the output array.
        /// Returns the number of valid neighbors written.
        /// </summary>
        public static int GetCardinalNeighbors(in int2 coord, int width, int height,
            ref NativeArray<int2> output)
        {
            int count = 0;

            var up = new int2(coord.x, coord.y + 1);
            if (IsInBounds(in up, width, height))
                output[count++] = up;

            var down = new int2(coord.x, coord.y - 1);
            if (IsInBounds(in down, width, height))
                output[count++] = down;

            var left = new int2(coord.x - 1, coord.y);
            if (IsInBounds(in left, width, height))
                output[count++] = left;

            var right = new int2(coord.x + 1, coord.y);
            if (IsInBounds(in right, width, height))
                output[count++] = right;

            return count;
        }

        /// <summary>
        /// Writes up to 8 neighbors (cardinal + diagonal) into the output array.
        /// Returns the number of valid neighbors written.
        /// </summary>
        public static int GetEightWayNeighbors(in int2 coord, int width, int height,
            ref NativeArray<int2> output)
        {
            int count = 0;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var neighbor = new int2(coord.x + dx, coord.y + dy);
                    if (IsInBounds(in neighbor, width, height))
                        output[count++] = neighbor;
                }
            }

            return count;
        }
    }
}
