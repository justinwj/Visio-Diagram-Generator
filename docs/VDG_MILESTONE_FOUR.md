**VDG MILESTONE FOUR PLAN**

Goal
- [ ] Support containers (zones/tiers) to give visual structure even with interim colors and to improve scanability of dense diagrams.

Scope
- [ ] Containers for lanes/tiers: visually group nodes in each tier with rounded rectangles, titles, and optional icons.
- [ ] Nested containers: allow sub‑zones within a tier (e.g., bounded service groups).
- [ ] Container styles: light fills, dashed outlines by default; customizable via metadata.
- [ ] Container-aware layout: respect container padding, avoid overlaps, and route connectors around container bounds when configured.
- [ ] Export semantics: persist container names in document properties for downstream automation.

Schema Additions (backward compatible)
- [ ] `layout.containers`: `{ paddingIn: number, cornerIn: number, style?: { fill?: string, stroke?: string, linePattern?: string } }` (defaults: padding 0.3, corner 0.12).
- [ ] Node metadata: `containerId` (to assign nodes to an explicit container; default is implicit tier container).
- [ ] Container list (optional): `containers`: array of `{ id, label, tier, bounds?: { x,y,width,height }, style? }` for explicit sub‑zones.

CLI Flags
- [ ] `--container-padding <in>`
- [ ] `--container-corner <in>`
- [ ] `--route-around-containers <true|false>` (alias of existing routing flag)

Layout/Algorithm Changes
- [ ] Compute tier container bounds from node placements plus padding and corner radius.
- [ ] For explicit sub‑containers, compute bounds from member nodes; validate containment within the parent tier.
- [ ] Update routing to prefer corridor paths that skirt container edges when `routeAroundContainers` is true (offset midlines from container bounds by `container-padding/2`).
- [ ] Reserve label clearance at top‑left of each container for container titles.

Visio Runner Changes
- [ ] Draw tier containers before nodes; send containers to back layer.
- [ ] Apply style cells from `layout.containers.style` or per‑container style.
- [ ] Render container titles (bold) and optional tag line (e.g., environment/zone name).

Diagnostics
- [ ] Report container count, container padding/corner radius, and any nodes not assigned to a valid container.
- [ ] Warn when sub‑container overflows parent tier or overlaps peers.

Deliverables
- [ ] Schema + README updates for containers.
- [ ] Tier and sub‑container drawing in CLI runner.
- [ ] Routing aware of containers when `routeAroundContainers` is set.
- [ ] Tests: container drawing smoke test (skip COM via VDG_SKIP_RUNNER), diagnostics for bad container configs.
- [ ] Samples: explicit sub‑containers inside a tier.

Acceptance Criteria
- [ ] Containers render for each tier with correct bounds and titles.
- [ ] Sub‑containers render inside parent tier without overlap.
- [ ] Connectors route around containers when enabled.
- [ ] Backward compatible with M1–M3 diagrams (containers optional).

Recommended Sequence
- [ ] Schema + CLI for `layout.containers` and explicit `containers[]`.
- [ ] Compute/draw tier containers and titles.
- [ ] Add sub‑containers and containment checks.
- [ ] Container‑aware routing offsets (skirt edges).
- [ ] Samples + diagnostics + tests.
