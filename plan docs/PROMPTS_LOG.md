# Prompts / Actions Log
_Last updated: 2025-08-25 05:52:29_

## Session 2025-08-25 05:52:29
- Converted repository to **CLI-first**; archived add-in/installer content (`backlog/`).
- Created/used `Convert-ToCLI.git.fixed.ps1` with Git push support and dry-run safety.
- Generated VS Code workspace and tasks; set default solution to `Visio-Diagram-Generator.sln`.
- Pinned SDK via `global.json`; created `Directory.Build.props` (fixed malformed XML and ensured TFMs).
- Retargeted `VDG.Core` and `VDG.Core.Contracts` to **net8.0**; cleaned stale `obj/bin`.
- Resolved test package warning by using **xUnit/Test SDK floating majors**.
- Verified: `dotnet restore`, `dotnet build`, and `dotnet test` all **succeeded**.
- Added **`.github/workflows/dotnet.yml`** CI (build+test).

---

