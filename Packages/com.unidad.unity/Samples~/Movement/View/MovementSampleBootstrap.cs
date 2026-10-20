using Unidad.Adapter.Stage;
using Unidad.Core.DI;
using Unidad.Flow;

namespace Unidad.Samples.Movement.View
{
    /// <summary>The Movement scene's bootstrap: everything it shows comes from <see cref="MovementHost"/>.</summary>
    public sealed class MovementSampleBootstrap : StageBootstrap
    {
        public static readonly SceneId SceneName = new("Movement");

        protected override SceneId Scene => SceneName;

        protected override void BuildStage(Container container, StageContext context) { }
    }
}
