# Dataflow View Pseudocode Prompts

Data-centric diagrams explain how information moves through any VBA-based solution; invSys remains only an example fixture. Pseudocode must outline how datasets, operations, and edges are extracted.

## Shared Inputs
- Named ranges, tables, and worksheet columns referenced in code.
- External data sources (CSV, Access, SQL, web APIs).
- CRUD metadata inferred from verbs (Load, Save, Update, Delete).
- Logging/snapshot artifacts captured in metadata.

## 27. CRUD Flow View
- **Goal:** show create/read/update/delete paths for key entities.
- **Pseudocode:** identify entity names, scan procedures for CRUD verbs, classify edges (C/R/U/D), annotate data stores.
- **Questions:** how to differentiate read vs. calculate, handling batch operations.

## 28. Cross-Module Dataflow
- **Goal:** highlight data moving between modules (e.g., Worksheet → Module → Form).
- **Pseudocode:** follow variable/parameter annotations, use metadata to mark producers/consumers, draw directional edges showing data lineage.
- **Need:** heuristics for shared global state vs. explicit parameters.

## 29. Report/Export Flow
- **Goal:** show how reports are generated and exported.
- **Pseudocode:** detect procedures writing files/emails, map upstream data sources, annotate output formats and destinations.
- **Outstanding:** capturing scheduled exports vs. ad-hoc.

## 30. Logging & Snapshot Dataflow
- **Goal:** show where logs/snapshots are written and consumed.
- **Pseudocode:** scan for logging APIs, build nodes for log sinks (files, worksheets), map readers if any.
- **Clarify:** severity levels, retention, cross-link with error architecture.

## 31. Transaction Dataflow
- **Goal:** portray transactional steps (inventory transactions, approvals).
- **Pseudocode:** identify transaction boundaries, show sequence of data mutations, highlight rollback/undo paths.
- **Need decisions on:** representing concurrency (if any), linking to workflow diagrams for context.
