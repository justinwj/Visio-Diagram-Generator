# PR Checklist Enforcement Guide

The **PR Checklist Enforcement** workflow validates every pull request so reviewers can trust that Information Representation (IR) changes follow governance requirements. It runs automatically on `pull_request` events (open, edit, reopen, and synchronize).

## How to Pass the Check
1. **Start from the default PR template.** Keep the `## Summary`, `## Testing`, `## IR Impact`, and `## IR Checklist Exception Rationale` headings unchanged so the automation can parse your PR body.
2. **Update the IR Impact checklist.** Tick every statement that matches your change. Leaving an item unchecked signals that the work is still in progress for that requirement.
3. **Explain any exceptions.** When at least one IR Impact checkbox is unchecked, replace the placeholder text in *IR Checklist Exception Rationale* with a short justification (20+ characters). Mention owners, follow-up items, and link to any approvals or tracking issues.
4. **Save the PR description.** The workflow re-runs after each update. Once all required items are satisfied, the “PR Checklist Enforcement / Validate IR checklist” check will succeed.

## What Counts as a Good Rationale?
- Names the remaining work and why it cannot ship now (e.g., pending fixture regeneration next sprint).
- Links to an issue, approval, or follow-up PR.
- Assigns an owner and timeframe when possible.

Avoid placeholders such as `None`, `N/A`, or `TBD`; the workflow rejects them.

## Common Failure Messages
- `PR body is empty. Please use the pull request template so the IR checklist can be evaluated.`  
  Use *Preview* → *Submit* in GitHub to populate the template, then fill it in.
- `Unable to locate the "IR Impact" section in the PR description. Please keep the checklist intact.`  
  Restore the `## IR Impact` heading and checklist from the template.
- `No IR Impact checkboxes were found. Please complete the checklist before requesting review.`  
  Ensure each checklist item starts with `- [ ]` or `- [x]` and reflects reality.
- `One or more IR Impact items are unchecked, but the "IR Checklist Exception Rationale" section is missing.`  
  Re-add the `## IR Checklist Exception Rationale` heading and fill out the section.
- `Please provide a meaningful exception rationale describing why unchecked items are acceptable.`  
  Replace placeholder text with real context describing the exception.
- `Exception rationale is too short. Include specific context (at least 20 characters).`  
  Provide multiple sentences or a link with commentary so the reviewer understands the plan.

## Additional Resources
- `docs/IR_Governance.md` &rarr; Full checklist, automation policy, and acceptable exception guidance.
- `docs/Glossary.md` &rarr; Definitions for IR terminology referenced in the checklist.
