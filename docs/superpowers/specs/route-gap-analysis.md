# Route Gap Analysis

**Date:** 2026-05-21
**Source:** `docs/superpowers/specs/nav-map.md` (Phase 1 deliverable)
**Tool:** `scripts/v2_audit/gap_analysis.py`
**Branch:** `feature/gap-analysis-routes`

## Summary

| Category | Count |
|---|---|
| Total unique mockup destinations | 45 |
| Mapped routes that exist | 26 |
| Mapped routes that are MISSING | 5 |
| Unmappable / planned destinations | 14 |

> **Note on heuristic corrections:** The `_mockup_to_route` heuristic in `nav_dimension.py` produces
> singular forms for 4 destinations (`/game/[id]`, `/agent/[id]`, `/toolkit/[id]`, `/kb/[id]`) where
> the actual app uses plural paths (`/games/[id]`, `/agents/[id]`, `/toolkits/[id]`, `/knowledge-base/[id]`).
> These are corrected by the `_SINGULAR_TO_PLURAL` map in `gap_analysis.py` and counted as **existing**.
> The original nav_dimension.py heuristic is unchanged so as not to break the audit runner.

## Missing Routes (5)

Routes referenced by mockups but not implemented in `apps/web/src/app/`. Scaffolds created in this PR.

| Mockup Destination | Mapped Route | Ref Count | Priority | Scaffold Created | Recommended Implementation |
|---|---|---|---|---|---|
| `sp4-citation-pdf-viewer` | `/knowledge-base/[id]/pdf` | 10 | high | yes | Inline PDF viewer (react-pdf or iframe) embedded in KB detail; requires document binary endpoint |
| `sp3-faq-enhanced` | `/faq-enhanced` | 6 | high | yes | Redirect-only; `/faq` already implements `sp3-faq-enhanced.jsx` fully — alias or remove |
| `sp3-legal` | `/legal` | 5 | high | yes | Legal hub page linking to `/terms` and `/privacy`; the two sub-pages already exist |
| `sp4-game-chat-tab` | `/games/[id]/chat` | 4 | high | yes | AI chat sub-tab inside game detail; mockup `sp4-game-chat-tab` shows full chat thread embedded in `/games/[id]` tabs |
| `sp3-shared-game-detail` | `/shared-game-detail` | 3 | high | yes | Redirect-only; `/shared-games/[id]` already implements the detail view — old slug-less mockup route |

Priority heuristic applied:
- **high**: referenced 3+ times OR top-level navigation item
- **medium**: referenced 1-2 times, mid-tier
- **low**: referenced once, leaf functionality

### Scaffolds

Each scaffold is a `page.tsx` placeholder with a 2-second auto-redirect to the closest existing parent:

| Scaffold path | Redirect target |
|---|---|
| `apps/web/src/app/(public)/faq-enhanced/page.tsx` | `/faq` |
| `apps/web/src/app/(public)/legal/page.tsx` | `/terms` |
| `apps/web/src/app/(public)/shared-game-detail/page.tsx` | `/shared-games` |
| `apps/web/src/app/(authenticated)/games/[id]/chat/page.tsx` | `/games/[id]` |
| `apps/web/src/app/(authenticated)/knowledge-base/[id]/pdf/page.tsx` | `/knowledge-base/[id]` |

## Unmappable destinations (14)

Mockup destinations the route-mapping heuristic returns `None` for. These are either internal mockup
hub pages, planned-but-not-yet-designed routes, or wizard flows without a stable Next.js counterpart.
No scaffold created; listed for visibility only.

| Destination | Ref Count | Reason |
|---|---|---|
| `sp7-game-night-detail-rsvp` | 16 | `_PLANNED_DESTS` — intentionally planned-not-implemented |
| `sp4-kb-hub` | 11 | No heuristic rule; KB hub concept not yet defined in route tree |
| `00-hub` | 7 | Mockup-demo internal hub page — not a real app route |
| `sp4-add-game-bgg-step` | 7 | BGG search step — wizard state within `/library/private/add`; no stable URL |
| `01-screens` | 5 | Mockup-demo internal screens index — not a real app route |
| `sp6-libro-game-glossary-editor` | 4 | In-play glossary editor; no defined stable route yet |
| `03-drawer-variants` | 3 | Mockup-demo internal page — not a real app route |
| `sp4-game-nights-index` | 3 | No heuristic rule; `/game-nights` (plural) exists but heuristic does not map this name |
| `sp4-upload-wizard-extended` | 2 | Extended upload wizard — wizard step, not a standalone route |
| `04-design-system` | 1 | Mockup-demo design system page — not a real app route |
| `05-dark-mode` | 1 | Mockup-demo dark mode page — not a real app route |
| `public` | 1 | Generic public hub mockup — not a real app route |
| `sp4-session-live` | 1 | Maps ambiguously to `/sessions/live` (already exists) — heuristic gap |
| `sp4-session-summary` | 1 | Session summary — maps ambiguously; check `/sessions/[id]` tabs |

## Existing routes (26)

Routes referenced by mockups that already exist in `apps/web/src/app/`. Not scaffolded.

| Mockup Destination | Canonical Route | Ref Count |
|---|---|---|
| `librogame-runthrough-game-onboarding` | `/games/[id]` | 75 |
| `librogame-runthrough-play-session` | `/games/[id]` | 51 |
| `librogame-runthrough-game-detail` | `/games/[id]` | 43 |
| `sp4-dashboard` | `/dashboard` | 27 |
| `librogame-runthrough-resume-picker` | `/games/[id]` | 15 |
| `sp4-hub-toolkits` | `/hub/toolkits` | 14 |
| `sp3-join` | `/join` | 13 |
| `settings` | `/settings` | 12 |
| `sp3-how-it-works` | `/how-it-works` | 12 |
| `sp4-players-index` | `/players` | 12 |
| `sp4-library-desktop` | `/library` | 10 |
| `sp4-sessions-index` | `/sessions` | 9 |
| `librogame-runthrough-translate-viewer` | `/games/[id]` | 8 |
| `sp4-agents-index` | `/agents` | 8 |
| `sp4-game-detail` | `/games/[id]` (via `/game/[id]` correction) | 66 |
| `sp4-agent-detail` | `/agents/[id]` (via `/agent/[id]` correction) | 19 |
| `sp4-toolkit-detail` | `/toolkits/[id]` (via `/toolkit/[id]` correction) | 14 |
| `sp4-kb-detail` | `/knowledge-base/[id]` (via `/kb/[id]` correction) | 4 |
| `librogame-runthrough-quota-credits` | `/games/[id]` | 6 |
| `sp4-games-index` | `/games` | 6 |
| `onboarding` | `/onboarding` | 5 |
| `librogame-runthrough-setup-wizard` | `/games/[id]` | 4 |
| `sp3-shared-games` | `/shared-games` | 4 |
| `notifications` | `/notifications` | 3 |
| `sp4-discover` | `/discover` | 1 |
| `sp4-player-detail` | `/players/[id]` | 1 |

## Follow-up

Each scaffolded route has a `// TODO` comment and a priority rating. The two genuine implementation
gaps (with non-trivial work) are:

1. **`/knowledge-base/[id]/pdf`** (priority: high, 10 refs) — requires PDF viewer component and
   binary document endpoint from the API.
2. **`/games/[id]/chat`** (priority: high, 4 refs) — requires AI chat integration within the
   game detail tabs UI.

The three alias/redirect scaffolds (`/faq-enhanced`, `/legal`, `/shared-game-detail`) may be
converted to permanent redirects via `next.config.js` `redirects()` once confirmed with the team.
