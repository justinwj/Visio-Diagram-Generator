# Project State — Visio-Diagram-Generator
_Last updated: 2025-08-25 05:52:29_

## Current Snapshot
- **Solution:** `Visio-Diagram-Generator.sln`
- **Projects:**
  - `src/VDG.Core` (**net8.0**)
  - `src/VDG.Core.Contracts` (**net8.0**)
  - `src/VDG.CLI` (**net8.0**)
  - `tests/VDG.Core.Tests` (**net8.0**, xUnit)
- **Build/Test:** ✅ Build succeeded; ✅ Tests passing.
- **SDK:** Using .NET SDK 8.x (pinned via `global.json` with roll-forward `latestMinor`).
- **Editor:** VS Code workspace present (`Visio-Diagram-Generator.code-workspace`), `.vscode/tasks.json` set up for restore/build/test.

## Recent Changes
- Removed/archived VSTO Add-in artifacts into `backlog/`.
- Regenerated solution; added only CLI/test projects.
- Introduced `Directory.Build.props` for unified compiler/analyzer settings.
- Updated unit test packages to floating majors to avoid NU1603 noise.
- Added `.github/workflows/dotnet.yml` for CI (build + test + coverage).

## Open Items / Next Steps
- Define CLI verbs for **VDG.CLI** (e.g., `render`, `validate`, `plan`) and wire to `VDG.Core`.
- Add more unit tests around `VDG.Core` contracts and algorithms.
- (Optional) Add `dotnet-format` & `reportgenerator` to local tool manifest and enforce format in CI.
- (Optional) Add Dependabot (`.github/dependabot.yml`) for NuGet/Actions updates.
- (Optional) Publish as a dotnet **tool** when CLI surface is stable.

---

