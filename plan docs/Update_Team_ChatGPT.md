# Update — Visio Diagram Generator (CLI-first baseline)
_Last updated: 2025-08-25 05:52:29_

## What changed
- Project is now **CLI-first** on **.NET 8**. Add-in/installer projects moved to `backlog/` (ignored by git).
- Solution: `Visio-Diagram-Generator.sln` with projects under `src/` and tests under `tests/`.
- Unified compiler/analyzer settings via `Directory.Build.props`; SDK pinned via `global.json`.
- VS Code tasks and workspace are present; GitHub Actions CI added at `.github/workflows/dotnet.yml`.

## How to get running
```bash
git pull
dotnet restore Visio-Diagram-Generator.sln
dotnet build   Visio-Diagram-Generator.sln -c Debug
dotnet test    Visio-Diagram-Generator.sln -c Debug
```
In VS Code, open the repo root (or the `Visio-Diagram-Generator.code-workspace`) and let the C# extension load the solution.

## Conventions (Please follow)
- Target **net8.0** for all new projects. If you forget, `Directory.Build.props` will set it.
- Keep tests on xUnit with floating majors (`2.*`, `17.*` for `Microsoft.NET.Test.Sdk`).
- Don’t modify `backlog/`—it’s historical archive only.

## What we need next
- Agree on initial **CLI verbs** (e.g., `render`, `validate`, `plan`) and I/O format.
- Add/expand unit tests for `VDG.Core`.
- (Optional) Approve `Dependabot` + `dotnet-format` check in CI.

— End of update —

