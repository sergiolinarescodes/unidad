using UnityEngine;

namespace Unidad.Core.Rendering
{
    /// <summary>
    /// Cached scene-renderer info consumed by the TexelSplatting GBuffer pass.
    /// One entry per <see cref="UnityEngine.MeshRenderer"/> whose material shader name
    /// starts with <c>TexelSplatting/GBuffer</c> (discovered by the texel driver). The
    /// GBuffer pass draws each cached renderer's mesh with its own material into the
    /// probe cube-array; <see cref="isStoneShader"/> flags meshes that also feed the
    /// CPU BVH for sun shadows, and <see cref="isGrassShader"/> flags grass meshes that
    /// are skipped for the eye probe to prevent shimmer.
    ///
    /// Field names intentionally match the self-contained struct the texel-splatting
    /// renderer source declared locally (cribbed byte-for-byte), so the copied
    /// <c>GBufferPass</c>/<c>TexelSplattingFeature</c> compile against this shared type
    /// without code changes.
    /// </summary>
    public struct CachedRenderer
    {
        public Renderer renderer;
        public MeshFilter meshFilter;
        public bool isStoneShader; // for BVH shadow construction
        public bool isGrassShader; // skip for eye probe to prevent shimmer
    }
}
