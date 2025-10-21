**VDG MILESTONE TWELVE - Scalable Multi-Page Visio Export & Diagnostics**

Goal
- [ ] Achieve reliable end-to-end `.vsdx` rendering for large VBA datasets (e.g., full `samples/invSys`) without crashes or unusable 99% occupancy layouts.

Scope
- [x] Harden multi-page layout so Visio pages are created and tracked before any shape/connector placement.
- [x] Introduce smarter pagination and lane/module partitioning when occupancy or connector counts exceed thresholds.
- [ ] Extend diagnostics to gracefully report overflow/partial outputs instead of throwing exceptions.
- [ ] Add CLI filtering options so users can render subsets or “page through” very large exports.
- [ ] Validate outputs by rendering `samples/invSys` end-to-end and capturing success in CI artifacts.
- [ ] Define and meet performance/telemetry targets (render completion < 5 minutes, working-set < 500 MB, progress reported every ≤1 s).

Milestone Breakdown
- **Milestone A – Reliable Multi-Page Layout**
  - [x] Audit `VDG.CLI` paging branch; ensure connectors never target null pages.
  - [x] Create/track Visio page objects ahead of placement; update `DrawConnectorsPaged` & `DrawMultiPage`.
  - [ ] Add unit/CLI tests covering multi-page traversal.
- **Milestone B – Crowding Mitigation & Paging Strategy**
  - [x] Implement automatic lane/module splitting when occupancy exceeds limits.
  - [x] Refine paging planner heuristics (height-aware grouping, connector caps, slack) and add algorithm tests.
  - [ ] Chunk densely connected modules across additional pages based on connector counts.
  - [x] Surface layout decisions in diagnostics metadata.
- **Milestone C – Diagnostics & Graceful Degradation**
  - [ ] Extend diagnostics to flag overflowed modules/pages, log partial outputs, and avoid exceptions.
  - [x] Emit summary annotations (overflow module list, skipped connectors) in CLI output.
- [ ] Establish an error-handling policy (partial diagrams allowed, skipped connectors logged, documented fallback heuristics).
- **Milestone D – User-Facing Filtering**
  - [ ] Add CLI parameters for module/page subsets (e.g., `--modules`, `--max-pages`).
  - [ ] Support inclusive/exclusive filters (`--modules include m1,m3`, `--modules exclude m2`), and clarify interaction with pagination in docs.
  - [ ] Update docs with guidance on paging/filter workflows.
  - [ ] Add regression tests covering filtered renders.
- **Milestone E – Validation & CI Integration**
  - [ ] Render `samples/invSys` end-to-end to `.vsdx` without errors.
  - [ ] Add CI smoke that runs the multi-page render (with `VDG_SKIP_RUNNER` disabled on Windows agent).
  - [ ] Capture diagnostics/log artifacts proving successful pagination.

Risks & Open Questions
- Visio automation API limits (page count, connector performance) may require batching or throttling.
- Need a Windows CI agent with Visio installed for automated full renders.
- Long-running renders must expose progress events (`--progress-interval-ms`) and respect CLI timeouts (`--timeout-ms`) to avoid runaway jobs.
- Document fallback plans when Visio is unavailable (local dev can rely on JSON output; CI step can be optional when license isn’t present).
