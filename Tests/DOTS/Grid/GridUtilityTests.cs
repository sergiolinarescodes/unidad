using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class GridUtilityTests
    {
        // --- ToIndex ---

        [Test]
        public void ToIndex_Basic_ReturnsCorrectLinearIndex()
        {
            Assert.AreEqual(7, GridUtility.ToIndex(2, 1, 5)); // y*w + x = 1*5+2
        }

        [Test]
        public void ToIndex_ZeroCoords_ReturnsZero()
        {
            Assert.AreEqual(0, GridUtility.ToIndex(0, 0, 10));
        }

        // --- ToCoord ---

        [Test]
        public void ToCoord_Basic_ReturnsCorrectXY()
        {
            var coord = GridUtility.ToCoord(7, 5);
            Assert.AreEqual(new int2(2, 1), coord);
        }

        [Test]
        public void ToCoord_RoundTrip_WithToIndex()
        {
            int x = 3, y = 4, width = 8;
            int index = GridUtility.ToIndex(x, y, width);
            var coord = GridUtility.ToCoord(index, width);
            Assert.AreEqual(new int2(x, y), coord);
        }

        [Test]
        public void ToCoord_ZeroIndex_ReturnsOrigin()
        {
            Assert.AreEqual(new int2(0, 0), GridUtility.ToCoord(0, 5));
        }

        // --- WorldToGrid ---

        [Test]
        public void WorldToGrid_PositivePosition()
        {
            var result = GridUtility.WorldToGrid(new float3(2.5f, 0f, 3.7f), 1f);
            Assert.AreEqual(new int2(2, 3), result);
        }

        [Test]
        public void WorldToGrid_NegativePosition()
        {
            var result = GridUtility.WorldToGrid(new float3(-1.5f, 0f, -0.1f), 1f);
            Assert.AreEqual(new int2(-2, -1), result);
        }

        [Test]
        public void WorldToGrid_ExactBoundary()
        {
            var result = GridUtility.WorldToGrid(new float3(2f, 0f, 3f), 1f);
            Assert.AreEqual(new int2(2, 3), result);
        }

        [Test]
        public void WorldToGrid_FractionalCellSize()
        {
            var result = GridUtility.WorldToGrid(new float3(1.5f, 0f, 3.0f), 2f);
            Assert.AreEqual(new int2(0, 1), result);
        }

        // --- GridToWorld ---

        [Test]
        public void GridToWorld_Basic_ReturnsCenterOfCell()
        {
            var result = GridUtility.GridToWorld(new int2(2, 3), 1f);
            Assert.AreEqual(2.5f, result.x, 0.001f);
            Assert.AreEqual(0f, result.y, 0.001f);
            Assert.AreEqual(3.5f, result.z, 0.001f);
        }

        [Test]
        public void GridToWorld_CellSizeScaling()
        {
            var result = GridUtility.GridToWorld(new int2(1, 2), 2f);
            Assert.AreEqual(3f, result.x, 0.001f); // (1+0.5)*2
            Assert.AreEqual(0f, result.y, 0.001f);
            Assert.AreEqual(5f, result.z, 0.001f); // (2+0.5)*2
        }

        // --- IsInBounds ---

        [Test]
        public void IsInBounds_Inside_ReturnsTrue()
        {
            Assert.IsTrue(GridUtility.IsInBounds(new int2(2, 3), 5, 5));
        }

        [Test]
        public void IsInBounds_Outside_ReturnsFalse()
        {
            Assert.IsFalse(GridUtility.IsInBounds(new int2(5, 3), 5, 5));
            Assert.IsFalse(GridUtility.IsInBounds(new int2(-1, 0), 5, 5));
        }

        [Test]
        public void IsInBounds_Boundary_LastValidCell()
        {
            Assert.IsTrue(GridUtility.IsInBounds(new int2(4, 4), 5, 5));
            Assert.IsTrue(GridUtility.IsInBounds(new int2(0, 0), 5, 5));
        }

        // --- ManhattanDistance ---

        [Test]
        public void ManhattanDistance_SamePoint_ReturnsZero()
        {
            Assert.AreEqual(0, GridUtility.ManhattanDistance(new int2(3, 4), new int2(3, 4)));
        }

        [Test]
        public void ManhattanDistance_DifferentPoints()
        {
            // |1-3| + |2-6| = 2 + 4 = 6
            Assert.AreEqual(6, GridUtility.ManhattanDistance(new int2(1, 2), new int2(3, 6)));
        }

        // --- Cardinal Neighbors ---

        [Test]
        public void GetCardinalNeighbors_Center_Returns4()
        {
            var output = new NativeArray<int2>(4, Allocator.Temp);
            int count = GridUtility.GetCardinalNeighbors(new int2(2, 2), 5, 5, ref output);
            Assert.AreEqual(4, count);
            output.Dispose();
        }

        [Test]
        public void GetCardinalNeighbors_Corner_Returns2()
        {
            var output = new NativeArray<int2>(4, Allocator.Temp);
            int count = GridUtility.GetCardinalNeighbors(new int2(0, 0), 5, 5, ref output);
            Assert.AreEqual(2, count);
            output.Dispose();
        }

        [Test]
        public void GetCardinalNeighbors_Edge_Returns3()
        {
            var output = new NativeArray<int2>(4, Allocator.Temp);
            int count = GridUtility.GetCardinalNeighbors(new int2(0, 2), 5, 5, ref output);
            Assert.AreEqual(3, count);
            output.Dispose();
        }

        // --- Eight-Way Neighbors ---

        [Test]
        public void GetEightWayNeighbors_Center_Returns8()
        {
            var output = new NativeArray<int2>(8, Allocator.Temp);
            int count = GridUtility.GetEightWayNeighbors(new int2(2, 2), 5, 5, ref output);
            Assert.AreEqual(8, count);
            output.Dispose();
        }

        [Test]
        public void GetEightWayNeighbors_Corner_Returns3()
        {
            var output = new NativeArray<int2>(8, Allocator.Temp);
            int count = GridUtility.GetEightWayNeighbors(new int2(0, 0), 5, 5, ref output);
            Assert.AreEqual(3, count);
            output.Dispose();
        }

        [Test]
        public void GetEightWayNeighbors_Edge_Returns5()
        {
            var output = new NativeArray<int2>(8, Allocator.Temp);
            int count = GridUtility.GetEightWayNeighbors(new int2(0, 2), 5, 5, ref output);
            Assert.AreEqual(5, count);
            output.Dispose();
        }
    }
}
