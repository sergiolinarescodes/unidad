using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Gathers per-instance rendering data (transforms, colors, animation params)
    /// into contiguous NativeArrays grouped by InstanceRenderable (MeshId + MaterialId).
    ///
    /// Runs in PresentationSystemGroup so all simulation writes are visible.
    /// Uses SystemBase because ISharedComponentData iteration requires managed APIs
    /// and the output must be accessible from the managed world.
    ///
    /// Consumers read batches via the static <see cref="Batches"/> and <see cref="BatchCount"/> fields,
    /// or through <see cref="IRenderInstanceService"/> which wraps this data.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class InstanceGatherSystem : SystemBase
    {
        /// <summary>Current frame's gathered batch data. Read by IRenderInstanceService.</summary>
        public static GatheredBatch[] Batches;

        /// <summary>Number of valid entries in <see cref="Batches"/>.</summary>
        public static int BatchCount;

        EntityQuery _renderableQuery;
        EntityQuery _colorQuery;
        EntityQuery _animQuery;

        protected override void OnCreate()
        {
            _renderableQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<InstanceRenderable, LocalToWorld>()
                .Build(this);

            _colorQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<InstanceRenderable, LocalToWorld, InstanceColor>()
                .Build(this);

            _animQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<InstanceRenderable, LocalToWorld, InstanceAnimation>()
                .Build(this);

            RequireForUpdate(_renderableQuery);
        }

        protected override void OnUpdate()
        {
            // Collect unique shared component values
            EntityManager.GetAllUniqueSharedComponents(out NativeList<InstanceRenderable> uniqueList,
                Allocator.Temp);

            // Count valid (non-default) renderables
            int batchCount = 0;
            for (int i = 0; i < uniqueList.Length; i++)
            {
                var r = uniqueList[i];
                if (r.MeshId != 0 || r.MaterialId != 0)
                    batchCount++;
            }

            // Ensure static array is large enough
            if (Batches == null || Batches.Length < batchCount)
                Batches = new GatheredBatch[math.max(batchCount, 8)];

            int batchIdx = 0;
            for (int u = 0; u < uniqueList.Length; u++)
            {
                var renderable = uniqueList[u];
                if (renderable.MeshId == 0 && renderable.MaterialId == 0)
                    continue;

                // Query entities for this specific mesh+material combo
                _renderableQuery.SetSharedComponentFilter(renderable);
                int entityCount = _renderableQuery.CalculateEntityCount();

                if (entityCount == 0)
                {
                    DisposeBatch(batchIdx);
                    Batches[batchIdx] = new GatheredBatch
                    {
                        Renderable = renderable,
                        Count = 0
                    };
                    batchIdx++;
                    continue;
                }

                // Allocate output arrays
                var matrices = new NativeArray<float4x4>(entityCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                var colors = new NativeArray<float4>(entityCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);

                // Check if all entities in this batch have InstanceColor
                _colorQuery.SetSharedComponentFilter(renderable);
                int colorCount = _colorQuery.CalculateEntityCount();

                if (colorCount == entityCount)
                {
                    // All entities have color — use the combined job
                    Dependency = new GatherTransformsAndColorsJob
                    {
                        Matrices = matrices,
                        Colors = colors
                    }.Schedule(_colorQuery, Dependency);
                    Dependency.Complete();
                }
                else
                {
                    // Gather transforms with default white for all
                    Dependency = new GatherTransformsJob
                    {
                        Matrices = matrices,
                        Colors = colors,
                        DefaultColor = new float4(1f, 1f, 1f, 1f)
                    }.Schedule(_renderableQuery, Dependency);
                    Dependency.Complete();
                }

                // Check for animation data
                NativeArray<float4> animParams = default;
                bool hasAnimation = false;

                _animQuery.SetSharedComponentFilter(renderable);
                int animCount = _animQuery.CalculateEntityCount();

                if (animCount > 0 && animCount == entityCount)
                {
                    hasAnimation = true;
                    animParams = new NativeArray<float4>(entityCount, Allocator.TempJob,
                        NativeArrayOptions.UninitializedMemory);

                    Dependency = new GatherAnimParamsJob
                    {
                        AnimParams = animParams
                    }.Schedule(_animQuery, Dependency);
                    Dependency.Complete();
                }

                // Dispose previous frame's arrays for this batch slot
                DisposeBatch(batchIdx);

                Batches[batchIdx] = new GatheredBatch
                {
                    Renderable = renderable,
                    Matrices = matrices,
                    Colors = colors,
                    AnimParams = animParams,
                    Count = entityCount,
                    HasAnimation = hasAnimation
                };
                batchIdx++;
            }

            // Dispose any leftover batches from previous frame beyond current count
            if (Batches != null)
            {
                for (int i = batchIdx; i < Batches.Length; i++)
                    DisposeBatch(i);
            }

            BatchCount = batchCount;

            // Reset filters
            _renderableQuery.ResetFilter();
            _colorQuery.ResetFilter();
            _animQuery.ResetFilter();

            uniqueList.Dispose();
        }

        void DisposeBatch(int index)
        {
            if (Batches == null || index >= Batches.Length) return;
            ref var b = ref Batches[index];
            if (b.Matrices.IsCreated) b.Matrices.Dispose();
            if (b.Colors.IsCreated) b.Colors.Dispose();
            if (b.AnimParams.IsCreated) b.AnimParams.Dispose();
            b = default;
        }

        protected override void OnDestroy()
        {
            if (Batches != null)
            {
                for (int i = 0; i < Batches.Length; i++)
                    DisposeBatch(i);

                Batches = null;
                BatchCount = 0;
            }
        }
    }

    /// <summary>
    /// One batch of gathered instance data, ready for GPU upload.
    /// </summary>
    public struct GatheredBatch
    {
        public InstanceRenderable Renderable;
        public NativeArray<float4x4> Matrices;
        public NativeArray<float4> Colors;
        public NativeArray<float4> AnimParams;
        public int Count;
        public bool HasAnimation;
    }
}
