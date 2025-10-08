# Glossary

Common acronyms and project-specific terms used throughout the Visio Diagram Generator documentation.

- **CFG** - Control Flow Graph; visual/graph representation of possible execution paths within a VBA procedure.
- **CLI** - Command-line interface; refers to the `VDG.CLI` front-end used to generate diagrams.
- **IR** - Intermediate Representation; the structured JSON model that captures VBA project semantics for downstream tooling.
- **PR** - Pull Request; a change proposal submitted for review in the project's source control workflow.
- **VDG** - Visio Diagram Generator; the umbrella project and tooling suite maintained in this repository.
- **vba2json** - Repository script that walks exported VBA sources and emits the IR JSON payload.
- **vba-ir2diagram** - Repository script that maps IR JSON into diagram JSON consumable by `VDG.CLI`.
