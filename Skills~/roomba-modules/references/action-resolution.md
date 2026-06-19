# Action Resolution & Script Rules

## Turn Execution Order

```
1. INCREMENT turn number
2. BUILD execution order (BFS from core, sorted by attachment order)
3. CALCULATE energy budgets (BFS waterfall from battery through conductors)
4. EXECUTE pipeline:
   a. Collect input variables from upstream outputs
   b. Set energy budget
   c. Compile + run script
   d. Discover outputs (static analysis of top-level assignments)
   e. Skip passive modules and battery
5. CHECK for runtime errors
6. RESOLVE actions (direction→move, rotation→turn, clean→vacuum, etc.)
7. DRAIN battery (sum of all energy budgets)
8. CHECK game over (battery=0) and win (all dirt cleaned)
9. RETURN PlaygroundTurnResult
```

## Action Table

| ActionOutput | Value | World Action |
|-------------|-------|--------------|
| `direction` | `"forward"` | `world.TryMoveForward()` |
| `direction` | `"backward"` | `world.TryMoveBackward()` |
| `rotation` | `"left"` | `world.SetRotation(current.RotateLeft())` |
| `rotation` | `"right"` | `world.SetRotation(current.RotateRight())` |
| `rotation` | `"none"` | No rotation (skip) |
| `clean` | Any truthy | `world.TryVacuum()` |
| `target` | `"x,y"` | `world.TryMoveRoomba(dx, dy)` |
| `charge` | Any truthy | `world.RechargeBattery(1)` |
| `status` | Anything | No action (data-only, used by Core) |
| `energy` | Anything | No action (data-only, used by Battery) |

Actions resolve in execution order (BFS from core). Movement before rotation if Thruster appears before Gyroscope.

## Script Language

Python-like syntax with indentation-based blocks.

### Types
- Number: `42`, `3.14`
- String: `"hello"`
- Bool: `True`, `False`
- None: `None`
- List: `[1, 2, 3]`

### Control flow
```python
if condition:
    block
elif other:
    block
else:
    block

while condition:
    block

for item in list:
    block
```

### Functions
```python
def add(a, b):
    return a + b
```

### Operators
- Arithmetic: `+`, `-`, `*`, `/`, `%`
- Comparison: `==`, `!=`, `<`, `<=`, `>`, `>=`
- Logical: `and`, `or`, `not`

## Output Discovery Rules

The pipeline uses **static analysis** to discover which variables a script outputs. Only **top-level assignments** are discovered.

```python
# Discovered as output: "direction"
direction = "forward"

# NOT discovered (inside if block)
if condition:
    direction = "forward"
```

Correct pattern: always assign at top level, then override conditionally.

## Data Flow Between Modules

Upstream module outputs become downstream module input globals:

```
Core script → outputs: status="online"
    ↓ (injected as global)
Thruster script → can read `status`, outputs: direction="forward"
    ↓ (ActionResolver reads "direction")
World → TryMoveForward()
```

## Energy Budget in Scripts

- Each native function has an `EnergyCost`
- VM tracks energy used during execution
- If a function call exceeds budget: VM halts silently (no error)
- Script stops mid-execution — partial results may occur

## Grid & Movement

- Roomba starts at grid center facing Up: `(width/2, height/2)`
- All cells are walkable (empty and dirt) — no walls on the grid
- `"wall"` from `scan()` means out of bounds
- Movement returns false at boundaries — roomba stays put
- Vacuum returns false if no dirt — harmless
- Roomba rotation: `Up→Right→Down→Left` (clockwise via `RotateRight()`)
- Direction offsets: Up=(0,1), Right=(1,0), Down=(0,-1), Left=(-1,0)
