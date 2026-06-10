# Tracking Issues Drafts — DS-17 Phase B

NOT created until designer sign-off. After approval, `create-tracking-issues.mjs`
reads this file and creates GitHub issues.

## Draft 1: admin-mockups/design_files/sp4-hub-games.html

**Title**: `[DS-17] Delete obsolete sp4-hub-games mockup — /hub/games route retired by Asse D P2 multi-tab refactor`

**Body**:

## Context

The sp4-hub-games.{html,jsx} mockup pair targets the retired /hub/games route. Per CLAUDE.md Asse D follow-up P2 (#1899), the /games route was refactored as a multi-tab hub (discover/catalogo/trending/community) with Discover as default tab. The /hub/games legacy route is no longer canonical.

## Scope

- DELETE admin-mockups/design_files/sp4-hub-games.html
- DELETE admin-mockups/design_files/sp4-hub-games.jsx
- Verify no references in MOCKUPS_INDEX.md (currently not listed → non-mappable, consistent with retirement)
- Update audit manifest if scanned

## Acceptance

- [ ] Files deleted
- [ ] CI passes
- [ ] No broken refs in docs

## Refs

- Stage 3 #1026 (closed 2026-05-18) — hub/<entity> directories emptied
- Asse D P2 #1899 — /games multi-tab hub refactor
- DS-17 Phase B mockup audit (umbrella #2063)

---

## Draft 2: admin-mockups/design_files/sp4-hub-agents.html

**Title**: `[DS-17] Delete obsolete sp4-hub-agents mockup — /hub/agents route retired by Stage 3 deversioning`

**Body**:

## Context

The sp4-hub-agents.{html,jsx} mockup pair targets the retired /hub/agents route. Per CLAUDE.md Stage 3 #1026 (closed 2026-05-18), the legacy hub/<entity> directories were emptied post-codemod. Current canonical agents route is /agents (see sp4-agents-index.html → /agents in MOCKUPS_INDEX).

## Scope

- DELETE admin-mockups/design_files/sp4-hub-agents.html
- DELETE admin-mockups/design_files/sp4-hub-agents.jsx
- Verify no references in MOCKUPS_INDEX.md (currently not listed → non-mappable, consistent with retirement)
- Update audit manifest if scanned

## Acceptance

- [ ] Files deleted
- [ ] CI passes
- [ ] No broken refs in docs

## Refs

- Stage 3 #1026 (closed 2026-05-18) — hub/<entity> directories emptied
- sp4-agents-index → /agents (canonical)
- DS-17 Phase B mockup audit (umbrella #2063)

---

## Draft 3: admin-mockups/design_files/sp4-dashboard.html

**Title**: `Already tracked in #2114`

**Body**:

## Context

sp4-dashboard.{html,jsx} represents the pre-Asse-C dashboard design. Asse C #1898 shipped 2026-06-05 (sess.34) refactored the dashboard to priority-driven 4-section layout (ProssimiSection / RecentiSection / SuggestedSection / FriendsActivitySection) replacing the legacy 5 entity sections.

The codebase has surpassed this mockup — it is forward-refactor-obsolete.

## Existing tracking

Issue #2114 already tracks the deletion/supersession of this mockup. No new issue needed.

## Refs

- Asse C #1898 — priority-driven dashboard shipped 2026-06-05
- Existing tracking issue: #2114
- DS-17 Phase B mockup audit (umbrella #2063)

---

## Draft 4: admin-mockups/design_files/sp4-add-game-bgg-step.html

**Title**: `[DS-17] Delete obsolete sp4-add-game-bgg-step mockup — BGG ToS violation (user-side BGG access forbidden per #1903 ADR)`

**Body**:

## Context

The sp4-add-game-bgg-step.{html,jsx} mockup pair designs a USER-FACING BGG (BoardGameGeek) search step inside AddGameDrawer with rate-limit banner, HTTP 202 throttled state, and tier-quota lock. This violates the BGG ToS constraint documented in #1903 ADR and reinforced by CLAUDE.md DP-5 feedback (sess.46h 2026-06-09):

> 'user-side BGG access bloccato per ToS compliance — FirstGameStep usa catalog interno (api.games.getAll) NON useSearchBggGames (admin-only)'

## Forbidden surfaces in mockup

- BGG search input visible to non-admin users
- BGG rate-limit messaging exposed to user
- BGG throttled (HTTP 202) UX
- Tier-quota lock specifically gating BGG access

## Scope

- DELETE admin-mockups/design_files/sp4-add-game-bgg-step.html
- DELETE admin-mockups/design_files/sp4-add-game-bgg-step.jsx
- Remove entry from MOCKUPS_INDEX.md (currently mapped to /library/proposals, /library/propose)
- Verify AddGameDrawer current implementation uses catalog interno (api.games.getAll), NOT useSearchBggGames

## Acceptance

- [ ] Files deleted
- [ ] MOCKUPS_INDEX.md entry removed
- [ ] CI passes
- [ ] AddGameDrawer audit confirms ToS compliance

## Refs

- #1903 ADR — BGG ToS user-side access forbidden
- CLAUDE.md DP-5 BGG flag during mockup migration (sess.46h)
- Issue #911 (original mockup issue)
- DS-17 Phase B mockup audit (umbrella #2063)

---

## Draft 5: admin-mockups/design_files/sp4-hub-games.jsx

**Title**: `[DS-17] Delete obsolete sp4-hub-games mockup — /hub/games route retired by Asse D P2 multi-tab refactor`

**Body**:

## Context

The sp4-hub-games.{html,jsx} mockup pair targets the retired /hub/games route. Per CLAUDE.md Asse D follow-up P2 (#1899), the /games route was refactored as a multi-tab hub (discover/catalogo/trending/community) with Discover as default tab. The /hub/games legacy route is no longer canonical.

## Scope

- DELETE admin-mockups/design_files/sp4-hub-games.html
- DELETE admin-mockups/design_files/sp4-hub-games.jsx
- Verify no references in MOCKUPS_INDEX.md (currently not listed → non-mappable, consistent with retirement)
- Update audit manifest if scanned

## Acceptance

- [ ] Files deleted
- [ ] CI passes
- [ ] No broken refs in docs

## Refs

- Stage 3 #1026 (closed 2026-05-18) — hub/<entity> directories emptied
- Asse D P2 #1899 — /games multi-tab hub refactor
- DS-17 Phase B mockup audit (umbrella #2063)

---

## Draft 6: admin-mockups/design_files/sp4-hub-agents.jsx

**Title**: `[DS-17] Delete obsolete sp4-hub-agents mockup — /hub/agents route retired by Stage 3 deversioning`

**Body**:

## Context

The sp4-hub-agents.{html,jsx} mockup pair targets the retired /hub/agents route. Per CLAUDE.md Stage 3 #1026 (closed 2026-05-18), the legacy hub/<entity> directories were emptied post-codemod. Current canonical agents route is /agents (see sp4-agents-index.html → /agents in MOCKUPS_INDEX).

## Scope

- DELETE admin-mockups/design_files/sp4-hub-agents.html
- DELETE admin-mockups/design_files/sp4-hub-agents.jsx
- Verify no references in MOCKUPS_INDEX.md (currently not listed → non-mappable, consistent with retirement)
- Update audit manifest if scanned

## Acceptance

- [ ] Files deleted
- [ ] CI passes
- [ ] No broken refs in docs

## Refs

- Stage 3 #1026 (closed 2026-05-18) — hub/<entity> directories emptied
- sp4-agents-index → /agents (canonical)
- DS-17 Phase B mockup audit (umbrella #2063)

---

## Draft 7: admin-mockups/design_files/sp4-dashboard.jsx

**Title**: `Already tracked in #2114`

**Body**:

## Context

sp4-dashboard.{html,jsx} represents the pre-Asse-C dashboard design. Asse C #1898 shipped 2026-06-05 (sess.34) refactored the dashboard to priority-driven 4-section layout (ProssimiSection / RecentiSection / SuggestedSection / FriendsActivitySection) replacing the legacy 5 entity sections.

The codebase has surpassed this mockup — it is forward-refactor-obsolete.

## Existing tracking

Issue #2114 already tracks the deletion/supersession of this mockup. No new issue needed.

## Refs

- Asse C #1898 — priority-driven dashboard shipped 2026-06-05
- Existing tracking issue: #2114
- DS-17 Phase B mockup audit (umbrella #2063)

---

## Draft 8: admin-mockups/design_files/sp4-add-game-bgg-step.jsx

**Title**: `[DS-17] Delete obsolete sp4-add-game-bgg-step mockup — BGG ToS violation (user-side BGG access forbidden per #1903 ADR)`

**Body**:

## Context

The sp4-add-game-bgg-step.{html,jsx} mockup pair designs a USER-FACING BGG (BoardGameGeek) search step inside AddGameDrawer with rate-limit banner, HTTP 202 throttled state, and tier-quota lock. This violates the BGG ToS constraint documented in #1903 ADR and reinforced by CLAUDE.md DP-5 feedback (sess.46h 2026-06-09):

> 'user-side BGG access bloccato per ToS compliance — FirstGameStep usa catalog interno (api.games.getAll) NON useSearchBggGames (admin-only)'

## Forbidden surfaces in mockup

- BGG search input visible to non-admin users
- BGG rate-limit messaging exposed to user
- BGG throttled (HTTP 202) UX
- Tier-quota lock specifically gating BGG access

## Scope

- DELETE admin-mockups/design_files/sp4-add-game-bgg-step.html
- DELETE admin-mockups/design_files/sp4-add-game-bgg-step.jsx
- Remove entry from MOCKUPS_INDEX.md (currently mapped to /library/proposals, /library/propose)
- Verify AddGameDrawer current implementation uses catalog interno (api.games.getAll), NOT useSearchBggGames

## Acceptance

- [ ] Files deleted
- [ ] MOCKUPS_INDEX.md entry removed
- [ ] CI passes
- [ ] AddGameDrawer audit confirms ToS compliance

## Refs

- #1903 ADR — BGG ToS user-side access forbidden
- CLAUDE.md DP-5 BGG flag during mockup migration (sess.46h)
- Issue #911 (original mockup issue)
- DS-17 Phase B mockup audit (umbrella #2063)

---

## Draft 9: admin-mockups/design_files/sp4-hub-toolkits.html

**Title**: `DS-17 Phase B: classify sp4-hub-toolkits as forward-refactor-obsolete (Pre-Stage-3 retired)`

**Body**:

## Context

During DS-17 Phase B mockup audit (sub-issue #2127, cluster sp4-sessions), the file pair `admin-mockups/design_files/sp4-hub-toolkits.{html,jsx}` was classified as `forward-refactor-obsolete`.

## Evidence

- HTML title explicitly tags it: `Pre-Stage-3 · Hub Toolkits (authenticated) — /hub/toolkits`
- Same `Pre-Stage-3 · Hub …` pattern as sp4-hub-games and sp4-hub-agents, both already classified `forward-refactor-obsolete` in sp4-core audit (precedent set in cluster sp4-core, 8 obsolete pairs incl. sp4-hub-games + sp4-hub-agents)
- Stage 3 #1026 (under umbrella #1023, design system de-versioning, **COMPLETE 2026-05-18**) retired the legacy `hub/<entity>` pattern — see CLAUDE.md § Active Freezes:
  > 'hub/<entity> 3-routes (REFACTOR-FORWARD)'
- Current canonical hub layout supersedes the Pre-Stage-3 mockup; migrating this fixture would re-introduce a retired pattern.

## Action

- [ ] Mark `sp4-hub-toolkits.html` + `sp4-hub-toolkits.jsx` as `forward-refactor-obsolete` in `audits/2026-06-10-mockup-design-intent-manifest.json`.
- [ ] Skip Storybook story migration for this pair (DS-17 Phase 2.5+ pattern).
- [ ] Document in `MOCKUPS_INDEX.md` annotation that the route `/hub/toolkits` is no longer the canonical surface; the actual user-reachable route is `/toolkits` (sp4-toolkit-detail family) post Stage 3.
- [ ] Verify no `@mockup` JSDoc annotations in `apps/web/src/app/**/page.tsx` reference this mockup (DS-17-1 sweep #2084 mappable=true gate).

## References

- Umbrella: #2063 (DS-17 mockup fidelity)
- Phase B audit sub-issue: #2127
- Stage 3 closure: #1026
- Precedent: sp4-core cluster audit, 8 forward-refactor-obsolete files (sp4-dashboard, sp4-hub-games, sp4-hub-agents, sp4-add-game-bgg-step pairs)
- CLAUDE.md § Design System De-versioning — COMPLETE 2026-05-18

## Cluster

`sp4-sessions` (52 files: 50 `current` + 2 `forward-refactor-obsolete` for sp4-hub-toolkits pair)

---

## Draft 10: admin-mockups/design_files/sp4-hub-toolkits.jsx

**Title**: `DS-17 Phase B: classify sp4-hub-toolkits as forward-refactor-obsolete (Pre-Stage-3 retired)`

**Body**:

## Context

During DS-17 Phase B mockup audit (sub-issue #2127, cluster sp4-sessions), the file pair `admin-mockups/design_files/sp4-hub-toolkits.{html,jsx}` was classified as `forward-refactor-obsolete`.

## Evidence

- HTML title explicitly tags it: `Pre-Stage-3 · Hub Toolkits (authenticated) — /hub/toolkits`
- Same `Pre-Stage-3 · Hub …` pattern as sp4-hub-games and sp4-hub-agents, both already classified `forward-refactor-obsolete` in sp4-core audit (precedent set in cluster sp4-core, 8 obsolete pairs incl. sp4-hub-games + sp4-hub-agents)
- Stage 3 #1026 (under umbrella #1023, design system de-versioning, **COMPLETE 2026-05-18**) retired the legacy `hub/<entity>` pattern — see CLAUDE.md § Active Freezes:
  > 'hub/<entity> 3-routes (REFACTOR-FORWARD)'
- Current canonical hub layout supersedes the Pre-Stage-3 mockup; migrating this fixture would re-introduce a retired pattern.

## Action

- [ ] Mark `sp4-hub-toolkits.html` + `sp4-hub-toolkits.jsx` as `forward-refactor-obsolete` in `audits/2026-06-10-mockup-design-intent-manifest.json`.
- [ ] Skip Storybook story migration for this pair (DS-17 Phase 2.5+ pattern).
- [ ] Document in `MOCKUPS_INDEX.md` annotation that the route `/hub/toolkits` is no longer the canonical surface; the actual user-reachable route is `/toolkits` (sp4-toolkit-detail family) post Stage 3.
- [ ] Verify no `@mockup` JSDoc annotations in `apps/web/src/app/**/page.tsx` reference this mockup (DS-17-1 sweep #2084 mappable=true gate).

## References

- Umbrella: #2063 (DS-17 mockup fidelity)
- Phase B audit sub-issue: #2127
- Stage 3 closure: #1026
- Precedent: sp4-core cluster audit, 8 forward-refactor-obsolete files (sp4-dashboard, sp4-hub-games, sp4-hub-agents, sp4-add-game-bgg-step pairs)
- CLAUDE.md § Design System De-versioning — COMPLETE 2026-05-18

## Cluster

`sp4-sessions` (52 files: 50 `current` + 2 `forward-refactor-obsolete` for sp4-hub-toolkits pair)

---

## Draft 11: SPEC-PANEL — 5 game-detail tab mockups missing (Wiegers+Cockburn)

**Title**: `[DS-17 Phase C gap] Commission 5 game-detail sub-tab mockups (US-9 unblocked)`

**Body**:

## Context

DS-17 Phase B spec-panel critique (sub-issue #2127, `audits/2026-06-10-mockup-coverage-gap-report.md`) found that US-9 (Game detail tabs) has 5/7 sub-routes WITHOUT designer-validated mockup coverage. PRD f3 (#1929 game-detail rebuild) shipped tabs ahead of mockup specs.

## Routes without mockup coverage

- `/games/[id]/rules` — rules tab
- `/games/[id]/reviews` — M1 friend-first commentary tab
- `/games/[id]/strategies` — strategies tab
- `/games/[id]/chat` — chat tab (partial — sp4-game-chat-tab.html composite)
- `/games/[id]/faqs` — FAQs tab (partial — sp3-faq-enhanced.html shared)

## Scope

Commission 5 mockup pairs (HTML+JSX) under `admin-mockups/design_files/`:
- [ ] `sp4-game-detail-tab-rules.{html,jsx}` (S effort, sub-tab variant)
- [ ] `sp4-game-detail-tab-reviews.{html,jsx}` (M effort, commentary friend-first M1)
- [ ] `sp4-game-detail-tab-strategies.{html,jsx}` (S effort)
- [ ] `sp4-game-detail-tab-chat.{html,jsx}` — standalone (not composite) (S effort)
- [ ] `sp4-game-detail-tab-faqs.{html,jsx}` — game-detail variant (S effort)

Plus update MOCKUPS_INDEX.md, generate fidelity.json stubs.

## Acceptance

- [ ] 5 mockup pairs committed
- [ ] MOCKUPS_INDEX.md updated
- [ ] 5 fidelity.json stubs generated + `pnpm lint:fidelity` passes
- [ ] US-9 happy path 7/7 routes covered

## Refs

- Gap report: `audits/2026-06-10-mockup-coverage-gap-report.md`
- PRD f3 game-detail rebuild: #1929
- Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
- Phase B sub-issue: #2127

---

## Draft 12: SPEC-PANEL — Games hub tab variants undocumented (Adzic)

**Title**: `[DS-17 Phase C gap] Document /games?tab={catalogo,trending,community} ComingSoon decision OR commission 3 mockups`

**Body**:

## Context

DS-17 Phase B spec-panel critique (sub-issue #2127) found that the Games hub (`/games`) has 4 tabs per Asse D P2 #1899 sess.36 (discover/catalogo/trending/community) but ONLY discover has mockup coverage. 3 tabs ship as ComingSoon placeholders per CLAUDE.md.

## Specification-by-example gap

```
Given a user on /games
When they switch to tab=catalogo
Then they see [...???...]  ← no mockup contract

Given a user on /games
When they switch to tab=trending
Then they see [...???...]  ← no mockup contract

Given a user on /games
When they switch to tab=community
Then they see [...???...]  ← no mockup contract (ships as ComingSoon stub)
```

## Decision required (pick one)

**Option A — Document ComingSoon as intentional**:
- [ ] Add `audits/coming-soon-tabs-decision.md` documenting that 3 tabs intentionally ship as placeholder
- [ ] Note in MOCKUPS_INDEX.md that /games?tab={catalogo,trending,community} have no mockup per ComingSoon stub design
- [ ] Add fidelity.json for sp4-discover.html with `_comment` noting it's tab-1-of-4

**Option B — Commission 3 mockups**:
- [ ] `sp4-games-tab-catalogo.{html,jsx}` (M effort)
- [ ] `sp4-games-tab-trending.{html,jsx}` (S effort)
- [ ] `sp4-games-tab-community.{html,jsx}` (M effort, replaces ComingSoon)
- [ ] Update MOCKUPS_INDEX.md, generate fidelity.json stubs

## Acceptance

- [ ] Decision committed in PR with rationale (A or B)
- [ ] If A: doc committed + index annotated
- [ ] If B: 3 mockup pairs + index + fidelity.json pass lint

## Refs

- Asse D follow-up P2: #1899 sess.36 (4-tab Games hub shipped 2026-06-05)
- Gap report: `audits/2026-06-10-mockup-coverage-gap-report.md`
- Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
- Phase B sub-issue: #2127

---

## Draft 13: SPEC-PANEL — /hub/* route-vs-mockup contradiction (Fowler)

**Title**: `[DS-17 architecture decision] /hub/* routes LIVE in codebase but mockups OBSOLETE — retire or refresh?`

**Body**:

## Context

DS-17 Phase B spec-panel critique (sub-issue #2127) identified a route-vs-mockup contradiction violating "single source of truth":

| Route (codebase, LIVE) | Mockup (Phase B audit) |
|---|---|
| `/hub/agents` | sp4-hub-agents.{html,jsx} → `forward-refactor-obsolete` (Draft 2/6) |
| `/hub/games` | sp4-hub-games.{html,jsx} → `forward-refactor-obsolete` (Draft 1/5) |
| `/hub/games/[id]` | NO mockup |
| `/hub/toolkits` | sp4-hub-toolkits.{html,jsx} → `forward-refactor-obsolete` (Draft 9/10) |
| `/hub` (entry) | NO mockup |

## Two paths forward (pick one)

**Option A — Retire `/hub/*` routes (Stage 3 #1026 intent)**:
- Add 301 redirects from `/hub/games` → `/games?tab=catalogo`, `/hub/agents` → `/agents`, `/hub/toolkits` → `/toolkit`, `/hub` → `/games?tab=discover`
- Delete page.tsx files under `apps/web/src/app/(authenticated)/hub/`
- Verify no internal links reference `/hub/*` (search codebase)
- This option aligns with Phase B obsolete classifications + Asse D P2 #1899 multi-tab refactor

**Option B — Refresh mockups + keep routes**:
- Commission 5 new mockups: `sp4-hub-{entry,games,games-detail,agents,toolkits}-refreshed.{html,jsx}` (post Stage-3 design language)
- Update MOCKUPS_INDEX.md
- Generate fidelity.json with `design_intent: current` overriding Phase B obsolete classification
- Estimate: ~10gg designer + 3gg dev

## Recommendation (Fowler)

**Option A** aligns with:
- Stage 3 #1026 de-versioning intent (closed 2026-05-18)
- Asse D P2 #1899 multi-tab refactor (Discover as default)
- Phase B obsolete classifications (5 hub mockups all flagged)
- Single source of truth principle

## Acceptance

- [ ] Decision committed in PR with rationale (A or B)
- [ ] If A: redirects + page.tsx deletions + link audit
- [ ] If B: 5 refreshed mockup pairs + MOCKUPS_INDEX.md + fidelity.json overrides

## Refs

- Stage 3 #1026 — design system de-versioning (closed 2026-05-18)
- Asse D follow-up P2 #1899 — /games multi-tab hub refactor
- Gap report: `audits/2026-06-10-mockup-coverage-gap-report.md`
- Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
- Phase B sub-issue: #2127

---

