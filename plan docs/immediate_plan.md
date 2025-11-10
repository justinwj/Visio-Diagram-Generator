# Immediate Plan (delete below lists when done, this MD should not get long)

Based on the approved vision and the current project maturity (with stable semantics, planner integration, and golden validation now in place), the next highest-impact areas for Codex to tackle are:
	1. Stakeholder-Rich Review Feedback **(Done – 2025-11-09)**:
The CLI now emits semantic/planner summaries at the console, persists a `.review.txt/.review.json` pair beside each diagram, injects the JSON into diagram metadata, and mirrors the same block into `*.diagnostics.json` so reviewers (or CI) can see subsystem splits, lane pressure, missing roles, and suggestions without opening Visio. Runtime review sensitivity is configurable via `--review-severity-threshold`, `--role-confidence-cutoff`, and `--review-flow-residual-cutoff` (or `VDG_REVIEW_*` env vars); suppression notes are captured for traceability. Remaining follow-ups: surface these summaries inside reviewer dashboards/portal views once stakeholders are ready.
	2. Seed/Override File Integration:
Enable partial or full seed/override ingestion (e.g., for known team ownership, special module roles, or custom subsystems) so advanced users or stakeholders can tune classifications without retraining or changing heuristics. This further aligns the pipeline with the project’s "reviewable, deterministic, and extensible" vision.
	3. Advanced Layout and Flow Scenarios:
Extend the planner and renderer to gracefully visualize edge-case layouts—such as highly interconnected subsystems, dynamic call graphs, or multi-layer aggregates. This ensures even complex VBA workloads get readable, reviewable diagrams.
	4. Documentation and Reviewer Guidance:
Generate up-to-date vision-centric documentation (possibly automate extraction from golden artifacts/metadata) so future contributors and reviewers understand the classification pipeline, seed workflow, and semantic concepts with minimal onboarding friction.

