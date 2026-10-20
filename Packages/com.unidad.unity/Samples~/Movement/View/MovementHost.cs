using Unidad.Adapter;
using Unidad.Adapter.Stage;
using Unidad.Adapter.Ui;
using Unidad.Core.Abstractions;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;
using Unidad.Core.Ticking;
using Unidad.Flow;
using UnityEngine;
using UnityEngine.UIElements;
using static Unidad.Samples.Movement.MovementTuning;

namespace Unidad.Samples.Movement.View
{
    /// <summary>Adds the Movement scene's host.</summary>
    public sealed class MovementViewInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder) =>
            builder.AddMany<IStageHost>(c => new MovementHost(c.Resolve<IMovementService>(), c.Resolve<ITickRegistry>()));
    }

    /// <summary>
    /// Spawns the level and draws it on an overlay document at <see cref="PanelOrder.Hud"/>: the two platforms and the box
    /// as rectangles, 48 px per unit from (480, 400), y flipped. The input bridge ticks in Input, the view in Presentation.
    /// </summary>
    internal sealed class MovementHost : IStageHost
    {
        private static readonly Color PlatformColor = new(0.3f, 0.35f, 0.42f);
        private static readonly Color BoxColor = new(0.95f, 0.55f, 0.15f);
        private const float PlatformThickness = 1f;

        private readonly IMovementService _movement;
        private readonly ITickRegistry _ticks;

        public MovementHost(IMovementService movement, ITickRegistry ticks)
        {
            _movement = movement;
            _ticks = ticks;
        }

        public int Order => 0;

        public bool AppliesTo(SceneId scene) => scene == MovementSampleBootstrap.SceneName;

        public void Bind(Container container, StageContext context)
        {
            _movement.SpawnLevel();
            var root = context.Panels.Overlay("Movement", PanelOrder.Hud).Root;
            root.Add(Rect(LeftPlatformMinX, PlatformTopY, LeftPlatformMaxX - LeftPlatformMinX, PlatformThickness, PlatformColor));
            root.Add(Rect(RightPlatformMinX, PlatformTopY, RightPlatformMaxX - RightPlatformMinX, PlatformThickness, PlatformColor));
            var box = Rect(-BoxHalf, BoxHalf, BoxHalf * 2f, BoxHalf * 2f, BoxColor);
            root.Add(box);

            _ticks.Register(new MovementInputBridge(_movement), TickGroup.Input);
            _ticks.Register(new MovementView(_movement, box), TickGroup.Presentation);
        }

        // A world rectangle by its top-left corner (y up) and size, in panel pixels.
        private static VisualElement Rect(float left, float top, float width, float height, Color color)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute;
            element.style.left = OriginX + left * PixelsPerUnit;
            element.style.top = OriginY - top * PixelsPerUnit;
            element.style.width = width * PixelsPerUnit;
            element.style.height = height * PixelsPerUnit;
            element.style.backgroundColor = color;
            return element;
        }
    }

    /// <summary>Mirrors the box's position each frame; owns no rules.</summary>
    internal sealed class MovementView : ITickable
    {
        private readonly IMovementService _movement;
        private readonly VisualElement _box;

        public MovementView(IMovementService movement, VisualElement box)
        {
            _movement = movement;
            _box = box;
        }

        public void Tick(float deltaTime)
        {
            var p = _movement.State.Position;
            _box.style.translate = new Translate(p.x * PixelsPerUnit, -p.y * PixelsPerUnit);
        }
    }
}
