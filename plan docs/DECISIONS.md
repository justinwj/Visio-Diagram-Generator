# Decisions Log — Visio-Diagram-Generator
_Last updated: 2025-08-25 05:52:29_

## 2025-08-25 05:52:29 — CLI-first migration & .NET 8 baseline
**Status:** Adopted

- **Architecture:** Move to **CLI-first**; VSTO Add-in and installer content **archived** under `backlog/` (ignored by git).
- **Solution:** Use `Visio-Diagram-Generator.sln` at repo root; auto-add projects from `src/` and `tests/`.
- **Target Framework:** Standardize on **.NET 8** for `VDG.Core`, `VDG.Core.Contracts`, and `VDG.CLI`.  
  - Central defaults via **`Directory.Build.props`** (nullable/implicit usings enabled, analyzers on).
  - SDK pinned with **`global.json`** (8.x, roll-forward to latest minor).
- **Testing:** Keep xUnit/Test SDK packages on floating majors (`xunit` `2.*`, `xunit.runner.visualstudio` `2.*`, `Microsoft.NET.Test.Sdk` `17.*`, `coverlet.collector` `6.*`).
- **Tooling:** Provide VS Code workspace/tasks and git hygiene (`.gitignore`, `.gitattributes`).
- **Scripts:** Use **`Convert-ToCLI.git.fixed.ps1`** as the safe, non-destructive converter (approved verbs & dry-run). 
- **CI/CD:** GitHub Actions workflow at **`.github/workflows/dotnet.yml`** builds + tests on push/PR (NuGet cache optional).
- **Repo Name:** GitHub remote expected to be **Visio-Diagram-Generator** (solution name matches).

### Notes
- Avoid editing files under `backlog/` (historical archive).
- Any new project should default to `net8.0`; projects missing a TFM will inherit via `Directory.Build.props`.

---

