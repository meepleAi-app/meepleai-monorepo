# DS-17-10 sp3 cluster migration — Design

**Status**: design approved 2026-06-11 sess.46p brainstorming
**Owner**: badsworm@gmail.com
**Sub-issue**: [#2208](https://github.com/meepleAi-app/meepleai-monorepo/issues/2208)
**Parent umbrella**: [#2063 DS-17 Mockup-to-App Fidelity](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
**Predecessor sub-issues**: #2160 DS-17 auth (PR #2164, sess.46n) + #2166 DS-17-11 sp6-7-nano (PR #2173, sess.46o)
**Unblock trigger**: EPIC #2096 closure sess.46p PR #2207 (`b98e4328b`)

## 1. Context

DS-17 Phase C-1 step 3/3 chiusura. Cluster sp3 (community/public surface — 8 mockup) era stato DEFERRED sess.46o per `sp3-shared-game-detail` rebuild blocker (EPIC #2096 WIP). EPIC #2096 ora CLOSED → trigger MET, reactivation possibile.

**Phase C-1 step status**:
| Step | Sub-issue | Status |
|---|---|---|
| 1 — auth cluster | #2160 | ✅ MERGED PR #2164 (`85d3ccfc8`) sess.46n |
| 2 — sp6-7-nano cluster | #2166 | ✅ MERGED PR #2173 (`e320b2de0`) sess.46o |
| **3 — sp3 cluster** | **#2208** | **🚧 this spec** |

Closure di DS-17-10 → **Phase C-1 step 3/3 complete** → unblocks Phase C-2 (SP4 core 106 mockup + sp4-sessions 50 mockup).

## 2. Decisioni (8 DEC totali)

### 2.1 Decisioni preserved (3 user-locked sess.46o)

Memory note `~/.claude/projects/D--Repositories-meepleai-monorepo-frontend/memory/ds-17-10-sp3-deferred-decisions.md`:

| # | Decisione | Rationale |
|---|---|---|
| DEC-Memory-1 | **sp3-library-public route-create + ship** (NOT skip pattern) | User scelta: validare empiricamente forward-refactor design vs defer Phase D. Effort +1gg vs skip pattern. |
| DEC-Memory-2 | **BGG cleanup as Stage 0 prep** — edit 3 JSX twins (how-it-works + shared-game-detail + faq-enhanced) pre-AI dispatch + extend #2151 | Pattern DEC-Pilot-7 identico a DS-17-11 sp6-7-nano sess.46o. AI dispatch reads post-cleanup state guaranteed BGG-free. |
| DEC-Memory-3 | **sp3-shared-game-detail story uses POST-#2096 rebuild deliverables** | Trigger MET via PR #2207. Story renders POST-rebuild `GameDetailDesktop` (M1+M2+M3+M5+M6+M4+M7 deliverables wire). |

### 2.2 Decisioni nuove sess.46p (5 user-locked brainstorming)

| # | Decisione | Rationale |
|---|---|---|
| DEC-1 | **1 sub-issue combined per cluster** (parity DS-17-11 #2166 pattern) | 1 spec + 1 plan + 1 PR admin-squash (P145 38a). Closure unica clean. Effort -30 min vs split. |
| DEC-2 | **BGG cleanup Stage 0 prep PRIMA story migration** (DEC-Pilot-7 pattern) | Atomic commit `chore(mockups): #DS17-10 BGG removal sp3 cluster` pre-branch story work. AI dispatch reads post-cleanup state. |
| DEC-3 | **Baseline capture inline per stem** (8 PNG Desktop 1440x900) | Visual gate attivo immediatamente. Rejected P247 defer Phase D batch (preferred user). |
| DEC-4 | **sp3-library-public forward-refactor handling**: ship + designer review NOTE in fidelity.json + tracking issue OPENED per future review | Riconosce forward-refactor 0.6 conf (memory note). Tracking issue → future re-design audit. |
| DEC-5 | **1 Agent per stem (7 dispatch sequential)** | Isolated scope per Agent → no context overflow risk. Sequential per skill rule (no parallel implementer). Effort: ~4-5h sequential vs ~3h batch (slight overhead, +1h). |

## 3. Scope finale (8 stems, NO skip)

| # | Stem | Route | Action | design_intent | Effort |
|---|---|---|---|---|---|
| 1 | sp3-shared-games | `(public)/shared-games/page.tsx` | Ship | current | ~20 min |
| 2 | sp3-shared-game-detail | `(public)/shared-games/[id]/` | Ship POST-#2096 integration | current | ~30 min |
| 3 | **sp3-library-public** | `(public)/library-public/` ❌ NEW | **Route-create + LibraryPublicHome + ship** | forward-refactor | ~6h |
| 4 | sp3-legal | `(public)/legal/page.tsx` | Ship | current | ~20 min |
| 5 | sp3-join | `(public)/join/page.tsx` (multi-route /event /session) | Ship canonical /join | current | ~30 min |
| 6 | sp3-how-it-works | `(public)/how-it-works/page.tsx` | Ship POST BGG cleanup | current | ~20 min |
| 7 | sp3-faq-enhanced | `(public)/faq/page.tsx` (multi-route) | Ship canonical /faq POST BGG cleanup | current | ~30 min |
| 8 | sp3-accept-invite | `(public)/accept-invite/page.tsx` (multi-route) | Ship canonical /accept-invite | current | ~30 min |

= 8 stems, ~3-4gg active work cumulative.

## 4. Architecture

### 4.1 Stage 0 — BGG cleanup atomic commit

3 JSX twins edit, HTML twins clean (no edit needed):

| File | Edit | Severity |
|---|---|---|
| `admin-mockups/design_files/sp3-how-it-works.jsx` | Line 231: `<span>cerca su BGG…</span>` → rimuovi reference | HIGH (already in #2151) |
| `admin-mockups/design_files/sp3-how-it-works.jsx` | Line 461: `'Cerca giochi direttamente da BoardGameGeek o aggiungili manualmente'` → `'Aggiungi giochi dal catalogo interno'` | HIGH (already in #2151) |
| `admin-mockups/design_files/sp3-shared-game-detail.jsx` | Lines 67-68: `{ id:'kb-wing-bgg', title:'BoardGameGeek FAQ', kind:'URL', url:'boardgamegeek.com/wingspan/faq' }` → rimuovi KB entry | MEDIUM (Phase B miss) |
| `admin-mockups/design_files/sp3-faq-enhanced.jsx` | Line 51: `'...link BoardGameGeek...'` → `'Suggerisci giochi via /contact'` | LOW (Phase B miss) |

**Commit**: `chore(mockups): #DS17-10 BGG removal sp3 cluster`

**#2151 extension**: comment con 2 nuovi findings (MEDIUM sp3-shared-game-detail + LOW sp3-faq-enhanced erano Phase B miss).

### 4.2 Stage 1 — sp3-library-public route + component (NEW)

**Files to create**:
- `apps/web/src/app/(public)/library-public/page.tsx` — Next.js App Router server component wrapper
- `apps/web/src/app/(public)/library-public/page.stories.tsx` — Storybook entry per route
- `apps/web/src/components/features/library-public/LibraryPublicHome.tsx` — client component pure render
- `apps/web/src/components/features/library-public/CommunityStatsRow.tsx` — NEW primitive (stats banner)
- `apps/web/src/components/features/library-public/FeaturedGamesCarousel.tsx` — NEW primitive (curated showcase)

**Reuses existing primitives** (per memory note):
- `HeroGradient` (pattern pub-hero da sp3-how-it-works precedente)
- `MeepleCard` con `entity="game"` variant `hero` / `grid`
- `EntityChip`

**Server-side data fetching** (in page.tsx server component):
```tsx
// Pseudo-code (concrete impl in plan)
export default async function LibraryPublicPage() {
  const featured = await fetchFeaturedGames(); // server-side
  const stats = await fetchCommunityStats();   // server-side
  return <LibraryPublicHome featured={featured} stats={stats} />;
}
```

**Mock data fixture** (per Storybook MSW handlers):
- `featured`: 4-6 game objects con `gameId/title/coverUrl/playerCount/rating`
- `stats`: `{ totalGames: number, totalPlayers: number, totalSessions: number, totalCommunityContent: number }`

**fidelity.json update** (runtime: `obsolete_tracking_issue` filled with NEW tracking issue number opened in Stage 1):
```json
{
  "design_intent": "forward-refactor",
  "obsolete_tracking_issue": "<TRACKING_ISSUE_NUM opened during Stage 1>",
  "viewports": ["desktop"]
}
```

**Tracking issue OPENED in Stage 1**: title "Designer review sp3-library-public forward-refactor (DS-17-10 sub-issue #2208)" — future iteration audit, no blocker su merge corrente. Issue number filled into fidelity.json in same Stage 1 commit.

### 4.3 Stage 2 — AI dispatch 7 standard stems story migration

**Pattern**: 1 Agent dispatch per stem (DEC-5). Sequential.

**Per ogni stem** (7 dispatch):
1. Agent reads mockup `.jsx` file
2. Agent generates 4-file scaffolds:
   - `apps/web/src/app/(public)/<route>/page.stories.tsx` — Storybook entry
   - Frame matrix argTypes per mockup multi-frame pattern P239
   - `<mockup>.fidelity.json` companion update se manca canonical config (most are present from Phase B)
   - MSW handlers in story file se data fetch needed (sp3-shared-games + sp3-shared-game-detail)
3. Human iterates ~30-40 min per stem (verify imports, alignment with route component, story label match)

**Special case sp3-shared-game-detail** (POST-rebuild integration):
```tsx
import { GameDetailDesktop } from '@/components/game-detail/GameDetailDesktop';

export default {
  title: 'SP3 / Shared Game Detail',
  component: GameDetailDesktop,
  parameters: {
    msw: {
      handlers: [
        // /api/v1/library/[gameId] returns LibraryGameDetail fixture
        // /api/v1/games/[gameId]/session-contributors returns Contributor[]
      ]
    }
  }
};

export const Default = { args: { gameId: 'seeded-fixture-id', initialTab: 'info' } };
export const ToolboxTab = { args: { ...Default.args, initialTab: 'toolbox' } };
export const HouseRulesTab = { args: { ...Default.args, initialTab: 'houseRules' } };
export const AgentChatTab = { args: { ...Default.args, initialTab: 'aiChat' } };
export const PartiteTab = { args: { ...Default.args, initialTab: 'partite' } };
```

Verifies the 7 EPIC #2096 deliverables wire correctly:
- M1 GameHero v2 (PR #2101)
- M2 Tabs animated underline (PR #2103)
- M3 ConnectionBar pip community (inline)
- M5 SessionContributorsStrip (PR #2036)
- M6 GameInfoTab 3-Card (PR #2207)
- M4 GameToolboxTab 1-Card (PR #2207)
- M7 Layout restructure (PR #2108)

### 4.4 Stage 3 — Baseline capture inline (8 PNG)

Storybook test runner capture Desktop 1440x900 per ogni stem:
```bash
pnpm --filter @meepleai/web exec storybook-test-runner --update-snapshots
# Output: <story>.spec.ts-snapshots/<story>-chromium.png per stem
```

**Output**: 8 PNG files (1 per stem) committed in story baseline directory.

**Commit**: `chore(stories): #2208 sp3 cluster baselines (8 PNG)`

## 5. Data flow

| Stem | Fetch | Notes |
|---|---|---|
| sp3-shared-games | MSW handler `/api/v1/shared-games?limit=N` | List response with mocked games |
| sp3-shared-game-detail | MSW `/api/v1/library/[gameId]` + `/api/v1/games/[gameId]/session-contributors` | POST-rebuild integration via GameDetailDesktop |
| sp3-library-public | MSW `/api/v1/library-public/featured?limit=4-6` + community stats | Server component fetch + props pass to client |
| sp3-legal | None | Static content |
| sp3-join | None | Static content |
| sp3-how-it-works | None | Static content |
| sp3-faq-enhanced | None | Static content |
| sp3-accept-invite | None (invitation token in URL params) | Static fallback if no token |

## 6. Testing

| Layer | Action |
|---|---|
| Storybook | 1 story per stem + Frame matrix argTypes |
| Visual gate | 8 PNG baseline via Storybook test runner |
| fidelity gate | `pnpm lint:fidelity` validates 8 fidelity.json |
| Annotation gate | `pnpm mockup-annotations:audit --denominator mappable --threshold 80` |
| Token gate | `pnpm lint:tokens` 0 violations |
| Mockup token gate | `pnpm lint:tokens:mockups --strict --max-baseline 1500` |
| BGG gate (codebase) | `pnpm lint:bgg` 0 violations |
| BGG gate (mockups) | `pnpm lint:bgg-mockups` clean POST Stage 0 cleanup |
| Typecheck | `pnpm typecheck` 0 errors |
| Backend build | Pre-push hook `dotnet build` 0 errors |

## 7. Sequencing

```
Pre-flight (P124):
✅ 1. git pull main-dev + verify branch hygiene (done)
✅ 2. gh issue list search → no duplicate (done)
✅ 3. gh issue create #2208 (done)
✅ 4. git checkout -b feature/issue-2208-ds-17-10-sp3-cluster (done)

Stage 0 BGG cleanup (~30 min):
5. Edit 3 JSX twins (how-it-works lines 231/461 + shared-game-detail lines 67-68 + faq-enhanced line 51)
6. git commit -m "chore(mockups): #DS17-10 BGG removal sp3 cluster"
7. Extend #2151 via gh issue comment (3 new findings)

Stage 1 sp3-library-public route + component (~6h):
8. Create page.tsx + page.stories.tsx
9. Create LibraryPublicHome.tsx + CommunityStatsRow.tsx + FeaturedGamesCarousel.tsx
10. Update sp3-library-public.fidelity.json
11. Open tracking issue "Designer review sp3-library-public forward-refactor"
12. git commit -m "feat(library-public): #2208 route + LibraryPublicHome component"

Stage 2 AI dispatch 7 stems (~4-5h sequential):
13. Dispatch Agent #1: sp3-shared-games
14. Dispatch Agent #2: sp3-shared-game-detail (POST-rebuild)
15. Dispatch Agent #3: sp3-legal
16. Dispatch Agent #4: sp3-join
17. Dispatch Agent #5: sp3-how-it-works
18. Dispatch Agent #6: sp3-faq-enhanced
19. Dispatch Agent #7: sp3-accept-invite
20. git commit per stem (or batch commit "feat(stories): #2208 sp3 cluster 7 story migration")

Stage 3 baseline capture (~1h):
21. pnpm storybook-test-runner --update-snapshots
22. git commit -m "chore(stories): #2208 sp3 cluster baselines (8 PNG)"

Stage 4 quality gates (~30 min):
23. pnpm test + lint + lint:tokens + lint:tokens:mockups + lint:bgg + lint:bgg-mockups + lint:fidelity + mockup-annotations:audit + typecheck

Stage 5 merge + closure (~30 min):
24. git push -u origin feature/issue-2208-ds-17-10-sp3-cluster
25. gh pr create --base main-dev
26. Designer review OR user waiver Opzione C
27. gh pr merge 2208 --admin --squash --delete-branch (P145 38a)
28. gh issue close #2208 with AC evidence
29. EPIC #2063 (DS-17 umbrella) Phase C-1 step 3/3 complete progress note
30. Memory entry `ds-17-10-sp3-cluster-shipped.md` + Phase C-2 preview
```

## 8. Effort recap

| Stage | Effort |
|---|---|
| Pre-flight + sub-issue + branch | ✅ done (~15 min) |
| Stage 0 BGG cleanup + #2151 extend | 30 min |
| Stage 1 sp3-library-public route + component | ~6h (1 day) |
| Stage 2 AI dispatch 7 stems sequential | ~4-5h (DEC-5 sequential, +1h vs batch) |
| Stage 3 baseline capture 8 PNG | 1h |
| Stage 4 quality gates | 30 min |
| Stage 5 merge + closure + memory | 30 min |
| **Total active work** | **~13-14h (~3.5gg)** |

## 9. Risk register

| # | Risk | Prob | Impact | Mitigation |
|---|---|---|---|---|
| R1 | sp3-library-public forward-refactor designer rejects post-merge | Medium | Low | Tracking issue OPENED + designer review queue (DEC-4) |
| R2 | AI dispatch hallucinations su 7 stems | Low | Medium | DEC-5 1 Agent per stem isolated scope, verified pattern P244 hybrid DS-17-11 |
| R3 | BGG cleanup miss in #2151 list | Low | Low | Stage 0 atomic commit + grep verify post-cleanup |
| R4 | sp3-shared-game-detail POST-rebuild story rotture | Low | High | MSW handlers test + verify M1-M7 deliverables wire (PR #2207 freshly merged in main-dev) |
| R5 | Baseline rebaseline needed post-PR designer feedback | Medium | Low | DEC-3 risk accepted (vs P247 defer batch rejected per user) |
| R6 | Annotation gate fail per nuova route sp3-library-public | Low | Medium | Update `admin-mockups/MOCKUPS_INDEX.md` with sp3-library-public mapping during Stage 1 |
| R7 | Stage 2 AI dispatch context overflow | Low | Low | DEC-5 1 Agent per stem (vs batch) mitigates |
| R8 | Phase D forward-refactor lavoro scope creep | Low | Low | OUT OF SCOPE esplicito (§ 10) |

## 10. Out of scope (explicit)

- ❌ M1-M7 EPIC #2096 deliverables (all shipped predecessor PRs #2101 + #2103 + inline + #2036 + #2108 + #2207)
- ❌ sp3 mockup design refactoring (design_intent: current → ship as-designed)
- ❌ BGG references in codebase (only mockup files in scope; codebase clean per #1903 #2123)
- ❌ Mobile viewports (DEC viewports: desktop only per fidelity.json across all 8 sp3 stems)
- ❌ Backend changes (pure FE work, no API surface changes)
- ❌ Phase D forward-refactor designer work (tracking issue per future re-design audit; not blocker for merge)
- ❌ Phase C-2 SP4 core (106 mockup) + sp4-sessions (50 mockup) — future Phase C step
- ❌ Other DS-17 phases (Phase D, Phase E)

## 11. References

| Type | Path / Link |
|---|---|
| Sub-issue | [#2208](https://github.com/meepleAi-app/meepleai-monorepo/issues/2208) |
| Parent umbrella | [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) DS-17 Mockup-to-App Fidelity |
| Phase C-1 spec | `docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md` |
| Predecessor sub-issue 1 | #2160 DS-17 auth (PR #2164) |
| Predecessor sub-issue 2 | #2166 DS-17-11 sp6-7-nano (PR #2173) |
| Unblock trigger | EPIC #2096 closure (PR #2207 `b98e4328b`) |
| BGG ToS umbrella | #2151 |
| Memory note (preserved decisioni) | `~/.claude/projects/D--Repositories-meepleai-monorepo-frontend/memory/ds-17-10-sp3-deferred-decisions.md` |
| Memory note (epic closure) | `~/.claude/projects/D--Repositories-meepleai-monorepo-frontend/memory/epic-2096-closure-shipped.md` |
| Mockup files | `admin-mockups/design_files/sp3-*.{html,jsx,fidelity.json}` |
| Mockup index | `admin-mockups/MOCKUPS_INDEX.md` |
| EPIC #2096 GameDetailDesktop | `apps/web/src/components/game-detail/GameDetailDesktop.tsx` (POST-#2207 integration) |
| Token canonicalization | `docs/for-developers/specs/2026-05-12-token-canonicalization.md` |

## 12. Acceptance criteria (sub-issue ready, mirrored in #2208 body)

### Stage 0 BGG cleanup
- [ ] 3 JSX twins edited (how-it-works lines 231/461 + shared-game-detail lines 67-68 + faq-enhanced line 51)
- [ ] `pnpm lint:bgg-mockups` clean post-cleanup
- [ ] #2151 comment con 3 nuovi findings appended

### Stage 1 sp3-library-public
- [ ] `apps/web/src/app/(public)/library-public/page.tsx` server wrapper
- [ ] `LibraryPublicHome.tsx` client component
- [ ] `CommunityStatsRow.tsx` + `FeaturedGamesCarousel.tsx` primitives
- [ ] `sp3-library-public.fidelity.json` updated with tracking issue
- [ ] Tracking issue OPENED for future designer review

### Stage 2 7 standard stems migration
- [ ] 7 Agent dispatch sequential (1 per stem)
- [ ] 7 `page.stories.tsx` files con Frame matrix argTypes
- [ ] sp3-shared-game-detail story renders POST-rebuild `GameDetailDesktop` component verifying M1-M7 wire

### Stage 3 baseline capture
- [ ] 8 PNG baseline Desktop 1440x900
- [ ] Storybook test runner pass

### Stage 4 quality gates
- [ ] `pnpm test` → 0 regression
- [ ] `pnpm lint` → 0 errors
- [ ] `pnpm lint:tokens` → 0 violations
- [ ] `pnpm lint:tokens:mockups --strict --max-baseline 1500` → no regression
- [ ] `pnpm lint:bgg` → clean
- [ ] `pnpm lint:bgg-mockups` → clean post Stage 0
- [ ] `pnpm lint:fidelity` → 0 violations
- [ ] `pnpm mockup-annotations:audit --denominator mappable --threshold 80` → ≥80%
- [ ] `pnpm typecheck` → 0 errors

### Stage 5 closure
- [ ] Admin-squash merge P145 38a volta
- [ ] Sub-issue #2208 closed con AC evidence
- [ ] EPIC #2063 (DS-17 umbrella) progress note Phase C-1 step 3/3 complete
- [ ] Memory entry `ds-17-10-sp3-cluster-shipped.md`
- [ ] Phase C-2 SP4 core + sp4-sessions preview

---

**End of design spec.**
