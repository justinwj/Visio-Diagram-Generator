# IR Change Governance

Use this checklist whenever a pull request alters the VBA [IR](docs/Glossary.md#ir) surface or the tooling that emits/consumes it. The guide is designed for reviewers and will also feed future automation gates.

1. Keep minor releases additive; bump the major schema version when removing or retyping fields and document migrations in `docs/CHANGELOG_IR.md`.
2. Update the spec (`docs/VBA_IR.md`), schema (`shared/Config/vbaIr.schema.json`), and changelog together so the documentation, schema, and implementation stay in lockstep.
3. Regenerate fixtures under `tests/fixtures/vba/` and `tests/fixtures/ir/`, then refresh the automation in `tests/VDG.VBA.CLI.Tests` (schema validation, determinism, content) to cover the new surface.
4. Refresh generator metadata (`tools/vba2json.ps1`, see [vba2json](docs/Glossary.md#vba2json)) and [CLI](docs/Glossary.md#cli) help output when behaviour changes; keep `generator.version` aligned with the released tool version.
5. For deprecations, mark fields in the spec, record the sunset in the changelog, keep them optional until the next major release, and add tests that prove consumers tolerate both old and new shapes.

## Review Cadence
- Revisit this checklist at least quarterly or whenever the IR schema publishes a new minor version. Capture any policy changes or follow-up actions in `docs/CHANGELOG_IR.md`.

## Checklist Automation
- Every pull request runs the **PR Checklist Enforcement** status check. It passes when all IR Impact items are checked or when unchecked items include a justified exception rationale.
- Repository admins should mark the `PR Checklist Enforcement / Validate IR checklist` check as **required** in branch protection rules so merges cannot bypass the policy.
- For periodic reviews, run `tools/audit-ir-exceptions.ps1` (requires GitHub CLI) to list recent PRs that exercised the exception path.

### Acceptable Exceptions
- A blocking upstream dependency or pending schema change with a linked issue or approval.
- Emergency hotfixes where completing the checklist would materially delay the fix (document follow-up work).
- Documentation-only PRs that intentionally defer code instrumentation (explain the scope and follow-up owner).

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

## FAQ
**Q: The PR Checklist Enforcement check failed with “provide a justification”. What should I do?**  
Update the PR body so that either all IR Impact checkboxes are ticked or the *IR Checklist Exception Rationale* section contains a short explanation (≥20 characters) and links to the issue/approval that allows the exception.
