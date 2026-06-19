---
name: roomba-modules
description: >
  Builds modular robots for the RoombaGame system — assembling module trees,
  wiring energy pipelines, writing module scripts, and creating presets/scenarios.
  Use when user asks to "build a robot", "add a module", "create a preset",
  "write a scenario", or works on files in Assets/Scripts/RoombaGame/.
---

# Roomba Module Assembly

## Instructions

### Step 1: Understand the Module Tree

A robot is a tree of modules on an assembly grid, powered by a battery. Each turn the pipeline walks the tree via BFS, runs scripts, and resolves outputs into world actions.

```
Battery → Core → [child modules...]
```

- **Battery** generates energy, always placed below Core
- **Core** distributes energy to up to 3 children (N/E/W outputs)
- **Connector** distributes energy to up to 3 children (N/E/W outputs) — like Core but not the root
- **Child modules** consume energy and produce actions

Consult `references/module-reference.md` for the full module table, face layouts, and energy costs.

### Step 2: Wire Connections

Connections are directional: a module's **Output** face must touch an adjacent module's **Input** face.

```csharp
service.AddConnection("battery", "core");   // always first
service.AddConnection("core", "thruster");  // core → child
```

Most modules have `S:Input` only — they go **above** their parent. Use **rotation** for lateral placement:

| Rotation Steps | Input Moves To | Use When |
|---------------|---------------|----------|
| 0 | South | Module above parent |
| 1 CW | West | Module right of parent |
| 3 CW | East | Module left of parent |

### Step 3: Handle Multiple Cores / Connectors

When a robot needs more than 3 modules from a single hub, use a **Connector** or a second **Core** as an energy relay.

**Connector module**: `"connector"` — an energy relay with the same face layout as Core (N/E/W outputs, S input). Add it to `ConductorModuleIds`.

**Auto-instance IDs**: Use `PlaygroundPresets.Resolve()` to auto-assign unique instance IDs for duplicate modules. First occurrence keeps the base ID; subsequent get `_2`, `_3`, etc.

```csharp
// Two thrusters auto-resolve to "thruster" and "thruster_2"
new PresetModulePlacement("thruster", pos1, "direction = \"forward\""),
new PresetModulePlacement("thruster", pos2, "direction = \"forward\"", RotationSteps: 1),
```

Requirements for conductors:
1. Add to `ConductorModuleIds` (or `ConductorBaseIds` in preset definition)
2. Wire as a child: `service.AddConnection("core", "connector")`
3. Connector children wire to it: `service.AddConnection("connector", "thruster")`

### Step 4: Write Module Scripts

Python-like syntax. The **ActionOutput variable** must be assigned at top level.

```python
# CORRECT — top-level default, conditional override
direction = "forward"
tile = peek()
if tile == "wall":
    direction = "backward"
```

```python
# WRONG — assignment only inside if, output not discovered
tile = peek()
if tile == "wall":
    direction = "backward"
```

Use `peek()` for simple wall detection (returns `"wall"`, `"none"`, or `"dust"`). Use `turn()` for turn-based logic.

Consult `references/action-resolution.md` for the full action table and script rules.

### Step 5: Apply Presets (if applicable)

Presets bundle module placements with positions, scripts, and rotation. Use `PlaygroundPresets.Resolve()` to get auto-instance IDs:

```csharp
// 1. Resolve preset with already-placed modules
var alreadyPlaced = new HashSet<string> { "battery", "core" };
var resolved = PlaygroundPresets.Resolve(
    preset, gridSize, baseDefs, coreConfig, conductors, alreadyPlaced);

// 2. Add additional defs/configs/conductors before SetupGame
allDefs.AddRange(resolved.AdditionalDefinitions);
moduleConfigs.AddRange(resolved.AdditionalConfigs);
foreach (var id in resolved.AdditionalConductors)
    conductors.Add(id);

// 3. SetupGame with complete allDefs
service.SetupGame(config);

// 4. Apply resolved placements
foreach (var p in resolved.Placements)
{
    service.TrackAttachment(p.InstanceId);
    if (p.Script != null)
        service.SetModuleScript(p.InstanceId, p.Script);
}
```

### Step 6: Battery Drain

Battery drains each turn by the **sum of all module energy budgets**. Game over at 0. Default is 100.

```csharp
// In ExecuteTurn(), after ResolveActions:
var totalDrain = result.EnergyBudgets.Values.Sum();
_batteryEnergy = Math.Max(0, _batteryEnergy - (int)totalDrain);
```

## Examples

### Example 1: WallFollower Preset (reference layout)

User says: "Create a wall-following cleaner robot"

Layout with two cores and 6 modules (auto-instance: core → core_2):
```
               Gyroscope (c, c+2)
Vacuum (c-1, c+1)  Core_2 (c, c+1)  Thruster (c+1, c+1)
Scanner (c-1, c)    Core (c, c)      GPS (c+1, c)
                    Battery (c, c-1)
```

| Module | Rotation | Script |
|--------|----------|--------|
| core (→core_2) | 0 | default |
| gyroscope | 0 | `rotation = "none"\ntile = peek()\nif tile == "wall":\n    rotation = "right"` |
| thruster | 1 CW | `direction = "forward"` |
| vacuum | 3 CW | `clean = True` |

Conductors: `{ "battery", "core", "core_2" }`

### Example 2: SpeedBot (duplicate modules)

Two thrusters = two moves per turn. Uses connector for the second thruster branch.

```
Thruster (c, c+2)              ← "thruster"
Connector (c, c+1)             Thruster (c+1, c+1)  ← "thruster_2" (CW 1×)
Vacuum (c-1, c)   Core (c, c)  Scanner (c+1, c)
                   Battery (c, c-1)
```

Conductors: `{ "battery", "core", "connector" }`

### Example 3: Simple test setup

User says: "Add a test for thruster movement"

```csharp
SetupWithCoreAndBattery(CreateConfig(batteryEnergy: 100));
PlaceActiveModule("thruster", "direction = \"forward\"");

var result = _service.ExecuteTurn();

Assert.IsTrue(result.Success);
Assert.AreEqual(new GridPosition(2, 3), _service.GetState().RoombaPosition);
```

## Troubleshooting

### Module not executing
**Cause:** Module not in `allDefs` passed to `SetupGame()`, or not tracked via `TrackAttachment()`.
**Solution:** Ensure the module definition exists in allDefs and is tracked before executing.

### Energy not reaching module
**Cause:** A non-conductor module is between the core and the target module.
**Solution:** Only conductors (battery, core, connector, and their instances) forward energy. Never chain through thruster/vacuum/etc.

### Output not discovered
**Cause:** ActionOutput variable only assigned inside an `if` block.
**Solution:** Always assign a default value at top level, then override conditionally.

### Preset module not connecting
**Cause:** Face layout not rotated — Input face doesn't align with parent's Output.
**Solution:** Apply `RotationSteps` via `RotateCW()` before placing. 1 CW = Input faces West, 3 CW = Input faces East.

### scan() comparison fails silently
**Cause:** `scan()` returns a **list**, not a string. Comparing `scan() == "wall"` always fails.
**Solution:** Use `peek()` instead — it returns a simple string (`"wall"`, `"none"`, `"dust"`).

### Duplicate module IDs conflict
**Cause:** Placing two modules with the same base ID manually.
**Solution:** Use `PlaygroundPresets.Resolve()` for auto-instance IDs, or manually create cloned definitions with unique IDs.
