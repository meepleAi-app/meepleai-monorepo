# DS-17 Phase C-2 SP4 split + DS-17-12 sp4-core-catalog — Design

**Status**: design approved 2026-06-11 sess.46p brainstorming
**Owner**: badsworm@gmail.com
**Sub-issue (1st of 4)**: [#2214](https://github.com/meepleAi-app/meepleai-monorepo/issues/2214) DS-17-12 sp4-core-catalog
**Parent umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) DS-17 Mockup-to-App Fidelity
**Phase C-1 closure**: 3/3 step shipped sess.46p (auth #2160 PR #2164 + sp6-7-nano #2166 PR #2173 + sp3 #2208 PR #2211)

## 1. Context

DS-17 Phase C-1 (3 cluster, ~22h cumulative) closed sess.46p. **Phase C-2** target: SP4 surface (57 mockup HTML stems = 17 sp4-session-* + 40 non-session). Cumulative effort estimate ~40-46h (~8-10gg) → cluster split into 4 sub-issue mandatory per single-PR cap (~12-14h).

This document is a **META + 1st sub-issue spec**:
- § 2-3 cover META Phase C-2 split (4 sub-issue boundaries, sequencing, shared DEC) — applicable a tutti i 4 sub-issue
- § 4-12 cover DS-17-12 sp4-core-catalog detail (14 stems) — implementabile immediato

DS-17-13 sp4-core-content + DS-17-14 sp4-core-admin + DS-17-15 sp4-sessions specifiche brainstormed in future sessions per DEC-2.

## 2. Phase C-2 META cluster split (4 sub-issue)

| # | Sub-issue | Stems | Effort | Status |
|---|---|---|---|---|
| 1 | **DS-17-12 sp4-core-catalog** | 14 | ~12-14h | **THIS spec** |
| 2 | DS-17-13 sp4-core-content | ~13 | ~12-14h | Future brainstorm |
| 3 | DS-17-14 sp4-core-admin | ~10 | ~10-12h | Future brainstorm |
| 4 | DS-17-15 sp4-sessions (skeleton-first) | 3 base + 7 lazy stub | ~6h | Future brainstorm |

**Total Phase C-2 cumulative**: ~40-46h. Per-game session stories (Catan/Codenames/Paleo/Power Grid/Puerto Rico/Wingspan/Zombicide live+summary = 14 stories) **deferred a Phase C-3** (~6-8h follow-up).

### 2.1 Sub-issue boundaries reference

**DS-17-12 sp4-core-catalog** (this spec, § 4 detail):
- Stems: library + games + agents + chat + discover + add-game + upload (catalog-facing surface)

**DS-17-13 sp4-core-content** (future):
- Stems: kb + editor + toolkit + play-records (content-creation surface)

**DS-17-14 sp4-core-admin** (future):
- Stems: dashboard (obsolete) + hub-* (obsolete) + players + game-nights + misc admin surfaces

**DS-17-15 sp4-sessions (skeleton-first)** (future):
- Stems: sp4-session-skeleton-live + sp4-session-summary-skeleton + sp4-session-play (3 base) + 7 per-game lazy stub stories (Catan/Codenames/Paleo/Power Grid/Puerto Rico/Wingspan/Zombicide). Per-game live+summary MSW + flavor components defer a Phase C-3.

### 2.2 META DEC (apply to all 4 sub-issue)

| # | Decisione | Rationale |
|---|---|---|
| DEC-1 | 4 sub-issue feature-area split (catalog + content + admin + sessions skeleton-first) | Single-PR cap ~12-14h. Clean feature boundaries. 4 PR sequential P145 39a-42a vs 3 (PRs too large ~600+ LOC) vs 5 (overhead +1 cycle). |
| DEC-2 | META + DS-17-12 in 1 combined spec; sub-issue 13-15 future brainstorm | Concrete next-step actionable + future flexibility per discovery durante DS-17-12 execution. |
| DEC-3 | DS-17-15 sessions skeleton-first + per-game lazy (no MSW per Catan/Codenames/etc.) | Reduces sessions effort ~12-14h → ~6h. Per-game complexity defer a Phase C-3 (MSW + flavor components scope substantial). |
| DEC-4 | Hybrid P251-aware AI dispatch (inline batch simple + Agent complex) | P251 evidence DS-17-10: scaffold-template-similar stems → batch inline 80-90% time saving vs subagent dispatch. Reserve Agent for stem-specific reasoning (route-create, forward-refactor, integration, MSW). |
| DEC-inherited-1 | DEC-Pilot-7 BGG cleanup as Stage 0 prep (conditional su audit results per sub-issue) | Pattern identico DS-17-11 + DS-17-10. Pre-execution grep BGG references; if found → atomic commit + extend #2151. |
| DEC-inherited-2 | Forward-refactor → ship + tracking issue (sp3-library-public precedent) | Ship base scaffold + tracking issue OPENED per future designer review. NOT skip (precedent applied DS-17-10 sp3-library-public PR #2211 + #2209 tracking). |

### 2.3 Sequencing Phase C-2 (4 sub-issue sequential)

```
DS-17-12 catalog (THIS spec) → PR ~ #2215 P145 39a
  ↓
DS-17-13 content brainstorm → spec → plan → execution → PR P145 40a
  ↓
DS-17-14 admin brainstorm → spec → plan → execution → PR P145 41a
  ↓
DS-17-15 sessions skeleton-first brainstorm → spec → plan → execution → PR P145 42a
  ↓
EPIC #2063 Phase C-2 4/4 complete (Phase D + per-game session follow-up Phase C-3 NEXT)
```

Sequential per skill rule (no parallel implementation subagents). Brainstorming can be parallel future sessions if desired.

---

## 3. SP4 stem inventory (57 total)

| Category | Count | Examples |
|---|---|---|
| sp4-session-* | 17 | skeleton + per-game (Catan/Codenames/Paleo/Power Grid/Puerto Rico/Wingspan/Zombicide live+summary) + play |
| Non-session sp4 | 40 | library/games/agents/discover/chat/citation/add-game/upload (catalog) + kb/editor/toolkit/play-records (content) + dashboard/hub/players/game-nights (admin) |
| **Sub-totals by design_intent** | | |
| current (ship) | ~50 | Standard migration pattern |
| forward-refactor (ship + tracking) | 2 | sp4-kb-detail + sp4-library-mobile |
| forward-refactor-obsolete (skip) | 5 | sp4-dashboard + sp4-hub-agents + sp4-hub-games + sp4-hub-toolkits + sp4-add-game-bgg-step |

---

## 4. DS-17-12 sp4-core-catalog detail (this spec)

### 4.1 Stems (14)

| # | Stem | Route | design_intent | Action | Effort |
|---|---|---|---|---|---|
| 1 | sp4-library-desktop | `(authenticated)/library/` | current | Ship | ~30 min |
| 2 | **sp4-library-mobile** | `(authenticated)/library/` (mobile <768px) | **forward-refactor** | **Ship + tracking issue** | ~2h |
| 3 | sp4-library-wishlist | `(authenticated)/library/wishlist/` | current | Ship | ~30 min |
| 4 | **sp4-add-game-bgg-step** | `(authenticated)/library/proposals/` | **forward-refactor-obsolete** | **SKIP + closure note** | ~5 min |
| 5 | sp4-add-game-drawer | drawer mockup | current | Ship | ~30 min |
| 6 | sp4-add-game-pdf-dedup | `(authenticated)/library/private/add/` | current | Ship | ~30 min |
| 7 | sp4-games-index | `(authenticated)/games/` | current | Ship | ~30 min |
| 8 | sp4-game-detail | `(authenticated)/games/[id]/` | current | Ship POST EPIC #2096 (verify M1-M7 wire) | ~45 min (Agent dispatch) |
| 9 | sp4-agents-index | `(authenticated)/agents/` | current | Ship | ~30 min |
| 10 | sp4-agent-detail | `(authenticated)/agents/[id]/` | current | Ship | ~30 min |
| 11 | sp4-game-chat-tab | component-mock (embedded) | current | Ship as component-mock story | ~30 min |
| 12 | sp4-citation-pdf-viewer | component-mock (embedded) | current | Ship as component-mock story | ~30 min |
| 13 | sp4-discover | `(authenticated)/discover/` | current | Ship | ~30 min |
| 14 | sp4-upload-wizard-extended | upload wizard | current | Ship | ~30 min |

**Distribution**: 1 skip + 1 forward-refactor + 1 Agent (POST-#2096) + 11 inline batch standard = 14 stems.

### 4.2 Implementation strategy per stem class

| Class | Stems | Pattern |
|---|---|---|
| **Skip (1)** | sp4-add-game-bgg-step | Document in PR body. NO story file. Update fidelity.json with closure note + DS-17-12 reference. |
| **Forward-refactor (1)** | sp4-library-mobile | Agent dispatch — scaffold base story + open designer review tracking issue + update fidelity.json `obsolete_tracking_issue` (sp3-library-public precedent). |
| **POST-#2096 wire verify (1)** | sp4-game-detail | Agent dispatch — story renders existing `games/[id]/page.tsx` (uses GameDetailView component); verify integrates correctly with EPIC #2096 deliverables. MSW handlers if data fetching. |
| **Standard ship inline (11)** | All current sans wire-verify | Hybrid P251 batch — all 11 stems scaffold-template-similar (page component import + Storybook nextjs parameters + Default story). |

### 4.3 Stage 0 BGG cleanup audit

Pre-execution grep:
```bash
grep -in "BGG\|BoardGameGeek\|boardgamegeek" admin-mockups/design_files/sp4-{library-*,add-game-*,games-index,game-detail,agents-*,agent-detail,game-chat-tab,citation-*,discover,upload-wizard-*}.jsx 2>/dev/null
```

If references found → Stage 0 atomic commit + extend #2151 (DEC-inherited-1 DEC-Pilot-7 pattern). If clean → skip Stage 0.

**Hint**: sp4-add-game-bgg-step is OBSOLETE → its content irrelevant. Other catalog stems likely clean (per Phase B audit). Verify via grep.

### 4.4 Routes existence verification

Pre-execution check that target routes exist (no route-create planned in DS-17-12, unlike DS-17-10 sp3-library-public):

```bash
for route in library library/wishlist library/proposals library/private/add games agents discover; do
  if [ -f "apps/web/src/app/(authenticated)/$route/page.tsx" ]; then
    echo "✓ $route"
  else
    echo "✗ MISSING $route"
  fi
done
```

If MISSING → escalate to user (route-create out of DS-17-12 scope, would need new sub-issue).

### 4.5 Component-mock stems handling

sp4-game-chat-tab + sp4-citation-pdf-viewer are component-mocks (no standalone route). Story file location:
- `apps/web/src/components/<feature>/<Component>.stories.tsx` (next to component) per existing pattern
- Identify the actual component path via grep before scaffolding

Story title format: `Component-mocks / sp4-game-chat-tab`, `Component-mocks / sp4-citation-pdf-viewer`.

## 5. Sequencing DS-17-12

```
Pre-flight (P124):
✅ 1. git pull main-dev + branch hygiene (done)
✅ 2. gh issue list search → no duplicate (done)
✅ 3. gh issue create #2214 (done)
✅ 4. git checkout -b feature/issue-2214-ds-17-12-sp4-catalog (done)

Stage 0 BGG cleanup audit (conditional, ~15 min):
5. Grep BGG references in sp4-catalog stems
6. If found: atomic commit + extend #2151
7. If clean: skip Stage 0

Stage 1 sp4-library-mobile forward-refactor (~2h):
8. Agent dispatch — scaffold story + designer review tracking issue
9. Update sp4-library-mobile.fidelity.json with obsolete_tracking_issue
10. Commit feat(stories): #2214 sp4-library-mobile forward-refactor

Stage 2 sp4-add-game-bgg-step skip (~5 min):
11. Update sp4-add-game-bgg-step.fidelity.json (closure note per design_intent: forward-refactor-obsolete already labeled)
12. Document skip in PR body
13. Commit chore(mockups): #2214 sp4-add-game-bgg-step skip closure note

Stage 3 standard stems hybrid (~6-7h):
14. Verify component-mock stem paths (sp4-game-chat-tab + sp4-citation-pdf-viewer)
15. Agent dispatch sp4-game-detail (POST-#2096 wire verify)
16. Inline batch 10 standard stems (library-desktop + library-wishlist + add-game-drawer + add-game-pdf-dedup + games-index + agents-index + agent-detail + game-chat-tab + citation-pdf-viewer + discover + upload-wizard-extended)
17. Commit feat(stories): #2214 sp4 catalog cluster N stems story migration

Stage 4 quality gates (~30 min):
18. pnpm test + lint + lint:tokens + lint:bgg + lint:fidelity + mockup-annotations:audit + typecheck

Stage 5 merge + closure (~30 min):
19. git push -u origin feature/issue-2214-ds-17-12-sp4-catalog
20. gh pr create --base main-dev
21. Designer review SKIP (Opzione C precedent)
22. gh pr merge --admin --squash --delete-branch (P145 39a)
23. gh issue close #2214 + EPIC #2063 Phase C-2 step 1/4 progress
24. Memory entry ds-17-12-sp4-catalog-shipped.md
```

## 6. Effort recap

| Stage | Effort |
|---|---|
| Pre-flight + sub-issue + branch | ✅ done (~15 min) |
| Stage 0 BGG audit (conditional) | ~15 min |
| Stage 1 sp4-library-mobile forward-refactor | ~2h |
| Stage 2 sp4-add-game-bgg-step skip | ~5 min |
| Stage 3 12 standard stems (1 Agent + 11 inline) | ~7h |
| Stage 4 quality gates | ~30 min |
| Stage 5 merge + closure + memory | ~30 min |
| **Total DS-17-12 active work** | **~10-11h** (within ~12-14h cap) |

## 7. Risk register

| # | Risk | Mitigation |
|---|---|---|
| R1 | sp4-game-detail POST-#2096 deliverables wire issues | Agent dispatch + verify MSW fixtures match LibraryGameDetail interface (PR #2207 shipped, fresh in main-dev) |
| R2 | sp4-library-mobile forward-refactor designer rejects | Tracking issue + PR body annotation (sp3-library-public precedent) |
| R3 | Stem count drift (14 actual vs 13 estimated) | Confirmed via `ls admin-mockups/design_files/sp4-*.html` filter. 14 finalizzato. |
| R4 | DS-17-12 effort overrun >14h | Skeleton-first fallback per heavy stems; defer single complex stem to follow-up |
| R5 | Component-mock stem path discovery | Pre-execution grep to identify actual component file location |
| R6 | Routes existence check fail | Escalate to user (route-create out of scope per DS-17-12) |
| R7 | Phase C-2 sequencing assumption (DS-17-12 first) | If priority shift requested, re-brainstorm at sub-issue level |

## 8. Out of scope (explicit)

- ❌ DS-17-13 + DS-17-14 + DS-17-15 detail (future brainstorming per DEC-2)
- ❌ Per-game session stories (Catan/Codenames/etc.) — deferred Phase C-3 per DEC-3
- ❌ Baseline capture (P252 defer pattern — non-blocking per merge)
- ❌ EPIC #2096 deliverables re-implementation (sp4-game-detail uses existing components)
- ❌ Route-create (DS-17-12 scope assume routes esistono; escalate if MISSING)
- ❌ Mobile viewports beyond sp4-library-mobile (DEC viewports: desktop only across sp4 stems per fidelity.json)
- ❌ Backend changes (pure FE work)

## 9. References

| Type | Path / Link |
|---|---|
| Sub-issue | [#2214](https://github.com/meepleAi-app/meepleai-monorepo/issues/2214) |
| Parent umbrella | [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) DS-17 Mockup-to-App Fidelity |
| Phase C-1 closure spec | `docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md` |
| Predecessor cluster (sp3) | `docs/superpowers/specs/2026-06-11-ds-17-10-sp3-cluster-design.md` |
| Predecessor PRs | #2164 + #2173 + #2211 |
| BGG ToS umbrella | #2151 |
| Memory note | `~/.claude/projects/.../memory/ds-17-10-sp3-cluster-shipped.md` (P251-P254 patterns) |
| Mockup files | `admin-mockups/design_files/sp4-{library-*,add-game-*,games-*,game-detail,agents-*,agent-detail,game-chat-tab,citation-*,discover,upload-wizard-*}.{html,jsx,fidelity.json}` |
| Mockup index | `admin-mockups/MOCKUPS_INDEX.md` |
| EPIC #2096 closure trigger | PR #2207 `b98e4328b` (M6+M4 final closure sess.46p) |

## 10. Acceptance criteria (sub-issue ready, mirrored in #2214 body)

### Stage 0 BGG cleanup
- [ ] BGG references audit done (grep sp4-catalog stems)
- [ ] If found: atomic commit + #2151 extended
- [ ] If clean: documented skip in PR body

### Stage 1 sp4-library-mobile
- [ ] Story file created (scaffold base)
- [ ] Designer review tracking issue OPENED
- [ ] fidelity.json updated with `obsolete_tracking_issue: "#<NEW_TRACKING_ISSUE_NUM>"`

### Stage 2 sp4-add-game-bgg-step
- [ ] No story file created (skip)
- [ ] fidelity.json closure note added
- [ ] PR body documents skip rationale

### Stage 3 12 standard stems
- [ ] 12 stories created
- [ ] sp4-game-detail POST-#2096 wire verified (Agent dispatch)
- [ ] 11 inline batch stems scaffolded
- [ ] Component-mock stem paths discovered + scaffolded (sp4-game-chat-tab + sp4-citation-pdf-viewer)

### Stage 4 quality gates
- [ ] `pnpm test` → 0 regression
- [ ] `pnpm lint` → 0 errors
- [ ] `pnpm lint:tokens` → 0 violations
- [ ] `pnpm lint:bgg` → clean
- [ ] `pnpm lint:fidelity` → all PASS
- [ ] `pnpm typecheck` → 0 errors
- [ ] `pnpm mockup-annotations:audit` → ≥80% mappable

### Stage 5 closure
- [ ] Admin-squash merge P145 39a volta
- [ ] Sub-issue #2214 closed con AC evidence
- [ ] EPIC #2063 (DS-17 umbrella) progress Phase C-2 step 1/4 complete
- [ ] Memory entry `ds-17-12-sp4-catalog-shipped.md`
- [ ] DS-17-13 sp4-core-content brainstorm trigger noted

## 11. Acceptance criteria (META Phase C-2)

- [ ] DS-17-12 catalog shipped (this sub-issue)
- [ ] DS-17-13 content brainstormed + shipped (future)
- [ ] DS-17-14 admin brainstormed + shipped (future)
- [ ] DS-17-15 sessions skeleton-first brainstormed + shipped (future)
- [ ] EPIC #2063 Phase C-2 4/4 complete (Phase D + Phase C-3 follow-up NEXT)

---

**End of design spec.**
