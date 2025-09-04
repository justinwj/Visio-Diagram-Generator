# DECISIONS

## 2025-09-04 — Architecture alignment & user-facing defaults

1) **Runtime split (pinned)**
   - **Visio runner = net48** (`VDG.VisioRuntime.exe`) for reliable COM interop.
   - **F# CLI = net8.0-windows**; it **invokes** the runner via `Process.Start`.
   - **Shared logic = netstandard2.0** library (used by both).

2) **No VSTO surfaces**
   - Removed Ribbon/ThisAddIn assumptions from plans and commentary; delivery is CLI + runner only.

3) **Connectors policy (pinned)**
   - **Do not** load a connectors stencil; use `Application.ConnectorToolDataObject`.
   - Create connectors **after** shapes; **glue to shape pins** (or side-policy points).

4) **Page sizing policy (generate-first)**
   - Default `PageSizingMode = "auto"` so diagrams **always generate**.
   - `fixed` requires positive `PageWidthIn`/`PageHeightIn`; `fit-one-page` is export-time shrink-to-fit.

5) **Plan cleanup**
   - Removed duplicate **Prompt 6** in `VDG_Prompt_Plan_v3_CLI_v2.txt`.
   - Tests plan rewritten for **CLI (net8)** ↔ **runner (net48)** lanes.

Outcome: Prompt 3 goals achieved on **net48** runner; plan and scaffolds now match the actual architecture.

# DECISIONS

## 2025-09-03 — Runtime stabilizations for JSON → VSDX

### Root causes identified
- `KeyNotFoundException` bubbled out of `VisioStaHost.JobBase.Wait()` when a stencil document wasn’t cached and a later lookup failed. Logs also showed `BASIC_U.VSSX` being loaded twice.
- “Visio is unable to write to this directory or disk drive” appeared when the runtime attempted to save a *stencil* (read‑only) or when the save path resolved to an unexpected working directory.

### Decisions
1) **Cache stencils + don’t steal focus**  
   Open stencils **hidden & read‑only** and immediately cache the returned `Document` in `_stencilCache` (case‑insensitive). Prevents `ActiveDocument` switching and later cache misses.

2) **Always ensure a drawing & page exist**  
   Call `EnsureDocumentAndPage()` before drawing to guarantee `ActiveDocument`/`ActivePage` are valid.

3) **No connectors stencil required**  
   Use `Application.ConnectorToolDataObject` for edges; glue after both endpoints exist.

4) **Save the drawing, not a stencil**  
   `SaveAsVsdx()` now selects the **drawing** owning `ActivePage` (when it’s a drawing) or falls back to the first drawing in `Documents`. Normalize to an absolute path and ensure the directory exists.

5) **CLI pathing rule**  
   The CLI resolves a **relative output path against the JSON’s folder**, prints WorkingDir/JSON/Output, and catches COM exceptions with HRESULTs.

### Outcome
- Running  
  `VDG.VisioRuntime.exe samples\sample_diagram.json samples\mydiagram.vsdx`  
  now creates and opens `mydiagram.vsdx` in the expected `samples` folder without prompts or exceptions.
