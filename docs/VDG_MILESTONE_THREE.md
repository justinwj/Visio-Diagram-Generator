**VDG MILESTONE THREE PLAN**

Goal
- [ ] Improve connector routing by grouping related edges, encouraging orthogonal segments, and reserving vertical/horizontal channels to cut down on overlap.

Scope
- [x] Orthogonal routing as default (Manhattan-style right-angle segments).
- [x] Connection points/ports on nodes with side preferences (auto/left/right/top/bottom) - minimal: side selection by tier with GlueToPos.
- [x] Edge grouping/bundling (by lane, groupId, or node pair) with small offsets to keep bundles legible (offsets applied to side glue positions).
- [x] Reserved channels between lanes to guide long connectors and reduce crossings (corridor midlines + staggering in channels).
- [x] Better label placement (detached text boxes on longest segment, configurable offset).

Schema Additions (backward compatible)
- [x] `layout.routing`: `{ "mode": "orthogonal|straight", "bundleBy": "lane|group|nodePair|none", "bundleSeparationIn": number, "channels": { "gapIn": number }, "routeAroundContainers": boolean }`
- [x] `node.ports`: `{ "inSide": "auto|left|right|top|bottom", "outSide": "auto|left|right|top|bottom" }` (hints; engine selects best on auto).
- [x] `edge`: optional `{ "waypoints": [ {"x": number, "y": number}... ], "priority": number }` for manual overrides and conflict breaking.

CLI Flags
- [x] `--route-mode <orthogonal|straight>`
- [x] `--bundle-by <lane|group|nodepair|none>`
- [x] `--bundle-sep <in>`
- [x] `--channel-gap <in>`
- [x] `--route-around <true|false>`
- [x] Precedence: CLI > JSON > defaults.

Layout/Algorithm Changes
- [x] Port selection: choose node sides based on relative lane positions and port hints (minimal: side selection by tier order; GlueToPos with side fallback).
- [x] Channel generation (minimal): compute vertical corridor midlines; when `channels.gapIn` is set and no `bundleBy` specified, default bundling to `lane` for more orderly flows across corridors.
- [x] Path planning (minimal): for cross-lane edges, draw orthogonal polylines routed through averaged corridor midlines (H–V–H). Per-edge offsets applied at attach points; straight/connector fallback when corridors unavailable.
- [x] Crossing reduction (minimal): stagger corridor X positions per lane bundle index using `channels.gapIn` to reduce overlaps among parallel cross-lane edges.
- [x] Bundling offsets: groups computed per `bundleBy` and applied as small offsets along glue positions (left/right/top/bottom).
- [x] Pagination compatibility: edges crossing pages continue to use markers; record cross-page paths for diagnostics.

Visio Runner Changes
- [x] Replace raw `DrawLine` with Dynamic Connector shapes (right-angle routing) glued to shape connection points (now attempts GlueToPos on lane-facing sides; falls back to PinX).
- [x] Apply route style cells (e.g., `Routestyle`, `LineRouteExt`) and keep connectors on back layer.
- [x] If algorithm supplies explicit waypoints, draw explicit polylines honoring waypoints (labels detached). Connectors still used as fallback for non-corridor routes.

Diagnostics
- [x] Report: connector count.
- [x] Report: bundle group counts (by selected `bundleBy`) and max bundle size.
- [x] Report: baseline straight-line crossing estimate; list vertical corridor positions when `channels.gapIn` is set.
- [x] Report: planned route crossings (after), average path length, and channel utilization.
- [x] Warn when corridor is missing for cross-lane edges; warn when bundleSeparationIn is ineffective for tiny shapes. (Port/connector fallback warnings left for a later milestone.)

Deliverables
- [x] Schema + README updates for `layout.routing`, `node.ports`, and CLI flags.
- [x] Router implementation (minimal) with ports/channels/bundles offsets and corridor-aware polylines.
- [x] Visio runner: dynamic connectors + glue + route style; waypoint support pending.
- [x] Samples: a dense multi-lane sample at `samples/m3_dense_sample.json`, cross-lane stress at `samples/m3_crosslane_stress.json`, tiny-shapes bundle-warning at `samples/m3_tiny_bundle_warning.json`.
- [x] Tests: `VDG.CLI.Tests` validates routing flags, bundle/corridor diagnostics, planned routes/utilization, waypoint count, and bundle-separation warning. `VDG_SKIP_RUNNER` avoids COM in CI.

Acceptance Criteria
- [x] Connectors render as orthogonal segments by default.
- [ ] Bundling reduces visual clutter; bundles visibly offset; labels not obstructed. (visual acceptance)
- [ ] Long cross-lane connectors prefer channels; fewer overlaps/crossings vs. M2 baseline. (verified via stress sample and diagnostics)
- [x] Pagination behavior preserved; markers still clear and labeled.
- [x] Configurable via JSON/CLI; defaults do not break existing diagrams.

Recommended Starting Sequence
1) Schema + CLI for `layout.routing` and port hints. [completed]
2) Switch runner to dynamic connectors + glue (immediate orthogonal improvement leveraging Visio). [completed]
3) Implement ports + simple corridor routing between lanes. [in progress: corridor diagnostics only]
4) Add bundling with offset and diagnostics (crossings, lengths). [in progress: diagnostics only]
5) Optional waypoints + label positioning tweaks. [pending]
