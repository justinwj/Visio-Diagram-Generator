# last statements from previous Ai telling something important to the next

Fixture Drift Guardrail
Whenever you touch rendering logic, run pwsh ./tools/render-fixture.ps1 -FixtureName invSys (without -Update) before committing. If you see diffs, apply/refresh the matching tests/fixtures/config/<fixture>/<mode>.diagram.override.json first, then rerun with -Update -Note "<reason>". The override + ledger workflow keeps multi-page view-mode coverage stable so CI never surprises you.

2025-10-29
- Dynamic card-width heuristics just landed in `ViewModePlanner`; algorithms tests updated and passing. Expect invSys callgraph layout to widen modules with long labels—run the fixture guardrail above before the next render/commit so we capture intentional drift.

