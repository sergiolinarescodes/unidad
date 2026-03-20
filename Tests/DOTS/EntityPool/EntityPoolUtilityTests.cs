using NUnit.Framework;
using Unity.Collections;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class EntityPoolUtilityTests
    {
        [Test]
        public void HashPoolId_Deterministic()
        {
            var name1 = new FixedString64Bytes("TestPool");
            var name2 = new FixedString64Bytes("TestPool");
            Assert.AreEqual(
                EntityPoolUtility.HashPoolId(in name1),
                EntityPoolUtility.HashPoolId(in name2));
        }

        [Test]
        public void HashPoolId_DifferentStrings_DifferentHash()
        {
            var a = new FixedString64Bytes("PoolA");
            var b = new FixedString64Bytes("PoolB");
            Assert.AreNotEqual(
                EntityPoolUtility.HashPoolId(in a),
                EntityPoolUtility.HashPoolId(in b));
        }

        [Test]
        public void GetAvailableCount_MatchingPoolId()
        {
            var array = new NativeArray<Pooled>(3, Allocator.Temp);
            array[0] = new Pooled { PoolId = 1 };
            array[1] = new Pooled { PoolId = 2 };
            array[2] = new Pooled { PoolId = 1 };
            Assert.AreEqual(2, EntityPoolUtility.GetAvailableCount(in array, 1));
            array.Dispose();
        }

        [Test]
        public void GetAvailableCount_EmptyArray_ReturnsZero()
        {
            var array = new NativeArray<Pooled>(0, Allocator.Temp);
            Assert.AreEqual(0, EntityPoolUtility.GetAvailableCount(in array, 1));
            array.Dispose();
        }

        [Test]
        public void GetAvailableCount_NoMatch_ReturnsZero()
        {
            var array = new NativeArray<Pooled>(2, Allocator.Temp);
            array[0] = new Pooled { PoolId = 1 };
            array[1] = new Pooled { PoolId = 2 };
            Assert.AreEqual(0, EntityPoolUtility.GetAvailableCount(in array, 99));
            array.Dispose();
        }
    }
}
