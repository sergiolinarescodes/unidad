using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Copies LocalToWorld matrices for entities that have NO InstanceColor.
    /// Colors default to white.
    /// </summary>
    [BurstCompile]
    public partial struct GatherTransformsJob : IJobEntity
    {
        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> Matrices;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float4> Colors;

        public float4 DefaultColor;

        void Execute([EntityIndexInQuery] int index, RefRO<LocalToWorld> ltw)
        {
            Matrices[index] = ltw.ValueRO.Value;
            Colors[index] = DefaultColor;
        }
    }

    /// <summary>
    /// Copies LocalToWorld matrices and InstanceColor values for entities
    /// that have both components.
    /// </summary>
    [BurstCompile]
    public partial struct GatherTransformsAndColorsJob : IJobEntity
    {
        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> Matrices;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float4> Colors;

        void Execute([EntityIndexInQuery] int index, RefRO<LocalToWorld> ltw, RefRO<InstanceColor> color)
        {
            Matrices[index] = ltw.ValueRO.Value;
            Colors[index] = color.ValueRO.Value;
        }
    }

    /// <summary>
    /// Copies InstanceAnimation parameters into a packed float4 array.
    /// x = time + phaseOffset, y = speed, z = clipId (as float), w = reserved.
    /// Prepared for future VAT support.
    /// </summary>
    [BurstCompile]
    public partial struct GatherAnimParamsJob : IJobEntity
    {
        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float4> AnimParams;

        void Execute([EntityIndexInQuery] int index, RefRO<InstanceAnimation> anim)
        {
            AnimParams[index] = new float4(
                anim.ValueRO.Time + anim.ValueRO.PhaseOffset,
                anim.ValueRO.Speed,
                anim.ValueRO.ClipId,
                0f);
        }
    }
}
