# Immediate Plan (delete below lists when done, this MD should not get long)

Generated: 2025-11-10  
Last milestone completed: Seed overrides and advanced layout metadata integration (semantic review surfaces, planner cues, fixture hashes) finished 2025-11-09. This plan reflects the post-71f5afc status before any new development starts.

## 1. Advanced Layout Wiring & Fixture Determinism (**fixtures refreshed – awaiting Visio validation**)
- **Purpose**: Finish the planner/runner wiring described in `plan docs/advanced_layout_scenarios.md` so lane segments, heat bands, overflow badges, flow bundles, cycle legends, and page-context cues materialize deterministically in both `.vsdx` and `.review.*`, aligning with the remaining Gate 0 goals in `plan docs/Vision_statement.md`.
- **Current Status (2025-11-11)**:
  - Planner/CLI now emit `pageContexts`, `laneSegments`, `flowBundles`, and deterministic advanced cues.
  - All canonical fixtures (hello_world, cross_module_calls, events_and_forms, invSys modes) regenerated with advanced mode; ledger/metadata hashes updated.
  - Diagnostics and review artifacts reflect the new cues; CI goldens match.
- **Next Steps**:
  - Run Visio automation on both desktop tower and Zenbook Duo to confirm heat bands, badges, and context notes render identically outside `VDG_SKIP_RUNNER`.
  - Capture validation notes/screenshots into `inspect_output.ps1` (or dedicated log) and confirm no connector/shape drift.
- **Proposed Owners**: Planner/CLI wiring – Codex (done); Fixture refresh – tools automation (done); Visio validation – Justin + Codex.
- **Done When**: Both Windows hosts render the refreshed fixtures with matching cues and no drift; validation is logged for the milestone review.
- **References**: `plan docs/advanced_layout_scenarios.md`, `docs/AdvancedLayouts.md`, `plan docs/fixtures_log.md`, `inspect_output.ps1`.
- **Constraints**: Visio automation requires Windows with desktop MSBuild and Office VISLIB; headless validation may use `VDG_SKIP_RUNNER`, but final sign-off must include real Visio runs.

## 2. Reviewer Dashboard & Reporting Expansion
- **Purpose**: Extend the reviewer dashboard so advanced layout cues, severity thresholds, and review hashes surface centrally, keeping stakeholders aligned without opening Visio (see `plan docs/review_dashboard.md`, `plan docs/review_threshold_switches.md`).
- **Next Steps**:
  - Teach `tools/summarize-reviews.ps1` (or successor) to ingest `advancedPages` + hash data from `plan docs/fixtures_metadata.json`.
  - Add severity/confidence/flow cutoff context and suppression notes to the dashboard rows.
  - Ensure CI uploads the refreshed dashboard artifact on both desktop and Zenbook builds.
- **Proposed Owners**: Review tooling – Codex + reviewer portal maintainer.
- **Done When**: Dashboard lists per-diagram advanced cues, thresholds, and hash checks; reviewers can trace any warning/error directly to `.review.*`.
- **References**: `plan docs/review_dashboard.md`, `plan docs/review_threshold_switches.md`, `.review.*` artifacts beside fixtures.
- **Constraints**: Dashboard generation must stay deterministic—scripts should run with `VDG_SKIP_RUNNER=1` and pinned timestamps unless explicitly overridden.

## 3. Regression & CI Coverage for New Edge-Case Goldens
- **Purpose**: Lock in goldens/tests for the new stress fixtures called out in the advanced roadmap (dense UI overflow, cycle hotspots, fan-out integrations) plus traditional regression suites (`tests/VDG.*`, `tools/render-fixture.ps1`).
- **Next Steps**:
  - Capture IR/diagram/diagnostics/review outputs for the new fixtures; append ledger rows with SHA256 hashes.
  - Add targeted unit tests (e.g., `AdvancedLayoutPlannerTests`) covering soft/hard limits, flow bundle aggregation, cycle collapse determinism.
  - Wire CI jobs so Linux/WSL agents run planner/CLI tests (`dotnet test`, `pwsh ./tools/render-fixture.ps1 -FixtureName <…>` w/ `VDG_SKIP_RUNNER=1`) while Windows agents validate Visio automation paths.
- **Proposed Owners**: Test authoring – Codex; CI integration – DevOps/automation maintainers.
- **Done When**: New fixtures live under `tests/fixtures/render/**`, CI runs pass across both host types, and `plan docs/fixtures_log.md` reflects the refresh with rationale notes.
- **References**: `plan docs/advanced_layout_scenarios.md` (Golden & Testing sections), `plan docs/fixtures_log.md`, `plan docs/fixtures_metadata.json`, `docs/FixtureGuide.md`.
- **Constraints**: Never bypass the drift guardrail in `plan docs/drift_check.md`; Windows Visio runs must execute on the desktop or Zenbook with VISLIB in place.

## 4. Reviewer Documentation & Quickstart Refresh
- **Purpose**: Keep onboarding docs (`docs/AdvancedLayouts.md`, `docs/Design.md`, reviewer quickstarts) synchronized with the latest CLI switches, review outputs, and dashboard cues so reviewers and AI contributors avoid terminology drift.
- **Next Steps**:
  - Document the advanced legend cues and suppression behavior directly in reviewer quickstarts and `.review.txt` template notes.
  - Highlight the new CLI switches (`--layout-advanced-mode`, `--layout-lane-soft-limit`, etc.) plus review threshold flags in `docs/VDG_VBA_CLI.md` and README quickstarts.
  - Capture environment-specific guidance (Windows-only Visio automation vs. cross-platform planner/testing) in the docs to replace the struck cross-platform section in `plan docs/strategy.md`.
- **Proposed Owners**: Documentation – Codex with reviewer stakeholders for approval.
- **Done When**: Docs mention every active flag, cue, and workflow; onboarding materials reference ledger/review dashboards and align with the terminology enforced by tests.
- **References**: `docs/AdvancedLayouts.md`, `docs/Design.md`, `docs/VDG_VBA_CLI.md`, `plan docs/strategy.md`.
- **Constraints**: Use consistent vocabulary already present in review outputs (`ReviewSummary`, `advancedPages`, `laneSegments`) to avoid rewording that could desync dashboards/tests.

## 5. Planner Architecture & Metadata Backlog
- **Purpose**: Close the remaining Gate 0 items in `plan docs/Vision_statement.md` and the planner guardrails in `plan docs/strategy.md`—notably EdgeChannel metadata, corridor tests, capacity-limit enforcement, and “zero skipped connectors” diagnostics.
- **Next Steps**:
  - Implement `EdgeChannel` metadata propagation end-to-end and add verification tests before layout execution.
  - Expand corridor/capacity test coverage (lane spillover, bundle generation, pagination decisions) per `plan docs/strategy.md` §Testing & Tooling.
  - Keep diagnostics proving zero skipped connectors for `invSys` once spillover/bundling is finalized; attach evidence to fixture notes.
- **Proposed Owners**: Planner + diagnostics – Codex (coord with F# maintainers).
- **Done When**: EdgeChannel metadata exists in IR→planner→runner outputs, corridor tests cover the cases called out in the vision, and diagnostics show zero skipped connectors on flagship fixtures.
- **References**: `plan docs/Vision_statement.md`, `plan docs/strategy.md`, `docs/PagingPlanner.md`, `plan docs/progress.md`.
- **Constraints**: Maintain deterministic, seed-aware behavior—respect `--taxonomy-seed`, `--seed-mode`, and record any overrides in review metadata for auditability.

_Pause here_: No coding tasks should proceed until this plan is reviewed/validated against the current repository state and reviewer expectations.
