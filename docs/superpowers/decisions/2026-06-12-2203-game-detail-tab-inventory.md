# Decision: Game Detail Tab Inventory Canonical (#2203)

**Status**: Draft — awaiting product + tech lead sign-off
**Created**: 2026-06-12
**Decider**: Product Owner + Tech Lead (synchronous decision)
**Sub-issue of**: #2194 umbrella (game detail tab orphan routes)
**Track**: D — Architecture (post Track A+C delivered)
**Blocks**: #2204 (implementation) — cannot start without this decision

---

## Context

`/games/{id}` renders a 7-tab UI (`Info / Regole / FAQ / Partite / pages.gameDetail.tabs.stats / Agenti / Documenti`) but the filesystem routes diverge:

| Tab UI | Tab label | Filesystem route | State |
|---|---|---|---|
| Info | `/page.tsx` | exists | ✅ wired |
| Regole | `/rules/page.tsx` | exists | ✅ wired |
| FAQ | `/faqs/page.tsx` | exists | ✅ wired |
| Partite | `/sessions/page.tsx` | exists | UI disabled with 🔒 |
| Statistiche | — | — | UI disabled with 🔒 + **i18n broken** raw key shown |
| Agenti | — | — | UI tab but no route |
| Documenti | — | — | UI tab but no route |

