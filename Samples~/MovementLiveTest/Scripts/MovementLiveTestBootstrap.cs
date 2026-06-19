using System.Collections.Generic;
using Reflex.Core;
using Unidad.Core.Abstractions;
using Unidad.Core.Bootstrap;
using Unidad.Core.LiveTesting;
using Unidad.Core.Physics2D;
using UnityEngine;

namespace Experimental.Movement
{
    /// <summary>
    /// Per-scene bootstrap for the Movement Live MCP Test. Installs Physics2D (required
    /// by MovementService) then Movement, drives the box via the deterministic
    /// FixedStep, and registers the live test so MCP tools + the editor panel can run it.
    ///
    /// Runs Physics2D in simulationMode = Script: the world only advances when the
    /// harness calls FixedStep (tickRunner.FixedTickAll → Physics2D.Simulate), making
    /// every assertion deterministic and synchronous.
    /// </summary>
    public sealed class MovementLiveTestBootstrap : UnidadBootstrap
    {
        protected override void RegisterInstallers(List<ISystemInstaller> installers)
        {
            installers.Add(new Physics2DSystemInstaller()); // MUST precede MovementInstaller
            installers.Add(new MovementInstaller());
        }

        protected override List<IFixedTickable> ResolveFixedTickables(Container container)
        {
            return new List<IFixedTickable> { (IFixedTickable)container.Resolve<IMovementService>() };
        }

        protected override void OnContainerReady(Container container)
        {
            UnityEngine.Physics2D.simulationMode = SimulationMode2D.Script;
            UnityEngine.Physics2D.queriesStartInColliders = false;

            var service = container.Resolve<IMovementService>();
            service.SpawnLevel();

            var tickRunner = GetComponentInChildren<TickRunner>();
            LiveTestRegistry.SetActive(new MovementLiveTestScene(service), dt =>
            {
                if (tickRunner != null) tickRunner.FixedTickAll(dt);
                UnityEngine.Physics2D.Simulate(dt);
            });

            Debug.Log("[MovementLiveTest] Ready — simulationMode=Script, live test 'movement' registered.");
        }

        private void OnDisable()
        {
            UnityEngine.Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
            LiveTestRegistry.Clear();
        }
    }
}
