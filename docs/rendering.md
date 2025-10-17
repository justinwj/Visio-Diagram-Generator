# Render Pipeline Quick Start

Milestone Nine delivers a single-step story for generating Visio callgraph diagrams from VBA sources. The flow stitches together `vba2json`, `ir2diagram`, and `VDG.CLI` so you can run it locally, in CI, or via the smoke script.

## Prerequisites

- .NET 8 SDK installed (`dotnet --info`).
- Repository built at least once (`dotnet build Visio-Diagram-Generator.sln -c Release`).
- Visio automation optional; set `VDG_SKIP_RUNNER=1` to exercise rendering without launching the COM runner.

## End-to-End Commands

```powershell
# 1) VBA sources -> IR JSON
dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/hello_world --out out/tmp/hello.ir.json

# 2) IR -> Diagram JSON (callgraph mode)
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/hello.ir.json --out out/tmp/hello.diagram.json --mode callgraph

# 3) Render diagram (skip Visio, emit diagnostics JSON)
$env:VDG_SKIP_RUNNER = "1"
src\VDG.CLI\bin\Release\net48\VDG.CLI.exe --diag-json out/tmp/hello.diagnostics.json out/tmp/hello.diagram.json out/tmp/hello.vsdx

# 4) One-command pipeline (IR + Diagram + Render)
dotnet run --project src/VDG.VBA.CLI -- render --in tests/fixtures/vba/hello_world --out out/tmp/hello_full.vsdx --mode callgraph --diagram-json out/tmp/hello_full.diagram.json --diag-json out/tmp/hello_full.diagnostics.json
```

Notes:
- `--diag-json` writes rich diagnostics payloads (crowding, crossings, utilization). The defaults emit warnings; set `VDG_DIAG_FAIL_LEVEL=error` to gate builds once thresholds are tuned.
- Diagram styling honours the palette documented in `docs/StylingDefaults.md` with legend asset `docs/render_legend.png`.
- A styled sample diagram (`samples/vba_callgraph_styled.vsdx`) and its JSON counterpart (`samples/vba_callgraph.diagram.json`) are kept current with the defaults—regenerate via the commands above whenever styling changes.

## Diagnostics Policy Snapshot

- Lane crowding warning threshold: occupancy >= 0.85; overcrowded error threshold: >= 0.95.
- Page crowding warning threshold: occupancy >= 0.90; > 1.0 triggers overflow errors.
- Crossing and utilization thresholds default to warnings only; override with `VDG_DIAG_CROSS_WARN`, `VDG_DIAG_CROSS_ERR`, and `VDG_DIAG_UTIL_WARN`.
- Fail behaviour is warn-only by default. Opt in to stricter gating with `VDG_DIAG_FAIL_LEVEL=warning` or `VDG_DIAG_FAIL_LEVEL=error` (alias `VDG_DIAG_FAIL_ON` still honoured).
- All ratios and severities accept overrides via environment variables, allowing experimentation without editing diagram JSON.

## Render Smoke Script

`tools/render-smoke.ps1` wraps the full pipeline, produces both the `.vsdx` and a summary file (`out/perf/render_diagnostics.json`), and compares the summary with `tests/baselines/render_diagnostics.json`.

```powershell
# Capture current metrics and compare with baseline (fails if drift > 5%)
pwsh ./tools/render-smoke.ps1

# Refresh the baseline after intentional styling/layout changes
pwsh ./tools/render-smoke.ps1 -UpdateBaseline
```

The summary captures connector counts, lane/page occupancy ratios, top container metrics, and grouped diagnostics. Drift beyond +/- 5% on identical fixtures fails the smoke to catch accidental layout regressions. The raw diagnostics JSON emitted by `VDG.CLI` is copied alongside the summary (`out/perf/render_diagnostics.raw.json`) for deeper inspection.

When refreshing the baseline:
1. Run the script with `-UpdateBaseline` and review the regenerated `tests/baselines/render_diagnostics.json`.
2. Commit the baseline together with the styling/layout change so subsequent runs compare against the right expectations.
3. To compare two summaries manually, load both JSON files into PowerShell (`ConvertFrom-Json`) or your favourite diff tool; the data is sorted for deterministic diffs.

## CI Integration

The `render-smoke` job in `.github/workflows/dotnet.yml` calls the smoke script (with `VDG_SKIP_RUNNER=1`) and uploads the summary and raw diagnostics as artifacts. To tailor behaviour in CI:

- Override lane/page thresholds via workflow `env` entries (for example `VDG_DIAG_LANE_WARN: "0.80"`).
- Enable failure on warnings by setting `VDG_DIAG_FAIL_LEVEL: warning` once the team is ready to enforce limits.
- Add fixtures by invoking `./tools/render-smoke.ps1 -In <fixture>` and capturing corresponding baselines under `tests/baselines`.
- The script also tightens thresholds once per run to ensure diagnostics fail-level gating works (`VDG_DIAG_FAIL_LEVEL=warning`), so CI will fail if warnings no longer trip the guard.

## Appendix: Large Diagram Troubleshooting

- Prefer landscape layouts for huge callgraphs: rerun `render` with `--page-width 14 --page-height 8.5` (legal) or 17×11 for tabloid-scale exports.
- Tighten vertical spacing on tall diagrams via `--spacing-v 0.45`; complement with `--diag-height <in>` to receive early overflow warnings when experimenting.
- If lane crowding keeps tripping errors, split the module set across multiple export passes (e.g., one render per VBA project area) or enable pagination (`--paginate true`) so `VDG.CLI` injects additional virtual sheets.
- Large-sheet troubleshooting checklist:
  1. Inspect `out/perf/render_diagnostics.json` for `PageCrowding`/`LaneCrowding` metrics—values >95% mean you should widen spacing or increase page size.
  2. Use `VDG_DIAG_FAIL_LEVEL=warning` temporarily to stop builds while you iterate on layout changes.
  3. Re-run the single-command pipeline after each adjustment to confirm schema validity and `.vsdx` generation remain intact.

## Troubleshooting

- `VDG.CLI exited with 65` – check that the diagram JSON exists and validates against schema 1.2 (`tools/diagram-validate.ps1`).
- Missing diagnostics JSON – ensure you passed `--diag-json` (or set `layout.diagnostics.emitJson=true` inside the diagram metadata).
- CI still green after crowding changes – export diagnostics thresholds via environment variables (`VDG_DIAG_LANE_WARN`, `VDG_DIAG_LANE_ERR`, `VDG_DIAG_PAGE_WARN`) and opt into failures with `VDG_DIAG_FAIL_LEVEL=warning|error`.
