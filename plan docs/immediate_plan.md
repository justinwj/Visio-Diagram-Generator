# Immediate Plan (delete below lists when done, this MD should not get long)

1. Formalize capacity constants in F# (`layout.view.maxModulesPerLane`, `layout.view.maxConnectorsPerRow`, etc.) and enforce them during segment splitting.
2. Design a corridor descriptor type (`EdgeChannel`?) and thread it through `EdgeRoute` so the runner can distinguish planner-guided paths from fallbacks.
3. Update spillover logic to record row-level offsets, ensuring future runner spacing uses planner data instead of heuristics.
4. Begin writing unit tests for `computeViewLayout` focusing on module spillover and channel emission.
