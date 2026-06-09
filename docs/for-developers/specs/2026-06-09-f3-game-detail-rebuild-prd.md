# F3 Game Detail Full Rebuild — PRD Draft

**Issue:** #2010 (follow-up of umbrella #1974)
**Status:** **DRAFT** — questions open for product. NOT actionable until product + design responses.
**Date:** 2026-06-09
**Author:** Claude Code (draft synthesis)

> This document captures the spec-panel synthesis from #1974 audit (2026-06-07, Wiegers + Fowler + Newman + Cockburn) as a set of explicit open questions. It does NOT prescribe answers. The F3 full-rebuild work stays parked until the questions in §3 are resolved.

---

## 1. Background & scope summary

### 1.1 What "F3 full rebuild" means

The `/library/[gameId]` route currently ships a 4-tab page (`Dettagli` · `Agente` · `Toolkit` · `FAQ`) with a hero card + ConnectionBar + tab panel. The SP4 mockup at `admin-mockups/design_files/sp4-game-detail.jsx` (1163+ lines) shows a substantially richer surface:

- 6-tab navigation (the 4 above + `Recensioni` + `Dischi`)
- Hero illustration + meta strip (designer · year · duration · players · complexity · rating) — *partially shipped*
- Descrizione · Specifiche · House-Rules · Documenti sections inside the Info tab — *partially shipped*
- Inline agent chat embedded in the page (vs current modal/drawer)
- Sessioni storiche timeline

### 1.2 What has shipped already

The umbrella autonomous-loop work on #1974 landed **two partial cuts** of F3:

- **PR #2005** — Hero meta strip extended with designer + complexity entries
- **PR #2008** — Info-tab Descrizione section + Designer / Categorie / Meccaniche rows

Additionally, the 2026-06-09 follow-ups in this session shipped:
- **PR #2050** — Hero SP3 mockup parity (entity-badge pill, animated tabs underline, designers field on `/library/{id}`)
- **PR #2054** — `ConnectionBar` pill counts (`agentCount` + `chatThreadCount`)
- **PR #2059** — `SessionContributorsStrip` avatar component

### 1.3 What this PRD is for

To close the gap between the partial cuts already shipped and the full SP4 rebuild we need product + design alignment on **four blockers** (§3). Until those clear, F3 stays parked. This PRD enumerates the questions, sketches preliminary recommendations the team can refute or ratify, and lays out a phased implementation roadmap conditional on the answers.

---

## 2. Stakeholders & primary actors

Per Cockburn's primary-actor analysis:

| Surface | Primary actor | Job-to-be-done |
|---|---|---|
| Existing tabs (Dettagli / Agente / Toolkit / FAQ) | Library owner | Manage owned game + use AI assistant for it |
| `Recensioni` (NEW) | Library owner OR community user | "Tell me if this game is worth playing / record my opinion" |
| `Dischi` (AMBIGUOUS) | TBD per §3.2 | TBD per §3.2 |
| `Documenti` (RISK OF DUPLICATION) | Library owner | View/manage PDFs linked to the game |
| Sessioni storiche | Library owner | "Show me my play history" |

**Open**: the `Recensioni` and `Dischi` actor + JTBD pairs are unresolved (§3.1, §3.2). Cockburn flagged: we cannot write acceptance criteria without actor + goal pairing.

---

## 3. Open questions (the four blockers)

### 3.1 — `Recensioni` tab: new feature, not a tab change

**Status:** unresolved. **Blocking:** yes.

The `Recensioni` (Reviews) tab is a *new feature*, not a re-skin. No review entity exists on the BE today. Questions for product:

| # | Question | Owner | Blocking phase |
|---|---|---|---|
| Q1.1 | What review entity ships on the BE? Schema (rating + title + body + author + game), ownership model, ratings shape (1-5 stars vs 1-10 vs thumbs)? | Backend + Product | F3.4 |
| Q1.2 | Moderation flow — auto-publish, manual approve, report-and-takedown? | Product | F3.4 |
| Q1.3 | Spam / abuse handling — rate limit per user, blocklist, abuse-reports queue? | Product + Security | F3.4 |
| Q1.4 | Visibility model — self-only / friend-only / public? Per-user-per-game uniqueness (one review per user per game) or multiple? | Product | F3.4 |
| Q1.5 | Localization — review language detection + display? Translation on read? | Product | F3.4 (Phase 2 candidate) |
| Q1.6 | Edit / delete history — keep edits visible? Soft-delete on takedown? | Product + Compliance | F3.4 |
| Q1.7 | Rating influence on game catalog — does the review's rating roll up into `SharedGame.AverageRating`, or is `Recensioni.rating` a separate axis from BGG? | Product | F3.4 |

