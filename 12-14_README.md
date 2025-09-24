# Visio Diagram Generator

This repository contains a multi‐project solution for generating Visio diagrams from structured models and VBA projects. It comprises:

* **VDG.Core.Contracts** – Portable contracts defining basic diagram entities (`Node`, `Edge`, `DiagramModel`)【67179597286132†L0-L16】.
* **VDG.Core** – Core library for analysis, including a procedure graph builder for VBA projects.
* **VisioDiagramGenerator.Algorithms** – F# library implementing a simple layout engine that positions nodes horizontally and routes edges as straight lines.
* **VisioDiagramGenerator.CliFs** – F# command line interface for generating diagrams, running layouts, invoking the COM runner and optionally uploading previews to OneDrive.
* **VDG.CLI** – Stub COM runner (net48) which will eventually render layouts to VSDX via Visio automation.

## Building and Testing

The repository uses .NET 8.0. Install the .NET SDK specified in `global.json` and run:

```bash
dotnet build Visio-Diagram-Generator.sln
dotnet test Visio-Diagram-Generator.sln
```

## Packaging

Use the PowerShell script `scripts/12-14_build-release.ps1` to publish the CLI and runner and produce zip archives under the `artifacts/` directory. The script reads the version from `Directory.Build.props` if present, or defaults to `0.1.0`. Run it from the repository root:

```powershell
pwsh -File scripts/12-14_build-release.ps1
```

Artifacts will be created:

* `VDG_CLI_[version]_win-x64.zip` – Contains the F# CLI published for Windows x64.
* `VDG_Runner_[version]_net48.zip` – Contains the stub COM runner.

## Live Preview

The CLI can upload a generated VSDX file to the current user's OneDrive using Microsoft Graph. Set the following environment variables before running:

* `VDG_GRAPH_CLIENT_ID` – Client ID of your AAD app registration.
* `VDG_GRAPH_TENANT_ID` – Tenant ID for your organisation.

Then generate a model and request a preview link:

```bash
dotnet run --project src/VisioDiagramGenerator.CliFs generate myModel.json --output myModel.vsdx --live-preview
```

You'll be prompted with a device code flow to authenticate. Once uploaded, the CLI will print a shareable URL.

## CLI Usage

```
generate <model.json> [--output <out.vsdx>] [--live-preview]
    Generates a diagram from a JSON model, runs the layout engine and writes a VSDX file. Optionally uploads a live preview.

vba-analysis <project.xlsm> [--output <out.vsdx>] [--live-preview]
    (Not yet implemented) Parses a VBA project, builds a procedure call graph and generates a diagram.

export <diagram.vsdx> <format> [--output <out.file>]
    (Not yet implemented) Converts a VSDX file to another format.

help
    Shows this usage information.
```

## Algorithms and VBA Analysis

The F# algorithms library defines `PointF`, `NodeLayout`, `EdgeRoute` and `LayoutResult` types and provides a `LayoutEngine.compute` function that produces a deterministic layout for any `DiagramModel`. Nodes are spaced evenly along the X axis and edges are straight lines. This is a starting point for future enhancements.

The procedure graph builder in `VDG.Core` reads VBA modules via an `IVbeGateway` implementation and constructs a call graph. Each procedure becomes a node, and call statements create directed edges【67179597286132†L0-L16】.
