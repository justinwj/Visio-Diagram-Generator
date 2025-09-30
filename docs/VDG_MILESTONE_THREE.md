**VDG MILESTONE THREE PLAN**

Goal
- [ ] Improve connector routing by grouping related edges, encouraging orthogonal segments, and reserving vertical/horizontal channels to cut down on overlap.

Scope
- [ ] Orthogonal routing as default (Manhattan-style right‑angle segments).
- [ ] Connection points/ports on nodes with side preferences (auto/left/right/top/bottom).
- [ ] Edge grouping/bundling (by lane, groupId, or node pair) with small offsets to keep bundles legible.
- [ ] Reserved channels between lanes to guide long connectors and reduce crossings.
- [ ] Better label placement (keep off main segments; optional mid‑segment label).

Schema Additions (backward compatible)
- [ ] `layout.routing`: `{ "mode": "orthogonal|straight", "bundleBy": "lane|group|nodePair|none", "bundleSeparationIn": number, "channels": { "gapIn": number }, "routeAroundContainers": boolean }`
- [ ] `node.ports`: `{ "inSide": "auto|left|right|top|bottom", "outSide": "auto|left|right|top|bottom" }` (hints; engine selects best on auto).
- [ ] `edge`: optional `{ "waypoints": [ {"x": number, "y": number}... ], "priority": number }` for manual overrides and conflict breaking.

CLI Flags
- [ ] `--route-mode <orthogonal|straight>`
- [ ] `--bundle-by <lane|group|nodepair|none>`
- [ ] `--bundle-sep <in>`
- [ ] `--channel-gap <in>`
- [ ] `--route-around <true|false>`
- [ ] Precedence: CLI > JSON > defaults.

Layout/Algorithm Changes
- [ ] Port selection: choose node sides based on relative lane positions and port hints.
- [ ] Channel generation: compute vertical corridors between tiers; horizontal corridors within lanes for intra‑lane edges.
- [ ] Path planning: build orthogonal polylines source→port→nearest corridor→target corridor→port. Offset bundles by index.
- [ ] Crossing reduction: prefer free corridors; nudge segments when conflicts detected; minimize doglegs.
- [ ] Pagination compatibility: edges crossing pages continue to use markers; record cross‑page paths for diagnostics.

Visio Runner Changes
- [ ] Replace raw `DrawLine` with Dynamic Connector shapes (right‑angle routing) glued to shape connection points.
- [ ] Apply route style cells (e.g., `Routestyle`, `LineRouteExt`) and keep connectors on back layer.
- [ ] If algorithm supplies explicit waypoints, set control points (or lock down segments) to preserve polylines.

Diagnostics
- [ ] Report: connector count, bundles created, crossings reduced (before/after estimate), average path length, and channel utilization.
- [ ] Warn when ports/paths cannot satisfy constraints (fallback to straight connectors).

Deliverables
- [ ] Schema + README updates for `layout.routing`, `node.ports`, and CLI flags.
- [ ] Router implementation in algorithms with ports/channels/bundles.
- [ ] Visio runner: dynamic connectors + glue + route style; waypoint support as available.
- [ ] Samples: one dense, cross‑lane architecture showcasing bundles + channels.
- [ ] Tests/fixtures: golden outputs; console diagnostics demonstrating reduced crossings.

Acceptance Criteria
- [ ] Connectors render as orthogonal segments by default.
- [ ] Bundling reduces visual clutter; bundles visibly offset; labels not obstructed.
- [ ] Long cross‑lane connectors prefer channels; fewer overlaps/crossings vs. M2 baseline.
- [ ] Pagination behavior preserved; markers still clear and labeled.
- [ ] Configurable via JSON/CLI; defaults do not break existing diagrams.

Recommended Starting Sequence
1) Schema + CLI for `layout.routing` and port hints.
2) Switch runner to dynamic connectors + glue (immediate orthogonal improvement leveraging Visio).
3) Implement ports + simple corridor routing between lanes.
4) Add bundling with offset and diagnostics (crossings, lengths).
5) Optional waypoints + label positioning tweaks.

