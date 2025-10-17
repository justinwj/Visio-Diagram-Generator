**VDG MILESTONE TEN - End-to-End Tests & Fixtures**

Goal
- [ ] Establish deterministic end-to-end coverage from `vba2json` through `VDG.CLI` (with `VDG_SKIP_RUNNER=1`), backed by refreshed fixtures and CI wiring.

Scope
- [ ] Fixtures: curate and lock `hello_world`, `cross_module_calls`, and `events_and_forms` as golden test data.
- [ ] Tests: expand suites to cover
  - `vba2json` shape/metadata expectations.
  - IR → diagram mapping assertions across modes.
  - End-to-end `render` pipeline producing diagram JSON + diagnostics with `VDG_SKIP_RUNNER=1`, asserting threshold behaviour.
- [ ] CI: ensure workflows validate deterministic outputs, compare diagnostics baselines, and run under PowerShell 7.5.3+.

Artifacts / Deliverables
- [ ] Updated fixture documentation describing scenarios and regeneration process.
- [ ] Golden outputs (IR, Diagram JSON, diagnostics, `.vsdx` stubs) under `tests/fixtures` and `samples/`.
- [ ] CI jobs publishing test/fixture artifacts for debugging.

Acceptance Criteria
- [ ] `dotnet test` passes on PowerShell 7.5.3 with deterministic hashes for key fixtures.
- [ ] Render smoke (`tools/render-smoke.ps1`) runs in CI, enforces diagnostics thresholds, and validates baselines without manual intervention.
- [ ] Regression fixtures stay in sync with samples; instructions exist for regenerating when behavior changes.
