# Mockup Portfolio Review — 2026-06-08

> **Scope**: review completa del portfolio mockup in `admin-mockups/` per identificare
> mockup canonical, duplicati, gap, e drift documentazione. Limitato a user-facing
> end-user (SP3 / SP4 / SP6 / SP7 / SP8) — esclude SP5 admin dashboard e dev-fixtures.
>
> **Method**: cross-reference fra
> 1. `admin-mockups/design_files/*.html` (filesystem, ~95 file end-user)
> 2. `admin-mockups/briefs/SP{3,4,6,7,8}*.md` (use case attesi)
> 3. `admin-mockups/MOCKUPS_INDEX.md` (classification)
> 4. `docs/for-developers/frontend/v2-migration-matrix.md` (Route → Mockup index canonical)
> 5. `docs/for-developers/audits/2026-05-{12,22}-mockup-gaps.md` (audit precedenti)
> 6. 4 gap report esistenti (`libro-detail`, `players-detail`, `translate`, `agents-index`)
>
> **Decisioni**: review interattiva con maintainer (2 turni di
> `AskUserQuestion`, 8 domande chiave).

## TL;DR

| Dimensione | Conteggio | Note |
|---|---:|---|
| Mockup end-user inventariati | ~95 HTML + ~110 JSX | Da `design_files/`, esclude dev-fixtures + `standalone/` + `mockup-meeplecard/` |
| Route end-user mappate | 84 | Da v2-migration-matrix Route → Mockup index |
| Cluster duplicati identificati | 5 | 1 duplicato vero · 2 obsoleti documentati · 1 surface-distinte · 1 mirror voluto |
| Gap P0 (mockup mancanti dal brief) | 3 | SP7-C edit · SP6-F house-rule · SP7-H library-game-agent |
| Gap P1 (HTML companion mancanti per JSX esistenti) | 3 | SP6 quota/translation/play-session — risolti tramite cleanup (vedi §4) |
| Drift documentazione | 1 | `nanolith-game-night-storyboard` → rename to `librogame-game-night-storyboard` non sincronizzato |

**Outcome**: portfolio largamente sano. **5 cluster** valutati caso-per-caso con
maintainer; **3 azioni concrete** (cleanup .jsx Sara obsoleti + 4 nuovi mockup +
sync index/matrix). Roadmap implementation è in linea col v2-migration-matrix
(Wave 1+2+3+4 + SP6 Phase A/B/C + SP7 wave 1 + SP8 mobile-parity tutti shipped o
in delivery).

## 1. Inventario canonical (high-level)

### 1.1. Per scope

