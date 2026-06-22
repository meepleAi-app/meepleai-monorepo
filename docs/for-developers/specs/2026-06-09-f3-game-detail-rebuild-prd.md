# F3 Game Detail Full Rebuild — PRD

**Issue:** #2010 (follow-up of umbrella #1974)
**Status:** **PARTIAL — 4 meta-decisioni risolte**, 5 Q residue di dettaglio (vedi §3a). F3 core implementabile.
**Date:** 2026-06-09 (rev. 2 — decisioni product)
**Author:** Claude Code (draft synthesis + product decisioni recorded)

> Documento originale (rev. 1) capturava la spec-panel synthesis come 14 Q aperte. La rev. 2 incorpora le 4 meta-decisioni product prese il 2026-06-09 via socratic-mode review (§3a) e ridimensiona le Q residue + la roadmap (§4).

---

## 3a. Decisioni prese 2026-06-09 (Socratic review)

Le 14 Q originali sono state collassate in **4 meta-decisioni** durante una sessione socratic-mode. Tutte risolte:

### M1 — Identità prodotto: **Racconto + commentary (social/curated)**

MeepleAI è il *racconto* del boardgame, NON il sostituto di BGG. Friend-first di default. Rating sono espressione personale (non analitica), niente roll-up su catalog. Moderation lightweight (report-based, no pre-publish manual approval).

**Risolve direttamente:**
- **Q1.1** Schema entity → `Review { id, gameId, authorUserId, ratingPersonal: 1-10, title, body, visibility: 'friends'|'public' (default friends), createdAt, editedAt, deletedAt, languageDetected }`. `ratingPersonal` è label-prefix "Il mio voto" (no claim oggettivo).
- **Q1.2** Moderation → report-based, no pre-publish approval. Reports finiscono in admin queue (epic separato F3.4.x).
- **Q1.4** Visibility → `friends` default. `public` opt-in esplicito al primo write con disclaimer "visibile a chiunque" + warning anti-spam.
- **Q1.7** Rating rollup → **NO rollup** su `SharedGame.AverageRating`. BGG resta canonico. MeepleAI espone "I tuoi amici dicono N/10" come block separato accanto al BGG block.

**Lascia aperto (Q residue):**
- **Q1.3** Spam rate-limit (`rate-limit per user`): valore numerico (es. 3 review/24h per user) da product.
- **Q1.5** Localization: detection accept, ma cosa fare a read-time se review in lingua altra? Defer Phase 2 oppure mostrare "[mostra originale / traduci]" toggle.
- **Q1.6** Edit/delete history: visibilità storica edit ⇒ GDPR right-to-rectify implica audit log invisibile all'utente. Behaviour UX (mostrare "edited" badge?) decisione UX residua.

### M2 — Tab Dischi: **Merge con Partite (b), enrich con stats/streaks/wins**

Tab `Partite` esistente viene **rinominata in `Dischi`** e arricchita con stats (count totale, ultimo `playedAt`, win-rate %, streak corrente di consecutive wins, average duration). Mantiene il timeline storico esistente.

**Risolve:**
- **Q2.1** → meaning = (b) historical sessions + stats
- **Q2.2** → N/A (no achievements entity needed; stats sono derived da Session aggregate esistente)
- **Q2.3** → keep tab, rename Partite → Dischi (label change is breaking URL — vedi §3a M4 sotto)

### M3 — Tab Documenti: **Option B (read-only summary, 2 surfaces)**

Tab `Documenti` ⇒ thin read-only summary all'interno di `/library/[id]`. Surface: `{N PDF • Ultimo: TITLE indexato XdAY fa • [Manage in KB →]}`. `/library/[id]/kb` resta canonico per upload / delete / indexing state. No data-sync, no duplicazione UI.

**Risolve:**
- **Q3.1** → Option B (read-only summary)
- **Q3.2** → surface esatto: count + most-recent title + last indexing timestamp + link "Manage in KB"
- **Q3.3** → N/A (no route consolidation)

### M4 — URL rename: **Evidence-first discovery (1-2h)**

Decisione finale (label-only vs rename completo) **bloccata** su discovery quantitativa:

