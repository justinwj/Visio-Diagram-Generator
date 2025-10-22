**VDG MILESTONE TEN - End-to-End Tests & Fixtures**

Goal
- [x] Establish deterministic end-to-end coverage from `vba2json` through `VDG.CLI` (with `VDG_SKIP_RUNNER=1`), backed by refreshed fixtures and CI wiring.

Scope
- [x] Fixtures: curate and lock `hello_world`, `cross_module_calls`, and `events_and_forms` as golden test data.
  - [x] Snapshot VBA exports plus regeneration metadata (source commit, command lines) in `plan docs/fixtures_metadata.json` (authored by `tools/render-fixture.ps1 -Update`).
  - [x] Capture golden IR (`*.ir.json`), diagram (`*.diagram.json`), diagnostics (`*.diagnostics.json`), and rendered `.vsdx` stubs under `tests/fixtures` / `samples`.
  - [x] Author `tools/render-fixture.ps1` for regenerate/check flows, emitting SHA256 hashes + diff hints.
- [x] Tests: expand suites to cover regression paths end-to-end.
  - [x] Maintain a “test mode matrix” asserting both `callgraph` and `module-structure` render modes in diagram mapping coverage.
  - [x] `vba2json` shape/metadata expectations validated via deterministic hash comparisons.
  - [x] IR to diagram mapping assertions across callgraph and module-structure modes.
  - [x] `render` helper smoke (with `VDG_SKIP_RUNNER=1`) producing diagram JSON + diagnostics, asserting threshold behaviour and artifact presence.
  - [x] Hash verification helpers shared across tests to detect fixture drift.
- [x] CI: ensure workflows validate deterministic outputs, compare diagnostics baselines, and run under PowerShell 7.5.3+.
  - [x] Add dedicated `render-fixtures` job invoking the new script in check mode.
  - [x] Publish fixture artifacts (`*.ir.json`, `*.diagram.json`, diagnostics, `.vsdx`) on workflow failure for diffing.
  - [x] Gate on hash drift (job fails whenever hashes change).

Artifacts / Deliverables
- [x] Updated fixture documentation describing scenarios, hash expectations, regeneration process, and troubleshooting for hash mismatches in `docs/FixtureGuide.md` (linked from `docs/rendering.md`).
- [x] Golden outputs (IR, Diagram JSON, diagnostics, `.vsdx` stubs) under `tests/fixtures` and `samples/`.
- [x] `tools/render-fixture.ps1` helper (check vs regenerate) with hashes plus timestamp ledger (`plan docs/fixtures_log.md`).
- [x] CI jobs publishing test/fixture artifacts for debugging and linking to the ledger.
- [x] Baseline policy note: intentional fixture updates are self-reviewed, documented alongside ledger entries, and CI is trusted to report any drift for manual inspection-no extra peer review gate.

Acceptance Criteria
- [x] `dotnet test` passes on PowerShell 7.5.3 with deterministic hashes for key fixtures.
- [x] Render smoke (`tools/render-smoke.ps1`) runs in CI, enforces diagnostics thresholds, and validates baselines without manual intervention.
- [x] Regression fixtures stay in sync with samples; instructions exist for regenerating when behavior changes.
- [x] Fixture regeneration script reports no drift (hash matches) in CI unless `-Update` was executed intentionally.
- [x] Any failed CI or local check publishes expected vs actual hashes and diff hints for key JSON payloads.
- [x] Outside of the deliberate `-Update` workflow, fixture sync remains fully automated in CI and local scripts (no manual patching).
