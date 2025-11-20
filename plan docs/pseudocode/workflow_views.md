# Workflow / Process View Pseudocode Prompts

Workflow diagrams focus on concrete business scenarios (Add Goods Received, Deduct Shipments, etc.) for any VBA-based solution; invSys remains a sample fixture only. Because the set is open-ended, the pseudocode template must be reusable.

## Template Questions (per workflow)
1. **Entry point** – which procedure(s) start the flow? Are they worksheet events, form buttons, or macros?
2. **Scope of expansion** – how deep into the call graph do we go? Include shared utilities?
3. **Business phases** – what named phases do we display (Validate → Calculate → Persist)?
4. **Data touchpoints** – which worksheets/tables/config entries appear?
5. **Error handling** – do we show alternate paths or consolidate into a single “Error Handler” node?
6. **Pagination/layout** – lane mapping (UI vs. Module vs. Data), loops/branches that require special corridor treatment.

## Workflow Catalog (initial set)
- 14. Add Goods Received
- 15. Deduct Shipments
- 16. Login
- 17. Undo/Redo
- 18. Bulk Import
- 19. Reset PIN
- 20. Create/Delete User
- 21. Admin Controls
- 22. Orders Tally

For each workflow we need:
- **Narrative summary** – what business question it answers.
- **Trigger metadata** – control names, worksheet ranges, timer jobs.
- **Success vs. failure paths** – how to highlight branching nodes, e.g., “Table Inventory not found → Exit Sub”.
- **Artifacts to reuse** – fixture slices, screenshots, diagnostics.

## Implementation Hooks
- `ProcedureGraphBuilder` – define slicing logic to isolate workflow-specific subgraphs.
- `Transformations.fs` – convert workflow metadata into `DiagramModel` nodes/edges with labels matching the business steps.
- `ViewModePlanner` / `PagingPlanner` – specify lane assignments (UI/Module/Data), capacity overrides, and channel labeling for loops.
