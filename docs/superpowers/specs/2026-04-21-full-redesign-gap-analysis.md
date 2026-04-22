# Full Redesign v2 — Gap Analysis & Migration Roadmap

**Date**: 2026-04-21
**Owner**: redesign-v2 program
**Branch**: (tracciabile, non legato a singola branch)
**Source mockups**: `admin-mockups/design_files/`

## Goal

Mappare l'intera superficie UI non-admin dell'app MeepleAI contro i mockup Claude Design disponibili, identificare i gap, e produrre la roadmap per sostituire completamente l'interfaccia corrente con il nuovo design system.

## Scope

**In scope**:
- Tutte le route sotto `(auth)`, `(public)`, `(authenticated)`, `(chat)`
- Error/offline states globali (`error.tsx`, `not-found.tsx`, `offline/`)

**Out of scope** (decisioni utente 2026-04-21):
- Pannello `admin/*`
- Dev/internal tooling: `n8n`, `pipeline-builder`, `versions`
- `/editor` (rule editor) — classificato come dev/internal tool
- `/setup-account` — deprecato, redirect a `/onboarding` (onboarding tour copre il bisogno)
- Varianti pubbliche di `library` e `shared-games` — spostate sotto `(authenticated)`
- Backend changes

## Inventario mockup Claude Design

Mockup disponibili in `admin-mockups/design_files/`:

