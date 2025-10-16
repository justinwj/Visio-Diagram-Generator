# IR→Diagram Callgraph Performance & Memory Plan

## Current Baseline
- `tools/perf-smoke.ps1` runs a small fixture (`tests/fixtures/vba/cross_module_calls`) through `vba2json` + `ir2diagram` and emits metrics to `out/perf/perf.json`.
- Emitted JSON captures:
  - `vba2json.ms`, `ir2diagram.ms`, and exit codes.
  - Diagram payload sizes, node/edge counts, dynamic call counters.
  - Optional `summary.progress.emits/lastMs` when the CLI prints progress pulses.
- The script assumes PowerShell 7 (`ProcessStartInfo.ArgumentList`), causing failures when executed under Windows PowerShell (see error when running in this environment).

## Gaps Identified
1. **Input scale**: Existing fixture has 2 modules/2 procedures – not representative of the 500+ procedure targets called out in the milestone.
2. **Runtime compatibility**: `tools/perf-smoke.ps1` should work in both Windows PowerShell 5.x and PowerShell 7+. The current reliance on `ArgumentList` breaks the former.
3. **Metrics breadth**: Current payload lacks memory/GC statistics and object allocation insight.
4. **Repeatability & history**: Single JSON overwrites (`out/perf/perf.json`); we need per-run artifacts to trend over time.

## Proposed Harness
### 1. Fixture Generation
- Create a synthetic VBA fixture with 500+ procedures (e.g., `benchmarks/vba/massive_callgraph`) by templating modules/procedures to ensure deterministic call patterns.
- Store generated IR/diagram snapshots alongside scripts to regenerate (e.g., `tools/generate-benchmark.ps1`).

### 2. Execution Script (`tools/perf-ir2diagram.ps1`)
- Fork from `tools/perf-smoke.ps1` but refactor argument handling to support both PowerShell editions:
  - Replace `ProcessStartInfo.ArgumentList` with manual string arguments or use `Start-Process -FilePath dotnet -ArgumentList ...`.
  - Allow passing fixture, repetitions, and optional diagram mode via parameters.
- Capture:
  - `Stopwatch` timings for each stage.
  - CLI stdout progress line for modules/procs/edges/dynamic counters.
  - Process metrics: `WorkingSet64`, `PrivateMemorySize64`, GC counts via `[System.GC]::CollectionCount()`.
  - Optional `dotnet-counters collect --process-id <pid> --counters System.Runtime` to gather allocation rate and POH size; write results to sidecar files.
- Emit per-run artifacts under `out/perf/ir2diagram/<timestamp>/`:
  - `summary.json` (extended schema described below).
  - Raw stdout/stderr logs.
  - Optional `dotnet-counters.csv`.

### 3. Metrics Schema (draft)
```json
{
  "timestampUtc": "2025-10-13T07:15:00Z",
  "input": {
    "fixture": "benchmarks/vba/massive_callgraph",
    "modules": 180,
    "procedures": 540,
    "edges": 2400
  },
  "timings": {
    "vba2jsonMs": 640,
    "ir2diagramMs": 1185
  },
  "memory": {
    "workingSetBytes": 268435456,
    "privateBytes": 194000000,
    "gen0Collections": 4,
    "gen1Collections": 2,
    "gen2Collections": 1
  },
  "diagram": {
    "outBytes": 812345,
    "nodes": 540,
    "edges": 2400,
    "dynamicSkipped": 12,
    "dynamicIncluded": 0,
    "progress": { "emits": 9, "lastMs": 1184 }
  },
  "counters": {
    "allocBytesPerSec": 123456,
    "gcHeapSizeBytes": 167772160
  },
  "environment": {
    "dotnetSdk": "8.0.x",
    "os": "$env:OS",
    "cpu": "$env:PROCESSOR_IDENTIFIER"
  }
}
```

### 4. CI Integration Plan
- Add a GitHub Actions job (nightly/weekly) executing the new harness on self-hosted Windows runner with PowerShell 7 to avoid compatibility issues, but keep scripts compatible with Windows PowerShell for local developers.
- Publish artifacts to workflow summary and keep last N runs in `out/perf/history/`.
- Add threshold assertions (e.g., ir2diagram must stay < 2s, working set < 512MB) with configurable tolerances.

## Next Steps
1. Refactor `tools/perf-smoke.ps1` (or create new script) to remove `ArgumentList` dependency and support additional metrics.
2. Generate high-volume VBA fixture and commit to `benchmarks/`.
3. Extend `ir2diagram` CLI to optionally emit allocation stats if we decide to gather them directly (e.g., `--perf-log` writing GC counts).
4. Wire CI job + documentation once instrumentation stabilises.