**Preliminary recommendation (refutable)**: defer `Recensioni` to a dedicated epic (own PRD, own scope, own BE entity, own moderation tooling). Ship F3 *without* `Recensioni` to unblock the rest of the rebuild. The mockup tab can stay as a placeholder "Coming soon" panel until the epic lands.

---

### 3.2 — `Dischi` tab: semantically ambiguous

**Status:** unresolved. **Blocking:** yes.

The label "Dischi" (Records) maps to three plausible meanings, each with a different actor + JTBD:

| Meaning | Description | Where it lives today |
|---|---|---|
| **(a)** Personal play records | Records-of-play for this specific user | `/play-records` already covers (own route) |
| **(b)** Historical sessions of the game | All my sessions of this game | `Partite` tab already covers this surface |
| **(c)** Achievements / milestones | Badges, streaks, "first win", etc. | No entity exists today |

If **(a)** → drop the tab (route already exists). If **(b)** → drop or merge with `Partite`. If **(c)** → new entity, new epic.

**Questions for product:**

| # | Question | Owner | Blocking phase |
|---|---|---|---|
| Q2.1 | Which of (a) / (b) / (c) was the mockup author thinking of? | Product + Design | F3.5 |
| Q2.2 | If (c), what's the minimum-viable achievement catalog (which 3-5 events trigger badges)? | Product | F3.5 |
| Q2.3 | If (a) or (b), should we keep the label or drop the tab to avoid UX duplication? | Product + Design | F3.5 |

**Preliminary recommendation (refutable)**: drop the `Dischi` tab from F3 scope until product picks one meaning. The `Partite` tab already covers (b); `/play-records` already covers (a). If (c) is the goal, that's a new epic and shouldn't block F3.

---

### 3.3 — `Documenti` tab: risk of duplicating `/kb` route

**Status:** unresolved. **Blocking:** partial (can ship "read-only summary" cut without full resolution).

`KbHubContent` at `/library/[gameId]/kb` already owns:
- PDF list + indexing state
- Upload + delete
- Recent polish via #1987, #1996, #1997, #1998, #2000

Adding a `Documenti` tab inside `/library/[gameId]` has three viable shapes, each with trade-offs:

| Option | Description | Trade-off | Fowler / Newman lean |
|---|---|---|---|
| **A.** Duplicate editing surface in the tab | Same upload/delete UI in both routes | Two-place sync issue — Fowler vetoed | ❌ |
| **B.** Thin read-only summary, links to `/kb` for editing | Tab shows count + recent docs + "Manage in KB" link | Workable; no sync issue | ✅ recommended |
| **C.** Move the `/kb` route inside the tab | Bigger refactor; collapses 2 routes into 1 | Major URL change, navigation rewiring | △ if approved as future work |

**Questions for product + design:**

| # | Question | Owner | Blocking phase |
|---|---|---|---|
| Q3.1 | Confirm B (thin read-only) vs C (route consolidation) | Product + Design | F3.3 |
| Q3.2 | If B: what does the read-only summary surface — count? Most-recent N? Latest indexing state? | Design | F3.3 |
| Q3.3 | If C: what's the deprecation window for `/library/[gameId]/kb` deep-links (sidebar, mini-nav, external bookmarks)? | Product + Eng | F3.3 (next epic) |

**Preliminary recommendation (refutable)**: ship Option B for F3.3. Defer Option C to a follow-up route-consolidation epic if/when product wants to collapse the surfaces.

---

### 3.4 — Tab renames as breaking URL changes

**Status:** unresolved. **Blocking:** yes if renames go ahead, NO if labels-only.

Current tab IDs reach the page via `?tab=info|aiChat|toolbox|houseRules|partite`. The mockup renames:
- `aiChat → agente`
- `toolbox → toolkit`

Crispin flagged: this is a **breaking URL change disguised as a label tweak**. Affected surfaces include:

- User bookmarks (no migration possible)
- Seed scripts (e.g. dogfood snapshot)
- Playwright E2E specs (deep-link assertions)
- BE redirect rules (if any exist)
- Sidebar / mini-nav internal links (FE codebase)

**Questions:**

| # | Question | Owner | Blocking phase |
|---|---|---|---|
| Q4.1 | Are the URL IDs allowed to break for the rename, OR keep IDs and re-label only? | Product + Eng | F3.1 |
| Q4.2 | If rename: ship server-side redirect (`?tab=aiChat → ?tab=agente`) + deprecation window? How long? | Eng | F3.1 |
| Q4.3 | If rename: update seed scripts + Playwright in the same PR? | QA + Eng | F3.1 |

**Preliminary recommendation (refutable)**: keep the URL IDs stable, change only the display labels. Saves migration cost; aligns with the *spirit* of the mockup (which is a label change, not a routing change).

---

## 4. Implementation phases (conditional on §3 answers)

Each phase is gated on the relevant question being answered.

