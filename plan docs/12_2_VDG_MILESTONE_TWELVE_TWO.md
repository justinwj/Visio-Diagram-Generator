**VDG MILESTONE 12.2 – Layer Segmentation & Connector Bridging**

Goal  
Handle large datasets without Visio performance collapse by capping each layer at ~1 000 shapes/1 000 connectors, auto-splitting overflow onto additional layers/pages, and wiring cross-layer connector bridges during pre-compute.

Scope
- Layer-aware paging  
  - Extend planner to track cumulative shapes/connectors per layer and spill excess onto new layers/pages before hitting Visio limits.  
  - Ensure segmentation preserves logical groupings (modules, forms, containers) while respecting shape budgets.
- Connector bridging  
  - Detect edges that cross layer boundaries; insert explicit “transfer” nodes/ports or layer-bridge metadata so rendering can expose connection points between layers.  
  - Maintain readable diagnostics that flag cross-layer edges and list their endpoints.
- Data surface & diagnostics  
  - Add metrics for `layerCount`, `layerShapeCount`, `layerConnectorCount`, and list of cross-layer bridges.  
  - Warn when a single module or container exceeds layer budgets and cannot be split.
- Renderer updates  
  - Teach C# renderer to honour layer assignments: create Visio layers dynamically, place shapes/connectors on correct layers, and expose bridging anchors (e.g., stub shapes) for cross-layer links.  
  - Provide CLI options to filter or render specific layers.
- Documentation & automation  
  - Document layer segmentation rules, bridging semantics, and how to navigate multi-layer output (README, PagingPlanner, ErrorHandling).  
  - Update smoke tests/fixtures (invSys) so diagnostics demonstrate layer counts and bridges; adjust CI to assert layer thresholds.

Milestone Breakdown
1. **Analysis & Design**  
   - Inventory current shape/connector counts per layer/page (invSys baseline).  
   - Draft layer-cap rule (shape + connector budgets) and bridging schema (`BridgeId`, source layer, target layer, connector metadata).
2. **Planner Enhancements**  
   - Extend F# planners (ViewMode + Paging) to maintain layer buckets with budget enforcement.  
   - Introduce cross-layer bridge records while keeping layout deterministic.
3. **Renderer & Diagnostics**  
   - Update `VDG.CLI` to create Visio layers on the fly, assign shapes/connectors, and draw bridge anchors.  
   - Emit diagnostics showing layer stats, bridge counts, and overflow warnings.
4. **Fixtures & CI**  
   - Regenerate invSys (and at least one smaller fixture) to validate layer counts.  
   - Update smoke script/baselines to assert `layerCount`, `bridges`, and zero layer overflow errors.
5. **Docs & Developer Guidance**  
   - Expand ErrorHandling/FixtureGuide with layer-focused troubleshooting.  
   - Provide guidance on how to inspect/merge layers when regenerating fixtures.

Acceptance Criteria
- Large diagrams (invSys callgraph) render with automatic layer segmentation: no layer exceeds ~1 000 shapes/1 000 connectors.  
- Diagnostics include layer metrics and bridge inventories; CI verifies they stay within thresholds.  
- Cross-layer connectors round-trip cleanly in Visio (bridge anchors visible, connections traceable).  
- Documentation and scripts explain how to work with layered outputs, refresh fixtures, and interpret diagnostics.  
- Fewer “partialRender” warnings due to layer overload; when unavoidable, diagnostics identify offending modules/layers.

Risks & Notes
- Layer rebalancing may disrupt existing fixture baselines; coordinate updates carefully.  
- Connector bridges must remain readable—avoid cluttering the diagram with excessive stubs.  
- Ensure layer creation logic integrates with Visio automation limits (naming, colours, visibility).  
- Coordinate with Milestone 13 view-mode work so both layout fidelity and layer segmentation stay aligned.
