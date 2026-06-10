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

## Draft 14: SPEC-PANEL #2 — Nav-chrome 3-way drift (Fowler)

**Title**: `[DS-17 architecture] Nav-chrome canonical primitives never consumed — pick deprecate-or-backfill`

**Body**:

## Context

DS-17 Phase B spec-panel addendum #2 (`audits/2026-06-10-nav-chrome-bgg-naming-audit.md` Section A) identified 3-way drift in nav-chrome:

| Layer | Pattern | State |
|---|---|---|
| Nav primitives `nanolith-nav-*.html` | search-pill ⌘K, LiveSessionPill (D5), dynamic-slot-3 (D6), chat slide-over (D7) | Self-documenting only |
| Page-mocks `sp4/sp3/sp7-*.jsx` | Should reuse primitives | Each re-implements inline `DesktopAuthNav` / `PhoneTopNav` / `MobileBottomBar` |
| Runtime `AppTopBar.tsx` | Should match primitives | Lacks search-pill, LiveSessionPill, dynamic-slot-3 |

Symptom: each layer evolves independently. No layer is the "single source of truth". Page-mock-to-runtime divergence will silently regenerate during Phase C migration.

## Additional violations

- `sp4-players-index.jsx` + `sp4-sessions-index.jsx` — primary nav destinations (in `TOP_BAR_NAV_IDS`) but render NO nav-chrome
- Detail mockups (sp4-game-detail, sp4-toolkit-detail, sp4-discover, sp4-game-nights-index, sp4-player-detail, sp4-agent-detail, sp4-kb-detail) drop 5-voice topbar — runtime always shows it
- No mockup documents OPEN mobile SideDrawer state (8-voice MAIN_NAV_ITEMS only visible at runtime)

## Two paths forward (pick one)

**Option A — Deprecate canonical nav primitives**:
- Mark `nanolith-nav-{topbar,bottom-mobile,chat-panel}.html` as `forward-refactor-obsolete`
- Remove decisions D5/D6/D7 from architecture docs
- Accept inline DesktopAuthNav / PhoneTopNav / MobileBottomBar re-implementation per page-mock as the convention
- Estimate: ~2gg (docs cleanup)

**Option B — Backfill + reference primitives uniformly**:
- Update `AppTopBar.tsx` to include search-pill (⌘K), LiveSessionPill, dynamic-slot-3
- Add `sp4-side-drawer-open.{html,jsx}` mockup documenting 8-voice open state
- Regenerate `sp4-players-index.{html,jsx}` + `sp4-sessions-index.{html,jsx}` with canonical chrome
- Update 7 detail mockups to render canonical 5-voice
- Estimate: ~5-7gg (designer + dev)

## Recommendation (Fowler)

**Option B** preserves design system integrity. The primitives represent decisions (D5/D6/D7) that emerged from explicit design work; abandoning them loses that institutional memory.

## Acceptance

- [ ] Decision committed in PR with rationale (A or B)
- [ ] If A: 3 primitives reclassified + fidelity.json `design_intent: forward-refactor-obsolete`
- [ ] If B: 4 mockups regenerated + 1 new mockup (side-drawer-open) + AppTopBar.tsx feature-complete

## Refs

- Nav-chrome audit: `audits/2026-06-10-nav-chrome-bgg-naming-audit.md` § Section A
- Asse B sidebar removal: #1977 (audit follow-up of #1974 F18)
- Nav primitives source: `admin-mockups/design_files/nanolith-nav-*.html`
- Umbrella: #2063
- Phase B sub-issue: #2127

---

## Draft 15: SPEC-PANEL #2 — BGG lint gate + 9 new violations (Newman+Wiegers)

**Title**: `[DS-17 ToS compliance] Add pnpm lint:bgg-mockups gate + flag 7 mockup violations + 2 codebase findings`

**Body**:

## Context

DS-17 Phase B audit flagged ONLY `sp4-add-game-bgg-step.{html,jsx}` for BGG ToS violation per #1903 ADR. Spec-panel addendum #2 (Section B) audit found **9 additional forbidden user-side BGG surfaces** that escaped Phase B classification:

### 7 mockup violations (NEW)

