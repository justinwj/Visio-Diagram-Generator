**VDG MILESTONE SIX — Define VBA IR + Versioning**

Goal
- [x] Produce a documented, versioned, and test‑validated JSON IR for VBA projects that downstream tools can convert into diagram JSON and render via VDG.

Scope
- [x] IR design and JSON Schema (v0.1).
- [x] Examples and fixtures demonstrating the IR.
- [x] Validation tooling (script) and guidance for CI checks.
- [x] Documentation for mapping IR → diagram JSON.
- [x] Initial basic metrics fields (lines, cyclomatic optional).
- [x] Explicit versioning and evolution policy.
- [x] Initial symbol table + robust parsing foundation (params/returns, local types, With-block tracking).

Out of Scope (this milestone)
- [ ] Full VBA parser implementation (beyond skeleton).
- [ ] IR→diagram converter full implementation (beyond skeleton).
- [ ] Live VBIDE extraction (tracked later).

Artifacts
- [x] `docs/VBA_IR.md` (spec + mapping guidelines + FAQ).
- [x] `shared/Config/vbaIr.schema.json` (JSON Schema draft 2020‑12, version 0.1).
- [x] `tests/fixtures/vba/*` (tiny mock projects: exported .bas/.cls/.frm).
- [x] `tests/fixtures/ir/*` (golden IR JSONs for each fixture).
- [x] `tools/ir-validate.ps1` (optional schema validator for CI/local).
- [x] `docs/CHANGELOG_IR.md` (versioning and deprecation log; starts at v0.1).
- [x] `tools/vba2json.ps1` (skeleton extractor: file walk + simple signatures and calls).
- [x] `tools/vba-ir2diagram.ps1` (skeleton IR→diagram converter for project call graphs).

IR Surface (v0.1)
- project
  - `name`, `version?`, `modules[]`.
- module
  - `id` (stable), `name`, `kind` (`Module|Class|Form`), `file`, `attributes?[]`, `procedures[]`.
- procedure
  - `id`, `name`, `kind` (`Sub|Function|PropertyGet|PropertyLet|PropertySet`), `access` (`Public|Private|Friend`), `static?`,
    `params[] { name, type?, byRef? }`, `returns?`, `locs { file, startLine, endLine }`, `calls[]`, `metrics { cyclomatic?, lines? }`, `tags?[]`.
- call
  - `target` (`Module.Proc`), `isDynamic?` (e.g., `CallByName`, `Application.Run`), `site { module, file, line }`.

Conventions
- [x] Field casing lowerCamel; enum casing Pascal.
- [ ] IDs: `Module.Proc` unique; modules unique by `name` within project.
- [ ] Optional fields omitted (not null); additive evolution preferred.
- [ ] Stable ordering for determinism (modules, then procedures by name).

Versioning
- [ ] `irSchemaVersion`: `0.1` (SemVer `major.minor`).
- [ ] Minor = additive only; tooling ignores unknown fields.
- [ ] Major = breaking changes with migration notes in `docs/CHANGELOG_IR.md`.
- [ ] Embed generator metadata: `{ generator: { name: "vba2json", version: "x.y.z" } }`.

Mapping Guidance (IR → Diagram JSON)
- [ ] First mode: Project Call Graph.
  - Node: `Module.Proc` (label same); Container: module; Tier by artifact type: Forms | Classes | Modules.
  - Edge: `call`; carry `call.site` into `edges[].metadata`.
  - Carry IR context into `nodes[].metadata` (`code.module`, `code.proc`, `code.kind`, `code.access`, `code.locs`).

Validation & Tests
- [x] Fixtures in `tests/fixtures/vba/`:
  - `hello_world` (1 module / 1 Sub)
  - `cross_module_calls` (2–3 modules; crossing calls)
  - `events_and_forms` (form + handler; class module)
- [x] Golden IRs in `tests/fixtures/ir/`.
- [ ] Schema validation tests (IR conforms to `vbaIr.schema.json`).
- [ ] Determinism tests (stable IDs/ordering).
- [ ] Minimal content checks (modules, procedures, calls exist as expected).
- [ ] Optional: `tools/ir-validate.ps1` used by a CI step.

Acceptance Criteria
- [ ] Spec and schema published with examples and FAQ; repository lints/validates them in CI.
- [x] Three fixtures produce IRs that validate (via `tools/ir-validate.ps1`).
- [x] IR contains sufficient data to draw a project call graph with VDG.
- [x] Versioning policy documented; tools tolerate unknown fields.
- [x] CLI can render call graph from sources in one step (`render`).

Work Breakdown
1) Spec & Schema
- [x] Draft `docs/VBA_IR.md` with entities, enums, and examples.
- [x] Implement `shared/Config/vbaIr.schema.json`.
- [x] Provide canonical sample IR (2 modules, 1 crossing call).
2) Fixtures & Golden IR
- [x] Create minimal `.bas/.cls/.frm` fixtures under `tests/fixtures/vba/`.
- [x] Author expected IR JSONs under `tests/fixtures/ir/`.
3) Validation Tooling
 - [x] Add `tools/ir-validate.ps1` (schema validate + friendly errors).
 - [x] Wire a CI job to validate golden IRs.
4) Documentation
 - [x] `docs/CHANGELOG_IR.md` scaffold with v0.1 entry.
 - [x] IR FAQ (IDs, dynamic calls, property procedures, line numbers).
5) Governance
 - [ ] Define PR guidance for IR changes (additive only for minors; deprecations policy).
6) Parser Hardening & Symbol Table (Step 3)
- [x] Extract params (name/type/ByRef) and returns from signatures.
- [x] Track local variable types and `Set … = New …` constructions.
- [x] With-block awareness for `.Method()` target typing.
- [x] Rewrite qualified calls `obj.Method` to `Type.Method` when `obj` type is known.
7) Expand Call Detection (Step 1)
- [x] Unqualified call detection at line start (`Call Proc(...)`, `Proc(...)`).
- [x] Additional patterns (aliases, chained calls) - basic alias + chained-call heuristics wired into v0.1 parser.
8) New Diagram Modes (Step 2)
- [x] `module-callmap` (module-level call aggregation).
- [x] `event-wiring` (Form control events → handler procedures).
 - [x] `proc-cfg` (per-procedure CFG) — MVP: Start→Calls→End sequence; control branching planned.

6) Scaffolds (optional within this milestone)
- [x] Add `tools/vba2json.ps1` skeleton (signatures/calls, not a full parser).
- [x] Add `tools/vba-ir2diagram.ps1` skeleton (project call graph mapping).
- [ ] Wire a sample end-to-end doc snippet (vba2json → ir-validate → ir2diagram → VDG.CLI).

Risks & Mitigations
- Ambiguity for dynamic calls → mark `isDynamic`, record call site; resolve later if possible.
- Procedure identity drift → standardize `Module.Proc`, suffix stable index on collision (document rule).
- Forms metadata variance → treat form modules as `kind: "Form"`; use `attributes[]` for edge cases.

Timeline (suggested)
- [ ] Day 1–2: Draft spec + schema + examples.
- [ ] Day 3: Fixtures + golden IRs + validator + CI.
- [ ] Day 4: Review/adjust; finalize v0.1; publish docs.
