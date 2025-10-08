# IR Change Governance

Use this checklist whenever a pull request alters the VBA [IR](docs/Glossary.md#ir) surface or the tooling that emits/consumes it. The guide is designed for reviewers and will also feed future automation gates.

1. Keep minor releases additive; bump the major schema version when removing or retyping fields and document migrations in `docs/CHANGELOG_IR.md`.
2. Update the spec (`docs/VBA_IR.md`), schema (`shared/Config/vbaIr.schema.json`), and changelog together so the documentation, schema, and implementation stay in lockstep.
3. Regenerate fixtures under `tests/fixtures/vba/` and `tests/fixtures/ir/`, then refresh the automation in `tests/VDG.VBA.CLI.Tests` (schema validation, determinism, content) to cover the new surface.
4. Refresh generator metadata (`tools/vba2json.ps1`, see [vba2json](docs/Glossary.md#vba2json)) and [CLI](docs/Glossary.md#cli) help output when behaviour changes; keep `generator.version` aligned with the released tool version.
5. For deprecations, mark fields in the spec, record the sunset in the changelog, keep them optional until the next major release, and add tests that prove consumers tolerate both old and new shapes.

## Review Cadence
- Revisit this checklist at least quarterly or whenever the IR schema publishes a new minor version. Capture any policy changes or follow-up actions in `docs/CHANGELOG_IR.md`.

## End-to-End Smoke Workflow

```powershell
# 1) Export IR from VBA sources
dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/cross_module_calls --out out/tmp/cross.ir.json

# 2) Validate schema + invariants
pwsh ./tools/ir-validate.ps1 -InputPath out/tmp/cross.ir.json

# 3) Convert IR to diagram JSON
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/cross.ir.json --out out/tmp/cross.diagram.json --mode callgraph

# 4) Render diagram with the main CLI
dotnet run --project src/VDG.CLI -- out/tmp/cross.diagram.json out/tmp/cross.vsdx
```