1. Pull access logs ultimi 30gg per `/library/[id]?tab=aiChat|toolbox`
2. Se < 100 req/mese su legacy IDs → rename completo + redirect server-side + 30gg deprecation banner
3. Se ≥ 100 req/mese → label-only (URL IDs invariati)

**Decision deadline**: prima dello start di F3.1 (tab nav skeleton).

**Note**: la rename di `Partite → Dischi` (M2) eredita lo stesso evidence-gate. Se label-only è la scelta, l'URL resta `?tab=partite` con label visualizzata "Dischi".

---

## ⚠️ La sezione §3 sotto è preservata per traceability storica (rev. 1)

Le Q originali enumerate sotto restano nel documento per **traceability** del processo decisionale, ma le decisioni prese in §3a sopra **prevalgono** sulla loro versione "open". Saltare alla §4 per la roadmap aggiornata.

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

## 4. Implementation phases — rev. 2 (post-decisioni 2026-06-09)

Effort + gates aggiornati con le 4 meta-decisioni di §3a.

| Phase | Deliverable | Effort | Status / Gates |
|---|---|---|---|
| **F3.0** | **Discovery URL rename** (M4) — pull access logs `/library/[id]?tab=aiChat\|toolbox\|partite` ultimi 30gg; decidere label-only vs rename completo | 1-2h | 🔓 unblocked — prerequisite per F3.1 |
| **F3.1** | Tab nav skeleton (5 tab finali: Info / Agente / Toolkit / FAQ / Dischi). URL strategy applicata da F3.0 | 3-5h | ⏳ blocked on F3.0 |
| **F3.2** | House Rules promotion. Source-of-truth alignment con `AgentMemory` BC | 3h | 🔓 unblocked |
| **F3.3** | Documenti section (Option B — read-only summary): `{N PDF • Ultimo: TITLE indexed XdAY • Manage in KB →}` | 3-4h | 🔓 unblocked (M3 chiuso) |
| **F3.4** | Dischi (era Partite) enrich — stats: count totale, ultimo playedAt, win-rate %, streak corrente, average duration. Timeline storico preservato. | 4-6h | 🔓 unblocked (M2 chiuso) |
| **F3.5** | Inline agent chat embedded | 4-6h | 🔓 unblocked (no URL change in chat scope) |
| **F3.6** | **Recensioni** (separate epic) — BE entity + 5 Q residue closure + FE list + write/edit/delete + report-queue admin | 16-24h | ⏸ epic separato, parked finché Q1.3/Q1.5/Q1.6 risposte |

**F3 core rebuild** = F3.0 → F3.1 → F3.2 → F3.3 → F3.4 → F3.5 (~18-25h totali, da fare in sequenza dopo F3.0).

**F3.6 (Recensioni)** sta in epic dedicato perché:
- Tocca BE entity nuova + admin moderation queue + report flow
- Q residue (Q1.3 rate-limit, Q1.5 localization, Q1.6 edit history UX) richiedono ulteriori scelte product prima della stesura.

### Q residue da chiudere prima di F3.6

| Q | Owner | Decision needed |
|---|---|---|
| **Q1.3** Spam rate-limit | Product | Numero (es. 3 review per user / 24h?) |
| **Q1.5** Localization at read | Product + UX | "Show original / Translate" toggle? Detect + label? Defer? |
| **Q1.6** Edit history UX | Product + UX + Legal | "edited at" badge sì/no? Conservare diff per audit GDPR? |

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
- [x] Implementation phases re-scoped based on the gating structure (§4 rev. 2)
- [x] Risk register established (§5)
- [x] **Product responses recorded** — 2026-06-09 (§3a). 4 meta-decisioni chiuse, 5 Q residue tracciate per F3.6 epic.
- [x] **Multi-PR breakdown finalised** — §4 rev. 2 specifica 7 phase (F3.0-F3.6) con effort + gates.

This PRD's deliverable è ora **completo**:
- §3 = traceability storica delle 14 Q originali (rev. 1)
- §3a = decisioni effettive prese (rev. 2)
- §4 rev. 2 = roadmap implementabile

Issue #2010 può chiudersi una volta che F3.0 (discovery URL) parte.

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