| Scope | Brief | Mockup HTML attesi | Mockup HTML presenti | Status |
|---|---|---:|---:|---|
| **SP3** public-secondary | 9 page (#1-9) | 9 | 8 | Contact #9 marcato facoltativo dal brief — non bloccante |
| **SP4** entity-desktop | 16 page (A-I) + wave residui | 16 | 16+ | Wave 1+2+3 shipped; Wave 4 D1/G2 done; E1/F1 mockup-ready |
| **SP4** session demos | skeleton + 7 game-specific | 16 (8 live + 8 summary) | 16 | Tutti shipped (skeleton polimorfico + 7 ad-hoc premium) |
| **SP4** editor user-facing | 5 (issue #1489) | 5 | 5 | CLOSED 2026-06-02 — `sp4-editor-*` mockup canonical |
| **SP4** toolkit sub-pages | 4 (issue #1490) | 4 | 4 | CLOSED 2026-06-02 — `sp4-toolkit-{history,play,stats,templates}` |
| **SP4** play-records | 5 mockup canonical | 5 | 5 | Shipped, FE stubs pending |
| **SP4** library wishlist | 1 (issue #1491) | 1 | 1 | CLOSED 2026-06-02 — `sp4-library-wishlist.html` |
| **SP6** libro-game MVP | 6 (A-F) | 6 | 3 | **GAP CONFERMATO**: D/E/F mancano come HTML (vedi §4) |
| **SP7** game-night + agents | 13 (A-M con #487 extension) | 13 | 9 | Wave 2 agent-builder rinominato in `sp4-editor-*`; gap su C/H/I/J |
| **SP8** libro-game companion | 3 stati (estensione esistente) | — | 1 (base) | Estensione di `librogame-runthrough-play-session.html` — stati 05/06/07 pending |
| **SP8** mobile-parity | 1 (`sp4-library-mobile.html`) | 1 | 1 | ✅ shipped |
| **Nanolith / libro-game runthrough** | 14 file Aaron Iter 1+4 | — | 14 | Cluster consolidato post-IA #871 |

### 1.2. Esclusioni esplicite da scope review

| Cartella | Contenuto | Razionale esclusione |
|---|---|---|
| `admin-mockups/standalone/` | ~55 file poster derivativi (`meeple-card-*`, `mobile-card-*`, `play-records--*`, `toolkit-mobile--*`) | Sono poster split derivati dai canonical, non route page-level |
| `admin-mockups/mockup-meeplecard/` | 8 file + 1 screenshot | Primitive testing per MeepleCard, non route end-user |
| `design_handoff_admin/admin/` | 28 admin page (`sp5-admin-*`) | Decisione utente: scope solo end-user, esclude SP5 admin |
| `design_files/*-fixture*` + `01-screens.html`, `04-design-system.html`, etc. | 12 dev-fixture (design system / hub / playground) | Non route-mapped |

## 2. Cluster duplicati — decisioni interattive

5 cluster sospetti analizzati. 3 confermati come azionabili (cleanup), 2
classificati come surface distinte / mirror intenzionali.

### 2.1. Cluster A — Resume picker libro-game ⚠️ DUPLICATO VERO

| File | Persona | Stati | Origine |
|---|---|---|---|
| `sp6-libro-game-resume-state.html` | Aaron (estensione family Sara) | 4 (first-time / single / multi / stale) | SP6 brief Cap.2 |
| `librogame-runthrough-resume-picker.html` | Aaron | 4 (state-01 EmptyFirstTime / state-02 ResumeHero / state-03 MultiCampaignList / state-04 StaleWarningCard) | Phase A Nanolith (issue #835/#838) |

Entrambi mappano alla stessa route `/library/[gameId]/play`, stessa persona,
4 stati identici. Naming convention differente (SP6 vs librogame-runthrough).

**Decisione**: tenere `librogame-runthrough-resume-picker.html` come canonical.

**Rationale**:
- Naming allineato al cluster `librogame-runthrough-*` post-IA consolidation #871 (14 file coerenti)
- Tracking Gherkin esplicito (issue #835/#838)
- File SP6 è cronologicamente più vecchio (pre-consolidation)

**Extension del scope (richiesta dal maintainer)**:
> Aggiungere CTA "Tutorial" condizionale al resume picker: se l'utente ha caricato un
> PDF di tipo "tutorial" → mostrare CTA "Tutorial" come azione aggiuntiva nel picker.

Da implementare nel mockup canonical (`librogame-runthrough-resume-picker.html`) con:
- Nuovo stato `state-05-with-tutorial` (variante di state-02 con CTA "Tutorial" extra)
- Indicatore visivo del tipo PDF caricato (entity=kb con sub-type tutorial)
- ConnectionPip update per riflettere tutorial KB se presente

**Tracking**: scope incluso in B19 cleanup (vedi §6.1).

### 2.2. Cluster B — Play session libro-game ✓ OBSOLETO DOCUMENTATO

| File | Persona | Status |
|---|---|---|
| `sp6-libro-game-play-session.jsx` | Sara | OBSOLETO |
| `librogame-runthrough-play-session.{html,jsx}` | Aaron | CANONICAL |

Header esplicito del file canonical:
> `Sostituisce sp6-libro-game-play-session.html (persona Sara → Aaron, IA consolidata)`

**Decisione**: eliminare `sp6-libro-game-play-session.jsx` (dead code).

**Rationale**:
- Documentazione esplicita ("Sostituisce...")
- Aaron version ha overdelivered: 4 stati (story / encounter / chat-overlay / glossary-inline) vs scope brief originale
- SP8 companion (3 nuovi stati pending: diary / paragrafi / end-campaign) estende
  la versione Aaron, non quella Sara

### 2.3. Cluster C — Translation viewer ✓ OBSOLETO DOCUMENTATO

| File | Persona | Stati | Status |
|---|---|---|---|
| `sp6-libro-game-translation-viewer.jsx` | Sara | Brief SP6-D (route `/gamebook/.../paragraph/[num]` fullscreen) | OBSOLETO |
| `librogame-runthrough-translate-viewer.html` | Aaron | 13 stati (A-M, incluso 4 Aaron-CORE refinement shipped 2026-05-23) | CANONICAL |

**Decisione**: eliminare `sp6-libro-game-translation-viewer.jsx`.

**Rationale**:
- Stesso pattern del Cluster B (Sara version pre-IA-consolidation)
- Aaron version ha 4 stati Aaron-CORE refinement aggiunti il 2026-05-23 (Loading 4-step skeleton · Reader mode toggle · Multi-language detection + override · Manual-mode entry)
- Spec di riferimento: `docs/superpowers/specs/2026-05-23-mockup-refinement-aaron-core-design.md`
- Gap report `translate-gap-report.md` (#1487) traccia drift 61.5% — FE implementation pending ma mockup è canonical

### 2.4. Cluster D — Quota credits ✓ SURFACE DISTINTE (NON duplicati)

| File | Persona | Route | Scope |
|---|---|---|---|
| `sp6-libro-game-quota-credits.jsx` | Sara | `/gamebook/checkout` | Full checkout flow 4-step (warning → pack picker → Stripe → success) |
| `librogame-runthrough-quota-credits.html` | Aaron | overlay global | Status overlay sempre visibile (NON checkout) |

**Decisione**: tenere entrambi, documentare i ruoli distinti.

**Rationale**:
- Use case e route distinti (full checkout flow vs status overlay)
- Le due UI non si sovrappongono per scope
- Da documentare nel MOCKUPS_INDEX.md per evitare future confusioni

**Action**: aggiungere note di scope esplicite nel MOCKUPS_INDEX (vedi §5).

### 2.5. Cluster E — Glossary editor ✓ OBSOLETO documentato (mirror)

| File | Tipo | Status |
|---|---|---|
| `sp6-libro-game-glossary-editor.jsx` | Component-mock | OBSOLETO |
| `librogame-runthrough-glossary-editor.html` | Component-mock | CANONICAL |

MOCKUPS_INDEX dichiara esplicitamente: *"Glossary editor (mirror of sp6 jsx)"*.

**Decisione**: eliminare `sp6-libro-game-glossary-editor.jsx`.

**Rationale**:
- Mirror intenzionale ma residuo evolutivo (pattern Cluster B/C)
- Aaron HTML version è il canonical per via dell'IA consolidation
- "Mirror" implica copia, non co-esistenza funzionale

## 3. Gap identificati

### 3.1. P0 — Mancanti dal brief, **no fallback** documentato

#### Gap #1 — `sp7-game-night-edit.html` (SP7 Wave 1 C)

- **Brief reference**: SP7 §C, route `/game-nights/[id]/edit`
- **Status filesystem**: completamente assente (né HTML né JSX)
- **Impact**: feature richiesta dal brief SP7 wave 1, gap per US-31 host workflow
  (edit serata pianificata, reschedule warning, cancel section)
- **Coverage attuale**: nessun mockup copre il flow edit
- **Action**: aprire **B20** issue (vedi §6.2)

#### Gap #2 — `sp6-libro-game-house-rule.{html,jsx}` (SP6 Wave 1 F)

- **Brief reference**: SP6 §F, drawer dentro `/gamebook/[gameId]/play`
- **Status filesystem**: completamente assente
- **Impact**: feature G3.3 + G3.4 del vision Libro-game (low-confidence Q&A
  trigger → "🤝 Definisci house rule"). 2 tab (Crea + Le tue rules), saved
  success state, edit/delete actions
- **Coverage attuale**: nessuno
- **Action**: aprire **B21** issue (vedi §6.2)

#### Gap #3 — `sp7-library-game-agent.html` (SP7 Wave 3 H)

- **Brief reference**: SP7 §H, route `/library/games/[gameId]/agent`
- **Status filesystem**: assente
- **Coverage parziale**: `sp4-game-chat-tab.html` (chat tab embedded in
  `/library/[gameId]/agent`) + `sp4-agent-detail.html` (character sheet)
- **Discussion**: le 2 surface parziali coprono parte del scope (chat + agent
  detail), ma il brief chiede un mockup integrato single-source con
  micropattern coordinate (suggested queries strip, citation expanded drawer,
  message-action drawer, error states, mobile fullscreen vs desktop split).
- **Action**: marcare come **deferred** — verificare se le 2 surface attuali
  bastano per US-13/33 prima di aprire un nuovo gap (vedi §6.3 follow-up)

### 3.2. P1 — Mancanti come HTML, esistono come JSX (risolti via cleanup)

| File mancante | Brief | Decisione |
|---|---|---|
| `sp6-libro-game-play-session.html` | SP6 §C | Risolto via cleanup Cluster B (canonical = `librogame-runthrough-play-session.html`) |
| `sp6-libro-game-translation-viewer.html` | SP6 §D | Risolto via cleanup Cluster C (canonical = `librogame-runthrough-translate-viewer.html`) |
| `sp6-libro-game-quota-credits.html` | SP6 §E | Sara version come JSX-only è OK perché Aaron coverage è `librogame-runthrough-quota-credits.html` (overlay, ruolo distinto). Brief SP6 chiede il full checkout flow Sara → mockup HTML opzionale, può restare JSX-only |

### 3.3. P2 — Notifications v2 (SP7 Wave 3 I/J)

| File mancante | Brief | Fallback attuale |
|---|---|---|
| `sp7-notifications-hub.html` | SP7 §I | `notifications.html` (SP1, pre-v2 tokens) |
| `sp7-notifications-preferences.html` | SP7 §J | `notifications.html` (idem) |

**Coverage attuale**: `notifications.html` esiste ma è pre-design-system-v2
(non usa `entityHsl()` helper, non ha 9-entity color palette per severity, non
ha bulk actions / snooze / mention filtering specifiche del brief SP7).

**Decisione utente**: creare i 2 mockup `sp7-notifications-*` nuovi in v2.

**Action**: aprire **B22** issue (vedi §6.2).

### 3.4. P3 — Drift documentazione

**Issue**: `nanolith-game-night-storyboard.html` è citato in:
- `admin-mockups/MOCKUPS_INDEX.md` (linea 192: *"`/game-nights/[id]` (storyboard variant)"*)
- `docs/for-developers/frontend/v2-migration-matrix.md` (citato in /game-nights/[id] row, righe 758)

Ma il file **non esiste** nel filesystem. Esiste invece:
- `librogame-game-night-storyboard.html` (stesso scope, naming evolved)

**Cause**: rename non sincronizzato post-IA consolidation #871.

**Action**: sync inline in MOCKUPS_INDEX + v2-matrix (vedi §5).

## 4. Cleanup file da eliminare

3 file `.jsx` Sara identificati come dead code:

| File | Cluster | Rationale |
|---|---|---|
| `admin-mockups/design_files/sp6-libro-game-play-session.jsx` | B | Sostituito esplicitamente da `librogame-runthrough-play-session.jsx` |
| `admin-mockups/design_files/sp6-libro-game-translation-viewer.jsx` | C | Sostituito da `librogame-runthrough-translate-viewer.html` con +8 stati |
| `admin-mockups/design_files/sp6-libro-game-glossary-editor.jsx` | E | MOCKUPS_INDEX dichiara "mirror of sp6 jsx" → mirror residuo |

**File da TENERE come JSX-only (no .html companion)**:
- `sp6-libro-game-quota-credits.jsx` (ruolo distinto da overlay Aaron, Cluster D)

**Total cleanup**: 3 file eliminati, ~XKB recuperati, riduzione confusione naming.

## 5. Update richiesti — MOCKUPS_INDEX.md + v2-migration-matrix.md

### 5.1. `admin-mockups/MOCKUPS_INDEX.md`

**Rimuovere**:
- Riga `nanolith-game-night-storyboard.html` (file inesistente)
- Righe relative ai 3 `.jsx` Sara eliminati (Cluster B/C/E)

**Aggiungere**:
- Riga `librogame-game-night-storyboard.html` (presente, era citato col naming nanolith-)
- Nota di scope per `sp6-libro-game-quota-credits.jsx` ("Full checkout flow Sara, distinto dall'overlay Aaron `librogame-runthrough-quota-credits.html`")
- Update summary count: page-mock 67 → 67 (no change), component-mock 51 → 48 (-3 da cleanup)

### 5.2. `docs/for-developers/frontend/v2-migration-matrix.md`

**Sync Route → Mockup index**:
- `/game-nights/[id]` row (linea 758): sostituire `nanolith-game-night-storyboard.html` → `librogame-game-night-storyboard.html`
- `/library/[gameId]/play/[campaignId]` row (linea 722): rimuovere reference a `sp6-libro-game-index.html` (è index `/gamebook`, non play-session)
- `/library/[gameId]/play` row (linea 721): mantenere entrambi `librogame-runthrough-resume-picker.html` + `sp6-libro-game-resume-state.html` finché il cleanup non lana

**Sync sezione SP6 Phase 1**:
- Aggiungere riga per `sp6-libro-game-quota-credits.jsx` (ruolo distinto)
- Aggiungere riga per **gap SP6-F** (`sp6-libro-game-house-rule` mancante)

## 6. Issue GitHub e PR follow-up

### 6.1. Issue B20 — Mockup portfolio cleanup (this audit)

> **Nota numerazione**: B11-B17, B19 sono già usati (Play Records / Pricing /
> Editor / Toolkit sub-pages / Wishlist / Sessions sub-pages / Session skeleton).
> B13, B18 sono liberi ma non in scope. B20+ sono i prossimi liberi.

**Title**: `[Design v1 · B20] Mockup portfolio cleanup (3 SP6 .jsx Sara obsoleti + MOCKUPS_INDEX drift + tutorial CTA)`

**Body summary**:
- Eliminare 3 .jsx Sara obsoleti (Cluster B/C/E)
- Sync MOCKUPS_INDEX.md (rimozioni + rename nanolith→librogame)
- Sync v2-migration-matrix.md (sezione SP6 + linea /library/[gameId]/play/[campaignId])
- Aggiungere CTA "Tutorial" condizionale a `librogame-runthrough-resume-picker.html`
  - Nuovo stato `state-05-with-tutorial` (variante di state-02)
  - Condizionale a presenza PDF tipo "tutorial" caricato
  - Verifica con backend: il modello PdfDocument ha un campo "type" (rules / faqs / tutorial / etc)? Se no, evaluation della schema-extension richiesta

**PR**: branch `feature/issue-{B20_number}-mockup-portfolio-cleanup`

### 6.2. Issue B21 — `sp7-game-night-edit.html` greenfield

**Title**: `[Design v1 · B21] Mockup sp7-game-night-edit (SP7 Wave 1 C gap)`

**Body summary**:
- Brief reference: `admin-mockups/briefs/SP7-game-night-agent-builder.md` §C
- Route `/game-nights/[id]/edit`
- 7 stati richiesti (pristine / dirty-no-conflict / dirty-reschedule / dirty-conflict / cancel-modal-pristine / cancel-modal-with-reason / success-toast)
- Riusa ConfirmModal pattern SP6-B
- Componenti emergenti: nessuno nuovo (compositions da SP4/SP7 wave 1)

### 6.3. Issue B22 — `sp6-libro-game-house-rule.{html,jsx}` greenfield

**Title**: `[Design v1 · B22] Mockup sp6-libro-game-house-rule (SP6 §F drawer)`

**Body summary**:
- Brief reference: `admin-mockups/briefs/SP6-libro-game.md` §F
- Drawer dentro `/gamebook/[gameId]/play` (no route propria)
- 2 tab (Crea / Le tue rules) + saved success state
- Trigger da chat low-confidence response in SP6 §C Tab Chat
- Componenti emergenti: `DiaryMarkdownEditor` reuse + textarea custom

### 6.4. Issue B23 — Notifications v2 (Wave 3 SP7 I/J)

**Title**: `[Design v1 · B23] Mockup sp7-notifications-hub + sp7-notifications-preferences (SP7 Wave 3 v2)`

**Body summary**:
- Brief reference: `admin-mockups/briefs/SP7-game-night-agent-builder.md` §I + §J
- Route `/notifications` e `/notifications/preferences`
- Status: `notifications.html` (SP1) esiste ma è pre-v2 (no entityHsl, no severity dots, no bulk actions)
- I (hub): timeline grouped by date, severity dots (critical/important/info/read),
  filter tabs (Tutte / Non lette / Mention / Critiche), bulk actions, swipe
  actions per notification card
- J (preferences): form 3 sezioni (per event type · frequency · channels)
- Stati richiesti: vedi brief §I (8 stati) + §J (5 stati)
- Coordinamento: confrontarsi con la PR di implementazione `UserNotifications` BC
  (24 cmd + 18 query backend, US-41) per schema-alignment

### 6.5. Issue B24 (FOLLOW-UP) — verifica copertura SP7-H

**Title**: `[Design v1 · B24] (verifica) sp7-library-game-agent gap vs sp4-game-chat-tab + sp4-agent-detail`

**Body summary**:
- Brief SP7 §H chiede mockup integrato per `/library/games/[gameId]/agent`
- Coverage attuale parziale: `sp4-game-chat-tab.html` (chat tab) + `sp4-agent-detail.html` (character sheet)
- **Question**: le 2 surface attuali bastano per US-13/33 happy path, o serve un
  mockup integrato single-source?
- Discovery: leggere i 10 stati del brief §H, verificare se `sp4-game-chat-tab`
  copre i suggested queries strip + citation expanded drawer + message-action
  drawer. Se sì → marcare brief §H come "coperto da surface composition". Se no
  → aprire mockup separato

## 7. Validation runtime — status integration

| Surface | Status | URL |
|---|---|---|
| Mockup statico (browser) | ✅ Attivo background | `http://127.0.0.1:8765/index.html` (rediretta a `sp4-dashboard.html`) |
| Implementation runtime (`make integration`) | ⏳ Pending (richiede maintainer per SSH passphrase) | `localhost:3000` (FE) + `localhost:8080/scalar/v1` (API) post tunnel |

**Validation pattern** (post integration up):
1. Aprire mockup canonical in tab A (es. `sp4-dashboard.html`)
2. Aprire implementazione live in tab B (es. `http://localhost:3000/dashboard`)
3. Side-by-side comparison: tokens / layout / stati / interactions
4. Identificare drift visivo / funzionale / accessibilità

**Riferimenti gap report C-series già completati**:
- `libro-detail-gap-report.md` — 0% drift ✅
- `players-detail-gap-report.md` — 10% drift ✅
- `translate-gap-report.md` — 61.5% drift ⚠️ (4 Aaron-CORE refinement pending)
- `agents-index-gap-report.md` — 0% drift ✅

**Suggerimento**: estendere il pattern C-series a `/dashboard`, `/discover`,
`/sessions/[id]/live`, `/game-nights` (post B19 cleanup).

## 8. Riassunto azioni

| # | Azione | Priorità | Owner | Tracking |
|---|---|---|---|---|
| 1 | Eliminare 3 .jsx Sara obsoleti | P0 | Maintainer | B20 |
| 2 | Sync MOCKUPS_INDEX.md (rimozioni + rename) | P0 | Maintainer | B20 |
| 3 | Sync v2-migration-matrix.md | P0 | Maintainer | B20 |
| 4 | Aggiungere CTA "Tutorial" condizionale a resume-picker | P1 | Claude Design / maintainer | B20 ext |
| 5 | Creare mockup `sp7-game-night-edit` | P1 | Claude Design | B21 |
| 6 | Creare mockup `sp6-libro-game-house-rule` | P1 | Claude Design | B22 |
| 7 | Creare mockup `sp7-notifications-{hub,preferences}` v2 | P1 | Claude Design | B23 |
| 8 | Verificare copertura SP7-H | P2 | Maintainer (read-only audit) | B24 |
| 9 | Validation runtime side-by-side (post tunnel) | P2 | Maintainer | local |
| 10 | Estendere C-series gap report a dashboard/discover/sessions/game-nights | P3 | Maintainer | follow-up |

## 9. Cross-references

- [`admin-mockups/MOCKUPS_INDEX.md`](../../../admin-mockups/MOCKUPS_INDEX.md) — file classification
- [`docs/for-developers/frontend/v2-migration-matrix.md`](../frontend/v2-migration-matrix.md) — Route → Mockup index canonical
- [`docs/for-developers/audits/2026-05-12-mockup-gaps.md`](./2026-05-12-mockup-gaps.md) — gap audit precedente (5 cluster)
- [`docs/for-developers/audits/2026-05-22-mockup-gaps.md`](./2026-05-22-mockup-gaps.md) — gap audit più recente (9 cluster)
- [`admin-mockups/briefs/_common.md`](../../../admin-mockups/briefs/_common.md) — preambolo brief
- [`admin-mockups/briefs/SP3-public-secondary.md`](../../../admin-mockups/briefs/SP3-public-secondary.md)
- [`admin-mockups/briefs/SP4-entity-desktop.md`](../../../admin-mockups/briefs/SP4-entity-desktop.md)
- [`admin-mockups/briefs/SP4-wave-3.md`](../../../admin-mockups/briefs/SP4-wave-3.md)
- [`admin-mockups/briefs/SP6-libro-game.md`](../../../admin-mockups/briefs/SP6-libro-game.md)
- [`admin-mockups/briefs/SP7-game-night-agent-builder.md`](../../../admin-mockups/briefs/SP7-game-night-agent-builder.md)
- [`admin-mockups/briefs/SP8-libro-game-companion.md`](../../../admin-mockups/briefs/SP8-libro-game-companion.md)
- [`admin-mockups/briefs/SP8-mobile-parity.md`](../../../admin-mockups/briefs/SP8-mobile-parity.md)
- [`admin-mockups/design_handoff/libro-detail-gap-report.md`](../../../admin-mockups/design_handoff/libro-detail-gap-report.md) — 0% drift
- [`admin-mockups/design_handoff/players-detail-gap-report.md`](../../../admin-mockups/design_handoff/players-detail-gap-report.md) — 10% drift
- [`admin-mockups/design_handoff/translate-gap-report.md`](../../../admin-mockups/design_handoff/translate-gap-report.md) — 61.5% drift
- [`admin-mockups/design_handoff/agents-index-gap-report.md`](../../../admin-mockups/design_handoff/agents-index-gap-report.md) — 0% drift
