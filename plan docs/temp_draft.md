# temporary file for drafting project strategy document

# View-Mode Presentation Strategy (this portion is more of a to do list needs some adaptation or something)

## Why This Is Needed
- Visio’s routing engine collapses connectors back onto module borders unless we provide pre-spaced corridors and avoid glue points. The current F# planner emits dense, lane-wide blocks that leave the runner no usable geometry.
- Rows/tiers inherit 10–40 modules, causing thousands of edges to originate from the same channel; Visio rejects most of them and the remainder overlap badly.
- Same-module edges still compete with cross-module corridors because there is no notion of reserved intra-module channels or edge bundling metadata.
- We skip ~11k connectors per render; diagnostics are meaningless until the planner makes pagination and corridor choices deterministic.

## Strategic Goals
1. **Deterministic Spatial Allocation** – The planner must assign modules to pages, tiers, and rows with hard capacity caps so no band exceeds a target node/connector count.
2. **Channel-First Routing** – Compute routing corridors (horizontal & vertical) per module pair plus intra-module loops. Expose these as explicit points in the layout payload so the runner only paints them.
3. **Adaptive Module Geometry** – Card width/height should depend on connector fan-out, not fixed metadata. Nodes within a card need offsets that match the corridor plan.
4. **Lossless Pagination** – If connectors cross pages, emit bridge metadata (entry/exit anchors, labels) instead of dropping edges. Diagnostics should confirm zero skipped connectors for baseline datasets.
5. **Runner Simplicity** – Keep C# focused on painting shapes and honoring metadata. All layout intelligence lives in F# so we can unit test it.

## Planner Work Breakdown
### 1. Capacity-Driven Placement
- Introduce per-tier and per-row limits (modules, nodes, connectors). When over capacity, split into additional rows or new pages _before_ slotting begins.
- Feed paging planner with connector density metrics so it can spread “hot” modules across pages.
- Track row heights and cumulative offsets to give each row a stable baseline; record this in the layout payload for diagnostics.

### 2. Corridor & Bundle Planning
- For every module pair, cluster edges by source/target anchors and create corridor descriptors: direction, centerline, width, max bundle size.
- Reserve intra-module corridors (top/bottom or left/right) using module-specific counters so self-call edges never fall back to card centers.
- Emit per-bundle offsets in the layout JSON (e.g. `routing.channels` with `center`, `width`, `bundleIndex`) for the runner.

### 3. Adaptive Module Cards
- Compute node grid parameters from both node count and outgoing bundle count; widen cards or introduce column breaks when fan-out exceeds thresholds.
- Stagger node attachment points within each card to line up with the corridor list (e.g. map connector bundles to attachment slots).
- Persist overflow metadata so that truncated nodes are reproducible (badge text, counts) while we refine visual summarization.

### 4. Cross-Page Bridges
- When a connector leaves the current page tier, emit bridge metadata (`entryAnchor`, `exitAnchor`, label, bundle id). Runner uses this to draw stubs and callouts on both pages.
- Diagnostics should track remaining cross-page bundles and enforce zero silent skips.

### 5. Testing & Tooling
- Add F# tests for lane spillover, bundle generation, and corridor spacing to guarantee deterministic output.
- Capture a set of snapshot fixtures (JSON-only) for representative VBA inputs, validating there are no skipped connectors and layout bounds remain within target page sizes.

## Immediate Engineering Steps
1. Formalize capacity constants in F# (`layout.view.maxModulesPerLane`, `layout.view.maxConnectorsPerRow`, etc.) and enforce them during segment splitting.
2. Design a corridor descriptor type (`EdgeChannel`?) and thread it through `EdgeRoute` so the runner can distinguish planner-guided paths from fallbacks.
3. Update spillover logic to record row-level offsets, ensuring future runner spacing uses planner data instead of heuristics.
4. Begin writing unit tests for `computeViewLayout` focusing on module spillover and channel emission.



# F# C# Split Strategy

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

## Threshold Calibration
- Establish reference datasets (e.g., invSys full callgraph, cross_module_calls) and automate metric collection: modules per lane, connectors per channel, skipped edge counts.
- Run iterative planner sweeps, adjusting caps (`maxModulesPerLane`, `maxConnectorsPerRow`, corridor width) and storing results in a calibration log so density vs. readability trends are documented.
- Promote stable defaults once two successive runs hold connector skips at 0 and maintain >= 15% whitespace around module clusters; keep the calibration suite in CI to guard regressions.

