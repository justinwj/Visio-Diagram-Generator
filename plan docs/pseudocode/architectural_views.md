# Architectural View Pseudocode Prompts

Applies to any VBA-based (or future language) solution; invSys is only a sample fixture for illustration. Use these prompts as a general template.

These eight views explain how the VBA codebase is structured internally. Pseudocode must specify the extraction and aggregation steps before implementation begins.

## Shared Concerns
- Which metadata fields distinguish UI/Logic/Data/Integration layers?
- How do we surface forms, worksheet modules, class modules, and standard modules distinctly?
- What diagnostics identify missing metadata or ambiguous ownership?

## 6. Layered Architecture View
- **Goal:** UI → Logic → Data → Integration pipeline.
- **Pseudocode:** map each module to a layer, compute lane occupancy, summarize cross-layer edges, annotate hotspots (overloaded lanes).
- **Questions:** fallback layer assignment rules, handling multi-layer modules, treatment of shared utilities.

## 7. Module Internal Structure View
- **Goal:** show submodules, classes, and forms within a module container.
- **Pseudocode:** traverse module IR, list procedures/forms, build containment nodes, highlight key members (public subs, state variables).
- **Need:** heuristics for collapsing helpers, labeling patterns (event handlers vs. helpers).

## 8. Component Communication View
- **Goal:** emphasize flows between major components (forms, services, data adapters).
- **Pseudocode:** aggregate call graph edges by component pairs, compute weight buckets, show synchronous vs. asynchronous (if any).
- **Clarify:** threshold for showing edges, bundling rules for cyclic communication.

## 9. Data Architecture View
- **Goal:** depict datasets, tables, files, logs, and bindings.
- **Pseudocode:** extract references to named ranges, tables, external files; classify operations (read/write); emit data nodes plus binding edges.
- **Open issues:** detecting ad-hoc range usage, linking forms to data contexts.

## 10. Error & Logging Architecture View
- **Goal:** track error propagation and logging sinks.
- **Pseudocode:** scan for `On Error` usage, logging calls, build nodes for handlers/loggers, show propagation paths and failover behaviors.
- **Need clarity on:** severity levels, fallback paths, global vs. per-module logging facilities.

## 11. Security / Permissions View
- **Goal:** visualize roles, guarded operations, protected sheets/forms.
- **Pseudocode:** identify security checks (password prompts, role gating), map to resource nodes, show allowed pathways.
- **Questions:** metadata source for roles, how to highlight missing checks.

## 12. Configuration View
- **Goal:** document app settings, environment variables, workbook-defined config.
- **Pseudocode:** parse named ranges/config files, map modules that consume settings, show flow from config to behavior.
- **Unresolved:** supporting multiple environments, distinguishing constants vs. runtime settings.

## 13. State-Transition View
- **Goal:** show how state changes during key operations.
- **Pseudocode:** derive states from worksheets/forms (Idle, Editing, Validating), map transitions triggered by user or automation, annotate guard conditions.
- **Need:** canonical state model definitions, approach for composite states or nested workflows.
