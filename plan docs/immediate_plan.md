# Immediate Plan (delete below lists when done, this MD should not get long)

1. Compute deterministic attachment slots per module side (based on visible node count + bundles) so corridors leave evenly spaced anchors instead of stacking at the center.
2. Rework corridor routing to snap bundle polylines to those slots (both planner + runner) and suppress legacy callout leaders; add tests that check unique slots per bundle.
3. Layer in diagnostics for corridor overlap (distance between adjacent channels/labels) so renders flag hairballs before Visio output.
