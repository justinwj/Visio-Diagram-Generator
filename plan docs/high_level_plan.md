Here’s a clear recap of the F# / C# split—the architectural guardrail designed to maximize clarity, purity, and testability:

***

### **F# Responsibilities (VisioDiagramGenerator.Algorithms/)**
- **Pure Logic & Layout Algorithms:**
  - Compute all *layout, paging, tiering, grid slot* and connector planning given an IR input and options.
  - Emit *deterministic, side-effect-free* records/lists: e.g., `LayoutPlan`, `PagePlan`, `GridSlot`, `NodePlacement`.
- **Heuristics, Metrics, and Grouping:**
  - Apply rules for paging, slotting, module grouping, spillover, and any composite “view/print” logic.
  - Return all decisions as data that can be tested in isolation.
- **Unit Testing:**
  - FsUnit/xUnit covers all algorithmic transforms and ensures that for the same IR and options, results are always the same (no randomness, idempotent).
- **No Rendering or IO:**
  - Never touch Visio COM, file IO, or CLI output—just export strongly-typed F# records.

***

### **C# Responsibilities (VDG.CLI/ & Interop)**
- **Orchestration & Rendering:**
  - Handle all Visio COM automation: shape placement, page/sheet creation, and connector drawing, as directed by F# layout plans.
  - Manage CLI pipeline, user flags, and invocation logic.
  - Map output from F# modules onto Visio concepts (pages, shapes, connectors, text, styling).
- **Diagnostics/Reporting:**
  - Summarize layout/algorithm outputs in diagnostics JSON and CLI summaries.
  - Pipe F# results through into files, reports, and logs.
- **Filtering & Preprocessing:**
  - If needed, do early-stage module/page filtering and build the IR/option set that is sent to the algorithms layer.
- **Integration/Smoke Testing:**
  - Run end-to-end smoke tests, including diagram creation, file output, and full fixture regression checks.

***

### **Data & API Boundary**
- **Pure Data Hand-off:**
  - C# CLI sends IR and options (input), F# returns a typed `LayoutPlan`/`PagePlan` (output).
  - Layout plan is the authoritative source—C# never re-computes layout or paging, only renders in the right order.

***

### **Benefits**
- **F# stays pure and highly testable.**
- **C# manages side-effects, automation, rendering, and integration.**
- **Future changes to layout/paging/heuristics are isolated, predictable, and easy to unit test.**

***

**Quick summary:**  
F# = brains of the operation (algorithmic, stateless, predictable).  
C# = hands and face for the user (Visio automation, reporting, user interaction, file output).  
Keep all intelligence, decisions, and transforms pure and in F#; push all real-world effect and interop to C#.