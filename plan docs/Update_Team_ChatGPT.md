# Team Update — 2025-09-04

**What changed**
- Architecture locked: **CLI (net8)** → calls **Visio runner (net48)**; shared logic in **netstandard2.0**.
- **No VSTO** anywhere in the plan or commentary.
- **Connectors**: use `ConnectorToolDataObject`; **no connectors stencil**.
- **Page sizing**: default `PageSizingMode="auto"` so large diagrams **always generate**.
- Prompt plan cleaned (removed duplicate P6); tests plan aligned with CLI/runner lanes.

**Why**
- net48 runner gives the most reliable COM interop; CLI stays modern and fast on net8.

**Current status**
- E2E: `sample_diagram.json → mydiagram.vsdx` works via runner.
- CLI integration: plan set; CLI will shell out to runner.

**Next**
- Implement Prompt 5 in CLI (schema validation UX).
- Add runtime hook for ApplyPageSizing; expand connector/route tests.

**How to run**
```powershell
$json = (Resolve-Path ".\samples\sample_diagram.json").Path
$out  = Join-Path (Split-Path -Parent $json) "mydiagram.vsdx"
& "src\VDG.VisioRuntime\bin\x64\Release\net48\VDG.VisioRuntime.exe" $json $out
# or via CLI (net8):
dotnet run --project .\src\VisioDiagramGenerator.CliFs -- generate --config $json --out $out
```


# Team Update — 2025-09-03

**What we fixed**
- Stencil handling: opened **hidden+read‑only** and **cached**; avoids ActiveDocument switching and dictionary misses.
- Document lifecycle: we **guarantee** a drawing + page before draw calls.
- Connectors: switched to **ConnectorToolDataObject** (no connectors stencil).
- Save logic: `SaveAsVsdx` always targets the **drawing** document and writes to a **normalized absolute path** (mkdir if needed).

**Why it was failing**
- Earlier runs showed stencils loading multiple times and a `KeyNotFoundException` via `VisioStaHost.JobBase.Wait()`. Saving sometimes targeted a **stencil** or a path resolved to a different working directory, yielding a COM “cannot write” error.

**Current status**
- `sample_diagram.json → mydiagram.vsdx`: **working end‑to‑end**. Output lands in `samples\` and opens in Visio.
- CLI logs WorkingDir/JSON/Output and anchors relative output to the JSON folder.

**What’s next**
- Move on to **Prompt 5**.
- Add regression tests for: stencil caching; connectors; width/height/text on dropped masters; output path normalization.

**How to run (dev)**
```powershell
$json = (Resolve-Path ".\samples\sample_diagram.json").Path
$out  = Join-Path (Split-Path -Parent $json) "mydiagram.vsdx"
& "src\VDG.VisioRuntime\bin\x64\Release\net48\VDG.VisioRuntime.exe" $json $out
```
