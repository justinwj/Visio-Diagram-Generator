# Taxonomy Seed Schema (Draft)

## Purpose
Allow reviewers or system owners to override heuristic semantic classifications (subsystems, roles, ownership, tags) without changing source code. Seed files are optional; when present they layer determinstic data on top of `SemanticArtifactsBuilder` output while maintaining auditability.

## Schema Overview

```jsonc
{
  "modules": {
    "ModuleIdOrName": {
      "primarySubsystem": "UI.Forms",
      "primaryRole": "Coordinator",
      "owner": "Inventory Team",
      "tags": ["ui", "forms", "critical"],
      "confidence": 0.9,
      "notes": "Confirm with SME",
      "metadata": {
        "laneColor": "blue",
        "tierHint": "Forms"
      }
    }
  },
  "procedures": {
    "Module.ProcedureId": {
      "primaryRole": "Validator",
      "owner": "QA",
      "tags": ["validation"],
      "confidence": 0.85,
      "notes": "Overrides auto-detected Utility",
      "metadata": {
        "laneOverride": "Validators"
      }
    }
  },
  "defaults": {
    "subsystems": {
      "UI.Forms": {
        "owner": "Forms Guild",
        "description": "User-facing forms and dialogs"
      }
    }
  }
}
```

### Field Rules
- **modules / procedures**: object maps keyed by canonical module/procedure IDs (case-insensitive). Consumers should normalize ID casing.
- **primarySubsystem / primaryRole**: strings matching existing taxonomy enums; if not recognized, still stored but flagged for review.
- **owner**: arbitrary string (team name, service owner).
- **tags**: array of short strings; merged with heuristic tags.
- **confidence**: optional `0.0–1.0` value recorded to metadata when supplied (otherwise heuristics emit their own confidence).
- **notes**: freeform string included in audit metadata.
- **metadata**: open-ended object reserved for future planner hints, lane coloring, etc.
- **defaults**: optional helper to declare subsystem metadata (owner/description) when heuristics need to surface additional context. These values populate the taxonomy legend if not already known.

## Merge Strategy
1. If a module/procedure appears in the seed file, its fields override the heuristic output.
2. Missing fields fall back to heuristics (e.g., only `owner` is supplied, so `primarySubsystem` still comes from heuristics).
3. CLI `--seed-mode strict` will warn/error if a seed entry does not match any module/procedure in the IR.
4. Seed-applied entries set `semantics.module.seeded=true` (and similar for procedures) to preserve audit trails.

## CLI Plumbing (for implementation)
- Flag: `--taxonomy-seed <path>` (env `VDG_TAXONOMY_SEED`).
- Optional `--seed-mode <merge|strict>` (env `VDG_TAXONOMY_SEED_MODE`) with default `merge`.
- Provide clear error messaging when the file cannot be parsed or is missing.

## Testing Targets
- Seed file applied to known module overrides subsystem/role/owner.
- Seed file with partial data (only tags/notes) merges cleanly.
- Strict mode fails when an entry references a nonexistent module/proc.
- Deterministic fixture outputs validated both with and without a seed file.
