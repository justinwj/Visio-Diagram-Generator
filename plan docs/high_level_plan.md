# High-Level Plan

## Architecture Guardrails
### F# Responsibilities (VisioDiagramGenerator.Algorithms)
- Own every layout decision: paging, tiering, slotting, connector planning, and spillover heuristics.
- Split oversized modules into deterministic segments and assign page plans before placement so downstream code works against segment-aware IDs.
- Emit deterministic, side-effect-free records (`LayoutPlan`, `PagePlan`, `NodePlacement`, etc.) plus per-page origins/bounds and diagnostics metadata.
- Keep logic pure and fully unit-tested (FsUnit/xUnit); no Visio COM, IO, or rendering-side effects.

### C# Responsibilities (VDG.CLI & Interop)
- Orchestrate CLI options, Visio COM automation, and file output using the data returned by the F# layer.
- Map layout data onto Visio concepts (pages, shapes, connectors, styling) and manage diagnostics/reporting.
- Handle integration and smoke tests, plus any preprocessing/filtering required before handing IR/options to the algorithms layer.

### Data & API Boundary
- C# sends IR & options, F# returns authoritative layout data. The CLI must never re-compute layout, paging, or spillover.
- Paging diagnostics, metadata, overflow stats, and per-page coordinates originate exclusively in the F# layout payload.

### Benefits
- Purity in the planner keeps algorithms predictable and easy to test.
- Side effects remain isolated to the CLI/Visio automation layer.
- Future layout and pagination changes stay localized to F# logic.

## Operating Practices & Caveats
### Separation of Concerns
- Screen (view-mode) diagrams prioritize ≤1,000 objects per layer and interactive clarity.
- Print (page-aware) diagrams follow separate pagination and paper-fit heuristics.

### Repository Discipline
- Keep every generated artefact (outputs, diagnostics, strategy docs) in-repo for traceability.

### Cross-File/Chunk Considerations
- Provide navigation aids when outputs spill across VSDX files (indexes, hyperlinks, callout stubs).
- Maintain manifests mapping global IDs to per-file shapes/connectors for traceability.
- Expand integration tests to validate multi-file continuity and anchor uniqueness.

### Platform & Viewer Differences
- Features such as layers, hyperlinks, and custom metadata behave differently across Visio Desktop, Visio Online, Power BI, etc. Test key flows on every target platform and document fallbacks.

## Visio Capabilities & Practical Limits
- Deterministic planner output, metadata-rich shapes, diagnostics panels, and parameterized CLI workflows are all achievable via .NET automation.
- Simulate advanced graph layout by precomputing corridors, bundling connectors, staggering attachments, and drawing custom polylines instead of relying on Visio’s dynamic connectors.
- Expect performance constraints at thousands of shapes/connectors per page; pagination and channel planning are mandatory at this scale.

## Automation Controls (Disable When Rendering)
1. **Connector Routing/Reroute** – Set `Reroute = 0` or use polylines to bypass Visio’s path finding.
2. **Snap & Glue** – Temporarily disable during automation, then restore settings.
3. **Layout & Auto-Align** – Use manual layout styles and lock move/resize cells as needed.
4. **Events** – Suppress events (`Application.EventsEnabled = False`) while generating complex diagrams.
5. **Protection Locks** – Lock shapes from edits or deletions post-generation.
6. **Undo Buffer** – Suspend undo logging for faster batch operations.

Apply these controls selectively and reset them once automation completes to preserve user experience.

## View-Mode Algorithm Strategy
### Why the Planner Must Improve
- Visio collapses connectors to card borders unless corridors are reserved and glue points avoided.
- Lanes inherit dozens of modules, creating dense connector bundles that Visio skips or overlaps.
- Self-module edges still compete with cross-module traffic; there’s no notion of reserved intra-module channels.
- We currently skip ~11k connectors per invSys render, making diagnostics unreliable until layout decisions are deterministic.

### Strategic Goals
1. **Deterministic Spatial Allocation** – Hard caps on modules/nodes/connectors per tier/row/page.
2. **Channel-First Routing** – Pre-compute horizontal/vertical corridors and intra-module loops; expose these in layout metadata.
3. **Adaptive Module Geometry** – Card dimensions and attachment slots respond to connector fan-out.
4. **Lossless Pagination** – Emit bridge metadata (entry/exit anchors, labels) instead of silently dropping cross-page edges.
5. **Runner Simplicity** – Keep Visio automation dumb; it should only paint what the planner already decided.

### Planner Work Breakdown
1. **Capacity-Driven Placement**
   - Introduce per-tier/per-row limits (modules, nodes, connectors) and spill into new rows/pages before placement.
   - Feed paging planner with connector-density metrics to distribute “hot” modules.
   - Record row heights/origins in layout payload for diagnostic clarity.
2. **Corridor & Bundle Planning**
   - Cluster edges per module pair, define corridor descriptors (direction, centerline, width, bundle index).
   - Reserve intra-module corridors via per-module counters.
   - Emit bundle metadata (e.g., `routing.channels`) for the runner to consume.
3. **Adaptive Module Cards**
   - Compute grid parameters from node count and bundle pressure; stagger attachment points to align with corridors.
   - Persist overflow summaries (badge text/count) to keep truncated nodes reproducible.
4. **Cross-Page Bridges**
   - Emit bridge metadata (entry/exit anchors, labels, bundle id) for cross-page edges.
   - Ensure diagnostics enforce zero skipped connectors once bridges are in place.
5. **Testing & Tooling**
   - Add unit tests for spillover, channel creation, and pagination decisions.
   - Capture snapshot fixtures (JSON) for large VBA inputs to validate zero skips and bounded layout dimensions.

### Immediate Engineering Steps
1. Formalize capacity constants (`layout.view.maxModulesPerLane`, `layout.view.maxConnectorsPerRow`, etc.) and enforce them during segment splitting.
2. Design a corridor descriptor type (e.g., `EdgeChannel`) and plumb it through `EdgeRoute` so the runner distinguishes planner-guided paths.
3. Update spillover logic to record row-level offsets; retire runner heuristics in favour of planner data.
4. Begin unit tests targeting `computeViewLayout` (module spillover, channel emission, pagination).

### Open Questions
- What thresholds keep diagrams readable for 1k+ connectors? Gather empirical data from invSys and other fixtures.
- How should modules be ordered when capacity forces wrap-around (alphabetical vs. connector-driven)?
- Do we need divergent strategies for screen vs. print modes, or can we re-parameterize a single planner?

### Action Items
- [ ] Encode capacity limits and enforce multi-row placements in F#.
- [ ] Prototype `EdgeChannel` metadata and wire it to the runner.
- [ ] Capture diagnostics showing zero skipped connectors for invSys once bundling/spillover land.
- [ ] Expand tests to lock in corridor computations, pagination, and spillover behaviour.
