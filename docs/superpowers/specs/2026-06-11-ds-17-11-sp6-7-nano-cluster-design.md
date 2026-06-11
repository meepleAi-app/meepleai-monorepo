# DS-17-11 sp6-7-nano Cluster — Sub-issue Design (Phase C-1 step 2 revised)

**Date**: 2026-06-11 (sess.46o brainstorming)
**Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) — DS-17 Mockup-to-App Fidelity
**Parent spec**: [`2026-06-11-ds-17-phase-c-pilot-migration-design.md`](./2026-06-11-ds-17-phase-c-pilot-migration-design.md) (DEC-Pilot-1..8)
**Predecessor**: DS-17-9 auth ✅ SHIPPED PR #2164 `85d3ccfc8` (sess.46n)
**Successor**: DS-17-10 sp3 ⏳ DEFERRED post-#2096 closure (see [[ds-17-10-sp3-deferred-decisions]] memory note)

---

## Why this sub-issue is second (not third per original plan)

Originale DEC-Pilot-3 v1 sequence `auth → sp3 → sp6-7-nano` revised in DEC-Pilot-3 v2 (sess.46o) → `auth → sp6-7-nano → sp3 (post-#2096)`.

**Trigger**: `sp3-shared-game-detail` mockup è target di EPIC [#2096](https://github.com/meepleAi-app/meepleai-monorepo/issues/2096) `/library/[gameId] sp3 mockup rebuild — 6 milestone` (WIP 12-16h, GameHero v2 + animated tabs + ConnectionBar). Snapshot baseline catturato durante DS-17-10 ship rischierebbe rebaseline forzata appena #2096 merge.

**User decision** (sess.46o): block DS-17-10 fino a #2096 closure, advance DS-17-11 sp6-7-nano first.

---

## Scope finale (10 unique stems → 4 ship + 6 skip)

### Ship (4 stems → ~4 stories, ~20-28 baseline PNGs)

| Stem | Route (existing) | Frame count est. | Notes |
|---|---|---|---|
| `sp7-game-night-create` | `apps/web/src/app/(authenticated)/game-nights/new/page.tsx` | ~5-7 | Wizard form |
| `sp7-game-night-detail-rsvp` | `apps/web/src/app/(authenticated)/game-nights/[id]/page.tsx` | ~5-7 | RSVP flow + states |
| `sp7-game-night-live` | `apps/web/src/app/(authenticated)/game-nights/[id]/live/page.tsx` | ~5-7 | Live session in-flight (post BGG cleanup) |
| `sp7-game-night-summary` | `apps/web/src/app/(authenticated)/game-nights/[id]/summary/page.tsx` | ~5-7 | Post-completion summary |

### Skip (6 stems — forward-refactor route MISSING)

| Stem | Reason | Tracking |
|---|---|---|
| `sp7-game-night-transition` | Route `/game-nights/[id]/transition` MISSING | Phase D tracking issue (raggruppato) |
| `sp7-game-night-join-public` (JSX-only, no HTML twin) | Route `/game-nights/join/[code]` (public) MISSING | Phase D tracking issue (raggruppato) |
| `sp6-libro-game-index` | Route `/libro-game/*` MISSING completamente | Phase D tracking issue raggruppato libro-game ecosystem |
| `sp6-libro-game-resume-state` | Idem libro-game ecosystem | Idem |
| `sp6-libro-game-photo-upload` (BGG HIGH severity) | Idem + BGG ecosystem refactor required | Idem (BGG cleanup mockup still required per DEC-Pilot-7) |
| `sp6-libro-game-quota-credits` (JSX-only) | Idem libro-game ecosystem | Idem |

= **1 tracking issue raggruppato** `[DS-17 Phase D] sp6-7-nano forward-refactor — libro-game ecosystem + game-night gap stems implementation`. 6 stems → defer Phase D dedicata.

---

## DEC-Pilot-7 — BGG cleanup pre-AI dispatch (Stage 0)

2 mockup con BGG references richiedono edit pre-Stage 1:

### `sp6-libro-game-photo-upload.{html,jsx}` — SEVERITY HIGH

UI funzionale violazione ToS (#1903 ADR + #2151):
- Tab "🌐 BGG" attivo in search switcher (Catalogo + BGG tabs)
- Loading state `Step1_BGGLoading` con "Cerco su BoardGameGeek…"
- Hardcoded `BGG_RESULTS` array (`bgg-andor` etc.)
- State `1-bgg-loading` in wizard step enumeration
- "Cerca su BoardGameGeek" action card
- "Nessun risultato nel catalogo condiviso. Cerca su BGG ↑"

**Edit scope**:
- Rimuovi BGG tab from `pu-search-tab` markup (both HTML + JSX)
- Rimuovi `Step1_BGGLoading` function + state `1-bgg-loading` from STATES enumeration
- Rimuovi `BGG_RESULTS` array data
- Rimuovi "Cerca su BoardGameGeek" action card from `Step1_NoResults`
- Update fallback text "Cerca su BGG ↑" → "Suggerisci via /contact"

**Note**: mockup STAYS skipped per DEC-Pilot-8 (no route), ma admin-mockups hygiene impone cleanup per evitare drift Phase B re-classification ambiguity.

### `sp7-game-night-live.jsx:561` — SEVERITY MEDIUM (already in #2151 flag list)

Add-game CTA navigates to forbidden mockup (`sp4-add-game-bgg-step.html` retired per #1903).

**Edit scope**:
- Replace CTA target → internal catalog quick-add flow (no BGG)
- Verify HTML twin (`sp7-game-night-live.html`) clean (Phase B audit suggests yes)

### Commit + extend #2151

```
chore(mockups): #DS17-11 BGG removal sp6-7-nano cluster

- sp6-libro-game-photo-upload.{html,jsx}: rimosso tab BGG + loading state + results array + action card (Phase B miss esteso)
- sp7-game-night-live.jsx:561: replace BGG CTA target → catalog flow

Extends #2151 ToS compliance scope.

Refs: #1903 BGG ToS ADR
```

Comment on #2151 con 1 nuovo finding (sp6-libro-game-photo-upload era Phase B miss).

---

## Components reused (no new patterns)

All components inherit from parent spec § Components:

| # | Component | Reuse from |
|---|---|---|
| 1 | Cluster Scaffold Generator | DS-17-9 auth (Task 5 pattern, 1 Agent dispatch per cluster) |
| 2 | Story file pattern | Phase 2.5 Library + GameDetail pilots, mirror title prefix `Pages/SP6/<Name>` o `Pages/SP7/<Name>` (DS-17-9 code-reviewer Finding 1) |
| 3 | Fixture file pattern | `apps/web/src/__tests__/fixtures/mockup-pilots/sp6-7-nano/<stem>.ts` |
| 4 | Snapshot spec | `apps/web/e2e/storybook/sp6-7-nano.snapshot.spec.ts` (FRAMES array derived from committed stories per code-reviewer Finding 5) |
| 5 | Designer review queue generator | `generate-cluster-review-queue.mjs` shipped DS-17-9 (no new TDD) |
| 6 | Human iteration checklist | DS-17-9 7-step per mockup |

---

## Title convention

Mirror Phase 2.5 (`Pages/SP4/Library Mockup Matrix`) + DS-17-9 (`Pages/Auth/<Name>`):

- `sp7-*` mockups → `Pages/SP7/<Name>`
  - Es. `Pages/SP7/Game Night Create` → slug `pages-sp7-game-night-create`
- `sp6-*` mockups → `Pages/SP6/<Name>` (libro-game tutti skipped, no slug practical impact)

---

## Routes pre-check (Code-reviewer Finding 4 pattern)

```bash
# Verify routes exist BEFORE constructing cluster JSON
for stem in sp7-game-night-create sp7-game-night-detail-rsvp sp7-game-night-live sp7-game-night-summary; do
  case $stem in
    sp7-game-night-create) route="apps/web/src/app/(authenticated)/game-nights/new/page.tsx" ;;
    sp7-game-night-detail-rsvp) route="apps/web/src/app/(authenticated)/game-nights/[id]/page.tsx" ;;
    sp7-game-night-live) route="apps/web/src/app/(authenticated)/game-nights/[id]/live/page.tsx" ;;
    sp7-game-night-summary) route="apps/web/src/app/(authenticated)/game-nights/[id]/summary/page.tsx" ;;
  esac
  [ -f "$route" ] && echo "✓ $stem → $route" || echo "✗ MISSING $stem → $route"
done
```

Expected: 4/4 ✓ (verified sess.46o discovery).

---

## Data flow

```
[admin-mockups/design_files/sp7-game-night-{create,detail-rsvp,live,summary}.{html,jsx}]
[admin-mockups/design_files/sp6-libro-game-photo-upload.{html,jsx}]  ← BGG cleanup ONLY (skip ship)
[admin-mockups/design_files/sp7-game-night-live.{jsx}]                 ← BGG cleanup + ship story
        │
        ▼
┌─── Stage 0: BGG cleanup prep ───────────────────────────────────┐
│  1. Edit 2 mockup files removing BGG refs                        │
│  2. Commit chore mockup cleanup                                  │
│  3. Comment on #2151 extending findings                          │
└─────────────────────────────────────────────────────────────────┘
        │
        ▼
[audits/c1-sp6-7-nano-cluster.json] ← cluster subset (4 ship + 6 skip annotated)
        │
        ▼
┌─── Stage 1: AI Pre-flight Scaffold Generator ──────────────────┐
│  1. Filter audit JSON for 4 ship stems (skip 6 forward-refactor)│
│  2. Dispatch 1 Agent(general-purpose):                          │
│     - Read each ship mockup HTML+JSX twin                       │
│     - Cross-ref game-nights/<route>/page.tsx                    │
│     - Identify axis + frames                                     │
│     - Emit 4 scaffold dirs (1 per ship stem × 4 file)           │
│  Output: scaffolds/sp6-7-nano/<stem>/                            │
└─────────────────────────────────────────────────────────────────┘
        │
        ▼
[git commit "chore(stories): #NNNN sp6-7-nano scaffolds (AI pre-flight)"]
        │
        ▼
┌─── Stage 2: Human iteration (1 story at a time) ────────────────┐
│  For each of 4 ship mockups:                                     │
│    1. Read scaffold drafts                                        │
│    2. Refine story + fixture                                      │
│    3. pnpm storybook → verify renders                            │
│    4. pnpm test:storybook:snapshots:update                        │
│    5. git commit "feat(stories): #NNNN <name>"                  │
└─────────────────────────────────────────────────────────────────┘
        │
        ▼
┌─── Stage 3: Cluster integration ────────────────────────────────┐
│    1. Update sp6-7-nano.snapshot.spec.ts FRAMES array            │
│    2. pnpm lint:fidelity (verify refs)                            │
│    3. pnpm typecheck + pnpm lint                                  │
│    4. Update fidelity.json story_path + fixtures_path             │
│    5. Generate designer review queue (4 shipped + 6 skipped)      │
│    6. Open Phase D tracking issue raggruppato 6 forward-refactor │
│    7. rm -rf scaffolds/sp6-7-nano/                                │
│    8. git commit "chore(stories): #NNNN finalize cluster"        │
└─────────────────────────────────────────────────────────────────┘
        │
        ▼
[PR + admin-squash merge → main-dev]
        │
        ▼
[Umbrella body update + trigger DS-17-10 sp3 reactivation check (post-#2096)]
```

---

## Error handling

All error modes inherit parent spec § Error handling. Sub-issue-specific additions:

| Failure mode | Handling |
|---|---|
| BGG cleanup edit breaks mockup syntax | Run mockup in browser standalone (`open admin-mockups/design_files/sp6-libro-game-photo-upload.html`) → fix syntax → re-commit cleanup |
| Stage 1 AI scaffolds mistakenly include skipped stem | Verify scaffold dir matches 4-stem ship list; if extra → manual rm; document in `_skipped.md` |
| Stage 2 story import path wrong (route not exist for skipped stem) | Confirm stem is in ship list (`sp7-game-night-{create,detail-rsvp,live,summary}`); if not in list, abort story creation |
| Designer queue forward-refactor section empty (skip list mismatch) | Verify `audits/c1-sp6-7-nano-cluster.json` `classifications` includes all 6 skip stems with proper `design_intent` flag |
| #2096 unexpectedly closes during DS-17-11 execution | Phase C-1 parent sequence allows DS-17-10 sp3 immediate activation post-DS-17-11 merge (parallel close acceptable) |

---

## Testing strategy

Inherits parent spec § Testing strategy:

- Unit: `generate-cluster-review-queue.mjs` (already shipped + tested DS-17-9, no new tests)
- Integration: Playwright snapshot specs per cluster (sp6-7-nano.snapshot.spec.ts)
- Smoke: human story render verify in `pnpm storybook`
- Regression: diagnostic.snapshot.spec.ts STORIES array extended with 1 sp6-7-nano sample slug
- Manual: 3 random shipped stories designer queue verify + 1 BGG cleanup mockup verify clean

---

## Acceptance criteria

- [ ] DS-17-11 sub-issue created on GitHub
- [ ] 2 BGG cleanup commits (`sp6-libro-game-photo-upload` + `sp7-game-night-live`) + #2151 comment extension
- [ ] 4 stories committed under `apps/web/src/app/(authenticated)/game-nights/<route>/<component>.stories.tsx`
- [ ] 4 fixture files under `apps/web/src/__tests__/fixtures/mockup-pilots/sp6-7-nano/`
- [ ] 1 snapshot spec `apps/web/e2e/storybook/sp6-7-nano.snapshot.spec.ts`
- [ ] ~20-28 baseline PNGs captured
- [ ] `pnpm test:storybook:snapshots` passes
- [ ] `pnpm lint:fidelity` + typecheck + lint clean
- [ ] Designer queue `docs/for-developers/frontend/c1-sp6-7-nano-review-queue.md` published with 4 shipped + 6 skipped sections
- [ ] 1 Phase D tracking issue opened (libro-game ecosystem + game-night gap stems implementation)
- [ ] Diagnostic spec STORIES array extended with 1 sp6-7-nano sample
- [ ] Umbrella #2063 body updated with sp6-7-nano cluster row
- [ ] `scaffolds/sp6-7-nano/` deleted post-consume

---

## Out of scope

- 6 forward-refactor skipped stems story migration → Phase D tracking issue (raggruppato)
- `sp7-game-night-edit` mockup (commission gap per #2026) → not in audit, no story possible
- `sp6-libro-game-house-rule` mockup (per #2027) → not in audit, no story possible
- DS-17-10 sp3 sub-issue → DEFERRED post-#2096 closure (see [[ds-17-10-sp3-deferred-decisions]])
- CI snapshot gate flip → deferred Phase C-2 completion per DEC-Pilot-5
- `sp6-libro-game-photo-upload` story migration → BGG mockup cleanup only, story shipping deferred Phase D (no route)
- Phase B tracking issues addressing (#2138-2153) → separate workstreams

---

## Effort estimate (revised vs originale plan template)

| Phase | Originale (per plan template DS-17-11) | Revised (DEC-Pilot v2/7/8) |
|---|---|---|
| Stage 0 BGG cleanup prep | N/A | +0.5gg |
| Stage 1 AI pre-flight | ~30min compute | ~30min compute |
| Stage 2 Human iteration | ~10 stems × 0.5gg = ~5gg | ~4 stems × 0.5gg = ~2gg |
| Stage 3 Cluster integration | ~0.5gg | ~0.5gg |
| Stage 4 PR + merge | ~0.5gg | ~0.5gg |
| **Total** | **~7-9gg** | **~3.5-4.5gg (~50% reduction)** |

Phase C-1 timeline impact: DS-17-11 ~3.5-4.5gg + DS-17-10 ~3-4gg post-#2096 = ~7-9gg total (~50% reduction su DS-17-11 vs originale, compensa #2096 wait timing).

---

## References

- Parent umbrella: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
- Parent spec: [`2026-06-11-ds-17-phase-c-pilot-migration-design.md`](./2026-06-11-ds-17-phase-c-pilot-migration-design.md)
- Phase 2.5 pilot pattern: `docs/for-developers/frontend/page-mock-story-pattern.md`
- Phase 4 prelude (Storybook provider wiring): `docs/superpowers/specs/2026-06-10-ds-17-phase-4-prelude-intl-hardening-design.md` (PR #2124 sess.46m)
- Phase B audit: `audits/2026-06-10-mockup-design-intent-audit.json` (byCluster.sp6-7-nano)
- BGG ToS umbrella: #2151
- BGG ADR: #1903
- EPIC blocking DS-17-10 sp3: #2096 `/library/[gameId] sp3 mockup rebuild — 6 milestone`
- Sibling shipped (predecessor): DS-17-9 auth PR #2164 `85d3ccfc8` sess.46n
- Sibling deferred: DS-17-10 sp3 (memory note `ds-17-10-sp3-deferred-decisions.md`)
- Companion tracking issues (out of scope, follow-up): #2026 sp7-game-night-edit, #2027 sp6-libro-game-house-rule, #2028 sp7-notifications
