**VDG MILESTONE ELEVEN - Advanced Modes & Metrics**

Goal
- [x] Deliver richer diagram output (Module Structure, Event Wiring, per-procedure CFG) using the existing Export → Translate → Diagram pipeline, while enriching IR metrics to drive layout hints.

Scope
- [x] Diagram mode expansion (built atop exported VBA sources):
  - [x] Module Structure: lane/containment view of projects.
  - [x] Event Wiring: capture form/control event handlers and wiring.
  - [x] Procedure CFG: per-procedure control-flow diagrams.
- [x] IR enrichment:
  - [x] Inject cyclomatic complexity and source SLOC metrics into IR (calculated from exported code files during translation).
  - [x] Emit diagram hints (weights, grouping) to influence layout/visual cues across new modes.
- [x] Hyperlink metadata: ensure exported fixtures retain file/line anchors so diagrams can point back to sources captured during export; embed links in `.vsdx` outputs and CLI summaries where applicable.

Artifacts / Deliverables
- [x] Updated CLI/Docs covering the new diagram modes and hyperlink metadata emitted during export.
- [x] Revised IR specification/changelog documenting new metrics and hints.
- [x] Golden fixtures + samples for Module Structure, Event Wiring, and CFG outputs (with hyperlinks).
- [x] Automated tests validating hyperlink integrity (file/line resolution), metric accuracy, and mode rendering.

Acceptance Criteria
- [x] Exported VBA sources translated through the existing pipeline produce diagrams with working hyperlinks back to the originating file/line in both `.vsdx` and summary outputs.
- [x] Each new mode renders via CLI, passes schema validation, and has deterministic fixtures.
- [x] IR metrics (cyclomatic, SLOC) appear in the enriched IR and influence diagram hints as intended.
- [x] CI exercises VBIDE-less regression story (mocked extraction) and the new mode fixtures without manual intervention.
- [x] Automated tests confirm hyperlink targets remain valid as fixtures evolve.

Backlog
- Live VBIDE automation with round-trip hyperlinks (convenience feature) - revisit after validating the enhanced export-driven diagrams.

Compatibility Note
- Current focus: Excel VBA export fixtures (e.g., `samples/invSys`). Future support roadmap includes Visio, Project, and PowerPoint exports using the same pipeline.

Phase 1 (Fixtures & Anchors)
- [x] Curate `samples/invSys` exports that cover module structure, event wiring, and per-procedure logic scenarios.
- [x] Extend the exporter to record file path, module name, and line number metadata for every procedure/control.
- [x] Generate golden fixtures (`.bas`, `.cls`, enriched IR) with precomputed cyclomatic + SLOC metrics.

Phase 2 (CLI & Pipeline Prototypes)
- [x] Update the CLI pipeline so each new mode ingests the enriched fixtures, emits diagram JSON with anchors/metric hints, and produces `.vsdx` files containing embedded hyperlinks.
- [x] Include a CLI summary/log section that lists hyperlink targets for validation.

Phase 3 (Validation & Documentation)
- [x] Add automated tests (e.g., in `ParserSmokeTests`) that verify diagram hyperlinks resolve to the correct file/line and that IR metrics match expectations.
- [x] Document the new modes and hyperlink behaviour (quick start + reference), summarising metric fields and anchor presentation for end users.
