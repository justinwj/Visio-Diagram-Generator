# VDG VBA Intermediate Representation ([IR](docs/Glossary.md#ir)) v0.1

Purpose
- A small, stable JSON format that captures the structure of a VBA project (modules, procedures, and calls) so downstream tools can convert it to diagram JSON and render via [VDG](docs/Glossary.md#vdg).

Versioning
- `irSchemaVersion`: `0.1` (SemVer `major.minor`).
- Minor versions are additive only (new optional fields/enums). Tools must ignore unknown fields.
- Major version bump for breaking changes; provide migration notes in `docs/CHANGELOG_IR.md`.

Related Resources
- IR change governance checklist and smoke workflow: `docs/IR_Governance.md`.
- Project glossary for acronyms and terminology: `docs/Glossary.md`.

Top‑Level Shape
```json
{
  "irSchemaVersion": "0.1",
  "generator": { "name": "vba2json", "version": "0.1.0" },
  "project": {
    "name": "MyProject",
    "modules": [ /* Module objects */ ]
  }
}
```

Module
```json
{
  "id": "Module1",                      // Stable id within project
  "name": "Module1",                    // Source name
  "kind": "Module",                     // Module | Class | Form
  "file": "src/Module1.bas",            // Source file path (relative allowed)
  "metrics": { "lines": 42 },           // Optional (requires --infer-metrics)
  "attributes": ["Option Explicit"],     // Optional
  "procedures": [ /* Procedure objects */ ]
}
```

Procedure
```json
{
  "id": "Module1.DoWork",               // Stable id (Module.Proc)
  "name": "DoWork",                      // Procedure name
  "kind": "Function",                    // Sub | Function | PropertyGet | PropertyLet | PropertySet
  "access": "Public",                    // Public | Private | Friend
  "static": true,                       // Present only when procedure is declared Static
  "params": [ { "name": "count", "type": "Integer", "byRef": false } ],
  "returns": "String",
  "locs": { "file": "src/Module1.bas", "startLine": 12, "endLine": 42 },
  "calls": [ /* Call objects */ ],
  "metrics": { "cyclomatic": 3, "lines": 31 }, // Optional (requires --infer-metrics)
  "tags": ["utility"]
}
```

Call
```json
{
  "target": "Module2.OtherProc",        // Target id (Module.Proc)
  "isDynamic": false,                    // true for CallByName, Application.Run, etc.
  "branch": "loop|then",                // Optional branch/loop context tags
  "site": { "module": "Module1", "file": "src/Module1.bas", "line": 27 }
}
```

Conventions
- Field casing: lowerCamel for fields; PascalCase for enum values.
- IDs: `Module.Proc` unique within project. Modules unique by `name`.
- Optional fields should be omitted when unknown (not set to null).
- Tools should emit a stable order (modules by name; procedures by name) for deterministic diffs.
- Metrics should only appear when the generator has been asked for them (e.g., `vba2json --infer-metrics`); omit the field otherwise.

Example (Minimal)
```json
{
  "irSchemaVersion": "0.1",
  "generator": { "name": "vba2json", "version": "0.1.0" },
  "project": {
    "name": "Sample",
    "modules": [
      {
        "id": "Module1",
        "name": "Module1",
        "kind": "Module",
        "file": "src/Module1.bas",
        "procedures": [
          {
            "id": "Module1.DoWork",
            "name": "DoWork",
            "kind": "Sub",
            "access": "Public",
            "locs": { "file": "src/Module1.bas", "startLine": 10, "endLine": 20 },
            "calls": [ { "target": "Module2.OtherProc", "isDynamic": false, "site": { "module": "Module1", "file": "src/Module1.bas", "line": 15 } } ]
          }
        ]
      },
      {
        "id": "Module2",
        "name": "Module2",
        "kind": "Module",
        "file": "src/Module2.bas",
        "procedures": [
          {
            "id": "Module2.OtherProc",
            "name": "OtherProc",
            "kind": "Sub",
            "access": "Public",
            "locs": { "file": "src/Module2.bas", "startLine": 5, "endLine": 12 }
          }
        ]
      }
    ]
  }
}
```

Mapping to Diagram JSON (Project Call Graph)
- Node = `Module.Proc`, label `Module.Proc`.
- Container = Module; tiers by `module.kind`: Forms | Classes | Modules.
- Edge = `call` with:
  - `edges[].metadata.code.edge = "call"`
  - `edges[].metadata.code.site.module|file|line` copied from `call.site`
  - `edges[].metadata.code.branch` when branch information is present
  - `edges[].metadata.code.dynamic = "true"` when IR `call.isDynamic = true`
- Add to `nodes[].metadata`:
  - `code.module`, `code.proc`, `code.kind`, `code.access`
  - `code.locs.file`, `code.locs.startLine`, `code.locs.endLine`

Dynamic calls and unknown targets
- IR may include calls with `isDynamic = true`. These propagate to diagram edges via `metadata.code.dynamic = "true"`.
- When the IR target cannot be resolved (`target = "~unknown"`), converters should skip emitting the edge by default to avoid noise. Tools may offer an opt-in to include such edges (e.g., `--include-unknown`) and can render them to a sentinel node `~unknown` for debugging.

Future Extensions
- Additional call metadata (arity, inferred targets for dynamics), attributes for forms/classes, module references.
- Additional diagram modes: Module Structure, per-procedure [CFG](docs/Glossary.md#cfg), Event Wiring.

FAQ

Q: How are procedure IDs formed and kept stable?
- A: `id = ModuleName.ProcName`. The `name` is the source name. Properties are represented as procedures with `kind = PropertyGet|PropertyLet|PropertySet`. Multiple accessors of the same property therefore share the same `id`; if you need uniqueness per accessor, combine `id + ':' + kind` (or the generator may optionally suffix `_get/_let/_set`).

Q: What about duplicate module names or renamed files?
- A: Module `name` should be unique within a project. If duplicates exist, a generator may suffix a stable discriminator (e.g., `Module (2)`) and include `originalName` and `file` to preserve provenance. Tools should treat `id` as the stable key and prefer `file` for source navigation.

Q: How are dynamic calls represented?
- A: Calls that cannot be statically resolved (e.g., `CallByName`, `Application.Run`) set `isDynamic = true` and still record a best‑effort `target` if one can be inferred (else keep a placeholder like `"~unknown"`). The `site` object always records the source line location.

Q: How are line numbers defined?
- A: `startLine` and `endLine` are 1‑based and inclusive, referring to physical lines in `locs.file`. Tools should not assume CRLF vs LF — treat files as text; the generator determines positions.

Q: Are nulls allowed? What about unknown fields?
- A: Prefer omitting unknown/empty optional fields rather than setting them to `null`. Consumers must ignore unknown fields to allow additive evolution of the schema.

Q: Is ordering guaranteed?
- A: Generators should emit a deterministic order: modules sorted by `name` (then `id`), and procedures sorted by `name`. This enables stable diffs and repeatable builds.

Q: How are forms and events modeled?
- A: Forms are modules with `kind = "Form"`. Event handlers are ordinary procedures (e.g., `Command1_Click`). There is no special event edge type in v0.1; they appear as normal calls if they call other procedures. Future versions may add an explicit event wiring section.

Q: What about file paths?
- A: Relative paths are recommended (repo‑relative) to improve portability; absolute paths are allowed but discouraged. The combination of `module.name` and `locs.file` should be sufficient for navigation.
