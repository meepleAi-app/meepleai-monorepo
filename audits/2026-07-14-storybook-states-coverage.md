# Storybook canonical-state coverage (DEC-A5 / #2342)

Generated: 2026-07-14T14:58:39.293Z
Source: `admin-mockups/MOCKUPS_INDEX.md` · Canonical states: default, empty, loading, error, sse

| Metric | Count |
| --- | --- |
| Total page-mock entries | 68 |
| Covered | 24 |
| Coverage gaps (baseline n/a) | 44 |
| Contract violations (always blocking) | 0 |
| Skipped (obsolete) | 0 |

## Coverage gaps (whitelist-incremental, ratchet down)

| Mockup | Routes | Reason |
| --- | --- | --- |
| `sp7-notifications-hub.html` | /notifications | no-story-path |
| `sp7-notifications-preferences.html` | /notifications/preferences | no-story-path |
| `sp3-accept-invite.html` | /accept-invite, /invites/[token] | no-story-path |
| `sp3-faq-enhanced.html` | /faq, /games/[id]/faqs | no-story-path |
| `sp3-how-it-works.html` | /how-it-works | no-story-path |
| `sp3-join.html` | /join, /sessions/join | no-story-path |
| `sp3-legal.html` | /privacy, /terms, /cookies, /cookie-settings | no-story-path |
| `sp3-shared-game-detail.html` | /shared-games/[id] (Wave A.3 | no-story-path |
| `sp3-shared-games.html` | /shared-games | no-story-path |
| `sp4-add-game-pdf-dedup.html` | /library/private/add, /upload | no-story-path |
| `sp4-agent-detail.html` | /agents/[id], /library/[gameId]/agent | no-story-path |
| `sp4-agents-index.html` | /agents, /editor/agent-proposals/*, /chat/agents/create | no-story-path |
| `sp4-discover.html` | /discover | no-story-path |
| `sp4-game-detail.html` | /games/[id], /library/[gameId], /private-games/[id] | no-story-path |
| `sp4-game-nights-index.html` | /game-nights | no-story-path |
| `sp4-games-index.html` | /games | no-story-path |
| `sp4-kb-hub.html` | /knowledge-base | no-story-path |
| `sp4-library-desktop.html` | /library | no-story-path |
| `sp4-play-records-detail.html` | /play-records/[id] | no-story-path |
| `sp4-play-records-edit.html` | /play-records/[id]/edit | no-story-path |
| `sp4-play-records-index.html` | /play-records | no-story-path |
| `sp4-play-records-new.html` | /play-records/new | no-story-path |
| `sp4-play-records-stats.html` | /play-records/stats | no-story-path |
| `sp4-player-detail.html` | /players/[id], /players/[id]/{achievements | no-story-path |
| `sp4-players-index.html` | /players | no-story-path |
| `sp4-session-catan-live.html` | /sessions/[id]/live Catan demo (medium euro + trading | no-story-path |
| `sp4-session-catan-summary.html` | /sessions/[id] Catan post-game . Premium #3/7. | no-story-path |
| `sp4-session-codenames-live.html` | /sessions/[id]/live Codenames demo (team deduction party game | no-story-path |
| `sp4-session-codenames-summary.html` | /sessions/[id] Codenames post-game . Premium #7/7. | no-story-path |
| `sp4-session-paleo-live.html` | /sessions/[id]/live Paleo demo (co-op preistorico Simultaneous | no-story-path |
| `sp4-session-paleo-summary.html` | /sessions/[id] Paleo post-game . Premium #6/7. | no-story-path |
| `sp4-session-power-grid-live.html` | /sessions/[id]/live Power Grid demo (heavy euro auction + network | no-story-path |
| `sp4-session-power-grid-summary.html` | /sessions/[id] Power Grid post-game . Premium #4/7. | no-story-path |
| `sp4-session-puerto-rico-live.html` | /sessions/[id]/live Puerto Rico demo (heavy euro role-selection | no-story-path |
| `sp4-session-puerto-rico-summary.html` | /sessions/[id] Puerto Rico post-game . Premium #2/7. | no-story-path |
| `sp4-session-skeleton-live.html` | /sessions/[id]/live **generic skeleton** . Consumes AiToolkitSuggestionDto polymorphically. Demo shows side-by-side Wingspan  vs Paleo . Closes #1750 . | no-story-path |
| `sp4-session-summary-skeleton.html` | /sessions/[id] **generic post-game skeleton** . Demo shows side-by-side Wingspan vs Paleo. Closes #1750 . | no-story-path |
| `sp4-session-wingspan-live.html` | /sessions/[id]/live Wingspan demo + consolidated tabs ?tab=scores\|photos\|agent\|players\|chat\|tools\|notes (was 4 separate sub-routes pre-2026-05-31 | no-story-path |
| `sp4-session-wingspan-summary.html` | /sessions/[id] Wingspan demo + consolidated tabs ?tab=scoreboard\|notes\|players (was 3 separate sub-routes pre-2026-05-31 | no-story-path |
| `sp4-session-zombicide-live.html` | /sessions/[id]/live Zombicide GH demo (co-op miniatures dungeon-crawler | no-story-path |
| `sp4-session-zombicide-summary.html` | /sessions/[id] Zombicide post-game . Premium #5/7. | no-story-path |
| `sp4-sessions-index.html` | /sessions, /games/[id]/sessions | no-story-path |
| `chat-fullscreen.html` | /chat/[threadId], /chat/new | no-story-path |
| `librogame-game-night-storyboard.html` | /game-nights/[id] (storyboard variant — was nanolith-game-night-storyboard.html pre-rename post-IA consolidation #871 | no-story-path |

## Gate semantics

- **contract-violation**: story omits a state its fidelity declares → **always fails** (fix story or align `states_covered`).
- **coverage-gap**: mockup with no fidelity/story → tolerated under `--max-baseline N`; a NEW gap fails. Migrate a page → lower `N` (ratchet-down).
