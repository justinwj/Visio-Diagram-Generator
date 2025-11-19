# Pseudocode Coverage Map

## Goals Snapshot
- **Immediate tactic (plan docs/immediate_plan.md)** – stay focused on a single `invSys` slice (`modInvMan.AddGoodsReceived_Click`), verify planner output + Visio runner end-to-end, and exit only when that subroutine renders with zero skipped connectors and documented toggles.
- **Strategy gates (plan docs/strategy.md)** – Gate 0 insists on deterministic semantics (taxonomy/roles/flows) before any layout, Gate 1 reviews those semantics with stakeholders, and Gate 2 drives deterministic spatial layout: channel-first routing, strict capacity caps, and a Visio runner that simply honors the precomputed geometry.

## Existing Code Files Requiring Pseudocode
| File | Why pseudocode helps | Scope of the pseudocode |
| --- | --- | --- |
| `src/VDG.VBA.CLI/Program.cs` | The IR→diagram CLI currently mixes argument parsing, semantic enrichment, layout metadata, pagination controls, and artifact emission. Without structured pseudocode it is hard to reason about the checkpoints that guarantee the invSys slice obeys Gate 0/1 contracts before we call the planners. | Sketch the full pipeline: parse args, load IR, run `SemanticArtifactsBuilder`, apply metadata to nodes/edges, prune fixture slices, emit diagnostics, write taxonomy/flows/review files, and invoke the planners (view vs. print). Highlight timeout/error paths and how we feed the Visio runner. |
| `src/VDG.VBA.CLI/Semantics/SemanticArtifactsBuilder.cs` | This class houses the heuristics that classify modules/procedures into subsystems, teams, and roles. To tune those heuristics for `Worksheet_Change` flows we need pseudocode that explains scoring, seed overrides, and metadata writes. | Describe how we walk modules, derive tiers, classify subsystems, apply taxonomy seeds, infer tags, compute confidences, and emit module/procedure maps. Include where reasons/tags are stored so plan reviewers can validate the logic before code changes. |
| `src/VDG.Core/Analysis/ProcedureGraphBuilder.cs` | Builds the raw call graph from VBA modules; we must extend it to understand control-flow subtleties in the screenshots (event handlers, row/cell loops). | Pseudocode should cover: exporting modules, identifying procedure declarations, resolving implicit call targets across modules, and attaching metadata (event handler flag, worksheet scope). Note early exits when targets are absent so we can plan diagnostics. |
| `src/VisioDiagramGenerator.Algorithms/AST.fs` | Empty placeholder; we lack a documented AST for semantic nodes/edges, making it hard to describe how invSys metadata maps into the planners. | Define the structures and traversal pseudocode for forms, modules, procedures, flow bundles, and semantic tags; call out how worksheet events differ from command handlers. |
| `src/VisioDiagramGenerator.Algorithms/DataModels.fs` | Another placeholder; we need a normalized dataset (modules, tiers, bundles, overflow buckets) that the planners consume. | Outline pseudocode for shaping `DiagramDataset`, connector stats, module weights, and residuals that later feed `PagingPlanner` and `ViewModePlanner`. |
| `src/VisioDiagramGenerator.Algorithms/Transformations.fs` | Missing implementation for transforming semantic IR into planner-friendly `DiagramModel` objects. | Describe the transformation pipeline: merge taxonomy + flow annotations, collapse bundles, derive node sizes, emit metadata keys (tier/role/channel hints), and attach diagnostics (skipped connectors, overflow). |
| `src/VisioDiagramGenerator.Algorithms/LayoutEngine.fs` | Provides tiered grid placement but lacks a narrative for how spacing, tier ordering, and fallback edge routes interact. Understanding this is critical before tightening corridor math. | Add pseudocode detailing: tier ordering lookup, bucket creation per tier, per-column centering, node layout emission, and fallback edge routing that simply draws center-to-center segments. |
| `src/VisioDiagramGenerator.Algorithms/LayoutAlgorithms.fs` | Stub file; intended to host the richer channel/corridor algorithms promised in Gate 2 but currently empty. | Document the intended steps: compute lane corridors, allocate edge bundles per module pair, generate per-channel anchor points, handle intra-module loops, and spillovers when limits are exceeded. |
| `src/VisioDiagramGenerator.Algorithms/ViewModePlanner.fs` | Large, high-impact planner that already manipulates spacing profiles, lane segments, channel labels, and page contexts. Pseudocode will let us reason about tweaks before editing hundreds of lines. | Break down the flow: ingest semantic metadata, compute spacing profile, bucket modules per tier, build lane segments under capacity rules, assign connectors to channels, synthesize `FlowBundlePlan`, `PageContextPlan`, and `ChannelLabel` objects, and emit diagnostics for overflow/partial renders. |
| `src/VisioDiagramGenerator.Algorithms/PagingPlanner.fs` | Page splitting logic balances connectors, occupancy, module spans, and lane splits—perfect territory for regression bugs without a scripted plan. | Write pseudocode that walks sorted modules, tracks running totals, flushes pages when any threshold trips, preserves span ranges, and records occupancy/height math for diagnostics. |
| `src/VDG.CLI/Program.cs` | The Visio automation runner coordinates lane containers, placements, connector drawing, bridging, layers, summaries, and diagnostics. Pseudocode will clarify how planner output is honored and where Visio-specific toggles kick in. | Capture the sequence: hydrate layout/plan, compute placements per page (multi-page vs. single), draw nodes, connectors, callouts, channel labels, apply layers, disable reroute/snap, record skipped connectors, and emit structured diagnostics (lane occupancy, bridges, partial renders). |
| `src/VDG.VisioRuntime/Rendering/VisioJsonRenderer.cs` | Even the simplified JSON runner needs a narrated plan so we do not regress document setup or shape ordering when integrating with new planners or templates. | Outline pseudocode for: ensure document/page, load stencils once, drop shapes (master vs. primitive), cache Visio shape IDs, draw connectors in a second pass, label them, and finish with page resize/fit. |

