# Immediate Plan (delete below lists when done, this MD should not get long)

## Current Tactic
- Stop chasing full `invSys` fan-out; validate end-to-end rendering on a single representative subroutine first.
- Keep the original strategy gates in mind: taxonomy + semantic tags must still be correct for the slice we render.

## Slice-First Checklist
1. **Target selection** – Pick an `invSys` subroutine with enough calls to exercise bundling but small enough (<50 connectors) to reason about. Capture its module, dependencies, and semantic tags in `fixtures_metadata.json`. *(Chosen: `modInvMan.AddGoodsReceived_Click`.)*
2. **Fixture extraction** – Trim the IR/diagram JSON so it only contains the chosen subroutine, its immediate callers/callees, and required globals. Commit this as `fixtures/minimal_invSys_<name>.json`. *(Tracked at `fixtures/minimal_invSys_AddGoodsReceived.diagram.json` via `tools/slice_diagram_fixture.py` + metadata entry `invSys.AddGoodsReceived`.)*
3. **Planner focus pass** – Run the F# planner on the trimmed fixture with capacity caps enabled, log emitted `EdgeChannel` metadata, and diff against expectations. *(First code tweak landed: modules now blend actual node positions with corridor slots so exits aren’t stacked at a single point.)*
4. **CLI render loop** – Drive Visio with the new payload until the subroutine layout is visually correct (connectors land in the right slots, no skipped edges). Capture diagnostics + screenshots. *(Runner now honors route endpoints on both source + target sides and exposes `layout.view.skipLaneContainers=true` to drop the redundant tier frame, so renders should start matching the planner anchors.)*
5. **Visio-specific hardening (runner)** – Disable auto-routing/snap/glue, bind connector labels/callouts to emitted geometry, enforce semantic module ordering, and guarantee arrow directionality (points ordered source→target and suppress start arrows) so Visio stops overriding planner intent.
6. **Regression hooks** – Add/adjust unit tests around the planner output for this subroutine and wire the fixture into the existing render scripts so we can grow from this baseline.

## Exit Criteria
- One subroutine renders with zero skipped connectors, deterministic pagination, and explainable corridor assignments.
- Diagnostics + documentation note the exact input, planner options, and Visio toggles needed so the slice can expand later.
