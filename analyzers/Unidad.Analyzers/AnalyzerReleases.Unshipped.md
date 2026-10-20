; Unidad analyzer rules. Every rule here replaces a paragraph of architectural prose that a
; previous project carried and violated anyway. Escape one with `#pragma warning disable UDxxx`
; plus a comment saying why. Every rule runs only in assemblies named Unidad.* or Cds.*.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UD001 | Unidad | Error | A service must not create engine objects or call engine singletons
UD002 | Unidad | Error | Update/LateUpdate/FixedUpdate/OnGUI is not declared
UD003 | Unidad | Error | A service must not reach into the scene
UD004 | Unidad | Error | A service must not read ambient time
UD006 | Unidad | Error | A tuning value must be a named constant
UD008 | Unidad | Error | Devices and the cursor are read only by an input bridge
UD009 | Unidad | Error | Simulation math must be platform-independent
UD010 | Unidad | Error | Objects and scenes come from the factory and the scene loader
UD011 | Unidad | Error | Assets are loaded through AssetLibrary
UD012 | Unidad | Error | Nothing reads GPU data back
UD013 | Unidad | Error | A service must not create its own randomness
UD014 | Unidad | Error | No mutable static state
UD015 | Unidad | Error | Burst subset violation
UD016 | Unidad | Error | The Unidad namespace belongs to the framework

; UD005 (hand-typed pin) and UD007 (a measurement must not assert) were retired on 2026-09-07 with
; the snapshot and measurement lanes they policed. The ids are not reused.
