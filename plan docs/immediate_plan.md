# Immediate Plan (delete below lists when done, this MD should not get long)

Based on the approved vision and the current project maturity (with stable semantics, planner integration, and golden validation now in place), the next highest-impact areas for Codex to tackle are:
	1. Stakeholder-Rich Review Feedback **(Done – 2025-11-09)**:
The CLI now emits semantic/planner summaries at the console, persists a `.review.txt/.review.json` pair beside each diagram, injects the JSON into diagram metadata, and mirrors the same block into `*.diagnostics.json` so reviewers (or CI) can see subsystem splits, lane pressure, missing roles, and suggestions without opening Visio. Runtime review sensitivity is configurable via `--review-severity-threshold`, `--role-confidence-cutoff`, and `--review-flow-residual-cutoff` (or `VDG_REVIEW_*` env vars); suppression notes are captured for traceability. Remaining follow-ups: surface these summaries inside reviewer dashboards/portal views once stakeholders are ready.
	2. Seed/Override File Integration **(Done – 2025-11-10)**:
Seed schema documented (`plan docs/taxonomy_seed_schema.md`), CLI flags (`--taxonomy-seed`, `--seed-mode`, plus env mirrors) implemented, and `SemanticArtifactsBuilder` layers overrides before planner consumption. Outputs now capture `seeded=true` evidence and seed provenance in metadata/review files so reviewers can audit what was overridden versus inferred. Follow-ups: polish onboarding docs and optional dashboard surfacing of seeded fields.

	3. Advanced Layout and Flow Scenarios **(In Progress – advanced render wiring 2025-11-10)**:
Planner emits lane segments, flow bundles, and cycle clusters; Visio runner now renders heat bands, overflow badges, and per-page legends. Fixtures regenerated with advanced mode enabled (invSys by default) and review hashes tracked in the ledger to keep `.review.*` goldens deterministic. Follow-ups: continue refining planner heuristics and add additional dense fixtures if stakeholders request more coverage.

	4. Documentation and Reviewer Guidance **(Updated 2025-11-10)**:
`docs/AdvancedLayouts.md` and `docs/Design.md` document advanced switches, cue interpretations, and ledger expectations. Keep onboarding material fresh (review dashboard, `.review.*` semantics) as more reviewers adopt the new workflow.
