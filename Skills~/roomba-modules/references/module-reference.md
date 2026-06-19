# Module Reference

## Active Modules

| ID | Display | Energy/Turn | ActionOutput | Unlocks | Default Script |
|----|---------|-------------|--------------|---------|----------------|
| `thruster` | Thruster | 5.0 | `direction` | `forward()`, `backward()` | `direction = "forward"` |
| `gyroscope` | Gyroscope | 3.0 | `rotation` | `rotateLeft()`, `rotateRight()` | `rotation = "left"` |
| `vacuum` | Vacuum | 4.0 | `clean` | `vacuum()` | `clean = True` |
| `teleporter` | Teleporter | 6.0 | `target` | `teleport()` | `target = "2,2"` |
| `charger` | Charger | 2.0 | `charge` | `recharge()` | `charge = True` |
| `connector` | Connector | 1.0 | `status` | — | `status = "relay"` |

## Passive Modules

| ID | Display | Unlocks |
|----|---------|---------|
| `scanner` | Scanner | `scan()`, `peek()` |
| `gps` | GPS | `position()` |

## Assembly-Only Modules

| ID | Display | Energy/Turn | Role |
|----|---------|-------------|------|
| `core` | Core | 2.0 | Central hub. Conductor. 3 outputs (N/E/W). |
| `battery` | Battery | 0.0 | Power source. Conductor. Editor-locked. |

## Face Layouts (default orientation)

| Module | North | East | South | West |
|--------|-------|------|-------|------|
| Core | Output | Output | Input | Output |
| Battery | Output | None | None | None |
| Thruster | Output | None | Input | None |
| Gyroscope | Output | None | Input | None |
| Vacuum | Output | None | Input | None |
| Teleporter | Output | Output | Input | None |
| Charger | None | None | Input | None |
| Scanner | None | None | Input | None |
| GPS | None | None | Input | None |
| Connector | Output | Output | Input | Output |

## Rotation Transform

`RotateCW()` shifts faces: West->North, North->East, East->South, South->West.

### Thruster example (N=Out, E=None, S=In, W=None)

| Steps | N | E | S | W | Input faces |
|-------|---|---|---|---|-------------|
| 0 | Out | None | In | None | South |
| 1 | None | Out | None | In | West |
| 2 | In | None | Out | None | North |
| 3 | None | In | None | Out | East |

## Native Functions

| Function | Arity | Returns | Energy Cost | Unlocked By |
|----------|-------|---------|-------------|-------------|
| `forward()` | 0 | Bool | config.MoveCost | Thruster |
| `backward()` | 0 | Bool | config.MoveCost | Thruster |
| `rotateLeft()` | 0 | Bool | 0.5 | Gyroscope |
| `rotateRight()` | 0 | Bool | 0.5 | Gyroscope |
| `vacuum()` | 0 | Bool | config.VacuumCost | Vacuum |
| `scan()` | 0 | List | 1.0 | Scanner |
| `peek()` | 0 | String | 0.5 | Scanner |
| `position()` | 0 | String | 0 | GPS |
| `battery()` | 0 | Number | 0 | Built-in |
| `area()` | 0 | Number | 0 | Built-in |
| `turn()` | 0 | Number | 0 | Built-in |
| `recharge()` | 0 | Number | 2.0 | Charger |
| `teleport(dir)` | 1 | Bool | config.MoveCost * 2 | Teleporter |

### peek() — simple wall detection

Returns a string for the cell immediately ahead of the roomba:
- `"wall"` — next cell is out of bounds
- `"none"` — empty cell
- `"dust"` — dirt cell

Preferred over `scan()` for simple scripts — returns a plain string, not a list.

### turn() — current turn number

Returns the current 1-based turn number. Zero energy cost, always available.

### scan() details

Returns a **list** of `[x, y, entityName]` entries for up to 2 tiles ahead. NOT a simple string — use `peek()` for simple comparisons.

### Function availability

- Passive modules unlock functions **globally** for all scripts
- Active modules unlock functions only for their own script
- `battery()`, `area()`, and `turn()` are always available

## Energy Propagation

BFS waterfall from battery through conductors:

1. Battery consumes 0, forwards all energy
2. Each conductor consumes `min(maxEnergy, supply)`, forwards remainder
3. Multiple children: sorted by subtree demand, distributed fairly
4. Non-conductors consume and **block** — children get nothing

Conductor modules: `battery`, `core`, `connector` (and any auto-instance clones like `core_2`).

## Auto-Instance IDs

Presets use base module IDs. `PlaygroundPresets.Resolve()` auto-assigns unique instance IDs for duplicates:
- First occurrence: `instanceId = baseId` (e.g., `"thruster"`)
- Subsequent: `instanceId = "{baseId}_{n}"` (e.g., `"thruster_2"`)
- Cloned definitions and configs are created for instances with `n > 1`
- Conductor base IDs propagate to all instances

## Key Files

| What | Path |
|------|------|
| Module definitions | `Assets/Scripts/RoombaGame/RoombaModuleDefinition.cs` |
| Face layouts + configs | `Assets/Scripts/RoombaGame/Assembly/AssemblyModuleConfig.cs` |
| Face layout record | `Assets/Scripts/RoombaGame/Assembly/ModuleFaceLayout.cs` |
| Pipeline + energy | `Assets/Scripts/RoombaGame/ModulePipeline.cs` |
| Playground service | `Assets/Scripts/RoombaGame/RoombaPlaygroundService.cs` |
| Playground interface | `Assets/Scripts/RoombaGame/IRoombaPlaygroundService.cs` |
| Action resolver | `Assets/Scripts/RoombaGame/ModuleActionResolver.cs` |
| Presets | `Assets/Scripts/RoombaGame/PlaygroundPresets.cs` |
| Constants | `Assets/Scripts/RoombaGame/RoombaModuleConstants.cs` |
| Game events | `Assets/Scripts/RoombaGame/RoombaGameEvents.cs` |
| Rotation math | `Assets/Scripts/RoombaGame/RoombaRotation.cs` |
| Assembly grid | `Assets/Scripts/RoombaGame/Assembly/AssemblyGrid.cs` |
| Scenarios | `Assets/Scripts/RoombaGame/Scenarios/*.cs` |
| Tests | `Assets/Tests/Core/RoombaGame/RoombaPlaygroundServiceTests.cs` |
| Native function tests | `Assets/Tests/Core/RoombaGame/NativeFunctionTests.cs` |