| Mockup | Forbidden surface |
|---|---|
| `sp4-upload-wizard-extended.{html,jsx}` | Step 0 source picker offers "Da BoardGameGeek" card → links to forbidden mockup |
| `sp4-library-desktop.jsx` | Hero "↓ Importa BGG" CTA + empty-state CTA links to forbidden mockup |
| `sp4-game-chat-tab.html` | Low-confidence chat fallback: "BGG forum thread" citation + "🔗 BGG · ricerca esterna" chip |
| `sp5-profile-settings.{html,jsx}` | "Connected services" panel exposes BGG as OAuth target |
| `sp3-how-it-works.jsx` | Onboarding card: "Connetti BGG — Sincronizza la tua collezione BoardGameGeek — OAuth · 30s" |
| `settings.jsx` | BggIcon SVG + Bio "BGG rank: 1.492" + Connected services BGG entry |
| `sp7-game-night-live.jsx:561` | Add-game CTA navigates to forbidden mockup |

### 2 codebase findings

| File | Line | Issue |
|---|---|---|
| `apps/web/src/components/dashboard/QuickActionCards.tsx` | 63 | "Cerca nel catalogo BGG" displayed to ALL users (no admin gate). Component appears orphan but is in bundle. |
| `apps/web/src/components/features/settings/settings-sections.ts` | 77 | User settings sidebar item subtitle still says "BGG, Discord" |

## Scope