## Overflow & Truncation Handling
- Extend the layout payload with explicit overflow records (`hiddenNodes`, `collapsedBundles`, `badgeLabel`) per module so the runner can render consistent summarization.
- Provide design guidance: badges near the card corner, hover/callout panels listing hidden items, and optional drill-through links for detail diagrams.
- Document overflow semantics in the CLI help and strategy docs so downstream consumers know how to interpret truncated areas.

## User-Driven Parameterization
- Expose key planner knobs through CLI/config (`--view-max-modules-per-lane`, `--view-corridor-width-in`, `--view-row-spacing-in`).
- Tag layout metadata with the effective values so rendered files record the configuration that produced them.
- Add validation to warn when user-supplied parameters exceed safe limits (for example, corridors narrower than 0.5in) and fall back to defaults when omitted.

## Bridge Navigation Experience
- For each cross-page bundle, emit both visual stubs and metadata (`targetPage`, `targetModule`, hyperlink URI). The runner can convert these into labelled callouts with "Go to page X" hyperlinks.
- Standardize callout styling (color, icon, orientation) so bridge entries are instantly recognizable.
- Maintain a manifest or index page listing all bridges for quick navigation across multi-file outputs.

## Modularization & Extensibility
- Keep corridor/channel planning in isolated modules (for example, `ChannelPlanner` in F#) returning typed descriptors; avoid coupling to Visio-specific notions.
- Define interfaces for bundle scoring, slot assignment, and label placement so new edge styles or layout profiles can plug in without rewrites.
- Cover the channel planner with property-based tests to ensure future extensions preserve invariants (no overlapping corridors, monotonic spacing).

## Screen vs. Print Profiles
- Introduce layout profiles that bundle parameter sets: `screen` (wider spacing, relaxed pagination, high emphasis on whitespace) vs `print` (tighter spacing, paper size fit).
- Allow profiles to inherit common defaults while overriding key metrics (tier spacing, corridor width, max nodes per page).
- Document recommended usage per profile and provide CLI switches (`--profile screen|print|export`) so users can generate the appropriate layout without manual tuning.

# Open Questions to Prioritize and answered
For high-density, algorithm-driven output—far beyond consensus VBA workflows—advanced strategies, many borrowed from graph visualization, data science, and scalable UI/UX design. Here are solutions optimized for automation, clarity, and performance:
# How will channel/edge bundles interact visually with overlapping modules or multiple layers/pages?
1. Visual Organization: Rendering High-Density Bundles/Channels
    • Hierarchical Layouts:
Use multi-level grouping (modules → submodules → nodes) so each visual band is only responsible for a manageable subset. Collapse low-activity regions and expand only hot spots, giving context and clarity even in deep diagrams.
Cap each Visio VSDX file at 25,000 shapes (nodes plus connectors across all layers/pages) aligns with proven Visio limits for stability, performance, and compatibility.
Recommendations for VDG Chunking Strategy
    • Set VSDX Cap:
Make 25,000 shapes the upper limit per VSDX output. This keeps each file responsive and safe for both authoring and view-mode navigation, even on typical business hardware.
    • Automate Overflow:
When the planner algorithm reaches the cap, seamlessly begin outputting the next VSDX file. Link files via navigable connector stubs, hyperlinks, index pages, or external metadata.
    • User Experience: 
        ○ Provide clear navigation cues: "Go to next segment," "Open continuation file," etc.
        ○ Include summary/overview pages in each file for context when crossing boundaries.
    • Diagnostics:
Emit metadata or warnings anytime a chunk splits to a new VSDX, so users can audit overflow and maintain traceability.
Why This Works
    • Improves stability: Prevents large diagrams from crashing or breaking Visio features.
    • Eases export/import: Facilitates incremental generation, review, and distribution.
    • Future-proofs for web, Power BI, and shared cloud scenarios.
Summary: With VDG, implement 25,000 shapes as the per-file threshold, then overflow to new files automatically—establishing a scalable, reliable, and maintainable process for massive diagrams. This approach is widely used for complex technical modeling in Visio workflows.
    • Edge Bundling Algorithms:
Apply edge bundling (curving, merging, or visually clustering related connectors into smooth, grouped paths) to minimize visual overlap. This is common in tools like D3.js and Graphviz, and reduces visual clutter even with thousands of edges.
    • Layered Views/Chunked Navigation:
Split diagrams into layers/pages by density, function, or flow, then let users toggle, filter, or drill down. Provide summary “overviews” with interactive zoom or “expand” controls for dense areas.
    • Interactive Callouts:
Replace dense connector clouds with clickable callouts, stubs, or “expand for details” icons. Only render full connector clouds on demand.
# Can you automate feedback (does the rendered Visio diagram match the planner JSON—are channels, bundles, and bridges realized as planned)?
2. Planner–Renderer Consistency and Validation
    • Round-trip Model Checking:
Build automated “diff” checks that compare planner (JSON/meta) expectations with rendered output—flagging discrepancies, missing connectors, overlaps, or visualization failures.
    • Metadata-Embedded Shapes:
Every shape and connector gets a unique, machine-readable ID and bundle/channel tag; automation scripts can verify layout completeness, trace cross-layer connections, and update diagrams incrementally.
    • Diagnostics Dashboard:
Render a diagnostics dashboard panel in every diagram—summarizing skip counts, overflow flags, anchor-to-bridge traces, and click-to-debug links—so users and developers instantly see problem areas.
# What conventions will you use for anchor/label naming and diagnostics so output remains human-readable and debuggable?
3. Anchor/Labeling, Debuggability in Dense Diagrams
    • Semantic Anchoring:
Assign meaningful, human and machine-readable anchor IDs that encode module, tier, page/layer, and connector class. Use anchor maps for navigation and debugging.
    • Hyperlink Navigation:
Equip stubs, callouts, and summary shapes with hyperlinks (internal, external, macro-driven) so users can click to jump to dense or remote nodes—making navigation as easy as browsing hyperlinks in web docs.
    • Error Tolerance and Summarization:
When unavoidable truncation or overflow occurs, automatically summarize what’s omitted (counts, example links, hover-over details), so density never translates to total loss of context.
Implementation Best Practices
    • Iterative Testing:
Use automated test fixtures—benchmark density, clarity, interactivity, and navigation, adjusting algorithms until output meets real user goals.
    • Parameterization:
Make capacity, chunking, and bundling logic tunable—adapt to different data sets and user needs.
    • Export Strategies:
Allow multi-output options (.vsdx per chunk, layers, interactive SVG, etc.), using whatever output best supports scale and clarity.
These solutions transcend consensus VBA. They borrow from scalable network visualization, interactive UI/UX, and automated testing—equipping your diagram generator to deliver clarity, usability, and robust analytics even at the largest data scales. Applying them makes VDG more like modern visualization engines than legacy automation scripts, positioning it for broad, high-value use.

# “gotchas” and advanced considerations to help bulletproof your design:

1. Cross-File Navigation & User Context Loss
    • Problem: When you overflow to new VSDX files, users can lose visual context—especially for connectors or modules that span files.
    • Solution: 
        ○ Implement index/crosswalk documents with hyperlinks pointing to all continuation or related files.
        ○ Use callout stubs or summary pages within each chunk to indicate where overflow originates/lands.
2. Metadata Integrity & Sync
    • Problem: Shape IDs, layer names, and connector references may get out of sync across files, breaking traceability for navigation, debugging, and analytics.
    • Solution: 
        ○ Embed persistent, globally unique IDs and descriptive tags in every shape and connector.
        ○ Maintain a manifest or registry mapping how objects span files.
3. Automated Testing Across Chunks
    • Problem: Unit/integration tests may only check single-file output, missing cross-chunk breaks or corruption.
    • Solution: 
        ○ Design multi-file test suites that validate: continuity, navigation, unique anchor IDs, and connector completeness.
4. Platform, Edition and Viewer Caveats
    • Problem: Some Visio features (layers, hyperlinks, custom metadata) display differently in Visio Online, Power BI, and desktop.
    • Solution: 
        ○ Test sample output in all target environments.
        ○ Document feature caveats and fallback strategies for limited or read-only viewers.
5. Save/Load and Incremental Diagram Building
    • Problem: Massive diagrams can hit save/load timeouts, or break undo history.
    • Solution: 
        ○ Advise users to close/reopen files one chunk at a time.
        ○ Consider incremental build tools or partial exports in VDG.
6. Connector Re-routing and Locking
    • Problem: Even with good planning, Visio may re-route or reset connectors at file open or page refresh.
    • Solution: 
        ○ Explicitly lock connector geometry and disable auto-routing during generation.
7. Data Graphics, Themes, and Formatting
    • Problem: Advanced formatting (data graphics, themes, background pages) may not propagate or can get corrupted across chunked files.
    • Solution: 
        ○ Apply formatting programmatically and keep stylesheets synchronized.
        ○ Avoid over-reliance on global themes when chunking.
8. Clear User Documentation
    • Problem: Users may be confused by cross-file workflow or chunk navigation.
    • Solution: 
        ○ Include quickstart guides or navigation how-tos in the repo.
        ○ Automate documentation export with each diagram batch.

# Advanced Strategies For Big Diagrams
Yes, for high-density, algorithm-driven output—far beyond consensus VBA workflows—there are advanced strategies, many borrowed from graph visualization, data science, and scalable UI/UX design. Here are solutions optimized for automation, clarity, and performance:

### 1. Visual Organization: Rendering High-Density Bundles/Channels

- **Hierarchical Layouts:**  
  Use multi-level grouping (modules → submodules → nodes) so each visual band is only responsible for a manageable subset. Collapse low-activity regions and expand only hot spots, giving context and clarity even in deep diagrams.

- **Edge Bundling Algorithms:**  
  Apply edge bundling (curving, merging, or visually clustering related connectors into smooth, grouped paths) to minimize visual overlap. This is common in tools like D3.js and Graphviz, and reduces visual clutter even with thousands of edges.

- **Layered Views/Chunked Navigation:**  
  Split diagrams into layers/pages by density, function, or flow, then let users toggle, filter, or drill down. Provide summary “overviews” with interactive zoom or “expand” controls for dense areas.

- **Interactive Callouts:**  
  Replace dense connector clouds with clickable callouts, stubs, or “expand for details” icons. Only render full connector clouds on demand.

### 2. Planner–Renderer Consistency and Validation

- **Round-trip Model Checking:**  
  Build automated “diff” checks that compare planner (JSON/meta) expectations with rendered output—flagging discrepancies, missing connectors, overlaps, or visualization failures.

- **Metadata-Embedded Shapes:**  
  Every shape and connector gets a unique, machine-readable ID and bundle/channel tag; automation scripts can verify layout completeness, trace cross-layer connections, and update diagrams incrementally.

- **Diagnostics Dashboard:**  
  Render a diagnostics dashboard panel in every diagram—summarizing skip counts, overflow flags, anchor-to-bridge traces, and click-to-debug links—so users and developers instantly see problem areas.

### 3. Anchor/Labeling, Debuggability in Dense Diagrams

- **Semantic Anchoring:**  
  Assign meaningful, human and machine-readable anchor IDs that encode module, tier, page/layer, and connector class. Use anchor maps for navigation and debugging.

- **Hyperlink Navigation:**  
  Equip stubs, callouts, and summary shapes with hyperlinks (internal, external, macro-driven) so users can click to jump to dense or remote nodes—making navigation as easy as browsing hyperlinks in web docs.

- **Error Tolerance and Summarization:**  
  When unavoidable truncation or overflow occurs, automatically summarize what’s omitted (counts, example links, hover-over details), so density never translates to total loss of context.

### Implementation Best Practices

- **Iterative Testing:**  
  Use automated test fixtures—benchmark density, clarity, interactivity, and navigation, adjusting algorithms until output meets real user goals.
- **Parameterization:**  
  Make capacity, chunking, and bundling logic tunable—adapt to different data sets and user needs.
- **Export Strategies:**  
  Allow multi-output options (.vsdx per chunk, layers, interactive SVG, etc.), using whatever output best supports scale and clarity.

# implementing high-density, algorithmic layouts 
(with bundling, layered navigation, round-trip validation, and diagnostics)—is challenging, but technically doable with VDG’s .NET-to-Visio automation pipeline, provided you carefully adapt to Visio’s model and performance limits.

What’s Doable with VDG’s .NET to Visio Pipeline
1. Hierarchical Layouts and Chunked Navigation
    • Yes: You can use .NET code to pre-calculate and partition content, generate multiple Visio pages, and assign modules/nodes to layers or separate files.
    • Practical Advice: Use .NET’s data structures and LINQ to chunk, group, and order diagram content before feeding it to Visio. Automate layer/page creation and enable navigation with hyperlinks or VBA macros.
2. Edge Bundling, Channel Reservation, and Adaptive Geometry
    • Partially: While .NET can calculate edge bundles and reserve unique routing corridors, Visio’s built-in router will still attempt to “clean up” or reroute lines when connectors are glued. You can:
        ○ Use polylines or unglued lines to visually bundle edges—even if these aren’t true dynamic connectors.
        ○ Stagger connection points by assigning unique, precomputed offsets/attachment points to avoid overlaps.
    • Workaround: Avoid Visio’s smart connectors for bundled, custom-routed edges; treat them as drawn lines or grouped shapes.
3. Metadata Embedding and Round-Trip Validation
    • Yes: .NET can inject custom metadata (shape data, tags, property fields) onto each shape and connector. You can write code to:
        ○ Verify shapes/connectors post-render,
        ○ Export diagnostics (JSON, CSV) for validation,
        ○ Support re-synchronization and round-trip updates.
4. Diagnostics Panels, Interactive Callouts, and Summarization
    • Yes: .NET can programmatically place diagnostics callout shapes, clickable tooltips, and summary panels within each Visio diagram or page, including links and drill-down targets.
5. Parameterization
    • Yes: All planner and output logic can be parameterized at the .NET application level, with CLI/config options surfaced to users.

# Capping object count per WSDX file
Yes, that’s a strong and practical strategy. Capping each Visio VSDX file at 25,000 shapes (nodes plus connectors across all layers/pages) aligns with proven Visio limits for stability, performance, and compatibility.

## Recommendations for VDG Chunking Strategy

- **Set VSDX Cap:**  
  Make 25,000 shapes the upper limit per VSDX output. This keeps each file responsive and safe for both authoring and view-mode navigation, even on typical business hardware.
- **Automate Overflow:**  
  When the planner algorithm reaches the cap, seamlessly begin outputting the next VSDX file. Link files via navigable connector stubs, hyperlinks, index pages, or external metadata.
- **User Experience:**  
  - Provide clear navigation cues: "Go to next segment," "Open continuation file," etc.
  - Include summary/overview pages in each file for context when crossing boundaries.
- **Diagnostics:**  
  Emit metadata or warnings anytime a chunk splits to a new VSDX, so users can audit overflow and maintain traceability.

# Turn Visio Features off
Certain Visio features can be turned off or programmatically suppressed while generating diagrams with .NET automation or VBA. This helps produce more predictable, planner-driven output by limiting Visio’s automatic behaviors that may interfere with algorithmic layouts.()
Features You Can Disable or Control
    1. Connector Routing & Reroute
        ○ Disable Auto Routing:
For individual connectors, set Shape.CellsU("Reroute") = 0 (or via COM: shape.CellsU["Reroute"].ResultIU = 0) to turn off auto-rerouting when shapes move or get re-glued.
        ○ Lock to Geometry:
Use polylines or static lines (not “Connectors”) if you want to override Visio’s dynamic pathfinding entirely.
    2. Snap & Glue
        ○ Turn Off Snapping and Gluing (Globally):
Temporarily disable these settings during automation:
Application.ActiveWindow.GlueSettings = False
Application.ActiveWindow.SnapSettings = False

Remember to restore original settings after automation is complete, to not affect user experience.
    3. Layout and Auto-Align
        ○ Disable Dynamic Layout:
On the page or document, you can adjust the layout properties to prevent shapes from auto-arranging:
Application.ActiveWindow.Page.LayoutStyle = visLFSManual
        ○ Prevent Overlap:
Optionally set manual placement on all shapes: shape.CellsU("LockMoveX").FormulaU = "1"; shape.CellsU("LockMoveY").FormulaU = "1"
    4. Event Reactions
        ○ Suspend Events:
Use Application.EventsEnabled = False to prevent event triggers and macro reactions while generating diagrams (just remember to turn back on).
    5. Protection Locks
        ○ Lock Shape Behavior:
Set ShapeSheet cells like LockGroup, LockSelect, LockDelete to prevent user or automation from modifying certain objects after creation.
    6. Undo Buffer
        ○ Suspend Undo Logging:
Use Application.DeferRecalc and Application.UndoEnabled = False for complex batch operations for speed and memory.

Implementation Advice
    • Temporarily Suppress features: Always restore original settings after generation to keep normal Visio usability.
    • Combine with Planner Output: Apply these suppression settings only while running your automation—the runner (CLI) can toggle these features as needed per step.
    • Selective Use: You may want to selectively turn off snapping, glue, or reroute for specific shapes/connectors only—leaving core Visio functionality available for user tweaking after generation.

By controlling these automation-targeted features, you gain much more deterministic output and minimize the risk of Visio “fixing” your algorithmically generated geometry or layouts. This control is critical to producing professional, high-fidelity diagrams with VDG.
