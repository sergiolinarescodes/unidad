using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Unidad.Core.Rendering
{
    /// <summary>
    /// Managed implementation of IRenderInstanceService.
    ///
    /// The service does NOT directly reference the DOTS assembly. Instead, the consumer
    /// (e.g., a MonoBehaviour controller) reads gathered NativeArrays from
    /// InstanceGatherSystem and passes them via <see cref="SetBatchData"/>.
    /// Then <see cref="UploadToGPU"/> copies to ComputeBuffers.
    /// </summary>
    public class RenderInstanceService : SystemServiceBase, IRenderInstanceService
    {
        readonly Dictionary<int, Mesh> _meshes = new();
        readonly Dictionary<int, Material> _materials = new();
        readonly Dictionary<int, AnimationClipData> _clips = new();

        int _nextMeshId = 1;
        int _nextMaterialId = 1;
        int _nextClipId = 1;

        // GPU buffers per batch (keyed by packed MeshId<<16|MaterialId)
        readonly Dictionary<int, BatchBuffers> _buffers = new();

        // Staging: data set by consumer before UploadToGPU
        readonly List<StagedBatch> _staged = new();

        // Output batches
        RenderBatch[] _batches = new RenderBatch[8];
        int _batchCount;

        public RenderInstanceService(IEventBus eventBus) : base(eventBus) { }

        public int RegisterMesh(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            int id = _nextMeshId++;
            _meshes[id] = mesh;
            return id;
        }

        public int RegisterMaterial(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            int id = _nextMaterialId++;
            _materials[id] = material;
            return id;
        }

        public int RegisterAnimationClip(AnimationClipData clip)
        {
            int id = _nextClipId++;
            _clips[id] = clip;
            return id;
        }

        public Mesh GetMesh(int meshId)
            => _meshes.TryGetValue(meshId, out var m) ? m : null;

        public Material GetMaterial(int materialId)
            => _materials.TryGetValue(materialId, out var m) ? m : null;

        public AnimationClipData GetAnimationClip(int clipId)
            => _clips.TryGetValue(clipId, out var c) ? c : default;

        /// <summary>
        /// Stage batch data for GPU upload. Call once per batch, then call UploadToGPU().
        /// This decouples the service from the DOTS assembly — the caller reads
        /// InstanceGatherSystem.Batches and passes the data here.
        /// </summary>
        public void SetBatchData(
            int meshId, int materialId,
            NativeArray<float4x4> matrices, NativeArray<float4> colors, int count,
            NativeArray<float4> animParams = default, bool hasAnimation = false)
        {
            _staged.Add(new StagedBatch
            {
                MeshId = meshId,
                MaterialId = materialId,
                Matrices = matrices,
                Colors = colors,
                AnimParams = animParams,
                Count = count,
                HasAnimation = hasAnimation
            });
        }

        public void UploadToGPU()
        {
            int stagedCount = _staged.Count;

            if (stagedCount == 0)
            {
                _batchCount = 0;
                return;
            }

            if (_batches.Length < stagedCount)
                _batches = new RenderBatch[math.max(stagedCount, _batches.Length * 2)];

            _batchCount = stagedCount;

            for (int i = 0; i < stagedCount; i++)
            {
                var s = _staged[i];
                if (s.Count == 0)
                {
                    _batches[i] = default;
                    continue;
                }

                int key = PackKey(s.MeshId, s.MaterialId);

                if (!_buffers.TryGetValue(key, out var buf))
                {
                    buf = new BatchBuffers();
                    _buffers[key] = buf;
                }

                buf.EnsureCapacity(s.Count);
                buf.Matrices.SetData(s.Matrices, 0, 0, s.Count);
                buf.Colors.SetData(s.Colors, 0, 0, s.Count);

                bool hasAnim = s.HasAnimation && s.AnimParams.IsCreated;
                if (hasAnim)
                {
                    buf.EnsureAnimCapacity(s.Count);
                    buf.AnimParams.SetData(s.AnimParams, 0, 0, s.Count);
                }

                _batches[i] = new RenderBatch
                {
                    MeshId = s.MeshId,
                    MaterialId = s.MaterialId,
                    Mesh = GetMesh(s.MeshId),
                    Material = GetMaterial(s.MaterialId),
                    MatricesBuffer = buf.Matrices,
                    ColorsBuffer = buf.Colors,
                    AnimParamsBuffer = hasAnim ? buf.AnimParams : null,
                    Count = s.Count,
                    HasAnimation = hasAnim,
                    CastShadows = true
                };
            }

            _staged.Clear();
        }

        public RenderBatch[] GetBatches(out int count)
        {
            count = _batchCount;
            return _batches;
        }

        static int PackKey(int meshId, int materialId)
            => (meshId << 16) | (materialId & 0xFFFF);

        public override void Dispose()
        {
            base.Dispose();

            foreach (var buf in _buffers.Values)
                buf.Dispose();
            _buffers.Clear();

            _staged.Clear();
            _batchCount = 0;
        }

        struct StagedBatch
        {
            public int MeshId;
            public int MaterialId;
            public NativeArray<float4x4> Matrices;
            public NativeArray<float4> Colors;
            public NativeArray<float4> AnimParams;
            public int Count;
            public bool HasAnimation;
        }

        class BatchBuffers
        {
            public ComputeBuffer Matrices;
            public ComputeBuffer Colors;
            public ComputeBuffer AnimParams;
            int _capacity;
            int _animCapacity;

            public void EnsureCapacity(int needed)
            {
                if (_capacity >= needed) return;
                int newCap = math.max(needed, math.max(_capacity * 2, 64));
                Matrices?.Dispose();
                Colors?.Dispose();
                Matrices = new ComputeBuffer(newCap, 64); // float4x4
                Colors = new ComputeBuffer(newCap, 16);   // float4
                _capacity = newCap;
            }

            public void EnsureAnimCapacity(int needed)
            {
                if (_animCapacity >= needed) return;
                int newCap = math.max(needed, math.max(_animCapacity * 2, 64));
                AnimParams?.Dispose();
                AnimParams = new ComputeBuffer(newCap, 16);
                _animCapacity = newCap;
            }

            public void Dispose()
            {
                Matrices?.Dispose();
                Colors?.Dispose();
                AnimParams?.Dispose();
                Matrices = null;
                Colors = null;
                AnimParams = null;
                _capacity = 0;
                _animCapacity = 0;
            }
        }
    }
}
