# Strategy for Vision-Driven Diagram Generation

## Vision Alignment Summary
- Deliver diagrams that satisfy the vision statement: actionable, readable, and purpose-driven views of every module, form, procedure, and edge.
- Treat information design as the primary objective; spatial layout only proceeds once the semantic model is correct and validated.
- Guarantee that every emitted page explains *why* structures exist (roles, flows, responsibilities), not merely *how* they connect.

## Gate 0: Semantic Modeling & Domain Abstraction
### Objectives
- Build a deterministic taxonomy that classifies modules, procedures, and forms into subsystems aligned with business intent.
- Detect roles (event handler, command, data loader, validator, etc.) and control/data flows so downstream diagrams can narrate behavior.
- Produce domain-aware bundles that collapse repetitive edges into meaningful aggregates.

### Required Artifacts (must exist before layout work)
1. `taxonomy.json` (or equivalent) containing subsystem hierarchy, ownership, and descriptive tags for every module/procedure.
2. `flows.json` describing role-to-role interactions (e.g., `Form.Event -> MouseScroll.Hook -> Tally.Update`).
3. Test coverage that validates classification heuristics against known modules and fixtures.
4. Documentation of unresolved items or ambiguous classifications to review with subject-matter experts.

### Subsystem Taxonomy
- Walk the VBA IR to infer domains: workbook/worksheet modules, user forms, admin utilities, data access layers, telemetry, etc.
- Apply naming, attributes (e.g., `Worksheet_Change`), metadata, and file location cues to categorize both modules and procedures.
- Allow multiple lenses: primary subsystem, secondary capability tags, lifecycle stage (input, processing, output).
- Persist revision history so classifications are auditable and can evolve without breaking downstream consumers.

### Role and Flow Detection
- Identify role signatures (event handler, command dispatcher, calculator, persistence, validation) using metadata, call signatures, and naming patterns.
- Trace flow chains by following call graph edges and annotating them with role transitions; capture both dominant paths and exceptional ones.
- Detect shared resources (globals, worksheets, configuration objects) and mark flows that coordinate through them.
- Generate summaries that plain-language describe the purpose of each flow before it ever gets drawn.

### Information-Driven Bundling & Summarization
- Group edges by semantic context (role, subsystem pair, shared control) rather than purely by geometric proximity.
- Replace dense hairballs with meta-edges that state aggregated intent (e.g., "SelectedItem calculations: 12 procedures").
- Emit overflow/summary nodes when raw detail would exceed readability caps, linking back to detailed drill-down views.
- Align bundling thresholds with business meaning (e.g., group all UI-to-domain validation edges, keep security-sensitive flows explicit).

## Gate 1: Semantic Review & Feedback Loop
- Review taxonomy and flow artifacts with engineering stakeholders before enabling Visio output.
- Block layout execution if required roles, subsystems, or flow descriptions are missing or fail tests.
- Capture review notes and feed them back into the classifiers; treat the semantic layer as a living contract.

## Gate 2: Spatial Layout & Rendering Execution
### Why This Is Needed
- Visio’s routing engine collapses connectors onto module borders unless we provide pre-spaced corridors and avoid glue points.
- Rows/tiers inherit 10–40 modules, causing thousands of edges to originate from the same channel, which Visio cannot render cleanly.
- Same-module edges compete with cross-module corridors because there is no notion of reserved intra-module channels or bundling metadata.
- We previously skipped ~11k connectors per render; deterministic layout is mandatory before diagnostics are trustworthy.

### Strategic Goals
1. **Deterministic Spatial Allocation** – Assign modules to pages, tiers, and rows with strict capacity caps so no band exceeds target node/connector counts.
2. **Channel-First Routing** – Compute routing corridors (horizontal & vertical) per module pair plus intra-module loops. Expose these as explicit geometry in the layout payload.
3. **Adaptive Module Geometry** – Size cards and attachment slots based on connector fan-out and the semantic bundles produced in Gate 0.
4. **Lossless Pagination** – Emit bridge metadata (entry/exit anchors, labels) when connectors cross pages instead of dropping edges; diagnostics must confirm zero skipped connectors for baseline datasets.
5. **Runner Simplicity** – Keep C# focused on rendering shapes using data emitted from F# so every decision is testable.

### Planner Work Breakdown
1. **Capacity-Driven Placement**
   - Introduce per-tier and per-row limits (modules, nodes, connectors). When over capacity, split into additional rows or new pages *before* slotting begins.
   - Feed pagination with connector density and semantic importance so high-impact modules get dedicated space.
   - Track row heights and cumulative offsets in the layout payload for diagnostics.
2. **Corridor & Bundle Planning**
   - For every module pair, cluster edges by semantic bundle and planned attachment anchors; generate corridor descriptors (direction, centerline, width, bundle index).
   - Reserve intra-module corridors with module-specific counters so loops never fall back to card centers.
   - Emit per-bundle offsets and channel labels in the layout JSON (`EdgeChannel`, `ChannelLabel`).
3. **Adaptive Module Cards**
   - Compute node grids from node count, bundle demand, and semantic priority; widen cards or introduce columns as needed.
   - Map connector bundles to attachment slots so the runner can draw pre-aligned polylines.
   - Persist overflow metadata so truncated nodes are reproducible (badge text, counts) while we refine summarization.
4. **Cross-Page Bridges**
   - When a connector leaves the current page, emit bridge metadata (`entryAnchor`, `exitAnchor`, label, bundle id). Draw stubs and callouts on both pages.
   - Log diagnostics for every bridge and enforce zero silent skips.
