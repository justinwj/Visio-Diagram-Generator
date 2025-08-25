# VDG — Project State (Source of Truth)

**Current prompt:** 2 — Core contracts (compile-first)  
**Owner now:** TBD  
**Branch:** TBD (agent fills in during Agent mode)  
**Last update:** 2025-08-20T18:01:10Z

## Invariants (do not drift)
- **TFMs**: Add-in **net48**; `VDG.Core` **netstandard2.0**; `VDG.Core.Contracts` **netstandard2.0**; Tests **net8.0**
- **Office/bitness**: Excel 365 64‑bit; Visio 2024 64‑bit
- **Interop**: `Microsoft.Office.Interop.Visio` and `Microsoft.Office.Interop.Excel` via NuGet (no direct GAC/PIA)
- **Visio automation policy**: start a **new Visio instance** for integration; default to **FakeVisio** in tests
- **Smoke output**: `C:\Users\justu\OneDrive - Black Scottie Chai\Solution Notes\VDG-<timestamp>.vsdx`

## Next action
- Awaiting go to execute Prompt 2 deliverables (compile‑first core contracts) or adjust per your instruction.

## Done recently
- Prompt 1 — Solution scaffold & references: status/commit TBD (confirm on repo)

