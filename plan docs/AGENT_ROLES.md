# Agent Roles & Handoff Rules

- **ChatGPT 5 Pro** — Heavy lifts: multi-file codegen/refactors, large diffs, shipping zips. This one is used where every detail matters and large context window may be needed for all those details
- **GPT-5 Thinking** — high level planning, design clarifications & explorations, compile-first stubs, tests, troubleshooting.
- (Optional) Others — Quick Q&A, focused edits.

**Handoff contract**
1. Before handing off, update `/docs/VDG/PROJECT_STATE.md` and `/docs/VDG/PROMPTS_LOG.md` and `Update_Team_ChatGPT.md`.
2. Ship *only* the files for the current prompt (diff-only), keep TFMs/invariants intact.
3. Record any deviations in `/docs/VDG/DECISIONS.md` (short bullets).

