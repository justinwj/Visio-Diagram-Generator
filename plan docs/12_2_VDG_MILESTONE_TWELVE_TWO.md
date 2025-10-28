**VDG MILESTONE 12.2 – Layer Segmentation & Connector Bridging**

Goal  
Handle large datasets without Visio performance collapse by capping each layer at ~1 000 shapes/1 000 connectors, auto-splitting overflow onto additional layers/pages, and wiring cross-layer connector bridges during pre-compute.

Recommended Order of Operations

1. **Baseline & Design**
   - [x] Inventory current shape/connector counts per page/layer (invSys baseline).  
     - Current callgraph (view mode) renders 210 nodes, 33 containers, 1 684 connectors; planner splits into 7 pages with 34–40 nodes and up to 54 connectors per page, all on the default layer.
   - [x] Define layer budget rules (shape + connector caps) and a bridging schema (`BridgeId`, source layer, target layer, connector metadata).  
     - Layer budgets: soft target 900 shapes & 900 connectors per layer; hard cap 1 000 shapes/connectors triggers forced split and emits `LayerOverflow` diagnostics.  
     - Planner must preserve logical groups; when a single module exceeds the cap it is isolated on its own layer and flagged.  
     - Bridge schema: `BridgeId`, `sourceLayer`, `sourceNodeId`, `targetLayer`, `targetNodeId`, `connectorId`, `metadata` (preserve original edge metadata).  
     - Each bridge also records `entryAnchor`/`exitAnchor` (diagram coordinates) so renderer can place stub shapes and diagnostics can list cross-layer endpoints.

2. **Planner Enhancements (F#)**
   - [ ] Extend view-mode/paging planners to track cumulative shapes/connectors and enforce layer budgets.  
   - [ ] Emit cross-layer bridge records while keeping layout deterministic.

3. **Renderer & CLI Work (C#)**
   - [ ] Teach `VDG.CLI` to create Visio layers dynamically and assign shapes/connectors to the correct layer.  
   - [ ] Render bridge anchors or stubs for cross-layer connectors; add CLI switches to render specific layers.  
   - [ ] Update diagnostics output with `layerCount`, per-layer shape/connector totals, and bridge summaries.

4. **Fixture & CI Alignment**
   - [ ] Regenerate `invSys` and at least one smaller fixture to confirm layer counts and bridge data.  
   - [ ] Update smoke/baseline checks to assert new metrics (`layerCount`, `bridges`, layer overflow warnings).

5. **Documentation & Guidance**
   - [ ] Document layer segmentation, bridge semantics, and multi-layer navigation (README, PagingPlanner, ErrorHandling).  
   - [ ] Provide regeneration guidance for layered fixtures and troubleshooting tips.

Acceptance Criteria
- [ ] Large diagrams (invSys callgraph) render with automatic layer segmentation; no layer exceeds ~1 000 shapes/1 000 connectors.  
- [ ] Diagnostics include layer metrics and bridge inventories; CI verifies thresholds.  
- [ ] Cross-layer connectors round-trip cleanly; bridge anchors are clearly represented.  
- [ ] Updated docs explain how to work with layered outputs and fixture refresh workflows.  
- [ ] “partialRender” warnings reduce; unavoidable cases identify offending layer/module.

Risks & Notes
- [ ] Layer rebalancing may disrupt existing baselines—coordinate updates carefully.  
- [ ] Bridge visualization must stay readable; avoid clutter from excessive stubs.  
- [ ] Ensure layer creation aligns with Visio automation constraints (naming, visibility, colors).  
- [ ] Coordinate with Milestone 13 view-mode work so layout fidelity and layer segmentation evolve together.
