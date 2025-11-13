# last statements from previous Ai telling something important to the next

Fixture Drift Guardrail
Whenever you touch rendering logic, run pwsh ./tools/render-fixture.ps1 -FixtureName invSys (without -Update) before committing. If you see diffs, apply/refresh the matching tests/fixtures/config/<fixture>/<mode>.diagram.override.json first, then rerun with -Update -Note "<reason>". The override + ledger workflow keeps multi-page view-mode coverage stable so CI never surprises you.

Automated Clean-up Before Every Test Run
- Before running any tests or fixture refresh scripts, automatically delete all previously generated diagram outputs for the target mode/fixture, especially:
  - `invSys.callgraph.ir.json`
  - `invSys.callgraph.diagram.json`
  - `invSys.callgraph.diagnostics.json`
  - `invSys.callgraph.vsdx`
- Ensuring a clean slate prevents stale data from masking regressions or interfering with UI validation.

