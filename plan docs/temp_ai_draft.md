# Refinement Notes

## Threshold Calibration
- Establish reference datasets (e.g., invSys full callgraph, cross_module_calls) and automate metric collection: modules per lane, connectors per channel, skipped edge counts.
- Run iterative planner sweeps, adjusting caps (`maxModulesPerLane`, `maxConnectorsPerRow`, corridor width) and storing results in a calibration log so density vs. readability trends are documented.
- Promote stable defaults once two successive runs hold connector skips at 0 and maintain >= 15% whitespace around module clusters; keep the calibration suite in CI to guard regressions.

## Overflow & Truncation Handling
- Extend the layout payload with explicit overflow records (`hiddenNodes`, `collapsedBundles`, `badgeLabel`) per module so the runner can render consistent summarization.
- Provide design guidance: badges near the card corner, hover/callout panels listing hidden items, and optional drill-through links for detail diagrams.
- Document overflow semantics in the CLI help and strategy docs so downstream consumers know how to interpret truncated areas.

## User-Driven Parameterization
- Expose key planner knobs through CLI/config (`--view-max-modules-per-lane`, `--view-corridor-width-in`, `--view-row-spacing-in`).
- Tag layout metadata with the effective values so rendered files record the configuration that produced them.
- Add validation to warn when user-supplied parameters exceed safe limits (for example, corridors narrower than 0.5in) and fall back to defaults when omitted.

## Bridge Navigation Experience
- For each cross-page bundle, emit both visual stubs and metadata (`targetPage`, `targetModule`, hyperlink URI). The runner can convert these into labelled callouts with "Go to page X" hyperlinks.
- Standardize callout styling (color, icon, orientation) so bridge entries are instantly recognizable.
- Maintain a manifest or index page listing all bridges for quick navigation across multi-file outputs.

## Modularization & Extensibility
- Keep corridor/channel planning in isolated modules (for example, `ChannelPlanner` in F#) returning typed descriptors; avoid coupling to Visio-specific notions.
- Define interfaces for bundle scoring, slot assignment, and label placement so new edge styles or layout profiles can plug in without rewrites.
- Cover the channel planner with property-based tests to ensure future extensions preserve invariants (no overlapping corridors, monotonic spacing).

## Screen vs. Print Profiles
- Introduce layout profiles that bundle parameter sets: `screen` (wider spacing, relaxed pagination, high emphasis on whitespace) vs `print` (tighter spacing, paper size fit).
- Allow profiles to inherit common defaults while overriding key metrics (tier spacing, corridor width, max nodes per page).
- Document recommended usage per profile and provide CLI switches (`--profile screen|print|export`) so users can generate the appropriate layout without manual tuning.
