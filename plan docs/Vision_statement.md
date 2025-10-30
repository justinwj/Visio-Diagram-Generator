
# Goal(s)
- [ ] Achieve reliable end-to-end `.vsdx` rendering for large VBA datasets (e.g., full `samples/invSys`) without crashes or unusable 99% occupancy layouts.
- [ ] Make “view-mode” diagrams fully informative and actionable for developers: display all modules, forms, procedures, and call edges, with correct connector routing and visual layout.
- [x] Teach the planner/CLI pair to operate on deterministic, page-aware module segments so large fixtures stay readable without dropping nodes.

## Current Traction
- The planner now emits per-node segment assignments and page-aware bridges, allowing the CLI to diagnose pagination and draw stubs without losing parent module context.
- Per-page layout origins are exported alongside segment metadata, giving the renderer deterministic page-local coordinates for multi-page diagrams.
