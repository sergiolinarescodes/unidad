using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Unidad.Core.Rendering
{
    /// <summary>
    /// Managed service for GPU instanced rendering of ECS entities.
    ///
    /// Usage:
    /// 1. Register meshes/materials to get int IDs.
    /// 2. Add InstanceRenderable (with those IDs) + InstanceColor to ECS entities.
    /// 3. Each frame, call UploadToGPU() then iterate GetBatches() to draw.
    ///
    /// The InstanceGatherSystem (DOTS) automatically collects entity transforms and
    /// colors into NativeArrays grouped by mesh+material. This service wraps that data
    /// into ComputeBuffers ready for DrawMeshInstancedProcedural.
    /// </summary>
    public interface IRenderInstanceService : IDisposable
    {
        /// <summary>Register a mesh for instanced rendering. Returns a stable int ID.</summary>
        int RegisterMesh(Mesh mesh);

        /// <summary>Register a material for instanced rendering. Returns a stable int ID.</summary>
        int RegisterMaterial(Material material);

        /// <summary>Register a VAT animation clip. Returns a stable int ID. Prepared for future use.</summary>
        int RegisterAnimationClip(AnimationClipData clip);

        /// <summary>Get a registered mesh by ID.</summary>
        Mesh GetMesh(int meshId);

        /// <summary>Get a registered material by ID.</summary>
        Material GetMaterial(int materialId);

        /// <summary>Get a registered animation clip by ID.</summary>
        AnimationClipData GetAnimationClip(int clipId);

        /// <summary>
        /// Stage one batch of gathered instance data for GPU upload.
        /// Call once per batch (from InstanceGatherSystem.Batches), then call UploadToGPU().
        /// </summary>
        void SetBatchData(
            int meshId, int materialId,
            NativeArray<float4x4> matrices, NativeArray<float4> colors, int count,
            NativeArray<float4> animParams = default, bool hasAnimation = false);

        /// <summary>
        /// Upload all staged batch data to ComputeBuffers.
        /// Call once per frame after all SetBatchData calls.
        /// </summary>
        void UploadToGPU();

        /// <summary>
        /// Get the current frame's render batches. Each batch contains a mesh, material,
        /// ComputeBuffers, and instance count ready for DrawMeshInstancedProcedural.
        /// Call after UploadToGPU().
        /// </summary>
        RenderBatch[] GetBatches(out int count);
    }

    /// <summary>
    /// A single render batch: all instances sharing the same mesh and material.
    /// </summary>
    public struct RenderBatch
    {
        public int MeshId;
        public int MaterialId;
        public Mesh Mesh;
        public Material Material;
        public ComputeBuffer MatricesBuffer;
        public ComputeBuffer ColorsBuffer;
        public ComputeBuffer AnimParamsBuffer;
        public int Count;
        public bool HasAnimation;
    }

    /// <summary>
    /// Data for a baked Vertex Animation Texture (VAT) clip.
    /// Prepared for future use.
    /// </summary>
    public struct AnimationClipData
    {
        /// <summary>Texture encoding per-vertex positions for each animation frame.</summary>
        public Texture2D PositionTexture;

        /// <summary>Texture encoding per-vertex normals for each animation frame.</summary>
        public Texture2D NormalTexture;

        /// <summary>Total animation duration in seconds.</summary>
        public float Duration;

        /// <summary>Number of frames baked into the texture.</summary>
        public int FrameCount;

        /// <summary>Number of vertices in the animated mesh.</summary>
        public int VertexCount;
    }
}
