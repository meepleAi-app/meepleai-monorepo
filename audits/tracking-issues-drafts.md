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