1. **Reclassify 7 mockups** in fidelity.json as `forward-refactor-obsolete` (mirror sp4-add-game-bgg-step pattern, reference #1903)
2. **Fix codebase**: delete or admin-gate `QuickActionCards.tsx:63` + remove BGG from `settings-sections.ts:77` subtitle
3. **Add lint gate**: `pnpm lint:bgg-mockups` whitelist-incremental script
   - Scan `admin-mockups/design_files/**/*.{html,jsx}` + `apps/web/src/**` (excluding `/admin/`, `/api/`)
   - Match regex `/BGG|BoardGameGeek|boardgamegeek/i`
   - PASS if `design_intent === 'forward-refactor-obsolete'` OR line-level `<!-- BGG-ALLOWED: <reason> -->` justification OR file path matches admin scope
   - FAIL otherwise
   - Baseline locked at current violations; growth = CI break
   - Mirror pattern: `apps/web/scripts/mockup-annotations/lint-tokens-mockups.mjs` (DS-17-2 #2070)
4. **CI integration**: add to PR-gate workflow with `--strict --max-baseline <N>` flag

## Acceptance

- [ ] 7 mockup fidelity.json files reclassified `forward-refactor-obsolete` with #1903 ref
- [ ] `QuickActionCards.tsx:63` + `settings-sections.ts:77` fixed
- [ ] `pnpm lint:bgg-mockups` script implemented + tests
- [ ] CI workflow updated
- [ ] Baseline committed (`audits/2026-06-11-bgg-baseline.json`)

## Refs

- BGG ADR: #1903 (user-side BGG access forbidden)
- Nav-chrome+BGG+naming audit: `audits/2026-06-10-nav-chrome-bgg-naming-audit.md` § Section B
- DP-5 feedback (sess.46h 2026-06-09): user-side BGG access blocked
- Pattern reference: DS-17-2 #2070 (lint:tokens:mockups whitelist-incremental)
- Phase 1 spec (BGG removal): `docs/superpowers/specs/2026-05-22-hide-bgg-user-facing-design.md`
- Phase 2 spec (residual surfaces): `docs/superpowers/specs/2026-05-22-hide-bgg-user-facing-phase-2-design.md`
- Umbrella: #2063
- Phase B sub-issue: #2127

---

## Draft 16: SPEC-PANEL #2 — Naming + route architecture consistency (Adzic)

**Title**: `[DS-17 architecture] Standardize CRUD verbs + dynamic params + finish #2025 cleanup + suffix vocabulary`

**Body**:

## Context

DS-17 Phase B spec-panel addendum #2 (Section C) identified 8 systemic naming inconsistencies affecting mockup-to-route lookup reliability:

### Critical issues

1. **`sp4-*` prefix overloaded**: same prefix used for authenticated core (~30 files) AND session game-specific demos (~40 files for catan/codenames/paleo/power-grid/puerto-rico/wingspan/zombicide/skeleton variants)
2. **Twin family confusion**: `librogame-runthrough-*` vs surviving `sp6-libro-game-*` (incomplete #2025 cleanup — 4 files still duplicated)
3. **CRUD verb split**: mockups lean to `-create`, routes lean to `/new`
4. **Dynamic param sprawl**: 10 variants (`[id]` ✓, `[gameId]`, `[sessionId]`, `[campaignId]`, `[privateGameId]`, `[token]`, `[code]`, `[inviteToken]`, `[threadId]`, `[name]`) — should be 2-3
5. **Route namespace duplications**:
   - `/sessions/[id]/*` (8) vs `/sessions/live/[sessionId]/*` (5)
   - `/toolkit/*` (6 sing.) vs `/toolkits/[id]` (2 plur.)
   - `/hub/{games,agents,toolkits}` vs `/{games,agents,toolkits}` (Draft 13 covers this)
   - `/settings/*` vs `/profile?tab=settings&section=*` (Phase B implicit)
6. **Language drift**: `sp4-kb-globale.html` (Italian) → `/knowledge-base/global` (English)
7. **`nanolith-` prefix doesn't telegraph "primitive"**
8. **Subcomponent suffix sprawl**: 13 distinct suffixes used uncoordinated (-parts, -flavor, -ui, -tabs, -sections, -bodies, -renderers, -dice, -data, -tools, -stats, -live, -summary)

## Scope

1. **Standardize CRUD verb**: rename `sp7-game-night-create.html` → `sp7-game-night-new.html`; rename `sp4-editor-proposals-create.html` → `sp4-editor-proposals-new.html`. Update MOCKUPS_INDEX.md + any internal references.
2. **Collapse dynamic params**:
   - Migrate `[inviteToken]` → `[token]` (`/join/[inviteToken]` → `/join/[token]`)
   - Audit `[privateGameId]` → consider collapse to `[id]` (URL prefix disambiguates)
   - Decide `[token]` vs `[code]` semantic (opaque secret vs human-readable)
3. **Complete #2025 cleanup**: delete remaining 4 `sp6-libro-game-*` files OR rename to `.deprecated.html`
4. **Fix language drift**: rename `sp4-kb-globale.html` → `sp4-kb-global.html`
5. **Rename `nanolith-*`**: rename to `primitive-nav-{topbar,bottom-mobile,chat-panel}.html` OR move to `00-05` dev-fixture family
6. **Publish suffix vocabulary**: add table to `admin-mockups/README.md`:
   - `-live` Active/live play state
   - `-summary` Post-game completed state
   - `-parts` Shared sub-components for the family
   - `-flavor` Game-specific UI variants
   - `-data` Dataset fixture
   - `-renderers` Polymorphic dispatcher
   - `-tabs` Tab strip + content
   - `-sections` Vertical layout sections
   - `-ui` UI-only variant
   - `-bodies` (define)
   - `-tools` (define)
   - `-dice` (define)
   - `-stats` (define)

### Route namespace consolidation (separate sub-issues)

- 16a — Consolidate `/sessions/[id]/*` + `/sessions/live/[sessionId]/*` (sub-issue)
- 16b — Decide `/toolkit` vs `/toolkits` (sub-issue)
- 16c — `/hub/*` already tracked in Draft 13
- 16d — `/settings/*` vs `/profile?tab=settings` already implicit in Phase B obsoletes

## Acceptance

- [ ] CRUD verb rename: 2 mockups renamed + MOCKUPS_INDEX.md updated
- [ ] Dynamic params: `[inviteToken]` migrated; `[privateGameId]` decision committed
- [ ] #2025 cleanup completed: 4 sp6 files deleted or .deprecated
- [ ] `sp4-kb-globale.html` renamed
- [ ] `nanolith-nav-*` renamed
- [ ] Suffix vocabulary published in `admin-mockups/README.md`
- [ ] 4 sub-issues opened for namespace consolidation

## Refs

- Naming audit: `audits/2026-06-10-nav-chrome-bgg-naming-audit.md` § Section C
- #2025 cleanup precedent (sp6 deletions): closed
- Stage 3 #1026 — design system de-versioning (closed 2026-05-18)
- Umbrella: #2063
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

