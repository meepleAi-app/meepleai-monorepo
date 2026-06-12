# Mockup Designer Review Queue — DS-17 Phase B

**Source**: `audits/2026-06-10-mockup-design-intent-audit.md`
**Generated**: 2026-06-10
**Auditor**: AI subagent fan-out (Phase B sub-issue #2127)

## How to approve

Comment on this PR with the magic phrase:

```
DESIGNER APPROVED: 2026-06-10 <your-name>
```

Example: `DESIGNER APPROVED: 2026-06-15 alice-doe`

Magic phrase regex: `^DESIGNER APPROVED: \d{4}-\d{2}-\d{2} [\w\s-]+$`

After approval, tracking issues will be created for `forward-refactor-obsolete` entries via `pnpm audit-mockups:create-issues`.

## Obsolete candidates (require review)

- [ ] `admin-mockups/design_files/sp4-hub-games.html` — HTML title 'Pre-Stage-3 · Hub Games (public) — /hub/games'. Route /hub/games was retired by Asse D follow-up P2 #1899 which refactored /games as multi-tab hub (discover/catalogo/trending/community) with Discover default tab. Mockup represents pre-refactor target the codebase has already surpassed. Not in MOCKUPS_INDEX (non-mappable). Per CLAUDE.md Stage 3 #1026 (closed 2026-05-18) the legacy hub/<entity> directories were emptied post-codemod.
  - Suggested tracking: [DS-17] Delete obsolete sp4-hub-games mockup — /hub/games route retired by Asse D P2 multi-tab refactor
- [ ] `admin-mockups/design_files/sp4-hub-agents.html` — HTML title 'Pre-Stage-3 · Hub Agents (authenticated) — /hub/agents'. Route /hub/agents was retired by Stage 3 #1026 (closed 2026-05-18); current canonical agents route is /agents per MOCKUPS_INDEX (sp4-agents-index → /agents). Mockup represents pre-refactor target. Not in MOCKUPS_INDEX (non-mappable).
  - Suggested tracking: [DS-17] Delete obsolete sp4-hub-agents mockup — /hub/agents route retired by Stage 3 deversioning
- [ ] `admin-mockups/design_files/sp4-dashboard.html` — Pre-existing CLAUDE.md precedent: sp4-dashboard.{html,jsx} superseded by Asse C #1898 priority-driven dashboard refactor (shipped 2026-06-05 sess.34). 5 entity sections legacy → 4 priority sections (ProssimiSection/RecentiSection/SuggestedSection/FriendsActivitySection). Mockup represents pre-Asse-C design that codebase has surpassed. Existing tracking issue: #2114.
  - Suggested tracking: Already tracked in #2114
- [ ] `admin-mockups/design_files/sp4-add-game-bgg-step.html` — Mockup designs AddGameDrawer → tab 'From BGG' (user-side BGG search) per JSX comment. Per CLAUDE.md DP-5 feedback (sess.46h 2026-06-09) and #1903 ADR: user-side BGG access is BLOCKED for ToS compliance. Only admin-only via useSearchBggGames hook is allowed. Mockup specifies BGG rate-limit, HTTP 202 throttled, tier-quota — all USER-FACING BGG features that violate ToS. Pattern: catalog interno via api.games.getAll, NON useSearchBggGames user-side. MOCKUPS_INDEX listing as /library/proposals predates the ToS restriction.
  - Suggested tracking: [DS-17] Delete obsolete sp4-add-game-bgg-step mockup — BGG ToS violation (user-side BGG access forbidden per #1903 ADR)
- [ ] `admin-mockups/design_files/sp4-hub-games.jsx` — JSX twin of sp4-hub-games.html titled 'Pre-Stage-3 · Hub Games (public) — /hub/games'. Route /hub/games retired by Stage 3 #1026 + superseded by /games multi-tab hub (Asse D P2 #1899). Not in MOCKUPS_INDEX (non-mappable).
  - Suggested tracking: [DS-17] Delete obsolete sp4-hub-games mockup — /hub/games route retired by Asse D P2 multi-tab refactor
- [ ] `admin-mockups/design_files/sp4-hub-agents.jsx` — JSX twin of sp4-hub-agents.html titled 'Pre-Stage-3 · Hub Agents (authenticated) — /hub/agents'. Route /hub/agents retired by Stage 3 #1026 (closed 2026-05-18); current canonical is /agents.
  - Suggested tracking: [DS-17] Delete obsolete sp4-hub-agents mockup — /hub/agents route retired by Stage 3 deversioning
- [ ] `admin-mockups/design_files/sp4-dashboard.jsx` — JSX twin of sp4-dashboard.html. Pre-existing CLAUDE.md precedent: superseded by Asse C #1898 priority-driven dashboard refactor (shipped 2026-06-05 sess.34). Existing tracking issue: #2114.
  - Suggested tracking: Already tracked in #2114
- [ ] `admin-mockups/design_files/sp4-add-game-bgg-step.jsx` — JSX twin of sp4-add-game-bgg-step.html. Designs user-side BGG search step in AddGameDrawer (rate-limit banner, HTTP 202 throttled, tier-quota lock). Violates BGG ToS per #1903 ADR and CLAUDE.md DP-5 (user-side BGG access bloccato). Forbidden surface.
  - Suggested tracking: [DS-17] Delete obsolete sp4-add-game-bgg-step mockup — BGG ToS violation (user-side BGG access forbidden per #1903 ADR)
- [ ] `admin-mockups/design_files/sp4-hub-toolkits.html` — Title 'Pre-Stage-3 · Hub Toolkits (authenticated) — /hub/toolkits'. Same Pre-Stage-3 hub-* pattern as sp4-hub-games and sp4-hub-agents, which were retired by Stage 3 #1026 (design system de-versioning, umbrella #1023). Per CLAUDE.md, hub/<entity> 3-routes pattern was refactored. Mockup superseded by current hub layout — do not migrate.
  - Suggested tracking: DS-17 Phase B: classify sp4-hub-toolkits as forward-refactor-obsolete (Pre-Stage-3 retired)
- [ ] `admin-mockups/design_files/sp4-hub-toolkits.jsx` — JSX pair for Pre-Stage-3 Hub Toolkits mockup. Same Pre-Stage-3 hub-* pattern as sp4-hub-games and sp4-hub-agents (classified obsolete in sp4-core audit). Stage 3 #1026 (umbrella #1023, COMPLETE 2026-05-18) retired the legacy hub/<entity> pattern. Do not migrate.
  - Suggested tracking: DS-17 Phase B: classify sp4-hub-toolkits as forward-refactor-obsolete (Pre-Stage-3 retired)

## Pair disagreements (require designer arbitration)

_None._

## Low confidence (< 0.6, optional review)

_None._

## Coverage gaps — SPEC-PANEL addendum (2026-06-10)

Spec-panel critique with 4 experts (Cockburn lead · Adzic · Wiegers · Fowler) analyzed 224 mockups vs 30 user stories vs 130 codebase routes. Full report: [`audits/2026-06-10-mockup-coverage-gap-report.md`](../../../audits/2026-06-10-mockup-coverage-gap-report.md).

**Coverage**: 95 of 130 user-side routes have mockup coverage (73%); 35 routes uncovered.

### 3 follow-up decisions (Drafts 11/12/13 — created post designer sign-off)

- [ ] **Draft 11 (Wiegers + Cockburn — P1)**: Commission 5 game-detail sub-tab mockups for US-9 happy path:
  - `sp4-game-detail-tab-rules.{html,jsx}`
  - `sp4-game-detail-tab-reviews.{html,jsx}` (M1 friend-first commentary)
  - `sp4-game-detail-tab-strategies.{html,jsx}`
  - `sp4-game-detail-tab-chat.{html,jsx}` (standalone, not composite)
  - `sp4-game-detail-tab-faqs.{html,jsx}` (game-detail variant of sp3-faq-enhanced)
- [ ] **Draft 12 (Adzic — P1)**: Decide `/games?tab={catalogo,trending,community}` — document ComingSoon as intentional OR commission 3 mockups
- [ ] **Draft 13 (Fowler — Architecture)**: Decide `/hub/*` route-vs-mockup contradiction — retire routes (Option A, Stage 3 #1026 intent) OR refresh 5 mockups (Option B)

### Top routes-without-mockup gaps (full list in gap report)

- 4 of 6 `/games/[id]/*` sub-tab routes (rules, reviews, strategies, chat) — PRD f3 #1929 shipped ahead of mockups
- 3 of 4 `/games?tab=*` query variants (catalogo, trending, community)
- 5 of 5 `/editor/agent-proposals/*` routes — entire domain uncovered
- 5 of 5 `/hub/*` routes — contradiction (LIVE but obsolete)
- 4 of 4 sessions sub-routes (`/sessions/[id]/{notes,scoreboard,players,join}`)
- `/profile/achievements`, `/knowledge-base/global`, `/knowledge-base/[id]/pdf`

### Overall quality scores

| Score | Value |
|---|---|
| Mockup-to-route coverage | 7.3/10 (95/130) |
| US sequence completeness | 6.5/10 (13/30 US have ≥1 missing mockup) |
| Architecture consistency | 5.0/10 (/hub/* contradiction + 7 many-to-many mappings) |
| Specification testability | 7.0/10 (5 obsoletes correctly flagged) |
| **Overall** | **6.5/10** |

## Structural consistency — SPEC-PANEL addendum #2 (2026-06-11)

Second spec-panel critique on nav-chrome / BGG / naming consistency. Full report: [`audits/2026-06-10-nav-chrome-bgg-naming-audit.md`](../../../audits/2026-06-10-nav-chrome-bgg-naming-audit.md).

## US-2 walkthrough addendum — auth-flow gap (2026-06-12, #2170)

`auth-flow.fidelity.json` was reclassified `current` → `forward-refactor` to track a gap discovered during the US-2 Marco Socratic walkthrough.

- [ ] **#2170 — `auth-flow.html`: add `SessionExpiredScreen`** — The live `/login?reason=session_expired` surface renders a warning banner above `AuthCard` (`t('auth.session.expired')` — yellow alert). The mockup ships 6 phone screens but none of them shows this state, so the design intent for "resume session" (central to US-2) is undocumented. Decision required: commission a 7th screen, or downgrade the live banner to match the mockup.
- [ ] **OAuth provider drift (#2170 bonus)** — Mockup shows 2 OAuth providers (Google + Discord). Live shows 3 (Google + Discord + GitHub). Decision required: add GitHub to the mockup, or remove GitHub from the live UI.

### 3 follow-up decisions (Drafts 14/15/16 — created post designer sign-off)

- [ ] **Draft 14 (Fowler, architecture)**: Nav-chrome 3-way drift — primitives never consumed by page-mocks or runtime. Decide deprecate-or-backfill.
- [ ] **Draft 15 (Newman + Wiegers, ToS compliance)**: Add `pnpm lint:bgg-mockups` gate + 9 new violations (7 mockups + 2 codebase findings)
- [ ] **Draft 16 (Adzic, naming)**: Standardize CRUD verbs (-create vs -new) + dynamic params (10→2-3) + complete #2025 cleanup + suffix vocabulary + nanolith rename

### 7 NEW BGG forbidden mockups (require designer reclassification to `forward-refactor-obsolete`)

These mockups carry user-side BGG surfaces forbidden per #1903 ADR. Phase B initial audit classified them `current`; designer review needed to confirm reclassification:

- [ ] `admin-mockups/design_files/sp4-upload-wizard-extended.{html,jsx}` — Step 0 source picker offers BoardGameGeek card linking to forbidden mockup
- [ ] `admin-mockups/design_files/sp4-library-desktop.jsx` — Hero "↓ Importa BGG" CTA + empty-state CTA links to forbidden mockup
- [ ] `admin-mockups/design_files/sp4-game-chat-tab.html` — Low-confidence chat fallback: BGG forum thread citation
- [ ] `admin-mockups/design_files/sp5-profile-settings.{html,jsx}` — Connected services panel exposes BGG OAuth
- [ ] `admin-mockups/design_files/sp3-how-it-works.jsx` — Onboarding card "Connetti BGG — Sincronizza la tua collezione BoardGameGeek"
- [ ] `admin-mockups/design_files/settings.jsx` — BggIcon + Bio "BGG rank" + Connected services BGG entry
- [ ] `admin-mockups/design_files/sp7-game-night-live.jsx:561` — Add-game CTA navigates to forbidden mockup

### Structural quality scores

| Score | Value |
|---|---|
| Nav-chrome consistency | 3.5/10 |
| BGG ToS compliance | 6.5/10 (7+ violations remain undetected pre-Draft 15) |
| Naming consistency | 5.0/10 |
| Route architecture | 6.0/10 |
| **Overall structural** | **5.5/10** |
