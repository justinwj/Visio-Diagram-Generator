# Immediate Plan (delete below lists when done, this MD should not get long)

1. Fix `computeViewLayout` row/page segmentation so tiers split when `maxModulesPerLane`/`maxNodesPerLane` caps are hit; cover with unit tests on wide fixtures (include `invSys` slice).
2. Tune default layout caps (lane + page occupancy, tier spacing) or introduce an `invSys` profile, then rerun the guardrail render to confirm occupancy drops below 100 %.
3. Inspect bridge/callout generation on the refreshed layout; trim or redesign any skyscraper artifacts once new pagination is in place.
4. Refresh corridor/channel diagnostics after the layout changes so runner vs. planner drift stays transparent.
