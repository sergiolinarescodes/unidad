using Unidad.Core.DOTS;

namespace Unidad.Core.Rendering
{
    /// <summary>
    /// Bridges the InstanceGatherSystem output to IRenderInstanceService.
    /// Reads gathered NativeArrays and uploads to GPU ComputeBuffers in one call.
    /// </summary>
    public static class RenderBridge
    {
        /// <summary>
        /// Read all gathered batches from InstanceGatherSystem and upload to the render service.
        /// Call once per frame in the consumer's Update, after ECS systems have run.
        /// </summary>
        public static void SyncToService(IRenderInstanceService service)
        {
            var batches = InstanceGatherSystem.Batches;
            int count = InstanceGatherSystem.BatchCount;

            if (batches == null || count == 0)
            {
                service.UploadToGPU();
                return;
            }

            for (int i = 0; i < count; i++)
            {
                ref var batch = ref batches[i];
                if (batch.Count <= 0) continue;
                if (!batch.Matrices.IsCreated || !batch.Colors.IsCreated) continue;

                service.SetBatchData(
                    batch.Renderable.MeshId,
                    batch.Renderable.MaterialId,
                    batch.Matrices,
                    batch.Colors,
                    batch.Count,
                    batch.HasAnimation && batch.AnimParams.IsCreated ? batch.AnimParams : default,
                    batch.HasAnimation);
            }

            service.UploadToGPU();
        }
    }
}
