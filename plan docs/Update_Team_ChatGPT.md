# Update_Team_ChatGPT — Session Sync

**Agents**: Pro GPT-5 (lead), Fast GPT-5 (execution speed), Thinking GPT-5 (deep reasoning)  
**Doc Purpose**: Single source of truth updated each working session so any agent can resume context quickly.

---
## 0) Session Header
- **Date/Time (UTC)**: {{auto}}
- **Operator**: <your name>
- **Repo / Branch**: <org/repo> @ <branch>
- **Commit(s)**: <short SHAs>
- **Environment sanity**: .NET SDK 9.x ✅ | VS 2022 17.12+ ✅ | Visio 64-bit ✅

## 1) TL;DR (≤5 bullets)
- <what changed this session>
- <what’s pending>
- <risks or blockers>
- <next milestone>
- <who owns what>

## 2) Decision Log (append-only)
- [YYYY-MM-DD] Switched to stand-alone CLI on net9.0-windows; archived VSTO add-in.
- [YYYY-MM-DD] Output path + filename regex agreed.
- [YYYY-MM-DD] Viewer is Visio for the web; no legacy Viewer.

## 3) Current Status (traffic light)
- **Environment setup**: 🟢/🟡/🔴 – notes:
- **Scaffolding (projects/TFMs)**: 🟢/🟡/🔴 – notes:
- **FakeVisioService tests**: 🟢/🟡/🔴 – notes:
- **Visio COM adapter (minimal)**: 🟢/🟡/🔴 – notes:
- **Web viewing (optional)**: 🟢/🟡/🔴 – notes:

## 4) Next Actions by Role
- **Pro GPT-5** (you are here): Plan/coordinate, deliver specs, review PRs, keep this doc current.
- **Fast GPT-5**: Implement scaffolding or small diffs rapidly; follow Decisions & Constraints.
- **Thinking GPT-5**: Explore tricky interop/layout topics; propose robust patterns and tests.
- **Human**: Run tests locally, validate smoke outputs, push to GitHub.

## 5) Constraints & Interfaces
- **TFMs**: Core=netstandard2.0; CLI/Runtime=net9.0-windows (x64, STA)
- **Office**: Visio 2024 (or newer), 64-bit
- **Interop**: COM (Microsoft Visio 16.0 Type Library), Embed Interop Types=true
- **Outputs**: C:\Users\justu\OneDrive - Black Scottie Chai\Solution Notes\VDG-<UTC>-<GuidN>.vsdx
- **Viewer**: Visio for the web (SharePoint/OneDrive); no legacy Viewer
- **Security**: No secrets in repo; Graph (if used) via Device Code & user secrets

## 6) Test & Demo Plan
- Unit: filename pattern, command sequence, simple layout asserts (FakeVisioService)
- Integration-light: end-to-end with FakeVisioService (no Office)
- Manual smoke: run CLI on dev box, generate .vsdx, open in Visio desktop and/or upload to SPO/OneDrive

## 7) Artifacts touched this session
- Files: <list files or projects>
- PR/Commit: <link or SHA>
- Output: <paths created>

## 8) Open Questions
- <q1>
- <q2>

## 9) Mini-CHANGELOG (since last session)
- <added/changed/fixed> ...
