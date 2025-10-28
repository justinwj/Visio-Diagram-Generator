**VDG MILESTONE 12.1 – Diagnostics Hardening & Crowding Tuning**

Goal  
Stabilise paging/occupancy diagnostics so high-density datasets (e.g. `invSys`) produce actionable, trustworthy warnings without drowning in 99 % lane/page alerts, paving the way for Milestone 13 view-mode fidelity work.

- [x] Diagnostics audit  
  - [x] Trace every code-path that sets `LaneCrowding*`, `PageCrowding*`, `PartialRender`, and `Skipped*` markers in `VDG.CLI` and the F# planners.  
  - [x] Catalogue current thresholds (defaults, metadata overrides, env vars) and the heuristics that lead to 99 % occupancy spam.
- [ ] Heuristic tuning  
  - [ ] Adjust F# planner defaults (spacing, segmentation ratios, connector caps) to reduce noise while preserving safety.  
  - [ ] Evaluate per-lane density metrics for Forms/Sheets/Classes vs Modules; introduce calibrated defaults or tier-specific multipliers if required.
- [ ] Fixture-specific overrides  
  - [ ] Introduce deterministic overrides (e.g. `tests/fixtures/config/invSys/...`) to exercise edge cases without polluting global defaults.  
  - [ ] Document override intent so the ledger explains why a fixture tolerates certain mitigations.
- [x] Diagnostics UX improvements  
  - [x] Ensure CLI summary warnings clearly rank severity (error vs warning) and link to `docs/ErrorHandling.md`.  
  - [x] Capture mitigations in diagnostics JSON with explicit reason codes (`crowding`, `split`, `filtered`, etc.).
- [ ] Documentation & guidance  
  - [ ] Extend `docs/ErrorHandling.md`, `docs/PagingPlanner.md`, and `docs/FixtureGuide.md` with updated thresholds, override playbooks, and mitigation workflow.  
  - [ ] Provide a quick checklist for contributors to follow before updating fixture baselines.
- [ ] Automation alignment  
  - [ ] Update `render-smoke` baseline and `fixtures_metadata.json` once tuning is validated.  
  - [ ] Ensure CI wrestles the same thresholds (env vars, overrides) so warnings do not flip unexpectedly.

Milestone Breakdown
1. **Diagnostics Inventory**  
   - [x] Trace log/JSON emission points; map them to planner stats.  
   - [x] Capture a before snapshot from `invSys` (callgraph) and at least one smaller fixture (`events_and_forms`) for comparison.
2. **Heuristic Experiments**  
   - [ ] Prototype lane spacing and segmentation tweaks in F#; validate via targeted `render` runs (not full baseline yet).  
   - [ ] Adjust CLI defaults or metadata fallbacks if experiments show broad improvement.
3. **Fixture Overrides & Guardrails**  
   - [ ] Add/refresh overrides where necessary (e.g., forcing bigger page height) and document in the ledger note.  
   - [ ] Guarantee overrides merge deterministically and are reviewed alongside fixture updates.
4. **Docs & Tooling Updates**  
   - [ ] Refresh error-handling doc with the tuned behaviour and decision tree.  
   - [ ] Add troubleshooting FAQ (e.g., “Why do I still see partial renders?”).
5. **Baseline Refresh & CI Confirmation**  
   - [ ] Re-run `tools/render-fixture.ps1 -FixtureName invSys` (and other affected fixtures) after tuning.  
   - [ ] Capture new diagnostics hashes/metadata and verify `render-smoke.ps1` stays within tolerance.

Acceptance Criteria
- [ ] `invSys` (callgraph) fixture runs with either no lane/page errors or clearly justified warnings; mitigations are limited, intentional, and documented.  
- [x] Diagnostics JSON and CLI output distinguish between crowding, overflow, and override scenarios with reason codes.  
- [ ] Updated documentation describes the thresholds, override workflow, and expectation for `partialRender`.  
- [ ] `render-smoke.ps1` baseline reflects the tuned diagnostics, and CI jobs (including optional Visio smoke) pass without unexpected warning spam.  
- [ ] A regression checklist (in docs or plan) records how to validate diagnostics after future planner/layout changes.

Risks / Notes
- [ ] Tuning thresholds too aggressively could hide genuine overflow issues; keep before/after diffs for reference.  
- [ ] Planner changes may affect upcoming Milestone 13 work—coordinate to avoid conflicting heuristics.  
- [ ] Ensure overrides do not mask bugs; each override needs a note in the fixture ledger with rationale.
