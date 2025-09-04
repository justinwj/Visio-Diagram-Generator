# PROMPTS LOG — 2025-09-04

## Session: Plan alignment and runtime finalization
- Switched **Prompt 3** to a **net48** Visio runner; F# CLI remains **net8.0-windows** and calls the runner via `Process.Start`.
- Removed all **Ribbon.xml/ThisAddIn** mentions from the prompt plan.
- Pinned **No-Connectors-Stencil** rule; connectors via `ConnectorToolDataObject` glued to pins.
- Updated **VDG_psuedocode_refactored.txt** to include the rule + added **PageSizingMode** (`auto|fixed|fit-one-page`).
- Rewrote **VDG_tests_per_prompt_plan_CLI_v3.txt** for CLI↔runner lanes; dropped duplicate **Prompt 6**.
- Result: `sample_diagram.json → mydiagram.vsdx` stable; moving to Prompt 5.

Artifacts updated:
- DECISIONS.md, PROJECT_STATE.md, PROMPTS_LOG.md, Update_Team_ChatGPT.md
- VDG_Prompt_Plan_v3_CLI_v2.txt (duplicate removed; lanes clarified)
- VDG_psuedocode_refactored.txt (connectors + page sizing)
- VDG_tests_per_prompt_plan_CLI_v3.txt (CLI/runner lanes)


# PROMPTS LOG — 2025-09-03

## Session: JSON→VSDX runtime stabilization

**Initial run & failure**
```
& "src\VDG.VisioRuntime\bin\x64\Release\net48\VDG.VisioRuntime.exe" "samples\sample_diagram.json" "mydiagram.vsdx"

[INFO] Loaded stencil: BASIC_U.VSSX
[INFO] Loaded stencil: BASIC_U.VSSX

Unhandled Exception: System.Collections.Generic.KeyNotFoundException
  at VDG.VisioRuntime.Infrastructure.VisioStaHost.JobBase.Wait() ...
```
(Stencil loaded twice; missing cache entry downstream.)

**Fixes applied**
- `LoadStencil` opens **hidden+RO** and **caches** the `Document`.
- `EnsureDocumentAndPage` called before any drawing.
- Connectors via `ConnectorToolDataObject`; no connectors stencil.
- `SaveAsVsdx` saves the **drawing** and normalizes output path.

**Residual issue & resolution**
- COM “unable to write” traced to saving a stencil/relative path in a different working dir. Resolved by anchoring output to **JSON’s directory** and printing absolute paths in the CLI.

**Successful run**
```
& "…\VDG.VisioRuntime.exe" "samples\sample_diagram.json" "samples\mydiagram.vsdx"

[INFO] Loaded stencil: BASIC_U.VSSX
Saved diagram to samples\mydiagram.vsdx
```
File appears immediately and opens in Visio.
