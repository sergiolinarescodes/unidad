# Unidad

Unity framework core library. Opinionated, Reflex-DI based building blocks for game-side service code:

- **EventBus** — strongly-typed publish/subscribe over value-type event structs
- **SystemServiceBase** — service base class with automatic subscription lifecycle
- **Reflex DI integration** — installer pattern via `ISystemInstaller`
- **ITickable** — explicit per-frame stepping (no `MonoBehaviour.Update` soup)
- **IGameObjectFactory** + `GameObjectPool<T>` + `PoolRegistry` — pooled instantiation
- **ModifierStack** + `IModifier<TValue>` — stackable stat/value modifiers
- **RegistryBase<TKey, TValue>** — typed entity registries
- **IState / StateMachine** — phase/turn state machines
- **IContributor** — scorer-based decision authoring (used for AI behavior)
- **Grid utilities** — coordinate maths, validation, iteration helpers
- **Scenario test harness** — `DataDrivenScenario`, `TestScenarioDefinition`, `ManualTimeProvider`, `InstantAnimationResolver`, `MockEventBus`, `TestEventBus`
- **Debug providers** — `IDebugProvider` + `DebugModeService` for in-Editor inspection
- **Bootstrap** — `UnidadBootstrap` MonoBehaviour spawns `TickRunner` and wires the Reflex container

## Installation

### As a Unity Package Manager (UPM) git dependency

Add to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unidad.core": "https://github.com/sergiolinarescodes/unidad.git"
  }
}
```

### As an embedded local package (recommended for active development)

Clone into your project's `Packages/` directory and reference by path:

```bash
cd <your-unity-project>/Packages/
git clone https://github.com/sergiolinarescodes/unidad.git com.unidad.core
```

Then in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unidad.core": "file:com.unidad.core"
  }
}
```

### As a git submodule (for syncing across multiple projects)

```bash
git submodule add https://github.com/sergiolinarescodes/unidad.git Packages/com.unidad.core
```

## Quick Start

```csharp
public sealed class GameBootstrap : UnidadBootstrap
{
    protected override void RegisterInstallers(ContainerBuilder builder)
    {
        builder.AddSystemInstaller(new YourSystemInstaller());
    }
}
```

```csharp
public interface IYourService { void DoThing(); }

internal sealed class YourService : SystemServiceBase, IYourService
{
    public YourService(IEventBus bus, ITimeProvider time) : base(bus) { }

    public void DoThing() => Publish(new ThingHappenedEvent());
}

public readonly record struct ThingHappenedEvent;

public sealed class YourSystemInstaller : ISystemInstaller
{
    public void Install(ContainerBuilder builder)
    {
        builder.AddSingleton<IYourService, YourService>();
    }

    public ISystemTestFactory CreateTestFactory() => new YourTestFactory();
}
```

## Requirements

- Unity 6.0 LTS (6000.0+)
- Reflex DI (`com.gustavopsantos.reflex`)

## Used By

- [unity-ppo-racing-trainer](https://github.com/sergiolinarescodes/unity-ppo-racing-trainer) — open-source PPO car racing agents in Unity ML-Agents (real-world consumer reference)

## License

MIT — see [LICENSE](LICENSE).
