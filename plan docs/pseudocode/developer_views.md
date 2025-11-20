# Developer-Facing View Pseudocode Prompts

Applies to any VBA-based (or future) codebase; invSys is only an example fixture. These prompts should stay language/domain-agnostic.

These ten diagrams target engineers hunting for bugs, regressions, or code understanding. Pseudocode needs to describe filtering, metrics, and layout rules for each.

## Shared Requirements
- Support per-module scopes to avoid full-system overload.
- Provide toggles for depth, edge weights, highlighting (e.g., hotspots).
- Explain how diagnostics feed into visual cues (color, badges, labels).

## 32. Call Graph View (filtered)
- **Goal:** show call relationships within a module or bounded neighborhood.
- **Pseudocode:** select module, expand N hops, bundle helpers, annotate edge direction/count, ensure readability <50 connectors.

## 33. Event Graph
- **Goal:** map events to their handlers and subsequent calls.
- **Pseudocode:** detect event procedure signatures, link to controls/worksheets, show downstream routines distinct from generic call graph.

## 34. Module Dependency View
- **Goal:** show which modules call or depend on others.
- **Pseudocode:** aggregate call graph edges at module level, compute in/out degree, highlight strong dependencies or mutual recursion.

## 35. Error Propagation View
- **Goal:** visualize try/catch/On Error boundaries and where errors bubble.
- **Pseudocode:** parse error handling constructs, build nodes for handlers, show propagation edges toward calling modules or global error loggers.

## 36. Function Responsibility Map
- **Goal:** annotate what each function is responsible for (roles, data touched).
- **Pseudocode:** rely on semantic metadata (primary role, tags), generate legend per role, highlight mismatches (e.g., UI function touching data).

## 37. Hotspot View
- **Goal:** show functions/modules changing most often.
- **Pseudocode:** integrate git history metrics, assign heat scores, overlay on call graph or module map with gradient coloring.

## 38. Anchor Classes / Core Routines View
- **Goal:** highlight central routines and their dependencies.
- **Pseudocode:** compute centrality metrics, pick anchors, show concentric rings or layered dependency tree.

## 39. Threading / Asynchrony View
- **Goal:** (mostly trivial) confirm whether any async/timer logic exists.
- **Pseudocode:** detect `Application.OnTime`, background tasks, show control flow for those routines, mark synchronous default if none found.

## 40. Control Flow Graph (CFG)
- **Goal:** high granularity view of branches, loops, exits within a function.
- **Pseudocode:** parse procedure AST, emit nodes per basic block, edges for branch conditions, show error handlers as separate nodes.

## 41. Data Structure View
- **Goal:** display type definitions, fields, and relationships.
- **Pseudocode:** extract custom classes/types, map relationships (composition, usage), show field metadata (type, optionality).
