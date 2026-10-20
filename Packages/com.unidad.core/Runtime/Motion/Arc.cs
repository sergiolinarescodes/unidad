using System;
using System.Numerics;

namespace Unidad.Core.Motion
{
    /// <summary>A thrown object's path: the eased chord plus a parabola of <c>4·t·(1-t)·height</c> on linear t. +Y is up.</summary>
    public static class Arc
    {
        public static Vector3 Sample(Vector3 from, Vector3 to, float height, float t, Ease ease = Ease.OutCubic) => throw new NotImplementedException();
    }
}
