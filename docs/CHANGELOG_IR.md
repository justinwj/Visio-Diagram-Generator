# VBA IR Changelog

All notable changes to the VBA IR (Intermediate Representation) will be documented in this file.

## Unreleased
- Added optional `module.metrics` object (lines, cyclomatic) so generators can opt-in to module-level metrics (e.g., `vba2json --infer-metrics`).

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
