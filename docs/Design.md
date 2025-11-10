**Visio Diagram Generator — Architecture Overview**

Components
- `src/VDG.VBA.CLI` (C#): Sidecar CLI for source → IR and IR → Diagram conversion.
- `src/VDG.CLI` (C# + F#): Runner that validates Diagram JSON (schema 1.2), lays out nodes/edges, emits diagnostics (M5), and automates Visio to save `.vsdx`.
- `src/VisioDiagramGenerator.Algorithms` (F#): Core layout algorithms (tiers, spacing, pagination, routing stubs) and diagnostics computation.
- `src/VDG.Core` / `src/VDG.Core.Contracts`: Models and pipeline abstractions used across the toolchain.
- `src/VDG.VisioRuntime` (C#/F#): Visio COM interop and rendering services.
- `shared/Config/*.schema.json`: Public JSON schema contracts (Diagram 1.2, IR v0.1).

Data Flow
- VBA sources (`.bas/.cls/.frm`) → `vba2json` → IR (`irSchemaVersion: 0.1`).
- IR → `ir2diagram` (callgraph mode) → Diagram JSON (`schemaVersion: 1.2`).
- Diagram JSON → `VDG.CLI` → `.vsdx` (with diagnostics JSON when enabled).

Design Notes
- Deterministic ordering (modules/procedures) for stable diffs.
- Optional fields are omitted; consumers ignore unknown fields (additive evolution).
- Diagnostics: layout crowding, crossings/utilization, container occupancy — tunable via CLI and JSON.
- CI: schema validation for IR/Diagram; render smoke with `VDG_SKIP_RUNNER=1`.

Review Feedback Surface
- `ir2diagram` prints a semantic/planner summary immediately after classification (subsystem counts, role tallies, confidence gaps, pagination warnings, suggestions).
- Each diagram export emits a sibling `.review.txt` (human-readable) and `.review.json` (machine-readable) plus embeds the JSON into `metadata.properties["review.summary.json"]`.
- When Visio automation runs with `--diag-json`, `VDG.CLI` copies the same payload into `ReviewSummary` inside the diagnostics JSON so CI/review portals can ingest the insights without opening the diagram.
- Semantic artefacts now default to deterministic timestamps (hash-derived); pass `--semantics-generated-at <ISO8601>` or set `VDG_SEMANTICS_GENERATED_AT` when a real-world timestamp is required.

Docs & Schemas
- IR spec: `docs/VBA_IR.md` (mapping rules, dynamic calls, FAQ)
- Diagram schema: `shared/Config/diagramConfig.schema.json`
- IR schema: `shared/Config/vbaIr.schema.json`
