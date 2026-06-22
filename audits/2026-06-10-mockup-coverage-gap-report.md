# Mockup-vs-User-Story Coverage Gap Report — DS-17 Phase B addendum

**Date**: 2026-06-10
**Method**: `/sc:spec-panel` critique with 4 experts (Cockburn lead · Adzic · Wiegers · Fowler)
**Input data**: 224 mockup files + 30 user-side US + 130 codebase user-side routes (sub-issue #2127)

This report extends the Phase B audit (`audits/2026-06-10-mockup-design-intent-audit.md`) with a US-perspective gap analysis. Phase B classified WHAT exists; this addendum identifies WHAT IS MISSING.

---

## Quantitative summary

| Metric | Value |
|---|---|
| Total user-side routes (codebase) | 130 |
| Routes covered by ≥1 mockup | 95 (73%) |
| Routes WITHOUT mockup coverage | 35 (27%) |
| Mockups classified obsolete (Phase B) | 5 unique pairs |
| User stories identified | 30 (24 docs-grounded + 6 inferred) |
| US with ≥1 missing mockup in happy path | 13/30 (43%) |

---

## Top 10 missing mockups (prioritized for Phase C input)

| Priority | Missing mockup | Route(s) | US affected | Effort |
|---|---|---|---|---|
| P1 | `sp4-game-detail-tab-rules.html` | `/games/[id]/rules` | US-9 | S (tab variant of sp4-game-detail.html) |
| P1 | `sp4-game-detail-tab-reviews.html` | `/games/[id]/reviews` | US-9 | M (commentary friend-first M1) |
| P1 | `sp4-game-detail-tab-strategies.html` | `/games/[id]/strategies` | US-9 | S |
| P1 | `sp4-games-tab-catalogo.html` | `/games?tab=catalogo` | US-8 | M |
| P1 | `sp4-games-tab-trending.html` | `/games?tab=trending` | US-8 | S |
| P2 | `sp4-session-archive-detail.html` | `/sessions/[id]/scoreboard`, `/notes` | US-21 | M |
| P2 | `sp4-editor-agent-proposal-detail.html` | `/editor/agent-proposals/[id]/edit`+`/test` | (internal editor, no end-user US) | M |
| P3 | `sp4-profile-achievements.html` | `/profile/achievements` | US-26 | S |
| P3 | `sp4-knowledge-base-global.html` | `/knowledge-base/global` | US-19 | M |
| P3 | `sp4-knowledge-base-pdf-viewer.html` | `/knowledge-base/[id]/pdf` | US-19 | M |

**Total effort estimate**: 5×S + 5×M = ~10gg designer + ~5gg dev integration. Suggest scope into Phase C migration sweep sub-issues per cluster.

---

## Architectural inconsistencies flagged

### 1. `/hub/*` route-vs-mockup contradiction (Fowler)

| Route (codebase, LIVE) | Mockup status (Phase B audit) |
|---|---|
| `/hub/agents` | sp4-hub-agents.{html,jsx} → `forward-refactor-obsolete` (drafts tracking issue) |
| `/hub/games` | sp4-hub-games.{html,jsx} → `forward-refactor-obsolete` (drafts tracking issue) |
| `/hub/games/[id]` | NO mockup |
| `/hub/toolkits` | sp4-hub-toolkits.{html,jsx} → `forward-refactor-obsolete` (drafts tracking issue) |
| `/hub` (entry) | NO mockup |

**Two possible interpretations**:
1. **Routes should be DELETED** — Stage 3 #1026 de-versioning intent; replaced by `/games` multi-tab Asse D P2 #1899. Orphan routes after refactor.
2. **Routes need REFRESHED mockups** — kept for backward-compat URLs.

**Decision required**: open a sub-issue under Asse D P2 #1899 to choose path. Phase B obsolete classifications align with option 1.

### 2. Games hub tab variants undocumented (Adzic)

`/games` has 4 tabs (`?tab=discover` default, `?tab=catalogo`, `?tab=trending`, `?tab=community`) shipped per Asse D P2 #1899 sess.36. ONLY `?tab=discover` has mockup coverage (sp4-discover.html). Other 3 tabs have NO designer-validated visual contract; per CLAUDE.md they ship as ComingSoon placeholders.

**Decision required**: document ComingSoon as intentional design (commit a fidelity note), OR commission 3 dedicated mockups.

### 3. Many-to-many mapping ambiguity (Fowler)

7 mockups serve multiple routes per `MOCKUPS_INDEX.md`. Violates Interface Segregation Principle — no canonical primary. Risk: divergent route designs over time create split-brain ambiguity.

**Recommendation**: for each many-to-many mapping, designate ONE canonical route in MOCKUPS_INDEX.md `Mapped routes` column.

Examples:
- `settings.html` → 8 routes (settings + 7 sub-routes)
- `auth-flow.html` → 8 routes (login + register + 6 verify/reset variants)
- `sp4-toolkit-detail.html` → 5 routes
- `sp4-player-detail.html` → 5 routes
- `sp3-legal.html` → 4 routes (privacy/terms/cookies/cookie-settings)
- `notifications.html` → 2 routes
- `sp4-game-detail.html` → 3 routes (games + library variant + private-games)

---

## US-perspective gap matrix (top 10 of 30 US)

| US | Persona | Pages in sequence | Mockup coverage | Gap |
|---|---|---|---|---|
| US-9 Game detail tabs | Giulia | 7 pages (rules/faqs/reviews/sessions/strategies/chat) | 2/7 (29%) | 5 sub-tab mockups missing |
| US-13 GameNight create wizard | Marco | 4 pages | 4/4 (100%) ✅ | Covered |
| US-16 Session live tracking | Marco | 4 pages | 2/4 (50%) | scores/players sub-route mockups partial |
| US-19 In-chat lookup with citation | Alpha | 4 pages (dashboard→library→library/agent→KB) | 3/4 (75%) | `/knowledge-base/[id]/pdf` viewer mockup absent |
| US-21 Archive sessions | Aaron | 4 pages | 1/4 (25%) | scoreboard/notes/players sub-route mockups absent |
| US-7 Discover Netflix 7-row | Sara | 2 pages | sp4-discover.html pre Asse D #1899 multi-tab | needs forward-refactor for Discover-tab variant of `/games` |
| US-20 Player detail tabs | Marco | 6 pages (5 sub-tabs) | 1 composite mockup ✅ | OK (sp4-player-detail) |
| US-26 Profile + achievements | Giulia | 3 pages | settings.html → /profile; **achievements uncovered** | `/profile/achievements` mockup absent |
| US-27 AI agent chat | Sara | 3 pages | 2/3 (67%) | `/agents/[id]` mockup ambiguous via sp4-agent-detail |
| US-30 Toolkit detail/install/use | Marco | 4 pages | 2/4 (50%) | `/toolkits/[id]` standalone detail mockup absent |

---

## Expert consensus — top 3 actions

1. **WIEGERS + COCKBURN**: Open follow-up issue per Phase C migration: "Generate 5 game-detail tab mockups (US-9 unblocked)". Track under Asse D umbrella.
2. **ADZIC**: Document explicit ComingSoon placeholder decision for `/games?tab={catalogo,trending,community}` in CLAUDE.md, OR commission 3 mockups.
3. **FOWLER**: Resolve `/hub/*` route-vs-mockup contradiction — file decision sub-issue: retire routes OR commission new mockups.

These 3 consensus items are appended to `audits/tracking-issues-drafts.md` as Drafts 6/7/8 (created post designer sign-off via `pnpm audit-mockups:create-issues`).

---

## Quality scores

| Score | Value | Notes |
|---|---|---|
| Mockup-to-route coverage | 7.3/10 (95/130) | Strong but with 35-route gap |
| US sequence completeness | 6.5/10 | 13/30 US have ≥1 missing mockup |
| Architecture consistency | 5.0/10 | /hub/* contradiction + 7 many-to-many mappings |
| Specification testability | 7.0/10 | 5 obsoletes correctly flagged via Phase B |
| **Overall** | **6.5/10** | covers main app surfaces but post-MVP routes shipped ahead of designer-validated specs |

---

## Refs

- Phase B audit (this addendum extends it): `audits/2026-06-10-mockup-design-intent-audit.json`
- Sub-issue: #2127
- Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
- Spec-panel input agents output: research via parallel general-purpose subagents (2026-06-10)
- MOCKUPS_INDEX (mockup→route mapping): `admin-mockups/MOCKUPS_INDEX.md`
