# VBA IR Changelog

All notable changes to the VBA IR (Intermediate Representation) will be documented in this file.

## Unreleased
- _No changes yet._

## 0.2 — Hyperlink anchors & enriched metrics
- Bumped `irSchemaVersion` to `0.2`.
- Added `source { file, module?, line? }` anchors on modules and procedures to drive hyperlink targets.
- Expanded `metrics` to include `sloc` alongside existing `lines`/`cyclomatic`, and capture module-level aggregates when available.
- Clarified schema appendix to reflect the new anchor/metric fields.

## 0.1 — Initial draft
- Top-level envelope with `irSchemaVersion` and optional `generator` metadata.
- Project surface: `project { name, modules[] }`.
- Module: `id`, `name`, `kind (Module|Class|Form)`, `file?`, `attributes?[]`, `procedures[]`.
- Procedure: `id`, `name`, `kind (Sub|Function|PropertyGet|PropertyLet|PropertySet)`,
  `access? (Public|Private|Friend)`, `static?`, `params?[]`, `returns?`,
  `locs? { file, startLine, endLine }`, `calls?[]`, `metrics? { cyclomatic?, lines? }`, `tags?[]`.
- Call: `target (Module.Proc)`, `isDynamic?`, `site? { module, file, line }`.
- Conventions documented: lowerCamel fields, Pascal enums, additive evolution, stable ordering.

Notes
- Minor versions are additive; tools should ignore unknown fields.
- Breaking changes will bump the major version with migration notes.
