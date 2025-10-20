**VDG MILESTONE ELEVEN - Advanced Modes & Metrics**

Goal
- [ ] Deliver richer diagram output (Module Structure, Event Wiring, per-procedure CFG) using the existing Export → Translate → Diagram pipeline, while enriching IR metrics to drive layout hints.

Scope
- [ ] Diagram mode expansion (built atop exported VBA sources):
  - [ ] Module Structure: lane/containment view of projects.
  - [ ] Event Wiring: capture form/control event handlers and wiring.
  - [ ] Procedure CFG: per-procedure control-flow diagrams.
- [ ] IR enrichment:
  - [ ] Inject cyclomatic complexity and source SLOC metrics into IR (calculated from exported code files during translation).
  - [ ] Emit diagram hints (weights, grouping) to influence layout/visual cues across new modes.
- [ ] Hyperlink metadata: ensure exported fixtures retain file/line anchors so diagrams can point back to sources captured during export; embed links in `.vsdx` outputs and CLI summaries where applicable.

Artifacts / Deliverables
- [ ] Updated CLI/Docs covering the new diagram modes and hyperlink metadata emitted during export.
- [ ] Revised IR specification/changelog documenting new metrics and hints.
- [ ] Golden fixtures + samples for Module Structure, Event Wiring, and CFG outputs (with hyperlinks).
- [ ] Automated tests validating hyperlink integrity (file/line resolution), metric accuracy, and mode rendering.

Acceptance Criteria
- [ ] Exported VBA sources translated through the existing pipeline produce diagrams with working hyperlinks back to the originating file/line in both `.vsdx` and summary outputs.
- [ ] Each new mode renders via CLI, passes schema validation, and has deterministic fixtures.
- [ ] IR metrics (cyclomatic, SLOC) appear in the enriched IR and influence diagram hints as intended.
- [ ] CI exercises VBIDE-less regression story (mocked extraction) and the new mode fixtures without manual intervention.
- [ ] Automated tests confirm hyperlink targets remain valid as fixtures evolve.

Backlog
- Live VBIDE automation with round-trip hyperlinks (convenience feature) - revisit after validating the enhanced export-driven diagrams.

Compatibility Note
- Current focus: Excel VBA export fixtures (e.g., `samples/invSys`). Future support roadmap includes Visio, Project, and PowerPoint exports using the same pipeline.

Phase 1 (Fixtures & Anchors)
- [ ] Curate `samples/invSys` exports that cover module structure, event wiring, and per-procedure logic scenarios.
- [ ] Extend the exporter to record file path, module name, and line number metadata for every procedure/control.
- [ ] Generate golden fixtures (`.bas`, `.cls`, enriched IR) with precomputed cyclomatic + SLOC metrics.

Phase 2 (CLI & Pipeline Prototypes)
- [ ] Update the CLI pipeline so each new mode ingests the enriched fixtures, emits diagram JSON with anchors/metric hints, and produces `.vsdx` files containing embedded hyperlinks.
- [ ] Include a CLI summary/log section that lists hyperlink targets for validation.

Phase 3 (Validation & Documentation)
- [ ] Add automated tests (e.g., in `ParserSmokeTests`) that verify diagram hyperlinks resolve to the correct file/line and that IR metrics match expectations.
- [ ] Document the new modes and hyperlink behaviour (quick start + reference), summarising metric fields and anchor presentation for end users.
