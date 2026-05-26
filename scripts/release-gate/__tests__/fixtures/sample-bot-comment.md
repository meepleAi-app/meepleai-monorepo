<!-- release-gate-bot:v1 -->

## Release-gate Classification

Commit `abc1234` · Generated 2026-05-22T10:00:00Z

**Verdict**: ⚠️ WARNING

0 blocker · 3 warning · 1 informational

| Check | Severity | Owner | Override path | Notes |
|---|---|---|---|---|
| `Frontend - A11y E2E` | ⚠️ warning | qa | baseline-update | ~159 known violations tracked in #1094 (Phase A→D plan). Advisory until Phase D ships. |
| `Lighthouse CI` | ⚠️ warning | frontend-dev | exception-comment | Performance budget. Flake-prone on busy runners; retry before reverting. |
| `codecov/patch` | ⚠️ warning | devops | baseline-update | Patch coverage threshold. Tolerated drops require updated baseline. |
| `Lychee Link Check` | ℹ️ informational | devops | exception-comment | External link rot. Non-blocking unless docs-critical (see #1014). |

<sub>Classification duration: 142 ms · PR #999</sub>
