# Specialized View Pseudocode Prompts

Applies to any solution where auxiliary data (perf, tests, releases) is available; invSys is just a sample fixture. Keep prompts domain-agnostic.

These eight diagrams rely on auxiliary data (perf logs, tests, release notes). Pseudocode must state how we ingest external metrics and merge them into diagram metadata.

## Shared Inputs
- Performance captures (`perf-smoke`, custom logs).
- Test coverage reports.
- Git history and release metadata.
- Feature flags for refactors, deprecations, migrations.

## 42. Performance Bottleneck View
- **Goal:** highlight slow procedures/modules.
- **Pseudocode:** pull timing stats, map to modules, color-code by percentile, show upstream/downstream context for hotspots.

## 43. Test Coverage View
- **Goal:** show which modules/procedures are tested.
- **Pseudocode:** import coverage JSON, align test names with modules, mark coverage percentage, flag gaps.

## 44. Onboarding View (Simplified)
- **Goal:** offer a beginner-friendly overview.
- **Pseudocode:** select essential modules/forms, hide noise, annotate with descriptions and next read recommendations.

## 45. Refactor Opportunity View
- **Goal:** surface modules ripe for cleanup.
- **Pseudocode:** combine metrics (complexity, bug history), highlight long functions, cyclical dependencies, missing tests.

## 46. “What To Change If…” View
- **Goal:** change impact analysis for specific features.
- **Pseudocode:** query dependency graph for feature tag, show affected modules, highlight risky dependencies and required tests.

## 47. Deprecation/Legacy View
- **Goal:** show legacy modules slated for removal.
- **Pseudocode:** read deprecation metadata, mark modules as legacy, show replacement paths or blockers.

## 48. Release/Version Diff View
- **Goal:** compare two releases visually.
- **Pseudocode:** diff module/edge lists between versions, annotate additions/removals/changes, include release metadata (date, author).

## 49. Migration Roadmap View
- **Goal:** outline steps for migrating to a new architecture/tooling.
- **Pseudocode:** list migration phases, link modules to phases, show prerequisites, highlight blockers.
