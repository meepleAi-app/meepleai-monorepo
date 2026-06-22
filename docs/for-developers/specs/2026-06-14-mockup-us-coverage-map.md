---
status: PROPOSED (gap analysis ready for review; US-INT-2..6 draft full; US-GAP-* compact)
issue: TBD (umbrella tracker da aprire; parent DS-17 #2063)
spec-panel: 2026-06-14 (Cockburn · Wiegers · Adzic · Crispin · Fowler · Nygard)
verdict: gap-fill mapping ready; US-INT-* full need ADR resolution + sub-issue split; US-GAP-* immediately actionable
parent: #2063 DS-17 (Storybook stories pattern), #2127 Phase B audit, #1023 Design System De-versioning (CLOSED)
related-specs:
  - docs/superpowers/specs/2026-06-12-us-int-1-kb-onboarding-spec.md (US-INT-1 template)
  - docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md (DS-17 panel review)
  - docs/for-developers/audits/2026-05-22-mockup-gaps.md (audit gap baseline)
  - docs/for-developers/workflows/us-verification-protocol.md (US-N verification queue)
  - admin-mockups/briefs/SP7-game-night-agent-builder.md (US-31/33/41 brief)
---

# Mockup-to-US Coverage Map — 2026-06-14

> **Tipo**: spec doc Tipo B+C combinato (vedi DEC-1 più sotto)
> · **Scope**: 115 mockup user-facing (67 page-mock + 48 component-mock; esclusi 12 dev-fixture)
> · **Output**: 5 US-INT-* full + 17 US-GAP-* compact + cross-ref issue aperte
> · **Maturity**: PROPOSED — review designer + maintainer richiesto prima dell'opening umbrella

## Sezione 0 — Premise & Scope

### Contesto

Il monorepo MeepleAI ha **127 mockup** in `admin-mockups/design_files/` (post DS-17 Phase B audit
2026-06-10). Di questi:
- **12 dev-fixture** (`tokens.css`, `00-hub.html`, `04-design-system.html`, ...) — design system
  reference, fuori scope di questo doc (non sono "testabili" come feature utente).
- **67 page-mock** — corrispondono a route Next.js reali (`/dashboard`, `/library`, `/sessions/[id]`, ...).
- **48 component-mock** — drawer, overlay, citation viewer, sub-tab placeholder. Testabili nel
  contesto del page-mock che li ospita.

**Problema attuale** (post-panel 2026-06-09 DS-17 review, score workflow 4.5/10):
- Mockup HTML sono **"looks like this"** ma non rispondono a "how do I know it's correct?"
- Test coverage stato (default/empty/loading/error/sse) **non standardizzata** — molti page-mock
  mostrano solo lo stato felice.
- Mapping mockup ↔ US ↔ issue **disperso** in 5+ doc differenti.

### Decisioni lockate per questo doc (DEC-*)

**DEC-1**: produciamo **Tipo B (US-INT-*)** + **Tipo C (US-GAP-*)** combinati in un singolo doc spec.
- *Esclusione esplicita*: Tipo A (1 spec per ogni mockup) è già pianificato sotto **DS-17 #2063 Phase
  3** come Storybook stories. NON duplichiamo.
- *Razionale*: gli scenari end-to-end (B) e i mockup orfani (C) sono il gap reale **non coperto** da
  DS-17.

**DEC-2**: format mix — 5 US-INT-* full (stile US-INT-1, ~100 righe ciascuna) + 17 US-GAP-* compact
(~25 righe ciascuna).
- *Razionale*: US-INT-* sono journey ricchi che richiedono Cockburn + Gherkin + failure modes. US-GAP-*
  sono "esiste un mockup, deve esistere una US che lo motivi e lo test-i" — basta GWT + AC.

**DEC-3**: scope issue analizzate = **umbrella attive + bloccanti P0/P1** (~15 issue).
- *Esclusione esplicita*: issue chiuse rilevanti citate ma NON re-analizzate (vedi Sezione 5
  cross-ref).

**DEC-4** (lock post-review 2026-06-14): le **5 US-GAP-SESS-*** (Codenames / Power Grid / Puerto Rico
/ Paleo / Zombicide) restano US distinte (non sub-spec di US-INT-4c), MA il loro Parent dichiarato è
US-INT-4 + US-INT-4c. Risoluzione Fowler tension (opt b nel §6): granularità diverse hanno scope
distinti — US-INT-4c definisce il **flavor module loader contract** (DTO + hot-load mechanism),
US-GAP-SESS-* definisce **il behavior test concrete per ogni flavor** (Spymaster overlay, hex
board, ecc.).
- *Razionale*: opt a (sub-spec di US-INT-4c) renderebbe ogni game-flavored un "sub-step" del loader,
  perdendo la testabilità E2E del comportamento gioco-specifico. opt b mantiene 2 livelli: contract
  + concrete behavior.
- *Conseguenza*: ogni US-GAP-SESS-* deve dichiarare `Parent: US-INT-4c (loader) + US-INT-4 (skeleton
  generic)` invece del solo `US-INT-4`. Vedi §4b cluster B.

### Audience (3 attori)

| Attore | Cosa cerca | Sezione da leggere |
|---|---|---|
| **Sviluppatore** (codifica → React) | "Quale mockup → quale route → quale US guida il tasking?" | 2.1, 4a per scenario completo, 4b per gap singolo |
| **QA / Tester** (E2E Playwright) | "Cosa devo testare per chiudere questa US? Quali stati canonici?" | 4a (Gherkin), 4b (AC compact) |
| **Designer** (review post-impl) | "Quale mockup è obsoleto? Quali ho già firmato? Cosa devo ancora vedere?" | 3.3 review pendings, 5 cross-ref issue |

---

## Sezione 1 — US esistenti nel repo (inventario)

### 1.1 Famiglie identificate

Discovery automatica (Grep repo-wide) ha trovato **22 identifier univoci di tipo US** in 4 famiglie
(15 US-N "puri" + 7 sub-Gherkin embedded `G31.N`/`G33.N`/`G41.N` riusati come identifier in mockup JSX state-variant):

| Famiglia | Count | Stato | Fonte primaria |
|---|---|---|---|
| **US-INT-*** | 1 (US-INT-1) | SPEC READY (NOT READY for impl — 2 P0 blockers residui) | `docs/superpowers/specs/2026-06-12-us-int-1-kb-onboarding-spec.md` |
| **US-3x** (Game Nights / Agent / Notifications) | 4 (US-31, US-32, US-33, US-41) | P1 ACTIVE | `admin-mockups/briefs/SP7-game-night-agent-builder.md` (37 ref) |
| **US-N legacy** (Login/Dashboard/Library/Games) | 10 (US-2, 6, 8, 9, 10, 13, 15, 25, 26, 27) | VERIFIED (verification queue) | `docs/for-developers/workflows/us-verification-protocol.md:139-150` |
| **Sub-Gherkin embedded** | 7 (sample: G31.1, G31.2, G31.4, G31.7, G33.1, G33.4, G41.4 — non esaustivo) | EMBED-LOCAL | inline JSX `sp7-*.jsx` state-variant labels |
| **AC-X.N** (Acceptance Criteria refs) | 15+ (D, E, F, C, N suffix; NON contati nel totale "22") | SPEC-LOCAL | sparsi in `docs/for-developers/specs/` + `docs/superpowers/specs/` |

### 1.2 Mappa US esistenti ↔ mockup ↔ stato

| US | Persona | Mockup primario | Mockup secondari | Spec doc | Stato impl |
|---|---|---|---|---|---|
| US-2 Login | Marco | `auth-flow.html` | — | verification-protocol.md | ✅ VERIFIED |
| US-6 Dashboard priority-driven | Marco | `sp4-dashboard.html` | (asse C #1898 superseded `.jsx`) | asse-C P1898 spec + #2114 obsolete tracking | ✅ SHIPPED sess.34 |
| US-8 Games hub multi-tab | Marco | `sp4-games-index.html` | `sp4-discover.html` (Discover default tab) | asse-D P2 spec | ✅ SHIPPED sess.36 (#2270 baseline cleanup) |
| US-9 Game detail tabs | Marco | `sp4-game-detail.html` | 5 sub-tab `.html` (#2148: rules/reviews/strategies/chat/faqs) | DS-17-13 sp4-content design | ⚠ tabs canonical fixed in ADR-061 |
| US-10 Library hybrid hub | Marco | `sp4-library-desktop.html` | `sp4-library-mobile.html` (forward-refactor) | DS-17-12 sp4-catalog design | ✅ Wave B.3 shipped |
| US-13 GameNight create wizard | Marco | `sp7-game-night-new.html` ✅ (filename brief aligned via PR #2351) | — | SP7 brief A | 🚫 NOT IMPLEMENTED (brief ready, FE TODO) |
| US-15 GameNight detail + RSVP | Marco/Davide | `sp7-game-night-detail-rsvp.html` | edit is a drawer overlay (no separate mockup — ratified Option C in #2344) | SP7 brief B+C | 🚫 NOT IMPLEMENTED |
| US-25 Notifications inbox | Marco | `notifications.html` | (component `sp7-notifications-*.html` to ship) | SP7 brief I+J | ⚠ partial (preferences shipped, hub TODO) |
| US-26 Profile + achievements | Marco | `sp5-profile-settings.html` | `sp4-player-detail.html` (self-view reuse) | sp5-profile-settings spec + #492 closure | ⚠ achievement detail sheet missing (audit 2026-05-22 §P0) |
| US-27 AI agent chat | Marco/Aaron | `chat-fullscreen.html` | `sp4-game-chat-tab.html` (composite), `sp7-library-game-agent.html` (game-scoped) | DS-17 Phase B audit | ⚠ scope splittato, chat full-screen desktop residue (audit 2026-05-22 §P0 #491) |
| US-INT-1 KB onboarding (Mage Knight) | Andrea | (cross-mockup: discover → game-detail → upload → chat) | `sp4-discover.html`, `sp4-game-detail.html`, `sp4-upload-wizard-extended.html`, `chat-fullscreen.html` | US-INT-1 spec | 🚫 NOT READY (P0 #2176, ADR-061 done #2204) |
| **US-31 Game Nights P1** | Marco (host) | `sp7-game-night-detail-rsvp.html` + 5 more | `sp7-game-night-{new,edit,live,transition,summary}.html` | SP7 brief Wave 1+1+ | 🚫 NOT IMPLEMENTED (mockup ready, FE TODO) |
| **US-32 Play Records (boardgamer)** | Marco | `sp4-play-records-index.html` | 4 more `sp4-play-records-*` (new, detail, edit, stats) | (NONE — gap N2 audit 2026-05-22) | 🚫 NOT IMPLEMENTED + ORFANO doc |
| **US-33 Agent Builder** | Aaron (superadmin) / Marco | `sp7-agent-proposals-list.html` + 3 | `sp7-agent-builder-{create,test,edit}.html`, `sp7-library-game-agent.html` | SP7 brief Wave 2+3 | 🚫 NOT IMPLEMENTED |
| **US-41 Notifications system** | Marco | `sp7-notifications-hub.html` | `sp7-notifications-preferences.html` (shipped) | SP7 brief Wave 3 | ⚠ partial |

> **Nota CRITICA — filename mismatch**: il brief SP7 chiama `sp7-game-night-create.{html,jsx}` mentre
> `MOCKUPS_INDEX.md` riporta `sp7-game-night-new.html` (filename effettivo nel filesystem). Questo
> spiega perché Agent C ha classificato `sp7-game-night-new.html` come ORFANO (0 doc menzioni)
> nonostante il brief SP7 lo documenti estesamente. **Vedi US-GAP-FILENAME-RENAME in §4b**.

### 1.3 Dove vivono le US (insight Cockburn)

Le US **non vivono nel codice** — esistono esclusivamente in `docs/` + `admin-mockups/briefs/`:

| Sorgente | Tipo di US | Esempio |
|---|---|---|
| `docs/for-developers/workflows/us-verification-protocol.md` | Verification queue (10 US legacy) | "1. US-2 Login (entry point ogni sessione)" |
| `docs/superpowers/specs/2026-06-12-us-int-1-kb-onboarding-spec.md` | Integration scenario full | US-INT-1 template (source story + Cockburn + Gherkin + sub-decomposition) |
| `admin-mockups/briefs/SP7-game-night-agent-builder.md` | Brief mockup + US tracking | US-31/33/41 con Gherkin id `G31.1`...`G33.10` |
| `admin-mockups/design_files/sp7-*.jsx` | Embedded state Gherkin id | `gherkin:'US-31.1'` come label nelle state variant |
| `docs/for-developers/specs/*.md` | Spec-local AC refs | `AC-D.1`, `AC-E.4`, `AC-F.1-F.5` |

**Findings cross-cutting**:
- ZERO US identifier trovati in `apps/web/__tests__/`, `apps/web/e2e/`, `tests/Api.Tests/`,
  `.github/`. **Le US non sono testate funzionalmente con tracciabilità diretta.**
- `@spec-ref` annotation in codice produttivo: **solo 2 file** (`NotificationPreferences.tsx`,
  `notifications/preferences/page.tsx`). Pattern proposto da Wiegers (DS-17 panel) **non adottato**.
- 4 famiglie convivono **senza naming convention unificata**: `US-N`, `US-INT-N`, `US-X.N`, `G31.N`,
  `AC-X.N`. Wiegers: "Standardizzare prima di proliferare."

---

## Sezione 2 — Mappa mockup ↔ coverage (115 mockup)

### 2.1 Coverage matrix per cluster

Legenda colonne:
- **doc#**: numero di file in `docs/` + `audits/` che menzionano il filename mockup
- **Categoria**: `FORTE` (≥10 doc) · `LIGHT` (1-9 doc) · `ORFANO` (0 doc)
- **US linkata**: US-N identifier mappato (dalla tabella §1.2)

#### Cluster AUTH & ONBOARDING (5 page-mock)

| Mockup | doc# | Categoria | US linkata | Route |
|---|---|---|---|---|
| `onboarding.html` | 12 | FORTE | (no US-N, US-INT prerequisite) | `/welcome`, `/onboarding`, `/setup` |
| `settings.html` | 12 | FORTE | US-26 (partial) | `/settings` + 7 sub-route |
| `notifications.html` | 9 | LIGHT | US-25 + US-41 | `/notifications`, `/notifications/preferences` |
| `auth-flow.html` | 7 | LIGHT | US-2 | `/login`, `/register`, `/reset-password`, `/oauth-callback`, `/verify-email` |
| `public.html` | 6 | LIGHT | (landing public, no US directly) | `/` |

#### Cluster SP3 — Public surfaces (8 page-mock)

| Mockup | doc# | Categoria | US linkata | Route |
|---|---|---|---|---|
| `sp3-join.html` | 9 | LIGHT | (deep-link join, US-15 indirectly) | `/join`, `/sessions/join` |
| `sp3-faq-enhanced.html` | 7 | LIGHT | (no US-N) | `/faq`, `/games/[id]/faqs` |
| `sp3-legal.html` | 7 | LIGHT | (no US-N) | `/privacy`, `/terms`, `/cookies` |
| `sp3-how-it-works.html` | 6 | LIGHT | (no US-N) | `/how-it-works` |
| `sp3-shared-game-detail.html` | 5 | LIGHT | (US-INT-1 partial) | `/shared-games/[id]` |
| `sp3-library-public.html` | 4 | LIGHT (forward-refactor, designer review #2209) | (no US-N — NEW route) | `/library-public`, `/library/shared/[token]` |
| `sp3-shared-games.html` | 4 | LIGHT | (no US-N — discovery) | `/shared-games` |
| `sp3-accept-invite.html` | 3 | LIGHT | (no US-N) | `/accept-invite`, `/invites/[token]` |

#### Cluster SP4 CORE — Games & Library (10 page-mock)

| Mockup | doc# | Categoria | US linkata | Route |
|---|---|---|---|---|
| `sp4-game-detail.html` | 17 | **FORTE** | US-9 | `/games/[id]`, `/library/[gameId]`, `/private-games/[id]` ⚠ (N4 routing conflict) |
| `sp4-dashboard.html` | 17 | **FORTE** | US-6 (superseded asse-C) | `/dashboard` |
| `sp4-library-desktop.html` | 12 | **FORTE** | US-10 | `/library` (desktop) |
| `sp4-games-index.html` | 7 | LIGHT | US-8 | `/games` (Discover default tab post asse-D P2) |
| `sp4-discover.html` | 7 | LIGHT | US-8 + US-INT-1 entry | `/discover` |
| `sp4-kb-detail.html` | 8 | LIGHT (forward-refactor) | (no US-N) | `/knowledge-base/[id]` (deferred — G4 v3 pivot) |
| `sp4-upload-wizard-extended.html` | 6 | LIGHT | US-INT-1 step 4 | `/upload`, `/gamebook/upload` |
| `sp4-kb-hub.html` | 5 | LIGHT | (no US-N) | `/knowledge-base` |
| `sp4-library-mobile.html` | 3 | LIGHT (forward-refactor #2216) | US-10 (mobile variant) | `/library` (mobile <768px) |
| `sp4-kb-global.html` | **0** | **ORFANO** | (no US-N) | `/knowledge-base/global` (NEW route from DS-17-13) |

#### Cluster SP4 ADMIN (Agents + Toolkit) (3 page-mock)

| Mockup | doc# | Categoria | US linkata | Route |
|---|---|---|---|---|
| `sp4-agent-detail.html` | 7 | LIGHT | US-33 (partial) | `/agents/[id]`, `/library/[gameId]/agent` |
| `sp4-agents-index.html` | 6 | LIGHT | US-33 | `/agents`, `/editor/agent-proposals/*` |
| `sp4-toolkit-detail.html` | 5 | LIGHT | (no US-N) | `/toolkit/*`, `/library/[gameId]/toolbox` |

#### Cluster SP4 PLAYERS & SESSIONS index (4 page-mock)

| Mockup | doc# | Categoria | US linkata | Route |
|---|---|---|---|---|
| `sp4-player-detail.html` | 11 | **FORTE** | US-26 (self-view reuse) | `/players/[id]`, `/players/[id]/*` |
| `sp4-sessions-index.html` | 10 | **FORTE** | (no US-N — landing) | `/sessions`, `/games/[id]/sessions` |
| `sp4-game-nights-index.html` | 8 | LIGHT | (no US-N — landing for US-31) | `/game-nights` |
| `sp4-players-index.html` | 6 | LIGHT | (no US-N — community) | `/players` |

#### Cluster SP4 PLAY RECORDS (5 page-mock — **TUTTI ORFANI**)

| Mockup | doc# | Categoria | US linkata | Route |
|---|---|---|---|---|
| `sp4-play-records-index.html` | **0** | **ORFANO** | US-32 (no spec) | `/play-records` |
| `sp4-play-records-new.html` | **0** | **ORFANO** | US-32 | `/play-records/new` |
| `sp4-play-records-detail.html` | **0** | **ORFANO** | US-32 | `/play-records/[id]` |
| `sp4-play-records-edit.html` | **0** | **ORFANO** | US-32 | `/play-records/[id]/edit` |
| `sp4-play-records-stats.html` | **0** | **ORFANO** | US-32 | `/play-records/stats` |

> **Cluster critico**. US-32 è ancora dichiarata "bloccante" nell'audit gaps 2026-05-22 §N2, ma
> **NESSUNA spec dedicata esiste**. Vedi US-INT-2 in §4a.

#### Cluster SP4 SESSIONS — Skeleton + game-flavored (16 page-mock)

| Mockup | doc# | Categoria | Route | Note |
|---|---|---|---|---|
| `sp4-session-skeleton-live.html` | 4 | LIGHT | `/sessions/[id]/live` (generic) | Polymorphic — closes #1750 B19-4b |
| `sp4-session-summary-skeleton.html` | **0** | **ORFANO** | `/sessions/[id]` (generic) | Companion to skeleton-live |
| `sp4-session-wingspan-live.html` | 1 | LIGHT | (route alias via `?tab=`) | Premium #1/7, sole game con doc coverage |
| `sp4-session-wingspan-summary.html` | 1 | LIGHT | (route alias via `?tab=`) | Premium #1/7 |
| `sp4-session-catan-live.html` | 2 | LIGHT | (deferred Phase C-3) | Premium #3/7 |
| `sp4-session-catan-summary.html` | 1 | LIGHT | (deferred Phase C-3) | Premium #3/7 |
| `sp4-session-puerto-rico-live.html` | **0** | **ORFANO** | (deferred Phase C-3) | Premium #2/7 |
| `sp4-session-puerto-rico-summary.html` | **0** | **ORFANO** | (deferred Phase C-3) | Premium #2/7 |
| `sp4-session-power-grid-live.html` | **0** | **ORFANO** | (deferred Phase C-3) | Premium #4/7 |
| `sp4-session-power-grid-summary.html` | **0** | **ORFANO** | (deferred Phase C-3) | Premium #4/7 |
| `sp4-session-zombicide-live.html` | **0** | **ORFANO** | (deferred Phase C-3) | Premium #5/7 |
| `sp4-session-zombicide-summary.html` | **0** | **ORFANO** | (deferred Phase C-3) | Premium #5/7 |
| `sp4-session-paleo-live.html` | **0** | **ORFANO** | (deferred Phase C-3) | Premium #6/7 |
| `sp4-session-paleo-summary.html` | **0** | **ORFANO** | (deferred Phase C-3) | Premium #6/7 |
| `sp4-session-codenames-live.html` | **0** | **ORFANO** | (deferred Phase C-3) | Premium #7/7 |
| `sp4-session-codenames-summary.html` | 1 | LIGHT | (deferred Phase C-3) | Premium #7/7 |

> **Cluster con 8 orfani**. La spec DS-17-15 #2231 ufficializza "skeleton-first, per-game deferred
> Phase C-3 #2234". L'absence di doc per i game-flavored è **scope-aligned**, ma il gap è che né lo
> skeleton-live né i game-flavored hanno una US che li motivi end-to-end. Vedi **US-INT-4** in §4a.

#### Cluster SP5 — Profile (1 page-mock)

| Mockup | doc# | Categoria | US linkata | Route |
|---|---|---|---|---|
| `sp5-profile-settings.html` | 4 | LIGHT | US-26 | `/profile?tab=settings` + sub-section |

#### Cluster SP7 — Game Nights (4 page-mock + 1 component-mock)

| Mockup | Tipo | doc# | Categoria | US linkata | Route |
|---|---|---|---|---|---|
| `sp7-game-night-detail-rsvp.html` | page-mock | 8 | LIGHT | US-15 + US-31 | `/game-nights/[id]` |
| `sp7-game-night-live.html` | page-mock | 4 | LIGHT | US-31 (extension #487) | `/game-nights/[id]/live` |
| `sp7-game-night-summary.html` | page-mock | 1 | LIGHT | US-31 (extension #487) | `/game-nights/[id]/summary` |
| `sp7-game-night-new.html` | page-mock | **0** ⚠ | **ORFANO (filename mismatch)** | US-13 + US-31 | `/game-nights/new` |
| `sp7-game-night-transition.html` | component-mock | (not scanned — modal) | n/a | US-31 (ext. #487) | modal opened from `/game-nights/[id]/live` |

> **Nota su `sp7-game-night-new.html`**: il brief SP7 (`SP7-game-night-agent-builder.md`) lo
> documenta come Wave 1 mockup A con nome `sp7-game-night-create.{html,jsx}`. Discrepanza
> filesystem ↔ brief → grep filename non trova match. **Vedi US-GAP-FILENAME-RENAME**.
>
> **Nota su `sp7-game-night-edit.html`**: documentato nel brief SP7 Wave 1 mockup C
> (`sp7-game-night-edit.{html,jsx}`) ma **non presente nel filesystem post-DS-17 Phase B audit**.
>
> **DISPOSITION 2026-06-16 (#2344 closed)**: ratificata **Option C — drawer overlay**.
> Edit non è un mockup separato; vive come drawer triggered da `sp7-game-night-detail-rsvp` via deep link `?action=edit`. Vedi **ADR-079** per il pattern canonico.
> Le funzionalità di edit risultano consolidate sotto `/game-nights/[id]` (route `[id]/edit` mappa
> al detail-rsvp page-mock per MOCKUPS_INDEX.md). US-INT-3 cita questo file come "Refs planned" non
> ancora committato. **Vedi sub-spec US-INT-3c per scope finale.**

#### Cluster CHAT (1 page-mock)

| Mockup | doc# | Categoria | US linkata | Route |
|---|---|---|---|---|
| `chat-fullscreen.html` | 5 | LIGHT | US-27 + US-INT-1 step 7 | `/chat/[threadId]`, `/chat/new` |

#### Cluster NANOLITH — Libro-game (14 page-mock + 3 primitive)

| Mockup | doc# | Categoria | US linkata | Note |
|---|---|---|---|---|
| `librogame-runthrough-game-onboarding.html` | 8 | LIGHT | (Aaron persona, US-32-adjacent) | `/library/[gameId]` libro variant |
| `librogame-runthrough-resume-picker.html` | 7 | LIGHT | (Aaron) | `/library/[gameId]/play` |
| `librogame-runthrough-setup-wizard.html` | 5 | LIGHT | (Aaron) | `/sessions/new` |
| `librogame-runthrough-translate-viewer.html` | 5 | LIGHT | (Aaron, ref Mockup Refinement spec) | `/library/[gameId]/play/[campaignId]/translate` |
| `librogame-game-night-storyboard.html` | 4 | LIGHT | (Aaron + US-31 cross) | `/game-nights/[id]` storyboard variant |
| `librogame-runthrough-game-detail.html` | 3 | LIGHT | (Aaron) | `/library/[gameId]` libro variant |
| `librogame-runthrough-play-session.html` | 3 | LIGHT | (Aaron, US-32-adjacent) | `/library/[gameId]/play/[campaignId]` |
| `librogame-runthrough-setup-chat.html` | 2 | LIGHT | (Aaron) | `/chat/new`, `/chat/[threadId]` setup variant |
| `librogame-runthrough-session-end.html` | 2 | LIGHT | (Aaron) | `/sessions/live/[sessionId]` end-state |
| `librogame-runthrough-encounter-cheatsheet.html` | 2 | LIGHT | (Aaron) | `/library/[gameId]/play/[campaignId]/encounter` |
| `primitive-nav-chat-panel.html` | 1 | LIGHT | (cross-route — used globally) | component-mock |
| `librogame-runthrough-library-search.html` | 1 | LIGHT | (Aaron) | component-mock |
| `primitive-nav-bottom-mobile.html` | **0** | **ORFANO** | (cross-route primitive) | component-mock |
| `primitive-nav-topbar.html` | **0** | **ORFANO** | (cross-route primitive) | component-mock |

### 2.2 Top 10 mockup per coverage (mix FORTE + LIGHT high)

> 7 FORTE (≥10 doc) + 3 LIGHT (8-9 doc, top per cluster). Linea di demarcazione FORTE/LIGHT tra
> rank 7 e 8.

| Rank | Mockup | doc# | Categoria | US | Cluster |
|---|---|---|---|---|---|
| 1 | `sp4-game-detail.html` | 17 | FORTE | US-9 | SP4 core |
| 1 | `sp4-dashboard.html` | 17 | FORTE | US-6 | SP4 core |
| 3 | `sp4-library-desktop.html` | 12 | FORTE | US-10 | SP4 core |
| 3 | `onboarding.html` | 12 | FORTE | (US-INT prereq) | Auth |
| 3 | `settings.html` | 12 | FORTE | US-26 | Auth |
| 6 | `sp4-player-detail.html` | 11 | FORTE | US-26 (self-view) | SP4 players |
| 7 | `sp4-sessions-index.html` | 10 | FORTE | (landing) | SP4 sessions |
| 8 | `sp3-join.html` | 9 | LIGHT | (US-15 deep-link) | SP3 |
| 8 | `notifications.html` | 9 | LIGHT | US-25 + US-41 | Auth |
| 10 | `sp7-game-night-detail-rsvp.html` | 8 | LIGHT | US-15 + US-31 | SP7 |

### 2.3 Lista 19 ORFANI (0 doc menzioni)

**Priorità ALTA** (production user-facing routes):

1. `sp4-play-records-index.html` — US-32 P1 — `/play-records`
2. `sp4-play-records-new.html` — US-32 P1 — `/play-records/new`
3. `sp4-play-records-detail.html` — US-32 P1 — `/play-records/[id]`
4. `sp4-play-records-edit.html` — US-32 P1 — `/play-records/[id]/edit`
5. `sp4-play-records-stats.html` — US-32 P1 — `/play-records/stats`
6. `sp7-game-night-new.html` — US-13/31 P1 — `/game-nights/new` ⚠ filename mismatch
7. `sp4-kb-global.html` — (no US) — `/knowledge-base/global` (NEW route DS-17-13)

**Priorità MEDIA** (deferred Phase C-3 per-game — 9 file orfani out of 14 game-flavored; Wingspan-live+summary, Catan-live+summary, Codenames-summary hanno LIGHT cov):

8. `sp4-session-puerto-rico-live.html` — (Premium #2/7) — `/sessions/[id]/live?game=puerto-rico`
9. `sp4-session-puerto-rico-summary.html` — (Premium #2/7) — `/sessions/[id]?game=puerto-rico`
10. `sp4-session-power-grid-live.html` — (Premium #4/7) — `/sessions/[id]/live?game=power-grid`
11. `sp4-session-power-grid-summary.html` — (Premium #4/7) — `/sessions/[id]?game=power-grid`
12. `sp4-session-zombicide-live.html` — (Premium #5/7) — `/sessions/[id]/live?game=zombicide`
13. `sp4-session-zombicide-summary.html` — (Premium #5/7) — `/sessions/[id]?game=zombicide`
14. `sp4-session-paleo-live.html` — (Premium #6/7) — `/sessions/[id]/live?game=paleo`
15. `sp4-session-paleo-summary.html` — (Premium #6/7) — `/sessions/[id]?game=paleo`
16. `sp4-session-codenames-live.html` — (Premium #7/7) — `/sessions/[id]/live?game=codenames`
17. `sp4-session-summary-skeleton.html` — companion del skeleton-live LIGHT-coperto

**Priorità BASSA** (primitives cross-route):

18. `primitive-nav-bottom-mobile.html` — global primitive, deve essere doc'd
19. `primitive-nav-topbar.html` — global primitive, deve essere doc'd

---

## Sezione 3 — Gap identificati

### 3.1 Famiglie orfane critiche (4 cluster)

**Cluster A — Play Records family** (5 mockup orfani):
- US-32 dichiarata P1 bloccante in audit gaps 2026-05-22 §N2 ma SENZA spec dedicata.
- Brief SP7 NON copre play-records (focus su game-nights/agent/notif).
- Cockburn: "Play records sono il *cuore* del Core Game Loop — Marco registra come è andata. Senza
  questa US, il loop sociale del game-night non si chiude."
- **Azione proposta**: US-INT-2 full + 5 US-GAP-PR-* compact.

**Cluster B — Session game-flavored** (8 mockup orfani):
- Spec DS-17-15 #2231 dichiara "skeleton-first, per-game deferred Phase C-3 #2234".
- Scope aligned ma manca una US generica per il polymorphic skeleton + N US per-game.
- Adzic: "I 7 GAP audit del 2026-06-13 (G1-G7 per /sessions/[id]) sono *exactly* il segnale che il
  test artifact non esiste — solo il design."
- **Azione proposta**: US-INT-4 full (skeleton generic) + 5 US-GAP-SESS-* compact (game-flavored
  raggruppato).

**Cluster C — KB global view** (1 mockup orfano):
- `sp4-kb-global.html` è NEW route da DS-17-13 spec (#2220).
- US guida: scoperta KB cross-game (es. "voglio vedere tutti i KB indicizzati nel sistema").
- **Azione proposta**: US-GAP-KB-GLOBAL compact.

**Cluster D — Navigation primitives** (2 mockup orfani):
- `primitive-nav-bottom-mobile.html` + `primitive-nav-topbar.html` sono component-mock cross-route.
- Asse-B #1897 li ha shipped (MainSidebar 8 voci + DesktopShell + cascadeNavigationStore) ma il
  filename match non è 1:1.
- **Azione proposta**: US-GAP-PRIMITIVE-NAV compact (verifica che impl shipped match il mockup).

### 3.2 Gap di stato (default/empty/loading/error/sse) — cross-cutting

Dal audit gaps 2026-05-22 §P3:

| Stato | Coverage attuale | Gap principali |
|---|---|---|
| **Empty** | `/library`, `/library/wishlist`, `/discover` ✓ | mancano: `/play-records`, `/editor/agent-proposals`, `/notifications`, `/toolkit/history` |
| **Error** | `librogame-runthrough-error-states` (chat/translate/encounter only) | mancano: `/sessions/live` SSE-disconnect, `/upload` PDF-corrupt, `/games/[id]` no-data |
| **Loading** | Componenti impl (es. `WishlistSkeleton`), non specificati in mockup | discrepancy spec/impl globale |
| **Permission-denied** | `auth-flow.html` (login only) | mancano: tier-locked features, suspended-account, expired-session inline |
| **Network-offline** | `librogame-runthrough-error-states` parziale | mancano: offline cache per `/library`, `/play-records`, `/notifications` |

**Audit 2026-06-13 spec-panel — `/sessions/[id]` 7 GAP** (richiamato in Batch 8 spec):
- G1: layout 3-column desktop non implementato
- G2: URL pattern child routes vs query param `?tab=`
- G3: ChatAgent always-visible vs tab separata
- G4: TopBar universale con live timer + connection status mancante
- G5: Polymorphic renderers non astratti (Scoring/Turn/Toolkit)
- G6: Zero game-specific extension implementate
- G7: 5 stati canonici non standardizzati

Vedi **US-INT-4** in §4a.

### 3.3 Designer review pendings (3 mockup forward-refactor)

3 mockup hanno `design_intent: forward-refactor` con designer review tracking issue ma **non firmati**:

| Mockup | Tracking issue | Status |
|---|---|---|
| `sp3-library-public.html` | #2209 | OPEN (designer review pending) |
| `sp4-library-mobile.html` | #2216 | OPEN (designer review pending) |
| `sp4-kb-detail.html` | #2311 | OPEN (forward-refactor split-view, via DS-17-13) |

> **Più ampio gap residuo asse-***: il body umbrella #1895 ha "Designer Review Tracking Matrix"
> con 15 route in stato "TBD" (mai firmate dal designer). Umbrella chiusa sess.37 con DEC-3
> acceptance gate non rispettato. Vedi **US-INT-6 Designer Review Acceptance Gate**.

### 3.4 Decisioni architetturali aperte (non-mockup)

| # | Decisione | Audit ref | Stato |
|---|---|---|---|
| N4 | Routing conflict `/private-games/[id]` vs `/library/private/[privateGameId]` | audit 2026-05-22 §P2 | OPEN |
| N6 | Pattern tabs-as-state vs tabs-as-route per `/games/[id]/{reviews,rules,strategies}` | audit 2026-05-22 §P2 | **CLOSED** via ADR-061 (7 canonical tabs) |
| DS-16 | Token bridge removal (`--bg-base`, `--gaming-*`, `--nh-*`, `--e-*`) | DS-17 panel DEC-5 | BLOCKED-BY DS-17 |
| Filename | Inconsistency `sp7-game-night-new` ↔ `sp7-game-night-create` | (this doc, novel) | OPEN — vedi US-GAP-FILENAME-RENAME |

### 3.5 Filename mismatch — gap novel (CRIT-novel)

Scoperto in questo doc tramite cross-reference Agent C + Agent A:

| Mockup file (filesystem) | Brief / spec doc | Status |
|---|---|---|
| `sp7-game-night-new.html` | brief SP7 cita `sp7-game-night-create` | INCONSISTENCY |

**Verifica raccomandata**: estendere lo scan a tutti gli `sp7-*`, `sp4-*` e ipotizzare che altri
mockup possano avere brief filename ≠ filesystem filename. Wiegers: "Non possiamo audit-are
'orfani' se la nostra fonte di nomi è inconsistente."

---

## Sezione 4a — US-INT-* full format (top 5)

### US-INT-2 — Play Records Lifecycle (Marco logs Saturday boardgame night)

#### Source story

> Marco è tornato dalla serata "Sabato boardgame con i Padovani" — hanno giocato Twilight Imperium
> (4h) e Brass Birmingham (3h). Apre l'app domenica mattina, vuole registrare cosa è successo:
> chi ha vinto, chi era presente, gli highlight. Poi guarda le statistiche del gruppo per vedere
> se Davide ha davvero vinto 3 volte di fila a Brass.

#### Cockburn happy path (7 step)

| # | Step | Actor | System | Acceptance criteria |
|---|---|---|---|---|
| 1 | Login + landing dashboard | Marco | Auth + redirect | session valida; dashboard mostra "Game Night Sabato — completata 12h fa" CTA "Registra il riepilogo" |
| 2 | Apri play-records hub | Marco clicca CTA o sidebar voice | `/play-records` renderizza | tab default "Recenti" mostra 3 game night completate senza record |
| 3 | Crea nuovo record | Marco clicca "Aggiungi record" | `/play-records/new` renderizza | form pre-fillable con GameNight ID + sessioni linked |
| 4 | Compila scoreboard | Marco inserisce winner, score, MVP, durata effettiva | `/play-records/new` accumula payload | autosave ogni 30s; toast "✓ Bozza salvata" |
| 5 | Aggiungi highlight + foto | Marco upload 3 foto + note testuali "Davide ha bloccato la mia rotta" | upload pipeline (S3) | foto max 5MB; OCR opzionale per scoresheet card; preview inline |
| 6 | Submit + redirect a detail | Marco clicca "Salva record" | `/play-records/[id]` renderizza | record visibile + chip "Condividi" + link a stats |
| 7 | Visualizza stats aggregati | Marco clicca "Statistiche" | `/play-records/stats` | filtro per gioco; trend mensile; "Brass: 3 vittorie consecutive Davide" insight pulsing |

#### Refined acceptance criteria (full Given/When/Then)

```gherkin
Given Marco (User role) has session valida
And Marco è host di GameNight "gn-sabato-padovani" (completata 12h fa, 2 sessioni linkate)
And GameNight ha 0 PlayRecord associati
And Marco ha 6 giocatori in roster (Marco + Giulia + Davide + Luca + Sara + Federica)

When Marco apre /dashboard
Then Sezione "Recenti" mostra GameNight completata con CTA "Registra il riepilogo →"
And CTA deep-link a /play-records/new?gameNightId=gn-sabato-padovani

When Marco clicca CTA
Then Within 1s /play-records/new renderizza
And Form è pre-filled con GameNight ID + roster + 2 sessioni Twilight Imperium + Brass

When Marco compila scoreboard sessione 1 (Twilight Imperium):
  | Player    | Score | Position |
  | Davide    | 12    | 1        |
  | Marco     | 10    | 2        |
  | Giulia    | 9     | 3        |
  | Luca      | 7     | 4        |
And Marco compila scoreboard sessione 2 (Brass Birmingham):
  | Player    | VP    | Position |
  | Davide    | 153   | 1        |
  | Sara      | 140   | 2        |
  | Marco     | 128   | 3        |
  | Federica  | 105   | 4        |
And Marco aggiunge highlight testuale "Davide ha bloccato la mia rotta nel turno 6"
And Marco upload 3 foto (scoresheet + tavola Brass + party shot)

Then Autosave parte ogni 30s; toast "✓ Bozza salvata 14:32"
And Foto upload mostra progress bar; OCR opzionale per scoresheet detect score 153 → match input

When Marco clicca "Salva record"
Then Within 2s /play-records/[id] renderizza
And Record è pubblico per il roster (default privacy)
And Banner "🎉 Record salvato — Davide MVP della serata"
And Chip "Condividi" copia link al record

When Marco clicca "Statistiche" sidebar
Then /play-records/stats renderizza
And Filtro game = "Brass Birmingham" attivo by default (last played)
And Insight pulsing "Davide ha vinto 3/3 a Brass — chiedi alle regole house?"
And Trend mensile mostra 8 record ultimi 30gg
```

#### Decomposition (4 sub-spec)

- **US-INT-2a — Play Records hub list + filter** (`sp4-play-records-index.html`) — P1 · S (3gg)
- **US-INT-2b — Play Records create form + autosave** (`sp4-play-records-new.html`) — P1 · M (5gg)
- **US-INT-2c — Play Records detail + share** (`sp4-play-records-detail.html` + `-edit`) — P1 · M (4gg)
- **US-INT-2d — Play Records stats aggregator** (`sp4-play-records-stats.html`) — P2 · M (4gg)

Total effort: ~16gg + upstream dependency su GameNight completion lifecycle (US-31 implementation).

#### Required ADRs

1. **ADR-N** — PlayRecord ownership model: host-edit vs all-players-edit (DDD: SessionTracking BC vs
   SharedGameCatalog BC ownership)
2. **ADR-N+1** — Photo upload pipeline: S3 path + OCR opt-in + dedup hash strategy
3. **ADR-N+2** — Stats aggregation refresh: real-time vs cached (Redis TTL) vs nightly batch

#### Blocker dependencies

- **US-31 implementation** — PlayRecord linked to GameNight, GameNight non implementato → blocker
- **#2176** — P0 dashboard counter mismatch (può impattare il counter "12h fa")
- **#1903 BGG ban** — non blocker (PlayRecord usa catalog interno, non BGG)

#### Failure modes (Nygard)

1. Upload foto >5MB → resize automatico client-side o reject?
2. OCR fail → manual fallback con clear UX feedback
3. Concurrent edit (host edita mentre Davide aggiunge highlight) → optimistic concurrency con `xmin` (per ADR-060 pattern)
4. GameNight cancelled mid-record → record orphan policy (mantieni vs cascade-delete)
5. Stats query slow on large dataset → P95 latency budget + skeleton state
6. Privacy: record condiviso con guest player non-User-linked → access control

**Observability**: 3+ Prometheus metrics (`meepleai_playrecord_create_total`, `_save_duration_ms`,
`_photo_upload_failures_total`) + Grafana panel.

#### Spec-panel verdict

⚠ **US-INT-2 NON è READY** — manca ADR ownership model + GameNight upstream + 5 mockup orfani da
documentare prima. Apertura sub-issue 2a-2d post-resolution ADR.

#### Refs

- Mockup canonici (5 orfani): `sp4-play-records-{index,new,detail,edit,stats}.html`
- Audit baseline: `docs/for-developers/audits/2026-05-22-mockup-gaps.md` §N2
- US-32 reference (no spec, this doc closes the gap)

---

### US-INT-3 — Game Night End-to-End (Marco creates Saturday night)

#### Source story

> È mercoledì sera. Marco vuole organizzare la serata di sabato. Apre l'app, crea una nuova game
> night, sceglie 3 candidati gioco, invita 6 amici. Riceve RSVP nei giorni successivi. Sabato sera
> avvia la sessione live, traccia 2 partite, conclude la serata e visualizza il summary condiviso.

#### Cockburn happy path (8 step)

| # | Step | Actor | System | Acceptance criteria |
|---|---|---|---|---|
| 1 | Crea GameNight | Marco | `/game-nights/new` wizard | 4 step (Quando/Dove/Chi/Cosa); StepIndicator riusato pattern SP6 |
| 2 | Invia inviti | Marco click "Crea e invia inviti" | Email + in-app notif + magic link RSVP | 6 inviti delivery ≤30s per ognuno |
| 3 | RSVP one-tap | Davide | `/game-nights/[id]` da deep-link email | bottoni grandi [Ci sarò ✅] [Forse 🤔] [Non posso ❌] |
| 4 | Voting candidati | Marco + RSVP-confermati | Tab 2 voting in `sp7-game-night-detail-rsvp` | Voti chiudono 1h prima evento |
| 5 | Avvia live hub | Marco click "Inizia serata" | `/game-nights/[id]/live` | 3-pane desktop / swipeable tabs mobile; status pulsing |
| 6 | Transition tra game | Marco click "Transition →" | `sp7-game-night-transition` modal | 2-col split recap last + preview next; KB rules quick-glance |
| 7 | End night | Marco click "Termina serata" | ConfirmModal + redirect | `sp7-game-night-summary.html` renderizza |
| 8 | Summary condiviso | Marco + roster | `/game-nights/[id]/summary` | KPI grid + cross-game timeline + per-game recap + foto gallery; CTA "Condividi" |

#### Refined acceptance criteria (full Given/When/Then)

```gherkin
Given Marco è host attivo
And ha 6 contatti registrati nel gruppo (5 user-linked + 1 guest email)
And library ha 3 game candidati: Twilight Imperium, Brass Birmingham, Spirit Island

When Marco clicca "Pianifica serata" da /dashboard CTA "Prossimi"
Then /game-nights/new renderizza wizard 4-step desktop (split-form) o mobile (multistep)

When Marco compila Step 1: sabato 20 maggio 21:00, durata "Tutto il giorno"
And Step 2: Casa Marco
And Step 3: 5 user-linked + 1 guest "francesco@example.com"
And Step 4: 3 candidates (TI, Brass, Spirit)
Then Step Review mostra summary read-only
And "Crea e invia inviti" CTA primary

When Marco clicca CTA
Then Within 30s: 6 email delivered (5 magic link + 1 guest signup link)
And 6 in-app notifications creati
And /game-nights/[id] renderizza con state-01 (3/6 RSVP attesi)
And GameNight status badge "🟠 Pianificata pulsing"

When Davide riceve email, clicca link, vede /game-nights/[id]
And Davide RSVP "Ci sarò"
Then In-app notif a Marco "🎮 Davide ha confermato RSVP"
And Counter aggiorna 4/6
And Tab 1 mostra Davide ✅ avatar

When Marco clicca "Voting" tab 24h prima evento
Then 3 candidates con voting bar
And user vota "+1 Brass Birmingham"
And Voting closes 1h prima evento (sat 20:00)

When sat 21:00 Marco arriva, clicca "Inizia serata"
Then GameNight transition status "🟠 Pianificata" → "🟢 In corso pulsing"
And /game-nights/[id]/live renderizza 3-pane desktop
And Game candidati vista left pane (Brass selected by vote winner)
And First session creata automatically /sessions/[live-id]

When Marco completa Brass session
And clicca "Transition →"
Then Modal mostra recap Brass winner Davide + preview "Twilight Imperium" rules quick-glance KB
And CTA "Avvia prossima session"

When Marco completa TI session, clicca "Termina serata"
Then ConfirmModal "Sei sicuro?"
And Conferma → /game-nights/[id]/summary redirect
And Banner "🏆 MVP della serata: Davide"
And Cross-game timeline visibile
And Roster riceve in-app notif "Serata terminata — vedi riepilogo"
```

#### Decomposition (6 sub-spec)

- **US-INT-3a — GameNight create wizard** (SP7-A) — P1 · L (5-7gg) · NEW route create
- **US-INT-3b — GameNight detail + RSVP** (SP7-B) — P1 · L (5-7gg) · invariante max 1 live
- **US-INT-3c — GameNight edit + reschedule** (SP7-C) — P2 · M (3gg) · **ratified Option C drawer overlay** (#2344 closed 2026-06-16, see ADR-079) — no standalone `sp7-game-night-edit` mockup commission; edit lives as drawer triggered from `sp7-game-night-detail-rsvp` via `?action=edit` deep link
- **US-INT-3d — GameNight live hub** (SP7-K, #487) — P1 · L (5-7gg) · 3-pane + diary
- **US-INT-3e — GameNight transition modal** (SP7-L, #487) — P2 · S (2gg)
- **US-INT-3f — GameNight summary** (SP7-M, #487) — P1 · M (4gg) · share + archive

Total effort: ~26-32gg (Wave 1 full implementation post-brief).

#### Required ADRs

1. **ADR-N** — GameNight ↔ Session aggregate boundary (vedi `2026-06-04-gamenight-session-domain-model.md` already shipped)
2. **ADR-N+1** — RSVP delivery: email transactional (Resend) + in-app dedup strategy
3. **ADR-N+2** — Voting closure mechanism: scheduled job vs lazy check on read

#### Blocker dependencies

- **#1896 asse-A semantic alignment** — CLOSED sess.32 (invariante #10 max 1 live, StartedAt, polymorphic ScoreType)
- **#1897 asse-B UI shell + DrawerStack** — CLOSED sess.33 (MainSidebar, cascadeNavigationStore)
- **#487 game-night runtime** — Mockup K+L+M shipped, FE TODO
- **Resend email provider** — SHIPPED (#1632 staging+prod)

#### Failure modes (Nygard)

1. Email bounce per guest non-user → fallback in-app reminder per host
2. RSVP after voting closure → grace period 5min + clear error message
3. Concurrent host edit + RSVP change → optimistic concurrency `xmin`
4. Live session crash mid-game → auto-resume da last checkpoint + diary continuity
5. Transition modal aperto, network drop → local state preservation
6. Summary share: privacy guest player → opt-in per record visibility

**Observability**: 5+ Prometheus metrics + 1 Grafana dashboard (GameNight lifecycle).

#### Spec-panel verdict

⚠ **US-INT-3 PARTIALLY READY** — backend asse-A done, mockup SP7 ready, FE implementation è il
prossimo sprint Game Nights Wave 1.

#### Refs

- Mockup canonici: `sp7-game-night-{new,detail-rsvp,edit,live,transition,summary}.html`
- Brief: `admin-mockups/briefs/SP7-game-night-agent-builder.md`
- Domain model: `docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`
- Filename mismatch: `sp7-game-night-new.html` ↔ brief `sp7-game-night-create` → vedi US-GAP-FILENAME-RENAME

---

### US-INT-4 — Live Session Generic Skeleton (polymorphic renderer for any game)

#### Source story

> Marco sta organizzando una serata mista. Vuole giocare a Brass Birmingham (heavy euro, Points
> scoring) e a Codenames (party game, BinaryWin scoring). L'app deve renderizzare correttamente
> entrambe le sessioni live USANDO LO STESSO componente — perché aggiungere supporto per Catan
> domani non deve richiedere un altro deploy.

#### Cockburn happy path (6 step)

| # | Step | Actor | System | Acceptance criteria |
|---|---|---|---|---|
| 1 | Avvia session Brass | Marco | `/sessions/[id]/live` renderizza skeleton | TopBar universale con live timer + connection status |
| 2 | Skeleton dispatch ScoreType "Points" | — | `ScoringPanelRenderer` switch case | Player rail con score input numerico |
| 3 | Avvia session Codenames | Marco | stessa route, gioco diverso | skeleton renderizza WordGrid + ClueHistoryTimeline |
| 4 | Skeleton dispatch ScoreType "BinaryWin" | — | `ScoringPanelRenderer` switch case | Winner team banner red/blue |
| 5 | Future: Catan added without redeploy | Marco | `AiToolkitSuggestionDto` polimorfico server-side | Skeleton consume DTO senza modifica componente |
| 6 | End session, summary skeleton | Marco | `sp4-session-summary-skeleton.html` | Hero result + tabbed review polimorfico |

#### Refined acceptance criteria (full Given/When/Then)

```gherkin
Given Skeleton component `sp4-session-skeleton-live.html` esiste come polymorphic renderer
And ScoreType ∈ {Points, Ranking, BinaryWin, Objectives}
And TurnOrderType ∈ 7 variants
And WidgetType ∈ 6 types

When Marco avvia session Brass Birmingham con ScoreType=Points
Then Skeleton renderizza:
  - TopBar live timer 00:00 + connection ✅
  - Player rail con score input numerico
  - Action log right column
  - ChatAgent always-visible (G3 fix da audit 2026-06-13)
  - 3-column desktop layout (G1 fix)
  - 5 stati standardizzati: default · empty · loading · error · sse-reconnecting (G7 fix)

When Marco avvia session Codenames con ScoreType=BinaryWin
Then Skeleton renderizza:
  - Stesso layout 3-col
  - WordGrid invece di player score rail
  - Team panel red/blue
  - ClueHistory timeline right column tab
  - "Spymaster view" overlay opzionale

When backend aggiunge gioco "Wingspan" con flavor module solo client-side
Then Skeleton renderizza Wingspan SENZA redeploy frontend (polymorphic DTO)
And Wingspan-specific widgets (bird-card-grid) render via WidgetRenderer

When session in stato "error" (SSE drop)
Then Skeleton mostra banner red "Connessione persa — retry in 5s"
And Action log shows last successful sync
And Recovery automatic dopo reconnect

When session ends
Then /sessions/[id] redirect a summary-skeleton
And `sp4-session-summary-skeleton.html` consume same DTO
And Hero "WINNER" + tabbed review (scoreboard / diary / photos / stats) polimorfico
```

#### Decomposition (3 sub-spec)

- **US-INT-4a — Session skeleton-live polymorphic renderer** (`sp4-session-skeleton-live.html`) — P1 · L (7-10gg) · chiude 7 GAP audit 2026-06-13
- **US-INT-4b — Session summary skeleton** (`sp4-session-summary-skeleton.html`) — P2 · M (5gg) · companion
- **US-INT-4c — Per-game flavor module loader** (`AiToolkitSuggestionDto` consumer) — P2 · M (5gg) · client-side hot-load

Total effort: ~17-20gg.

#### Required ADRs

1. **ADR-N** — Polymorphic DTO schema: `AiToolkitSuggestionDto` shape + versioning
2. **ADR-N+1** — Flavor module loading: bundled vs lazy + cache TTL
3. **ADR-N+2** — Live session 5-state machine (default/empty/loading/error/sse) — formalize

#### Blocker dependencies

- **#1750 B19-4b** — skeleton-live polymorphic renderer SHIPPED in mockup form
- **#2088** — `/sessions/[id]` route 404 (audit 2026-06-13) — P0 blocking
- **#2234** — per-game session impl 7 games — Phase C-3 deferred

#### Failure modes (Nygard)

1. DTO version mismatch frontend ↔ backend → graceful fallback to generic renderer
2. Flavor module 404 → fallback al skeleton senza game-specific widgets
3. SSE reconnect storm → exponential backoff + max retry
4. Score input race condition (2 player edita stesso turn) → last-write-wins con audit log
5. State machine deadlock (loading → error → loading loop) → max 3 retry + manual escape

**Observability**: SSE drop rate, flavor module load latency, state transitions histogram.

#### Spec-panel verdict

⚠ **US-INT-4 PARTIALLY READY** — mockup skeleton-live shipped, skeleton-summary orfano (0 doc),
route /sessions/[id] 404 (#2088). 7 GAP audit aperti.

#### Refs

- Mockup canonici: `sp4-session-skeleton-{live,summary}.html` (+ 14 game-flavored deferred Phase C-3)
- 7 GAP audit: `docs/superpowers/specs/2026-06-13-batch-8-issue-execution-plan-design.md` (G1-G7)
- B19 epic chiusa con skeleton solo, game-flavored Phase C-3 #2234

---

### US-INT-5 — Notification → Deep Link Action (generalized pattern)

#### Source story

> Marco riceve notifiche su 3 canali (email, in-app, push). Vuole che ogni notifica abbia una CTA
> chiara e funzionante: "Davide ha confermato RSVP" → tap → vede game night detail. "KB indicizzato"
> → tap → apre chat agent. Senza dover navigare manualmente.

#### Cockburn happy path (5 step)

| # | Step | Actor | System | Acceptance criteria |
|---|---|---|---|---|
| 1 | Event triggers notification | (sistema) | NotificationService dedup + multi-channel dispatch | dedup per `type+entityId+userId`; max 1 invio per 60s |
| 2 | Marco riceve in-app notif | Marco | bell icon badge counter incrementa | counter mono kicker "5 nuove" |
| 3 | Marco apre notification hub | Marco click bell | `/notifications` renderizza timeline | grouping per giorno; tab filter |
| 4 | Marco tap su notification | Marco | deep link a route correlata | within 1s redirect a entity/action |
| 5 | Marco completa action | Marco | side effect (RSVP, chat, etc.) | notification marcata read; counter -1 |

#### Refined acceptance criteria (full Given/When/Then)

```gherkin
Given Notification types definiti:
  | type             | entity | deep link template            |
  | kb_indexed       | kb     | /games/{gameId}/chat?suggested=true |
  | rsvp_confirmed   | event  | /game-nights/{eventId}        |
  | agent_published  | agent  | /agents/{agentId}             |
  | session_started  | session| /sessions/{sessionId}/live    |
  | mention          | chat   | /chat/{threadId}?msg={msgId}  |

When Event "Davide ha confermato RSVP per sat" triggers backend NotificationService
Then NotificationService.send(type='rsvp_confirmed', entityId='gn-sat', userId='marco')
And Dedup check: nessuna stessa notif per Marco negli ultimi 60s
And Dispatch 3 channels: in-app immediate + email (rispetta quiet hours) + push (se preferenze)

When Marco apre /notifications
Then Timeline grouped: "Oggi" / "Ieri" / "Questa settimana"
And Notif "🎮 Davide ha confermato RSVP" mostra:
  - Severity dot green (info)
  - Relative time "3 min fa"
  - Tap target full row

When Marco tap notif
Then Deep link /game-nights/gn-sat carica
And Within 1s detail page renderizza
And Notif marcata read (status update)
And Bell badge counter -1

When Marco apre notif "🎲 KB Twilight Imperium indicizzato"
Then Deep link /games/twilight-imperium/chat?suggested=true
And Chat page pre-popola con suggested query "Iniziamo dal setup?"

When Marco è in quiet hours 22:00-08:00
Then Email NON inviata (defer al mattino digest)
And In-app shown ma push silenced
And Digest email 08:00 raggruppa notif accumulate

When network drop durante deep link redirect
Then Cache locale serve notif details + retry redirect on reconnect
```

#### Decomposition (3 sub-spec)

- **US-INT-5a — Notifications hub timeline** (`sp7-notifications-hub.html`) — P1 · M (4gg)
- **US-INT-5b — Notifications preferences UI** (`sp7-notifications-preferences.html`) — P2 · S (2gg) · *partially shipped*
- **US-INT-5c — Deep link contract + dedup engine** (backend) — P1 · M (4gg)

Total effort: ~10gg.

#### Required ADRs

1. **ADR-N** — Notification dedup strategy: in-memory cache vs Redis vs DB unique constraint
2. **ADR-N+1** — Deep link versioning: URL pattern + backward compat su redesign route
3. **ADR-N+2** — Quiet hours timezone: server-side calc vs client opt-in

#### Blocker dependencies

- **UserNotifications BC** — 24 cmd + 18 query già implementati
- **Resend email provider** — SHIPPED
- **US-INT-1c** — deep link contract Notification→Chat (per ADR-061 game-detail tab canonical)
- **#487 game-night** — necessario per `rsvp_confirmed` deep link target

#### Failure modes (Nygard)

1. Notification storm (burst 50 RSVP in 1 min) → rate limit + batch summary
2. Deep link a route che ha avuto refactor → 410 Gone + redirect mapping
3. Push notif non delivery (device offline >24h) → fallback email digest
4. User unsubscribe via email → policy: lista email unsubscribe + UI revert
5. Quiet hours timezone mismatch → user-side override + server validation

**Observability**: notif delivery rate per channel + dedup hit rate + deep link 200/404 ratio.

#### Spec-panel verdict

⚠ **US-INT-5 PARTIALLY READY** — preferences UI shipped, hub mockup ready, deep link contract per
chat resolved via ADR-061. Hub implementation pending FE.

#### Refs

- Mockup canonici: `sp7-notifications-{hub,preferences}.html`
- Brief: SP7 Wave 3 I+J
- Pattern parent: US-INT-1c (deep link contract)

---

### US-INT-6 — Designer Review Acceptance Gate (close asse-* residual)

#### Source story

> Il designer Aaron ha disegnato i mockup per asse-A/B/C/D (umbrella #1895). Le 4 implementazioni
> sono state shipped sess.32-37. Ma il body umbrella #1895 ha una "Designer Review Tracking Matrix"
> con 15 route tutte in stato TBD — Aaron non ha mai firmato. L'umbrella è stata chiusa lo stesso.
> Vogliamo un meccanismo strutturato perché questo non riaccada.

#### Cockburn happy path (5 step)

| # | Step | Actor | System | Acceptance criteria |
|---|---|---|---|---|
| 1 | Implementazione shipped | Dev team | PR merged | code in main-dev |
| 2 | Designer review request | Dev clicca "Request designer review" | issue auto-creata con checklist | PR linked + 5 stati canonici screenshot |
| 3 | Designer review session | Aaron apre route deployed staging | side-by-side mockup vs live | criteri AC: tokens / states / a11y AA / responsive |
| 4 | Designer signoff | Aaron commenta verdetto | ✅ / ⚠ / 🚫 + nota | trigger CI gate: 🚫 blocca merge a main-staging |
| 5 | Sign-off captured | Bot Github | matrix update | umbrella body matrix riga aggiorna |

#### Refined acceptance criteria (full Given/When/Then)

```gherkin
Given Umbrella #XXX ha route list nel body con "Designer Review" column
And Ogni route ha mockup canonical + impl shipped
And Designer Aaron ha access a staging environment

When Dev mergia PR "feat(dashboard): asse-C priority-driven sections"
Then Bot auto-crea issue "Designer Review: /dashboard (PR #1898-7)"
And Issue ha checklist:
  - [ ] tokens canonical (no hardcoded color)
  - [ ] 5 stati screenshot (default / empty / loading / error / sse)
  - [ ] a11y AA (axe 0 violations)
  - [ ] responsive 375/768/1024/1440
  - [ ] match mockup `sp4-dashboard.html` block-level diff ≤5%
And Issue assignee = Aaron
And Umbrella matrix row updated: "/dashboard" status = "🟡 Pending review"

When Aaron review:
  - apre staging.meepleai.app/dashboard
  - side-by-side con admin-mockups/design_files/sp4-dashboard.html
  - controlla 5 stati via storybook (post DS-17)
  - axe scan via DevTools

And Aaron comment "✅ Approved — minor: spacing card 24→28px in mobile"
Then Bot parse comment → matrix update "/dashboard" status = "✅ Approved"
And Issue closed
And Optional follow-up PR per minor

When Aaron comment "🚫 Rejected — entity color #c-game wrong shade vs mockup"
Then CI gate set "blocking_merge_main_staging" su PR collegate
And Umbrella matrix row "🚫 Blocked"
And Dev team notif + must fix before next release

When 30gg pass senza signoff (Aaron unavailable)
Then Bot auto-comment "Designer review expired — escalate"
And Default policy: ⚠ "Approved with timeout" (logged for audit)
```

#### Decomposition (3 sub-spec)

- **US-INT-6a — Designer review bot + issue auto-create** — P2 · M (4gg)
- **US-INT-6b — Umbrella matrix sync workflow** — P2 · S (2gg)
- **US-INT-6c — Staging side-by-side comparison tool** — P3 · S (3gg) · uses Storybook + Playwright

Total effort: ~9gg.

#### Required ADRs

1. **ADR-N** — Designer signoff CI gate semantics: blocking vs advisory vs timeout-default
2. **ADR-N+1** — Auto-create issue noise: thresholds + batch grouping

#### Blocker dependencies

- **DS-17 #2063** — Storybook stories abilitano side-by-side staging vs mockup
- **#1015 baseline diff** — CLOSED COMPLETED, può essere riusato come telemetry

#### Failure modes (Nygard)

1. Aaron unavailable for >30gg → timeout-default policy needs explicit ADR
2. False positive block (es. typo in commento) → manual override label
3. Storybook story missing for new route → fallback to staging-only review (no mockup compare)
4. Issue spam (50 PR / week) → batch summary issue weekly instead of per-PR

**Observability**: review turnaround time, approval rate, timeout-default frequency.

#### Spec-panel verdict

⚠ **US-INT-6 EXPLORATORY** — process improvement, low urgency ma high ROI long-term. Sblocca le 15
route asse-* unsigned + previene futuri umbrella closed-with-gate-violated.

#### Refs

- Umbrella #1895 (asse-* CLOSED but matrix unsigned)
- DS-17 panel DEC-3 — "DEC-3 acceptance gate not met but umbrella closed anyway" (drift)
- Pattern parent: nessuno — è novel process

---

## Sezione 4b — US-GAP-* compact (17 user stories)

> **Formato compact**: ~25 righe per US. Title + actor + Gherkin GWT + 3-5 AC + mockup link + stato
> + issue correlata. Per scenari completi end-to-end, vedi §4a.

### Cluster A — Play Records family (5 US-GAP)

#### US-GAP-PR-01 — Play Records hub index

**Mockup**: `sp4-play-records-index.html` (ORFANO) · **Route**: `/play-records` · **Actor**: Marco · **Issue corr.**: nessuna · **Parent**: US-INT-2a

```gherkin
Given Marco ha 12 play records ultimi 30gg
When Marco apre /play-records
Then Lista mostra 12 record card grouped by month
And Filter chip [Tutti | Vinti | Persi | Last week] funzionante
And Stato empty se 0 record: "Nessuna partita registrata ancora · Aggiungi il primo"
And Stato loading skeleton 6 card
And Stato error "Impossibile caricare" + retry
```

**AC**: (1) lista deduplicata per record_id; (2) filter persistente in URL `?filter=won`; (3) tap row → `/play-records/[id]`; (4) sort default DESC by played_at; (5) infinite scroll a 20 record/page.

**Stato copertura**: default ✗ | empty ✗ | loading ✗ | error ✗ | sse N/A

---

#### US-GAP-PR-02 — Play Records create form

**Mockup**: `sp4-play-records-new.html` (ORFANO) · **Route**: `/play-records/new` · **Actor**: Marco · **Issue corr.**: nessuna · **Parent**: US-INT-2b

```gherkin
Given Marco arriva a /play-records/new (deep link da game night completata)
When Form pre-fill da GameNight ID + sessioni
And Marco aggiunge winner + scoreboard + highlight + 3 foto
Then Autosave ogni 30s
And Validation: winner deve essere in roster
And Submit → POST /api/v1/play-records → 201 → redirect a detail
```

**AC**: (1) form draft saved in localStorage + server-side; (2) photo upload max 5MB/file, max 10 file; (3) OCR opt-in per scoresheet; (4) preview pre-submit; (5) error inline per field validation.

**Stato copertura**: default ✗ | empty N/A | loading ✗ | error ✗ | sse N/A

---

#### US-GAP-PR-03 — Play Records detail view

**Mockup**: `sp4-play-records-detail.html` (ORFANO) · **Route**: `/play-records/[id]` · **Actor**: Marco / roster · **Issue corr.**: nessuna · **Parent**: US-INT-2c

```gherkin
Given record ID "rec-2026-05-17-padovani" esiste
When utente con access apre /play-records/[id]
Then mostra hero (game cover + winner badge + date)
And scoreboard table responsive
And highlights text + foto gallery 3 cols
And CTA "Modifica" se host, "Condividi" se roster
```

**AC**: (1) access control: solo roster vede; (2) edit lock se completato >7gg (immutable); (3) share link signed token; (4) foto fullscreen modal; (5) chip "🎯 MVP Davide".

**Stato copertura**: default ✗ | empty N/A | loading ✗ | error ✗ | sse N/A

---

#### US-GAP-PR-04 — Play Records edit form

**Mockup**: `sp4-play-records-edit.html` (ORFANO) · **Route**: `/play-records/[id]/edit` · **Actor**: Marco (host only) · **Issue corr.**: nessuna · **Parent**: US-INT-2c

```gherkin
Given record creato 3gg fa (within edit window)
When Marco (host) apre /play-records/[id]/edit
Then form pre-fill da record
And diff visible: "Modificato" badge per field changed
And Submit → PUT → optimistic concurrency xmin
And Conflict 409 → "Modificato da Davide 12s fa, ricarica"
```

**AC**: (1) edit window: 7gg post creation; (2) audit trail (chi ha modificato cosa); (3) restore version (last 5); (4) cancel → no change; (5) immutable se >7gg (banner explanation).

**Stato copertura**: default ✗ | empty N/A | loading ✗ | error ✗ | sse N/A

---

#### US-GAP-PR-05 — Play Records stats aggregator

**Mockup**: `sp4-play-records-stats.html` (ORFANO) · **Route**: `/play-records/stats` · **Actor**: Marco · **Issue corr.**: nessuna · **Parent**: US-INT-2d

```gherkin
Given roster ha ≥10 records ultimi 90gg
When Marco apre /play-records/stats
Then KPI grid 4-col (totale partite / vittorie / top game / MVP overall)
And Trend chart line (partite per mese ultimo anno)
And Per-player leaderboard sortable
And Per-game stats expandable accordion
```

**AC**: (1) cached Redis 5min TTL; (2) skeleton during fetch; (3) empty state se <3 records "Servono almeno 3 partite per le statistiche"; (4) export CSV opt-in; (5) filter range custom date.

**Stato copertura**: default ✗ | empty ✗ | loading ✗ | error ✗ | sse N/A

---

### Cluster B — Session game-flavored (5 US-GAP raggruppate per gioco)

#### US-GAP-SESS-CDNS — Codenames live + summary

**Mockup**: `sp4-session-codenames-{live,summary}.html` (LIVE ORFANO §2.3 item 16, SUMMARY LIGHT 1 doc) · **Route**: `/sessions/[id]/live` + `/sessions/[id]` (via `?game=codenames`) · **Actor**: Marco team Red/Blue · **Issue corr.**: #2234 Phase C-3 · **Parent**: US-INT-4 (skeleton generic) + US-INT-4c (flavor loader) — vedi DEC-4 §0

```gherkin
Given Codenames session avviata con ScoreType=BinaryWin
When skeleton consume DTO con game=codenames + flavor module
Then WordGrid 5x5 visibile + 25 word cards
And Spymaster overlay opzionale (key card)
And TeamPanel red/blue counters (9/8 agents)
And ClueHistoryTimeline right col
```

**AC**: (1) WordCard 5 stati (covered/red/blue/neutral/assassin); (2) reveal animation 200ms; (3) clue input validation (1 word + count); (4) assassin click → game over modal; (5) score post-game "WINNER team red".

**Stato copertura**: default ⚠ (live orfano) | summary LIGHT (1 doc)

---

#### US-GAP-SESS-PG — Power Grid live + summary

**Mockup**: `sp4-session-power-grid-{live,summary}.html` (entrambi ORFANI) · **Route**: idem · **Actor**: Marco 2-6 players · **Issue corr.**: #2234 · **Parent**: US-INT-4

```gherkin
Given Power Grid session avviata 4 player
When skeleton + flavor renderizza
Then PhaseTimeline (5 phases) + ResourceMarket + PowerPlantMarket
And AuctionOverlay durante phase 2
And NetworkMap durante phase 3-4
And Elektro counter mono kicker per player
```

**AC**: (1) PhaseTimeline highlights current phase; (2) auction state machine (bid/pass/won); (3) network city placement validation; (4) resource buy with cost calc; (5) end-game: cities powered + tiebreaker Elektro.

**Stato copertura**: default ✗ | empty N/A | loading ✗ | error ✗ | sse ⚠

---

#### US-GAP-SESS-PR — Puerto Rico live + summary

**Mockup**: `sp4-session-puerto-rico-{live,summary}.html` (entrambi ORFANI) · **Route**: idem · **Actor**: Marco 3-5 players · **Issue corr.**: #2234 · **Parent**: US-INT-4

```gherkin
Given Puerto Rico session avviata 4 player
When skeleton + flavor renderizza
Then RoleSelectionBoard con 7 ruoli
And PlantationGrid + BuildingGrid per player
And GalleonsShipping + ColonistShip
```

**AC**: (1) role selection sequence; (2) role action animation; (3) plantation tile placement; (4) building construction validation; (5) end-game VP breakdown (buildings + shipped goods + bonuses).

**Stato copertura**: default ✗ | empty N/A | loading ✗ | error ✗ | sse ⚠

---

#### US-GAP-SESS-PALEO — Paleo co-op live + summary

**Mockup**: `sp4-session-paleo-{live,summary}.html` (entrambi ORFANI) · **Route**: idem · **Actor**: Marco 1-4 co-op · **Issue corr.**: #2234 · **Parent**: US-INT-4

```gherkin
Given Paleo session avviata 3 player co-op simultaneous
When skeleton + flavor renderizza
Then TribePanel + DayPhaseIndicator + CardsDeckPanel
And ActionRevealOverlay (simultaneous play)
And CavePaintingProgress (victory) + SkullCluster (defeat)
```

**AC**: (1) simultaneous action reveal animation; (2) tribe extinction game-over; (3) cave painting accumulate (5 = win); (4) skull accumulate (5 = lose); (5) cause-of-loss summary if defeat.

**Stato copertura**: default ✗ | empty N/A | loading ✗ | error ✗ | sse ⚠

---

#### US-GAP-SESS-ZOMBI — Zombicide co-op live + summary

**Mockup**: `sp4-session-zombicide-{live,summary}.html` (entrambi ORFANI) · **Route**: idem · **Actor**: Marco 1-6 co-op · **Issue corr.**: #2234 · **Parent**: US-INT-4

```gherkin
Given Zombicide Green Horde session avviata 4 survivors
When skeleton + flavor renderizza
Then SurvivorCard skill tree Blue→Yellow→Orange→Red + equipment + AP
And BoardStatePanel + CombatDicePanel + SpawnDeckIndicator
And 3-phase round timeline
```

**AC**: (1) skill tree progression XP; (2) combat dice roll animation; (3) spawn deck cards; (4) scenario objectives tracking; (5) end-game: VICTORY (objectives) or DEFEAT (cause).

**Stato copertura**: default ✗ | empty N/A | loading ✗ | error ✗ | sse ⚠

---

### Cluster C — Single orphans (4 US-GAP)

#### US-GAP-KB-GLOBAL — Knowledge Base global view

**Mockup**: `sp4-kb-global.html` (ORFANO, NEW route) · **Route**: `/knowledge-base/global` · **Actor**: Marco / Aaron · **Issue corr.**: DS-17-13 #2220 · **Parent**: nessuno

```gherkin
Given Marco apre /knowledge-base/global
Then Lista KB cross-game (filtered da quelli a cui ha access)
And Group per game + per KB type (pdf/md/web)
And Search box fulltext
And KB card mostra: title, game linked, chunk count, indexed_at, used_by_agents
```

**AC**: (1) access control (private KB visibili solo a owner); (2) search debounce 300ms; (3) sort by indexed_at DESC default; (4) bulk action [export, delete] se admin; (5) empty state se 0 KB visibili.

**Stato copertura**: default ✗ | empty ✗ | loading ✗ | error ✗ | sse N/A

---

#### US-GAP-SUMMARY-SKEL — Session summary generic skeleton

**Mockup**: `sp4-session-summary-skeleton.html` (ORFANO) · **Route**: `/sessions/[id]` (post-end) · **Actor**: Marco roster · **Issue corr.**: #1750 B19-4b · **Parent**: US-INT-4b

```gherkin
Given session ended, /sessions/[id] redirect
When skeleton consume DTO finale
Then Hero result polimorfico (WINNER team / player MVP / VICTORY-DEFEAT)
And Tabbed review: scoreboard / diary / photos / chat highlights / stats
And Layout polimorfico (Wingspan vs Paleo demo side-by-side mockup)
```

**AC**: (1) hero render dispatch su ScoreType; (2) tab default = scoreboard; (3) photos lazy load; (4) chat highlights = top 5 by reaction; (5) stats = per-player KPI grid.

**Stato copertura**: default ✗ | empty N/A | loading ✗ | error ✗ | sse N/A

---

#### US-GAP-PRIMITIVE-NAV-MOBILE — Bottom-nav mobile primitive

**Mockup**: `primitive-nav-bottom-mobile.html` (ORFANO) · **Route**: cross-route (global) · **Actor**: tutti (mobile) · **Issue corr.**: #1897 asse-B (potential impl) · **Parent**: nessuno

```gherkin
Given Marco è su mobile (<768px)
When apre qualsiasi route authenticated
Then Bottom-nav fissa visible 5 voci (Home / Library / Sessions / Game Nights / Profile)
And Active indicator pulsing su current route
And Session mode override: bar diventa "Apri sessione" pulsing
```

**AC**: (1) max-height 60px; (2) safe-area iOS padding; (3) tap target ≥48px; (4) keyboard hide automatic on focus input; (5) session mode lock visible.

**Stato copertura**: default ⚠ (asse-B partially shipped — verify match) | empty N/A | loading N/A | error N/A | sse N/A

---

#### US-GAP-PRIMITIVE-NAV-TOPBAR — Top-bar global primitive

**Mockup**: `primitive-nav-topbar.html` (ORFANO) · **Route**: cross-route (global) · **Actor**: tutti · **Issue corr.**: #1897 asse-B (AppTopBar shipped) · **Parent**: nessuno

```gherkin
Given Marco è authenticated
When apre qualsiasi route
Then TopBar fissa 52px (logo + wordmark + search + notifications + avatar)
And CommandPalette opens su CMD+K (asse-B shipped)
And SearchPill (asse-B shipped) inline
```

**AC**: (1) altezza fissa 52px desktop / 48px mobile; (2) keyboard shortcut CMD+K opens palette; (3) notifications bell badge counter; (4) avatar dropdown menu (profile/settings/logout); (5) responsive: collapse search to icon <600px.

**Stato copertura**: default ⚠ (AppTopBar shipped — verify match mockup) | empty N/A | loading N/A | error N/A | sse N/A

---

### Cluster D — Cross-cutting gap (3 US-GAP)

#### US-GAP-FILENAME-RENAME — Filename inconsistency mockup ↔ brief

**Mockup**: `sp7-game-night-new.html` (filesystem) vs `sp7-game-night-create.{html,jsx}` (brief SP7) · **Route**: `/game-nights/new` · **Actor**: Maintainer · **Issue corr.**: nessuna (novel) · **Parent**: nessuno

```gherkin
Given mockup file system path `admin-mockups/design_files/sp7-game-night-new.html`
And brief `admin-mockups/briefs/SP7-game-night-agent-builder.md` lo chiamava `sp7-game-night-create` (stale)
When grep doc per filename "sp7-game-night-new" runs (pre-PR #2351)
Then Zero match (mockup classificato ORFANO erroneamente)
```

**AC**: (1) ✅ DONE — PR #2351 ha aggiornato brief SP7 (`sp7-game-night-create` → `sp7-game-night-new`, filesystem canonical confermato); (2) ✅ DONE — MOCKUPS_INDEX.md già coerente (riga 198); (3) ✅ DONE — v2-migration-matrix.md coerente; (4) cleanup residui 10 file refs stale (PR #XXXX); (5) audit script + CI check pre-merge per prevenire futuri filename drift (Tier 6 future work).

**Action**: ✅ closed via PR #2351 + cleanup PR follow-up. Filesystem canonical: `sp7-game-night-new.html`.

---

#### US-GAP-DESIGNER-REVIEW-MATRIX — Asse-* designer signoff residual

**Mockup**: 15 routes (Designer Review Tracking Matrix in #1895 body, ALL TBD) · **Actor**: Designer Aaron · **Issue corr.**: #1895 (CLOSED but matrix unsigned) · **Parent**: US-INT-6

```gherkin
Given umbrella #1895 closed sess.37
And Designer Review Tracking Matrix ha 15 route in stato "TBD"
And DEC-3 acceptance gate non rispettato
When designer Aaron review 15 route in batch
Then Per ogni route: ✅ / ⚠ / 🚫 + nota
And Matrix update reflect signoff
```

**AC**: (1) batch review session (2h); (2) staging side-by-side mockup; (3) axe AA pass per ognuna; (4) tokens canonical verification; (5) commit matrix update via PR.

**Action**: schedule designer review session; track via US-INT-6 process automation.

---

#### US-GAP-STATE-COVERAGE — 5 stati canonical cross-cutting

**Mockup**: vari (audit 2026-05-22 §P3 + audit 2026-06-13 G7) · **Actor**: dev + designer · **Issue corr.**: B18 (`state-matrix.html` dev-fixture shipped) · **Parent**: US-INT-4

```gherkin
Given pattern "5 stati canonici" (default/empty/loading/error/sse) definito in state-matrix.html
When dev implementa nuova route
Then DEVE coprire i 5 stati applicabili (sse = N/A per route senza data source SSE)
And Storybook story per ogni stato applicabile (post DS-17)
And review manuale + axe AA per ogni stato
```

> **⚠ Declassamento DEC-A5 — 2026-06-19 (issue #2342).** Il gate CI `lint:storybook-states` **non esiste** in `apps/web/package.json` (gli unici script `lint:*` sono `lint:tokens`, `lint:tokens:mockups`, `lint:bgg`, `lint:bgg-mockups`, `lint:mockup-state-naming`, `lint:fidelity`) **e non è enforced in nessun workflow** `.github/workflows/`. Le sub-issue Tier 2 (#2347-#2350, epic #2346) che spuntavano "CI lint:storybook-states green" lo facevano contro un gate inesistente; il loro plan (`2026-06-19-issue-2346-...md`) già lo riconosceva come gate assente. DEC-A5 è quindi **declassato a review manuale, non CI-enforced**. Il gate automatico resta un futuro deliverable **non-blocking** se/quando costruito.

**AC** (post-declassamento): (1) ~~lint:storybook-states blocking~~ → **deferred** (gate CI non implementato; futuro deliverable non-blocking); (2) PR template checklist (storie 5 stati applicabili + axe + E2E) — review manuale; (3) dev-fixture state-matrix.html riusabile as reference; (4) per ogni stato applicabile un Gherkin scenario nei test E2E; (5) axe AA per ogni stato.

**Action**: enforcement attuale = review manuale + PR checklist; il gate Storybook coverage automatico resta opzionale/futuro (non-blocking).

---

## Sezione 5 — Cross-ref issue aperte

### 5.1 Mappa issue umbrella → mockup → US proposta

| Issue umbrella | Stato | Mockup impattati | US correlata in questo doc |
|---|---|---|---|
| **#2063 DS-17** Mockup-to-App Fidelity | OPEN | tutti i 127 mockup (Storybook migration) | parent generale; non duplicato qui |
| #2127 DS-17 Phase B audit | CLOSED | 224 file classificati | parent (taxonomy mockup) |
| #2071 DS-17 Phase 1 — naming stati | OPEN | 10 canonical TBD | US-GAP-STATE-COVERAGE |
| #2114 sp4-dashboard.jsx obsolete | OPEN | `sp4-dashboard.html/jsx` | US-6 verified; mockup retirement |
| #2174 DS-17 Phase D sp6-7-nano | OPEN | 6 forward-refactor stems | (deferred, fuori scope questo doc) |
| #2151 lint:bgg-mockups | OPEN | 7 mockup BGG violations | (compliance, non US) |
| #2137 BGG ToS CI gate | OPEN | (CI gate) | (compliance) |
| #2316 DS-17 Phase C #2152 Bundle-F | OPEN | URL path collapse | N4 routing decision (audit §3.4) |
| #2234 DS-17 Phase C-3 per-game session 7 games | OPEN | 14 game-flavored mockup | US-INT-4 + US-GAP-SESS-* (5) |
| #2216 Designer review sp4-library-mobile | OPEN | `sp4-library-mobile.html` | US-INT-6 (designer review process) |
| #2209 Designer review sp3-library-public | OPEN | `sp3-library-public.html` | US-INT-6 |
| #2311 sp4-kb-detail forward-refactor | OPEN | `sp4-kb-detail.html` | US-INT-6 |
| #1895 asse-* CLOSED but matrix unsigned | CLOSED | 15 route asse-* | US-INT-6 + US-GAP-DESIGNER-REVIEW-MATRIX |
| #2148 5 game-detail sub-tab placeholder | CLOSED | 5 sub-tab HTML | US-9 (game detail tabs canonical) |
| **Novel (no issue yet)** | — | `sp7-game-night-new` filename inconsistency | US-GAP-FILENAME-RENAME |
| **Novel (no issue yet)** | — | 5 play-records orfani | US-INT-2 + US-GAP-PR-01..05 |

### 5.2 Bloccanti P0/P1 residui da `/audit gaps 2026-05-22`

| # | Bloccante | Stato | US correlata |
|---|---|---|---|
| #2088 | `/sessions/[id]` route 404 | OPEN | US-INT-4 (skeleton) |
| #2176 | dashboard/library counter mismatch | OPEN | US-INT-1 (KB onboarding) |
| #492 | Community closure false-positive (achievement detail sheet) | CLOSED but residual | US-26 (parziale) |
| #491 | Chat full-screen residual | CLOSED but residual | US-27 |
| #2271 | (Batch 8 spec ref) | OPEN | (TBD scope) |

### 5.3 Issue chiuse rilevanti — residui da NON ri-aprire

| # | Closure status | Residuo | Azione |
|---|---|---|---|
| #491 | CLOSED 2026-04-20 | `sp4-dashboard` shipped, chat full-screen desktop ⚠ | mockup `chat-fullscreen.html` shipped (audit 2026-05-22 §P1 sezione 2 chiusa) |
| #492 | CLOSED 2026-04-20 | achievement detail sheet MISSING (false-positive) | US-26 partial — extend or open B11a |
| #1895 | CLOSED sess.37 | matrix unsigned (15 route) | US-INT-6 process improvement |
| #2096 | CLOSED 2026-06-11 | nessun residuo (7/7 milestones) | — |
| #1903 | CLOSED | BGG ban shipped (3-layer enforcement) | mantenere SLO=0 Prometheus metric |

---

## Sezione 6 — Reflection del Panel (socratic close)

### 🎯 COCKBURN — Sul primary actor

> *"Abbiamo iniziato chiedendoci chi è l'attore. La risposta lente-multipla è stata corretta: ogni
> US ora ha dev/QA/designer view. Ma ho una domanda residua: per le US-GAP-SESS-* (game-flavored),
> il primary actor cambia gioco per gioco (Spymaster a Codenames, host a Power Grid). Le 5 US
> compact che ho viste sono game-agnostic — vuoi una pass di refinement per declinare il primary
> actor per ognuna, o questa granularità è sufficiente per la fase Phase C-3 deferred?"*

### 📋 WIEGERS — Sulla testabilità

> *"5 US-INT-* full + 17 US-GAP-* compact = 22 US testabili. Ognuna ha AC misurabili (autosave 30s,
> P95 latency budget, error rate threshold). Ma noto due punti di tensione residui: (1) nessuna US
> ha ancora un E2E Playwright esistente — sono tutte da scrivere; (2) le AC compact possono
> degradare a 'checklist senza outcome' se non c'è enforcement CI. Suggerirei di aprire l'umbrella
> con la clausola: ogni US-INT-* deve avere almeno 1 Playwright spec prima di sub-issue close."*

### 🔍 ADZIC — Sulla concretezza dell'esempio

> *"Le 5 US-INT-* hanno tutte uno scenario concreto (Marco/Davide/Aaron + nomi gioco reali + dati
> tipo `gn-sabato-padovani`). Bene. Ma vorrei che il maintainer notasse: US-INT-2 e US-INT-3
> condividono lo stesso protagonista (Marco) e lo stesso dataset (sabato boardgame). Questo è
> intenzionale — rende le 2 US **interconnesse** (US-INT-3 completion → US-INT-2 input). Questo è
> il pattern di 'specification by example': uno scenario può rivelare gap che 10 use case astratti
> non rivelano."*

### 🧪 CRISPIN — Sulla coverage degli stati

> *"Conto: 17 US-GAP-* compact, ognuna con riga 'Stato copertura: default ✗ | empty ✗ | loading ✗
> | error ✗ | sse ✗'. Quasi tutte hanno gli stati a ✗ — ESATTAMENTE il gap che il panel del 9
> giugno ha definito come CRIT-2. Buona news: ora abbiamo una checklist concreta. Cattiva news:
> il volume di stati da definire ≈ 85 stati totali (17 US × 5 stati). Suggerirei di prioritizzare
> i 5 stati per le 5 US-INT-* full prima dei compact (25 stati = sprint singolo)."*

### 🏛️ FOWLER — Sull'architettura

> *"DEC-1 (B+C combinato in singolo doc) era la scelta giusta — 22 US in 1 file vs 22 issue
> separate GitHub = noise/signal favorisce il singolo doc. Ma noto una tensione architetturale:
> US-INT-4 (skeleton polimorfico) e US-GAP-SESS-* (5 game-flavored) sono in tension. Se il
> skeleton è davvero polimorfico via DTO, le 5 US-GAP-SESS-* NON DOVREBBERO essere user stories
> ma flavor-module spec. Suggerirei: o (a) far diventare le SESS-* sub-spec di US-INT-4c (flavor
> loader), o (b) accettare che hanno granularità diversa (US = end-to-end test, flavor spec =
> component-level test). **[Risoluzione post-review 2026-06-14: opt b, vedi DEC-4 in §0]**"*

### 🛡️ NYGARD — Sulla production reliability

> *"Ogni US-INT-* full ha sezione 'Failure modes' con 5-6 scenari + Observability metrics
> proposte. Bene. Ma noto che nessuna US-GAP-* compact ha failure modes — è il trade-off
> previsto dal format compact. Aggiungerei una riga 'Failure mode chiave: ' al format compact
> (anche solo 1 frase) per non perdere il segnale Nygard. Sui Prometheus metrics: 5 US-INT-* ×
> ~5 metrics = 25 nuovi metrics. Verifica budget cardinality con SRE prima del rollout."*

### 🎓 Synthesis — Recommendation operativa

Il documento è **PROPOSED** e pronto per:
1. **Review maintainer** (badsworm@gmail.com) — entro 7gg, verdetto su scope + format
2. **Designer review** (Aaron) — verifica filename mismatch + designer review matrix
3. **Apertura umbrella tracker** (post-approval) — parent DS-17 #2063
4. **Sub-issue opening cascade**:
   - 5 issue per US-INT-* (parent umbrella + child sub-spec)
   - 5 issue per US-GAP-PR-* (Play Records family)
   - 1 issue rename + audit per US-GAP-FILENAME-RENAME
   - Resto US-GAP-* tracked nel doc (no GH issue immediate)
5. **Integration con DS-17 #2063** — le 5 US-INT-* possono diventare Storybook story files
   under `apps/web/src/components/features/<feature>/<feature>.integration.stories.tsx`

---

## Appendice A — Statistiche finali

| Metric | Value |
|---|---|
| Mockup analizzati | 115 (esclusi 12 dev-fixture) |
| Mockup FORTE coverage (≥10 doc) | 7 |
| Mockup LIGHT coverage (1-9 doc) | 89 |
| Mockup ORFANI (0 doc) | 19 |
| US identifier scoperti nel repo | 22 univoci (4 famiglie) |
| US-INT-* full prodotte | 5 (US-INT-2 / 3 / 4 / 5 / 6) |
| US-GAP-* compact prodotte | 17 |
| Issue umbrella attive analizzate | 11 |
| Bloccanti P0/P1 residui | 5 |
| Decisioni architetturali aperte | 4 (N4 routing, DS-16 token bridge, filename inconsistency, designer review process) |
| ADR-N riferimenti proposti | 14 (across 5 US-INT-*) |
| Cluster orfani critici | 4 (Play Records, Session game-flavored, KB-global, Nav-primitives) |

## Appendice B — File correlati (read in ordine)

1. `admin-mockups/MOCKUPS_INDEX.md` — 127 mockup canonical index
2. `admin-mockups/briefs/SP7-game-night-agent-builder.md` — US-31/33/41 brief
3. `docs/superpowers/specs/2026-06-12-us-int-1-kb-onboarding-spec.md` — template US-INT
4. `docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md` — DS-17 panel review
5. `docs/for-developers/audits/2026-05-22-mockup-gaps.md` — audit baseline
6. `docs/for-developers/workflows/us-verification-protocol.md` — US verification queue (10 legacy)
7. `docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md` — 20 invariants domain
8. `docs/for-developers/frontend/v2-migration-matrix.md` — route↔component mapping

## Appendice C — Change log

| Date | Version | Author | Change |
|---|---|---|---|
| 2026-06-14 | v1.0 | Claude Opus 4.7 (sess. /sc:spec-panel socratic) | Initial proposal post 4-agent parallel discovery + spec-panel synthesis 6 esperti (Cockburn · Wiegers · Adzic · Crispin · Fowler · Nygard) |
| 2026-06-14 | v1.1 | Claude Opus 4.7 (sess. review) | Adversarial review fixes — C-1 (added `sp4-session-codenames-live.html` to §2.3, expanded "8-15" range to explicit 8-16, renumbered 17-19), C-2 (corrected US-GAP-* count 18→17 in 5 locations + Crispin "90 stati" → "85 stati"), M-1 (added SP7 transition component-mock row + sp7-game-night-edit planned note), M-2 (added #2311 issue ref to §3.3 sp4-kb-detail), M-3 (added DEC-4 locking Fowler tension opt-b — US-GAP-SESS-* parent doppio US-INT-4+4c), M-4 (clarified "22 identifier" breakdown adding sub-Gherkin embedded row), mn-2 (renamed §2.2 "Top 10 FORTE" → "Top 10 per coverage mix FORTE+LIGHT"). Fowler synthesis "23 US in 1 file" → "22 US". |
| 2026-06-15 | v1.2 | Claude Opus 4.7 (sess. /sc:spec-panel critique umbrella #2342) | Post-merge critique fixes — CRIT-3 resolution: (1) §1.2 riga 104 — `sp7-game-night-new.html` filename brief mismatch → filename brief aligned via PR #2351; (2) §1.2 riga 105 — `sp7-game-night-edit.html` → annotato disposition #2344 pending; (3) §4b US-GAP-FILENAME-RENAME AC direzione invertita → corretta a "filesystem canonical, PR #2351 closed action"; (4) cleanup 10 file residui `sp7-game-night-create` stale refs in admin-mockups (.html `<script src>`, .jsx headers, .fidelity.json fixtures_path, design_handoff/* doc refs, MANIFEST.json). Companion umbrella v2.0 critique covers CRIT-1/2/4 + MAJ-1/2/3/4/5 (vedi umbrella #2342 body v2). |

---

🤖 Generated with [Claude Code](https://claude.com/claude-code) — `/sc:spec-panel --mode socratic`
session 2026-06-14