## Proposed New Pseudocode Artifacts
| Proposed File | Purpose | Scope |
| --- | --- | --- |
| `plan docs/pseudocode/invSys_addgoodsreceived.md` | Capture the business logic of the `modInvMan.AddGoodsReceived_Click` slice (Worksheet_Change gating through dictionary rebuilds) so future planners/semantics know which blocks and loops must appear. | Describe each decision/loop from the screenshots: target detection, event disabling, dictionary rebuild, ITEM_CODE + ROW sequencing, error handler. Link each block to expected node labels/edge labels. |
| `plan docs/pseudocode/semantic_gate0.md` | Provide a cross-cutting script for Gate 0 so reviewers can validate the taxonomy + role detection flow without diving into `SemanticArtifactsBuilder`. | Show the order: load IR, apply seed overrides, classify modules, classify procedures, compute residuals, emit taxonomy/flows/review JSON, and surface warnings when confidence falls below thresholds. |
| `plan docs/pseudocode/render_pipeline.md` | Document the full “IR → planners → Visio automation” choreography to keep the slice-first strategy understandable to new contributors. | Walk through staging fixtures, running `ViewModePlanner` + `PagingPlanner`, stitching layout plans into CLI metadata, invoking `VDG.CLI`, handling Visio toggles, capturing diagnostics, and saving screenshots/logs for regression hooks. |

## View Mode Coverage Requirements
The end user needs to pivot among 49+ view modes. Each category below lists the supporting code surfaces that require pseudocode plus the type of logic they must capture.

