# Callgraph Styling Defaults (Milestone 9)

This document captures the agreed styling pass for the Visio callgraph renderer so that CLI defaults, documentation, and samples stay in sync.

## Lane Palette

| Tier     | Fill Hex | Lane Accent Notes                     |
|----------|----------|----------------------------------------|
| Forms    | `#E3F2FD` | Light blue, outline `#3B7DDD`, header text white |
| Sheets   | `#F3E5F5` | Lavender, outline `#6A1B9A`, header text `#2E0A3A` |
| Classes  | `#FFF3E0` | Warm amber, outline `#B26A00`, header text `#2D1B00` |
| Modules  | `#E8F5E9` | Soft green, outline `#2E7D32`, header text `#0B3D18` |

- Lane headers: `Segoe UI Semibold` 12pt over a 0.3in accent strip matching the outline colour.
- Node body text: `Segoe UI` 9pt black; secondary metadata (tags, counts) `Segoe UI` 8pt `#4A4A4A`.
- Lane separators use 1pt neutral `#D0D7E2` strokes to keep tiers distinct without heavy borders.
- Default lane order: Forms -> Sheets -> Classes -> Modules.

## Containers

- Default line weight `1.5 pt`, stroke `#1F4E79`, fill `#FFFFFF` at 95% opacity with 8pt corner radius.
- Container titles (`metadata.title`) render in `Segoe UI Semibold` 11pt with stroke-matching text colour.
- Nested containers inherit the parent lane palette but lighten fill by 10% to maintain contrast.

## Connectors and Diagnostics Accents

- Call edges: stroke `#4A4A4A`, end arrow `LineArrow` 5, weight `0.75 pt`.
- Cross-lane bundles adopt the target lane accent for readability.
- Diagnostics overlays (crowding banners) use:
  - Warning: fill `#FFF4CE`, stroke `#DFA000`.
  - Error: fill `#FDE7E9`, stroke `#B32025`.

## Legend and Sample Assets

- Visual legend placeholder: `docs/render_legend.png` (generated programmatically for docs).
- Styled sample diagram: `samples/vba_callgraph_styled.vsdx` (mirrors the Milestone 9 defaults).

Keep the legend and sample refreshed whenever palette tweaks land so downstream teams see the latest tier/branding guidance.
