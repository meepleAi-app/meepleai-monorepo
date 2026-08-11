# Storybook canonical-state coverage (DEC-A5 / #2342)

Generated: 2026-07-15T07:55:27.529Z
Source: `admin-mockups/MOCKUPS_INDEX.md` · Canonical states: default, empty, loading, error, sse

| Metric | Count |
| --- | --- |
| Total page-mock entries | 68 |
| Covered | 50 |
| Coverage gaps (baseline 3) | 3 |
| Contract violations (always blocking) | 0 |
| Skipped (obsolete) | 1 |
| Skipped (deferred) | 14 |

## Coverage gaps (whitelist-incremental, ratchet down)

| Mockup | Routes | Reason |
| --- | --- | --- |
| `sp7-notifications-preferences.html` | /notifications/preferences | no-story-path |
| `sp4-session-skeleton-live.html` | /sessions/[id]/live | no-story-path |
| `chat-fullscreen.html` | /chat/[threadId], /chat/new | no-story-path |

## Gate semantics

- **contract-violation**: story omits a state its fidelity declares → **always fails** (fix story or align `states_covered`).
- **coverage-gap**: mockup with no fidelity/story → tolerated under `--max-baseline N`; a NEW gap fails. Migrate a page → lower `N` (ratchet-down).
- **skipped-obsolete / skipped-deferred**: fidelity `design_intent` is `forward-refactor-obsolete` (retired) or `deferred` (built later by a tracked umbrella) → excluded from the gap count; requires a tracking issue.
