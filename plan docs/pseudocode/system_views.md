# System-Level View Pseudocode Prompts

These five diagrams provide the “why” behind invSys. Each requires structured pseudocode so the planners know which nodes, edges, and metadata to emit.

## Shared Questions
- What are the canonical system/subsystem boundaries (Excel host, invSys workbook, integrations)?
- Which metadata fields expose external vs. internal modules?
- How do we represent runtime environment (machines, users) that are not VBA modules?

## 1. Ecosystem Map
- **Goal:** show every system, subsystem, and external dependency.
- **Pseudocode:** enumerate subsystems from taxonomy, map each to a node type, add external nodes for APIs/files, emit edges for data/control exchange.
- **Details to answer:** how to collapse hundreds of modules into <20 blocks; how to tag edges with protocol (COM, HTTP, file).

## 2. Service/Module Boundary View
- **Goal:** visualize major modules/components and their dependencies.
- **Pseudocode:** group modules by subsystem, compute dependency weights, emit directed edges with strength or call counts.
- **Details needed:** dependency thresholding (avoid clutter), ordering by gate/priority, highlighting circular dependencies.

## 3. Deployment & Hosting View
- **Goal:** describe where invSys code executes (Excel desktop, shared drives, databases).
- **Pseudocode:** list runtime hosts from config, attach modules to hosts, draw data paths between hosts and storage.
- **Clarify:** how to detect data stores (Access, SQL, CSV), how to represent human operators vs. services.

## 4. Integration Interfaces View
- **Goal:** depict APIs, imports/exports, and scheduled tasks.
- **Pseudocode:** derive interfaces from metadata (file paths, HTTP calls), classify direction (import/export/both), annotate with frequency or schedule.
- **Outstanding:** mapping macros that run on timers, capturing file formats.

## 5. Runtime Behavior Overview
- **Goal:** show typical runtime flow (e.g., inventory update).
- **Pseudocode:** choose canonical scenarios, order high-level steps, map to subsystems, annotate triggers (events/requests).
- **Need decisions on:** how many scenarios to overlay, whether to show alternate paths or just “happy path”.
