# VDG VBA Intermediate Representation (IR) v0.1

Purpose
- A small, stable JSON format that captures the structure of a VBA project (modules, procedures, and calls) so downstream tools can convert it to diagram JSON and render via VDG.

Versioning
- `irSchemaVersion`: `0.1` (SemVer `major.minor`).
- Minor versions are additive only (new optional fields/enums). Tools must ignore unknown fields.
- Major version bump for breaking changes; provide migration notes in `docs/CHANGELOG_IR.md`.

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
  "attributes": ["Option Explicit"],     // Optional
  "procedures": [ /* Procedure objects */ ]
}
```

Procedure
```json
{
  "id": "Module1.DoWork",               // Stable id (Module.Proc)
  "name": "DoWork",                      // Procedure name
  "kind": "Sub",                         // Sub | Function | PropertyGet | PropertyLet | PropertySet
  "access": "Public",                    // Public | Private | Friend
  "static": false,
  "params": [ { "name": "x", "type": "Integer", "byRef": false } ],
  "returns": null,                        // e.g., "String" for Function
  "locs": { "file": "src/Module1.bas", "startLine": 12, "endLine": 42 },
  "calls": [ /* Call objects */ ],
  "metrics": { "cyclomatic": 3, "lines": 31 },
  "tags": ["utility"]
}
```

Call
```json
{
  "target": "Module2.OtherProc",        // Target id (Module.Proc)
  "isDynamic": false,                    // true for CallByName, Application.Run, etc.
  "site": { "module": "Module1", "file": "src/Module1.bas", "line": 27 }
}
```

Conventions
- Field casing: lowerCamel for fields; PascalCase for enum values.
- IDs: `Module.Proc` unique within project. Modules unique by `name`.
- Optional fields should be omitted when unknown (not set to null).
- Tools should emit a stable order (modules by name; procedures by name) for deterministic diffs.

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
            "params": [],
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
            "params": [],
            "locs": { "file": "src/Module2.bas", "startLine": 5, "endLine": 12 },
            "calls": []
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
- Edge = `call` with `edges[].metadata.code.edge = "call"` and `edges[].metadata.call.site` from `call.site`.
- Add to `nodes[].metadata`:
  - `code.module`, `code.proc`, `code.kind`, `code.access`, `code.locs.file`, `code.locs.startLine`, `code.locs.endLine`.

Future Extensions
- Additional call metadata (arity, inferred targets for dynamics), attributes for forms/classes, module references.
- Additional diagram modes: Module Structure, per‑procedure CFG, Event Wiring.

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

