**VDG MILESTONE SIX — Define VBA IR + Versioning**

Goal
- [x] Produce a documented, versioned, and test‑validated JSON [IR](/docs/Glossary.md#ir) for VBA projects that downstream tools can convert into diagram JSON and render via [VDG](/docs/Glossary.md#vdg).

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
- [x] IDs: `Module.Proc` unique; modules unique by `name` within project. (Guarded by `ParserSmokeTests.GoldenIrFixturesValidateAgainstSchema`.)
- [x] Optional fields omitted (not null); additive evolution preferred. (See `ParserSmokeTests.Vba2JsonMatchesGoldenFixture`.)
- [x] Stable ordering for determinism (modules, then procedures by name). (See `ParserSmokeTests.Vba2JsonSortsModulesAndProcedures`.)

Versioning
- [x] `irSchemaVersion`: `0.1` (SemVer `major.minor`). (`ParserSmokeTests.Vba2JsonEmitsRequiredFields`)
- [x] Minor = additive only; tooling ignores unknown fields. (`docs/IR_Governance.md`)
- [x] Major = breaking changes with migration notes in `docs/CHANGELOG_IR.md`.
- [x] Embed generator metadata: `{ generator: { name: "vba2json", version: "x.y.z" } }`.

Mapping Guidance (IR → Diagram JSON)
- [x] First mode: Project Call Graph.
  - Node: `Module.Proc` (label same); Container: module; Tier by artifact type: Forms | Classes | Modules.
  - Edge: `call`; carry `call.site` into `edges[].metadata`. (Validated by `ParserSmokeTests.CrossModuleCalls_Appear_In_Callgraph_Diagram`)
  - Carry IR context into `nodes[].metadata` (`code.module`, `code.proc`, `code.kind`, `code.access`, `code.locs`). (Validated by `ParserSmokeTests.CrossModuleCalls_Appear_In_Callgraph_Diagram`)

Validation & Tests
- [x] Fixtures in `tests/fixtures/vba/`:
  - `hello_world` (1 module / 1 Sub)
  - `cross_module_calls` (2–3 modules; crossing calls)
  - `events_and_forms` (form + handler; class module)
  - `alias_and_chain` (factory returns + alias/chained calls)
  - `cfg_shapes` (If/Else + For loop scaffolds)
  - `cfg_nested` (loop containing branch; nested CFG coverage)
- [x] Golden IRs in `tests/fixtures/ir/`.
- [x] Schema validation tests (IR conforms to `vbaIr.schema.json`). (`ParserSmokeTests.GoldenIrFixturesValidateAgainstSchema`)
- [x] Determinism tests (stable IDs/ordering). (`ParserSmokeTests.Vba2JsonOutputIsDeterministic`)
- [x] Minimal content checks (modules, procedures, calls exist as expected). (`ParserSmokeTests.Vba2JsonEmitsRequiredFields`)
- [x] Optional: `tools/ir-validate.ps1` used by a CI step.

Acceptance Criteria
- [x] Spec and schema published with examples and FAQ; repository lints/validates them in CI. (CI: `.github/workflows/ir-validate.yml`)
- [x] Three fixtures produce IRs that validate (via `tools/ir-validate.ps1`).
- [x] IR contains sufficient data to draw a project call graph with VDG.
- [x] Versioning policy documented; tools tolerate unknown fields.
- [x] [CLI](/docs/Glossary.md#cli) can render call graph from sources in one step (`render`).

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
 - [ ] Define PR guidance for IR changes (additive only for minors; deprecations policy). - *skipped, come back to it later when it becomes relevant.*
6) Parser Hardening & Symbol Table (Step 3)
- [x] Extract params (name/type/ByRef) and returns from signatures.
- [x] Track local variable types and `Set … = New …` constructions.
- [x] With-block awareness for `.Method()` target typing.
- [x] Rewrite qualified calls `obj.Method` to `Type.Method` when `obj` type is known.
7) Expand Call Detection (Step 1)
- [x] Unqualified call detection at line start (`Call Proc(...)`, `Proc(...)`).
- [x] Additional patterns (aliases, chained calls) - heuristics now include return-type lookups for factories to power alias inference.
8) New Diagram Modes (Step 2)
- [x] `module-callmap` (module-level call aggregation).
- [x] `event-wiring` (Form control events → handler procedures).
 - [x] `proc-cfg` (per-procedure CFG) - MVP: Start->Calls->End sequence; now surfaces loop/branch scaffolds for simple conditionals.

6) Scaffolds (optional within this milestone)
- [x] Add `tools/vba2json.ps1` skeleton (signatures/calls, not a full parser).
- [x] Add `tools/vba-ir2diagram.ps1` skeleton (project call graph mapping).
- [x] Wire a sample end-to-end doc snippet ([vba2json](/docs/Glossary.md#vba2json) -> ir-validate -> ir2diagram -> [VDG.CLI](/docs/Glossary.md#cli)).

### Sample End-to-End Workflow

This walkthrough uses the committed fixtures to demonstrate the complete pipeline from VBA sources to a rendered diagram. Adjust paths as needed for your environment; the commands assume repository root as the working directory.

1. **Convert VBA sources to IR (Intermediate Representation)**
   ```powershell
   dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/cross_module_calls --out out/tmp/cross.ir.json
   ```
   Generates `out/tmp/cross.ir.json`. The tool surface is described in [vba2json](/docs/Glossary.md#vba2json).

2. **Validate the IR against the schema**
   ```powershell
   pwsh ./tools/ir-validate.ps1 -InputPath out/tmp/cross.ir.json
   ```
   Expected output: `IR OK: out/tmp/cross.ir.json`, confirming the file matches `shared/Config/vbaIr.schema.json`.

3. **Convert IR to diagram JSON**
   ```powershell
   dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/cross.ir.json --out out/tmp/cross.diagram.json --mode callgraph
   ```
   Produces `out/tmp/cross.diagram.json`, ready for rendering. See [VDG CLI](/docs/Glossary.md#cli) for additional modes and metadata.

4. **Render or post-process the diagram**
   ```powershell
   dotnet run --project src/VDG.CLI -- out/tmp/cross.diagram.json out/tmp/cross.vsdx
   ```
   This saves a Visio file at `out/tmp/cross.vsdx` that can be opened directly or further automated with [VDG CLI](/docs/Glossary.md#cli).

For additional reference inputs, consult the golden IR fixtures under `tests/fixtures/ir/` and the schema contract in `shared/Config/vbaIr.schema.json`.

Risks & Mitigations
- Ambiguity for dynamic calls → mark `isDynamic`, record call site; resolve later if possible.
- Procedure identity drift → standardize `Module.Proc`, suffix stable index on collision (document rule).
- Forms metadata variance → treat form modules as `kind: "Form"`; use `attributes[]` for edge cases.

Timeline (suggested)
- [ ] Day 1–2: Draft spec + schema + examples.
- [ ] Day 3: Fixtures + golden IRs + validator + CI.
- [ ] Day 4: Review/adjust; finalize v0.1; publish docs.
