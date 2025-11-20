**VDG MILESTONE FOUR PLAN**

Goal
- [x] Support containers (zones/tiers) to give visual structure even with interim colors and to improve scanability of dense diagrams.

Scope
- [x] Containers for lanes/tiers: visually group nodes in each tier with rounded rectangles, titles, and optional icons.
- [x] Nested containers: allow sub-zones within a tier (e.g., bounded service groups).
- [x] Container styles: light fills, dashed outlines by default; customizable via metadata.
- [x] Container-aware layout: respect container padding, avoid overlaps, and route connectors around container bounds when configured.
- [x] Export semantics: persist container names in document properties for downstream automation.

Schema Additions (backward compatible)
- [x] `layout.containers`: `{ paddingIn: number, cornerIn: number, style?: { fill?: string, stroke?: string, linePattern?: string } }` (defaults: padding 0.3, corner 0.12).
- [x] Node metadata: `containerId` (to assign nodes to an explicit container; default is implicit tier container).
- [x] Container list (optional): `containers`: array of `{ id, label, tier, bounds?: { x,y,width,height }, style? }` for explicit sub-zones.

CLI Flags
- [x] `--container-padding <in>`
- [x] `--container-corner <in>`
- [x] `--route-around-containers <true|false>` (alias of existing routing flag)

Layout/Algorithm Changes
- [x] Compute tier container bounds from node placements plus padding and corner radius.
- [x] For explicit sub-containers, compute bounds from member nodes; validate containment within the parent tier.
- [x] Update routing to prefer corridor paths that skirt container edges when `routeAroundContainers` is true (offset midlines from container bounds by `container-padding/2`).
- [x] Reserve label clearance at top-left of each container for container titles.

Visio Runner Changes
- [x] Draw tier containers before nodes; send containers to back layer.
- [x] Apply style cells from `layout.containers.style` or per-container style.
- [x] Render container titles (bold) and optional tag line (e.g., environment/zone name).

Diagnostics
- [x] Report container count, container padding/corner radius, and any nodes not assigned to a valid container.
- [x] Warn when sub-container overflows parent tier or overlaps peers.

Deliverables
- [x] Schema updates for containers. (README updated)
- [x] Tier and sub-container drawing in CLI runner.
- [x] Routing aware of containers when `routeAroundContainers` is set.
- [x] Tests: container diagnostics (count, settings) and unknown container warning via CLI with VDG_SKIP_RUNNER.
- [x] Samples: explicit sub-containers inside a tier.

Acceptance Criteria
- [x] Containers render for each tier with correct bounds and titles.
- [x] Sub-containers render inside parent tier without overlap.
- [x] Connectors route around containers when enabled.
- [x] Backward compatible with M1-M3 diagrams (containers optional).

Recommended Sequence
- [x] Schema + CLI for `layout.containers` and explicit `containers[]`.
- [x] Compute/draw tier containers and titles.
- [x] Add sub-containers and containment checks.
- [x] Container-aware routing offsets (skirt edges).
- [x] Samples + diagnostics + tests.

Notes
- Sample: `samples/m4_containers_sample.json` (works with Debug/Release builds).
- CLI flags: `--container-padding`, `--container-corner`, `--route-around`.
- PowerShell direct run (Debug example): `& "src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe" "samples\m4_containers_sample.json" "out\m4_containers_sample.vsdx"`.
- Exported properties (Visio DocumentSheet User cells):
  - `User.ContainerCount` (numeric)
  - `User.ContainerIds` (CSV string)
  - `User.ContainerLabels` (CSV string)
  - `User.ContainerTiers` (CSV string)