**Routes WITHOUT tab UI** (orphan):
- `/reviews/page.tsx` ← scaffolded by commit `0881856df` (nav-map gap fix)
- `/strategies/page.tsx` ← same commit
- `/chat/page.tsx` ← scaffolded by `54c504920` (#1411 nav-map gap analysis)

Backend endpoints `/api/v1/games/{id}/reviews` + `/api/v1/games/{id}/strategies` return **404** (audited 2026-06-11). `/chat` route redirects to `/games/{id}` (wrapper trivial).

→ 3 routes that exist on disk but are **unreachable from the UI shipped** and **unsupported by backend**.

---

## Analytics signal (codebase surrogate)

We don't have production analytics in this session, but the codebase tells us:

| Signal | Value |
|---|---|
| Inbound `<Link href>` references to `/games/{id}/reviews` | **0** |
| Inbound `router.push('/games/{id}/strategies')` | **0** |
| Inbound `router.push('/games/{id}/chat')` | **0** |
| Inbound `<Link href>` to `/games/{id}/agents` or `/documents` (UI tabs) | **0** routes exist |
| E2E coverage for the orphan routes | **0** specs (5 exist for `game-detail`, all cover `Info`/`rules`/`faqs`/`Partite`) |
| Git history | Both routes scaffolded by **gap-fix commits**, never wired into nav |

**Surrogate interpretation**: organic traffic to the orphan routes is **near-zero** (only direct URL access by curious users or stale bookmarks from prior nav-map iterations). Removal risk is low.

---

## Options recap (from #2203 issue body)

### Opzione A — Allineare tab UI with filesystem routes (route-driven)

7 tab canonical: `Info / Regole / FAQ / Recensioni / Sessioni / Strategie / Chat`

- ✅ Uses existing route scaffolds (Reviews/Strategies/Chat shipped as page files)
- ❌ Requires NEW backend endpoints `/reviews` + `/strategies` (currently 404 — see #2195)
- ❌ Removes UI tabs Agenti + Documenti (or requires NEW routes for them)
- ❌ Breaks muscle memory of users on the current 7-tab UI
- ❌ Mockup `sp4-game-detail.html` covers 2/7 tabs → 5 NEW mockups needed (Draft 11, #2198)
- 📅 Effort: **L (7-10 dev-days)** + designer time for 5 mockups

### Opzione B — Allineare filesystem routes with current UI (UI-driven)

7 tab canonical: `Info / Regole / FAQ / Partite / Statistiche / Agenti / Documenti`

- ✅ Preserves muscle memory of current users
- ✅ No new backend endpoints required (404s eliminated by removing the FE pages)
- ✅ Simplest implementation: delete 3 orphan pages + create `Agenti`/`Documenti` routes
- ❌ Loses Reviews/Strategies/Chat features (community commentary, strategy hub, in-game chat)
- ❌ Bookmarks to orphan routes (zero or near-zero per analytics surrogate) return 404
- ❌ Mockup parity: `Agenti`/`Documenti` mockups need to exist (verify status)
- 📅 Effort: **M (3-5 dev-days)** + minimal designer time

### Opzione C — Hybrid: 5 primary + dropdown "Altro"

5 primary: `Info / Regole / FAQ / Sessioni / Agenti`
Dropdown "Altro": `Recensioni / Strategie / Statistiche / Documenti / Chat`

- ✅ Preserves all features
- ❌ Discoverability low for dropdown items (vs primary tabs)
- ❌ Pattern unusual on game detail (not a standard ShadcN UI pattern)
- ❌ Doubles maintenance surface (tab list + dropdown config)
- ❌ Still needs backend `/reviews` + `/strategies` endpoints
- 📅 Effort: **M-L (5-7 dev-days)** + dropdown UX work + 5 mockups

---

## Recommendation

**Locked recommendation: Opzione B** (UI-driven, remove orphan routes)

**Reasoning**:
1. **Codebase signal**: 0 inbound references and 0 E2E coverage on orphan routes confirms they ship dead. Removal is low-risk.
2. **Backend gap**: BE `/reviews` + `/strategies` endpoints don't exist (#2195). Options A and C would require BE work that's NOT in any current roadmap.
3. **Mockup gap**: 5 sub-tab mockups missing for Reviews/Strategies/Chat (#2198 Draft 11) — Options A and C are blocked on designer commission.
4. **Smallest delta**: Option B can ship in this delivery cycle. Options A and C span 2-3 PRs across designer + backend + frontend.
5. **Reversibility**: If product later wants Reviews/Strategies, we re-add the route + BE endpoint + mockup in a future iteration. Removing now does NOT preclude adding later.

**Out of scope of this decision (but related)**:
- Whether `/agents` (separate route shipped earlier) should integrate as a `/games/{id}/agents` tab or stay as a global hub
- Documents tab semantics: pure file list vs RAG-aware preview

---

## Decision required (synchronous meeting)

| Question | Owner | Default |
|---|---|---|
| Lock Opzione A, B, or C? | Product + Tech Lead | **B** (per recommendation) |
| If B: keep "Agenti" tab as UI-only OR create `/agents` route under `/games/{id}/`? | Product | UI-only initially |
| If B: keep "Documenti" tab as UI-only OR create `/documents` route? | Product | UI-only initially |
| Implementation owner | Tech Lead | TBD |
| Deprecation phase needed for orphan routes? | Tech Lead | No (0 organic traffic surrogate) |
| Mockup commission needed (#2198 Draft 11)? | Designer | Cancel if B locked |

---

## Meeting agenda (~30 min synchronous)

1. **5 min — Context recap**: presenter walks through this doc § Context + § Analytics signal
2. **10 min — Option discussion**: Q&A on A/B/C tradeoffs
3. **10 min — Lock decision**: vote / consensus on A/B/C + answer 4 secondary questions
4. **5 min — Action items**: assign #2204 implementation owner, close #2198 if B locked, plan ADR commit

---

## ADR draft skeleton (post meeting)

To be committed at `docs/for-claude/architecture/adr/adr-N-game-detail-tab-canonical.md` after sign-off:

```markdown
# ADR-N: Game Detail Tab Inventory Canonical

**Status**: Accepted
**Date**: 2026-MM-DD
**Deciders**: <Product Owner> + <Tech Lead>
**Issue**: #2203 (sub-issue of #2194 umbrella)

## Context

[Copy § Context + § Analytics signal from this doc]

## Decision

We adopt **Opzione <A/B/C>** for the canonical game detail tab inventory:

[List of 7 canonical tabs]

## Consequences

### Positive
- [list]

### Negative
- [list, including any tradeoffs accepted]

### Neutral
- [list]

## Implementation plan

See sister issue #2204.

## Related

- Sister: #2204 (implementation)
- Sister: #2205 (i18n cleanup, parallel)
- Closes by removal: #2198 (if B locked)
- Closes by removal: #2195 (if B locked — BE endpoints no longer needed)
```

---

## Refs

- Umbrella: #2194 (game detail tab nav orphan routes)
- Sub-issue 1: #2203 (this decision — BLOCKING)
- Sub-issue 2: #2204 (implementation — blocked by this)
- Sub-issue 3: #2205 (i18n cleanup — parallel, can ship now)
- Sister BE: #2195 (404 endpoints, scope depends on this decision)
- Sister UX: #2197 (back link routing, tooltip)
- Sister Mockup: #2198 (Draft 11 5 sub-tab mockups, scope depends on this decision)
- Spec-panel critique: comment 4682309758 on #2194 (Wiegers/Nygard/Fowler/Newman/Adzic)
- Track D delivery: `docs/superpowers/plans/2026-06-11-p0-delivery-plan.md`
