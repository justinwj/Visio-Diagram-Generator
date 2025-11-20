# IR->Diagram Callgraph Performance & Memory Plan

## Current Baseline (Updated)
- `tools/perf-smoke.ps1` now supports both Windows PowerShell 5.x and PowerShell 7+, writing metrics to timestamped files when `-KeepHistory` is supplied.
- Metrics conform to `shared/Config/perfMetrics.schema.json`; additional context (progress pulses, shell memory, raw output) lands in a sibling `.extra.json`.
- When `GITHUB_STEP_SUMMARY` is set, the script appends a short perf summary for workflow visibility.

## Recent Additions
1. **Synthetic fixture**: `benchmarks/vba/massive_callgraph` (24 modules x 25 procedures = 600 procedures) generated via `tools/benchmarks/New-MassiveCallgraph.ps1`. The helper can optionally emit prebuilt IR (`benchmarks/ir/massive_callgraph.ir.json`) and diagram artifacts.
2. **Harness refinements**:
   - `Start-Process` with redirected output removes the `ProcessStartInfo.ArgumentList` dependency and keeps stdout/stderr for analysis.
   - New `-Preset` switch quickly selects common inputs (`cross-module`, `massive-callgraph`).
   - `-VerboseExtras` captures raw CLI output when deeper troubleshooting is needed.

## Remaining Gaps
1. **Memory/GC depth**: Extras capture shell working/private bytes only. Investigate `dotnet-counters` or custom instrumentation for allocation/heap data.
2. **Trend history**: `-KeepHistory` preserves each run, but automated aggregation/visualisation is still pending.
3. **Threshold enforcement**: No automated regression guardrails yet (e.g., failing when `ir2diagram` exceeds target duration or memory).

## Implementation Guide
1. **Fixture Regeneration**
   ```powershell
   pwsh ./tools/benchmarks/New-MassiveCallgraph.ps1 -Overwrite -GenerateArtifacts
   ```
   Outputs:
   - VBA sources under `benchmarks/vba/massive_callgraph`
   - IR: `benchmarks/ir/massive_callgraph.ir.json`
   - Diagram: `benchmarks/diagram/massive_callgraph.diagram.json`

2. **Running the Harness**
   ```powershell
   pwsh ./tools/perf-smoke.ps1 -Preset massive-callgraph -KeepHistory
   ```
   - Metrics -> `out/perf/perf_<timestamp>.json`
   - Extras -> `out/perf/perf_<timestamp>.extra.json`
   - Summary -> appended to `$env:GITHUB_STEP_SUMMARY` when present.

3. **Schema Compliance**
   - Metrics JSON remains compatible with `shared/Config/perfMetrics.schema.json`.
   - Additional data (progress pulses, private bytes, raw output) stays in the sidecar `.extra.json` to avoid schema violations.

4. **CI Hooks (Planned)**
   - Extend `.github/workflows/dotnet.yml` with a nightly job calling `tools/perf-smoke.ps1 -Preset massive-callgraph -KeepHistory`.
   - Publish the latest JSON/extra artifacts and rely on the built-in workflow summary.
   - Future work: store recent metrics (e.g., rolling CSV/Markdown trend) for long-term tracking.

## Next Steps
1. Integrate deeper runtime metrics (GC counts, allocation rates) using `dotnet-counters` or custom instrumentation.
2. Add guardrail thresholds and fail builds when perf deviates beyond agreed tolerances.
3. Automate aggregation/visualisation (rolling CSV/Markdown trends) for per-commit and scheduled runs.