| Phase | Deliverable | Effort | Gates on |
|---|---|---|---|
| **F3.1** | Tab nav skeleton — adopt 6 slots (current 4 + Recensioni placeholder + Dischi placeholder OR drop). URL stability or rename plan applied. | ~3-5h | Q4.* + Q2.* |
| **F3.2** | House Rules section/tab promotion. Source-of-truth alignment with `AgentMemory` BC. | ~3h | (none blocking — already known) |
| **F3.3** | Documenti section/tab — Option B (read-only summary linking to `/kb`) | ~3-4h | Q3.* (confirm Option B) |
| **F3.4** | Recensioni tab — BE entity + moderation + FE list + write/edit/delete + spam controls | ~12-20h (own epic) | Q1.* all |
| **F3.5** | Dischi tab — implementation depends entirely on §3.2 outcome | 0h (drop) — 12-20h (achievements epic) | Q2.* |
| **F3.6** | Inline agent chat embedded in page | ~4-6h | (consider Q4 if route changes; otherwise unblocked) |

**Approach**: ship F3.1 + F3.2 + F3.3 + F3.6 as the "F3 core rebuild" once Q4 is resolved. Carve out F3.4 (Recensioni) and F3.5 (Dischi if-(c)) as dedicated downstream epics. This unblocks the bulk of the visible re-skin without waiting on the high-cost product decisions.

---

## 5. Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| URL rename breaks bookmarks + Playwright | High (if Q4.1 → rename) | High | Server-side redirect + 90-day deprecation banner |
| `Documenti` duplicates `/kb` editing logic | High (if Q3.1 → Option A) | High (data-sync bugs) | Reject Option A; pick B or C |
| `Recensioni` ships without moderation pipeline → spam | Med | High | Q1.2 + Q1.3 must be answered before merging FE |
| `Dischi` ships as "Coming soon" placeholder indefinitely | Med | Low (UX clutter) | Drop from F3.1 if Q2.1 unanswered after 2 weeks |
| Mockup drifts from shipped surface in next iteration | Med | Med | Document this PRD's gating questions in the mockup-tracking matrix |

---

## 6. Acceptance for *this* PRD (not the rebuild itself)

Per #2010 issue body, this PRD lands when:

- [x] Open questions for product enumerated (§3) — **5 Q1 + 3 Q2 + 3 Q3 + 3 Q4 = 14 questions**
- [x] Preliminary recommendations stated for each question so product can refute or ratify
- [x] Implementation phases re-scoped based on the gating structure (§4)
- [x] Risk register established (§5)
- [ ] **Product responses recorded** — pending
- [ ] **Multi-PR breakdown finalised** — depends on product responses

This PRD's deliverable is the structured set of questions + recommendations; the *answers* are explicitly out of scope until product weighs in.

---

## 7. References

- Umbrella issue: [#1974](https://github.com/meepleAi-app/meepleai-monorepo/issues/1974)
- Source mockup: `admin-mockups/design_files/sp4-game-detail.jsx` (1163+ lines)
- Audit tracker: `claudedocs/2026-06-07-reskin-verification.md`
- Partial cuts already shipped: PR #2005, PR #2008, PR #2050, PR #2054, PR #2059
- Spec-panel synthesis comment: see #1974 final round
- Related BCs: `UserLibrary`, `KnowledgeBase`, `SessionTracking`, `AgentMemory`, `SharedGameCatalog`

---

## 8. Open question summary (one-shot list for product)

For convenience, here are all 14 questions in one place — product can paste this into a meeting agenda or ticket reply.

```
Q1.1 — Recensioni BE entity schema + ratings shape?
Q1.2 — Recensioni moderation flow (auto vs manual vs report-and-takedown)?
Q1.3 — Recensioni spam controls (rate-limit per user, blocklist, abuse-reports)?
Q1.4 — Recensioni visibility model (self / friend / public) + uniqueness rules?
Q1.5 — Recensioni localization (language detection, translation on read)?
Q1.6 — Recensioni edit/delete history (visible edits? soft-delete?)
Q1.7 — Recensioni rating ↔ SharedGame.AverageRating rollup?

Q2.1 — Dischi semantics — (a) play records / (b) historical sessions / (c) achievements?
Q2.2 — If (c): MVP achievement catalog (which 3-5 events)?
Q2.3 — If (a) or (b): drop the tab or keep the label?

Q3.1 — Documenti shape — Option B (read-only summary) vs Option C (route consolidation)?
Q3.2 — If B: what data points surface (count, recent N, latest indexing state)?
Q3.3 — If C: deprecation window for `/library/[gameId]/kb` deep-links?

Q4.1 — Tab IDs — break URLs to rename, OR keep IDs and re-label only?
Q4.2 — If rename: deprecation window + server-side redirect plan?
Q4.3 — If rename: include seed-script + Playwright updates in the same PR?
```
