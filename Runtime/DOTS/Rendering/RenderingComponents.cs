using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Add to any entity with LocalToWorld to opt into GPU instanced rendering.
    /// Entities with the same MeshId+MaterialId are automatically batched into a
    /// single DrawMeshInstancedProcedural call.
    ///
    /// Register meshes and materials via IRenderInstanceService to obtain IDs.
    /// </summary>
    public struct InstanceRenderable : ISharedComponentData, IEquatable<InstanceRenderable>
    {
        public int MeshId;
        public int MaterialId;

        public bool Equals(InstanceRenderable other)
            => MeshId == other.MeshId && MaterialId == other.MaterialId;

        public override int GetHashCode()
            => MeshId * 397 ^ MaterialId;
    }

    /// <summary>
    /// Optional per-instance color. When present, the value is uploaded to the
    /// GPU StructuredBuffer and available as _InstanceColors[SV_InstanceID] in shaders.
    /// If absent on an entity with InstanceRenderable, defaults to white (1,1,1,1).
    /// </summary>
    public struct InstanceColor : IComponentData
    {
        public float4 Value;
    }

    /// <summary>
    /// Optional: Vertex Animation Texture (VAT) playback state.
    /// When present, the gather system includes animation parameters in the batch data
    /// and the shader samples vertex positions from a baked animation texture.
    ///
    /// Prepared for future use — the shader feature is compiled out when no entities
    /// in a batch have this component.
    /// </summary>
    public struct InstanceAnimation : IComponentData
    {
        /// <summary>Registered VAT clip ID (via IRenderInstanceService).</summary>
        public int ClipId;

        /// <summary>Current playback time in seconds.</summary>
        public float Time;

        /// <summary>Playback speed multiplier (1.0 = normal).</summary>
        public float Speed;

        /// <summary>Per-instance phase offset for variation.</summary>
        public float PhaseOffset;
    }

    /// <summary>
    /// 1-frame event: render batch data was updated this frame.
    /// Enabled by InstanceGatherSystem after gathering instance data.
    /// </summary>
    public struct RenderBatchUpdated : IComponentData, IEnableableComponent { }
}