### 1. System-Level Views (1–5)
- **Supporting code:** `SemanticArtifactsBuilder`, `Transformations.fs`, `ViewModePlanner.fs`.
- **Pseudocode focus:** describe how taxonomy layers collapse into “systems/subsystems/external dependency” nodes, how module boundaries are summarized into bundles, and how runtime behavior metadata (request/event sequences) feeds high altitude diagrams.
- **New artifact:** `plan docs/pseudocode/system_views.md` – outlines per-view pipelines for Ecosystem Map, Service Boundary, Deployment/Hosting, Integration Interfaces, and Runtime Behavior Overview.

### 2. Architectural Views (6–13)
- **Supporting code:** `VDG.Core/Analysis`, `VisioDiagramGenerator.Algorithms/DataModels.fs`, `LayoutAlgorithms.fs`, `ViewModePlanner.fs`.
- **Pseudocode focus:** detail how layered tiers (UI→Logic→Data) map to Visio tiers, how intra-module structure is extracted (forms, classes), and how telemetry/security/configuration metadata flows into diagram decorations and diagnostics.
- **New artifact:** `plan docs/pseudocode/architectural_views.md` describing heuristics per view (Layered Architecture, Module Structure, Component Communication, Data Architecture, Error & Logging, Security, Configuration, State Transitions).

### 3. Workflow / Process Views (14–22, unbounded)
- **Supporting code:** `ProcedureGraphBuilder.cs`, `Transformations.fs`, `ViewModePlanner.fs`, `PagingPlanner.fs`.
- **Pseudocode focus:** show how we select entry procedures, expand only related calls, annotate with business labels, and guarantee pagination + corridor planning survives 10–50 workflows.
- **New artifact:** `plan docs/pseudocode/workflow_views.md` containing a template for describing each workflow plus guidance on slicing fixtures and mapping roles/lanes.

### 4. UI / Surface Views (23–26)
- **Supporting code:** `VDG.VBA.CLI/Semantics` (forms + worksheet metadata), `Transformations.fs`, `ViewModePlanner.fs`.
- **Pseudocode focus:** enumerate UserForms/worksheets, surface control hierarchies, and map control events to handlers/modules to satisfy forms map, controls interaction, event entry points, and navigation flows.
- **New artifact:** `plan docs/pseudocode/ui_surface_views.md` documenting extraction of controls metadata, navigation edges, and gating rules (e.g., worksheet vs. module context).

### 5. Dataflow Views (27–31)
- **Supporting code:** `VDG.Core.Analysis`, `DataModels.fs`, `Transformations.fs`, `ViewModePlanner.fs`.
- **Pseudocode focus:** capture CRUD semantics, cross-module data handoffs, exports, logging snapshots, and transaction flows. Needs explicit description of how we derive dataset nodes from IR (records, tables, named ranges).
- **New artifact:** `plan docs/pseudocode/dataflow_views.md` clarifying heuristics that detect data sources/sinks and how edges are labeled by operation (Create/Read/etc.).

### 6. Code-Level / Developer Views (32–41)
- **Supporting code:** `ProcedureGraphBuilder.cs`, `VDG.CLI/Program.cs` (diagnostics, filtering), `ViewModePlanner.fs`, `LayoutAlgorithms.fs`.
- **Pseudocode focus:** filtered call graph generation, event graph overlays, dependency matrices, error propagation tracing, function responsibility tagging, hotspot calculations, CFG extraction, and data-structure diagrams.
- **New artifact:** `plan docs/pseudocode/developer_views.md` describing how each developer-facing view samples data, what metadata is required, and how we prevent full-system overload (per-module scopes).

### 7. Specialized Views (42–49)
- **Supporting code:** `SemanticReviewReporter`, `VDG.CLI` diagnostics, `VisioDiagramGenerator.Algorithms` stats modules.
- **Pseudocode focus:** map performance profiles, coverage data, onboarding simplifications, refactor signals, change-impact overlays, legacy flags, diff timelines, and migration roadmaps onto diagram data.
- **New artifact:** `plan docs/pseudocode/specialized_views.md` documenting data sources (perf logs, git history, coverage, release metadata) and how they decorate diagrams without rewriting core planners.
