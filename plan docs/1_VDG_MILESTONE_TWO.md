**VDG MILESTONE TWO PLAN**
Goal
- [x] Improve readability for dense service architecture diagrams by adding spacing heuristics, vertical balance within lanes, and automatic pagination when content exceeds page bounds. Orientation remains horizontal.
Scope
- [x] Spacing: enforce minimum inter-node gaps; expose spacing knobs.
- [x] Vertical balance: distribute nodes within each lane to avoid tall stacks.
- [x] Pagination: split lanes into page-sized bands; create multiple pages as needed.
- [x] Cross-page edges: add simple continuation markers and log counts.
Schema Additions
- [x] `layout.spacing`: `{ "horizontal": number, "vertical": number }` (inches; defaults remain 1.2/0.6 if unset).
- [x] `layout.page`: `{ "widthIn": number, "heightIn": number, "marginIn": number, "paginate": boolean }` with safe defaults (e.g., 11 x 8.5, margin 0.75, paginate true).
- [x] Backward compatible; omitted fields use built-ins.
CLI Flags
- [x] `--spacing-h <in>` and `--spacing-v <in>` to override spacing per run.
- [x] `--page-width <in>`, `--page-height <in>`, `--page-margin <in>`, `--paginate true|false`.
- [x] Precedence: CLI > JSON > defaults (consistent with diagnostics).
Layout Engine Changes
- [x] Column widths: compute from node sizes per lane; apply `spacing.horizontal` between columns.
- [x] Vertical balancing inside a lane: sort by `groupId` then `label`; pack nodes with `spacing.vertical`, aiming to minimize tallest band.
- [x] Pagination MVP:
  - [x] Usable height `H = page.heightIn - 2*marginIn`.
  - [x] When a lane's packed height exceeds `H`, split into sequential bands that each fit within `H`.
  - [x] Assign `pageIndex` and `bandIndex`; offset Y by `pageIndex * H` for layout; X positions remain constant across pages for continuity.
- [x] Edge routing:
  - [x] Intra-page edges: current straight/orthogonal lines.
  - [x] Cross-page edges: draw to page-edge continuation markers labeled "→ Node (pN)" and "← from Node (pM)"; use dashed style.
2. Overflow Handling
- [x] In M2, overflows paginate. If a single node (or group, when implemented) still exceeds available page height after balancing:
  - [x] Emit a diagnostic describing the offending element and available height.
  - [x] Try shrinking lane container padding; if still impossible, issue a hard error indicating node must be resized or page size increased.
- [x] This sets up future “node rescaling” or alternate layout strategies.
3. Diagram Title/Header Placement
- [x] Add a title box on the first page using `metadata.title` (and optionally `metadata.version`/`author`).
- [x] Simple implementation: a top banner container with bold title; low effort, high presentation value.
4. Schema Versioning and Migration
- [x] Keep schema at 1.2 for M2; document new optional fields in README and schema.
- [x] Add a short "migration" note: older 1.0/1.1 files still run; to use pagination/spacing, add the optional `layout.spacing`/`layout.page` blocks.
- [x] Stamp schema version and generator version in document properties.
5. Node/Class Styling (Advanced Stub)
- [x] Reserve a future config hook for style maps, e.g.:
  - [x] `styleMap`: entries mapping `type` -> `{ shape: string, color: string, masterId?: string }`.
- [ ] No rendering changes in M2; document intent for M3+ to map `type` to stencil masters.
6. Export/Integration Future
- [x] Clarify in docs that the layout model is portable and could drive other outputs (SVG, PPTX, PDF) in later milestones; M2 focuses on Visio COM.
7. User/Operator Experience
- [x] Document the basic workflow:
  1) Prepare or update JSON.
  2) Adjust CLI spacing/page flags as needed.
  3) Run and inspect diagnostics for overflow/crowding messages.
  4) (Future) Use a UI or CI pipeline for repeatable generation.
Deliverables
- [x] Schema: `shared/Config/diagramConfig.schema.json` (add `layout.spacing`, `layout.page`).
- [x] CLI: `VDG.CLI` to parse new JSON, accept CLI overrides, and create multiple pages + continuation markers.
- [x] Algorithms: enhanced packing/balancing + pagination in `VisioDiagramGenerator.Algorithms`.
- [x] Docs: README updates describing spacing/page options and pagination behavior; this plan updated.
- [x] Samples: a dense architecture JSON that demonstrates multi-page output.
Acceptance Criteria
- [ ] With default settings, dense inputs paginate cleanly across pages without overlap.
- [x] Spacing overrides via JSON/CLI are respected and visible in output.
- [x] Title banner appears on page 1 when `metadata.title` is present.
- [x] Diagnostics clearly report: effective page size, number of pages, lanes that triggered pagination, and any elements that cannot fit.

---

## Recommended Starting Sequence

### 1. Schema & CLI Updates
- [x] Add new fields to schema: `layout.spacing`, `layout.page`, keep version at 1.2.
- [x] Implement CLI flags and precedence (CLI > JSON > defaults).
- [x] Update README/docs with examples and precedence notes.
- [x] Test with synthetic configs and CLI overrides.

### 2. Title Banner (Quick Win)
- [x] Add title/header placement on the first page using `metadata.title` (and optionally `version`/`author`).

### 3. Layout Engine: Spacing + Vertical Balancing
- [x] Use new spacing parameters during packing.
- [x] Balance vertically within lanes (sort by `groupId` then `label`).
- [x] Emit diagnostics for excessive height/crowding.

### 4. Pagination MVP
- [x] Implement banding when packed height exceeds usable page height.
- [x] Assign page/band indices and offset Y per page.
- [x] Diagnostics for pages, bands, and triggers.

### 5. Edge/Cross-Page Continuation Markers
- [x] Draw continuation markers for cross-page edges with dashed style and labels.
- [x] Log cross-page edge counts.

### 6. Overflow Handling
- [x] Hard diagnostics when a single node/group cannot fit a page after balancing; attempt container shrink before error.

### 7. User Experience/Docs
- [x] Update workflow to reflect multi-page output, flags, banner, and diagnostics.
- [x] Add a dense multi-page sample.

### 8. Advanced Stubs/Future-Proofing
- [x] Reserve `styleMap` field in schema/README (no rendering yet).
- [x] Note portability of the layout model for future SVG/PPTX/PDF exports.

### Summary Table

| Step | Why Start Here? |
|------|------------------|
| Schema/CLI updates | Foundation for all new logic; enables features via config. |
| Title banner | Easy, visual, immediate business value. |
| Spacing & balancing | Biggest readability gain for most inputs. |
| Pagination/banding | Scales for large diagrams; builds on spacing. |
| Edge markers | Improves navigation across pages. |
| Diagnostics/overflow | Ensures outputs remain business-grade. |
| Docs/samples | Helps users and accelerates validation. |
