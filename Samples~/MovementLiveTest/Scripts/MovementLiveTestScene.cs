using Unidad.Core.LiveTesting;

namespace Experimental.Movement
{
    /// <summary>
    /// The Live MCP Test for the 2D Movement feature: actions (MCP- and button-invokable),
    /// numeric/bool probes, and a scripted 5-case Plan. A thin adapter over
    /// <see cref="IMovementService"/> — no game logic lives here.
    ///
    /// Initial force/step values are a starting point; they are tuned live by running
    /// Unidad.LiveTest.RunPlan and reading probes — that tuning loop IS the workflow.
    /// </summary>
    [LiveTestScene("movement", "2D Movement", "Assets/Samples/Unidad Core/0.1.0/Movement Live MCP Test/MovementLiveTest.unity")]
    internal sealed class MovementLiveTestScene : LiveTestSceneBase
    {
        private readonly IMovementService _svc;

        public override string Id => "movement";
        public override string Name => "2D Movement";
        public override string ScenePath => "Assets/Samples/Unidad Core/0.1.0/Movement Live MCP Test/MovementLiveTest.unity";

        public MovementLiveTestScene(IMovementService svc)
        {
            _svc = svc;

            // ---- actions (the buttons) ----
            Action("respawn", "Respawn Level", _ => _svc.SpawnLevel());
            Action("jump", "Jump", a => _svc.Jump((float)a.Get("force", 12f)));
            Action("moveLeft", "Move Left", a => _svc.MoveLeft((float)a.Get("speed", 6f)));
            Action("moveRight", "Move Right", a => _svc.MoveRight((float)a.Get("speed", 6f)));
            Action("stop", "Stop", _ => _svc.StopHorizontal());
            Action("moveJump", "Move + Jump", a =>
            {
                _svc.MoveRight((float)a.Get("speed", 6f));
                _svc.Jump((float)a.Get("force", 12f));
            });

            // ---- probes (the asserted values) ----
            Probe("position.x", () => LiveTestValue.Of(_svc.State.Position.x));
            Probe("position.y", () => LiveTestValue.Of(_svc.State.Position.y));
            Probe("velocity.x", () => LiveTestValue.Of(_svc.State.Velocity.x));
            Probe("velocity.y", () => LiveTestValue.Of(_svc.State.Velocity.y));
            Probe("isGrounded", () => LiveTestValue.Of(_svc.State.IsGrounded));
            Probe("fellOff", () => LiveTestValue.Of(_svc.State.FellOff));
            Probe("isSettled", () => LiveTestValue.Of(_svc.State.IsGrounded && _svc.State.Velocity.magnitude < 0.05f));
            Probe("onRightPlatform", () => LiveTestValue.Of(
                _svc.State.IsGrounded && _svc.State.Position.x > 2f && !_svc.State.FellOff));

            BuildPlan();
        }

        private void BuildPlan()
        {
            // Case 1 — the box jumps properly.
            Step("c1: reset", "respawn", null, 10, null,
                Check("grounded after spawn", "isGrounded", LiveTestOp.IsTrue),
                Check("rests on platform", "position.y", LiveTestOp.ApproxEq, 1.0, 0.25));
            Step("c1: jump", "jump", Args(("force", 12)), 3, null,
                Check("moving up after jump", "velocity.y", LiveTestOp.Gt, 3),
                Check("rose above start", "position.y", LiveTestOp.Gt, 1.05));
            Step("c1: land", null, null, 0, Until("isGrounded", LiveTestOp.IsTrue, 1, 400),
                Check("landed grounded", "isGrounded", LiveTestOp.IsTrue),
                Check("landed near start height", "position.y", LiveTestOp.ApproxEq, 1.0, 0.3),
                Check("did not fall off", "fellOff", LiveTestOp.IsFalse));

            // Case 2 — moves left and right.
            Step("c2: reset", "respawn", null, 10, null,
                Check("grounded", "isGrounded", LiveTestOp.IsTrue));
            Step("c2: right", "moveRight", Args(("speed", 6)), 15, null,
                Check("moved right", "position.x", LiveTestOp.Gt, -3.5),
                Check("velocity rightward", "velocity.x", LiveTestOp.Gt, 0));
            Step("c2: stop", "stop", null, 3, null,
                Check("stopped", "velocity.x", LiveTestOp.ApproxEq, 0, 0.5));
            Step("c2: left", "moveLeft", Args(("speed", 6)), 25, null,
                Check("velocity leftward", "velocity.x", LiveTestOp.Lt, 0),
                Check("moved back left", "position.x", LiveTestOp.Lt, -3));

            // Case 3 — moves and jumps at the same time.
            Step("c3: reset", "respawn", null, 10, null,
                Check("grounded", "isGrounded", LiveTestOp.IsTrue));
            Step("c3: moveJump", "moveJump", Args(("speed", 6), ("force", 12)), 6, null,
                Check("rising", "velocity.y", LiveTestOp.Gt, 3),
                Check("moving right", "velocity.x", LiveTestOp.Gt, 0),
                Check("advanced right", "position.x", LiveTestOp.Gt, -4));

            // Case 4 — runs and jumps over the hole, lands on the far platform.
            Step("c4: reset", "respawn", null, 10, null,
                Check("grounded", "isGrounded", LiveTestOp.IsTrue));
            Step("c4: run", "moveRight", Args(("speed", 6)), 5, null);
            Step("c4: jump", "jump", Args(("force", 7)), 0, null);
            Step("c4: clear", null, null, 0, Until("onRightPlatform", LiveTestOp.IsTrue, 1, 500),
                Check("landed on right platform", "onRightPlatform", LiveTestOp.IsTrue),
                Check("did not fall off", "fellOff", LiveTestOp.IsFalse),
                Check("past the hole", "position.x", LiveTestOp.Gt, 2));

            // Case 5 — UNHAPPY: walks into the hole and falls off.
            Step("c5: reset", "respawn", null, 10, null,
                Check("grounded", "isGrounded", LiveTestOp.IsTrue));
            Step("c5: walk into hole", "moveRight", Args(("speed", 3)), 0,
                Until("fellOff", LiveTestOp.IsTrue, 1, 300),
                Check("fell off", "fellOff", LiveTestOp.IsTrue),
                Check("below threshold", "position.y", LiveTestOp.Lt, -3));
        }
    }
}
