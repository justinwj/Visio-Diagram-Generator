**VDG MILESTONE SEVEN — vba2json Sidecar CLI**

Goal
- [x] Ship a standalone `vba2json` CLI that ingests exported VBA modules and emits IR v0.1 JSON compatible with downstream tooling.

Scope
- [x] CLI accepts exported `.bas`, `.cls`, `.frm` inputs individually or via `--glob`.
- [x] Output defaults to stdout with optional `--out`.
- [x] Flags supported: `--project-name`, `--glob`, `--infer-metrics`, `--root`.
- [x] IR envelope remains schema v0.1; module merges maintain unique identifiers.
- [x] Parser MVP extracts procedure signatures (name, params, return) and call relationships.
- [x] Optional metrics toggle computes line counts for modules/procedures when enabled.
- [x] CLI reports recoverable errors to stderr with actionable messages; hard failures exit non-zero.

Out of Scope
- [x] Rich VBA grammar or full VBIDE integration (future milestones).
- [x] Advanced metrics (cyclomatic complexity, code churn, etc.).
- [x] GUI or interactive prompts.

Artifacts
- [x] Updated CLI docs / README snippet covering new flags.
- [x] `docs/VBA_IR.md` or addendum noting metrics toggle behavior.
- [x] Smoke tests under `tests/VDG.VBA.CLI.Tests` demonstrating glob/multi-file usage and metrics output.
- [x] Example outputs under `tests/fixtures/ir/` if fixtures change.
- [x] CI job adjustments if new validation steps are required.

Work Breakdown
1. **CLI Flag Wiring & Input Handling**
   - [x] Add support for `--project-name`, `--glob`, `--infer-metrics`, `--root`.
   - [x] Implement glob expansion and multi-file ingestion.
   - [x] Ensure merged modules retain unique IDs; detect collisions and surface descriptive errors.
2. **Output Handling**
   - [x] Default to stdout; honor `--out` for file writes.
   - [x] Preserve schema v0.1 and keep validator compatibility.
3. **Metrics Toggle**
   - [x] When `--infer-metrics` is supplied, populate line counts for modules/procedures.
   - [x] Keep the field omitted by default to minimize JSON noise and document future metric slots.
4. **Root Path Normalization**
   - [x] Apply `--root` to normalize file paths within the IR (relative paths preferred).
5. **Parser Enhancements**
   - [x] Confirm signature extraction handles params/return types for all supported procedure kinds.
   - [x] Expand call detection to handle multi-file projects (respect module name collisions).
6. **Testing**
   - [x] Extend smoke tests to cover globbed inputs, metrics toggle, and `--out` behavior.
   - [x] Validate output through `tools/ir-validate.ps1`.
   - [x] Adopt consistent fixture/test naming for sidecar scenarios (e.g., `sidecar-glob-metrics.json`).

Acceptance Criteria
- [x] CLI supports the new flags with documented behavior.
- [x] Multi-file projects produce a single, schema-valid IR via glob or explicit file list.
- [x] Metrics are included only when requested and match existing line-count semantics.
- [x] Tests cover CLI scenarios (single file, glob, metrics on/off, stdout vs `--out`).
- [x] Documentation updated for milestone completion and user onboarding.

Risks & Mitigations
- Glob patterns may introduce inconsistent ordering → normalize by sorting file paths prior to processing.
- Relative path mapping could leak absolute paths → ensure `--root` defaults to current directory when unspecified.
- Metrics toggle might drift from schema → guard with unit tests and schema validation in CI.
- Regex/token heuristics are limited → document MVP coverage and add fixtures for multiline/nested constructs over time.
- Mixed glob patterns (relative vs absolute) may surface path bugs → include tests covering both forms.
