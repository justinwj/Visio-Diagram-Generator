# CLI Review Threshold Switch Design

## Goals
- Allow reviewers (local or CI) to tune which semantic/planner issues appear in the console, `.review.*`, and diagnostics outputs.
- Keep defaults backwards compatible (`warning` severity, 0.55 confidence floor, current flow residual warning limit), while exposing both CLI flags and environment variables for automation.

## Proposed Flags & Environment Variables

| Flag | Env Var | Type | Default | Purpose |
| --- | --- | --- | --- | --- |
| `--review-severity-threshold <info|warning|error>` | `VDG_REVIEW_SEVERITY_THRESHOLD` | string enum | `warning` | Drop info-level entries (or even warnings) from all review outputs when reviewers only care about higher severities. |
| `--role-confidence-cutoff <0.0-1.0>` | `VDG_ROLE_CONFIDENCE_CUTOFF` | double | `0.55` | Override the minimum classification confidence before a module/procedure is considered low-confidence. |
| `--review-flow-residual-cutoff <int>` | `VDG_REVIEW_FLOW_RESIDUAL_CUTOFF` | int | `1600` (current constant) | Adjust when the “unresolved flows” warning/suggestion fires; useful when fixtures naturally have a higher/lower residual baseline. |

CLI flags override env vars; env vars override hard-coded defaults.  

## Parsing Flow (RunIr2Diagram)
1. Extend the options loop (currently handling `--summary-log`, `--output-mode`, etc.) to capture the new arguments.
2. Validate:
   - Severity must be one of `info`, `warning`, `error` (case-insensitive).
   - Confidence cutoff parsed via `double.TryParse` using invariant culture and constrained to `[0.0, 1.0]`.
   - Flow residual cutoff parsed via `int.TryParse` and must be non-negative.
3. After parsing CLI args, resolve fallbacks:
   ```csharp
   var severityThreshold = ParseSeverity(
       cliSeverity ?? Environment.GetEnvironmentVariable("VDG_REVIEW_SEVERITY_THRESHOLD") ?? "warning");

   var roleConfidenceCutoff = ParseDouble(
       cliConfidence ?? Environment.GetEnvironmentVariable("VDG_ROLE_CONFIDENCE_CUTOFF"), 0.55, min: 0.0, max: 1.0);

   var flowResidualCutoff = ParseInt(
       cliFlowCutoff ?? Environment.GetEnvironmentVariable("VDG_REVIEW_FLOW_RESIDUAL_CUTOFF"), defaultValue: 1600);
   ```
4. Package these into a `SemanticReviewOptions` struct passed to `SemanticReviewReporter.Build`.

## SemanticReviewReporter Integration
- Add `SemanticReviewOptions` parameter (`MinimumSeverity`, `RoleConfidenceCutoff`, `FlowResidualCutoff`) to `Build`.
- When adding warnings/suggestions:
  - Compare each entry’s severity to `MinimumSeverity`; filter out anything below the threshold.
  - Use `RoleConfidenceCutoff` instead of the current literal `0.55` when tallying `LowConfidenceModules` / `ProceduresWithoutRole`.
  - Use `FlowResidualCutoff` to decide whether to emit the unresolved-flow warning/suggestion.
- The filtered results are what get:
  - Printed to stdout.
  - Written to `.review.txt` and `.review.json`.
  - Embedded into `metadata.properties["review.summary.json"]` and mirrored into diagnostics (`ReviewSummary`).
- Include threshold context in `.review.txt` header, e.g. `Threshold: warning (confidence ≥0.60)` so manual reviewers know why certain entries may be absent.

## CLI Help & Docs
Add to `PrintUsage()` (ir2diagram section):
```
    --review-severity-threshold <info|warning|error>
        Minimum severity to surface in review summaries (default: warning).
        Env override: VDG_REVIEW_SEVERITY_THRESHOLD.
    --role-confidence-cutoff <0.0-1.0>
        Confidence floor for module/procedure classification warnings (default: 0.55).
        Env override: VDG_ROLE_CONFIDENCE_CUTOFF.
    --review-flow-residual-cutoff <int>
        Unresolved flow count that triggers warnings (default: 1600).
        Env override: VDG_REVIEW_FLOW_RESIDUAL_CUTOFF.
```
Mirror this summary in `docs/Design.md` / reviewer documentation once implemented.

## CI / Automation Usage
- Nightly “verbose” runs can set `VDG_REVIEW_SEVERITY_THRESHOLD=info` to capture everything in `.review.*`.
- Strict gating pipelines can raise `VDG_ROLE_CONFIDENCE_CUTOFF` (e.g., `0.65`) so low-confidence classifications flip to warnings earlier.
- `tools/render-fixture.ps1` already supports env passthrough; we can optionally extend it to accept `-ReviewSeverityThreshold`, etc., which simply populate the env vars before `Invoke-Dotnet`.

## Testing Strategy
- Parser unit tests: ensure CLI + env precedence works and invalid inputs throw `UsageException`.
- Extend `SemanticReviewSummaryPrintedAndStored` (or add a new test) to run `ir2diagram` with custom thresholds and assert that suppressed warnings disappear / appear as expected.
- Fixture regeneration is unaffected because the script pins deterministic timestamps, but we may add a smoke test running with non-default thresholds to ensure serialization remains stable.