| File | Copertura | Stato migrazione | Referenze |
|---|---|---|---|
| `00-hub.html` | Hub design system | ✅ reference | — |
| `01-screens.html` | Screen reference mobile | ✅ reference | — |
| `02-desktop-patterns.html` | Pattern desktop (sidebar, drawer) | ✅ reference | — |
| `03-drawer-variants.html` | Varianti drawer | ✅ reference | — |
| `04-design-system.html` | Token/primitive reference | ✅ M6 | tokens.css, components.css |
| `05-dark-mode.html` | Dark mode reference | ✅ M6 Task 14 | — |
| `auth-flow.{html,jsx}` | Login/Register/Forgot/Reset/Verify + 2FA | 🔄 Fase 2.6 (spec scritta 2026-04-21) | `2026-04-21-auth-flow-v2-design.md` |
| `onboarding.{html,jsx}` | 5-step product tour | ✅ Fase 2.5 (PR #484) | `2026-04-20-onboarding-product-tour-design.md` |
| `public.{html,jsx}` | Landing / Pricing / About / Terms / Contact | 🟡 parziale (Pricing in 2.1, resto M6 Task 13) | — |
| `settings.{html,jsx}` | Profile / Account / Preferences / Notifications / API Keys / Services | ✅ Fase 2.2-2.4 | — |
| `notifications.{html,jsx}` | Feed / Detail / Empty / Filters / Settings link | ✅ M6 Task 10-11 | — |
| `mobile-app.jsx` | Home / Search / Library / Chats / Profile + entity-detail (game, player, session, agent, kb, chat, event, toolkit, tool) + drawer | 🟡 parziale — card-drawer nav merged (#396), content non completamente migrato | — |

## Matrice copertura — routes vs mockup

### Legenda stato

- ✅ **Migrato**: in produzione con v2 primitives
- 🔄 **In corso**: spec/plan attivo
- 🟡 **Parziale**: parte della route migrata
- ❌ **Gap**: nessun mockup disponibile
- 🚫 **Escluso**: fuori scope

### `(auth)/*`

| Route | Mockup | Stato |
|---|---|---|
| `/login` | `auth-flow.jsx` LoginScreen | 🔄 Fase 2.6 |
| `/register` | `auth-flow.jsx` RegisterScreen | 🔄 Fase 2.6 |
| `/reset-password` | `auth-flow.jsx` ForgotScreen + ResetScreen | 🔄 Fase 2.6 |
| `/verify-email` | `auth-flow.jsx` VerifyScreen | 🔄 Fase 2.6 |
| `/verification-pending` | `auth-flow.jsx` (envelope + resend) | 🔄 Fase 2.6 |
| `/verification-success` | — (genera da `SuccessCard`) | 🔄 Fase 2.6 (riuso primitiva) |
| `/welcome` | `onboarding.jsx` prologo | ✅ |
| `/setup-account` | — | 🚫 **deprecato** — redirect a `/onboarding` in M7 |
| `/oauth-callback` | — (pagina tecnica, no UI rilevante) | 🚫 no-UI |
| `/invitation-expired` | — (error card) | ❌ gap error-state |

### `(public)/*`

| Route | Mockup | Stato |
|---|---|---|
| `/` (landing) | `public.jsx` LandingPage | 🟡 M6 Task 13 |
| `/pricing` | `public.jsx` PricingPage | ✅ Fase 2.1 |
| `/about` | `public.jsx` AboutPage | 🟡 M6 Task 13 |
| `/terms` | `public.jsx` TermsPrivacyPage(terms) | 🟡 M6 Task 13 |
| `/privacy` | `public.jsx` TermsPrivacyPage(privacy) | 🟡 M6 Task 13 |
| `/contact` | `public.jsx` ContactPage | 🟡 M6 Task 13 |
| `/cookies` | — (separato da terms) | ❌ gap |
| `/faq` | — | ❌ gap |
| `/how-it-works` | — | ❌ gap |
| `/library` (public catalog) | — | 🚫 **escluso** (sposta sotto `(authenticated)`) |
| `/shared-games` | — | 🚫 **escluso** (sposta sotto `(authenticated)`) |
| `/accept-invite` | — | ❌ gap |
| `/join` | — | ❌ gap |

### `(authenticated)/*`

| Route | Mockup | Stato |
|---|---|---|
| `/dashboard` | `mobile-app.jsx` HomeFeed (mobile) | 🟡 navigation OK, contenuto parziale |
| `/discover` | `mobile-app.jsx` SearchScreen (mobile) | 🟡 card-drawer nav merged |
| `/library` | `mobile-app.jsx` LibraryScreen (mobile) | 🟡 card-drawer nav merged |
| `/profile` | `mobile-app.jsx` ProfileScreen (mobile) | 🟡 |
| `/settings/*` | `settings.jsx` | ✅ Fase 2.2-2.4 |
| `/notifications` | `notifications.jsx` | ✅ M6 Task 11 |
| `/onboarding` | `onboarding.jsx` | ✅ Fase 2.5 |
| `/games/[id]` | `mobile-app.jsx` EntityDetail(game) | 🟡 GameDetailDesktop esiste, allineamento v2 parziale |
| `/sessions/[id]` | `mobile-app.jsx` EntityDetail(session) | 🟡 session-mode navigation merged (#396) |
| `/agents/[id]` | `mobile-app.jsx` EntityDetail(agent) | 🟡 |
| `/knowledge-base/[id]` | `mobile-app.jsx` EntityDetail(kb) | 🟡 |
| `/players/[id]` | `mobile-app.jsx` EntityDetail(player) | 🟡 |
| `/game-nights/*` | — (solo entity `event` accennata) | ❌ **P0 gap** — feature core non mockup-ata |
| `/toolkit/*` | — (tool/toolkit entity presenti ma no workflow) | ❌ **P0 gap** |
| `/play-records` | — | ❌ **P1 gap** |
| `/upload` | — | ❌ **P1 gap** |
| `/private-games/*` | — | ❌ **P1 gap** |
| `/editor` | — | 🚫 **escluso** (dev/internal tool) |
| `/setup` | — | 🚫 **escluso** (deprecato, redirect a `/onboarding`) |
| `/n8n`, `/pipeline-builder`, `/versions` | — | 🚫 escluso (dev tooling) |

### `(chat)/*`

| Route | Mockup | Stato |
|---|---|---|
| `/chat` | `mobile-app.jsx` ChatsScreen | 🟡 nav merged, contenuto parziale |

### Globali

| File | Mockup | Stato |
|---|---|---|
| `error.tsx` | — | ❌ gap error-state |
| `not-found.tsx` | — | ❌ gap error-state |
| `offline/page.tsx` | — | ❌ gap |
| `health/page.tsx` | — | 🚫 (pagina tecnica) |
| `metrics/page.tsx` | — | 🚫 (pagina tecnica) |

## Gap analysis — mockup da richiedere a Claude Design

Priorità basate su: (a) centralità feature per proposta valore, (b) volume utenti impattati, (c) prerequisiti per altre migrazioni.

### P0 — Bloccanti per sostituzione completa

1. **`desktop-app-shell`** — App desktop full shell:
   - **Approccio scelto (2026-04-21)**: evolvere `02-desktop-patterns.html` esistente, non richiedere nuovo mockup
   - Sidebar navigation con recents
   - Drawer stack (max 3, card-drawer nav già live in #396)
   - Breadcrumb/MiniNav desktop
   - Dashboard desktop (non solo mobile HomeFeed)
   - Integration ConnectionBar per entity detail desktop

2. **`game-night.{html,jsx}`** — Game Night flow end-to-end:
   - Creazione evento (data, invitati, giochi selezionati)
   - Orchestrazione multi-gioco (PlayOrder, transizioni)
   - Diary live con SSE events (GameStartedInNight, GameCompleted, NightFinalized)
   - Summary card post-serata
   - Auto-save 60s indicator
   - Cross-game timeline

3. **`toolkit-workflow.{html,jsx}`** — Toolkit completo:
   - Dice (d4/d6/d8/d10/d12/d20, multi-roll, history)
   - Timer (countdown, turn timer, pause/resume)
   - Counter (score tracker multi-player, step +/-)
   - Card deck manager (shuffle, draw, discard, reveal)
   - Session mode integration (pin/unpin tool in sessione attiva)

### P1 — Feature importanti scoperte

4. **`play-records.{html,jsx}`** — Diario cross-game:
   - Lista sessioni con filtri (gioco, data, esito)
   - Timeline cross-game
   - Detail drawer per sessione (riuso EntityDetail session)
   - Stats aggregate (ore giocate, giochi preferiti, co-giocatori)

5. **`upload-flow.{html,jsx}`** — PDF upload + processing:
   - Drag&drop + file picker
   - Progress bar upload
   - Stato processing (extraction, chunking, indexing)
   - Anteprima estrazione con approve/reject
   - Errori estrazione con retry

6. **`private-games.{html,jsx}`** — Private games management:
   - Lista PDF caricati (stato processing, dimensione, pagine)
   - Visibility toggle (private/shared)
   - Delete con conferma

### P2 — Edge/marketing

7. **`public-extras.{html,jsx}`** — Pagine pubbliche residue:
   - FAQ (accordion)
   - How-it-works (step-by-step con entity tokens)
   - Cookies (separato da Terms)
   - Accept-invite (landing + CTA)
   - Join (waitlist/invite-only)
   - NOTE: `library` e `shared-games` spostate sotto `(authenticated)`, non incluse qui

8. **`error-states.{html,jsx}`** — Error/offline/404:
   - 404 Not Found con CTA home
   - 500 Server Error con retry
   - Offline (con service worker integration)
   - Maintenance mode
   - Invitation expired

## Roadmap milestones

Ordering confermato (2026-04-21): **M7 Desktop shell → M8 Game Night → M9 Toolkit → M10 P1 bundle → M11 P2 bundle**.

### Milestone M7 — Desktop shell (P0, FIRST)
- Input: estensione di `02-desktop-patterns.html` esistente (no nuovo mockup richiesto)
- Rationale: sblocca pattern desktop per tutte le feature successive, evita doppia passata su M8/M9
- Spec + Plan + subagent-driven-development
- Branch: `refactor/redesign-v2-m7`
- Include: deprecazione `/setup-account` con redirect a `/onboarding`, spostamento `library`/`shared-games` sotto `(authenticated)`

### Milestone M8 — Game Night (P0)
- Prerequisito: mockup `game-night.{html,jsx}` da Claude Design
- Branch: `refactor/redesign-v2-m8`

### Milestone M9 — Toolkit (P0)
- Prerequisito: mockup `toolkit-workflow.{html,jsx}` da Claude Design
- Rationale: dopo Game Night perché Toolkit si integra in session mode già migrato
- Branch: `refactor/redesign-v2-m9`

### Milestone M10 — Play records + Upload + Private games (P1 bundle)
- Prerequisiti: mockup `play-records`, `upload-flow`, `private-games`
- Branch: `refactor/redesign-v2-m10`

### Milestone M11 — Public extras + Error states (P2 bundle)
- Prerequisiti: mockup `public-extras`, `error-states`
- Branch: `refactor/redesign-v2-m11`

## Processo per ogni milestone

Pattern già collaudato con Fase 2.1-2.6:

1. Ricezione mockup da Claude Design
2. `superpowers:brainstorming` → spec design
3. `superpowers:writing-plans` → plan TDD task-by-task
4. `superpowers:subagent-driven-development` → implementer + spec reviewer + code reviewer per task
5. Bundle baseline update
6. PR a branch madre (redesign-v2) con squash merge
7. Task tracking in GitHub issues + memoria locale

## Criteri di success globale

- Ogni route non-admin usa almeno un primitivo v2 (`AuthCard`, `Btn`, `Drawer`, `EntityCard`, `StepProgress`, etc.)
- Legacy components eliminati: `OAuthButtons` (post-Fase 2.6), `GameCard`/`PlayerCard` deprecated wrappers, shadcn `Tabs` in AuthModal
- Zero regressioni sui test esistenti (930+ backend, 900+ frontend)
- Bundle baseline aggiornato a ogni milestone
- Lighthouse a11y ≥ 95 su tutte le route nuove
- Dark mode coerente via `hsl(var(--c-*))` entity tokens

## Decisioni risolte (2026-04-21)

Tutte le decisioni architetturali chiarite in sessione:

1. **Desktop shell**: evolvere `02-desktop-patterns.html` esistente — no nuovo mockup richiesto a Claude Design.
2. **Rule editor (`/editor`)**: escluso come dev/internal tool (fuori scope come `n8n`/`pipeline-builder`).
3. **`/setup-account`**: deprecato — redirect a `/onboarding` in M7 (onboarding tour Fase 2.5 copre il bisogno).
4. **Public `library` + `shared-games`**: spostate sotto `(authenticated)` — non più pubbliche pre-auth.
5. **Ordering milestone**: M7 Desktop shell → M8 Game Night → M9 Toolkit → M10 P1 → M11 P2, per sbloccare pattern desktop prima delle feature che lo consumano.

## Out of scope (riconferma)

- Pannello `admin/*`
- Dev tooling: `n8n`, `pipeline-builder`, `versions`
- Backend changes
- New E2E/visual regression infra (riuso esistente Playwright + bundle baseline)
- Email template updates
- Mobile nativo (app web mobile-first è sufficiente)

## Related

- Fase attiva: `docs/superpowers/specs/2026-04-21-auth-flow-v2-design.md` (Fase 2.6)
- Precedenti: `docs/superpowers/specs/2026-04-20-onboarding-product-tour-design.md` (Fase 2.5)
- M6 foundation: `docs/superpowers/plans/2026-04-20-m6-migration-notes.md`
- Mockup source: `admin-mockups/design_files/`
- Tracking: GitHub PR #484 (redesign-v2-m6), futuri M7-M12