5. **Testing & Tooling**
   - Add F# tests for lane spillover, bundle generation, corridor spacing, and semantic bundling to guarantee deterministic output.
   - Capture snapshot fixtures validating no skipped connectors, ensuring geometry matches corridor plans, and confirming taxonomy references remain consistent.

## Additional System Strategies
### F#/C# Split Strategy
- **F# Responsibilities (VisioDiagramGenerator.Algorithms/)**
  - Compute all layout, paging, tiering, grid slot, connector planning, and semantic aggregation given IR input and options.
  - Emit deterministic, side-effect-free records/lists (`LayoutPlan`, `PagePlan`, `ChannelLabel`, etc.).
  - Apply heuristics for paging, slotting, grouping, spillover, and combined "view/print" logic.
  - Provide unit tests that ensure idempotent results for identical inputs.
- **C# Responsibilities (VDG.CLI/ & Interop)**
  - Handle Visio COM automation: shape placement, page creation, connector drawing, and diagnostic output.
  - Read the semantic and layout payloads, honouring attachment slots, corridors, and bridge metadata without re-computing geometry.
  - Manage CLI options, runner flags, environment toggles (`VDG_SKIP_RUNNER`), and packaging.
- **Interop Contracts**
  - Keep payload schemas versioned; add compatibility shims when breaking changes occur.
  - Use Shape Data / User cells to embed semantic tags so diagrams remain informative when exported.

~~### Cross-Platform Environment Split~~
- ~~**WSL Focus (Cross-Platform Components)**~~
  - ~~Run planners, semantic analyzers, F# tests, and CLI utilities that do not require Visio inside WSL for faster tooling, reproducible builds, and CI parity.~~
  - ~~Store shared artifacts (taxonomy/flow JSON, fixtures, diagnostics) in paths accessible from both environments.~~
- ~~**Windows 11 Focus (Visio Automation & Packaging)**~~
  - ~~Keep Visio-dependent runners, COM interop, and installer/packaging scripts on Windows.~~
  - ~~Validate final `.vsdx` outputs, ShapeSheet annotations, and packaging automation using native Visio.~~
- ~~**Coordination Practices**~~
  - ~~Provide scripts that sync build outputs between environments (e.g., `./tools/sync-wsl.ps1` future work).~~
  - ~~Document environment prerequisites so contributors understand which tasks live in WSL vs Windows.~~
  - ~~Align CI: run semantic/layout tests in Linux containers; schedule Windows runners for Visio smoke tests.~~
  - ~~Packaging remains unified: gather cross-platform artifacts and Windows binaries into a single release bundle (ZIP/MSIX) with clear documentation.~~

## Implementation Playbooks
### Implementing High-Density, Algorithmic Layouts
*(With bundling, layered navigation, round-trip validation, and diagnostics)* – This is challenging but achievable with VDG’s .NET-to-Visio pipeline when we adapt to Visio’s model and performance limits.

1. **Hierarchical Layouts and Chunked Navigation**
   - Use .NET code to pre-calculate and partition content, generate multiple Visio pages, and assign modules/nodes to layers or separate files.
   - Automate layer/page creation and enable navigation with hyperlinks or VBA macros.
2. **Edge Bundling, Channel Reservation, and Adaptive Geometry**
   - Calculate edge bundles and reserve routing corridors in planners; use polylines or unglued lines to keep Visio from re-routing bundles.
   - Stagger connection points via precomputed offsets/attachment points to avoid overlaps.
3. **Metadata Embedding and Round-Trip Validation**
   - Inject custom metadata (shape data, tags, property fields) onto each shape/connector.
   - Verify shapes/connectors post-render, export diagnostics (JSON/CSV), and support re-synchronization.
4. **Diagnostics Panels, Interactive Callouts, and Summarization**
   - Programmatically place diagnostics callouts, tooltips, and summary panels with links/drill-down targets.
5. **Parameterization**
   - Surface planner and output logic as CLI/config options for users.

### Capping Object Count per VSDX File
- **Set VSDX Cap** – Limit to 25,000 shapes per VSDX output to maintain responsiveness.
- **Automate Overflow** – When the cap is reached, start a new VSDX and link files via connector stubs, hyperlinks, or index pages.
- **User Experience** – Provide navigation cues ("Go to next segment", "Open continuation file"); include overview pages for context.
- **Diagnostics** – Emit metadata/warnings whenever a chunk splits so users can audit overflow.
- **Layer Cap** – Maintain 1–1,000 shapes per layer; create continuation connectors when exceeding the limit.

### Turn Visio Features Off (During Automation)
- **Connector Routing & Reroute** – Set `Shape.CellsU("Reroute") = 0`; use polylines when bundles must remain fixed.
- **Snap & Glue** – Disable during automation (`Application.ActiveWindow.GlueSettings = $false`, `SnapSettings = $false`); restore afterwards.
- **Layout and Auto-Align** – Set manual layout mode and lock movement (`LockMoveX`, `LockMoveY`).
- **Event Reactions** – Temporarily suspend events (`Application.EventsEnabled = False`) during batch updates.
- **Protection Locks** – Use ShapeSheet protection cells to freeze generated artifacts.
- **Undo Buffer** – Suspend undo logging for large renders, then restore state.

- **Implementation Advice** – Suppress features temporarily, coordinate with planner output, and apply overrides selectively.
